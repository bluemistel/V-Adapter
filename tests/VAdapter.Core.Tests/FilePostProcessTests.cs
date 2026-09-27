using VAdapter.Core.Media;
using VAdapter.Core.Models;
using Xunit;

namespace VAdapter.Core.Tests;

public class FilePostProcessTests
{
    private static readonly DateTime Now = new(2026, 8, 30, 14, 46, 53);
    private const string Source = @"C:\out\Voice\4-星界-セリフ.wav";

    private static FilePostProcessOptions Options() => new() { Enabled = true };

    // --- 移動先 ---

    [Fact]
    public void NoDestination_KeepsOriginalFolder()
    {
        var plan = FilePostProcessPlanner.Create(Source, "星界", Options(), Now);
        Assert.Equal(@"C:\out\Voice", plan.TargetDirectory);
    }

    [Fact]
    public void Destination_MovesToConfiguredFolder()
    {
        var o = Options();
        o.DestinationFolder = @"D:\project\voice";
        var plan = FilePostProcessPlanner.Create(Source, "星界", o, Now);
        Assert.Equal(@"D:\project\voice", plan.TargetDirectory);
    }

    [Fact]
    public void SpeakerSubfolder_AppendsSpeakerName()
    {
        var o = Options();
        o.DestinationFolder = @"D:\project\voice";
        o.SpeakerSubfolder = true;
        var plan = FilePostProcessPlanner.Create(Source, "星界", o, Now);
        Assert.Equal(@"D:\project\voice\星界", plan.TargetDirectory);
    }

    [Fact]
    public void SpeakerSubfolder_WithoutSpeaker_DoesNotCreateUnknownFolder()
    {
        var o = Options();
        o.SpeakerSubfolder = true;
        var plan = FilePostProcessPlanner.Create(Source, speaker: null, o, Now);
        Assert.Equal(@"C:\out\Voice", plan.TargetDirectory);
    }

    // --- 改名 ---

    [Fact]
    public void EmptyTemplate_KeepsOriginalName()
    {
        var plan = FilePostProcessPlanner.Create(Source, "星界", Options(), Now);
        Assert.Equal("4-星界-セリフ", plan.BaseName);
    }

    [Fact]
    public void Template_PrefixesDate()
    {
        var o = Options();
        o.NameTemplate = "{date}_{name}";
        var plan = FilePostProcessPlanner.Create(Source, "星界", o, Now);
        Assert.Equal("20260830_144653_4-星界-セリフ", plan.BaseName);
    }

    [Fact]
    public void Template_SuffixesDate()
    {
        var o = Options();
        o.NameTemplate = "{name}_{date:yyyyMMdd}";
        var plan = FilePostProcessPlanner.Create(Source, "星界", o, Now);
        Assert.Equal("4-星界-セリフ_20260830", plan.BaseName);
    }

    [Fact]
    public void Template_CanUseSpeaker()
    {
        var o = Options();
        o.NameTemplate = "{speaker}_{name}";
        var plan = FilePostProcessPlanner.Create(Source, "星界", o, Now);
        Assert.Equal("星界_4-星界-セリフ", plan.BaseName);
    }

    [Fact]
    public void Template_InvalidFileNameChars_AreSanitized()
    {
        var o = Options();
        o.NameTemplate = @"{name}:<test>";
        var plan = FilePostProcessPlanner.Create(Source, "星界", o, Now);
        Assert.DoesNotContain(':', plan.BaseName);
        Assert.DoesNotContain('<', plan.BaseName);
    }

    // --- 話者名が取れない環境（マクロ動作ベース等）での空トークン ---

    [Fact]
    public void EmptySpeaker_DoesNotLeaveDoubledSeparator()
    {
        // 話者ルールを持たない連携環境では {speaker} が空になる。
        // 「20260830_144653__4-星界-セリフ」のように区切りが二重になってはいけない。
        var o = Options();
        o.NameTemplate = FilePostProcessOptions.TemplateDateSpeakerName;
        var plan = FilePostProcessPlanner.Create(Source, speaker: null, o, Now);
        Assert.Equal("20260830_144653_4-星界-セリフ", plan.BaseName);
        Assert.DoesNotContain("__", plan.BaseName);
    }

