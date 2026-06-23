using System.IO;
using System.Reflection;
using VAdapter.App.Presets;
using VAdapter.Automation.Execution;
using VAdapter.Core.Models;
using VAdapter.Core.Serialization;
using VAdapter.Core.Storage;

namespace VAdapter.App.Services;

/// <summary>アプリ全体で共有する状態とサービス（簡易コンポジションルート）。</summary>
public sealed class AppState
{
    private readonly LibraryStore _store;

    public MacroLibrary Library { get; }
    public MacroRunner Runner { get; } = new();
    public HotkeyService Hotkeys { get; } = new();

    public AppState() : this(new LibraryStore()) { }

    public AppState(LibraryStore store)
    {
        _store = store;
        var library = _store.Load();

        // 初回起動（空）のときは既定ライブラリ（標準マクロ＋対象アプリテンプレート）をシード。
        if (library.Macros.Count == 0 && library.Targets.Count == 0)
        {
            library = LoadEmbeddedDefault() ?? SeedFallback();
            Library = library;
            Save();
        }
        else
        {
            Library = library;
        }
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

    /// <summary>ライブラリを永続化する。</summary>
    public void Save() => _store.Save(Library);

    /// <summary>ライブラリ保存後にホットキーを登録し直す。</summary>
    public void SaveAndRebindHotkeys()
    {
        Save();
        Hotkeys.RegisterAll(Library.Macros);
    }
}
