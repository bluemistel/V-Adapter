using System.Windows;
using System.Windows.Threading;
using VAdapter.App.Services;

namespace VAdapter.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // UI スレッドの未処理例外を捕捉してアプリの突然終了を防ぐ。
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        AppState state;
        try
        {
            state = new AppState();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"起動に失敗しました: {ex.Message}", "V-Adapter",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var window = new MainWindow(state);
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"予期しないエラーが発生しました:\n{e.Exception.Message}", "V-Adapter",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
