using System.Text.Json.Serialization;

namespace VAdapter.Core.Models;

/// <summary>マクロを構成する 1 命令の基底クラス。</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(WaitInstruction), WaitInstruction.KindId)]
[JsonDerivedType(typeof(ClickInstruction), ClickInstruction.KindId)]
[JsonDerivedType(typeof(SendKeysInstruction), SendKeysInstruction.KindId)]
[JsonDerivedType(typeof(WaitForWindowInstruction), WaitForWindowInstruction.KindId)]
[JsonDerivedType(typeof(WaitForTextInstruction), WaitForTextInstruction.KindId)]
[JsonDerivedType(typeof(WaitForActivationInstruction), WaitForActivationInstruction.KindId)]
[JsonDerivedType(typeof(WaitForDialogInstruction), WaitForDialogInstruction.KindId)]
[JsonDerivedType(typeof(SwitchTargetInstruction), SwitchTargetInstruction.KindId)]
[JsonDerivedType(typeof(LaunchAppInstruction), LaunchAppInstruction.KindId)]
[JsonDerivedType(typeof(ReadUiTextInstruction), ReadUiTextInstruction.KindId)]
[JsonDerivedType(typeof(SetSaveFileNameInstruction), SetSaveFileNameInstruction.KindId)]
[JsonDerivedType(typeof(WriteSubtitleInstruction), WriteSubtitleInstruction.KindId)]
public abstract class Instruction
{
    /// <summary>命令インスタンスの一意な識別子（並べ替え・編集用）。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>型判別子（コード上の利便のため。JSON では polymorphic discriminator を使うため非直列化）。</summary>
    [JsonIgnore]
    public abstract string Kind { get; }

    /// <summary>UI 一覧に表示する人間可読な要約。</summary>
    [JsonIgnore]
    public abstract string Summary { get; }
}

/// <summary>指定の時間（ミリ秒）待機する。</summary>
public sealed class WaitInstruction : Instruction
{
    public const string KindId = "wait";
    [JsonIgnore] public override string Kind => KindId;

    /// <summary>待機時間（ミリ秒）。</summary>
    public int DurationMs { get; set; } = 500;

    [JsonIgnore] public override string Summary => $"{DurationMs} ms 待機";
}

/// <summary>対象アプリの特定座標をクリックする。</summary>
public sealed class ClickInstruction : Instruction
{
    public const string KindId = "click";
    [JsonIgnore] public override string Kind => KindId;

    public int X { get; set; }
    public int Y { get; set; }

    /// <summary>
    /// 座標解釈の上書き。null の場合は対象アプリの既定（<see cref="TargetApplication.CoordinateMode"/>）に従う。
    /// </summary>
    public CoordinateMode? CoordinateModeOverride { get; set; }

    /// <summary>
    /// 相対クリック時の基準位置。X,Y はこのアンカーからのオフセット。
    /// 既定は左上（従来どおりクライアント左上からの絶対オフセット）。
    /// </summary>
    public ClickAnchor Anchor { get; set; } = ClickAnchor.TopLeft;

    public MouseButton Button { get; set; } = MouseButton.Left;

    [JsonIgnore]
    public override string Summary
    {
        get
        {
            var mode = CoordinateModeOverride switch
            {
                CoordinateMode.Relative => "相対",
                CoordinateMode.Absolute => "絶対",
                _ => "既定",
            };
            var anchor = Anchor == ClickAnchor.TopLeft ? "" : $"/{AnchorLabel(Anchor)}";
            return $"クリック ({X}, {Y}) [{mode}{anchor}/{Button}]";
        }
    }

    private static string AnchorLabel(ClickAnchor a) => a switch
    {
        ClickAnchor.TopRight => "右上",
        ClickAnchor.BottomLeft => "左下",
        ClickAnchor.BottomRight => "右下",
        ClickAnchor.Center => "中央",
        _ => "左上",
    };
}

/// <summary>対象アプリにキー（組み合わせ可能）を送信する。</summary>
public sealed class SendKeysInstruction : Instruction
{
    public const string KindId = "sendkeys";
    [JsonIgnore] public override string Kind => KindId;

    public KeyCombination KeyCombination { get; set; } = new();

    [JsonIgnore] public override string Summary => $"キー送信 {KeyCombination}";
}

/// <summary>対象アプリのウィンドウが表示されるまで待機する。</summary>
public sealed class WaitForWindowInstruction : Instruction
{
    public const string KindId = "waitwindow";
    [JsonIgnore] public override string Kind => KindId;

    /// <summary>タイムアウト（ミリ秒）。</summary>
    public int TimeoutMs { get; set; } = 5000;

    [JsonIgnore] public override string Summary => $"ウィンドウ表示待ち (≤{TimeoutMs} ms)";
}

/// <summary>
/// 対象ウィンドウ内に特定テキストが表示されるまで OCR で監視して待機する。
/// オーバーレイ描画されたダイアログの検出手段。
/// </summary>
public sealed class WaitForTextInstruction : Instruction
{
    public const string KindId = "waittext";
    [JsonIgnore] public override string Kind => KindId;

