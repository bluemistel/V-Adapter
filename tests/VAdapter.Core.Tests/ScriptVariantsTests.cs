using System.Runtime.CompilerServices;
using VAdapter.Core.Models;
using VAdapter.Core.Serialization;

namespace VAdapter.Core.Tests;

public class ScriptVariantsTests
{
    private const string TargetId = "target-a";

    private static (Macro Macro, MacroScript First, MacroScript Second) TwoVariants()
    {
        var first = new MacroScript { TargetApplicationId = TargetId, VariantName = "カーソル位置から再生" };
        var second = new MacroScript { TargetApplicationId = TargetId, VariantName = "ブロックを再生" };
        var other = new MacroScript { TargetApplicationId = "target-b" };
        var macro = new Macro { Name = "音声の再生", Scripts = { first, other, second } };
        return (macro, first, second);
    }

    [Fact]
    public void Resolve_NoChoice_UsesFirst()
    {
        var (macro, first, _) = TwoVariants();
        Assert.Same(first, ScriptVariants.Resolve(macro, TargetId, new IntegrationSettings()));
        Assert.Same(first, ScriptVariants.Resolve(macro, TargetId, settings: null));
    }

    [Fact]
    public void Resolve_UsesChosenScript()
    {
        var (macro, _, second) = TwoVariants();
        var settings = new IntegrationSettings();
        settings.ScriptChoices[ScriptVariants.Key(macro.Id, TargetId)] = second.Id;

        Assert.Same(second, ScriptVariants.Resolve(macro, TargetId, settings));
    }

    [Fact]
    public void Resolve_ChosenScriptRemoved_FallsBackToFirst()
    {
        var (macro, first, _) = TwoVariants();
        var settings = new IntegrationSettings();
        settings.ScriptChoices[ScriptVariants.Key(macro.Id, TargetId)] = "deleted-script";

        Assert.Same(first, ScriptVariants.Resolve(macro, TargetId, settings));
    }

    [Fact]
    public void Resolve_ChoiceForOtherMacro_IsIgnored()
    {
        var (macro, first, second) = TwoVariants();
        var settings = new IntegrationSettings();
        settings.ScriptChoices[ScriptVariants.Key("other-macro", TargetId)] = second.Id;

        Assert.Same(first, ScriptVariants.Resolve(macro, TargetId, settings));
    }

    [Fact]
    public void Resolve_NoScriptForTarget_ReturnsNull()
    {
        var (macro, _, _) = TwoVariants();
        Assert.Null(ScriptVariants.Resolve(macro, "target-c", new IntegrationSettings()));
    }

    [Fact]
    public void DisplayName_UsesVariantNameOrNumber()
    {
        var (macro, first, second) = TwoVariants();
        second.VariantName = null;

        Assert.Equal("カーソル位置から再生", ScriptVariants.DisplayName(macro, first));
        Assert.Equal("方式 2", ScriptVariants.DisplayName(macro, second));
    }

    [Fact]
    public void ScriptChoices_RoundTrip()
    {
        var settings = new IntegrationSettings();
        settings.ScriptChoices["m1/t1"] = "s2";
        var restored = VAdapterJson.Deserialize<IntegrationSettings>(VAdapterJson.Serialize(settings))!;
        Assert.Equal("s2", restored.ScriptChoices["m1/t1"]);
    }

    [Fact]
    public void BuiltInPlayMacro_VoicepeakHasBothVariants_SpaceFirst()
    {
        // 既定（未選択）は従来どおり Space の方式。ブロック再生は Ctrl+B を送る。
        var dir = new DirectoryInfo(Path.GetDirectoryName(ThisFilePath())!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "V-Adapter.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var path = Path.Combine(dir!.FullName, "src", "VAdapter.App", "script", "built-in", "音声の再生.vamacro");
        var bundle = VAdapterJson.Deserialize<MacroBundle>(File.ReadAllText(path))!;

        var voicepeak = bundle.Targets.Single(t => t.ProcessName == "voicepeak");
        var macro = bundle.Macros.Single();
        var variants = ScriptVariants.For(macro, voicepeak.Id);

        Assert.Equal(2, variants.Count);
        Assert.Same(variants[0], ScriptVariants.Resolve(macro, voicepeak.Id, new IntegrationSettings()));
        Assert.Equal(32, variants[0].Instructions.OfType<SendKeysInstruction>().Last().KeyCombination.VirtualKey);

        var block = variants[1].Instructions.OfType<SendKeysInstruction>().Single().KeyCombination;
        Assert.Equal(KeyModifiers.Control, block.Modifiers);
        Assert.Equal(66, block.VirtualKey);
    }

    private static string ThisFilePath([CallerFilePath] string path = "") => path;
}
