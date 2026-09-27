using System.Collections.ObjectModel;
using System.Windows;
using VAdapter.App.Services;
using VAdapter.Core.Media;
using VAdapter.Core.Models;
using VAdapter.Core.Serialization;

namespace VAdapter.App.Views;

public partial class IntegrationSettingsWindow : Window
{
    /// <summary>
    /// 保存フォルダ 1 件の表示用ラッパー。
    /// 「サブフォルダも含む」は行内のチェックボックスから <see cref="Model"/> へ直接書き戻される。
    /// </summary>
    public sealed class FolderRow
    {
        public WatchFolder Model { get; }
        public FolderRow(WatchFolder model) => Model = model;
    }

    private readonly AppState _state;
    private readonly IntegrationSettings _working;

    /// <summary>この画面から対象アプリを編集して保存したか（呼び出し元の表示更新用）。</summary>
    public bool TargetsChanged { get; private set; }

    private AviutlDropConfig? _currentConfig;
    private readonly ObservableCollection<FolderRow> _folders = new();
    private readonly ObservableCollection<SpeakerRule> _rules = new();
    private readonly ObservableCollection<Voiceroid2Character> _v2chars = new();
    private bool _initializing;
    private bool _loadingPostProcess;

    public IntegrationSettingsWindow(AppState state)
    {
        InitializeComponent();
        _state = state;
        _working = DeepClone(state.Integration);

        // 保存フォルダは全モード共通の単一リスト。
        FolderList.ItemsSource = _folders;
        foreach (var f in _working.WatchFolders)
            _folders.Add(new FolderRow(f));
        _folders.CollectionChanged += (_, _) => UpdateWatchStatus();

        RulesGrid.ItemsSource = _rules;

        // 後処理（全モード共通）
        LoadPostProcess();

        // VOICEROID2 オプション（全モード共通）
        V2EnableCheck.IsChecked = _working.Voiceroid2.Enabled;
        foreach (var c in _working.Voiceroid2.Characters)
            _v2chars.Add(c);
        V2Grid.ItemsSource = _v2chars;
        UpdateFolderIndexHint();

        _state.DropService.Log += OnServiceLog;
        Closed += (_, _) => _state.DropService.Log -= OnServiceLog;

        _initializing = true;
        (_working.ActiveMode switch
        {
            IntegrationMode.AviUtl => ModeAviUtl,
            IntegrationMode.AviUtl2 => ModeAviUtl2,
            IntegrationMode.External => ModeExternal,
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
        if (ModeExternal.IsChecked == true) return IntegrationMode.External;
        return IntegrationMode.MacroOnly;
    }

    private void LoadEditor(IntegrationMode mode)
    {
        _currentConfig = _working.ConfigFor(mode);

        var isMacroOnly = mode == IntegrationMode.MacroOnly;
        var isExternal = mode == IntegrationMode.External;
        MacroOnlyPanel.Visibility = isMacroOnly ? Visibility.Visible : Visibility.Collapsed;
        AviutlPanel.Visibility = isMacroOnly ? Visibility.Collapsed : Visibility.Visible;
        ExternalCmdPanel.Visibility = isExternal ? Visibility.Visible : Visibility.Collapsed;
        // frameAdvance（手動）と margin は gcmz 固有。外部アダプタでは隠す。
        FrameAdvancePanel.Visibility = isExternal ? Visibility.Collapsed : Visibility.Visible;
        MarginPanel.Visibility = mode == IntegrationMode.AviUtl2 ? Visibility.Visible : Visibility.Collapsed;

        // 編集ソフトパス（ランチャー）はモード共通。プレースホルダはモードごとに変える。
        EditorPathBox.Text = _working.EditorPathFor(mode) ?? string.Empty;
        EditorPlaceholderText.Text = mode switch
        {
            IntegrationMode.MacroOnly => "../YukkuriMovieMaker.exe",
            IntegrationMode.AviUtl => @"...\aviutl.exe",
            IntegrationMode.AviUtl2 => @"...\aviutl2.exe",
            _ => @"...\editor.exe",
        };
        UpdateEditorPlaceholder();

        _rules.Clear();

        // 話者ルールはモードごとに異なり、プレビューの話者名に影響するため切替のたびに見直す。
        if (_currentConfig is null)
        {
            UpdatePreview();
            UpdateWatchStatus();
            return;
        }

        DefaultLayerBox.Text = _currentConfig.DefaultLayer.ToString();
        FrameAdvanceBox.Text = _currentConfig.FrameAdvance.ToString();
        AdvanceToItemEndCheck.IsChecked = _currentConfig.AdvanceToItemEnd;
        FrameAdvanceBox.IsEnabled = !_currentConfig.AdvanceToItemEnd;
        StableWaitBox.Text = _currentConfig.StableWaitMs.ToString();
        MarginBox.Text = _currentConfig.Margin.ToString();

        if (_currentConfig is ExternalAdapterConfig ext)
        {
            CommandBox.Text = ext.CommandTemplate;
            TimeoutBox.Text = ext.TimeoutMs.ToString();
        }

        foreach (var r in _currentConfig.Rules)
            _rules.Add(r);

        UpdatePreview();
        UpdateWatchStatus();
        RefreshStatus();
    }

    private void FlushEditor()
    {
        // 編集ソフトパス（ランチャー）はモード共通で先に確定（MacroOnly は config が無いため）。
        var editor = NullIfEmpty(EditorPathBox.Text);
        if (_working.ActiveMode == IntegrationMode.MacroOnly)
            _working.MacroEditorPath = editor;
        else if (_currentConfig is not null)
            _currentConfig.EditorPath = editor;

        // 保存フォルダ（全モード共通）もモードに依らず確定。
        _working.WatchFolders = _folders.Select(r => r.Model).ToList();

        // VOICEROID2 オプション（全モード共通）もモードに依らず確定。
        V2Grid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        _working.Voiceroid2.Enabled = V2EnableCheck.IsChecked == true;
        _working.Voiceroid2.Characters = _v2chars.ToList();

        if (_currentConfig is null)
            return;

        // DataGrid の編集中セルを確定
        RulesGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

        _currentConfig.DefaultLayer = ParseOr(DefaultLayerBox.Text, _currentConfig.DefaultLayer);
        _currentConfig.FrameAdvance = ParseOr(FrameAdvanceBox.Text, _currentConfig.FrameAdvance);
        _currentConfig.AdvanceToItemEnd = AdvanceToItemEndCheck.IsChecked == true;
        _currentConfig.StableWaitMs = ParseOr(StableWaitBox.Text, _currentConfig.StableWaitMs);
        _currentConfig.Margin = ParseOr(MarginBox.Text, _currentConfig.Margin);
        _currentConfig.Rules = _rules.ToList();
        // 旧形式の監視フォルダは移行済み。保存し直す際に古い値を残さない。
        _currentConfig.Folders.Clear();

        if (_currentConfig is ExternalAdapterConfig ext)
        {
            ext.CommandTemplate = CommandBox.Text?.Trim() ?? string.Empty;
            ext.TimeoutMs = ParseOr(TimeoutBox.Text, ext.TimeoutMs);
        }
    }

    // --- 保存フォルダ ---

    private void OnAddFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "合成音声ソフトの保存先フォルダを選択" };
        if (dialog.ShowDialog(this) != true)
            return;
        if (_folders.Any(r => string.Equals(r.Model.Path, dialog.FolderName, StringComparison.OrdinalIgnoreCase)))
            return;
        _folders.Add(new FolderRow(new WatchFolder { Path = dialog.FolderName }));
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
        // ルールが増減すると後処理で話者名が使えるかが変わる。
        UpdatePreview();
    }