    /// <summary>検出対象のテキスト（部分一致）。</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// OCR を行う領域（対象ウィンドウのクライアント左上を基準とした相対矩形）。
    /// null の場合はウィンドウ全体を対象とする。
    /// </summary>
    public RelativeRect? Region { get; set; }

    /// <summary>タイムアウト（ミリ秒）。</summary>
    public int TimeoutMs { get; set; } = 10000;

    [JsonIgnore] public override string Summary => $"テキスト表示待ち \"{Text}\" (≤{TimeoutMs} ms)";
}

/// <summary>
/// 対象アプリが前面（アクティブ）になるまで同期的に待機する。
/// スクリプト先頭で、本アプリ実行直後に対象アプリへ切り替わるのを待つ用途。
/// </summary>
public sealed class WaitForActivationInstruction : Instruction
{
    public const string KindId = "waitactivation";
    [JsonIgnore] public override string Kind => KindId;

    /// <summary>タイムアウト（ミリ秒）。</summary>
    public int TimeoutMs { get; set; } = 5000;

    [JsonIgnore] public override string Summary => $"アプリ切替待ち (≤{TimeoutMs} ms)";
}

/// <summary>
/// Windows 標準のコモンダイアログ（名前を付けて保存 等）が表示されるまで待機する。
/// ダイアログ枠のウィンドウクラス（既定 "#32770"）と、対象アプリと同一プロセスであることで
/// 環境・ウィンドウサイズに左右されず検出する。タイトル部分一致で種類を絞り込める。
/// </summary>
public sealed class WaitForDialogInstruction : Instruction
{
    public const string KindId = "waitdialog";
    [JsonIgnore] public override string Kind => KindId;

    /// <summary>ダイアログタイトルの部分一致（例: "名前を付けて保存"）。空なら任意のダイアログ。</summary>
    public string? TitleContains { get; set; }

    /// <summary>ダイアログ枠のウィンドウクラス。Windows コモンダイアログは "#32770"。</summary>
    public string DialogClass { get; set; } = "#32770";

    /// <summary>検出時にダイアログを前面化するか。</summary>
    public bool BringToForeground { get; set; } = true;

    /// <summary>タイムアウト（ミリ秒）。</summary>
    public int TimeoutMs { get; set; } = 10000;

    [JsonIgnore]
    public override string Summary =>
        string.IsNullOrEmpty(TitleContains)
            ? $"ダイアログ表示待ち (≤{TimeoutMs} ms)"
            : $"ダイアログ表示待ち \"{TitleContains}\" (≤{TimeoutMs} ms)";
}

/// <summary>
/// 以降の「キー送信」「絶対クリック」の送信先ウィンドウを切り替える。
/// 例: 保存ダイアログ表示後にダイアログへ操作を移し、Enter やボタンクリックを送る。
/// （相対クリックは座標基準が対象アプリ本体のままのため、本切替の影響を受けない）
/// </summary>
public sealed class SwitchTargetInstruction : Instruction
{
    public const string KindId = "switchtarget";
    [JsonIgnore] public override string Kind => KindId;

    /// <summary>切替先の種類。</summary>
    public SwitchTargetKind Target { get; set; } = SwitchTargetKind.Dialog;

    /// <summary>ダイアログ切替時のタイトル部分一致（任意）。</summary>
    public string? TitleContains { get; set; }

    /// <summary>ダイアログ切替時の枠ウィンドウクラス（既定 "#32770"）。</summary>
    public string DialogClass { get; set; } = "#32770";

    /// <summary>ダイアログ切替時、検出までの待機タイムアウト（ミリ秒）。</summary>
    public int TimeoutMs { get; set; } = 5000;

    [JsonIgnore]
    public override string Summary => Target == SwitchTargetKind.Dialog
        ? (string.IsNullOrEmpty(TitleContains)
            ? "操作対象をダイアログへ"
            : $"操作対象をダイアログ「{TitleContains}」へ")
        : "操作対象をアプリ本体へ";
}

/// <summary>
/// 関連ソフト（対象アプリ／指定 exe）を起動する。ランチャー命令。
/// 実行ファイルが未指定のときは、スクリプトの対象アプリに登録された
/// <see cref="TargetApplication.ExecutablePath"/> を起動する。
/// </summary>
public sealed class LaunchAppInstruction : Instruction
{
    public const string KindId = "launchapp";
    [JsonIgnore] public override string Kind => KindId;

    /// <summary>
    /// 起動する実行ファイルのフルパス。空のときはスクリプトの対象アプリの実行ファイルを使用する。
    /// </summary>
    public string? ExecutablePath { get; set; }

    /// <summary>起動引数（任意）。</summary>
    public string? Arguments { get; set; }

    /// <summary>同名プロセスが既に起動中なら起動をスキップする。</summary>
    public bool SkipIfRunning { get; set; } = true;

