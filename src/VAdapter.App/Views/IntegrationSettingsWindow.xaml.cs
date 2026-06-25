using System.Collections.ObjectModel;
using System.Windows;
using VAdapter.App.Services;
using VAdapter.Core.Models;
using VAdapter.Core.Serialization;

namespace VAdapter.App.Views;

public partial class IntegrationSettingsWindow : Window
{
    /// <summary>監視フォルダの表示用ラッパー。</summary>
    public sealed class FolderRow
    {
        public WatchFolder Model { get; }
        public FolderRow(WatchFolder model) => Model = model;
        public string Display => Model.IncludeSubdirectories
            ? $"{Model.Path}   （サブフォルダ含む）"
            : Model.Path;
    }

    private readonly AppState _state;
    private readonly IntegrationSettings _working;

    private AviutlDropConfig? _currentConfig;
    private readonly ObservableCollection<FolderRow> _folders = new();
    private readonly ObservableCollection<SpeakerRule> _rules = new();
    private bool _initializing;

    public IntegrationSettingsWindow(AppState state)
    {
        InitializeComponent();
        _state = state;
        _working = DeepClone(state.Integration);

        FolderList.ItemsSource = _folders;
        RulesGrid.ItemsSource = _rules;

        _state.DropService.Log += OnServiceLog;
        Closed += (_, _) => _state.DropService.Log -= OnServiceLog;

        _initializing = true;
        (_working.ActiveMode switch
        {
            IntegrationMode.AviUtl => ModeAviUtl,
            IntegrationMode.AviUtl2 => ModeAviUtl2,
            _ => ModeMacro,
        }).IsChecked = true;
        _initializing = false;

        LoadEditor(_working.ActiveMode);
    }

    private void OnModeChecked(object sender, RoutedEventArgs e)
    {
        if (_initializing)
            return;

        // 切替前に現在の編集内容を保存
        FlushEditor();

        var mode = SelectedMode();
        _working.ActiveMode = mode;
        LoadEditor(mode);
    }

    private IntegrationMode SelectedMode()
    {
        if (ModeAviUtl.IsChecked == true) return IntegrationMode.AviUtl;
        if (ModeAviUtl2.IsChecked == true) return IntegrationMode.AviUtl2;
        return IntegrationMode.MacroOnly;
    }

    private void LoadEditor(IntegrationMode mode)
    {
        _currentConfig = _working.ConfigFor(mode);

        var isMacroOnly = mode == IntegrationMode.MacroOnly;
        MacroOnlyPanel.Visibility = isMacroOnly ? Visibility.Visible : Visibility.Collapsed;
        AviutlPanel.Visibility = isMacroOnly ? Visibility.Collapsed : Visibility.Visible;
        MarginPanel.Visibility = mode == IntegrationMode.AviUtl2 ? Visibility.Visible : Visibility.Collapsed;

        _folders.Clear();
        _rules.Clear();

        if (_currentConfig is null)
            return;

        DefaultLayerBox.Text = _currentConfig.DefaultLayer.ToString();
        FrameAdvanceBox.Text = _currentConfig.FrameAdvance.ToString();
        AdvanceToItemEndCheck.IsChecked = _currentConfig.AdvanceToItemEnd;
        FrameAdvanceBox.IsEnabled = !_currentConfig.AdvanceToItemEnd;
        StableWaitBox.Text = _currentConfig.StableWaitMs.ToString();
        MarginBox.Text = _currentConfig.Margin.ToString();

        foreach (var f in _currentConfig.Folders)
            _folders.Add(new FolderRow(f));
        foreach (var r in _currentConfig.Rules)
            _rules.Add(r);

        RefreshStatus();
    }

    private void FlushEditor()
    {
        if (_currentConfig is null)
            return;

        // DataGrid の編集中セルを確定
        RulesGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

        _currentConfig.DefaultLayer = ParseOr(DefaultLayerBox.Text, _currentConfig.DefaultLayer);
        _currentConfig.FrameAdvance = ParseOr(FrameAdvanceBox.Text, _currentConfig.FrameAdvance);
        _currentConfig.AdvanceToItemEnd = AdvanceToItemEndCheck.IsChecked == true;
        _currentConfig.StableWaitMs = ParseOr(StableWaitBox.Text, _currentConfig.StableWaitMs);
        _currentConfig.Margin = ParseOr(MarginBox.Text, _currentConfig.Margin);
        _currentConfig.Folders = _folders.Select(r => r.Model).ToList();
        _currentConfig.Rules = _rules.ToList();
    }

