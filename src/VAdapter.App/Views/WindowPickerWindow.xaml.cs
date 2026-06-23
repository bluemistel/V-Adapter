using System.Windows;
using VAdapter.Automation.Windows;

namespace VAdapter.App.Views;

public partial class WindowPickerWindow : Window
{
    private readonly WindowLocator _locator = new();

    /// <summary>選択されたウィンドウ（キャンセル時は null）。</summary>
    public WindowInfo? SelectedWindow { get; private set; }

    public WindowPickerWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        WindowList.ItemsSource = _locator.EnumerateVisibleWindows()
            .OrderBy(w => w.ProcessName)
            .ThenBy(w => w.Title)
            .ToList();
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => Refresh();

    private void OnSelect(object sender, RoutedEventArgs e)
    {
        if (WindowList.SelectedItem is not WindowInfo info)
            return;
        SelectedWindow = info;
        DialogResult = true;
    }
}
