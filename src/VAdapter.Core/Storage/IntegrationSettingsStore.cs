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

    /// <summary>
    /// ファイルが存在すれば読み込み、なければ既定設定（MacroOnly）を返す。
    /// 旧形式（連携環境ごとに分かれた監視フォルダ）は読み込み時に統合リストへ移行し、保存し直す。
    /// </summary>
    public IntegrationSettings Load()
    {
        if (!File.Exists(_filePath))
            return new IntegrationSettings();

        var json = File.ReadAllText(_filePath);
        var settings = VAdapterJson.Deserialize<IntegrationSettings>(json) ?? new IntegrationSettings();

        if (settings.MigrateWatchFolders())
        {
            // 移行結果を書き戻す。失敗しても読み込み自体は成功させる（次回また移行を試みる）。
            try { Save(settings); }
            catch { /* 読み取り専用の配置などでは移行結果をメモリ上だけで使う */ }
        }

        return settings;
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
