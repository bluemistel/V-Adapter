using VAdapter.Core.Media;

namespace VAdapter.Core.Tests;

public class WavInfoTests
{
    /// <summary>最小の PCM WAV を生成（durationSeconds 秒, 8000Hz mono 8bit）。</summary>
    private static string WriteWav(double durationSeconds)
    {
        const int sampleRate = 8000;
        const short channels = 1;
        const short bits = 8;
        int byteRate = sampleRate * channels * bits / 8;     // 8000 B/s
        int dataSize = (int)(byteRate * durationSeconds);
        var path = Path.Combine(Path.GetTempPath(), "vadapter-wav-" + Guid.NewGuid().ToString("N") + ".wav");

        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        w.Write("RIFF".ToCharArray());
        w.Write(36 + dataSize);
        w.Write("WAVE".ToCharArray());
        w.Write("fmt ".ToCharArray());
        w.Write(16);                 // fmt chunk size
        w.Write((short)1);           // PCM
        w.Write(channels);
        w.Write(sampleRate);
        w.Write(byteRate);
        w.Write((short)(channels * bits / 8)); // block align
        w.Write(bits);
        w.Write("data".ToCharArray());
        w.Write(dataSize);
        w.Write(new byte[dataSize]);
        return path;
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.5)]
    public void TryGetDurationSeconds_ParsesPcmWav(double seconds)
    {
        var path = WriteWav(seconds);
        try
        {
            var dur = WavInfo.TryGetDurationSeconds(path);
            Assert.NotNull(dur);
            Assert.Equal(seconds, dur!.Value, precision: 2);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TryGetDurationFrames_UsesFps()
    {
        var path = WriteWav(2.0); // 2 秒
        try
        {
            // 30fps → 60 フレーム
            Assert.Equal(60, WavInfo.TryGetDurationFrames(path, 30.0));
            // 29.97fps → ceil(2*29.97)=60
            Assert.Equal(60, WavInfo.TryGetDurationFrames(path, 30000.0 / 1001.0));
            // fps 無効 → null
            Assert.Null(WavInfo.TryGetDurationFrames(path, 0));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TryGetDurationSeconds_ReturnsNull_ForNonWav()
    {
        var path = Path.Combine(Path.GetTempPath(), "vadapter-notwav-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, "not a wav");
        try { Assert.Null(WavInfo.TryGetDurationSeconds(path)); }
        finally { File.Delete(path); }
    }
}
