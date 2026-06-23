using System.Runtime.InteropServices;
using VAdapter.Automation.Native;

namespace VAdapter.Automation.Input;

/// <summary>
/// 低レベルマウスフック。座標取得モードで「次の左クリック」のスクリーン座標を捕捉する用途。
/// メッセージループのあるスレッド（WPF UI スレッド等）で生成・破棄すること。
/// </summary>
public sealed class GlobalMouseHook : IDisposable
{
    private readonly NativeMethods.LowLevelMouseProc _proc;
    private IntPtr _hook = IntPtr.Zero;

    /// <summary>左ボタン押下時に発火（引数はスクリーン絶対座標）。</summary>
    public event Action<int, int>? LeftButtonDown;

    public GlobalMouseHook()
    {
        // デリゲートを GC から保護するためフィールド保持。
        _proc = HookCallback;
    }

    public bool IsActive => _hook != IntPtr.Zero;

    public void Start()
    {
        if (_hook != IntPtr.Zero)
            return;
        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL, _proc, NativeMethods.GetModuleHandle(null), 0);
    }

    public void Stop()
    {
        if (_hook == IntPtr.Zero)
            return;
        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (int)wParam == NativeMethods.WM_LBUTTONDOWN)
        {
            var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            LeftButtonDown?.Invoke(data.pt.X, data.pt.Y);
        }
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose() => Stop();
}
