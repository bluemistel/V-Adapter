using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using VAdapter.App.Services;
using VAdapter.App.ViewModels;
using VAdapter.App.Views;
using VAdapter.Core.Models;
using VAdapter.Core.Storage;

namespace VAdapter.App;

public partial class MainWindow : Window
{
    private readonly AppState _state;
    private readonly ObservableCollection<MacroRow> _rows = new();

    /// <summary>送信先トグルで選択中の対象アプリ ID（null は自動判定）。</summary>
    private string? _activeTargetId;
    private bool _suppressToggleEvents;

    public MainWindow(AppState state)
    {
        InitializeComponent();
        _state = state;
        MacroList.ItemsSource = _rows;
        ReloadRows();
        ReloadTargetSwitcher();

        // 展開トグルのシェブロン回転。
        OpsToggle.Checked += (_, _) => OpsChevron.Angle = 180;
        OpsToggle.Unchecked += (_, _) => OpsChevron.Angle = 0;
        LogToggle.Checked += (_, _) => LogChevron.Angle = 90;
        LogToggle.Unchecked += (_, _) => LogChevron.Angle = 0;

        Loaded += OnLoaded;
        Closed += (_, _) => _state.Dispose();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _state.Hotkeys.Attach(this);
        _state.Hotkeys.HotkeyPressed += OnHotkeyPressed;
        var failures = _state.Hotkeys.RegisterAll(_state.Library.Macros);
        if (failures > 0)
            Log($"注意: {failures} 件のショートカットを登録できませんでした（他アプリと競合の可能性）。");

        // 連携モードを適用（AviUtl/AviUtl2 のときは監視開始）。
        _state.DropService.Log += OnDropLog;
        _state.ApplyIntegration();
        UpdateModeLabel();

        Log("準備完了。マクロを選択して実行、またはショートカットで起動できます。");
    }

    private void OnDropLog(string message) =>
        Dispatcher.BeginInvoke(new Action(() => Log($"[連携] {message}")));

    private void OnOpenIntegration(object sender, RoutedEventArgs e)
    {
        var window = new Views.IntegrationSettingsWindow(_state) { Owner = this };
        if (window.ShowDialog() == true)
        {
            UpdateModeLabel();
            Log("連携設定を更新しました。");
        }
        else
        {
            // キャンセル時もテスト投げで一時変更した監視状態を確定設定へ戻す。
            _state.ApplyIntegration();
        }
    }

    private void UpdateModeLabel()
    {
        ModeLabel.Text = _state.Integration.ActiveMode switch
        {
            VAdapter.Core.Models.IntegrationMode.AviUtl => "連携: AviUtl",
            VAdapter.Core.Models.IntegrationMode.AviUtl2 => "連携: AviUtl2",
            _ => "連携: マクロ動作ベース",
        };
    }

    // --- 一覧の更新 ---

    private void ReloadRows()
    {
        _rows.Clear();
        foreach (var macro in _state.Library.Macros)
            _rows.Add(new MacroRow(macro, _state.Library));
    }

    // --- 送信先切り替え ---

    private void ReloadTargetSwitcher()
    {
        _suppressToggleEvents = true;
        TargetSwitcher.Children.Clear();

        // 選択中の対象アプリが削除されていたら解除。
        if (_activeTargetId is not null && _state.Library.FindTarget(_activeTargetId) is null)
            _activeTargetId = null;

        var pillStyle = (Style)FindResource("TargetPillToggle");
        foreach (var target in _state.Library.Targets)
        {
            var toggle = new ToggleButton
            {
                Style = pillStyle,
                Content = target.Name,
                Tag = target.Id,
                ToolTip = target.Name,
                IsChecked = target.Id == _activeTargetId,
            };
            toggle.Checked += OnTargetToggled;
            toggle.Unchecked += OnTargetToggled;
            TargetSwitcher.Children.Add(toggle);
        }

        if (_state.Library.Targets.Count == 0)
        {
            TargetSwitcher.Children.Add(new TextBlock
            {
                Text = "（対象アプリ未登録）",
                Foreground = (System.Windows.Media.Brush)FindResource("B.TextMuted"),
                FontSize = 11,
                Margin = new Thickness(2, 6, 2, 2),
                TextWrapping = TextWrapping.Wrap,
            });
        }

        _suppressToggleEvents = false;
    }

