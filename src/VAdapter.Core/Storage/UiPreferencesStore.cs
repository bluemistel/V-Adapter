using VAdapter.Core.Models;
using VAdapter.Core.Serialization;

namespace VAdapter.Core.Storage;

/// <summary><see cref="UiPreferences"/> を JSON ファイルに永続化するストア。</summary>
public sealed class UiPreferencesStore
{
    private readonly string _filePath;

    public UiPreferencesStore(string filePath)
    {
        _filePath = filePath;
    }

    public string FilePath => _filePath;

    /// <summary>
    /// 読み込む。ファイルが無い・壊れている場合は既定値を返す
    /// （操作状態が戻るだけなので、起動を止めない）。
    /// </summary>
    public UiPreferences Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new UiPreferences();
            return VAdapterJson.Deserialize<UiPreferences>(File.ReadAllText(_filePath)) ?? new UiPreferences();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return new UiPreferences();
        }
    }

    /// <summary>原子的に保存する（一時ファイル → 置換）。</summary>
    public void Save(UiPreferences preferences)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = VAdapterJson.Serialize(preferences);
        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(_filePath))
            File.Replace(tmp, _filePath, null);
        else
            File.Move(tmp, _filePath);
    }
}
