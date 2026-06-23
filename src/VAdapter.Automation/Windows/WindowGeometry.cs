using VAdapter.Automation.Native;
using VAdapter.Core.Models;

namespace VAdapter.Automation.Windows;

/// <summary>ウィンドウのジオメトリ取得と座標変換。</summary>
public static class WindowGeometry
{
    /// <summary>ウィンドウ矩形（スクリーン絶対座標）。</summary>
    public static (int Left, int Top, int Width, int Height) GetWindowRect(IntPtr hWnd)
    {
        NativeMethods.GetWindowRect(hWnd, out var r);
        return (r.Left, r.Top, r.Width, r.Height);
    }

    /// <summary>クライアント領域の左上スクリーン座標とサイズ。</summary>
    public static (int ScreenX, int ScreenY, int Width, int Height) GetClientAreaOnScreen(IntPtr hWnd)
    {
        NativeMethods.GetClientRect(hWnd, out var client);
        var origin = new NativeMethods.POINT { X = 0, Y = 0 };
        NativeMethods.ClientToScreen(hWnd, ref origin);
        return (origin.X, origin.Y, client.Width, client.Height);
    }

    /// <summary>クライアント相対座標をスクリーン絶対座標へ変換。</summary>
    public static (int X, int Y) ClientToScreen(IntPtr hWnd, int clientX, int clientY)
    {
        var pt = new NativeMethods.POINT { X = clientX, Y = clientY };
        NativeMethods.ClientToScreen(hWnd, ref pt);
        return (pt.X, pt.Y);
    }

    /// <summary>クライアント領域サイズ内でのアンカー基準点（クライアント相対）を返す。</summary>
    public static (int X, int Y) AnchorPoint(int clientWidth, int clientHeight, ClickAnchor anchor) => anchor switch
    {
        ClickAnchor.TopRight => (clientWidth, 0),
        ClickAnchor.BottomLeft => (0, clientHeight),
        ClickAnchor.BottomRight => (clientWidth, clientHeight),
        ClickAnchor.Center => (clientWidth / 2, clientHeight / 2),
        _ => (0, 0),
    };

    /// <summary>スクリーン絶対座標をクライアント相対座標へ変換。</summary>
    public static (int X, int Y) ScreenToClient(IntPtr hWnd, int screenX, int screenY)
    {
        var (originX, originY, _, _) = GetClientAreaOnScreen(hWnd);
        return (screenX - originX, screenY - originY);
    }
}
