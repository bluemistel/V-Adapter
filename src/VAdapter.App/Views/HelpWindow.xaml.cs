using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using VAdapter.App.Services;

namespace VAdapter.App.Views;

public partial class HelpWindow : Window
{
    private const string ReportFormUrl =
        "https://ionian-gallimimus-e47.notion.site/32b8c5bf8aa481978f37e470a25e1e01";

    /// <summary>AviUtl・AviUtl2 連携の手順（画像つき）。</summary>
    private const string AviutlWikiUrl =
        "https://github.com/bluemistel/V-Adapter/wiki/AviUtl%E3%83%BBAviUtl2%E9%80%A3%E6%90%BA";

    /// <summary>VoiSona Talk 推奨設定（1.3.9.1以降）。クイックエクスポート対応で必要になる設定手順。</summary>
    private const string VoiSonaWikiUrl =
        "https://github.com/bluemistel/V-Adapter/wiki/VoiSona-Talk%E6%8E%A8%E5%A5%A8%E8%A8%AD%E5%AE%9A%281.3.9.1%E4%BB%A5%E9%99%8D%29";

    private readonly UpdateService _updateService = new();

    public HelpWindow()
    {
        InitializeComponent();
        MenuList.SelectedIndex = 0;

        VersionBadge.Text = UpdateService.DisplayVersion;
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

    private void OnOpenAviutlWiki(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(AviutlWikiUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"ブラウザを開けませんでした。URL を手動で開いてください。\n\n{AviutlWikiUrl}\n\n{ex.Message}",
                "AviUtl・AviUtl2 連携", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnOpenVoiSonaWiki(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(VoiSonaWikiUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"ブラウザを開けませんでした。URL を手動で開いてください。\n\n{VoiSonaWikiUrl}\n\n{ex.Message}",
                "VoiSona Talk 推奨設定", MessageBoxButton.OK, MessageBoxImage.Information);
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
