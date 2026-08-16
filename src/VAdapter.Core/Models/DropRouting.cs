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

    private static bool IsBlank(string? s) => string.IsNullOrWhiteSpace(s);
}
