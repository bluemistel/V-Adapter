using VAdapter.Core.Models;

namespace VAdapter.Core.Storage;

/// <summary>マクロ .vamacro ファイル群の読み込み結果。</summary>
public sealed record ScriptLoadResult(List<Macro> Macros, List<TargetApplication> EmbeddedTargets);

/// <summary>
/// マクロを 1 件ずつ <c>.vamacro</c> ファイルとして管理するストア。
/// レイアウトは <c>{root}/built-in/*.vamacro</c>（同梱・更新で上書き）と
/// <c>{root}/user-script/*.vamacro</c>（ユーザー作成・インポート、更新で温存）。
/// ファイルの中身は <see cref="MacroBundle"/>（マクロ1件＋参照する対象アプリを内包）で、
/// インポート/エクスポートと同一フォーマット。
/// </summary>
public sealed class ScriptStore
{
    public const string FileExtension = MacroBundleService.FileExtension;

    private readonly string _builtInDir;
    private readonly string _userDir;

    // macroId → 保存先ファイルパス（リネーム・削除の追跡用）。
    private readonly Dictionary<string, string> _pathById = new();

    public ScriptStore(string scriptRootDir)
    {
        _builtInDir = Path.Combine(scriptRootDir, "built-in");
        _userDir = Path.Combine(scriptRootDir, "user-script");
    }

    public string BuiltInDir => _builtInDir;
    public string UserDir => _userDir;

    /// <summary>built-in / user-script 配下の全 .vamacro を読み込む。</summary>
    public ScriptLoadResult LoadAll()
    {
        Directory.CreateDirectory(_builtInDir);
        Directory.CreateDirectory(_userDir);
        _pathById.Clear();

        var macros = new List<Macro>();
        var targets = new List<TargetApplication>();
        LoadDir(_builtInDir, isBuiltIn: true, macros, targets);
        LoadDir(_userDir, isBuiltIn: false, macros, targets);
        return new ScriptLoadResult(macros, targets);
    }

    private void LoadDir(string dir, bool isBuiltIn, List<Macro> macros, List<TargetApplication> targets)
    {
        foreach (var file in Directory.EnumerateFiles(dir, "*" + FileExtension).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            MacroBundle bundle;
            try
            {
                bundle = MacroBundleService.DeserializeBundle(File.ReadAllText(file));
            }
            catch
            {
                continue; // 壊れた/不正なファイルはスキップ。
            }

            foreach (var t in bundle.Targets)
                if (targets.All(x => x.Id != t.Id))
                    targets.Add(t);

            foreach (var m in bundle.Macros)
            {
                m.IsBuiltIn = isBuiltIn;
                _pathById.TryAdd(m.Id, file);
                macros.Add(m);
            }
        }
    }

    /// <summary>ライブラリ内の全マクロを .vamacro へ保存し、消えたマクロのファイルを削除する。</summary>
    public void SaveAll(MacroLibrary library)
    {
        Directory.CreateDirectory(_builtInDir);
        Directory.CreateDirectory(_userDir);

        var present = new HashSet<string>();
        foreach (var macro in library.Macros)
        {
            Save(macro, library.Targets);
            present.Add(macro.Id);
        }

        foreach (var id in _pathById.Keys.Where(k => !present.Contains(k)).ToList())
        {
            TryDelete(_pathById[id]);
            _pathById.Remove(id);
        }
    }

    /// <summary>1 マクロを .vamacro へ保存する（built-in / user-script は IsBuiltIn で振り分け）。</summary>
    public void Save(Macro macro, IReadOnlyList<TargetApplication> targets)
    {
        var dir = macro.IsBuiltIn ? _builtInDir : _userDir;
        Directory.CreateDirectory(dir);

        var bundle = MacroBundleService.CreateBundle(new[] { macro }, new MacroLibrary { Targets = targets.ToList() });
        var json = MacroBundleService.SerializeBundle(bundle);
        var path = ResolvePath(dir, macro);

        // リネーム・配置変更で旧ファイルが残る場合は削除。
        if (_pathById.TryGetValue(macro.Id, out var old) && !PathEquals(old, path))
            TryDelete(old);

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(path))
            File.Replace(tmp, path, null);
        else
            File.Move(tmp, path);

        _pathById[macro.Id] = path;
    }

    public void Delete(string macroId)
    {
        if (_pathById.TryGetValue(macroId, out var p))
        {
            TryDelete(p);
            _pathById.Remove(macroId);
        }
    }

    /// <summary>表示名ベースのファイルパスを決める。名前が衝突する場合のみ id を付与。</summary>
    private string ResolvePath(string dir, Macro macro)
    {
        var baseName = Sanitize(macro.Name);
        var candidate = Path.Combine(dir, baseName + FileExtension);

        // 既にこの id が同じファイルを使っているならそのまま。
        if (_pathById.TryGetValue(macro.Id, out var existing) && PathEquals(existing, candidate))
            return candidate;

        // 別マクロが同名ファイルを使用中／別マクロのファイルが既存 → id を付与して衝突回避。
        var usedByOther = _pathById.Any(kv => kv.Key != macro.Id && PathEquals(kv.Value, candidate));
        if (usedByOther || File.Exists(candidate))
            candidate = Path.Combine(dir, $"{baseName}_{macro.Id[..Math.Min(8, macro.Id.Length)]}{FileExtension}");

        return candidate;
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string((name ?? string.Empty)
            .Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return string.IsNullOrEmpty(cleaned) ? "macro" : cleaned;
    }

    private static bool PathEquals(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* 後始末失敗は無視 */ }
    }
}