    [Fact]
    public void EmptySpeaker_AtEnd_DoesNotLeaveTrailingSeparator()
    {
        var o = Options();
        o.NameTemplate = "{name}_{speaker}";
        var plan = FilePostProcessPlanner.Create(Source, speaker: null, o, Now);
        Assert.Equal("4-星界-セリフ", plan.BaseName);
    }

    [Fact]
    public void EmptySpeaker_WithHyphenSeparator_IsCollapsed()
    {
        var o = Options();
        o.NameTemplate = "{date:yyyyMMdd}-{speaker}-{name}";
        var plan = FilePostProcessPlanner.Create(Source, speaker: "   ", o, Now);
        Assert.Equal("20260830-4-星界-セリフ", plan.BaseName);
    }

    [Fact]
    public void PresentSpeaker_KeepsSeparators()
    {
        var o = Options();
        o.NameTemplate = FilePostProcessOptions.TemplateDateSpeakerName;
        var plan = FilePostProcessPlanner.Create(Source, "星界", o, Now);
        Assert.Equal("20260830_144653_星界_4-星界-セリフ", plan.BaseName);
    }

    // --- 連携環境に依らない話者名の抽出 ---

    [Theory]
    [InlineData("01_ついなちゃん_テストです.wav", "ついなちゃん")]
    [InlineData("1_さとうささら_ささらさらさら。.wav", "さとうささら")]
    [InlineData("4-星界-セリフ.wav", "星界")]
    [InlineData("001_東北きりたん（ノーマル）_台詞.wav", "東北きりたん（ノーマル）")]
    // VOICEROID2 は V-Adapter 側が「日時_話者_本文」で保存する。時刻を話者と誤認しないこと。
    [InlineData("20260927_102140_紲星あかり_はーとふる.wav", "紲星あかり")]
    [InlineData("20260830_144653_紲星あかり_テストです.wav", "紲星あかり")]
    public void ExtractSpeaker_UsesDefaultPattern(string fileName, string expected) =>
        Assert.Equal(expected, DropRouting.ExtractSpeaker(fileName, pattern: null));

    [Theory]
    [InlineData("話者名なし.wav")]
    [InlineData("")]
    public void ExtractSpeaker_ReturnsNullWhenUnmatched(string fileName) =>
        Assert.Null(DropRouting.ExtractSpeaker(fileName, pattern: null));

    [Fact]
    public void ExtractSpeaker_InvalidPattern_ReturnsNull() =>
        Assert.Null(DropRouting.ExtractSpeaker("01_ささら_台詞.wav", pattern: "([unclosed"));

    [Fact]
    public void ExtractSpeaker_CustomPattern_IsHonored() =>
        Assert.Equal("ついな", DropRouting.ExtractSpeaker("voice[ついな].wav", @"\[(.+?)\]"));

    [Fact]
    public void ExtractSpeaker_EnablesSpeakerTokenWithoutSpeakerRules()
    {
        // マクロ動作ベース（話者ルールを持たない連携環境）でも {speaker} が埋まること。
        var speaker = DropRouting.ExtractSpeaker("01_ついなちゃん_テストです.wav", null);
        var o = Options();
        o.NameTemplate = FilePostProcessOptions.TemplateDateSpeakerName;

        var plan = FilePostProcessPlanner.Create(
            @"C:\out\Voice\01_ついなちゃん_テストです.wav", speaker, o, Now);

        Assert.Equal("20260830_144653_ついなちゃん_01_ついなちゃん_テストです", plan.BaseName);
    }

    // --- 既に日時が付いた名前（VOICEROID2）の二重付与 ---

    private const string Voiceroid2Source = @"C:\out\Voice\20260927_102140_紲星あかり_はーとふる.wav";

    [Fact]
    public void ExistingTimestamp_IsNotDuplicated()
    {
        // V-Adapter が VOICEROID2 用に付けた日時と、後処理の日時が二重にならないこと。
        var o = Options();
        o.NameTemplate = FilePostProcessOptions.TemplateDateName;
        var plan = FilePostProcessPlanner.Create(Voiceroid2Source, "紲星あかり", o, Now);

        Assert.Equal("20260830_144653_紲星あかり_はーとふる", plan.BaseName);
        Assert.DoesNotContain("20260927", plan.BaseName);
    }

