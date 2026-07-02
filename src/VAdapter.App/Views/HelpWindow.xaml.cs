using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using VAdapter.App.Services;

namespace VAdapter.App.Views;

public partial class HelpWindow : Window
{
    private const string ReportFormUrl =
        "https://ionian-gallimimus-e47.notion.site/32b8c5bf8aa481978f37e470a25e1e01";

    private readonly UpdateService _updateService = new();

    public HelpWindow()
    {
        InitializeComponent();
        MenuList.SelectedIndex = 0;

        var v = UpdateService.CurrentVersion();
        UpdateCurrentText.Text = $"現在のバージョン: v{v.Major}.{v.Minor}.{v.Build}";
    }

    private void OnMenuChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SecBasic is null) // テンプレート構築前は無視
            return;

        // メニュー順: 使い方(0) / AviUtl(1) / 更新履歴(2) / アップデート(3) / 不具合報告(4) / ライセンス(5)
        var index = MenuList.SelectedIndex;
        SecBasic.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        SecAviutl.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        SecChangelog.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
        SecUpdate.Visibility = index == 3 ? Visibility.Visible : Visibility.Collapsed;
        SecReport.Visibility = index == 4 ? Visibility.Visible : Visibility.Collapsed;
        SecLicense.Visibility = index == 5 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = "確認中…";
        try
        {
            var result = await _updateService.CheckAsync();
            if (result is null)
            {
                UpdateStatusText.Text = "更新の確認に失敗しました（ネットワーク未接続、または一時的な制限の可能性）。時間をおいて再試行してください。";
                return;
            }

            var cur = $"v{result.Current.Major}.{result.Current.Minor}.{result.Current.Build}";
            if (result is { UpdateAvailable: true, Latest: { } latest })
                UpdateStatusText.Text = $"新しいバージョン v{latest.Major}.{latest.Minor}.{latest.Build} が公開されています（現在 {cur}）。"
                    + "「ダウンロードページを開く」から取得してください。";
            else
                UpdateStatusText.Text = $"お使いのバージョン（{cur}）は最新です。";
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private void OnOpenReleases(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(UpdateService.ReleasesUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"ブラウザを開けませんでした。URL を手動で開いてください。\n\n{UpdateService.ReleasesUrl}\n\n{ex.Message}",
                "アップデート", MessageBoxButton.OK, MessageBoxImage.Information);
        }
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
