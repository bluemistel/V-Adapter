using VAdapter.Core.Models;
using VAdapter.Core.Serialization;

namespace VAdapter.Core.Storage;

/// <summary>
/// 対象アプリ（targets）のみを <c>library.json</c> に永続化するストア。
/// マクロは <see cref="ScriptStore"/> 側で .vamacro ファイルとして個別管理する。
/// </summary>
public sealed class TargetStore
{
    private readonly string _filePath;

    public TargetStore(string filePath) => _filePath = filePath;

    public string FilePath => _filePath;

    /// <summary>ファイルがあれば対象アプリ一覧を読み込む。なければ空。</summary>
    public List<TargetApplication> Load()
    {
        if (!File.Exists(_filePath))
            return new();
        var doc = VAdapterJson.Deserialize<TargetDocument>(File.ReadAllText(_filePath));
        return doc?.Targets ?? new();
    }

    /// <summary>対象アプリ一覧を原子的に保存する。</summary>
    public void Save(IEnumerable<TargetApplication> targets)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = VAdapterJson.Serialize(new TargetDocument { Targets = targets.ToList() });
        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(_filePath))
            File.Replace(tmp, _filePath, null);
        else
            File.Move(tmp, _filePath);
    }

    /// <summary>library.json のルート（対象アプリのみ）。</summary>
    private sealed class TargetDocument
    {
        public int Version { get; set; } = 1;
        public List<TargetApplication> Targets { get; set; } = new();
    }
}
