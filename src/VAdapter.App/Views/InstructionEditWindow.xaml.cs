using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VAdapter.App.Services;
using VAdapter.Automation.Windows;
using VAdapter.Core.Models;
using VAdapter.Core.Serialization;

namespace VAdapter.App.Views;

public partial class InstructionEditWindow : Window
{
    private readonly Instruction _working;
    private readonly TargetApplication? _target;
    private readonly WindowLocator _locator = new();

    /// <summary>編集後の命令（OK 時のみ有効）。</summary>
    public Instruction? Result { get; private set; }

    public InstructionEditWindow(Instruction instruction, TargetApplication? target)
    {
        InitializeComponent();
        _working = DeepClone(instruction);
        _target = target;
        LoadFields();
    }

    private void LoadFields()
    {
        switch (_working)
        {
            case WaitInstruction wait:
                TitleText.Text = "待機";
                WaitPanel.Visibility = Visibility.Visible;
                WaitMsBox.Text = wait.DurationMs.ToString();
                break;

            case ClickInstruction click:
                TitleText.Text = "クリック";
                ClickPanel.Visibility = Visibility.Visible;
                ClickModeCombo.SelectedIndex = click.CoordinateModeOverride switch
                {
                    CoordinateMode.Relative => 1,
                    CoordinateMode.Absolute => 2,
                    _ => 0,
                };
                ClickXBox.Text = click.X.ToString();
                ClickYBox.Text = click.Y.ToString();
                ClickButtonCombo.SelectedIndex = (int)click.Button;
                ClickAnchorCombo.SelectedIndex = (int)click.Anchor;
                break;

            case SendKeysInstruction keys:
                TitleText.Text = "キー送信";
                SendKeysPanel.Visibility = Visibility.Visible;
                UpdateKeyText(keys.KeyCombination);
                break;

            case WaitForWindowInstruction waitWin:
                TitleText.Text = "ウィンドウ表示待ち";
                WaitWindowPanel.Visibility = Visibility.Visible;
                WaitWindowMsBox.Text = waitWin.TimeoutMs.ToString();
                break;

            case WaitForActivationInstruction waitAct:
                TitleText.Text = "アプリ切替待ち";
                WaitActivationPanel.Visibility = Visibility.Visible;
                WaitActivationMsBox.Text = waitAct.TimeoutMs.ToString();
                break;

            case WaitForDialogInstruction waitDlg:
                TitleText.Text = "ダイアログ表示待ち";
                WaitDialogPanel.Visibility = Visibility.Visible;
                DialogTitleBox.Text = waitDlg.TitleContains ?? string.Empty;
                DialogClassBox.Text = waitDlg.DialogClass;
                DialogForegroundCheck.IsChecked = waitDlg.BringToForeground;
                WaitDialogMsBox.Text = waitDlg.TimeoutMs.ToString();
                break;

            case SwitchTargetInstruction switchTgt:
                TitleText.Text = "操作対象の切り替え";
                SwitchTargetPanel.Visibility = Visibility.Visible;
                SwitchTargetCombo.SelectedIndex = switchTgt.Target == SwitchTargetKind.AppWindow ? 1 : 0;
                SwitchTitleBox.Text = switchTgt.TitleContains ?? string.Empty;
                SwitchClassBox.Text = switchTgt.DialogClass;
                SwitchMsBox.Text = switchTgt.TimeoutMs.ToString();
                UpdateSwitchOptionsState();
                break;

            case WaitForTextInstruction waitText:
                TitleText.Text = "テキスト表示待ち";
                WaitTextPanel.Visibility = Visibility.Visible;
                WaitTextBox.Text = waitText.Text;
                WaitTextMsBox.Text = waitText.TimeoutMs.ToString();
                if (waitText.Region is { } r)
                {
                    WholeWindowCheck.IsChecked = false;
                    RegionXBox.Text = r.X.ToString();
                    RegionYBox.Text = r.Y.ToString();
                    RegionWBox.Text = r.Width.ToString();
                    RegionHBox.Text = r.Height.ToString();
                }
                else
                {
                    WholeWindowCheck.IsChecked = true;
                }
                UpdateRegionPanelState();
                break;
        }
    }

    // --- キー取得 ---

    private void OnKeyCapture(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var combo = KeyCaptureHelper.FromKeyEvent(e);
        if (combo is null)
            return;
        if (_working is SendKeysInstruction keys)
        {
            keys.KeyCombination = combo;
            UpdateKeyText(combo);
        }
    }

    private void OnClearKey(object sender, RoutedEventArgs e)
    {
        if (_working is SendKeysInstruction keys)
        {
            keys.KeyCombination = new KeyCombination();
            UpdateKeyText(keys.KeyCombination);
        }
    }

    private void UpdateKeyText(KeyCombination combo) =>
        KeyBox.Text = combo.IsValid ? combo.ToString() : "(未設定)";

    // --- 座標取得 ---

