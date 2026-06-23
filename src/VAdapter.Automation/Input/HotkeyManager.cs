using VAdapter.Core.Models;
using VAdapter.Automation.Native;

namespace VAdapter.Automation.Input;

/// <summary>
/// グローバルホットキー登録の薄いラッパー（RegisterHotKey/UnregisterHotKey）。
/// WPF 非依存。HWND とメッセージ受信側（HwndSource フック等）は呼び出し元が用意する。
/// </summary>
public static class HotkeyManager
{
    /// <summary>WM_HOTKEY メッセージ ID。</summary>
    public const int WM_HOTKEY = 0x0312;

    public static bool Register(IntPtr hWnd, int id, KeyCombination combo)
    {
        if (!combo.IsValid)
            return false;
        var mods = ToModFlags(combo.Modifiers) | NativeMethods.MOD_NOREPEAT;
        return NativeMethods.RegisterHotKey(hWnd, id, mods, (uint)combo.VirtualKey);
    }

    public static void Unregister(IntPtr hWnd, int id) =>
        NativeMethods.UnregisterHotKey(hWnd, id);

    private static uint ToModFlags(KeyModifiers modifiers)
    {
        uint flags = 0;
        if (modifiers.HasFlag(KeyModifiers.Control)) flags |= NativeMethods.MOD_CONTROL;
        if (modifiers.HasFlag(KeyModifiers.Alt)) flags |= NativeMethods.MOD_ALT;
        if (modifiers.HasFlag(KeyModifiers.Shift)) flags |= NativeMethods.MOD_SHIFT;
        if (modifiers.HasFlag(KeyModifiers.Win)) flags |= NativeMethods.MOD_WIN;
        return flags;
    }
}