    // --- フォルダ ---

    private void OnAddFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "監視するフォルダを選択" };
        if (dialog.ShowDialog(this) != true)
            return;
        _folders.Add(new FolderRow(new WatchFolder
        {
            Path = dialog.FolderName,
            IncludeSubdirectories = IncludeSubCheck.IsChecked == true,
        }));
    }

    private void OnRemoveFolder(object sender, RoutedEventArgs e)
    {
        if (FolderList.SelectedItem is FolderRow row)
            _folders.Remove(row);
    }

    // --- 話者ルール ---

    private void OnAddRule(object sender, RoutedEventArgs e)
    {
        // 既定で「_話者_ / -話者-」を抽出する正規表現をプリセット（調整したい人は編集可能）。
        var rule = new SpeakerRule
        {
            NamePattern = SpeakerRule.DefaultNamePattern,
            SpeakerName = "",
            Layer = _currentConfig?.DefaultLayer ?? 1,
        };
        _rules.Add(rule);
        RulesGrid.SelectedItem = rule;
    }

    private void OnRemoveRule(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is SpeakerRule rule)
            _rules.Remove(rule);
    }

    // --- 状態 / テスト ---

    private void OnAdvanceToggled(object sender, RoutedEventArgs e)
    {
        if (FrameAdvanceBox is not null)
            FrameAdvanceBox.IsEnabled = AdvanceToItemEndCheck.IsChecked != true;
    }

    private void OnRefreshStatus(object sender, RoutedEventArgs e) => RefreshStatus();

    private void RefreshStatus()
    {
        var available = _state.DropService.IsGcmzAvailable();
        StatusGcmz.Text = available
            ? "ごちゃまぜドロップス: 接続OK"
            : "ごちゃまぜドロップス: 未検出（AviUtl とプラグインの起動を確認）";

        if (!available)
        {
            StatusProject.Text = "プロジェクト: —";
            return;
        }

        var info = _state.DropService.ReadGcmzInfo();
        StatusProject.Text = info is null
            ? "プロジェクト: 情報取得不可"
            : info.HasProject
                ? $"プロジェクト: 読込済み（{info.Width}x{info.Height} / API v{info.ApiVersion}）"
                : $"プロジェクト: 未読込（API v{info.ApiVersion}）";
    }

    private void OnTestDrop(object sender, RoutedEventArgs e)
    {
        FlushEditor();
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "投げ込むファイルを選択（テスト）",
            Filter = "音声/全ファイル|*.wav;*.*",
        };
        if (dialog.ShowDialog(this) != true)
            return;

        // テスト時は選択中モードの設定を即時反映してから投げる
        _state.DropService.Apply(_working);
        var layer = ParseOr(DefaultLayerBox.Text, 1);

        // wav と同名 txt があれば一緒に投げる（PSDToolKit の発動条件②に合わせる）。
        var files = new List<string> { dialog.FileName };
        var txt = System.IO.Path.ChangeExtension(dialog.FileName, ".txt");
        if (!string.Equals(txt, dialog.FileName, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(txt))
            files.Add(txt);

        var result = _state.DropService.DropNow(files, layer);
        AppendLog(result.Success
            ? $"テスト投入成功: {System.IO.Path.GetFileName(dialog.FileName)}{(files.Count > 1 ? "（+txt）" : "")}"
            : $"テスト投入失敗: {result.Error}");
        // 監視状態を保存済み設定へ戻す（OKするまで永続化しない）
        _state.DropService.Apply(_state.Integration);
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        FlushEditor();
        _state.UpdateIntegration(_working);
        DialogResult = true;
    }

    // --- ログ ---

    private void OnServiceLog(string message) =>
        Dispatcher.BeginInvoke(new Action(() => AppendLog(message)));

    private void AppendLog(string message)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }

    private static int ParseOr(string? text, int fallback) =>
        int.TryParse(text?.Trim(), out var v) ? v : fallback;

    private static IntegrationSettings DeepClone(IntegrationSettings s) =>
        VAdapterJson.Deserialize<IntegrationSettings>(VAdapterJson.Serialize(s))!;
}
