namespace VAdapter.Core.Integration;

/// <summary>
/// 検知結果（音声＋任意の字幕＋ルーティング結果）から中立 <see cref="DropPayload"/> を組み立てる純粋ファクトリ。
/// ファイル存在判定など I/O は呼び出し側が行い、ここはデータ整形のみを担う（テスト容易性のため）。
/// </summary>
public static class DropPayloadFactory
{
    /// <summary>
    /// 音声（必須）と字幕（任意）からペイロードを生成する。
    /// </summary>
    /// <param name="audioPath">音声ファイルのフルパス。</param>
    /// <param name="subtitlePath">同名字幕のフルパス（存在しなければ null）。</param>
    /// <param name="speaker">解決済み話者名（任意）。</param>
    /// <param name="trackHint">希望トラック（レイヤー）番号。</param>
    /// <param name="advanceToItemEnd">投入後に終端へシークする意図か。</param>
    /// <param name="version">送信元アプリのバージョン（任意）。</param>
    public static DropPayload Create(
        string audioPath,
        string? subtitlePath,
        string? speaker,
        int trackHint,
        bool advanceToItemEnd,
        string? version = null)
    {
        var files = new List<DropFile>
        {
            new() { Role = DropFileRole.Audio, Path = audioPath },
        };
        if (!string.IsNullOrEmpty(subtitlePath))
            files.Add(new DropFile { Role = DropFileRole.Subtitle, Path = subtitlePath });

        return new DropPayload
        {
            Source = new DropSource { App = "V-Adapter", Version = version ?? string.Empty },
            Files = files,
            Speaker = string.IsNullOrEmpty(speaker) ? null : speaker,
            Routing = new DropRoutingHint { TrackHint = trackHint },
            Timing = new DropTiming { AdvanceToItemEnd = advanceToItemEnd },
        };
    }
}
