using System.IO;
using VAdapter.Core.Media;
using VAdapter.Core.Models;

namespace VAdapter.Automation.Integration;

/// <summary>
/// 後処理の実行（改名・移動）。計画は <see cref="FilePostProcessPlanner"/> が立て、
/// ここではファイル操作と名前衝突の解決だけを行う。
/// </summary>
public static class FilePostProcessor
{
    /// <summary>
    /// 音声より後に出力された付随ファイル（.lab / .srt 等）を追いかけて移動する時間。
    /// 出力される種類も順番もソフト・設定で変わるため、待つのではなく「現れたら移動」にしている。
    /// </summary>
    public static readonly TimeSpan CompanionFollowWindow = TimeSpan.FromSeconds(5);

    private const int CompanionPollIntervalMs = 200;

    /// <param name="AudioPath">後処理後の音声パス（移動しなかった場合は元のまま）。</param>
    /// <param name="SubtitlePath">後処理後の字幕パス（元が無ければ null）。</param>
    /// <param name="ProducedAudioPaths">この処理で新たに出現した音声パス（監視の再検知を防ぐ対象）。</param>
    /// <param name="Messages">実行ログへ出す説明。</param>
    /// <param name="PendingCompanions">まだ現れていない付随ファイル（後から追いかけて移動する対象）。無ければ null。</param>
    public sealed record Result(
        string AudioPath,
        string? SubtitlePath,
        IReadOnlyList<string> ProducedAudioPaths,
        IReadOnlyList<string> Messages,
        PendingCompanions? PendingCompanions = null);

    /// <summary>後から現れる付随ファイルの移動元（元の名前）と移動先（新しい名前）。</summary>
    public sealed record PendingCompanions(
        string SourceDirectory,
        string SourceBaseName,
        string TargetDirectory,
        string TargetBaseName,
        IReadOnlyList<string> Extensions);

    /// <summary>
    /// 音声・字幕のペアに後処理を適用する。設定が無効なら何もせず入力をそのまま返す。
    /// 途中で失敗しても、投げ込み自体は継続できるよう元のパスへフォールバックする。
    /// </summary>
    public static Result Run(string audioPath, string? subtitlePath, string? speaker, FilePostProcessOptions options)
    {
        if (!options.Enabled)
            return new Result(audioPath, subtitlePath, Array.Empty<string>(), Array.Empty<string>());

        var messages = new List<string>();
        var produced = new List<string>();
        PendingCompanions? pending = null;

        var plan = FilePostProcessPlanner.Create(audioPath, speaker, options, DateTime.Now);

        var audioExt = Path.GetExtension(audioPath);
        var subtitleExt = subtitlePath is null ? null : Path.GetExtension(subtitlePath);
        var companionExts = options.CompanionExtensions();

        var finalAudio = audioPath;
        var finalSubtitle = subtitlePath;

        // ── 移動・改名 ─────────────────────────────────────────────
        var sourceDir = Path.GetDirectoryName(audioPath) ?? string.Empty;
        var sourceBase = Path.GetFileNameWithoutExtension(audioPath);
        var sameDir = PathsEqual(sourceDir, plan.TargetDirectory);
        var sameName = string.Equals(sourceBase, plan.BaseName, StringComparison.Ordinal);

        if (!sameDir || !sameName)
        {
            try
            {
                Directory.CreateDirectory(plan.TargetDirectory);

                // 付随ファイルは後から現れることもあるため、出力の有無に関わらず連番の判定に含める
                // （後から来たものが既存ファイルとぶつからず、音声と同じ名前になるように）。
                var exts = new List<string> { audioExt };
                if (subtitleExt is not null) exts.Add(subtitleExt);
                exts.AddRange(companionExts);
                var baseName = ResolveCollision(plan.TargetDirectory, plan.BaseName, exts);

                var destAudio = Path.Combine(plan.TargetDirectory, baseName + audioExt);
                // 字幕・付随ファイルを先に置く。音声の出現をきっかけに取り込む編集ソフトがあるため、
                // 音声が現れた時点でほかが揃っている順序にする。
                if (subtitlePath is not null && File.Exists(subtitlePath))
                {
                    var destSubtitle = Path.Combine(plan.TargetDirectory, baseName + subtitleExt);
                    File.Move(subtitlePath, destSubtitle);
                    finalSubtitle = destSubtitle;
                }

                var notYet = new List<string>();
                foreach (var ext in companionExts)
                {
                    var src = Path.Combine(sourceDir, sourceBase + ext);
                    if (!TryMoveCompanion(src, Path.Combine(plan.TargetDirectory, baseName + ext)))
                        notYet.Add(ext); // まだ無い・書き込み中 → 後から追いかける。
                }

                File.Move(audioPath, destAudio);
                finalAudio = destAudio;
                produced.Add(destAudio);
                messages.Add($"後処理: {Path.GetFileName(destAudio)} → {plan.TargetDirectory}");

                if (notYet.Count > 0)
                    pending = new PendingCompanions(sourceDir, sourceBase, plan.TargetDirectory, baseName, notYet);
            }
            catch (Exception ex)
            {
                messages.Add($"後処理の移動に失敗（元の場所のまま続行）: {ex.Message}");
                finalAudio = audioPath;
                finalSubtitle = subtitlePath;
                pending = null;
            }
        }

        return new Result(finalAudio, finalSubtitle, produced, messages, pending);
    }

