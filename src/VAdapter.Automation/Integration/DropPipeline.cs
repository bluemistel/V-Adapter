using System.IO;
using System.Reflection;
using VAdapter.Automation.Aviutl;
using VAdapter.Core.Integration;
using VAdapter.Core.Models;

namespace VAdapter.Automation.Integration;

/// <summary>
/// 投げ込みの統括（旧 AviutlDropService の一般化）。
/// wav+txt 監視 → 話者ルーティング → 中立 <see cref="DropPayload"/> 生成 →
/// 選択中の <see cref="IImportAdapter"/> へ受け渡し、を担う。
/// アダプタは <see cref="IntegrationMode"/> で決定（MacroOnly=なし / AviUtl=gcmz v1 /
/// AviUtl2=gcmz v2 / External=外部コマンド）。
/// </summary>
public sealed class DropPipeline : IDisposable
{
    private readonly GcmzApi _gcmz = new();
    private readonly WavTxtWatcher _watcher = new();
    private readonly object _gate = new();

    private IntegrationMode _mode = IntegrationMode.MacroOnly;
    private AviutlDropConfig? _config;
    private IImportAdapter? _adapter;

    private static readonly string AppVersion =
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? string.Empty;

    /// <summary>ログ通知（UI 側で Dispatcher 経由表示）。</summary>
    public event Action<string>? Log;

    public DropPipeline()
    {
        _watcher.PairReady += OnPairReady;
        _watcher.Log += m => Log?.Invoke(m);
    }

    public IntegrationMode CurrentMode
    {
        get { lock (_gate) return _mode; }
    }

    /// <summary>設定を適用する。モードに応じてアダプタを選択し、監視を起動/停止する。</summary>
    public void Apply(IntegrationSettings settings)
    {
        lock (_gate)
        {
            _mode = settings.ActiveMode;
            _config = settings.ConfigFor(_mode);
            _adapter = CreateAdapter(_mode, settings);
        }

        if (_adapter is null || _config is null)
        {
            _watcher.Stop();
            Log?.Invoke("連携モード: マクロ動作ベース（投げ込み監視は無効）");
            return;
        }

        if (_config.Folders.Count == 0)
        {
            _watcher.Stop();
            Log?.Invoke($"連携モード: {_adapter.DisplayName}（監視フォルダ未設定）");
            return;
        }

        _watcher.Start(_config.Folders.Select(f => (f.Path, f.IncludeSubdirectories)), _config.StableWaitMs);
        Log?.Invoke($"連携モード: {_adapter.DisplayName}（監視中）");
    }

    /// <summary>現在適用中のアダプタの状態（MacroOnly のときは null）。</summary>
    public AdapterStatus? GetStatus()
    {
        lock (_gate)
            return _adapter?.GetStatus();
    }

    /// <summary>
    /// 指定設定（編集中の作業コピー等）の選択モードに対する状態を、監視に影響を与えず取得する。
    /// </summary>
    public AdapterStatus? GetStatus(IntegrationSettings settings) =>
        CreateAdapter(settings.ActiveMode, settings)?.GetStatus();

    /// <summary>テスト投げ込み（UI の「今すぐ投げる」）。現在のモードのアダプタで投入。</summary>
    public ImportResult DropNow(IReadOnlyList<string> files, int layer)
    {
        IImportAdapter? adapter;
        AviutlDropConfig? config;
        lock (_gate) { adapter = _adapter; config = _config; }

        if (adapter is null || config is null)
            return ImportResult.Fail("マクロ動作ベースのため投げ込みは無効です。");

        var audio = files.FirstOrDefault(f => f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    ?? files.FirstOrDefault();
        if (audio is null)
            return ImportResult.Fail("投げ込むファイルがありません。");

        var subtitle = files.FirstOrDefault(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase));
        var payload = DropPayloadFactory.Create(
            audio, subtitle, speaker: null, trackHint: layer,
            advanceToItemEnd: config.AdvanceToItemEnd, version: AppVersion);

        return adapter.Import(payload);
    }

    private void OnPairReady(string wavPath, string txtPath)
    {
        IImportAdapter? adapter;
        AviutlDropConfig? config;
        lock (_gate) { adapter = _adapter; config = _config; }
        if (adapter is null || config is null)
            return;

        var name = Path.GetFileName(wavPath);
        var route = DropRouting.Resolve(name, config);
        var speaker = route.Speaker is null ? "" : $"（{route.Speaker}）";
        Log?.Invoke($"検知: {name}{speaker} → トラック {route.Layer}");

        // wav と同名 txt を一緒に投入する（PSDToolKit の発動条件②に合わせる）。
        var subtitle = File.Exists(txtPath) ? txtPath : null;
        var payload = DropPayloadFactory.Create(
            wavPath, subtitle, route.Speaker, route.Layer,
            config.AdvanceToItemEnd, AppVersion);

        var result = adapter.Import(payload);
        Log?.Invoke(result.Success
            ? $"投入成功: {name}{(result.Info is { } i ? $"（{i}）" : "")}"
            : $"投入失敗: {result.Error}");
    }

    /// <summary>モードに対応するアダプタを生成する（MacroOnly は null）。</summary>
    private IImportAdapter? CreateAdapter(IntegrationMode mode, IntegrationSettings settings) => mode switch
    {
        IntegrationMode.AviUtl => new GcmzImportAdapter(_gcmz, settings.AviUtl, protocolVersion: 1),
        IntegrationMode.AviUtl2 => new GcmzImportAdapter(_gcmz, settings.AviUtl2, protocolVersion: 2),
        IntegrationMode.External => new ExternalCommandAdapter(settings.External.CommandTemplate, settings.External.TimeoutMs),
        _ => null,
    };

    public void Dispose()
    {
        _watcher.Dispose();
    }
}
