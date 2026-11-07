namespace VAdapter.Core.Models;

/// <summary>
/// 1 つの対象アプリに紐づくスクリプト（命令列）。マクロは対象アプリごとに本スクリプトを持つ。
/// スクリプト自体に名称は持たない（マクロ名 + 対象アプリ名で識別する）。
/// </summary>
public sealed class MacroScript
{
    /// <summary>一意な識別子。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 対象アプリの <see cref="TargetApplication.Id"/>。
    /// null は「共通（フォールバック）」スクリプトで、対象アプリ一致が無いときに実行される。
    /// </summary>
    public string? TargetApplicationId { get; set; }

    /// <summary>
    /// 方式の名前（任意。例: "ブロックを再生（Ctrl+B）"）。
    /// 同じ対象アプリ向けのスクリプトを複数持つとき、設定画面でどれを使うか選ぶための表示名。
    /// </summary>
    public string? VariantName { get; set; }

    /// <summary>実行する命令列（順序保持）。</summary>
    public List<Instruction> Instructions { get; set; } = new();
}