    private async void OnCaptureCoordinate(object sender, RoutedEventArgs e)
    {
        var mode = SelectedClickMode();
        var effective = mode ?? _target?.CoordinateMode ?? CoordinateMode.Absolute;
        var anchor = SelectedAnchor();

        CaptureCoordButton.IsEnabled = false;
        var saved = MinimizeWindowChain();
        try
        {
            // 最小化の反映を待ってから捕捉開始。
            await Task.Delay(250);
            var (screenX, screenY) = await CoordinateCapture.CaptureNextClickAsync();

            if (effective == CoordinateMode.Absolute)
            {
                ClickXBox.Text = screenX.ToString();
                ClickYBox.Text = screenY.ToString();
            }
            else
            {
                // クリック位置直下のトップレベルウィンドウを基準にする（ダイアログ等も可）。
                var window = _locator.GetTopLevelWindowAt(screenX, screenY);
                if (window is null)
                {
                    MessageBox.Show("クリック位置のウィンドウを特定できませんでした。", "確認",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var (cx, cy) = WindowGeometry.ScreenToClient(window.Handle, screenX, screenY);
                var (_, _, clientW, clientH) = WindowGeometry.GetClientAreaOnScreen(window.Handle);
                var (anchorX, anchorY) = WindowGeometry.AnchorPoint(clientW, clientH, anchor);

                // アンカーからのオフセットとして保存。
                ClickXBox.Text = (cx - anchorX).ToString();
                ClickYBox.Text = (cy - anchorY).ToString();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"座標の取得に失敗しました: {ex.Message}", "エラー",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RestoreWindowChain(saved);
            CaptureCoordButton.IsEnabled = true;
        }
    }

    private ClickAnchor SelectedAnchor() =>
        (ClickAnchor)Math.Max(0, ClickAnchorCombo.SelectedIndex);

    // --- ダイアログのウィンドウクラスをクリックで取得 ---

    private async void OnCaptureDialogClass(object sender, RoutedEventArgs e)
        => await CaptureWindowClassAsync(DialogClassBox, DialogTitleBox);

    private async void OnCaptureSwitchClass(object sender, RoutedEventArgs e)
        => await CaptureWindowClassAsync(SwitchClassBox, SwitchTitleBox);

    private async Task CaptureWindowClassAsync(
        System.Windows.Controls.TextBox classBox, System.Windows.Controls.TextBox? titleBox)
    {
        var saved = MinimizeWindowChain();
        try
        {
            await Task.Delay(250);
            var (screenX, screenY) = await CoordinateCapture.CaptureNextClickAsync();
            var window = _locator.GetTopLevelWindowAt(screenX, screenY);
            if (window is null)
            {
                MessageBox.Show("クリック位置のウィンドウを特定できませんでした。", "確認",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            classBox.Text = window.ClassName;
            if (titleBox is not null && string.IsNullOrWhiteSpace(titleBox.Text) && !string.IsNullOrEmpty(window.Title))
                titleBox.Text = window.Title;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ウィンドウクラスの取得に失敗しました: {ex.Message}", "エラー",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RestoreWindowChain(saved);
        }
    }

    /// <summary>
    /// このウィンドウと所有者チェーン（編集ダイアログ・メインウィンドウ）を最小化し、
    /// 復元用に元の状態を返す。モーダルダイアログを Visibility で隠すと DialogResult 例外が
    /// 発生し得るため、最小化で画面を空ける。
    /// </summary>
    private List<(Window Window, WindowState State)> MinimizeWindowChain()
    {
        var saved = new List<(Window, WindowState)>();
        Window? w = this;
        while (w is not null)
        {
            saved.Add((w, w.WindowState));
            w.WindowState = WindowState.Minimized;
            w = w.Owner;
        }
        return saved;
    }

    private void RestoreWindowChain(List<(Window Window, WindowState State)> saved)
    {
        // 所有者から順に戻すため逆順で復元。
        for (int i = saved.Count - 1; i >= 0; i--)
            saved[i].Window.WindowState = saved[i].State;
        Activate();
    }

    private CoordinateMode? SelectedClickMode() =>
        ((ClickModeCombo.SelectedItem as ComboBoxItem)?.Tag as string) switch
        {
            "relative" => CoordinateMode.Relative,
            "absolute" => CoordinateMode.Absolute,
            _ => null,
        };

    private async void OnDragRegion(object sender, RoutedEventArgs e)
    {
        if (_target is null)
        {
            MessageBox.Show("領域指定には対象アプリの割当が必要です（範囲はウィンドウ基準の相対座標で保存されます）。",
                "確認", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var window = _locator.FindForActivation(_target);
        if (window is null)
        {
            MessageBox.Show("対象アプリのウィンドウが見つかりません。アプリを起動してから再試行してください。",
                "確認", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var saved = MinimizeWindowChain();
        try
        {
            await Task.Delay(200);
            var overlay = new RegionCaptureWindow();
            if (overlay.ShowDialog() == true && overlay.SelectedScreenRect is { } r)
            {
                // スクリーン物理px → 対象ウィンドウのクライアント相対へ変換。
                var (cx, cy) = WindowGeometry.ScreenToClient(window.Handle, r.X, r.Y);
                WholeWindowCheck.IsChecked = false;
                RegionXBox.Text = cx.ToString();
                RegionYBox.Text = cy.ToString();
                RegionWBox.Text = r.Width.ToString();
                RegionHBox.Text = r.Height.ToString();
                UpdateRegionPanelState();
            }
        }
        finally
        {
            RestoreWindowChain(saved);
        }
    }

    private void OnSwitchTargetChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => UpdateSwitchOptionsState();

    private void UpdateSwitchOptionsState()
    {
        var isDialog = (SwitchTargetCombo.SelectedItem as ComboBoxItem)?.Tag as string != "AppWindow";
        if (SwitchDialogOptions is not null)
            SwitchDialogOptions.IsEnabled = isDialog;
    }

    private void OnWholeWindowToggled(object sender, RoutedEventArgs e) => UpdateRegionPanelState();

    private void UpdateRegionPanelState() =>
        RegionPanel.IsEnabled = WholeWindowCheck.IsChecked != true;

    // --- 確定 ---

    private void OnOk(object sender, RoutedEventArgs e)
    {
        switch (_working)
        {
            case WaitInstruction wait:
                if (!TryParse(WaitMsBox.Text, out var ms)) { Warn("待機時間"); return; }
                wait.DurationMs = ms;
                break;

            case ClickInstruction click:
                if (!TryParse(ClickXBox.Text, out var cx) || !TryParse(ClickYBox.Text, out var cy))
                { Warn("座標"); return; }
                click.X = cx;
                click.Y = cy;
                click.CoordinateModeOverride = SelectedClickMode();
                click.Anchor = (ClickAnchor)Math.Max(0, ClickAnchorCombo.SelectedIndex);
                click.Button = (VAdapter.Core.Models.MouseButton)Math.Max(0, ClickButtonCombo.SelectedIndex);
                break;

            case SendKeysInstruction keys:
                if (!keys.KeyCombination.IsValid) { Warn("キー（未設定）"); return; }
                break;

            case WaitForWindowInstruction waitWin:
                if (!TryParse(WaitWindowMsBox.Text, out var wms)) { Warn("タイムアウト"); return; }
                waitWin.TimeoutMs = wms;
                break;

            case WaitForActivationInstruction waitAct:
                if (!TryParse(WaitActivationMsBox.Text, out var ams)) { Warn("タイムアウト"); return; }
                waitAct.TimeoutMs = ams;
                break;

            case WaitForDialogInstruction waitDlg:
                if (!TryParse(WaitDialogMsBox.Text, out var dms)) { Warn("タイムアウト"); return; }
                waitDlg.TimeoutMs = dms;
                waitDlg.TitleContains = string.IsNullOrWhiteSpace(DialogTitleBox.Text) ? null : DialogTitleBox.Text.Trim();
                waitDlg.DialogClass = string.IsNullOrWhiteSpace(DialogClassBox.Text) ? "#32770" : DialogClassBox.Text.Trim();
                waitDlg.BringToForeground = DialogForegroundCheck.IsChecked == true;
                break;

            case SwitchTargetInstruction switchTgt:
                switchTgt.Target = (SwitchTargetCombo.SelectedItem as ComboBoxItem)?.Tag as string == "AppWindow"
                    ? SwitchTargetKind.AppWindow
                    : SwitchTargetKind.Dialog;
                if (switchTgt.Target == SwitchTargetKind.Dialog)
                {
                    if (!TryParse(SwitchMsBox.Text, out var sms)) { Warn("タイムアウト"); return; }
                    switchTgt.TimeoutMs = sms;
                    switchTgt.TitleContains = string.IsNullOrWhiteSpace(SwitchTitleBox.Text) ? null : SwitchTitleBox.Text.Trim();
                    switchTgt.DialogClass = string.IsNullOrWhiteSpace(SwitchClassBox.Text) ? "#32770" : SwitchClassBox.Text.Trim();
                }
                break;

            case WaitForTextInstruction waitText:
                if (string.IsNullOrWhiteSpace(WaitTextBox.Text)) { Warn("検出テキスト"); return; }
                if (!TryParse(WaitTextMsBox.Text, out var tms)) { Warn("タイムアウト"); return; }
                waitText.Text = WaitTextBox.Text.Trim();
                waitText.TimeoutMs = tms;
                if (WholeWindowCheck.IsChecked == true)
                {
                    waitText.Region = null;
                }
                else
                {
                    if (!TryParse(RegionXBox.Text, out var rx) || !TryParse(RegionYBox.Text, out var ry) ||
                        !TryParse(RegionWBox.Text, out var rw) || !TryParse(RegionHBox.Text, out var rh))
                    { Warn("領域"); return; }
                    waitText.Region = new RelativeRect { X = rx, Y = ry, Width = rw, Height = rh };
                }
                break;
        }

        Result = _working;
        DialogResult = true;
    }

    private static bool TryParse(string? s, out int value) =>
        int.TryParse(s?.Trim(), out value);

    private static void Warn(string field) =>
        MessageBox.Show($"{field} の値を確認してください。", "確認",
            MessageBoxButton.OK, MessageBoxImage.Warning);

    private static Instruction DeepClone(Instruction i) =>
        VAdapterJson.Deserialize<Instruction>(VAdapterJson.Serialize(i))!;
}