    [Fact]
    public void ExistingTimestamp_IsStrippedRegardlessOfDatePosition()
    {
        var o = Options();
        o.NameTemplate = "{name}_{date}";
        var plan = FilePostProcessPlanner.Create(Voiceroid2Source, "紲星あかり", o, Now);

        Assert.Equal("紲星あかり_はーとふる_20260830_144653", plan.BaseName);
    }

    [Fact]
    public void ExistingTimestamp_IsKeptWhenTemplateHasNoDate()
    {
        // 日時を付けないテンプレートでは、元の名前の日時をそのまま残す。
        var o = Options();
        o.NameTemplate = "{speaker}_{name}";
        var plan = FilePostProcessPlanner.Create(Voiceroid2Source, "紲星あかり", o, Now);

        Assert.Equal("紲星あかり_20260927_102140_紲星あかり_はーとふる", plan.BaseName);
    }

    [Fact]
    public void TimestampOnlyName_IsNotEmptied()
    {
        var o = Options();
        o.NameTemplate = FilePostProcessOptions.TemplateDateName;
        var plan = FilePostProcessPlanner.Create(@"C:\out\20260927_102140_.wav", null, o, Now);

        Assert.Equal("20260830_144653_20260927_102140_", plan.BaseName);
    }

    [Fact]
    public void NonTimestampPrefix_IsNotStripped()
    {
        // 8桁+6桁の数字列でなければ日時とみなさない。
        var o = Options();
        o.NameTemplate = FilePostProcessOptions.TemplateDateName;
        var plan = FilePostProcessPlanner.Create(@"C:\out\01_ついな_テスト.wav", "ついな", o, Now);

        Assert.Equal("20260830_144653_01_ついな_テスト", plan.BaseName);
    }

    [Fact]
    public void Voiceroid2_SpeakerSubfolderUsesRealSpeaker()
    {
        // 時刻（102140）がフォルダ名になってしまう問題の再発防止。
        var speaker = DropRouting.ExtractSpeaker("20260927_102140_紲星あかり_はーとふる.wav", null);
        var o = Options();
        o.SpeakerSubfolder = true;
        var plan = FilePostProcessPlanner.Create(Voiceroid2Source, speaker, o, Now);

        Assert.Equal(@"C:\out\Voice\紲星あかり", plan.TargetDirectory);
    }

    // --- 話者判定との順序 ---

    [Fact]
    public void SpeakerMustBeResolvedBeforeRenaming()
    {
        // 既定の話者パターンは「先頭トークン＋区切り」を前提にしているため、
        // 改名で先頭に日時を足すと別の位置を拾ってしまう。
        // パイプラインが改名前に話者を解決していることを、この性質で示す。
        var config = new AviutlDropConfig { DefaultLayer = 1 };
        config.Rules.Add(new SpeakerRule
        {
            NamePattern = @"^[^_\-]*[_\-](.+?)[_\-]",
            SpeakerName = "星界",
            Layer = 8,
        });

        var before = DropRouting.Resolve("4-星界-セリフ.wav", config);
        Assert.Equal(8, before.Layer);

        var o = Options();
        o.NameTemplate = "{date}_{name}";
        var renamed = FilePostProcessPlanner.Create(Source, before.Speaker, o, Now).BaseName + ".wav";

        var after = DropRouting.Resolve(renamed, config);
        Assert.NotEqual(8, after.Layer); // 改名後に解決すると振り分けが壊れる
    }

    // --- 設定の保存・読み込み ---