    /// <summary>
    /// 音声より後に現れた付随ファイルを、音声と同じ移動先・名前へ移動する。
    /// <see cref="CompanionFollowWindow"/> の間だけ見張り、現れなかったものは出力されない設定とみなして何もしない。
    /// </summary>
    public static async Task FollowCompanionsAsync(
        PendingCompanions pending, TimeSpan window, Action<string>? log, CancellationToken ct = default)
    {
        var remaining = new List<string>(pending.Extensions);
        var deadline = DateTime.UtcNow + window;

        while (remaining.Count > 0 && DateTime.UtcNow < deadline)
        {
            try { await Task.Delay(CompanionPollIntervalMs, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }

            foreach (var ext in remaining.ToList())
            {
                var src = Path.Combine(pending.SourceDirectory, pending.SourceBaseName + ext);
                var dest = Path.Combine(pending.TargetDirectory, pending.TargetBaseName + ext);
                if (TryMoveCompanion(src, dest))
                {
                    remaining.Remove(ext);
                    log?.Invoke($"後処理: {Path.GetFileName(dest)} → {pending.TargetDirectory}");
                }
            }
        }

        // 現れたのに動かせなかったもの（書き込みが続いている・移動先に同名がある等）だけ知らせる。
        foreach (var ext in remaining)
        {
            var src = Path.Combine(pending.SourceDirectory, pending.SourceBaseName + ext);
            if (File.Exists(src))
                log?.Invoke($"後処理: {Path.GetFileName(src)} を移動できなかったため、元の場所に残しています。");
        }
    }

    /// <summary>付随ファイルがあれば移動する。無い・使用中・移動先に同名がある場合は false。</summary>
    private static bool TryMoveCompanion(string source, string dest)
    {
        if (!File.Exists(source) || File.Exists(dest))
            return false;
        try
        {
            File.Move(source, dest);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false; // 合成音声ソフトが書き込み中。次の確認で再試行する。
        }
    }

    /// <summary>
    /// 既存ファイルとぶつかる場合に連番を付ける。音声・字幕・付随ファイルが同名であり続けるよう、
    /// どれか 1 つでも存在する番号は飛ばす。
    /// </summary>
    private static string ResolveCollision(string folder, string baseName, IReadOnlyList<string> exts)
    {
        if (!Occupied(folder, baseName, exts))
            return baseName;

        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{baseName}_{i}";
            if (!Occupied(folder, candidate, exts))
                return candidate;
        }
        return $"{baseName}_{Guid.NewGuid():N}";
    }

    private static bool Occupied(string folder, string baseName, IReadOnlyList<string> exts) =>
        exts.Any(ext => File.Exists(Path.Combine(folder, baseName + ext)));

    private static bool PathsEqual(string a, string b)
    {
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}
