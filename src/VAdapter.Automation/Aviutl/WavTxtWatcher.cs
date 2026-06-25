using System.IO;

namespace VAdapter.Automation.Aviutl;

/// <summary>
/// 監視フォルダ内に、同名の <c>*.wav</c> + <c>*.txt</c> ペアが出現したことを検知する。
/// 書き込み完了まで安定化（排他オープン可能になるまでリトライ）し、二重検知を防止する。
/// </summary>
public sealed class WavTxtWatcher : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly object _gate = new();

    // path → 初検知時刻
    private readonly Dictionary<string, DateTime> _pending = new(StringComparer.OrdinalIgnoreCase);
    // path → 処理時刻（二重防止・一定時間で掃除）
    private readonly Dictionary<string, DateTime> _processed = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _lastSize = new(StringComparer.OrdinalIgnoreCase);

    private System.Threading.Timer? _timer;
    private int _stableWaitMs = 1500;

    /// <summary>同名 wav+txt が確定したとき発火（wavパス, txtパス）。</summary>
    public event Action<string, string>? PairReady;

    /// <summary>診断ログ。</summary>
    public event Action<string>? Log;

    public bool IsRunning => _timer is not null;

    public void Start(IEnumerable<(string Path, bool IncludeSub)> folders, int stableWaitMs)
    {
        Stop();
        _stableWaitMs = Math.Max(300, stableWaitMs);

        foreach (var (path, includeSub) in folders)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                Log?.Invoke($"監視フォルダが見つかりません: {path}");
                continue;
            }

            var fsw = new FileSystemWatcher(path, "*.wav")
            {
                IncludeSubdirectories = includeSub,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            fsw.Created += OnWavEvent;
            fsw.Changed += OnWavEvent;
            fsw.Renamed += OnWavRenamed;
            fsw.EnableRaisingEvents = true;
            _watchers.Add(fsw);
            Log?.Invoke($"監視開始: {path}{(includeSub ? "（サブフォルダ含む）" : "")}");
        }

        if (_watchers.Count > 0)
            _timer = new System.Threading.Timer(Tick, null, 250, 250);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        foreach (var w in _watchers)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }
        _watchers.Clear();
        lock (_gate)
        {
            _pending.Clear();
            _lastSize.Clear();
        }
    }

    private void OnWavRenamed(object sender, RenamedEventArgs e) => Enqueue(e.FullPath);
    private void OnWavEvent(object sender, FileSystemEventArgs e) => Enqueue(e.FullPath);

    private void Enqueue(string path)
    {
        if (!path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            return;
        lock (_gate)
        {
            if (_processed.ContainsKey(path) || _pending.ContainsKey(path))
                return;
            _pending[path] = DateTime.UtcNow;
        }
    }

    private void Tick(object? state)
    {
        List<KeyValuePair<string, DateTime>> snapshot;
        lock (_gate)
        {
            snapshot = _pending.ToList();
            // 古い処理済み記録を掃除（10分）
            var cutoff = DateTime.UtcNow.AddMinutes(-10);
            foreach (var key in _processed.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList())
                _processed.Remove(key);
        }

        foreach (var (wav, firstSeen) in snapshot)
        {
            var elapsed = DateTime.UtcNow - firstSeen;

            if (!File.Exists(wav))
            {
                Forget(wav);
                continue;
            }

            var txt = Path.ChangeExtension(wav, ".txt");
            if (!File.Exists(txt))
            {
                // txt がまだなら待つ。一定時間出なければ諦める。
                if (elapsed.TotalMilliseconds > _stableWaitMs * 4)
                {
                    Log?.Invoke($"同名の .txt が見つからず無視: {Path.GetFileName(wav)}");
                    Forget(wav);
                }
                continue;
            }

            if (!IsStable(wav) || !IsReadable(txt))
            {
                // まだ書き込み中。安定化の上限を超えたら諦める。
                if (elapsed.TotalMilliseconds > _stableWaitMs * 8)
                {
                    Log?.Invoke($"書き込みが安定せず無視: {Path.GetFileName(wav)}");
                    Forget(wav);
                }
                continue;
            }

            // 確定
            lock (_gate)
            {
                _pending.Remove(wav);
                _lastSize.Remove(wav);
                _processed[wav] = DateTime.UtcNow;
            }
            try { PairReady?.Invoke(wav, txt); }
            catch (Exception ex) { Log?.Invoke($"投入処理で例外: {ex.Message}"); }
        }
    }

    private void Forget(string wav)
    {
        lock (_gate)
        {
            _pending.Remove(wav);
            _lastSize.Remove(wav);
        }
    }

    /// <summary>排他オープン可能、かつサイズが前回チェックから変化していないか。</summary>
    private bool IsStable(string path)
    {
        if (!IsReadable(path))
            return false;
        try
        {
            var size = new FileInfo(path).Length;
            lock (_gate)
            {
                var stable = _lastSize.TryGetValue(path, out var prev) && prev == size;
                _lastSize[path] = size;
                return stable;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool IsReadable(string path)
    {
        try
        {
            using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch
        {
            return false; // 書き込み中などでロックされている
        }
    }

    public void Dispose() => Stop();
}
