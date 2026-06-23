using VAdapter.Core.Models;
using VAdapter.Core.Serialization;

namespace VAdapter.Core.Tests;

public class SerializationTests
{
    private static MacroLibrary SampleLibrary()
    {
        var target = new TargetApplication
        {
            Id = "target-1",
            Name = "VOICEVOX",
            ProcessName = "VOICEVOX",
            WindowTitlePattern = "VOICEVOX",
            CoordinateMode = CoordinateMode.Relative,
        };

        var macro = new Macro
        {
            Id = "macro-1",
            Name = "音声の保存",
            IsBuiltIn = true,
            Shortcut = new KeyCombination(KeyModifiers.Control, 0x45, "E"),
            Scripts =
            {
                new MacroScript
                {
                    TargetApplicationId = target.Id,
                    Instructions =
                    {
                        new WaitForActivationInstruction { TimeoutMs = 3000 },
                        new WaitForWindowInstruction { TimeoutMs = 3000 },
                        new SendKeysInstruction
                        {
                            KeyCombination = new KeyCombination(KeyModifiers.Control, 0x45, "E"),
                        },
                        new WaitForTextInstruction
                        {
                            Text = "書き出し",
                            TimeoutMs = 8000,
                            Region = new RelativeRect { X = 0, Y = 0, Width = 400, Height = 200 },
                        },
                        new ClickInstruction
                        {
                            X = 120,
                            Y = 80,
                            CoordinateModeOverride = CoordinateMode.Relative,
                            Button = MouseButton.Left,
                        },
                        new WaitInstruction { DurationMs = 250 },
                    },
                },
            },
        };

        return new MacroLibrary { Targets = { target }, Macros = { macro } };
    }

    [Fact]
    public void Library_RoundTrips_PreservingScriptsAndInstructionTypes()
    {
        var original = SampleLibrary();

        var json = VAdapterJson.Serialize(original);
        var restored = VAdapterJson.Deserialize<MacroLibrary>(json);

        Assert.NotNull(restored);
        var macro = Assert.Single(restored!.Macros);
        Assert.Equal("音声の保存", macro.Name);
        Assert.True(macro.IsBuiltIn);
        Assert.Equal(KeyModifiers.Control, macro.Shortcut!.Modifiers);

        var script = Assert.Single(macro.Scripts);
        Assert.Equal("target-1", script.TargetApplicationId);

        Assert.Collection(script.Instructions,
            i => Assert.IsType<WaitForActivationInstruction>(i),
            i => Assert.IsType<WaitForWindowInstruction>(i),
            i => Assert.IsType<SendKeysInstruction>(i),
            i =>
            {
                var w = Assert.IsType<WaitForTextInstruction>(i);
                Assert.Equal("書き出し", w.Text);
                Assert.NotNull(w.Region);
                Assert.Equal(400, w.Region!.Width);
            },
            i =>
            {
                var c = Assert.IsType<ClickInstruction>(i);
                Assert.Equal(120, c.X);
                Assert.Equal(CoordinateMode.Relative, c.CoordinateModeOverride);
            },
            i => Assert.Equal(250, Assert.IsType<WaitInstruction>(i).DurationMs));
    }

    [Fact]
    public void Discriminator_IsWrittenAsKind()
    {
        var json = VAdapterJson.Serialize<Instruction>(new WaitForActivationInstruction { TimeoutMs = 10 });
        Assert.Contains("\"kind\": \"waitactivation\"", json);
    }

    [Fact]
    public void Enums_SerializeAsStrings()
    {
        var json = VAdapterJson.Serialize(new TargetApplication { CoordinateMode = CoordinateMode.Absolute });
        Assert.Contains("Absolute", json);
        Assert.DoesNotContain("\"coordinateMode\": 1", json);
    }
}