    [Fact]
    public void Options_SurviveSerializationRoundTrip()
    {
        var settings = new IntegrationSettings();
        settings.WatchFolders.Add(new WatchFolder { Path = @"C:\out", IncludeSubdirectories = true });
        var pp = settings.PostProcess;
        pp.Enabled = true;
        pp.NameTemplate = FilePostProcessOptions.TemplateDateName;
        pp.DestinationFolder = @"D:\ymm4\custom";
        pp.SpeakerSubfolder = true;

        var json = VAdapter.Core.Serialization.VAdapterJson.Serialize(settings);
        var back = VAdapter.Core.Serialization.VAdapterJson.Deserialize<IntegrationSettings>(json);

        Assert.NotNull(back);
        var r = back!.PostProcess;
        Assert.True(r.Enabled);
        Assert.Equal("{date}_{name}", r.NameTemplate);
        Assert.Equal(@"D:\ymm4\custom", r.DestinationFolder);
        Assert.True(r.SpeakerSubfolder);
        Assert.Equal(@"C:\out", Assert.Single(back.WatchFolders).Path);
        Assert.True(back.WatchFolders[0].IncludeSubdirectories);
    }

    [Fact]
    public void Options_DefaultIsDisabled()
    {
        var settings = new IntegrationSettings();
        Assert.False(settings.PostProcess.Enabled);
    }

    // --- 監視フォルダの統合（旧形式からの移行） ---

    [Fact]
    public void Migration_TakesFoldersFromActiveMode()
    {
        var old = new IntegrationSettings { Version = 1, ActiveMode = IntegrationMode.AviUtl2 };
        old.AviUtl.Folders.Add(new WatchFolder { Path = @"C:\old\aviutl" });
        old.AviUtl2.Folders.Add(new WatchFolder { Path = @"C:\old\aviutl2", IncludeSubdirectories = true });

        Assert.True(old.MigrateWatchFolders());

        // アクティブモードの設定が先頭（VOICEROID2 の保存先番号がここを基準にしているため）。
        Assert.Equal(@"C:\old\aviutl2", old.WatchFolders[0].Path);
        Assert.True(old.WatchFolders[0].IncludeSubdirectories);
        Assert.Equal(@"C:\old\aviutl", old.WatchFolders[1].Path);
        Assert.Equal(IntegrationSettings.CurrentVersion, old.Version);
    }

    [Fact]
    public void Migration_TakesLegacyPostProcessFolders()
    {
        // マクロ動作ベースでは後処理専用の監視フォルダにだけ値が入っていた。
        var old = new IntegrationSettings { Version = 1, ActiveMode = IntegrationMode.MacroOnly };
        old.PostProcess.Folders.Add(new WatchFolder { Path = @"C:\old\postprocess" });

        Assert.True(old.MigrateWatchFolders());
        Assert.Equal(@"C:\old\postprocess", Assert.Single(old.WatchFolders).Path);
    }

    [Fact]
    public void Migration_DeduplicatesByPath()
    {
        var old = new IntegrationSettings { Version = 1, ActiveMode = IntegrationMode.AviUtl };
        old.AviUtl.Folders.Add(new WatchFolder { Path = @"C:\out" });
        old.AviUtl2.Folders.Add(new WatchFolder { Path = @"C:\out\" });
        old.PostProcess.Folders.Add(new WatchFolder { Path = @"C:\out" });

        Assert.True(old.MigrateWatchFolders());
        Assert.Single(old.WatchFolders);
    }

    [Fact]
    public void Migration_IsSkippedForCurrentVersion()
    {
        // 既に移行済みの設定で、意図的に空にした監視フォルダを旧値で埋め戻さない。
        var current = new IntegrationSettings { ActiveMode = IntegrationMode.AviUtl };
        current.AviUtl.Folders.Add(new WatchFolder { Path = @"C:\stale" });

        Assert.False(current.MigrateWatchFolders());
        Assert.Empty(current.WatchFolders);
    }

    [Fact]
    public void Migration_RoundTripsFromLegacyJson()
    {
        // v0.0.6 が実際に書き出す形（camelCase）。
        const string legacy = """
            {
              "version": 1,
              "activeMode": "AviUtl2",
              "aviUtl2": { "folders": [ { "path": "C:\\voice\\out", "includeSubdirectories": true } ] }
            }
            """;

        var restored = VAdapter.Core.Serialization.VAdapterJson.Deserialize<IntegrationSettings>(legacy);
        Assert.NotNull(restored);
        Assert.True(restored!.MigrateWatchFolders());

        var folder = Assert.Single(restored.WatchFolders);
        Assert.Equal(@"C:\voice\out", folder.Path);
        Assert.True(folder.IncludeSubdirectories);
    }
}
