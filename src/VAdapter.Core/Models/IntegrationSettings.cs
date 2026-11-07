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

    /// <summary>外部アダプタ（コマンド実行）。payload.json を中立契約で外部コマンドへ渡す。</summary>
    External = 3,
}

/// <summary>
/// 連携設定のルート。連携環境（モード）と、環境ごとの独立した投げ込み設定を保持する。
/// マクロ実行は全モード共通で動作し、投げ込み監視は AviUtl/AviUtl2 のときのみ有効。
/// </summary>
public sealed class IntegrationSettings
{
    /// <summary>設定フォーマットの版。2 = 監視フォルダを <see cref="WatchFolders"/> に統合。</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>現在の設定フォーマット版。</summary>
    public const int CurrentVersion = 2;

    public IntegrationMode ActiveMode { get; set; } = IntegrationMode.MacroOnly;

    /// <summary>
    /// 合成音声ソフトの保存先として見張るフォルダ（全モード共通の唯一のリスト）。
    /// 投げ込み・後処理のどちらもここを見る。連携環境ごとに分けていた旧形式は
    /// <see cref="MigrateWatchFolders"/> がここへ引き継ぐ。
    /// </summary>
    public List<WatchFolder> WatchFolders { get; set; } = new();

    /// <summary>AviUtl（無印）環境の設定。</summary>
    public AviutlDropConfig AviUtl { get; set; } = new();

    /// <summary>AviUtl2 環境の設定。</summary>
    public AviutlDropConfig AviUtl2 { get; set; } = new();

    /// <summary>外部アダプタ（コマンド）環境の設定。</summary>
    public ExternalAdapterConfig External { get; set; } = new();

    /// <summary>
    /// マクロ動作ベース環境で起動する動画編集ソフトの実行ファイル（ランチャー用。YMM4 等）。
    /// 他モードの編集ソフトは各 <see cref="AviutlDropConfig.EditorPath"/> に保持する。
    /// </summary>
    public string? MacroEditorPath { get; set; }

    /// <summary>指定モードで起動する動画編集ソフトの実行ファイルパスを返す（未登録は null）。</summary>
    public string? EditorPathFor(IntegrationMode mode) => mode switch
    {
        IntegrationMode.MacroOnly => MacroEditorPath,
        _ => ConfigFor(mode)?.EditorPath,
    };

    /// <summary>VOICEROID2（AITalk5系）用オプション（例外処理。既定は無効）。</summary>
    public Voiceroid2Options Voiceroid2 { get; set; } = new();

    /// <summary>保存された音声・字幕の後処理（改名・整理・配布）。既定は無効。</summary>
    public FilePostProcessOptions PostProcess { get; set; } = new();

    /// <summary>
    /// 同じ対象アプリ向けのスクリプトが複数あるマクロで、どれを使うか（キーは
    /// <see cref="ScriptVariants.Key"/>、値はスクリプト ID）。未選択はマクロ内の先頭を使う。
    /// 組込マクロは更新で上書きされるため、選択はマクロ側ではなくここに持つ。
    /// </summary>
    public Dictionary<string, string> ScriptChoices { get; set; } = new();

    /// <summary>
    /// 旧形式（連携環境ごと／後処理専用に分かれていた監視フォルダ）を <see cref="WatchFolders"/> へ引き継ぐ。
    /// 設定の読み込み直後に一度だけ呼ぶ。既に <see cref="WatchFolders"/> があれば何もしない。
    /// </summary>
    /// <returns>移行を行った場合 true（呼び出し側が保存し直す判断に使う）。</returns>
    public bool MigrateWatchFolders()
    {
        var upToDate = Version >= CurrentVersion;
        Version = CurrentVersion;

        if (WatchFolders.Count > 0 || upToDate)
            return false;

        // アクティブモードの監視フォルダを最優先で引き継ぐ（VOICEROID2 の番号指定がここを基準にしているため）。
        var sources = new List<WatchFolder>();
        AddRange(sources, ConfigFor(ActiveMode)?.Folders);
        AddRange(sources, PostProcess.Folders);
        AddRange(sources, AviUtl.Folders);
        AddRange(sources, AviUtl2.Folders);
        AddRange(sources, External.Folders);

        if (sources.Count == 0)
            return false;

        WatchFolders = sources;
        return true;

        static void AddRange(List<WatchFolder> into, List<WatchFolder>? from)
        {
            if (from is null)
                return;
            foreach (var f in from)
            {
                if (string.IsNullOrWhiteSpace(f.Path))
                    continue;
                if (into.Any(x => PathEquals(x.Path, f.Path)))
                    continue;
                into.Add(f);
            }
        }
    }

