namespace VAdapter.Core.Integration;

/// <summary>
/// 外部アダプタのコマンドテンプレートを、実行ファイル名と引数へ展開する純粋ヘルパ。
/// テンプレート中の <c>{payload}</c> を payload.json のパスへ置換する。
/// </summary>
public static class ExternalCommandLine
{
    public const string PayloadPlaceholder = "{payload}";

    /// <summary>
    /// テンプレートを (実行ファイル, 引数) に展開する。
    /// 先頭トークンを実行ファイルとして取り出す（ダブルクオート対応）。
    /// <c>{payload}</c> はパスへ置換し、スペースを含む場合はクオートする。
    /// </summary>
    public static (string FileName, string Arguments) Build(string template, string payloadPath)
    {
        var quoted = payloadPath.Contains(' ') && !payloadPath.StartsWith('"')
            ? $"\"{payloadPath}\""
            : payloadPath;
        var expanded = (template ?? string.Empty).Replace(PayloadPlaceholder, quoted).TrimStart();

        if (expanded.Length == 0)
            return (string.Empty, string.Empty);

        if (expanded[0] == '"')
        {
            var end = expanded.IndexOf('"', 1);
            if (end < 0)
                return (expanded.Trim('"'), string.Empty);
            var file = expanded.Substring(1, end - 1);
            var args = expanded[(end + 1)..].TrimStart();
            return (file, args);
        }

        var sp = expanded.IndexOf(' ');
        return sp < 0
            ? (expanded, string.Empty)
            : (expanded[..sp], expanded[(sp + 1)..].TrimStart());
    }
}
