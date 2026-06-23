namespace VAdapter.Automation.Windows;

/// <summary>列挙されたトップレベルウィンドウの情報。</summary>
public sealed class WindowInfo
{
    public required IntPtr Handle { get; init; }
    public required string Title { get; init; }
    public required string ClassName { get; init; }
    public required uint ProcessId { get; init; }

    /// <summary>プロセス名（拡張子なし）。取得できない場合は空。</summary>
    public string ProcessName { get; init; } = string.Empty;

    public override string ToString() =>
        $"{(string.IsNullOrEmpty(Title) ? "(無題)" : Title)} — {ProcessName} [0x{Handle.ToInt64():X}]";
}
