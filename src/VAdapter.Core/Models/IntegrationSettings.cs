namespace VAdapter.Core.Models;

/// <summary>連携先の動画編集環境。</summary>
public enum IntegrationMode
{
    /// <summary>マクロ動作ベース（YMM4 等）。投げ込み監視は無効、現行どおりマクロ実行のみ。</summary>
    MacroOnly = 0,

    /// <summary>AviUtl + PSDToolKit（ごちゃまぜドロップス, dwData=1）。</summary>
    AviUtl = 1,

    /// <summary>AviUtl2 + PSDToolKit2（GCMZDrops2, dwData=2）。</summary>
    AviUtl2 = 2,
}

/// <summary>
/// 連携設定のルート。連携環境（モード）と、環境ごとの独立した投げ込み設定を保持する。
/// マクロ実行は全モード共通で動作し、投げ込み監視は AviUtl/AviUtl2 のときのみ有効。
/// </summary>
public sealed class IntegrationSettings
{
    public int Version { get; set; } = 1;

    public IntegrationMode ActiveMode { get; set; } = IntegrationMode.MacroOnly;

    /// <summary>AviUtl（無印）環境の設定。</summary>
    public AviutlDropConfig AviUtl { get; set; } = new();

    /// <summary>AviUtl2 環境の設定。</summary>
    public AviutlDropConfig AviUtl2 { get; set; } = new();

    /// <summary>指定モードに対応する投げ込み設定を返す（MacroOnly は null）。</summary>
    public AviutlDropConfig? ConfigFor(IntegrationMode mode) => mode switch
    {
        IntegrationMode.AviUtl => AviUtl,
        IntegrationMode.AviUtl2 => AviUtl2,
        _ => null,
    };
}

/// <summary>1つの動画編集環境（AviUtl / AviUtl2）への投げ込み設定。</summary>
public sealed class AviutlDropConfig
{
    /// <summary>監視対象フォルダ。</summary>
    public List<WatchFolder> Folders { get; set; } = new();

    /// <summary>話者ルール（ファイル名→レイヤー振り分け）。先頭から評価し最初に一致したものを使用。</summary>
    public List<SpeakerRule> Rules { get; set; } = new();

    /// <summary>ルール未一致時の既定レイヤー。</summary>
    public int DefaultLayer { get; set; } = 1;

    /// <summary>投入後にカーソルを進めるフレーム数（<see cref="AdvanceToItemEnd"/> が false のとき使用）。</summary>
    public int FrameAdvance { get; set; }

    /// <summary>
    /// 投入後にシークバーを挿入アイテムの終端へ移動する（音声長 × fps をフレーム数として進める）。
    /// 既定 true。
    /// </summary>
    public bool AdvanceToItemEnd { get; set; } = true;

    /// <summary>占有時の間隔（AviUtl2 のみ有効）。</summary>
    public int Margin { get; set; }

    /// <summary>ファイル書き込み完了待ち（安定化）の最大待機時間（ミリ秒）。</summary>
    public int StableWaitMs { get; set; } = 1500;
}

/// <summary>監視対象フォルダ。</summary>
public sealed class WatchFolder
{
    public string Path { get; set; } = string.Empty;

    /// <summary>サブフォルダも監視するか。</summary>
    public bool IncludeSubdirectories { get; set; }
}

/// <summary>
/// 話者ルール。ファイル名に対する正規表現が一致したら、そのレイヤーへ投入する。
/// 正規表現にキャプチャグループがあれば、話者名の抽出に利用する。
/// </summary>
public sealed class SpeakerRule
{
    /// <summary>
    /// 既定の話者抽出パターン。先頭ID + 区切り( _ または - ) + 話者名 + 区切り の形式から
    /// 話者名（グループ1）を抽出する。
    /// 例: "04_IA_台詞" → IA / "2-彩澄りりせ-台詞-…" → 彩澄りりせ / "001_東北きりたん（ノーマル）_台詞" → 東北きりたん（ノーマル）。
    /// </summary>
    public const string DefaultNamePattern = @"^[^_\-]*[_\-](.+?)[_\-]";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>ファイル名（拡張子含む）に対する正規表現。</summary>
    public string NamePattern { get; set; } = string.Empty;

    /// <summary>表示用の話者名（任意）。</summary>
    public string SpeakerName { get; set; } = string.Empty;

    /// <summary>一致時に投入するレイヤー。</summary>
    public int Layer { get; set; } = 1;

    public bool Enabled { get; set; } = true;
}
