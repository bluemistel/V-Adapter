using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace VAdapter.Core.Media;

/// <summary>
/// 保存ファイル名テンプレートを解決する純粋ロジック（VOICEROID2 等の名前付き保存で使用）。
/// トークン: <c>{date}</c> / <c>{date:yyyyMMdd}</c>（現在時刻）、<c>{変数名}</c>、
/// <c>{変数名:N}</c>（値の先頭 N 文字）。ファイル名として不正な文字はサニタイズする。
/// </summary>
public static class SaveNameComposer
{
    /// <summary>既定の日時書式。</summary>
    public const string DefaultDateFormat = "yyyyMMdd_HHmmss";

    /// <summary>既定のファイル名テンプレート（現在日時＋キャラクター名＋本文先頭10文字）。</summary>
    public const string DefaultTemplate = "{date}_{character}_{serifu:10}";

    private static readonly Regex TokenPattern = new(@"\{([^{}:]+)(?::([^{}]+))?\}", RegexOptions.Compiled);

    /// <summary>テンプレートを変数と現在時刻で解決し、サニタイズしたファイル名（拡張子なし）を返す。</summary>
    public static string ResolveName(string template, IReadOnlyDictionary<string, string> variables, DateTime now)
    {
        if (string.IsNullOrEmpty(template))
            template = DefaultTemplate;

        var resolved = TokenPattern.Replace(template, m =>
        {
            var key = m.Groups[1].Value.Trim();
            var arg = m.Groups[2].Success ? m.Groups[2].Value.Trim() : null;

            if (string.Equals(key, "date", StringComparison.OrdinalIgnoreCase))
            {
                var fmt = string.IsNullOrEmpty(arg) ? DefaultDateFormat : arg;
                try { return now.ToString(fmt, CultureInfo.InvariantCulture); }
                catch (FormatException) { return now.ToString(DefaultDateFormat, CultureInfo.InvariantCulture); }
            }

            var value = variables.TryGetValue(key, out var v) ? v ?? string.Empty : string.Empty;
            // {var:N} は先頭 N 文字。
            if (!string.IsNullOrEmpty(arg) && int.TryParse(arg, out var n) && n >= 0 && value.Length > n)
                value = value[..n];
            return value;
        });

        return Sanitize(resolved);
    }

    /// <summary>解決したファイル名にフォルダ・拡張子を付けてフルパスを返す。</summary>
    public static string ResolvePath(
        string folder, string template, IReadOnlyDictionary<string, string> variables, DateTime now, string extension)
    {
        var name = ResolveName(template, variables, now);
        if (string.IsNullOrEmpty(name))
            name = ResolveName(DefaultTemplate, variables, now);
        var ext = NormalizeExtension(extension);
        return Path.Combine(folder, name + ext);
    }

    /// <summary>ファイル名として不正な文字を "_" に置換し、前後空白・改行を除去する。</summary>
    public static string Sanitize(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (c == '\r' || c == '\n' || c == '\t')
                sb.Append('_');
            else
                sb.Append(invalid.Contains(c) ? '_' : c);
        }
        return sb.ToString().Trim().Trim('.');
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
            return string.Empty;
        return extension.StartsWith('.') ? extension : "." + extension;
    }
}
