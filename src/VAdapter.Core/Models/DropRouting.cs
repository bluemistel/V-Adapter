using System.Text.RegularExpressions;

namespace VAdapter.Core.Models;

/// <summary>ファイル名から話者・レイヤーを解決するルーティングロジック。</summary>
public static class DropRouting
{
    public readonly record struct Route(string? Speaker, int Layer, SpeakerRule? MatchedRule);

    /// <summary>
    /// ファイル名（拡張子含む・ディレクトリ除く）を話者ルールに照合し、投入レイヤーを決定する。
    /// 有効なルールを先頭から評価し、最初に一致したものを採用。未一致なら既定レイヤー。
    /// <para>
    /// 一致の判定:
    /// ①正規表現が一致すること。②ルールに話者名が設定されていて、かつパターンが話者名を
    /// キャプチャする（( ) を含む）場合は、抽出した話者名がルールの話者名と一致すること。
    /// ②により、既定パターン（どの話者にも一致する）を複数行に並べて話者ごとに
    /// レイヤーを振り分けられる。パターン自体が特定話者専用（キャプチャ無し）の場合、
    /// 話者名は表示用ラベルとして扱う（従来どおり）。
    /// </para>
    /// </summary>
    public static Route Resolve(string fileName, AviutlDropConfig config)
    {
        foreach (var rule in config.Rules)
        {
            if (!rule.Enabled || string.IsNullOrEmpty(rule.NamePattern))
                continue;

            Match match;
            try
            {
                match = Regex.Match(fileName, rule.NamePattern, RegexOptions.IgnoreCase);
            }
            catch (ArgumentException)
            {
                continue; // 不正な正規表現はスキップ。
            }

            if (!match.Success)
                continue;

            // パターンが話者名をキャプチャしていれば、その値を実際の話者名とみなす。
            var captured = match.Groups.Count > 1 && match.Groups[1].Success
                ? match.Groups[1].Value
                : null;

            // 話者名が設定されたルールは、その話者のファイルにだけ適用する。
            // （既定パターンはどの話者にも一致するため、これが無いと先頭行が全部を拾ってしまう）
            if (!IsBlank(rule.SpeakerName) && captured is not null
                && !string.Equals(captured.Trim(), rule.SpeakerName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // 話者名: ルールの SpeakerName 優先、無ければキャプチャグループ1を利用。
            var speaker = !IsBlank(rule.SpeakerName) ? rule.SpeakerName : captured;

            return new Route(speaker, rule.Layer, rule);
        }

        return new Route(null, config.DefaultLayer, null);
    }

    /// <summary>
    /// ファイル名から話者名だけを抽出する（レイヤー振り分けは行わない）。
    ///
    /// 話者ルールは AviUtl 系の投げ込み設定に属するため、マクロ動作ベース（YMM4 等）では存在しない。
    /// 一方でファイル名の付き方（<c>01_ついなちゃん_テストです</c> 等）はどの連携環境でも同じなので、
    /// 後処理の改名・話者サブフォルダではこちらを使い、環境に依らず話者名を扱えるようにする。
    /// </summary>
    /// <param name="fileName">ファイル名（拡張子含む・ディレクトリ除く）。</param>
    /// <param name="pattern">抽出パターン。空なら <see cref="SpeakerRule.DefaultNamePattern"/>。</param>
    /// <returns>グループ1で捕捉した話者名。抽出できなければ null。</returns>
    public static string? ExtractSpeaker(string fileName, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var effective = string.IsNullOrWhiteSpace(pattern) ? SpeakerRule.DefaultNamePattern : pattern;

        Match match;
        try
        {
            match = Regex.Match(fileName, effective, RegexOptions.IgnoreCase);
        }
        catch (ArgumentException)
        {
            return null; // 不正な正規表現は「抽出できなかった」扱いにする。
        }

        if (!match.Success || match.Groups.Count <= 1 || !match.Groups[1].Success)
            return null;

        var speaker = match.Groups[1].Value.Trim();
        return speaker.Length == 0 ? null : speaker;
    }

    private static bool IsBlank(string? s) => string.IsNullOrWhiteSpace(s);
}
