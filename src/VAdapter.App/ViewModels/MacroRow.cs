using CommunityToolkit.Mvvm.ComponentModel;
using VAdapter.Core.Models;

namespace VAdapter.App.ViewModels;

/// <summary>マクロ一覧の 1 行表示用ラッパー。</summary>
public sealed partial class MacroRow : ObservableObject
{
    public Macro Model { get; }

    private readonly MacroLibrary _library;

    public MacroRow(Macro model, MacroLibrary library)
    {
        Model = model;
        _library = library;
    }

    public string Name => Model.Name;

    public string Kind => Model.IsBuiltIn ? "標準" : "ユーザー";

    public string ShortcutText => Model.Shortcut?.IsValid == true ? Model.Shortcut.ToString() : "(なし)";

    /// <summary>スクリプトが紐づく対象アプリ名の一覧。</summary>
    public string TargetText
    {
        get
        {
            if (Model.Scripts.Count == 0)
                return "(スクリプトなし)";

            var names = Model.Scripts
                .Select(s => s.TargetApplicationId is null
                    ? "共通"
                    : _library.FindTarget(s.TargetApplicationId)?.Name ?? "(不明)")
                .ToList();
            return string.Join(", ", names);
        }
    }

    public bool IsEnabled
    {
        get => Model.IsEnabled;
        set
        {
            if (Model.IsEnabled == value) return;
            Model.IsEnabled = value;
            OnPropertyChanged();
        }
    }

    /// <summary>モデル変更後に表示を更新する。</summary>
    public void RefreshAll()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Kind));
        OnPropertyChanged(nameof(ShortcutText));
        OnPropertyChanged(nameof(TargetText));
        OnPropertyChanged(nameof(IsEnabled));
    }
}
