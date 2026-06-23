using CommunityToolkit.Mvvm.ComponentModel;
using VAdapter.Core.Models;

namespace VAdapter.App.ViewModels;

/// <summary>マクロ編集画面のスクリプト一覧（対象アプリ別）の 1 行。</summary>
public sealed partial class ScriptRow : ObservableObject
{
    public MacroScript Model { get; }
    private readonly MacroLibrary _library;

    public ScriptRow(MacroScript model, MacroLibrary library)
    {
        Model = model;
        _library = library;
    }

    public string Display
    {
        get
        {
            var name = Model.TargetApplicationId is null
                ? "共通（対象アプリなし）"
                : _library.FindTarget(Model.TargetApplicationId)?.Name ?? "(不明な対象アプリ)";
            return $"{name}  —  {Model.Instructions.Count} 命令";
        }
    }

    public void Refresh() => OnPropertyChanged(nameof(Display));
}
