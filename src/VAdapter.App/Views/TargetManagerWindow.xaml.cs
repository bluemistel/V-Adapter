using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using VAdapter.Core.Models;
using VAdapter.Core.Serialization;

namespace VAdapter.App.Views;

public partial class TargetManagerWindow : Window
{
    private readonly MacroLibrary _library;

    // 編集は作業コピーに対して行い、「保存して閉じる」でのみ本体へ反映する。
    // これにより「閉じる」での取り消し（破棄）が可能になる。
    private readonly ObservableCollection<TargetApplication> _working;
    private TargetApplication? _current;

    private bool _loading;      // LoadEditor 中の変更通知を無視するためのガード
    private bool _dirty;        // 未保存の変更があるか
    private bool _committing;    // 保存して閉じる中（Closing で確認をスキップ）

    public TargetManagerWindow(MacroLibrary library)
    {
        InitializeComponent();
        _library = library;

        _working = new ObservableCollection<TargetApplication>(
            library.Targets.Select(Clone));
        TargetList.ItemsSource = _working;

        // 最初に一番上の対象アプリを選択した状態にする。
        if (_working.Count > 0)
            TargetList.SelectedIndex = 0;
        else
            LoadEditor(null);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 別項目に切り替える前に現在の編集内容を作業コピーへ保存。
        ApplyToCurrent();

        _current = TargetList.SelectedItem as TargetApplication;
        LoadEditor(_current);
    }

    private void LoadEditor(TargetApplication? target)
    {
        _loading = true;
        try
        {
            EditorPanel.IsEnabled = target is not null;
            if (target is null)
            {
                NameBox.Text = ProcessBox.Text = TitleBox.Text = ClassBox.Text = ExePathBox.Text = string.Empty;
                RegexCheck.IsChecked = false;
                CoordCombo.SelectedIndex = 0;
                return;
            }

            NameBox.Text = target.Name;
            ProcessBox.Text = target.ProcessName ?? string.Empty;
            TitleBox.Text = target.WindowTitlePattern ?? string.Empty;
            ClassBox.Text = target.WindowClass ?? string.Empty;
            ExePathBox.Text = target.ExecutablePath ?? string.Empty;
            RegexCheck.IsChecked = target.TitleIsRegex;
            CoordCombo.SelectedIndex = target.CoordinateMode == CoordinateMode.Absolute ? 1 : 0;
        }
        finally
        {
            _loading = false;
        }
    }

    private void ApplyToCurrent()
    {
        if (_current is null)
            return;

        _current.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "(無名)" : NameBox.Text.Trim();
        _current.ProcessName = NullIfEmpty(ProcessBox.Text);
        _current.WindowTitlePattern = NullIfEmpty(TitleBox.Text);
        _current.WindowClass = NullIfEmpty(ClassBox.Text);
        _current.ExecutablePath = NullIfEmpty(ExePathBox.Text);
        _current.TitleIsRegex = RegexCheck.IsChecked == true;
        _current.CoordinateMode = (CoordCombo.SelectedItem as ComboBoxItem)?.Tag as string == "Absolute"
            ? CoordinateMode.Absolute
            : CoordinateMode.Relative;

        // 一覧の表示名を最新化（ItemsSource の入れ替えはせず選択を保つ）。
        TargetList.Items.Refresh();
    }

    /// <summary>編集フィールドが変更されたら未保存フラグを立てる（プログラムによる読込中は無視）。</summary>
    private void OnEditorChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading)
            _dirty = true;
    }

    private void OnNew(object sender, RoutedEventArgs e)
    {
        ApplyToCurrent();
        var target = new TargetApplication { Name = "新しい対象アプリ" };
        _working.Add(target);
        _dirty = true;
        TargetList.SelectedItem = target;
    }

    private void OnDelete(object sender, RoutedEventArgs e)
    {
        if (_current is null)
            return;

        // このアプリを参照しているスクリプトがあるか確認（本体側の現状で判定）。
        var refCount = _library.Macros
            .SelectMany(m => m.Scripts)
            .Count(s => s.TargetApplicationId == _current.Id);
        var msg = refCount > 0
            ? $"「{_current.Name}」を削除しますか？\n保存時、{refCount} 件のスクリプトの割当が共通（対象なし）に変更されます。"
            : $"「{_current.Name}」を削除しますか？";
        if (MessageBox.Show(msg, "確認", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _working.Remove(_current);
        _current = null;
        _dirty = true;
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

    private void OnBrowseExe(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "実行ファイルを選択",
            Filter = "実行ファイル (*.exe)|*.exe|すべてのファイル (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) == true)
            ExePathBox.Text = dialog.FileName;
    }

    // --- 保存 / 終了 ---

    private void OnSaveAndClose(object sender, RoutedEventArgs e)
    {
        ApplyToCurrent();
        Commit();
        _committing = true;
        DialogResult = true; // ウィンドウを閉じる（OnClosing は _committing で確認をスキップ）
    }

    private void OnCloseButton(object sender, RoutedEventArgs e) => Close();

    private void OnClosing(object sender, CancelEventArgs e)
    {
        if (_committing)
            return; // 保存して閉じる

        if (_dirty)
        {
            var r = MessageBox.Show(
                "保存されていない設定があります。設定を取り消しますか？",
                "確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes)
                e.Cancel = true; // 「いいえ」→ 閉じずに編集を続ける
            // 「はい」→ 取り消して閉じる（Commit しない）
        }
    }

    /// <summary>作業コピーを本体ライブラリへ反映する。削除された対象を参照するスクリプトは共通へ戻す。</summary>
    private void Commit()
    {
        var keptIds = _working.Select(t => t.Id).ToHashSet();
        foreach (var script in _library.Macros.SelectMany(m => m.Scripts))
        {
            if (script.TargetApplicationId is { } id && !keptIds.Contains(id))
                script.TargetApplicationId = null;
        }

        _library.Targets.Clear();
        foreach (var t in _working)
            _library.Targets.Add(t);

        _dirty = false;
    }

    private static TargetApplication Clone(TargetApplication t) =>
        VAdapterJson.Deserialize<TargetApplication>(VAdapterJson.Serialize(t))!;

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
