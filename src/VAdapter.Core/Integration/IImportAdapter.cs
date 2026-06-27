namespace VAdapter.Core.Integration;

/// <summary>
/// 投げ込みアダプタの抽象。<see cref="DropPayload"/>（中立契約）を受け取り、
/// 編集ソフト固有の取り込み操作へ写像する差し替え可能な実装。
/// 組込（gcmz）・外部コマンドなどがこれを実装する。
/// </summary>
public interface IImportAdapter
{
    /// <summary>アダプタ識別子（安定したキー）。</summary>
    string Id { get; }

    /// <summary>UI 表示名。</summary>
    string DisplayName { get; }

    /// <summary>接続状態を取得する（UI の状態表示用）。</summary>
    AdapterStatus GetStatus();

    /// <summary>ペイロードを取り込む。</summary>
    ImportResult Import(DropPayload payload);
}

/// <summary>取り込み結果。</summary>
public sealed class ImportResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    /// <summary>成功時の補足情報（例: シーク量）。</summary>
    public string? Info { get; init; }

    public static ImportResult Ok(string? info = null) => new() { Success = true, Info = info };
    public static ImportResult Fail(string error) => new() { Success = false, Error = error };
}

/// <summary>アダプタの接続状態。</summary>
public sealed class AdapterStatus
{
    /// <summary>投げ込み可能か。</summary>
    public bool Available { get; init; }

    /// <summary>状態の要約（1行）。</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>接続先の詳細（プロジェクト情報・コマンド等。任意）。</summary>
    public string? TargetInfo { get; init; }
}
