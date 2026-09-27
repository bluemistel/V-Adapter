using System.IO;
using System.Text.RegularExpressions;
using VAdapter.Core.Models;

namespace VAdapter.Core.Media;

/// <summary>
/// 後処理で「どこへ・どんな名前で置くか」を決める純粋ロジック（ファイル操作は行わない）。
/// 実際の移動・複製は実行側が担い、名前衝突の解決もそちらで行う。
/// </summary>
public static class FilePostProcessPlanner
{
    /// <summary>後処理の結果として置きたい場所と名前。</summary>
    /// <param name="TargetDirectory">音声・字幕を置くフォルダ。</param>
    /// <param name="BaseName">拡張子を除いたファイル名（wav / txt で共通）。</param>
    public sealed record Plan(
        string TargetDirectory,
        string BaseName);

    /// <summary>
    /// 後処理の計画を立てる。
    /// </summary>
    /// <param name="audioPath">保存された音声のフルパス。</param>
    /// <param name="speaker">解決済みの話者名（未判定なら null）。話者サブフォルダと <c>{speaker}</c> に使う。</param>
    /// <param name="options">後処理設定。</param>
    /// <param name="now">現在時刻（<c>{date}</c> の解決に使う）。</param>
    public static Plan Create(string audioPath, string? speaker, FilePostProcessOptions options, DateTime now)
    {
        var sourceDir = Path.GetDirectoryName(audioPath) ?? string.Empty;
        var originalName = Path.GetFileNameWithoutExtension(audioPath);

        // 移動先。未指定なら保存された場所に置いたままにする。
        var baseDir = string.IsNullOrWhiteSpace(options.DestinationFolder)
            ? sourceDir
            : options.DestinationFolder!.Trim();

        // 話者サブフォルダ。話者が判定できなかった場合は作らない（"(不明)" を作らない）。
        var speakerName = string.IsNullOrWhiteSpace(speaker) ? null : speaker!.Trim();
        var targetDir = options.SpeakerSubfolder && speakerName is not null
            ? Path.Combine(baseDir, SaveNameComposer.Sanitize(speakerName))
            : baseDir;

        var baseName = ResolveName(originalName, speakerName, options.NameTemplate, now);

        return new Plan(targetDir, baseName);
    }

    /// <summary>
    /// テンプレートを解決してファイル名（拡張子なし）を作る。空テンプレートなら元の名前のまま。
    /// <c>{speaker}</c> は話者ルールを持たない連携環境では空になるため、
    /// 空になったトークンは隣接する区切り文字ごと畳んで <c>「__」</c> や末尾の <c>「_」</c> を残さない。
    /// </summary>
    public static string ResolveName(string originalName, string? speaker, string template, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(template))
            return originalName;

        var speakerName = string.IsNullOrWhiteSpace(speaker) ? string.Empty : speaker!.Trim();

        // テンプレートが日時を付けるなら、元の名前が既に持っている日時は落とす。
        // VOICEROID2 は V-Adapter 側が「日時_話者_本文」で保存するため、
        // そのまま連結すると「20260927_102810_20260927_102810_話者_本文」と二重になる。
        var baseName = TemplateHasDate(template) ? StripLeadingTimestamp(originalName) : originalName;

        var collapsed = CollapseEmptyTokens(template, baseName, speakerName);

        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = baseName,
            ["speaker"] = speakerName,
        };

        var resolved = SaveNameComposer.ResolveName(collapsed, variables, now);

        // テンプレートの解決に失敗して空になった場合は、元の名前を失わないようにする。
        return string.IsNullOrWhiteSpace(resolved) ? originalName : resolved;
    }

    /// <summary>区切りとして扱う文字（この 1 個だけを空トークンと一緒に落とす）。</summary>
    private const string Separators = "_-  ";

    /// <summary>テンプレートに <c>{date}</c>（書式指定を含む）が含まれるか。</summary>
    private static readonly Regex DateTokenPattern =
        new(@"\{date(?::[^{}]*)?\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// 先頭の <c>yyyyMMdd_HHmmss_</c>。V-Adapter が
    /// <see cref="SaveNameComposer.DefaultTemplate"/> で付ける日時の形式に合わせている。
    /// </summary>
    private static readonly Regex LeadingTimestampPattern =
        new(@"^\d{8}_\d{6}_", RegexOptions.Compiled);

    private static bool TemplateHasDate(string template) => DateTokenPattern.IsMatch(template);

    /// <summary>
    /// 先頭に付いている日時を取り除く。取り除くと名前が空になる場合は元のまま返す
    /// （日時だけのファイル名を消してしまわないため）。
    /// </summary>
    private static string StripLeadingTimestamp(string name)
    {
        var stripped = LeadingTimestampPattern.Replace(name, string.Empty, 1);
        return string.IsNullOrWhiteSpace(stripped) ? name : stripped;
    }

    /// <summary>
    /// 値が空になるトークンを、隣接する区切り文字 1 個ごとテンプレートから取り除く。
    /// 後続の区切りを優先し、末尾トークンなら直前の区切りを落とす。
    /// 日時トークン（<c>{date}</c>）は常に値を持つため対象外。
    /// </summary>
    private static string CollapseEmptyTokens(string template, string originalName, string speakerName)
    {
        var empty = new List<string>();
        if (string.IsNullOrEmpty(speakerName))
            empty.Add("speaker");
        if (string.IsNullOrEmpty(originalName))
            empty.Add("name");
        if (empty.Count == 0)
            return template;

        var result = template;
        foreach (var token in empty)
        {
            // 書式指定（{speaker:5} 等）も同じトークンとして扱う。
            var pattern = @"\{" + token + @"(?::[^{}]*)?\}";
            result = Regex.Replace(result, pattern + "(.?)", m =>
            {
                var next = m.Groups[1].Value;
                // 直後が区切りなら、それも一緒に落とす（{date}_{speaker}_{name} → {date}_{name}）。
                return next.Length == 1 && Separators.Contains(next[0]) ? string.Empty : next;
            });
        }

        // 末尾トークンだった場合に取り残される区切りを落とす（{name}_{speaker} → {name}）。
        return result.Trim(Separators.ToCharArray());
    }
}
