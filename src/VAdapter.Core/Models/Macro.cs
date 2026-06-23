namespace VAdapter.Core.Models;

/// <summary>
/// ユーザー視点の 1 操作（再生・保存など）を表すマクロ。
/// グローバルショートカットで起動され、対象アプリごとに用意したスクリプトのうち
/// 状況に一致する 1 つを実行する。
/// </summary>
public sealed class Macro
{
    /// <summary>一意な識別子。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>表示名（例: "音声の再生"）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>アプリ作者が同梱する標準マクロか。</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>このマクロを有効（ホットキー登録対象）とするか。</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>起動用グローバルショートカット。null/未設定ならホットキー登録しない。</summary>
    public KeyCombination? Shortcut { get; set; }

    /// <summary>対象アプリごとのスクリプト群。</summary>
    public List<MacroScript> Scripts { get; set; } = new();
}