    private void OnTargetToggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleEvents || sender is not ToggleButton toggle)
            return;

        if (toggle.IsChecked == true)
        {
            _activeTargetId = toggle.Tag as string;

            // 排他選択: 他のトグルを解除。
            _suppressToggleEvents = true;
            foreach (var child in TargetSwitcher.Children)
            {
                if (child is ToggleButton other && !ReferenceEquals(other, toggle))
                    other.IsChecked = false;
            }
            _suppressToggleEvents = false;

            var name = _state.Library.FindTarget(_activeTargetId)?.Name ?? "(不明)";
            Log($"送信先を「{name}」に固定しました。");
        }
        else if (toggle.Tag as string == _activeTargetId)
        {
            // 自身を解除 → 自動判定へ。
            _activeTargetId = null;
            Log("送信先を自動判定に戻しました。");
        }
    }

    private void OnTogglePin(object sender, RoutedEventArgs e)
    {
        Topmost = PinButton.IsChecked == true;
        Log(Topmost ? "ウィンドウを最前面に固定しました。" : "最前面固定を解除しました。");
    }

    private void OnOpenHelp(object sender, RoutedEventArgs e)
    {
        var window = new Views.HelpWindow { Owner = this };
        window.ShowDialog();
    }

    private void OnClearActiveTarget(object sender, RoutedEventArgs e)
    {
        if (_activeTargetId is null)
            return;
        _activeTargetId = null;
        ReloadTargetSwitcher();
        Log("送信先を自動判定に戻しました。");
    }

    private MacroRow? SelectedRow => MacroList.SelectedItem as MacroRow;

    // --- ボタン操作 ---

    private void OnNewMacro(object sender, RoutedEventArgs e)
    {
        var macro = new Macro { Name = "新しいマクロ" };
        if (EditMacro(macro, isNew: true))
        {
            _state.Library.Macros.Add(macro);
            Persist();
            ReloadRows();
            SelectMacro(macro);
        }
    }

    private void OnEditMacro(object sender, RoutedEventArgs e)
    {
        var row = SelectedRow;
        if (row is null) return;
        if (EditMacro(row.Model, isNew: false))
        {
            Persist();
            row.RefreshAll();
        }
    }

    private void OnDuplicateMacro(object sender, RoutedEventArgs e)
    {
        var row = SelectedRow;
        if (row is null) return;

        // JSON 経由でディープコピーし ID を再採番。
        var json = VAdapter.Core.Serialization.VAdapterJson.Serialize(row.Model);
        var copy = VAdapter.Core.Serialization.VAdapterJson.Deserialize<Macro>(json)!;
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Name += "（コピー）";
        copy.IsBuiltIn = false;
        copy.Shortcut = null; // 競合回避のためショートカットは引き継がない。
        foreach (var script in copy.Scripts)
        {
            script.Id = Guid.NewGuid().ToString("N");
            foreach (var instr in script.Instructions)
                instr.Id = Guid.NewGuid().ToString("N");
        }

        _state.Library.Macros.Add(copy);
        Persist();
        ReloadRows();
        SelectMacro(copy);
    }

    private void OnDeleteMacro(object sender, RoutedEventArgs e)
    {
        var row = SelectedRow;
        if (row is null) return;

        if (MessageBox.Show($"マクロ「{row.Name}」を削除しますか？", "確認",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _state.Library.Macros.Remove(row.Model);
        Persist();
        ReloadRows();
    }

    private async void OnRunMacro(object sender, RoutedEventArgs e)
    {
        var row = SelectedRow;
        if (row is null)
        {
            Log("実行するマクロを選択してください。");
            return;
        }
        await RunMacroAsync(row.Model);
    }

    private async void OnHotkeyPressed(Macro macro) => await RunMacroAsync(macro);

    private async Task RunMacroAsync(Macro macro)
    {
        var progress = new Progress<string>(Log);
        try
        {
            var result = await _state.Runner.RunAsync(
                macro, _state.Library, progress, preferredTargetId: _activeTargetId);
            if (!result.Success)
                Log($"=> 失敗: {result.Error}");
            else
                Log("=> 成功");
        }
        catch (Exception ex)
        {
            Log($"=> 例外: {ex.Message}");
        }
    }

    private void OnEnabledChanged(object sender, RoutedEventArgs e)
    {
        // チェック変更を保存しホットキーを再登録。
        Persist();
    }

    // --- インポート / エクスポート ---

    private void OnImport(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = $"V-Adapter マクロ (*{MacroBundleService.FileExtension})|*{MacroBundleService.FileExtension}|JSON (*.json)|*.json",
            Title = "マクロのインポート",
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var json = File.ReadAllText(dialog.FileName);
            var bundle = MacroBundleService.DeserializeBundle(json);
            var imported = MacroBundleService.ImportInto(bundle, _state.Library);
            Persist();
            ReloadRows();
            ReloadTargetSwitcher();
            Log($"{imported.Count} 件のマクロをインポートしました。");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"インポートに失敗しました: {ex.Message}", "エラー",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        var row = SelectedRow;
        if (row is null)
        {
            Log("エクスポートするマクロを選択してください。");
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = $"V-Adapter マクロ (*{MacroBundleService.FileExtension})|*{MacroBundleService.FileExtension}",
            Title = "マクロのエクスポート",
            FileName = row.Name + MacroBundleService.FileExtension,
        };
        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            var bundle = MacroBundleService.CreateBundle(new[] { row.Model }, _state.Library);
            File.WriteAllText(dialog.FileName, MacroBundleService.SerializeBundle(bundle));
            Log($"「{row.Name}」をエクスポートしました: {dialog.FileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"エクスポートに失敗しました: {ex.Message}", "エラー",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnManageTargets(object sender, RoutedEventArgs e)
    {
        var window = new TargetManagerWindow(_state.Library) { Owner = this };
        window.ShowDialog();
        Persist();
        // 対象アプリ名・構成が変わった可能性があるので表示更新。
        foreach (var row in _rows)
            row.RefreshAll();
        ReloadTargetSwitcher();
    }

    // --- 共通 ---

    private bool EditMacro(Macro macro, bool isNew)
    {
        // 編集中はグローバルショートカットを解除し、キー設定の競合を防ぐ。
        _state.Hotkeys.Suspend();
        try
        {
            var window = new MacroEditWindow(macro, _state.Library) { Owner = this };
            return window.ShowDialog() == true;
        }
        finally
        {
            // Persist() 側で再登録されるが、キャンセル時にも確実に戻す。
            _state.Hotkeys.RegisterAll(_state.Library.Macros);
        }
    }

    private void SelectMacro(Macro macro)
    {
        var row = _rows.FirstOrDefault(r => r.Model.Id == macro.Id);
        if (row is not null)
            MacroList.SelectedItem = row;
    }

    private void Persist() => _state.SaveAndRebindHotkeys();

    private int _logCount;

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        LogBox.AppendText(line);
        LogBox.ScrollToEnd();
        _logCount++;
        LogCountText.Text = $"{_logCount} 件";
    }
}
