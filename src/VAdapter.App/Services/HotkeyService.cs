using System.Windows;
using System.Windows.Interop;
using VAdapter.Automation.Input;
using VAdapter.Core.Models;

namespace VAdapter.App.Services;

/// <summary>
/// グローバルホットキーの登録と WM_HOTKEY の受信（WPF HwndSource フック）。
/// 有効かつショートカット設定済みのマクロをまとめて登録する。
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private readonly Dictionary<int, Macro> _map = new();
    private HwndSource? _source;
    private int _nextId = 1;

    /// <summary>ホットキー押下時に発火（対応マクロを通知）。</summary>
    public event Action<Macro>? HotkeyPressed;

    private IntPtr Handle => _source?.Handle ?? IntPtr.Zero;

    /// <summary>メインウィンドウのハンドルにフックを接続する（ウィンドウ表示後に呼ぶ）。</summary>
    public void Attach(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        _source = HwndSource.FromHwnd(handle);
        _source?.AddHook(WndProc);
    }

    /// <summary>現在のマクロ群でホットキーを登録し直す。登録できなかった件数を返す。</summary>
    public int RegisterAll(IEnumerable<Macro> macros)
    {
        UnregisterAll();
        if (Handle == IntPtr.Zero)
            return 0;

        int failures = 0;
        foreach (var macro in macros)
        {
            if (!macro.IsEnabled || macro.Shortcut is not { IsValid: true })
                continue;

            int id = _nextId++;
            if (HotkeyManager.Register(Handle, id, macro.Shortcut))
                _map[id] = macro;
            else
                failures++;
        }
        return failures;
    }

    /// <summary>すべてのホットキーを一時解除する（編集中の競合回避用）。</summary>
    public void Suspend() => UnregisterAll();

    private void UnregisterAll()
    {
        if (Handle != IntPtr.Zero)
        {
            foreach (var id in _map.Keys)
                HotkeyManager.Unregister(Handle, id);
        }
        _map.Clear();
        _nextId = 1;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == HotkeyManager.WM_HOTKEY && _map.TryGetValue(wParam.ToInt32(), out var macro))
        {
            HotkeyPressed?.Invoke(macro);
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source?.RemoveHook(WndProc);
    }
}
