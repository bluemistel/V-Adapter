using VAdapter.Automation.Native;

namespace VAdapter.Automation.Input;

/// <summary>対象ウィンドウを確実に前面化する（AttachThreadInput による回避策込み）。</summary>
public static class ForegroundWindowHelper
{
    /// <summary>現在の前面ウィンドウのハンドル。</summary>
    public static IntPtr GetForeground() => NativeMethods.GetForegroundWindow();

    /// <summary>指定ウィンドウが前面（アクティブ）か。</summary>
    public static bool IsForeground(IntPtr hWnd) =>
        hWnd != IntPtr.Zero && NativeMethods.GetForegroundWindow() == hWnd;

    /// <summary>ウィンドウを復元・前面化する。既に前面なら何もしない。</summary>
    public static bool BringToForeground(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return false;

        if (NativeMethods.IsIconic(hWnd))
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);

        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == hWnd)
            return true;

        uint foregroundThread = NativeMethods.GetWindowThreadProcessId(foreground, out _);
        uint targetThread = NativeMethods.GetWindowThreadProcessId(hWnd, out _);
        uint currentThread = NativeMethods.GetCurrentThreadId();

        // 入力キューをアタッチして SetForegroundWindow の制限を回避する。
        bool attachedToForeground = false;
        bool attachedToTarget = false;
        try
        {
            if (foregroundThread != 0 && foregroundThread != currentThread)
                attachedToForeground = NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
            if (targetThread != 0 && targetThread != currentThread)
                attachedToTarget = NativeMethods.AttachThreadInput(currentThread, targetThread, true);

            NativeMethods.BringWindowToTop(hWnd);
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
            bool ok = NativeMethods.SetForegroundWindow(hWnd);
            return ok || NativeMethods.GetForegroundWindow() == hWnd;
        }
        finally
        {
            if (attachedToForeground)
                NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
            if (attachedToTarget)
                NativeMethods.AttachThreadInput(currentThread, targetThread, false);
        }
    }
}
