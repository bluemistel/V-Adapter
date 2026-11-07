namespace VAdapter.Core.Models;

/// <summary>
/// 1 つのマクロが同じ対象アプリ向けのスクリプトを複数持つとき（再生方式の違い等）、
/// どれを実行するかを決める。選択は <see cref="IntegrationSettings.ScriptChoices"/> に保持する。
/// </summary>
public static class ScriptVariants
{
    /// <summary>選択を保存するキー（"マクロID/対象アプリID"。共通スクリプトは対象アプリ ID を空にする）。</summary>
    public static string Key(string macroId, string? targetId) => $"{macroId}/{targetId}";

    /// <summary>対象アプリ向けのスクリプトを、マクロ内の並び順で返す。</summary>
    public static IReadOnlyList<MacroScript> For(Macro macro, string? targetId) =>
        macro.Scripts.Where(s => s.TargetApplicationId == targetId).ToList();

    /// <summary>
    /// 実行するスクリプトを返す。選択があればそれを、無い（または選んだスクリプトが削除された）ときは
    /// 先頭を返す。該当が 1 つも無ければ null。
    /// </summary>
    public static MacroScript? Resolve(Macro macro, string? targetId, IntegrationSettings? settings)
    {
        var candidates = For(macro, targetId);
        if (candidates.Count == 0)
            return null;

        if (settings?.ScriptChoices.TryGetValue(Key(macro.Id, targetId), out var chosenId) == true)
        {
            var chosen = candidates.FirstOrDefault(s => s.Id == chosenId);
            if (chosen is not null)
                return chosen;
        }

        return candidates[0];
    }

    /// <summary>設定画面・一覧で使う方式の表示名（未入力なら番号）。</summary>
    public static string DisplayName(Macro macro, MacroScript script)
    {
        if (!string.IsNullOrWhiteSpace(script.VariantName))
            return script.VariantName.Trim();
        var index = For(macro, script.TargetApplicationId).ToList().IndexOf(script);
        return $"方式 {index + 1}";
    }
}
