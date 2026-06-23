using System.Text;

namespace VAdapter.Automation.Ocr;

/// <summary>
/// OCR 結果と検索語の一致判定。日本語 OCR は空白の混入や全角/半角の揺れが多いため、
/// 空白除去 + 正規化（NFKC）してから部分一致を判定する。
/// </summary>
public static class OcrTextMatcher
{
    public static bool Contains(string? haystack, string? needle)
    {
        if (string.IsNullOrEmpty(needle))
            return false;
        var h = Normalize(haystack);
        var n = Normalize(needle);
        return n.Length > 0 && h.Contains(n, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string? s)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;

        s = s.Normalize(NormalizationForm.FormKC); // 全角/半角・互換文字を統一
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (!char.IsWhiteSpace(ch))
                sb.Append(ch);
        }
        return sb.ToString();
    }
}
