namespace VAdapter.Core.Models;

/// <summary>
/// 修飾キーと 1 つの主キー（Windows 仮想キーコード）の組み合わせ。
/// グローバルショートカットおよびキー送信命令の双方で使用する。
/// </summary>
public sealed class KeyCombination
{
    /// <summary>修飾キー（組み合わせ可能）。</summary>
    public KeyModifiers Modifiers { get; set; } = KeyModifiers.None;

    /// <summary>主キーの Windows 仮想キーコード（VK_*）。0 は未設定。</summary>
    public int VirtualKey { get; set; }

    /// <summary>表示用のキー名（例: "Space"）。UI で設定された値を保持。任意。</summary>
    public string? KeyName { get; set; }

    public KeyCombination() { }

    public KeyCombination(KeyModifiers modifiers, int virtualKey, string? keyName = null)
    {
        Modifiers = modifiers;
        VirtualKey = virtualKey;
        KeyName = keyName;
    }

    /// <summary>主キーが設定されているか。</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => VirtualKey != 0;

    /// <summary>"Ctrl+Shift+E" のような表示文字列を生成する。</summary>
    public override string ToString()
    {
        var parts = new List<string>(4);
        if (Modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(KeyModifiers.Win)) parts.Add("Win");
        parts.Add(KeyName ?? (VirtualKey != 0 ? $"VK_{VirtualKey}" : "(未設定)"));
        return string.Join("+", parts);
    }
}