    /// <summary>
    /// 話者ルール・後処理に旧既定パターン（<see cref="SpeakerRule.LegacyDefaultNamePattern"/>）が
    /// そのまま残っていれば、現在の既定パターンへ置き換える。利用者が書き換えたパターンは変更しない。
    /// 「行を追加」で既定パターンの文字列がルールへ複写されるため、既定を変えても既存ルールには届かない。
    /// </summary>
    /// <returns>置き換えを行った場合 true（呼び出し側が保存し直す判断に使う）。</returns>
    public bool MigrateSpeakerPatterns()
    {
        var changed = false;
        foreach (var config in new[] { AviUtl, AviUtl2, External })
        {
            foreach (var rule in config.Rules)
            {
                if (rule.NamePattern == SpeakerRule.LegacyDefaultNamePattern)
                {
                    rule.NamePattern = SpeakerRule.DefaultNamePattern;
                    changed = true;
                }
            }
        }

        if (PostProcess.SpeakerPattern == SpeakerRule.LegacyDefaultNamePattern)
        {
            PostProcess.SpeakerPattern = string.Empty; // 空＝既定パターン。
            changed = true;
        }

        return changed;
    }

    private static bool PathEquals(string a, string b)
    {
        try
        {
            return string.Equals(
                System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(a)),
                System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(b)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// VOICEROID2 の保存先フォルダを、キャラクター名とアクティブモードから解決する。
    /// マクロ動作ベースはキャラの明示フォルダ、AviUtl 系は <see cref="WatchFolders"/> の指定番号。
    /// 未一致時は AviUtl 系なら先頭監視フォルダ、マクロ動作ベースなら null。
    /// </summary>
    public string? ResolveVoiceroid2Folder(IntegrationMode mode, string? characterName)
    {
        var map = MatchVoiceroid2Character(characterName);

        if (mode == IntegrationMode.MacroOnly)
            return string.IsNullOrWhiteSpace(map?.MacroBaseFolder) ? null : map!.MacroBaseFolder;

        var folders = WatchFolders;
        if (folders.Count == 0)
            return null;

        var index = map?.MonitorFolderIndex ?? 0;
        if (index < 0 || index >= folders.Count)
            index = 0; // 番号が範囲外なら先頭へフォールバック。
        return folders[index].Path;
    }

    /// <summary>
    /// UI から取得した話者名に対応する登録キャラクターを返す（未一致は null）。
    /// VOICEROID2 のユーザープリセットは「キャラクター名＋任意の文字列」（例: <c>紲星あかり - コピー</c>）に
    /// なり得るが、区切り文字はユーザーが自由に決められるため仮定できない。
    /// そこで完全一致を優先し、無ければ前方一致（最長の登録名を優先）で解決する。
    /// </summary>
    public Voiceroid2Character? MatchVoiceroid2Character(string? characterName)
    {
        var name = characterName?.Trim();
        if (string.IsNullOrEmpty(name))
            return null;

        var candidates = Voiceroid2.Characters
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .ToList();

        var exact = candidates.FirstOrDefault(c =>
            string.Equals(c.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        // 前方一致。「あかり」と「紲星あかり」が両方登録されていても長い方を選ぶ。
        return candidates
            .Where(c => name.StartsWith(c.Name.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.Name.Trim().Length)
            .FirstOrDefault();
    }

    /// <summary>
    /// UI から取得した話者名を、登録済みのキャラクター名へ正規化する。
    /// 未登録・未一致ならそのまま返す（保存ファイル名や話者ルールの表記を揃えるために使う）。
    /// </summary>
    public string? CanonicalVoiceroid2Name(string? characterName) =>
        MatchVoiceroid2Character(characterName)?.Name?.Trim() ?? characterName;

    /// <summary>
    /// 指定モードに対応する監視/ルーティング設定を返す（MacroOnly は null）。
    /// External は <see cref="ExternalAdapterConfig"/> を基底 <see cref="AviutlDropConfig"/> として返す。
    /// </summary>
    public AviutlDropConfig? ConfigFor(IntegrationMode mode) => mode switch
    {
        IntegrationMode.AviUtl => AviUtl,
        IntegrationMode.AviUtl2 => AviUtl2,
        IntegrationMode.External => External,
        _ => null,
    };
}

/// <summary>1つの動画編集環境（AviUtl / AviUtl2 / 外部）への監視・ルーティング設定。</summary>
public class AviutlDropConfig
{
    /// <summary>
    /// 旧形式の監視対象フォルダ（連携環境ごと）。移行専用に残しており、現在は
    /// <see cref="IntegrationSettings.WatchFolders"/> が唯一の監視リスト。
    /// </summary>
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

    /// <summary>
    /// この環境で起動する動画編集ソフト（AviUtl / AviUtl2 等）の実行ファイルパス（ランチャー用）。
    /// 未登録のときはランチャー機能が無効。
    /// </summary>
    public string? EditorPath { get; set; }
}

/// <summary>
/// 外部アダプタ（コマンド実行）環境の設定。
/// 監視/ルーティングは <see cref="AviutlDropConfig"/> を継承し、コマンドテンプレートと
/// タイムアウトを追加する。テンプレート中の <c>{payload}</c> が payload.json のパスへ置換される。
/// </summary>
public sealed class ExternalAdapterConfig : AviutlDropConfig
{
    /// <summary>実行コマンドテンプレート（例: <c>python davinci_import.py {payload}</c>）。</summary>
    public string CommandTemplate { get; set; } = string.Empty;

    /// <summary>コマンドの最大実行時間（ミリ秒）。</summary>
    public int TimeoutMs { get; set; } = 15000;
}

/// <summary>
/// VOICEROID2（AITalk5系）向けの例外オプション。ファイル命名規則を持たないアプリ向けに、
/// V-Adapter 側で「保存先＋現在日時_キャラクター名_本文先頭」を組み立てて保存するための設定。
/// 命令側にフォルダを持たせず、ここへ集約する（組込マクロを編集不要にするため）。
/// </summary>
public sealed class Voiceroid2Options
{
    /// <summary>この機能を使うか（既定 false）。</summary>
    public bool Enabled { get; set; }

    /// <summary>キャラクター別の保存先マッピング。</summary>
    public List<Voiceroid2Character> Characters { get; set; } = new();
}

/// <summary>VOICEROID2 のキャラクター1件と保存先の対応。</summary>
public sealed class Voiceroid2Character
{
    /// <summary>VOICEROID2 の「キャラ表示エリア」に表示される名称（UI 取得値と照合）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>マクロ動作ベース（YMM4 等）のときの明示保存先フォルダ。</summary>
    public string? MacroBaseFolder { get; set; }

    /// <summary>AviUtl / AviUtl2 / External のとき、そのモードの監視フォルダ配列の何番目へ保存するか。</summary>
    public int MonitorFolderIndex { get; set; }
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
    /// 既定の話者抽出パターン。先頭の番号・日時（数字・空白・ピリオドと区切り _ / -）を読み飛ばし、
    /// 数字以外で始まる最初の要素を、次の区切りまで話者名（グループ1）として抽出する。
    /// 例: "04_IA_台詞" → IA / "2-彩澄りりせ-台詞-…" → 彩澄りりせ /
    /// "138_144541_琴葉 茜_台詞" → 琴葉 茜 / "20260927_102140_紲星あかり_本文" → 紲星あかり。
    /// <para>
    /// 2 文字目以降の数字は名前の一部とみなす（"No.7" 等）。話者名より前に数字以外の要素
    /// （セリフ・プロジェクト名等）がある命名や、話者名の後ろに区切りが無い命名には対応しない。
    /// </para>
    /// </summary>
    public const string DefaultNamePattern = @"^[\d\s._\-]*([^\d\s._\-][^_\-]*)[_\-]";

    /// <summary>
    /// v0.0.9 までの既定パターン（常に 2 番目の要素を取る）。番号が 2 つ並ぶ命名
    /// （A.I.VOICE2 の "009_162738_話者_…" 等）で数字を話者名と誤認したため置き換えた。
    /// 保存済みの話者ルールに残るこの文字列は <see cref="IntegrationSettings.MigrateSpeakerPatterns"/> が更新する。
    /// </summary>
    public const string LegacyDefaultNamePattern = @"^(?:\d{8}_\d{6}_|[^_\-]*[_\-])(.+?)[_\-]";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>ファイル名（拡張子含む）に対する正規表現。</summary>
    public string NamePattern { get; set; } = string.Empty;

    /// <summary>表示用の話者名（任意）。</summary>
    public string SpeakerName { get; set; } = string.Empty;

    /// <summary>一致時に投入するレイヤー。</summary>
    public int Layer { get; set; } = 1;

    public bool Enabled { get; set; } = true;
}

/// <summary>
/// 合成音声ソフトが書き出した「同名 wav+txt」を、V-Adapter 側で整えるための設定。
///
/// 保存ダイアログを書き換える方式（保存先を合成音声ソフトへ指示する）は、ソフトごとに
/// ダイアログの実装・入力制限が異なり対応コストが増え続けるため採らない。
/// 「保存された後に引き取って整える」ことで、合成音声ソフトへの依存を持たずに
/// 改名・話者別の整理・別フォルダへの配布をまとめて行う。
/// </summary>
public sealed class FilePostProcessOptions
{
    /// <summary>後処理を行うか（既定 OFF＝従来どおり何もしない）。</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 旧形式の後処理専用監視フォルダ。移行専用に残しており、現在は
    /// <see cref="IntegrationSettings.WatchFolders"/> が唯一の監視リスト。
    /// </summary>
    public List<WatchFolder> Folders { get; set; } = new();

    /// <summary>
    /// 改名テンプレート。空なら改名しない。
    /// トークン: <c>{name}</c>（元のファイル名・拡張子なし）、<c>{speaker}</c>（話者名）、
    /// <c>{date}</c> / <c>{date:書式}</c>（現在日時）。
    /// 先頭に日時を付けるなら <c>{date}_{name}</c>、末尾なら <c>{name}_{date}</c>。
    /// 解決結果が空になったトークンは、隣接する区切り文字ごと取り除かれる。
    /// </summary>
    public string NameTemplate { get; set; } = string.Empty;

    /// <summary>
    /// 整えた後の置き場所。空なら移動せず、保存された場所に置いたままにする。
    /// YMM4 のカスタムボイスフォルダなど、編集ソフトに読み込ませたい場所を指定する。
    /// </summary>
    public string? DestinationFolder { get; set; }

    /// <summary>話者名のサブフォルダへ振り分けるか（話者が判定できた場合のみ）。</summary>
    public bool SpeakerSubfolder { get; set; }

    /// <summary>
    /// 話者サブフォルダをキャラクター単位にまとめるためのキャラクター名（任意）。
    /// ファイル名から取り出した話者名はユーザープリセット名（例: <c>琴葉 葵(通常)</c>）になり得るため、
    /// 登録名で始まる話者名はその登録名のフォルダへまとめる。空なら従来どおりプリセット単位。
    /// サブフォルダ名にだけ使い、<c>{speaker}</c> の改名には影響させない。
    /// </summary>
    public List<string> SubfolderCharacters { get; set; } = new();

    /// <summary>
    /// 話者名からサブフォルダ名を決める。<see cref="SubfolderCharacters"/> のうち話者名が前方一致する
    /// 最長の登録名を返し、一致が無ければ話者名をそのまま返す。
    /// 全角・半角の空白の違いと大文字・小文字の違いは無視する。
    /// </summary>
    public string SubfolderNameFor(string speaker)
    {
        var target = NormalizeSpaces(speaker.Trim());
        var matched = SubfolderCharacters
            .Select(c => c?.Trim() ?? string.Empty)
            .Where(c => c.Length > 0 && target.StartsWith(NormalizeSpaces(c), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.Length) // 「琴葉」と「琴葉 葵」が両方あれば長い方。
            .FirstOrDefault();
        return matched ?? speaker.Trim();

        static string NormalizeSpaces(string s) => s.Replace('　', ' ');
    }

    /// <summary>
    /// ファイル名から話者名を抽出する正規表現（グループ1が話者名）。空なら
    /// <see cref="SpeakerRule.DefaultNamePattern"/> を使う。
    /// 「投げ込み」タブの話者ルールは AviUtl 系にしか無いため、マクロ動作ベースでも
    /// 話者名を使えるよう後処理側に持たせている。ルールで判定できた場合はそちらを優先。
    /// </summary>
    public string SpeakerPattern { get; set; } = string.Empty;

    /// <summary>「日時_元のファイル名」。後処理を有効にしたときの既定。</summary>
    public const string TemplateDateName = "{date}_{name}";

    /// <summary>「日時_話者名_元のファイル名」。話者ルールがある連携環境のみ有効。</summary>
    public const string TemplateDateSpeakerName = "{date}_{speaker}_{name}";
}
