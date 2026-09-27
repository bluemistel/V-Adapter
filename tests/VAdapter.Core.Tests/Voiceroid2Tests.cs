using VAdapter.Core.Media;
using VAdapter.Core.Models;
using VAdapter.Core.Serialization;

namespace VAdapter.Core.Tests;

public class Voiceroid2Tests
{
    // --- SaveNameComposer ---

    private static readonly DateTime Now = new(2026, 7, 1, 9, 8, 7);

    [Fact]
    public void ResolveName_DefaultTemplate_DateCharacterSerifu10()
    {
        var vars = new Dictionary<string, string>
        {
            ["character"] = "ずんだもん",
            ["serifu"] = "こんにちは、これはテストの音声です。",
        };
        var name = SaveNameComposer.ResolveName(SaveNameComposer.DefaultTemplate, vars, Now);
        Assert.Equal("20260701_090807_ずんだもん_こんにちは、これはテ", name); // serifu 先頭10文字
    }

    [Fact]
    public void ResolveName_CustomDateFormat()
    {
        var name = SaveNameComposer.ResolveName("{date:yyyyMMdd}_{character}", new Dictionary<string, string> { ["character"] = "四国めたん" }, Now);
        Assert.Equal("20260701_四国めたん", name);
    }

    [Fact]
    public void ResolveName_Sanitizes_InvalidChars()
    {
        var vars = new Dictionary<string, string> { ["character"] = "a/b:c*d" };
        var name = SaveNameComposer.ResolveName("{character}", vars, Now);
        Assert.DoesNotContain('/', name);
        Assert.DoesNotContain(':', name);
        Assert.DoesNotContain('*', name);
        Assert.Equal("a_b_c_d", name);
    }

    [Fact]
    public void ResolvePath_CombinesFolderAndExtension()
    {
        var vars = new Dictionary<string, string> { ["character"] = "A", ["serifu"] = "テスト" };
        var path = SaveNameComposer.ResolvePath(@"C:\out", "{character}_{serifu}", vars, Now, "wav");
        Assert.Equal(@"C:\out\A_テスト.wav", path);
    }

    [Fact]
    public void ResolveName_MissingVariable_BecomesEmpty()
    {
        var name = SaveNameComposer.ResolveName("{unknown}x", new Dictionary<string, string>(), Now);
        Assert.Equal("x", name);
    }

    // --- ResolveVoiceroid2Folder ---

    private static IntegrationSettings SettingsWithVoiceroid2()
    {
        var s = new IntegrationSettings();
        s.Voiceroid2.Enabled = true;
        s.Voiceroid2.Characters.Add(new Voiceroid2Character
        {
            Name = "ずんだもん",
            MacroBaseFolder = @"C:\ymm\zunda",
            MonitorFolderIndex = 1,
        });
        s.WatchFolders.Add(new WatchFolder { Path = @"C:\aviutl\f0" });
        s.WatchFolders.Add(new WatchFolder { Path = @"C:\aviutl\f1" });
        return s;
    }

    [Fact]
    public void ResolveFolder_MacroOnly_UsesExplicitFolder()
    {
        var s = SettingsWithVoiceroid2();
        Assert.Equal(@"C:\ymm\zunda", s.ResolveVoiceroid2Folder(IntegrationMode.MacroOnly, "ずんだもん"));
    }

    [Fact]
    public void ResolveFolder_AviUtl_UsesMonitorFolderIndex()
    {
        var s = SettingsWithVoiceroid2();
        Assert.Equal(@"C:\aviutl\f1", s.ResolveVoiceroid2Folder(IntegrationMode.AviUtl, "ずんだもん"));
    }

    [Fact]
    public void ResolveFolder_AviUtl_Unmatched_FallsBackToFirstFolder()
    {
        var s = SettingsWithVoiceroid2();
        Assert.Equal(@"C:\aviutl\f0", s.ResolveVoiceroid2Folder(IntegrationMode.AviUtl, "未登録キャラ"));
    }

    [Fact]
    public void ResolveFolder_MacroOnly_Unmatched_ReturnsNull()
    {
        var s = SettingsWithVoiceroid2();
        Assert.Null(s.ResolveVoiceroid2Folder(IntegrationMode.MacroOnly, "未登録キャラ"));
    }

    [Fact]
    public void ResolveFolder_IndexOutOfRange_FallsBackToFirst()
    {
        var s = SettingsWithVoiceroid2();
        s.Voiceroid2.Characters[0].MonitorFolderIndex = 9;
        Assert.Equal(@"C:\aviutl\f0", s.ResolveVoiceroid2Folder(IntegrationMode.AviUtl, "ずんだもん"));
    }

    // --- Round-trip ---