    private void OnRemoveRule(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is SpeakerRule rule)
            _rules.Remove(rule);
        UpdatePreview();
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
        // 編集中の作業コピーの選択モードに対する状態を、監視に影響を与えず取得する。
        FlushEditor();
        var status = _state.DropService.GetStatus(_working);
        if (status is null)
        {
            StatusGcmz.Text = "投げ込み: 無効（マクロ動作ベース）";
            StatusProject.Text = "—";
            return;
        }

        StatusGcmz.Text = status.Summary;
        StatusProject.Text = status.TargetInfo ?? "—";
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
        CommitPostProcess();
        _state.UpdateIntegration(_working);
        DialogResult = true;
    }

    // --- 保存後の整理（後処理） ---

    /// <summary>改名プリセット（ComboBox の並び順と一致させる）。</summary>
    private const int PresetNone = 0, PresetDate = 1, PresetDateSpeaker = 2, PresetCustom = 3;

    /// <summary>
    /// プレビュー用の架空のファイル名。実ファイルは使わない。
    /// 実ファイルを使うと、たまたま残っていた古いファイルの話者名が
    /// 「いま判別された結果」に見えてしまい、事実と異なる情報を与えるため。
    /// </summary>
    private const string PreviewSampleName = "01_キャラ名_セリフ";
    private const string PreviewSampleSpeaker = "キャラ名";

    private void LoadPostProcess()
    {
        _loadingPostProcess = true;

        var pp = _working.PostProcess;
        PpEnableCheck.IsChecked = pp.Enabled;
        PpTemplateBox.Text = pp.NameTemplate;
        PpDestBox.Text = pp.DestinationFolder ?? string.Empty;
        PpSpeakerSubCheck.IsChecked = pp.SpeakerSubfolder;
        PpPresetCombo.SelectedIndex = PresetFor(pp.NameTemplate);

        _loadingPostProcess = false;

        UpdatePostProcessEnabled();
        UpdatePreview();
    }

    /// <summary>テンプレート文字列に対応するプリセット番号を返す（一致しなければカスタム）。</summary>
    private static int PresetFor(string? template) => (template ?? string.Empty).Trim() switch
    {
        "" => PresetNone,
        FilePostProcessOptions.TemplateDateName => PresetDate,
        FilePostProcessOptions.TemplateDateSpeakerName => PresetDateSpeaker,
        _ => PresetCustom,
    };

    /// <summary>現在の選択から実際に使うテンプレート文字列を得る。</summary>
    private string CurrentTemplate() => PpPresetCombo.SelectedIndex switch
    {
        PresetDate => FilePostProcessOptions.TemplateDateName,
        PresetDateSpeaker => FilePostProcessOptions.TemplateDateSpeakerName,
        PresetCustom => PpTemplateBox.Text.Trim(),
        _ => string.Empty,
    };

    private void CommitPostProcess()
    {
        var pp = _working.PostProcess;
        pp.Enabled = PpEnableCheck.IsChecked == true;
        pp.NameTemplate = CurrentTemplate();
        pp.DestinationFolder = string.IsNullOrWhiteSpace(PpDestBox.Text) ? null : PpDestBox.Text.Trim();
        pp.SpeakerSubfolder = PpSpeakerSubCheck.IsChecked == true;
        // 旧形式（後処理専用の監視フォルダ・追加の配布先）は廃止。保存時に残さない。
        pp.Folders.Clear();
    }

    private void OnPpToggled(object sender, RoutedEventArgs e)
    {
        // 有効にした直後にテンプレート未設定なら、最も使われる「日時 ＋ 元のファイル名」を既定にする。
        if (!_loadingPostProcess && PpEnableCheck.IsChecked == true && PpPresetCombo.SelectedIndex == PresetNone
            && string.IsNullOrWhiteSpace(_working.PostProcess.NameTemplate))
            PpPresetCombo.SelectedIndex = PresetDate;

        UpdatePostProcessEnabled();
        UpdateWatchStatus();
        UpdatePreview();
    }

    /// <summary>無効時に設定項目を触れないようにして、有効/無効の関係を分かりやすくする。</summary>
    private void UpdatePostProcessEnabled()
    {
        var enabled = PpEnableCheck.IsChecked == true;
        if (PpBody is not null)
            PpBody.IsEnabled = enabled;
        // ③ の移動も後処理の一部なので、同じスイッチで有効・無効を揃える。
        if (PpDestBody is not null)
            PpDestBody.IsEnabled = enabled;
    }

    private void OnPpPresetChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (PpCustomPanel is null)
            return;

        var custom = PpPresetCombo.SelectedIndex == PresetCustom;
        PpCustomPanel.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;

        // カスタムへ切り替えたとき、直前のプリセットを編集の出発点として引き継ぐ。
        if (custom && !_loadingPostProcess && string.IsNullOrWhiteSpace(PpTemplateBox.Text))
            PpTemplateBox.Text = FilePostProcessOptions.TemplateDateName;

        UpdatePreview();
    }

    private void OnPpTemplateChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdatePreview();

    private void OnPpDestChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdatePreview();

    /// <summary>カレット位置へトークンを挿入する（手入力の誤記を防ぐ）。</summary>
    private void OnPpInsertToken(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string token })
            return;

        var caret = PpTemplateBox.SelectionStart;
        var text = PpTemplateBox.Text;
        PpTemplateBox.Text = text.Remove(caret, PpTemplateBox.SelectionLength).Insert(caret, token);
        PpTemplateBox.SelectionStart = caret + token.Length;
        PpTemplateBox.Focus();
    }

    /// <summary>
    /// 設定した内容で名前と置き場所がどうなるかを、架空のサンプルで示す。
    /// 実ファイルは参照しない（実際の判別結果と見間違えるため）。
    /// </summary>
    private void UpdatePreview()
    {
        if (PpPreviewText is null)
            return;

        var name = FilePostProcessPlanner.ResolveName(
            PreviewSampleName, PreviewSampleSpeaker, CurrentTemplate(), DateTime.Now);

        var dest = PpDestBox.Text?.Trim();
        var place = string.IsNullOrWhiteSpace(dest) ? "①の保存フォルダ" : dest;
        if (PpSpeakerSubCheck.IsChecked == true)
            place = System.IO.Path.Combine(place, PreviewSampleSpeaker);

        PpPreviewText.Text = $"例）{PreviewSampleName}.wav  →  {place}\\{name}.wav";
    }

    /// <summary>① の状態（監視できているか）を一行で示す。設定漏れを画面上で気づけるようにする。</summary>
    private void UpdateWatchStatus()
    {
        if (WatchStatusText is null)
            return;

        var count = _folders.Count(r => !string.IsNullOrWhiteSpace(r.Model.Path));
        var post = PpEnableCheck?.IsChecked == true;

        if (count == 0)
        {
            WatchStatusText.Text = "保存フォルダが未登録のため、投げ込みも後処理も動きません。";
            WatchStatusText.Foreground = (System.Windows.Media.Brush)FindResource("B.Danger");
            return;
        }

        WatchStatusText.Text = $"監視対象: {count} フォルダ／後処理: {(post ? "有効" : "無効")}";
        WatchStatusText.Foreground = (System.Windows.Media.Brush)FindResource("B.TextTertiary");
    }

    private void OnPpBrowseDest(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "整えた後の置き場所を選択" };
        if (dialog.ShowDialog(this) == true)
            PpDestBox.Text = dialog.FolderName;
    }

    // --- ログ ---

    private void OnServiceLog(string message) =>
        Dispatcher.BeginInvoke(new Action(() => AppendLog(message)));

    private void AppendLog(string message)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }

    // --- 動画編集ソフトの起動（ランチャー） ---

    private void OnEditorPathChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) =>
        UpdateEditorPlaceholder();

    private void UpdateEditorPlaceholder()
    {
        if (EditorPlaceholderText is null)
            return;
        EditorPlaceholderText.Visibility = string.IsNullOrEmpty(EditorPathBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnBrowseEditor(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "動画編集ソフトの実行ファイルを選択",
            Filter = "実行ファイル (*.exe)|*.exe|すべてのファイル (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) == true)
            EditorPathBox.Text = dialog.FileName;
    }

    private void OnLaunchEditorTest(object sender, RoutedEventArgs e)
    {
        var path = NullIfEmpty(EditorPathBox.Text);
        if (path is null)
        {
            AppendLog("起動テスト: 実行ファイルが未登録です。");
            return;
        }

        var result = VAdapter.Core.Launch.AppLauncher.Launch(path);
        if (!result.Success)
            MessageBox.Show(result.Error ?? "起動に失敗しました。", "エラー",
                MessageBoxButton.OK, MessageBoxImage.Error);
        AppendLog(result.Success
            ? (result.AlreadyRunning ? "起動テスト: 既に起動しています。" : "起動テスト: 起動しました。")
            : $"起動テスト失敗: {result.Error}");
    }

    // --- VOICEROID2 オプション ---

    private void OnV2EnabledToggled(object sender, RoutedEventArgs e) => UpdateFolderIndexHint();

    /// <summary>
    /// 保存フォルダの行番号が何に使われるかの説明は、VOICEROID2 を使う場合にしか関係しない。
    /// 常時出すと他の利用者には無関係な情報になるため、有効なときだけ表示する。
    /// </summary>
    private void UpdateFolderIndexHint()
    {
        if (FolderIndexHint is null)
            return;
        FolderIndexHint.Visibility = V2EnableCheck.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnV2AddChar(object sender, RoutedEventArgs e)
    {
        var c = new Voiceroid2Character { Name = "", MonitorFolderIndex = 0 };
        _v2chars.Add(c);
        V2Grid.SelectedItem = c;
    }

    private void OnV2RemoveChar(object sender, RoutedEventArgs e)
    {
        if (V2Grid.SelectedItem is Voiceroid2Character c)
            _v2chars.Remove(c);
    }

    private void OnV2BrowseFolder(object sender, RoutedEventArgs e)
    {
        if (V2Grid.SelectedItem is not Voiceroid2Character c)
        {
            AppendLog("VOICEROID2: 保存先を設定する行を選択してください。");
            return;
        }
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "マクロ動作ベース時の保存先フォルダを選択" };
        if (dialog.ShowDialog(this) == true)
        {
            c.MacroBaseFolder = dialog.FolderName;
            V2Grid.Items.Refresh();
        }
    }

    private static int ParseOr(string? text, int fallback) =>
        int.TryParse(text?.Trim(), out var v) ? v : fallback;

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static IntegrationSettings DeepClone(IntegrationSettings s) =>
        VAdapterJson.Deserialize<IntegrationSettings>(VAdapterJson.Serialize(s))!;

    /// <summary>対象アプリ管理を開く。保存された場合はライブラリを永続化し、呼び出し元へ変更を伝える。</summary>
    private void OnOpenTargetManager(object sender, RoutedEventArgs e)
    {
        var window = new TargetManagerWindow(_state.Library) { Owner = this };
        if (window.ShowDialog() != true)
            return;

        _state.SaveAndRebindHotkeys();
        TargetsChanged = true;
    }
}
