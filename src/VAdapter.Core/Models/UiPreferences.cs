namespace VAdapter.Core.Models;

/// <summary>
/// メイン画面の操作状態のうち、アプリを終了しても引き継ぎたいもの。
/// 連携設定（<see cref="IntegrationSettings"/>）とは役割が違うため別ファイルに保持する。
/// </summary>
public sealed class UiPreferences
{
    /// <summary>送信先を手動で固定するか（false は自動判定）。</summary>
    public bool ManualSendTarget { get; set; }

    /// <summary>手動のときに固定する対象アプリの ID（未選択は null）。</summary>
    public string? SendTargetId { get; set; }
}
