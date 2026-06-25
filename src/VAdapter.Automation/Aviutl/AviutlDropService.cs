using System.IO;
using VAdapter.Core.Media;
using VAdapter.Core.Models;

namespace VAdapter.Automation.Aviutl;

/// <summary>
/// 連携設定に従い、wav+txt 監視 → 話者ルールでレイヤー解決 → gcmz 外部連携APIへ投げ込み を統括する。
/// モードが MacroOnly のときは監視を行わない（現行のマクロ挙動のみ）。
/// </summary>
public sealed class AviutlDropService : IDisposable
{
    private readonly GcmzApi _gcmz = new();
    private readonly WavTxtWatcher _watcher = new();
    private readonly object _gate = new();

    private IntegrationMode _mode = IntegrationMode.MacroOnly;
    private AviutlDropConfig? _config;

    /// <summary>ログ通知（UI 側で Dispatcher 経由表示）。</summary>
    public event Action<string>? Log;

    public AviutlDropService()
    {
        _watcher.PairReady += OnPairReady;
        _watcher.Log += m => Log?.Invoke(m);
    }

    public IntegrationMode CurrentMode
    {
        get { lock (_gate) return _mode; }
    }

    /// <summary>設定を適用する。モードに応じて監視を起動/停止する。</summary>
    public void Apply(IntegrationSettings settings)
    {
        lock (_gate)
        {
            _mode = settings.ActiveMode;
            _config = settings.ConfigFor(_mode);
        }

        if (_mode == IntegrationMode.MacroOnly || _config is null)
        {
            _watcher.Stop();
            Log?.Invoke("連携モード: マクロ動作ベース（投げ込み監視は無効）");
            return;
        }

        if (_config.Folders.Count == 0)
        {
            _watcher.Stop();
            Log?.Invoke($"連携モード: {ModeLabel(_mode)}（監視フォルダ未設定）");
            return;
        }

        _watcher.Start(_config.Folders.Select(f => (f.Path, f.IncludeSubdirectories)), _config.StableWaitMs);
        Log?.Invoke($"連携モード: {ModeLabel(_mode)}（監視中）");
    }

    public bool IsGcmzAvailable() => _gcmz.IsAvailable();

    public GcmzInfo? ReadGcmzInfo() => _gcmz.ReadInfo();

    /// <summary>テスト投げ込み（UI の「今すぐ投げる」）。現在のモード設定で1ファイルを投入。</summary>
    public GcmzDropResult DropNow(IReadOnlyList<string> files, int layer)
    {
        AviutlDropConfig? config;
        IntegrationMode mode;
        lock (_gate) { config = _config; mode = _mode; }

        if (mode == IntegrationMode.MacroOnly)
            return GcmzDropResult.Fail("マクロ動作ベースのため投げ込みは無効です。");

        var wav = files.FirstOrDefault(f => f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase));
        var frameAdvance = config is null ? null : ResolveFrameAdvance(wav, config);
        return _gcmz.Drop(files, layer, frameAdvance, MarginFor(mode, config), ProtocolVersion(mode));
    }

    private void OnPairReady(string wavPath, string txtPath)
    {
        AviutlDropConfig? config;
        IntegrationMode mode;
        lock (_gate) { config = _config; mode = _mode; }
        if (config is null || mode == IntegrationMode.MacroOnly)
            return;

        var name = Path.GetFileName(wavPath);
        var route = DropRouting.Resolve(name, config);
        var speaker = route.Speaker is null ? "" : $"（{route.Speaker}）";
        Log?.Invoke($"検知: {name}{speaker} → レイヤー {route.Layer}");

        if (!_gcmz.IsAvailable())
        {
            Log?.Invoke("AviUtl / ごちゃまぜドロップス未検出のため投入をスキップしました。");
            return;
        }

        // wav と同名 txt を一緒に投入する。
        // PSDToolKit の発動条件「同じ名前の *.wav と *.txt を一緒にドロップした時」に合わせ、
        // 口パク準備・字幕準備等が生成されるようにする。
        var files = File.Exists(txtPath) ? new[] { wavPath, txtPath } : new[] { wavPath };
        var frameAdvance = ResolveFrameAdvance(wavPath, config);
        var result = _gcmz.Drop(files, route.Layer, frameAdvance, MarginFor(mode, config), ProtocolVersion(mode));
        Log?.Invoke(result.Success ? $"投入成功: {name}（シーク +{frameAdvance ?? 0}f）" : $"投入失敗: {result.Error}");
    }

    /// <summary>
    /// 投入後のシーク移動量（フレーム）を決定する。アイテム終端移動が ON なら音声長×fps、
    /// そうでなければ手動の FrameAdvance（0 は null=移動なし）。
    /// </summary>
    private int? ResolveFrameAdvance(string? wavPath, AviutlDropConfig config)
    {
        if (config.AdvanceToItemEnd && wavPath is not null)
        {
            var fps = _gcmz.ReadInfo()?.Fps ?? 0;
            if (WavInfo.TryGetDurationFrames(wavPath, fps) is { } frames)
                return frames;
        }
        return config.FrameAdvance > 0 ? config.FrameAdvance : null;
    }

    private static int ProtocolVersion(IntegrationMode mode) =>
        mode == IntegrationMode.AviUtl2 ? 2 : 1;

    private static int? MarginFor(IntegrationMode mode, AviutlDropConfig? config) =>
        mode == IntegrationMode.AviUtl2 ? config?.Margin : null;

    private static string ModeLabel(IntegrationMode mode) => mode switch
    {
        IntegrationMode.AviUtl => "AviUtl + PSDToolKit",
        IntegrationMode.AviUtl2 => "AviUtl2 + PSDToolKit2",
        _ => "マクロ動作ベース",
    };

    public void Dispose()
    {
        _watcher.Dispose();
    }
}
