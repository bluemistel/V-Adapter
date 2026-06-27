using System.IO;
using System.Reflection;
using VAdapter.App.Presets;
using VAdapter.Automation.Execution;
using VAdapter.Automation.Integration;
using VAdapter.Core.Models;
using VAdapter.Core.Serialization;
using VAdapter.Core.Storage;

namespace VAdapter.App.Services;

/// <summary>アプリ全体で共有する状態とサービス（簡易コンポジションルート）。</summary>
public sealed class AppState : IDisposable
{
    private readonly TargetStore _targetStore;
    private readonly ScriptStore _scriptStore;
    private readonly IntegrationSettingsStore _integrationStore;

    public MacroLibrary Library { get; }
    public MacroRunner Runner { get; } = new();
    public HotkeyService Hotkeys { get; } = new();

    /// <summary>連携設定（動画編集環境のモードと環境別設定）。</summary>
    public IntegrationSettings Integration { get; private set; }

    /// <summary>投げ込みパイプライン（監視 → 中立ペイロード → アダプタ）。</summary>
    public DropPipeline DropService { get; } = new();

    public AppState() : this(AppContext.BaseDirectory) { }

    public AppState(string rootDir)
    {
        // 対象アプリは library.json、マクロは script/{built-in,user-script}/*.vamacro として exe 同階層に保持。
        _targetStore = new TargetStore(Path.Combine(rootDir, "library.json"));
        _scriptStore = new ScriptStore(Path.Combine(rootDir, "script"));

        Library = LoadOrMigrate();

        // 連携設定も exe 同階層へ（完全ポータブル）。初回は旧 %APPDATA% から移行。
        var integrationPath = Path.Combine(rootDir, "integration.json");
        _integrationStore = new IntegrationSettingsStore(integrationPath);
        if (!File.Exists(integrationPath))
        {
            var legacy = IntegrationSettingsStore.DefaultPath();
            if (File.Exists(legacy))
            {
                try { _integrationStore.Save(new IntegrationSettingsStore(legacy).Load()); }
                catch { /* 移行失敗時は既定設定で続行 */ }
            }
        }
        Integration = _integrationStore.Load();
    }

    /// <summary>
    /// 新レイアウト（library.json + .vamacro）を読み込む。初回（library.json 不在）は、
    /// 旧 %APPDATA%/V-Adapter/library.json からの移行、無ければ埋め込みシードで初期化する。
    /// </summary>
    private MacroLibrary LoadOrMigrate()
    {
        var initialized = File.Exists(_targetStore.FilePath);

        var targets = _targetStore.Load();
        var scripts = _scriptStore.LoadAll();
        var addedFromScripts = MergeMissing(targets, scripts.EmbeddedTargets);
        var macros = scripts.Macros;

        if (!initialized)
        {
            // 旧バージョンからの初回移行（対象アプリのカスタマイズとユーザーマクロを引き継ぐ）。
            TryMigrateLegacy(targets, macros);

            // それでも空ならフォールバックの埋め込みシード。
            if (targets.Count == 0 && macros.Count == 0)
            {
                var seed = LoadEmbeddedDefault() ?? SeedFallback();
                targets = seed.Targets;
                macros = seed.Macros;
            }

            var seeded = new MacroLibrary { Targets = targets, Macros = macros };
            // 新レイアウトへ確定（library.json と各 .vamacro を作成）。
            _targetStore.Save(seeded.Targets);
            _scriptStore.SaveAll(seeded);
            return seeded;
        }

        // 更新で新たな組込ターゲットが増えていれば library.json に反映。
        if (addedFromScripts > 0)
            _targetStore.Save(targets);

        return new MacroLibrary { Targets = targets, Macros = macros };
    }

    /// <summary>旧 %APPDATA%/V-Adapter/library.json があれば、対象アプリとユーザーマクロを取り込む。</summary>
    private static void TryMigrateLegacy(List<TargetApplication> targets, List<Macro> macros)
    {
        var legacyPath = LibraryStore.DefaultPath();
        if (!File.Exists(legacyPath))
            return;

        MacroLibrary legacy;
        try { legacy = new LibraryStore(legacyPath).Load(); }
        catch { return; }

        // 対象アプリ: ユーザーのカスタマイズ（exe パス等）を優先して上書き／追加。
        foreach (var t in legacy.Targets)
        {
            var idx = targets.FindIndex(x => x.Id == t.Id);
            if (idx >= 0) targets[idx] = t;
            else targets.Add(t);
        }

        // マクロ: ユーザー作成分のみ取り込む（組込は同梱 .vamacro を使用）。
        foreach (var m in legacy.Macros.Where(m => !m.IsBuiltIn))
            if (macros.All(x => x.Id != m.Id))
                macros.Add(m);
    }

    /// <summary>pool に存在しない id の対象アプリを追加する。追加件数を返す。</summary>
    private static int MergeMissing(List<TargetApplication> pool, List<TargetApplication> incoming)
    {
        var added = 0;
        foreach (var t in incoming)
        {
            if (pool.All(x => x.Id != t.Id))
            {
                pool.Add(t);
                added++;
            }
        }
        return added;
    }

    /// <summary>連携設定を差し替えて保存し、監視サービスへ反映する。</summary>
    public void UpdateIntegration(IntegrationSettings settings)
    {
        Integration = settings;
        _integrationStore.Save(settings);
        DropService.Apply(settings);
    }

    /// <summary>現在の連携設定を監視サービスへ適用する（起動時など）。</summary>
    public void ApplyIntegration() => DropService.Apply(Integration);

    public void Dispose()
    {
        DropService.Dispose();
        Hotkeys.Dispose();
    }

    /// <summary>埋め込みの既定ライブラリ（default-library.json）を読み込む。失敗時は null。</summary>
    private static MacroLibrary? LoadEmbeddedDefault()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var name = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("default-library.json", StringComparison.OrdinalIgnoreCase));
            if (name is null)
                return null;

            using var stream = asm.GetManifestResourceStream(name);
            if (stream is null)
                return null;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var library = VAdapterJson.Deserialize<MacroLibrary>(json);
            return library is { Macros.Count: > 0 } ? library : null;
        }
        catch
        {
            return null;
        }
    }

    private static MacroLibrary SeedFallback()
    {
        var library = new MacroLibrary();
        StandardMacros.SeedInto(library);
        return library;
    }

    /// <summary>ライブラリを永続化する（library.json と全 .vamacro）。</summary>
    public void Save()
    {
        _targetStore.Save(Library.Targets);
        _scriptStore.SaveAll(Library);
    }

    /// <summary>ライブラリ保存後にホットキーを登録し直す。</summary>
    public void SaveAndRebindHotkeys()
    {
        Save();
        Hotkeys.RegisterAll(Library.Macros);
    }
}
