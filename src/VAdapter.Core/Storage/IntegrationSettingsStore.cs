using VAdapter.Core.Models;
using VAdapter.Core.Serialization;

namespace VAdapter.Core.Storage;

/// <summary>
/// <see cref="IntegrationSettings"/> を JSON ファイルに永続化するストア。
/// 既定の保存先は <c>%APPDATA%/V-Adapter/integration.json</c>。
/// </summary>
public sealed class IntegrationSettingsStore
{
    private readonly string _filePath;

    public IntegrationSettingsStore(string? filePath = null)
    {
        _filePath = filePath ?? DefaultPath();
    }

    public string FilePath => _filePath;

    public static string DefaultPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "V-Adapter");
        return Path.Combine(dir, "integration.json");
    }

    /// <summary>ファイルが存在すれば読み込み、なければ既定設定（MacroOnly）を返す。</summary>
    public IntegrationSettings Load()
    {
        if (!File.Exists(_filePath))
            return new IntegrationSettings();

        var json = File.ReadAllText(_filePath);
        return VAdapterJson.Deserialize<IntegrationSettings>(json) ?? new IntegrationSettings();
    }

    /// <summary>原子的に保存する（一時ファイル → 置換）。</summary>
    public void Save(IntegrationSettings settings)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = VAdapterJson.Serialize(settings);
        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(_filePath))
            File.Replace(tmp, _filePath, null);
        else
            File.Move(tmp, _filePath);
    }
}
