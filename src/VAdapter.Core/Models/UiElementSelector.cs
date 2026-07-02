namespace VAdapter.Core.Models;

/// <summary>
/// UI Automation 要素の選択条件。対象アプリのウィンドウ配下から、AutomationId / Name /
/// ControlType のうち指定されたものを AND 条件で照合して要素を特定する。
/// 「UI要素を取得（クリック）」で捕捉した値を保持する。
/// </summary>
public sealed class UiElementSelector
{
    /// <summary>AutomationId（最も安定。指定時はこれを主に照合）。</summary>
    public string? AutomationId { get; set; }

    /// <summary>要素名（Name プロパティ）。</summary>
    public string? Name { get; set; }

    /// <summary>コントロール種別（例: "Edit" / "Text" / "Document"）。</summary>
    public string? ControlType { get; set; }

    /// <summary>いずれかの条件が指定されているか。</summary>
    public bool HasAnyCondition =>
        !string.IsNullOrEmpty(AutomationId)
        || !string.IsNullOrEmpty(Name)
        || !string.IsNullOrEmpty(ControlType);

    /// <summary>UI 表示用の要約。</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(AutomationId)) parts.Add($"id={AutomationId}");
        if (!string.IsNullOrEmpty(Name)) parts.Add($"name={Name}");
        if (!string.IsNullOrEmpty(ControlType)) parts.Add($"type={ControlType}");
        return parts.Count == 0 ? "(未設定)" : string.Join(" / ", parts);
    }
}
