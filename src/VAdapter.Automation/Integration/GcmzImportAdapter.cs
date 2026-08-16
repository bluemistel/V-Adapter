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
                ? $"プロジェクト: 読込済み（{info.Width}x{info.Height} / API v{info.ApiVersion}{FormatConnectedTo(info)}）"
                : $"プロジェクト: 未読込（API v{info.ApiVersion}{FormatConnectedTo(info)}）";

        var mismatch = DescribeEditorMismatch(info);
        return new AdapterStatus
        {
            Available = true,
            Summary = mismatch is null ? "ごちゃまぜドロップス: 接続OK" : $"ごちゃまぜドロップス: 接続OK（{mismatch}）",
            TargetInfo = target,
        };
    }

    private static string FormatConnectedTo(GcmzInfo info) =>
        string.IsNullOrEmpty(info.ProcessName) ? "" : $" / 接続先 {info.ProcessName}";

    /// <summary>
    /// 選択中モードと実際の接続先が食い違っていれば説明文を返す（一致・判定不能なら null）。
    /// ミューテックス・共有メモリ名は AviUtl 無印と AviUtl2 で共通のため、両方を起動していると
    /// 先に登録した方へ接続してしまい、投入先や挙動が意図と変わることがある。
    /// </summary>
    private string? DescribeEditorMismatch(GcmzInfo? info)
    {
        var name = info?.ProcessName;
        if (string.IsNullOrEmpty(name))
            return null;

        var isAviUtl2 = name.Equals("aviutl2", StringComparison.OrdinalIgnoreCase);
        var isAviUtl1 = name.Equals("aviutl", StringComparison.OrdinalIgnoreCase);
        if (!isAviUtl2 && !isAviUtl1)
            return null; // 判定できない名前は警告しない。

        var expectsAviUtl2 = _protocolVersion == 2;
        if (expectsAviUtl2 == isAviUtl2)
            return null;

        var expected = expectsAviUtl2 ? "AviUtl2" : "AviUtl";
        return $"警告: 連携モードは {expected} ですが、接続先は {name} です。"
               + "AviUtl と AviUtl2 を同時に起動していると、ごちゃまぜドロップスの共有名が競合して"
               + "意図しない方へ接続されることがあります。使わない方を終了してください。";
    }

    public ImportResult Import(DropPayload payload)
    {
        var info = _gcmz.ReadInfo();
        var mismatch = DescribeEditorMismatch(info);
        var fps = info?.Fps ?? 0;
        var ctx = new GcmzMapContext(
            Fps: fps,
            ManualFrameAdvance: _config.FrameAdvance,
            Margin: _config.Margin,
            ProtocolVersion: _protocolVersion,
            DefaultLayer: _config.DefaultLayer);

        var args = GcmzPayloadMapper.Map(payload, ctx);
        var result = _gcmz.Drop(args.Files, args.Layer, args.FrameAdvance, args.Margin, args.ProtocolVersion);

        if (!result.Success)
            return ImportResult.Fail(result.Error ?? "投げ込みに失敗しました。");

        // 接続先の食い違いは投入自体は成功しても結果が意図と変わるため、必ずログへ出す。
        var seek = args.FrameAdvance is { } fa ? $"シーク +{fa}f" : null;
        var infoText = mismatch is null
            ? seek
            : seek is null ? mismatch : $"{seek} / {mismatch}";
        return ImportResult.Ok(infoText);
    }
}
