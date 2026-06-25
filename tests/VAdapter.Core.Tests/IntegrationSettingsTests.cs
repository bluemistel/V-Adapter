using VAdapter.Core.Models;
using VAdapter.Core.Serialization;
using VAdapter.Core.Storage;

namespace VAdapter.Core.Tests;

public class IntegrationSettingsTests
{
    private static IntegrationSettings Sample()
    {
        var s = new IntegrationSettings { ActiveMode = IntegrationMode.AviUtl };
        s.AviUtl.DefaultLayer = 3;
        s.AviUtl.FrameAdvance = 12;
        s.AviUtl.StableWaitMs = 2000;
        s.AviUtl.Folders.Add(new WatchFolder { Path = @"C:\voice\out", IncludeSubdirectories = true });
        s.AviUtl.Rules.Add(new SpeakerRule { NamePattern = @"_ずんだもん_", SpeakerName = "ずんだもん", Layer = 5 });
        s.AviUtl2.DefaultLayer = 1;
        s.AviUtl2.Margin = 10;
        return s;
    }

    [Fact]
    public void Settings_RoundTrips_PerEnvironment()
    {
        var json = VAdapterJson.Serialize(Sample());
        var restored = VAdapterJson.Deserialize<IntegrationSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal(IntegrationMode.AviUtl, restored!.ActiveMode);
        Assert.Equal(3, restored.AviUtl.DefaultLayer);
        Assert.Equal(12, restored.AviUtl.FrameAdvance);
        var folder = Assert.Single(restored.AviUtl.Folders);
        Assert.True(folder.IncludeSubdirectories);
        var rule = Assert.Single(restored.AviUtl.Rules);
        Assert.Equal("ずんだもん", rule.SpeakerName);
        Assert.Equal(5, rule.Layer);
        // 環境別に独立していること
        Assert.Equal(10, restored.AviUtl2.Margin);
        Assert.Equal(1, restored.AviUtl2.DefaultLayer);
    }

    [Fact]
    public void ActiveMode_SerializesAsString()
    {
        var json = VAdapterJson.Serialize(new IntegrationSettings { ActiveMode = IntegrationMode.AviUtl2 });
        Assert.Contains("AviUtl2", json);
        Assert.DoesNotContain("\"activeMode\": 2", json);
    }

    [Fact]
    public void ConfigFor_ReturnsPerModeConfig()
    {
        var s = Sample();
        Assert.Same(s.AviUtl, s.ConfigFor(IntegrationMode.AviUtl));
        Assert.Same(s.AviUtl2, s.ConfigFor(IntegrationMode.AviUtl2));
        Assert.Null(s.ConfigFor(IntegrationMode.MacroOnly));
    }

    [Fact]
    public void Store_Save_Then_Load_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), "vadapter-int-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new IntegrationSettingsStore(path);
            store.Save(Sample());
            var loaded = store.Load();
            Assert.Equal(IntegrationMode.AviUtl, loaded.ActiveMode);
            Assert.Equal(3, loaded.AviUtl.DefaultLayer);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Store_Load_Missing_ReturnsMacroOnly()
    {
        var store = new IntegrationSettingsStore(
            Path.Combine(Path.GetTempPath(), "vadapter-int-missing-" + Guid.NewGuid().ToString("N") + ".json"));
        Assert.Equal(IntegrationMode.MacroOnly, store.Load().ActiveMode);
    }

    [Theory]
    [InlineData("0001_ずんだもん_こんにちは.wav", 5, "ずんだもん")]
    [InlineData("0002_四国めたん_テスト.wav", 7, "四国めたん")]
    [InlineData("0003_不明話者_xxx.wav", 1, null)] // 未一致 → 既定レイヤー
    public void Resolve_RoutesByRule(string fileName, int expectedLayer, string? expectedSpeaker)
    {
        var config = new AviutlDropConfig { DefaultLayer = 1 };
        config.Rules.Add(new SpeakerRule { NamePattern = "ずんだもん", SpeakerName = "ずんだもん", Layer = 5 });
        config.Rules.Add(new SpeakerRule { NamePattern = "四国めたん", SpeakerName = "四国めたん", Layer = 7 });

        var route = DropRouting.Resolve(fileName, config);

        Assert.Equal(expectedLayer, route.Layer);
        Assert.Equal(expectedSpeaker, route.Speaker);
    }

    [Fact]
    public void Resolve_ExtractsSpeakerFromCaptureGroup_WhenNoName()
    {
        var config = new AviutlDropConfig { DefaultLayer = 2 };
        config.Rules.Add(new SpeakerRule { NamePattern = @"^\d+_(.+?)_", SpeakerName = "", Layer = 4 });

        var route = DropRouting.Resolve("0007_春日部つむぎ_やあ.wav", config);

        Assert.Equal(4, route.Layer);
        Assert.Equal("春日部つむぎ", route.Speaker);
    }

    [Theory]
    [InlineData("04_IA_台詞.wav", "IA")]
    [InlineData("2-彩澄りりせ-台詞-プロジェクト名.wav", "彩澄りりせ")]
    [InlineData("001_東北きりたん（ノーマル）_台詞.wav", "東北きりたん（ノーマル）")]
    public void DefaultPattern_ExtractsSpeaker_FromUnderscoreOrHyphen(string fileName, string expectedSpeaker)
    {
        var config = new AviutlDropConfig { DefaultLayer = 2 };
        config.Rules.Add(new SpeakerRule { NamePattern = SpeakerRule.DefaultNamePattern, SpeakerName = "", Layer = 3 });

        var route = DropRouting.Resolve(fileName, config);

        Assert.Equal(3, route.Layer);
        Assert.Equal(expectedSpeaker, route.Speaker);
    }

    [Fact]
    public void Resolve_SkipsDisabledAndInvalidRules()
    {
        var config = new AviutlDropConfig { DefaultLayer = 9 };
        config.Rules.Add(new SpeakerRule { NamePattern = "test", Layer = 3, Enabled = false });
        config.Rules.Add(new SpeakerRule { NamePattern = "(", Layer = 4 }); // 不正な正規表現

        var route = DropRouting.Resolve("test.wav", config);

        Assert.Equal(9, route.Layer); // どちらも採用されず既定
    }
}
