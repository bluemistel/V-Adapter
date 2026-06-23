using System.Text.Json;
using VAdapter.Core.Models;
using VAdapter.Core.Serialization;

namespace VAdapter.Core.Storage;

/// <summary>
/// <see cref="MacroLibrary"/> を 1 つの JSON ファイルに永続化するストア。
/// 既定の保存先は <c>%APPDATA%/V-Adapter/library.json</c>。
/// </summary>
public sealed class LibraryStore
{
    private readonly string _filePath;

    public LibraryStore(string? filePath = null)
    {
        _filePath = filePath ?? DefaultPath();
    }

    public string FilePath => _filePath;

    /// <summary>既定の保存先（%APPDATA%/V-Adapter/library.json）。</summary>
    public static string DefaultPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "V-Adapter");
        return Path.Combine(dir, "library.json");
    }

    /// <summary>ファイルが存在すれば読み込み、なければ空のライブラリを返す。</summary>
    public MacroLibrary Load()
    {
        if (!File.Exists(_filePath))
            return new MacroLibrary();

        var json = File.ReadAllText(_filePath);
        var library = VAdapterJson.Deserialize<MacroLibrary>(json) ?? new MacroLibrary();
        MigrateLegacyMacros(json, library);
        return library;
    }

    /// <summary>
    /// 旧スキーマ（マクロが単一の instructions / targetApplicationId を持つ形式）から、
    /// スクリプト集合形式へ移行する。新フィールド未設定のマクロのみ対象。
    /// </summary>
    private static void MigrateLegacyMacros(string json, MacroLibrary library)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("macros", out var macrosEl)
                || macrosEl.ValueKind != JsonValueKind.Array)
                return;

            int index = 0;
            foreach (var macroEl in macrosEl.EnumerateArray())
            {
                if (index >= library.Macros.Count)
                    break;
                var macro = library.Macros[index++];

                // 既に新形式（scripts あり）なら何もしない。
                if (macro.Scripts.Count > 0)
                    continue;

                var hasLegacyInstructions = macroEl.TryGetProperty("instructions", out var instrEl)
                    && instrEl.ValueKind == JsonValueKind.Array
                    && instrEl.GetArrayLength() > 0;
                var hasLegacyTarget = macroEl.TryGetProperty("targetApplicationId", out var targetEl)
                    && targetEl.ValueKind == JsonValueKind.String;

                if (!hasLegacyInstructions && !hasLegacyTarget)
                    continue;

                var script = new MacroScript
                {
                    TargetApplicationId = hasLegacyTarget ? targetEl.GetString() : null,
                };
                if (hasLegacyInstructions)
                {
                    script.Instructions =
                        VAdapterJson.Deserialize<List<Instruction>>(instrEl.GetRawText()) ?? new();
                }
                macro.Scripts.Add(script);
            }
        }
        catch (JsonException)
        {
            // 移行失敗時は無視（新規扱い）。
        }
    }

    /// <summary>原子的に保存する（一時ファイル → 置換）。</summary>
    public void Save(MacroLibrary library)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = VAdapterJson.Serialize(library);
        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, json);
        // 置換（既存があれば上書き）。
        if (File.Exists(_filePath))
            File.Replace(tmp, _filePath, null);
        else
            File.Move(tmp, _filePath);
    }
}
