namespace VAdapter.Core.Models;

/// <summary>
/// アプリ全体の永続化対象（マクロと対象アプリ設定）。マクロ → 対象アプリの参照整合性を
/// 1 ドキュメント内で保つため、両者をまとめて保持する。
/// </summary>
public sealed class MacroLibrary
{
    /// <summary>スキーマバージョン（将来のマイグレーション用）。</summary>
    public int Version { get; set; } = 1;

    public List<TargetApplication> Targets { get; set; } = new();

    public List<Macro> Macros { get; set; } = new();

    /// <summary>指定 ID の対象アプリを取得（なければ null）。</summary>
    public TargetApplication? FindTarget(string? id) =>
        id is null ? null : Targets.FirstOrDefault(t => t.Id == id);
}

/// <summary>
/// インポート/エクスポート用の可搬バンドル（.vamacro）。
/// 選択したマクロと、それが参照する対象アプリ設定を内包して単体で移送可能にする。
/// </summary>
public sealed class MacroBundle
{
    /// <summary>フォーマット識別子。</summary>
    public string Format { get; set; } = "vadapter.macro-bundle";

    public int Version { get; set; } = 1;

    public List<TargetApplication> Targets { get; set; } = new();

    public List<Macro> Macros { get; set; } = new();
}
