using System.Text.RegularExpressions;

namespace VAdapter.Core.Models;

/// <summary>ファイル名から話者・レイヤーを解決するルーティングロジック。</summary>
public static class DropRouting
{
    public readonly record struct Route(string? Speaker, int Layer, SpeakerRule? MatchedRule);

    /// <summary>
    /// ファイル名（拡張子含む・ディレクトリ除く）を話者ルールに照合し、投入レイヤーを決定する。
    /// 有効なルールを先頭から評価し、最初に正規表現が一致したものを採用。未一致なら既定レイヤー。
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

            // 話者名: ルールの SpeakerName 優先、無ければキャプチャグループ1を利用。
            var speaker = !string.IsNullOrEmpty(rule.SpeakerName)
                ? rule.SpeakerName
                : (match.Groups.Count > 1 && match.Groups[1].Success ? match.Groups[1].Value : null);

            return new Route(speaker, rule.Layer, rule);
        }

        return new Route(null, config.DefaultLayer, null);
    }
}
