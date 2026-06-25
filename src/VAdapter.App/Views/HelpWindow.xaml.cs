using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace VAdapter.App.Views;

public partial class HelpWindow : Window
{
    private const string ReportFormUrl =
        "https://ionian-gallimimus-e47.notion.site/32b8c5bf8aa481978f37e470a25e1e01";

    public HelpWindow()
    {
        InitializeComponent();
        MenuList.SelectedIndex = 0;
    }

    private void OnMenuChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SecBasic is null) // テンプレート構築前は無視
            return;

        var index = MenuList.SelectedIndex;
        SecBasic.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        SecAviutl.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        SecChangelog.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        SecReport.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
        SecLicense.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnOpenReportForm(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(ReportFormUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"ブラウザを開けませんでした。URL を手動で開いてください。\n\n{ReportFormUrl}\n\n{ex.Message}",
                "不具合報告", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