    [Fact]
    public void Voiceroid2Options_RoundTrips()
    {
        var s = SettingsWithVoiceroid2();
        var restored = VAdapterJson.Deserialize<IntegrationSettings>(VAdapterJson.Serialize(s))!;
        Assert.True(restored.Voiceroid2.Enabled);
        var c = Assert.Single(restored.Voiceroid2.Characters);
        Assert.Equal("ずんだもん", c.Name);
        Assert.Equal(@"C:\ymm\zunda", c.MacroBaseFolder);
        Assert.Equal(1, c.MonitorFolderIndex);
    }

    [Fact]
    public void NewInstructions_RoundTrip_AsPolymorphic()
    {
        var instrs = new List<Instruction>
        {
            new ReadUiTextInstruction { VariableName = "character", Selector = new UiElementSelector { AutomationId = "CharName", ControlType = "Text" } },
            new SetSaveFileNameInstruction { FileNameTemplate = "{date}_{character}_{serifu:10}", Extension = ".wav" },
            new WriteSubtitleInstruction { PathVariable = "savepath", TextVariable = "serifu" },
        };
        var json = VAdapterJson.Serialize(instrs);
        Assert.Contains("\"kind\": \"readui\"", json);
        Assert.Contains("\"kind\": \"savename\"", json);
        Assert.Contains("\"kind\": \"writesubtitle\"", json);

        var restored = VAdapterJson.Deserialize<List<Instruction>>(json)!;
        var read = Assert.IsType<ReadUiTextInstruction>(restored[0]);
        Assert.Equal("character", read.VariableName);
        Assert.Equal("CharName", read.Selector.AutomationId);
        Assert.IsType<SetSaveFileNameInstruction>(restored[1]);
        Assert.IsType<WriteSubtitleInstruction>(restored[2]);
    }

    // --- ユーザープリセット名（キャラクター名＋任意の文字列）の照合 ---

    [Theory]
    [InlineData("紲星あかり - コピー")]
    [InlineData("紲星あかり_ささやき")]
    [InlineData("紲星あかり(高め)")]
    [InlineData("紲星あかり2")]
    [InlineData("紲星あかり")]
    public void MatchCharacter_PresetNames_ResolveToBaseCharacter(string uiName)
    {
        var s = new IntegrationSettings();
        s.Voiceroid2.Characters.Add(new Voiceroid2Character { Name = "紲星あかり", MacroBaseFolder = @"C:\voice\akari" });

        Assert.Equal("紲星あかり", s.CanonicalVoiceroid2Name(uiName));
        Assert.Equal(@"C:\voice\akari", s.ResolveVoiceroid2Folder(IntegrationMode.MacroOnly, uiName));
    }

    [Fact]
    public void MatchCharacter_PrefersLongestRegisteredName()
    {
        var s = new IntegrationSettings();
        s.Voiceroid2.Characters.Add(new Voiceroid2Character { Name = "あかり", MacroBaseFolder = @"C:\short" });
        s.Voiceroid2.Characters.Add(new Voiceroid2Character { Name = "あかりだいすき", MacroBaseFolder = @"C:\long" });

        Assert.Equal("あかりだいすき", s.CanonicalVoiceroid2Name("あかりだいすき - コピー"));
        Assert.Equal(@"C:\long", s.ResolveVoiceroid2Folder(IntegrationMode.MacroOnly, "あかりだいすき - コピー"));
    }

    [Fact]
    public void MatchCharacter_ExactMatchWinsOverPrefix()
    {
        var s = new IntegrationSettings();
        s.Voiceroid2.Characters.Add(new Voiceroid2Character { Name = "あかり", MacroBaseFolder = @"C:\exact" });
        s.Voiceroid2.Characters.Add(new Voiceroid2Character { Name = "あか", MacroBaseFolder = @"C:\prefix" });

        Assert.Equal(@"C:\exact", s.ResolveVoiceroid2Folder(IntegrationMode.MacroOnly, "あかり"));
    }

    [Fact]
    public void MatchCharacter_SuffixOnly_DoesNotMatch()
    {
        var s = new IntegrationSettings();
        s.Voiceroid2.Characters.Add(new Voiceroid2Character { Name = "紲星あかり", MacroBaseFolder = @"C:\voice" });

        // 前方一致のみ。名前が後ろに含まれるだけのものは別キャラとして扱う。
        Assert.Null(s.MatchVoiceroid2Character("コピー - 紲星あかり"));
    }

    [Fact]
    public void CanonicalName_Unregistered_ReturnsInputUnchanged()
    {
        var s = new IntegrationSettings();
        s.Voiceroid2.Characters.Add(new Voiceroid2Character { Name = "紲星あかり" });

        Assert.Equal("東北きりたん", s.CanonicalVoiceroid2Name("東北きりたん"));
    }
}
