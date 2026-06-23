using System.Windows;
using System.Windows.Controls;
using VAdapter.Core.Models;

namespace VAdapter.App.Views;

public partial class TargetManagerWindow : Window
{
    private readonly MacroLibrary _library;
    private TargetApplication? _current;

    public TargetManagerWindow(MacroLibrary library)
    {
        InitializeComponent();
        _library = library;
        ReloadList();
    }

    private void ReloadList()
    {
        TargetList.ItemsSource = null;
        TargetList.ItemsSource = _library.Targets;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 別項目に切り替える前に現在の編集内容を保存。
        ApplyToCurrent();

        _current = TargetList.SelectedItem as TargetApplication;
        LoadEditor(_current);
    }

    private void LoadEditor(TargetApplication? target)
    {
        EditorPanel.IsEnabled = target is not null;
        if (target is null)
        {
            NameBox.Text = ProcessBox.Text = TitleBox.Text = ClassBox.Text = string.Empty;
            RegexCheck.IsChecked = false;
            CoordCombo.SelectedIndex = 0;
            return;
        }

        NameBox.Text = target.Name;
        ProcessBox.Text = target.ProcessName ?? string.Empty;
        TitleBox.Text = target.WindowTitlePattern ?? string.Empty;
        ClassBox.Text = target.WindowClass ?? string.Empty;
        RegexCheck.IsChecked = target.TitleIsRegex;
        CoordCombo.SelectedIndex = target.CoordinateMode == CoordinateMode.Absolute ? 1 : 0;
    }

    private void ApplyToCurrent()
    {
        if (_current is null)
            return;

        _current.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "(無名)" : NameBox.Text.Trim();
        _current.ProcessName = NullIfEmpty(ProcessBox.Text);
        _current.WindowTitlePattern = NullIfEmpty(TitleBox.Text);
        _current.WindowClass = NullIfEmpty(ClassBox.Text);
        _current.TitleIsRegex = RegexCheck.IsChecked == true;
        _current.CoordinateMode = (CoordCombo.SelectedItem as ComboBoxItem)?.Tag as string == "Absolute"
            ? CoordinateMode.Absolute
            : CoordinateMode.Relative;
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        ApplyToCurrent();
        ReloadList();
        TargetList.SelectedItem = _current;
    }

    private void OnNew(object sender, RoutedEventArgs e)
    {
        ApplyToCurrent();
        var target = new TargetApplication { Name = "新しい対象アプリ" };
        _library.Targets.Add(target);
        ReloadList();
        TargetList.SelectedItem = target;
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (_current is null)
            return;

        // このアプリを参照しているスクリプトがあるか確認。
        var refCount = _library.Macros
            .SelectMany(m => m.Scripts)
            .Count(s => s.TargetApplicationId == _current.Id);
        var msg = refCount > 0
            ? $"「{_current.Name}」を削除しますか？\n{refCount} 件のスクリプトの割当が共通（対象なし）に変更されます。"
            : $"「{_current.Name}」を削除しますか？";
        if (MessageBox.Show(msg, "確認", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        foreach (var script in _library.Macros.SelectMany(m => m.Scripts).Where(s => s.TargetApplicationId == _current.Id))
            script.TargetApplicationId = null;

        _library.Targets.Remove(_current);
        _current = null;
        ReloadList();
        LoadEditor(null);
    }

    private void OnPickWindow(object sender, RoutedEventArgs e)
    {
        if (_current is null)
            return;

        var picker = new WindowPickerWindow { Owner = this };
        if (picker.ShowDialog() == true && picker.SelectedWindow is { } w)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text) || NameBox.Text == "新しい対象アプリ")
                NameBox.Text = string.IsNullOrEmpty(w.ProcessName) ? w.Title : w.ProcessName;
            ProcessBox.Text = w.ProcessName;
            TitleBox.Text = w.Title;
            ClassBox.Text = w.ClassName;
            RegexCheck.IsChecked = false;
        }
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        ApplyToCurrent();
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
