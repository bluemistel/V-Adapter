using VAdapter.Core.Models;
using VAdapter.Core.Storage;

namespace VAdapter.Core.Tests;

public class BundleAndStoreTests
{
    private static MacroLibrary BuildLibrary()
    {
        var target = new TargetApplication { Id = "t1", Name = "VOICEVOX" };
        var macro = new Macro
        {
            Id = "m1",
            Name = "再生",
            Scripts =
            {
                new MacroScript
                {
                    Id = "s1",
                    TargetApplicationId = "t1",
                    Instructions = { new WaitInstruction { DurationMs = 100 } },
                },
            },
        };
        return new MacroLibrary { Targets = { target }, Macros = { macro } };
    }

    [Fact]
    public void Bundle_Includes_ReferencedTargetsOnly()
    {
        var lib = BuildLibrary();
        lib.Targets.Add(new TargetApplication { Id = "t2", Name = "未使用" });

        var bundle = MacroBundleService.CreateBundle(lib.Macros, lib);

        Assert.Single(bundle.Macros);
        var t = Assert.Single(bundle.Targets);
        Assert.Equal("VOICEVOX", t.Name);
    }

    [Fact]
    public void Import_Remaps_Ids_AndRewiresScriptTargetReference()
    {
        var source = BuildLibrary();
        var bundle = MacroBundleService.CreateBundle(source.Macros, source);
        var json = MacroBundleService.SerializeBundle(bundle);

        // 同一 ID を持つ既存ライブラリへ取り込む（衝突を起こさせる）。
        var dest = BuildLibrary();
        var roundTripped = MacroBundleService.DeserializeBundle(json);
        var imported = MacroBundleService.ImportInto(roundTripped, dest);

        var importedMacro = Assert.Single(imported);
        Assert.NotEqual("m1", importedMacro.Id);
        Assert.Equal(2, dest.Macros.Count);
        Assert.Equal(2, dest.Targets.Count);

        var importedScript = Assert.Single(importedMacro.Scripts);
        Assert.NotEqual("s1", importedScript.Id);

        // スクリプトの対象アプリ参照が、取り込んだ対象アプリの新 ID を指している。
        var importedTarget = dest.Targets.Single(t => t.Id == importedScript.TargetApplicationId);
        Assert.Equal("VOICEVOX", importedTarget.Name);
        Assert.False(importedMacro.IsBuiltIn);
    }

    [Fact]
    public void Import_NullTargetScript_StaysNull()
    {
        var bundle = new MacroBundle
        {
            Macros =
            {
                new Macro
                {
                    Name = "共通のみ",
                    Scripts = { new MacroScript { TargetApplicationId = null } },
                },
            },
        };
        var dest = new MacroLibrary();
        var imported = MacroBundleService.ImportInto(bundle, dest);
        Assert.Null(imported[0].Scripts[0].TargetApplicationId);
    }

    [Fact]
    public void LibraryStore_Save_Then_Load_RoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), "vadapter-test", Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var store = new LibraryStore(path);
            store.Save(BuildLibrary());

            var loaded = store.Load();
            Assert.Single(loaded.Macros);
            Assert.Equal("再生", loaded.Macros[0].Name);
            Assert.Equal("t1", loaded.Macros[0].Scripts[0].TargetApplicationId);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void LibraryStore_Migrates_LegacyMacroFormat()
    {
        // 旧スキーマ: マクロが単一の targetApplicationId + instructions を持つ。
        const string legacy = """
        {
          "version": 1,
          "targets": [ { "id": "t1", "name": "VOICEVOX" } ],
          "macros": [
            {
              "id": "m1",
              "name": "音声の再生",
              "isBuiltIn": true,
              "isEnabled": true,
              "targetApplicationId": "t1",
              "instructions": [
                { "kind": "wait", "id": "i1", "durationMs": 300 },
                { "kind": "sendkeys", "id": "i2", "keyCombination": { "modifiers": "Control", "virtualKey": 32 } }
              ]
            }
          ]
        }
        """;

        var path = Path.Combine(Path.GetTempPath(), "vadapter-legacy-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, legacy);
        try
        {
            var lib = new LibraryStore(path).Load();
            var macro = Assert.Single(lib.Macros);
            var script = Assert.Single(macro.Scripts);
            Assert.Equal("t1", script.TargetApplicationId);
            Assert.Equal(2, script.Instructions.Count);
            Assert.IsType<WaitInstruction>(script.Instructions[0]);
            Assert.IsType<SendKeysInstruction>(script.Instructions[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LibraryStore_Load_Missing_ReturnsEmpty()
    {
        var store = new LibraryStore(Path.Combine(Path.GetTempPath(), "vadapter-missing-" + Guid.NewGuid().ToString("N") + ".json"));
        var lib = store.Load();
        Assert.Empty(lib.Macros);
        Assert.Empty(lib.Targets);
    }
}
