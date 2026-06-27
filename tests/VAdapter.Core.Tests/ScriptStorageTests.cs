using VAdapter.Core.Models;
using VAdapter.Core.Storage;

namespace VAdapter.Core.Tests;

public class ScriptStorageTests : IDisposable
{
    private readonly string _root;

    public ScriptStorageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "vadapter-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
    }

    private static MacroLibrary SampleLibrary()
    {
        var target = new TargetApplication
        {
            Id = "t1",
            Name = "VOICEPEAK",
            ProcessName = "voicepeak",
            ExecutablePath = @"C:\VOICEPEAK\voicepeak.exe",
        };
        var lib = new MacroLibrary { Targets = { target } };

        lib.Macros.Add(new Macro
        {
            Id = "m-builtin",
            Name = "音声の再生",
            IsBuiltIn = true,
            Scripts =
            {
                new MacroScript
                {
                    TargetApplicationId = "t1",
                    Instructions = { new WaitInstruction { DurationMs = 100 } },
                },
            },
        });
        lib.Macros.Add(new Macro
        {
            Id = "m-user",
            Name = "ユーザー作成",
            IsBuiltIn = false,
            Scripts = { new MacroScript { Instructions = { new WaitInstruction() } } },
        });
        return lib;
    }

    // --- TargetStore ---

    [Fact]
    public void TargetStore_RoundTrips()
    {
        var path = Path.Combine(_root, "library.json");
        var store = new TargetStore(path);
        store.Save(SampleLibrary().Targets);

        var loaded = store.Load();
        var t = Assert.Single(loaded);
        Assert.Equal("VOICEPEAK", t.Name);
        Assert.Equal(@"C:\VOICEPEAK\voicepeak.exe", t.ExecutablePath);
    }

    [Fact]
    public void TargetStore_Missing_ReturnsEmpty()
    {
        Assert.Empty(new TargetStore(Path.Combine(_root, "none.json")).Load());
    }

    // --- ScriptStore ---

    [Fact]
    public void ScriptStore_SavesToBuiltInAndUserDirs_ByFlag()
    {
        var store = new ScriptStore(Path.Combine(_root, "script"));
        store.SaveAll(SampleLibrary());

        Assert.True(File.Exists(Path.Combine(store.BuiltInDir, "音声の再生.vamacro")));
        Assert.True(File.Exists(Path.Combine(store.UserDir, "ユーザー作成.vamacro")));
    }

    [Fact]
    public void ScriptStore_LoadAll_RestoresMacros_AndEmbeddedTargets()
    {
        var scriptRoot = Path.Combine(_root, "script");
        new ScriptStore(scriptRoot).SaveAll(SampleLibrary());

        var result = new ScriptStore(scriptRoot).LoadAll();

        Assert.Equal(2, result.Macros.Count);
        var builtin = Assert.Single(result.Macros, m => m.Id == "m-builtin");
        Assert.True(builtin.IsBuiltIn);
        var user = Assert.Single(result.Macros, m => m.Id == "m-user");
        Assert.False(user.IsBuiltIn);

        // 参照される対象アプリが .vamacro に内包され、復元できる。
        Assert.Contains(result.EmbeddedTargets, t => t.Id == "t1" && t.ExecutablePath!.EndsWith("voicepeak.exe"));
    }

    [Fact]
    public void ScriptStore_SaveAll_DeletesOrphanedFiles()
    {
        var scriptRoot = Path.Combine(_root, "script");
        var store = new ScriptStore(scriptRoot);
        var lib = SampleLibrary();
        store.SaveAll(lib);
        Assert.True(File.Exists(Path.Combine(store.UserDir, "ユーザー作成.vamacro")));

        // ユーザーマクロを削除して再保存 → 対応する .vamacro が消える。
        lib.Macros.RemoveAll(m => m.Id == "m-user");
        store.SaveAll(lib);
        Assert.False(File.Exists(Path.Combine(store.UserDir, "ユーザー作成.vamacro")));
        Assert.True(File.Exists(Path.Combine(store.BuiltInDir, "音声の再生.vamacro")));
    }

    [Fact]
    public void ScriptStore_Rename_RemovesOldFile()
    {
        var scriptRoot = Path.Combine(_root, "script");
        var store = new ScriptStore(scriptRoot);
        var lib = SampleLibrary();
        store.SaveAll(lib);

        var userMacro = lib.Macros.Single(m => m.Id == "m-user");
        userMacro.Name = "改名後";
        store.Save(userMacro, lib.Targets);

        Assert.False(File.Exists(Path.Combine(store.UserDir, "ユーザー作成.vamacro")));
        Assert.True(File.Exists(Path.Combine(store.UserDir, "改名後.vamacro")));
    }

    [Fact]
    public void ScriptStore_NameCollision_DifferentIds_DoNotOverwrite()
    {
        var scriptRoot = Path.Combine(_root, "script");
        var store = new ScriptStore(scriptRoot);
        var lib = new MacroLibrary();
        lib.Macros.Add(new Macro { Id = "a", Name = "同名", IsBuiltIn = false });
        lib.Macros.Add(new Macro { Id = "b", Name = "同名", IsBuiltIn = false });
        store.SaveAll(lib);

        // 2 つの別マクロが別ファイルとして残る。
        var files = Directory.GetFiles(store.UserDir, "*.vamacro");
        Assert.Equal(2, files.Length);

        var reloaded = new ScriptStore(scriptRoot).LoadAll();
        Assert.Equal(2, reloaded.Macros.Count);
    }
}

public class ShippedBuiltInVasTests
{
    [Fact]
    public void ShippedBuiltInVas_Parse_AsMacroBundles()
    {
        var dir = LocateBuiltInDir();
        if (dir is null)
            return; // ソースツリー外（パッケージ実行）ではスキップ。

        var files = Directory.GetFiles(dir, "*.vamacro");
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var bundle = MacroBundleService.DeserializeBundle(File.ReadAllText(file));
            Assert.NotEmpty(bundle.Macros);
            foreach (var m in bundle.Macros)
                Assert.NotEmpty(m.Scripts);
        }
    }

    private static string? LocateBuiltInDir([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
    {
        // tests/VAdapter.Core.Tests/ → リポジトリルート → src/VAdapter.App/script/built-in
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));
        var dir = Path.Combine(repoRoot, "src", "VAdapter.App", "script", "built-in");
        return Directory.Exists(dir) ? dir : null;
    }
}
