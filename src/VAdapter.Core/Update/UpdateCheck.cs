using System.Text.RegularExpressions;

namespace VAdapter.Core.Update;

/// <summary>
/// GitHub のタグ名からバージョンを解釈し、現在のアプリ版と比較する純粋ロジック。
/// タグは <c>v0.0.4</c> / <c>0.0.4</c> / <c>v0.0.4-alpha</c> 等を受け付ける。
/// </summary>
public static class UpdateCheck
{
    // 先頭の "v" を許容し、数字.数字[.数字[.数字]] を取り出す（後続の -alpha 等は無視）。
    private static readonly Regex TagPattern = new(@"^\s*v?(\d+(?:\.\d+){1,3})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>タグ名をバージョンへ変換する。解釈できなければ null。</summary>
    public static Version? ParseTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;
        var m = TagPattern.Match(tag);
        if (!m.Success)
            return null;
        return Version.TryParse(m.Groups[1].Value, out var v) ? Normalize(v) : null;
    }

    /// <summary>タグ名の集合から最新（最大）バージョンを返す。1つも解釈できなければ null。</summary>
    public static Version? SelectLatest(IEnumerable<string?> tags)
    {
        Version? latest = null;
        foreach (var t in tags)
        {
            var v = ParseTag(t);
            if (v is not null && (latest is null || v > latest))
                latest = v;
        }
        return latest;
    }

    /// <summary>
    /// 更新が必要か（<paramref name="latest"/> が <paramref name="current"/> より新しいか）。
    /// リビジョン差は無視して major.minor.build で比較する。
    /// </summary>
    public static bool IsUpdateAvailable(Version current, Version? latest)
    {
        if (latest is null)
            return false;
        return Normalize(latest) > Normalize(current);
    }

    /// <summary>欠けた要素を 0 に、リビジョンを無視して major.minor.build に正規化する。</summary>
    private static Version Normalize(Version v) =>
        new(Math.Max(0, v.Major), Math.Max(0, v.Minor), Math.Max(0, v.Build));
}
