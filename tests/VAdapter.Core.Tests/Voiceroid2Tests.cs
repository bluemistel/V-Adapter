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
        s.AviUtl.Folders.Add(new WatchFolder { Path = @"C:\aviutl\f0" });
        s.AviUtl.Folders.Add(new WatchFolder { Path = @"C:\aviutl\f1" });
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
}
