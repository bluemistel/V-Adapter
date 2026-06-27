using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using VAdapter.App.Services;
using VAdapter.App.ViewModels;
using VAdapter.Core.Models;
using VAdapter.Core.Serialization;

namespace VAdapter.App.Views;

public partial class MacroEditWindow : Window
{
    private readonly Macro _original;
    private readonly MacroLibrary _library;
    private readonly Macro _working;
    private readonly ObservableCollection<ScriptRow> _scriptRows;
    private ObservableCollection<Instruction>? _currentInstructions;

    public MacroEditWindow(Macro macro, MacroLibrary library)
    {
        InitializeComponent();
        _original = macro;
        _library = library;
        _working = DeepClone(macro);

        NameBox.Text = _working.Name;
        UpdateShortcutText();

        _scriptRows = new ObservableCollection<ScriptRow>(
            _working.Scripts.Select(s => new ScriptRow(s, _library)));
        ScriptList.ItemsSource = _scriptRows;

        if (_scriptRows.Count > 0)
            ScriptList.SelectedIndex = 0;
    }

    // --- ショートカット取得 ---

    private void OnShortcutGotFocus(object sender, RoutedEventArgs e)
    {
        if (_working.Shortcut is not { IsValid: true })
            ShortcutBox.Text = "(キーを押す…)";
    }

    private void OnShortcutLostFocus(object sender, RoutedEventArgs e) => UpdateShortcutText();

