using System.Windows.Input;
using VAdapter.Core.Models;

namespace VAdapter.App.Services;

/// <summary>WPF のキーイベントから <see cref="KeyCombination"/> を生成する。</summary>
public static class KeyCaptureHelper
{
    /// <summary>
    /// 押下されたキー組み合わせを取得する。修飾キー単独の押下時は null を返す
    /// （主キーが確定するまで待つため）。
    /// </summary>
    public static KeyCombination? FromKeyEvent(KeyEventArgs e)
    {
        // IME 有効時は e.Key が ImeProcessed になり実キーが取れないため、実キーへ解決する。
        var key = e.Key;
        if (key == Key.ImeProcessed)
            key = e.ImeProcessedKey;
        else if (key == Key.DeadCharProcessed)
            key = e.DeadCharProcessedKey;
        else if (key == Key.System)
            key = e.SystemKey;

        if (IsModifierKey(key))
            return null;

        var mods = KeyModifiers.None;
        var m = Keyboard.Modifiers;
        if (m.HasFlag(ModifierKeys.Control)) mods |= KeyModifiers.Control;
        if (m.HasFlag(ModifierKeys.Alt)) mods |= KeyModifiers.Alt;
        if (m.HasFlag(ModifierKeys.Shift)) mods |= KeyModifiers.Shift;
        if (m.HasFlag(ModifierKeys.Windows)) mods |= KeyModifiers.Win;

        int vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0)
            return null;

        return new KeyCombination(mods, vk, key.ToString());
    }

    private static bool IsModifierKey(Key k) => k is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or
        Key.LWin or Key.RWin or
        Key.System or Key.None;
}
