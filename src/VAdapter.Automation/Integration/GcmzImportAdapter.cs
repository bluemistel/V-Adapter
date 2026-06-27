using VAdapter.Automation.Aviutl;
using VAdapter.Core.Integration;
using VAdapter.Core.Models;

namespace VAdapter.Automation.Integration;

/// <summary>
/// 組込アダプタ: ごちゃまぜドロップス（gcmz / GCMZDrops2）の外部連携API。
/// 中立 <see cref="DropPayload"/> を <see cref="GcmzPayloadMapper"/> で gcmz パラメータへ写像し、
/// 既存 <see cref="GcmzApi"/>（Win32, 維持）へ委譲する。AviUtl=v1 / AviUtl2=v2。
/// </summary>
public sealed class GcmzImportAdapter : IImportAdapter
{
    private readonly GcmzApi _gcmz;
    private readonly AviutlDropConfig _config;
    private readonly int _protocolVersion;

    public GcmzImportAdapter(GcmzApi gcmz, AviutlDropConfig config, int protocolVersion)
    {
        _gcmz = gcmz;
        _config = config;
        _protocolVersion = protocolVersion;
    }

    public string Id => _protocolVersion == 2 ? "gcmz-aviutl2" : "gcmz-aviutl";

    public string DisplayName => _protocolVersion == 2 ? "AviUtl2 + PSDToolKit2" : "AviUtl + PSDToolKit";

    public AdapterStatus GetStatus()
    {
        if (!_gcmz.IsAvailable())
            return new AdapterStatus
            {
                Available = false,
                Summary = "ごちゃまぜドロップス: 未検出（AviUtl とプラグインの起動を確認）",
            };

        var info = _gcmz.ReadInfo();
        var target = info is null
            ? "プロジェクト: 情報取得不可"
            : info.HasProject
                ? $"プロジェクト: 読込済み（{info.Width}x{info.Height} / API v{info.ApiVersion}）"
                : $"プロジェクト: 未読込（API v{info.ApiVersion}）";

        return new AdapterStatus
        {
            Available = true,
            Summary = "ごちゃまぜドロップス: 接続OK",
            TargetInfo = target,
        };
    }

    public ImportResult Import(DropPayload payload)
    {
        var fps = _gcmz.ReadInfo()?.Fps ?? 0;
        var ctx = new GcmzMapContext(
            Fps: fps,
            ManualFrameAdvance: _config.FrameAdvance,
            Margin: _config.Margin,
            ProtocolVersion: _protocolVersion,
            DefaultLayer: _config.DefaultLayer);

        var args = GcmzPayloadMapper.Map(payload, ctx);
        var result = _gcmz.Drop(args.Files, args.Layer, args.FrameAdvance, args.Margin, args.ProtocolVersion);

        return result.Success
            ? ImportResult.Ok(args.FrameAdvance is { } fa ? $"シーク +{fa}f" : null)
            : ImportResult.Fail(result.Error ?? "投げ込みに失敗しました。");
    }
}
