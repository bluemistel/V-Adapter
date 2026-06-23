using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;

namespace VAdapter.App.Views;

/// <summary>
/// 仮想スクリーン全体を覆う半透明オーバーレイ。ドラッグで矩形を選択させ、
/// 選択結果をスクリーン物理ピクセルの矩形として返す。
/// </summary>
public partial class RegionCaptureWindow : Window
{
    private Point _startDip;
    private bool _dragging;

    /// <summary>選択されたスクリーン矩形（物理ピクセル）。キャンセル時は null。</summary>
    public (int X, int Y, int Width, int Height)? SelectedScreenRect { get; private set; }

    public RegionCaptureWindow()
    {
        InitializeComponent();

        // 仮想スクリーン全体を覆う（WPF DIP 単位）。
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        Loaded += (_, _) => Activate();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragging = true;
        _startDip = e.GetPosition(RootCanvas);
        Canvas.SetLeft(SelectionRect, _startDip.X);
        Canvas.SetTop(SelectionRect, _startDip.Y);
        SelectionRect.Width = 0;
        SelectionRect.Height = 0;
        SelectionRect.Visibility = Visibility.Visible;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
            return;
        var p = e.GetPosition(RootCanvas);
        var x = Math.Min(p.X, _startDip.X);
        var y = Math.Min(p.Y, _startDip.Y);
        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = Math.Abs(p.X - _startDip.X);
        SelectionRect.Height = Math.Abs(p.Y - _startDip.Y);
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
            return;
        _dragging = false;
        ReleaseMouseCapture();

        var endDip = e.GetPosition(RootCanvas);

        // DIP 上の 2 隅を物理ピクセルへ変換（PointToScreen はデバイスピクセルを返す）。
        var p1 = PointToScreen(_startDip);
        var p2 = PointToScreen(endDip);

        int left = (int)Math.Round(Math.Min(p1.X, p2.X));
        int top = (int)Math.Round(Math.Min(p1.Y, p2.Y));
        int width = (int)Math.Round(Math.Abs(p2.X - p1.X));
        int height = (int)Math.Round(Math.Abs(p2.Y - p1.Y));

        if (width >= 4 && height >= 4)
            SelectedScreenRect = (left, top, width, height);

        DialogResult = SelectedScreenRect is not null;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            DialogResult = false;
    }
}