    private void OnShortcutKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var combo = KeyCaptureHelper.FromKeyEvent(e);
        if (combo is null)
            return;
        _working.Shortcut = combo;
        UpdateShortcutText();
    }

    private void OnClearShortcut(object sender, RoutedEventArgs e)
    {
        _working.Shortcut = null;
        UpdateShortcutText();
    }

    private void UpdateShortcutText() =>
        ShortcutBox.Text = _working.Shortcut is { IsValid: true } s ? s.ToString() : "(なし)";

    // --- スクリプト管理 ---

    private ScriptRow? SelectedScriptRow => ScriptList.SelectedItem as ScriptRow;
    private MacroScript? CurrentScript => SelectedScriptRow?.Model;

    private void OnScriptSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var script = CurrentScript;
        InstructionArea.IsEnabled = script is not null;

        if (script is null)
        {
            InstructionList.ItemsSource = null;
            _currentInstructions = null;
            return;
        }

        _currentInstructions = new ObservableCollection<Instruction>(script.Instructions);
        InstructionList.ItemsSource = _currentInstructions;
    }

    private void OnAddScriptClick(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = (UIElement)sender, Placement = PlacementMode.Bottom };

        // 既に使われている対象（共通含む）は除外。
        var usedIds = _scriptRows.Select(r => r.Model.TargetApplicationId).ToHashSet();

        if (!usedIds.Contains(null))
            menu.Items.Add(BuildScriptMenuItem("共通（対象アプリなし）", null));

        foreach (var target in _library.Targets)
        {
            if (usedIds.Contains(target.Id))
                continue;
            menu.Items.Add(BuildScriptMenuItem(target.Name, target.Id));
        }

        if (menu.Items.Count == 0)
        {
            MessageBox.Show("追加できる対象アプリがありません。先に「対象アプリ管理」で登録してください。",
                "確認", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        menu.IsOpen = true;
    }

    private MenuItem BuildScriptMenuItem(string header, string? targetId)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) =>
        {
            var script = new MacroScript { TargetApplicationId = targetId };
            var row = new ScriptRow(script, _library);
            _scriptRows.Add(row);
            ScriptList.SelectedItem = row;
        };
        return item;
    }

    private void OnDeleteScript(object sender, RoutedEventArgs e)
    {
        var row = SelectedScriptRow;
        if (row is null)
            return;
        if (MessageBox.Show($"スクリプト「{row.Display}」を削除しますか？", "確認",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        _scriptRows.Remove(row);
    }

    // --- 命令の追加・編集・並べ替え ---

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.ContextMenu is { } menu)
        {
            menu.PlacementTarget = fe;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private void OnAddInstruction(object sender, RoutedEventArgs e)
    {
        if (_currentInstructions is null) return;
        if (sender is not MenuItem item || item.Tag is not string kind)
            return;

        Instruction instruction = kind switch
        {
            WaitInstruction.KindId => new WaitInstruction(),
            ClickInstruction.KindId => new ClickInstruction(),
            SendKeysInstruction.KindId => new SendKeysInstruction(),
            WaitForWindowInstruction.KindId => new WaitForWindowInstruction(),
            WaitForActivationInstruction.KindId => new WaitForActivationInstruction(),
            WaitForDialogInstruction.KindId => new WaitForDialogInstruction(),
            SwitchTargetInstruction.KindId => new SwitchTargetInstruction(),
            WaitForTextInstruction.KindId => new WaitForTextInstruction(),
            LaunchAppInstruction.KindId => new LaunchAppInstruction(),
            _ => throw new InvalidOperationException($"未知の命令種別: {kind}"),
        };

        var edited = EditInstruction(instruction);
        if (edited is not null)
        {
            _currentInstructions.Add(edited);
            CommitInstructions();
            InstructionList.SelectedItem = edited;
        }
    }

    private void OnEditInstruction(object sender, RoutedEventArgs e)
    {
        if (_currentInstructions is null) return;
        if (InstructionList.SelectedItem is not Instruction selected)
            return;
        var index = _currentInstructions.IndexOf(selected);
        var edited = EditInstruction(selected);
        if (edited is not null)
        {
            _currentInstructions[index] = edited;
            CommitInstructions();
            InstructionList.SelectedItem = edited;
        }
    }

    private Instruction? EditInstruction(Instruction instruction)
    {
        var target = _library.FindTarget(CurrentScript?.TargetApplicationId);
        var window = new InstructionEditWindow(instruction, target) { Owner = this };
        return window.ShowDialog() == true ? window.Result : null;
    }

    private void OnDuplicateInstruction(object sender, RoutedEventArgs e)
    {
        if (_currentInstructions is null) return;
        if (InstructionList.SelectedItem is not Instruction selected) return;

        // JSON 経由でディープコピーし ID を再採番、選択の直後に挿入。
        var copy = VAdapterJson.Deserialize<Instruction>(VAdapterJson.Serialize(selected))!;
        copy.Id = Guid.NewGuid().ToString("N");

        var index = _currentInstructions.IndexOf(selected);
        _currentInstructions.Insert(index + 1, copy);
        CommitInstructions();
        InstructionList.SelectedItem = copy;
    }

    private void OnDeleteInstruction(object sender, RoutedEventArgs e)
    {
        if (_currentInstructions is null) return;
        if (InstructionList.SelectedItem is Instruction selected)
        {
            _currentInstructions.Remove(selected);
            CommitInstructions();
        }
    }

    private void OnMoveUp(object sender, RoutedEventArgs e) => Move(-1);

    private void OnMoveDown(object sender, RoutedEventArgs e) => Move(+1);

    private void Move(int delta)
    {
        if (_currentInstructions is null) return;
        var index = InstructionList.SelectedIndex;
        var newIndex = index + delta;
        if (index < 0 || newIndex < 0 || newIndex >= _currentInstructions.Count)
            return;
        _currentInstructions.Move(index, newIndex);
        CommitInstructions();
        InstructionList.SelectedIndex = newIndex;
    }

    /// <summary>現在の命令一覧を選択中スクリプトへ書き戻し、表示を更新する。</summary>
    private void CommitInstructions()
    {
        if (_currentInstructions is null || CurrentScript is null)
            return;
        CurrentScript.Instructions = _currentInstructions.ToList();
        SelectedScriptRow?.Refresh();
    }

    // --- 確定 ---

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("名前を入力してください。", "確認", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _original.Name = NameBox.Text.Trim();
        _original.Shortcut = _working.Shortcut;
        _original.Scripts = _scriptRows.Select(r => r.Model).ToList();

        DialogResult = true;
    }

    private static Macro DeepClone(Macro m) =>
        VAdapterJson.Deserialize<Macro>(VAdapterJson.Serialize(m))!;
}
