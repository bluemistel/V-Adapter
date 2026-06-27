using VAdapter.Core.Media;

namespace VAdapter.Core.Integration;

/// <summary>gcmz 外部連携APIへ渡す具体パラメータ（写像結果）。</summary>
public sealed record GcmzDropArgs(
    IReadOnlyList<string> Files,
    int Layer,
    int? FrameAdvance,
    int? Margin,
    int ProtocolVersion);

/// <summary>
/// gcmz アダプタが必要とする、ペイロード外の文脈（編集ソフト固有の設定値）。
/// </summary>
/// <param name="Fps">プロジェクトのフレームレート（終端シークのフレーム算出に使用。0=不明）。</param>
/// <param name="ManualFrameAdvance">終端シークを使わない場合の手動フレーム数（0=移動なし）。</param>
/// <param name="Margin">占有時の間隔（AviUtl2 のみ有効）。</param>
/// <param name="ProtocolVersion">COPYDATASTRUCT.dwData（AviUtl=1 / AviUtl2=2）。</param>
/// <param name="DefaultLayer">TrackHint 未指定時の既定レイヤー。</param>
public readonly record struct GcmzMapContext(
    double Fps,
    int ManualFrameAdvance,
    int? Margin,
    int ProtocolVersion,
    int DefaultLayer);

/// <summary>
/// 中立 <see cref="DropPayload"/> を gcmz 外部連携APIのパラメータへ写像する純粋関数。
/// 旧 AviutlDropService の写像ロジック（TrackHint→layer / 終端シーク→frameAdvance / margin / dwData）をここへ集約。
/// </summary>
public static class GcmzPayloadMapper
{
    public static GcmzDropArgs Map(DropPayload payload, GcmzMapContext ctx)
    {
        var files = payload.Files.Select(f => f.Path).ToList();
        var layer = payload.Routing.TrackHint ?? ctx.DefaultLayer;
        var frameAdvance = ResolveFrameAdvance(payload, ctx);
        var margin = ctx.ProtocolVersion == 2 ? ctx.Margin : null;
        return new GcmzDropArgs(files, layer, frameAdvance, margin, ctx.ProtocolVersion);
    }

    /// <summary>
    /// 投入後のシーク移動量（フレーム）。終端シークが ON なら音声長×fps、
    /// そうでなければ手動値（0 は null=移動なし）。音声が無い/算出不可なら手動値へフォールバック。
    /// </summary>
    private static int? ResolveFrameAdvance(DropPayload payload, GcmzMapContext ctx)
    {
        if (payload.Timing.AdvanceToItemEnd)
        {
            var audio = payload.Files.FirstOrDefault(f => f.Role == DropFileRole.Audio)?.Path;
            if (audio is not null && WavInfo.TryGetDurationFrames(audio, ctx.Fps) is { } frames)
                return frames;
        }
        return ctx.ManualFrameAdvance > 0 ? ctx.ManualFrameAdvance : null;
    }
}