    [JsonIgnore]
    public override string Summary => string.IsNullOrWhiteSpace(ExecutablePath)
        ? "対象アプリを起動"
        : $"起動: {Path.GetFileName(ExecutablePath)}";
}

/// <summary>
/// 対象アプリの UI 要素（UI Automation）からテキストを取得し、実行変数へ格納する。
/// VOICEROID2 等の「キャラ表示エリア名」「本文」を取得する用途（例外処理）。
/// </summary>
public sealed class ReadUiTextInstruction : Instruction
{
    public const string KindId = "readui";
    [JsonIgnore] public override string Kind => KindId;

    /// <summary>取得する UI 要素の選択条件（AutomationId がある要素向け。任意）。</summary>
    public UiElementSelector Selector { get; set; } = new();

    /// <summary>
    /// 座標ベース取得。実行時にこの位置を <c>FromPoint</c> で取り直してテキストを読む
    /// （AutomationId を持たない WPF コントロールでも安定。値が動的でも都度読み直す）。
    /// X,Y は対象ウィンドウのクライアント領域内、<see cref="Anchor"/> 基準のオフセット。
    /// </summary>
    public bool PointCaptured { get; set; }

    public int X { get; set; }
    public int Y { get; set; }
    public ClickAnchor Anchor { get; set; } = ClickAnchor.TopLeft;

    /// <summary>取得したテキストを格納する変数名（例: "character" / "serifu"）。</summary>
    public string VariableName { get; set; } = "character";

    /// <summary>取得条件が設定済みか（座標 or セレクタ）。</summary>
    [JsonIgnore]
    public bool HasCapture => PointCaptured || Selector.HasAnyCondition;

    [JsonIgnore]
    public override string Summary
    {
        get
        {
            var how = PointCaptured ? $"座標 {X},{Y}" : Selector.Describe();
            return $"UIから情報取得 → {{{VariableName}}}（{how}）";
        }
    }
}

/// <summary>
/// 現在の操作対象（保存ダイアログ）のファイル名欄を、テンプレート＋実行変数から生成した
/// フルパスへ書き換える。保存先フォルダは連携設定の VOICEROID2 キャラ別マッピングから解決する。
/// </summary>
public sealed class SetSaveFileNameInstruction : Instruction
{
    public const string KindId = "savename";
    [JsonIgnore] public override string Kind => KindId;

    /// <summary>ファイル名テンプレート（例: <c>{date}_{character}_{serifu:10}</c>）。</summary>
    public string FileNameTemplate { get; set; } = Media.SaveNameComposer.DefaultTemplate;

    /// <summary>付与する拡張子（VOICEROID2 は wav）。</summary>
    public string Extension { get; set; } = ".wav";

    /// <summary>保存先フォルダの解決に使うキャラクター名の変数名。</summary>
    public string CharacterVariable { get; set; } = "character";

    /// <summary>生成したフルパスを格納する変数名（字幕書き出しで参照）。</summary>
    public string SavePathVariable { get; set; } = "savepath";

    /// <summary>
    /// ダイアログのファイル名欄にフォルダを含むフルパスを入れるか。
    /// 既定 false（ファイル名のみ）。VOICEROID2 の保存ダイアログは <c>\</c>/<c>:</c> を
    /// 「使えない文字」として拒否するため、フォルダは含めない（保存先は VOICEROID2 側の設定に従う）。
    /// パスをそのまま受け付ける標準ダイアログでは true にできる。
    /// </summary>
    public bool IncludeFolderInName { get; set; }

    [JsonIgnore]
    public override string Summary => $"保存ファイル名の書き換え（{FileNameTemplate}{Extension}）";
}

/// <summary>
/// 直前に決めた保存パス（<see cref="SetSaveFileNameInstruction.SavePathVariable"/>）と同じ基底名の
/// <c>.txt</c> を UTF-8 で書き出す。内容は本文の実行変数。
/// </summary>
public sealed class WriteSubtitleInstruction : Instruction
{
    public const string KindId = "writesubtitle";
    [JsonIgnore] public override string Kind => KindId;

    /// <summary>保存パスを保持する変数名（<see cref="SetSaveFileNameInstruction.SavePathVariable"/> と対応）。</summary>
    public string PathVariable { get; set; } = "savepath";

    /// <summary>字幕本文を保持する変数名。</summary>
    public string TextVariable { get; set; } = "serifu";

    /// <summary>
    /// 音声ファイルが実際に保存されるのを待つ上限（ミリ秒）。
    /// 保存ダイアログの既定フォルダが設定と異なる場合に、実際の保存先へ字幕を追従させるために使う。
    /// </summary>
    public int WaitForAudioMs { get; set; } = 5000;

    [JsonIgnore]
    public override string Summary => $"字幕テキストの書き出し（{{{TextVariable}}} → {{{PathVariable}}}.txt, UTF-8）";
}

/// <summary>ウィンドウのクライアント左上を基準とした相対矩形。</summary>
public sealed class RelativeRect
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
