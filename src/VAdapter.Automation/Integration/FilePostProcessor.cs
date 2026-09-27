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
    /// <param name="AudioPath">後処理後の音声パス（移動しなかった場合は元のまま）。</param>
    /// <param name="SubtitlePath">後処理後の字幕パス（元が無ければ null）。</param>
    /// <param name="ProducedAudioPaths">この処理で新たに出現した音声パス（監視の再検知を防ぐ対象）。</param>
    /// <param name="Messages">実行ログへ出す説明。</param>
    public sealed record Result(
        string AudioPath,
        string? SubtitlePath,
        IReadOnlyList<string> ProducedAudioPaths,
        IReadOnlyList<string> Messages);

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

        var plan = FilePostProcessPlanner.Create(audioPath, speaker, options, DateTime.Now);

        var audioExt = Path.GetExtension(audioPath);
        var subtitleExt = subtitlePath is null ? null : Path.GetExtension(subtitlePath);

        var finalAudio = audioPath;
        var finalSubtitle = subtitlePath;

        // ── 移動・改名 ─────────────────────────────────────────────
        var sourceDir = Path.GetDirectoryName(audioPath) ?? string.Empty;
        var sameDir = PathsEqual(sourceDir, plan.TargetDirectory);
        var sameName = string.Equals(
            Path.GetFileNameWithoutExtension(audioPath), plan.BaseName, StringComparison.Ordinal);

        if (!sameDir || !sameName)
        {
            try
            {
                Directory.CreateDirectory(plan.TargetDirectory);
                var baseName = ResolveCollision(plan.TargetDirectory, plan.BaseName, audioExt, subtitleExt);

                var destAudio = Path.Combine(plan.TargetDirectory, baseName + audioExt);
                // 字幕を先に置く。音声の出現をきっかけに取り込む編集ソフトがあるため、
                // 音声が現れた時点で字幕が揃っている順序にする。
                if (subtitlePath is not null && File.Exists(subtitlePath))
                {
                    var destSubtitle = Path.Combine(plan.TargetDirectory, baseName + subtitleExt);
                    File.Move(subtitlePath, destSubtitle);
                    finalSubtitle = destSubtitle;
                }

                File.Move(audioPath, destAudio);
                finalAudio = destAudio;
                produced.Add(destAudio);
                messages.Add($"後処理: {Path.GetFileName(destAudio)} → {plan.TargetDirectory}");
            }
            catch (Exception ex)
            {
                messages.Add($"後処理の移動に失敗（元の場所のまま続行）: {ex.Message}");
                finalAudio = audioPath;
                finalSubtitle = subtitlePath;
            }
        }

        return new Result(finalAudio, finalSubtitle, produced, messages);
    }

    /// <summary>
    /// 既存ファイルとぶつかる場合に連番を付ける。wav と txt が同名であり続けるよう、
    /// どちらか一方でも存在する番号は飛ばす。
    /// </summary>
    private static string ResolveCollision(string folder, string baseName, string audioExt, string? subtitleExt)
    {
        if (!Occupied(folder, baseName, audioExt, subtitleExt))
            return baseName;

        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{baseName}_{i}";
            if (!Occupied(folder, candidate, audioExt, subtitleExt))
                return candidate;
        }
        return $"{baseName}_{Guid.NewGuid():N}";
    }

    private static bool Occupied(string folder, string baseName, string audioExt, string? subtitleExt)
    {
        if (File.Exists(Path.Combine(folder, baseName + audioExt)))
            return true;
        return subtitleExt is not null && File.Exists(Path.Combine(folder, baseName + subtitleExt));
    }

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
