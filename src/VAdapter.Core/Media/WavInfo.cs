namespace VAdapter.Core.Media;

/// <summary>WAV ファイルのヘッダから再生時間を求める軽量パーサ（PCM 想定）。</summary>
public static class WavInfo
{
    /// <summary>
    /// WAV の再生時間（秒）を返す。解析できない場合は null。
    /// data チャンクサイズ / byteRate で算出する（PCM で正確）。
    /// </summary>
    public static double? TryGetDurationSeconds(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            if (fs.Length < 12)
                return null;
            if (new string(br.ReadChars(4)) != "RIFF")
                return null;
            br.ReadUInt32(); // RIFF サイズ
            if (new string(br.ReadChars(4)) != "WAVE")
                return null;

            int byteRate = 0;
            long dataSize = -1;

            while (fs.Position + 8 <= fs.Length)
            {
                var chunkId = new string(br.ReadChars(4));
                uint chunkSize = br.ReadUInt32();
                long chunkStart = fs.Position;

                if (chunkId == "fmt ")
                {
                    br.ReadUInt16(); // audioFormat
                    br.ReadUInt16(); // channels
                    br.ReadUInt32(); // sampleRate
                    byteRate = br.ReadInt32();
                    // 残りの fmt フィールドは読み飛ばす
                }
                else if (chunkId == "data")
                {
                    dataSize = chunkSize;
                }

                // 次のチャンクへ（ワード境界のパディング考慮）
                long next = chunkStart + chunkSize + (chunkSize % 2 == 1 ? 1 : 0);
                if (next <= fs.Position && chunkId != "data")
                    break; // 異常（無限ループ防止）
                fs.Position = next;

                if (byteRate > 0 && dataSize >= 0)
                    break;
            }

            if (byteRate <= 0 || dataSize < 0)
                return null;
            return (double)dataSize / byteRate;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>WAV の長さをフレーム数に換算する（fps が無効なら null）。</summary>
    public static int? TryGetDurationFrames(string path, double fps)
    {
        if (fps <= 0)
            return null;
        var seconds = TryGetDurationSeconds(path);
        if (seconds is not { } s)
            return null;
        return Math.Max(1, (int)Math.Ceiling(s * fps));
    }
}
