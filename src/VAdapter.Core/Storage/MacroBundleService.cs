using VAdapter.Core.Models;
using VAdapter.Core.Serialization;

namespace VAdapter.Core.Storage;

/// <summary>
/// マクロのインポート/エクスポート（.vamacro バンドル）を扱うサービス。
/// エクスポートは選択マクロと、各スクリプトが参照する対象アプリ設定を内包して可搬にする。
/// インポートは ID を再採番して既存ライブラリとの衝突を防ぐ。
/// </summary>
public static class MacroBundleService
{
    public const string FileExtension = ".vamacro";

    /// <summary>選択したマクロと、それらが参照する対象アプリのみを抽出してバンドル化する。</summary>
    public static MacroBundle CreateBundle(IEnumerable<Macro> macros, MacroLibrary library)
    {
        var macroList = macros.ToList();
        var neededTargetIds = macroList
            .SelectMany(m => m.Scripts)
            .Select(s => s.TargetApplicationId)
            .Where(id => id is not null)
            .Distinct()
            .ToHashSet();

        var targets = library.Targets
            .Where(t => neededTargetIds.Contains(t.Id))
            .Select(CloneTarget)
            .ToList();

        return new MacroBundle
        {
            Targets = targets,
            Macros = macroList.Select(CloneMacro).ToList(),
        };
    }

    public static string SerializeBundle(MacroBundle bundle) => VAdapterJson.Serialize(bundle);

    public static MacroBundle DeserializeBundle(string json) =>
        VAdapterJson.Deserialize<MacroBundle>(json)
        ?? throw new InvalidDataException("バンドルの読み込みに失敗しました（空または不正な形式）。");

    /// <summary>
    /// バンドルをライブラリへ取り込む。対象アプリ・マクロ・スクリプト・命令の ID をすべて再採番し、
    /// 各スクリプトの対象アプリ参照を新 ID へ張り替える。取り込んだマクロを返す。
    /// </summary>
    public static IReadOnlyList<Macro> ImportInto(MacroBundle bundle, MacroLibrary library)
    {
        // 対象アプリを再採番して追加。旧 ID → 新 ID の対応表を作る。
        var targetIdMap = new Dictionary<string, string>();
        foreach (var target in bundle.Targets)
        {
            var clone = CloneTarget(target);
            var oldId = target.Id;
            clone.Id = Guid.NewGuid().ToString("N");
            targetIdMap[oldId] = clone.Id;
            library.Targets.Add(clone);
        }

        var imported = new List<Macro>();
        foreach (var macro in bundle.Macros)
        {
            var clone = CloneMacro(macro);
            clone.Id = Guid.NewGuid().ToString("N");
            clone.IsBuiltIn = false; // 取り込んだものはユーザーマクロ扱い。

            foreach (var script in clone.Scripts)
            {
                script.Id = Guid.NewGuid().ToString("N");

                // 参照先の対象アプリ ID を張り替え（対応が無ければ共通=null に）。
                script.TargetApplicationId =
                    script.TargetApplicationId is { } tid && targetIdMap.TryGetValue(tid, out var newId)
                        ? newId
                        : null;

                foreach (var instr in script.Instructions)
                    instr.Id = Guid.NewGuid().ToString("N");
            }

            library.Macros.Add(clone);
            imported.Add(clone);
        }

        return imported;
    }

    // --- ディープコピー（JSON 経由で安全に複製） ---

    private static TargetApplication CloneTarget(TargetApplication t) =>
        VAdapterJson.Deserialize<TargetApplication>(VAdapterJson.Serialize(t))!;

    private static Macro CloneMacro(Macro m) =>
        VAdapterJson.Deserialize<Macro>(VAdapterJson.Serialize(m))!;
}
