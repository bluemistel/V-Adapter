namespace VAdapter.Core.Integration;

/// <summary>
/// 編集ソフト非依存の投げ込みペイロード（中立契約）。
/// 「合成音声出力の検知 → 正規化 → アダプタへ受け渡し」の中核データであり、
/// 具体的な fps 依存値（フレーム数など）は持たず、意図のみを表現する。
/// 各 <see cref="IImportAdapter"/> がこの意図を編集ソフト固有の操作へ写像する。
/// </summary>
public sealed class DropPayload
{
    /// <summary>スキーマのバージョン（後方互換判定用）。</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>送信元アプリ情報。</summary>
    public DropSource Source { get; set; } = new();

    /// <summary>投げ込むファイル（音声・字幕など、役割付き）。</summary>
    public List<DropFile> Files { get; set; } = new();

    /// <summary>解決済みの話者名（任意）。</summary>
    public string? Speaker { get; set; }

    /// <summary>配置ヒント（トラック番号など）。</summary>
    public DropRoutingHint Routing { get; set; } = new();

    /// <summary>タイミングの意図（終端へシークするか等）。</summary>
    public DropTiming Timing { get; set; } = new();
}

/// <summary>送信元アプリ情報。</summary>
public sealed class DropSource
{
    public string App { get; set; } = "V-Adapter";

    /// <summary>送信元アプリのバージョン（任意）。</summary>
    public string Version { get; set; } = string.Empty;
}

/// <summary>役割付きの1ファイル。</summary>
public sealed class DropFile
{
    /// <summary>役割（<see cref="DropFileRole"/> の値：<c>audio</c> / <c>subtitle</c> 等）。</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>ファイルのフルパス。</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>テキストファイルの文字コード（判明していれば。例: <c>utf-8</c> / <c>shift_jis</c>）。</summary>
    public string? Encoding { get; set; }
}

/// <summary><see cref="DropFile.Role"/> の既定値。</summary>
public static class DropFileRole
{
    public const string Audio = "audio";
    public const string Subtitle = "subtitle";
}

/// <summary>配置ヒント。編集ソフト固有の「レイヤー」を中立な「トラック番号ヒント」として表現する。</summary>
public sealed class DropRoutingHint
{
    /// <summary>希望トラック（レイヤー）番号。null のときはアダプタ既定に委ねる。</summary>
    public int? TrackHint { get; set; }
}

/// <summary>タイミングの意図。具体的なフレーム数は各アダプタが算出する。</summary>
public sealed class DropTiming
{
    /// <summary>投入後、カーソル/シークを挿入アイテムの終端へ進める意図。</summary>
    public bool AdvanceToItemEnd { get; set; }
}
