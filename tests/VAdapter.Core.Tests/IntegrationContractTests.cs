using System.Text;
using VAdapter.Core.Integration;
using VAdapter.Core.Serialization;

namespace VAdapter.Core.Tests;

public class IntegrationContractTests
{
    // --- DropPayload round-trip ---

    [Fact]
    public void DropPayload_RoundTrips()
    {
        var payload = DropPayloadFactory.Create(
            audioPath: @"C:\out\0001_ずんだもん_こんにちは.wav",
            subtitlePath: @"C:\out\0001_ずんだもん_こんにちは.txt",
            speaker: "ずんだもん",
            trackHint: 5,
            advanceToItemEnd: true,
            version: "0.0.2");

        var json = VAdapterJson.Serialize(payload);
        var restored = VAdapterJson.Deserialize<DropPayload>(json);

        Assert.NotNull(restored);
        Assert.Equal(1, restored!.SchemaVersion);
        Assert.Equal("V-Adapter", restored.Source.App);
        Assert.Equal("0.0.2", restored.Source.Version);
        Assert.Equal("ずんだもん", restored.Speaker);
        Assert.Equal(5, restored.Routing.TrackHint);
        Assert.True(restored.Timing.AdvanceToItemEnd);
        Assert.Collection(restored.Files,
            f => { Assert.Equal(DropFileRole.Audio, f.Role); Assert.EndsWith(".wav", f.Path); },
            f => { Assert.Equal(DropFileRole.Subtitle, f.Role); Assert.EndsWith(".txt", f.Path); });
    }

    [Fact]
    public void DropPayload_SerializesEnumLikeFields_CamelCase()
    {
        var json = VAdapterJson.Serialize(DropPayloadFactory.Create("a.wav", null, null, 1, false));
        Assert.Contains("schemaVersion", json);
        Assert.Contains("trackHint", json);
        Assert.Contains("advanceToItemEnd", json);
    }

    [Fact]
    public void Factory_OmitsSubtitle_WhenNull()
    {
        var payload = DropPayloadFactory.Create("a.wav", null, "", 2, false);
        Assert.Single(payload.Files);
        Assert.Null(payload.Speaker);
        Assert.Equal(2, payload.Routing.TrackHint);
    }

    // --- GcmzPayloadMapper ---

    [Fact]
    public void Mapper_MapsTrackHint_ToLayer()
    {
        var payload = DropPayloadFactory.Create("a.wav", null, null, 7, false);
        var args = GcmzPayloadMapper.Map(payload, new GcmzMapContext(Fps: 30, ManualFrameAdvance: 0, Margin: null, ProtocolVersion: 1, DefaultLayer: 1));
        Assert.Equal(7, args.Layer);
        Assert.Single(args.Files);
        Assert.Equal(1, args.ProtocolVersion);
        Assert.Null(args.Margin);
        Assert.Null(args.FrameAdvance);
    }

    [Fact]
    public void Mapper_UsesDefaultLayer_WhenTrackHintNull()
    {
        var payload = new DropPayload { Files = { new DropFile { Role = DropFileRole.Audio, Path = "a.wav" } } };
        var args = GcmzPayloadMapper.Map(payload, new GcmzMapContext(0, 0, null, 1, 9));
        Assert.Equal(9, args.Layer);
    }

    [Fact]
    public void Mapper_IncludesMargin_OnlyForProtocolV2()
    {
        var payload = DropPayloadFactory.Create("a.wav", null, null, 1, false);
        var v1 = GcmzPayloadMapper.Map(payload, new GcmzMapContext(0, 0, Margin: 10, ProtocolVersion: 1, DefaultLayer: 1));
        var v2 = GcmzPayloadMapper.Map(payload, new GcmzMapContext(0, 0, Margin: 10, ProtocolVersion: 2, DefaultLayer: 1));
        Assert.Null(v1.Margin);
        Assert.Equal(10, v2.Margin);
        Assert.Equal(2, v2.ProtocolVersion);
    }

    [Fact]
    public void Mapper_FallsBackToManualFrameAdvance_WhenNotAdvancingToEnd()
    {
        var payload = DropPayloadFactory.Create("a.wav", null, null, 1, advanceToItemEnd: false);
        var args = GcmzPayloadMapper.Map(payload, new GcmzMapContext(30, ManualFrameAdvance: 12, null, 1, 1));
        Assert.Equal(12, args.FrameAdvance);
    }

    [Fact]
    public void Mapper_ComputesFrameAdvance_FromWavDuration_WhenAdvancingToEnd()
    {
        var wav = WriteWav(byteRate: 16000, dataSize: 16000); // 1.0 秒
        try
        {
            var payload = DropPayloadFactory.Create(wav, null, null, 1, advanceToItemEnd: true);
            var args = GcmzPayloadMapper.Map(payload, new GcmzMapContext(Fps: 30, ManualFrameAdvance: 5, null, 1, 1));
            Assert.Equal(30, args.FrameAdvance); // ceil(1.0 * 30)
        }
        finally
        {
            File.Delete(wav);
        }
    }

    // --- ExternalCommandLine ---

    [Fact]
    public void CommandLine_SubstitutesPayload_AndSplitsFirstToken()
    {
        var (file, args) = ExternalCommandLine.Build("python davinci_import.py {payload}", @"C:\tmp\p.json");
        Assert.Equal("python", file);
        Assert.Equal(@"davinci_import.py C:\tmp\p.json", args);
    }

    [Fact]
    public void CommandLine_QuotesPayloadPath_WithSpaces()
    {
        var (file, args) = ExternalCommandLine.Build("import_to_myapp.exe {payload}", @"C:\my temp\p.json");
        Assert.Equal("import_to_myapp.exe", file);
        Assert.Equal("\"C:\\my temp\\p.json\"", args);
    }

    [Fact]
    public void CommandLine_HandlesQuotedExecutablePath()
    {
        var (file, args) = ExternalCommandLine.Build("\"C:\\Program Files\\app\\run.exe\" --in {payload}", @"C:\p.json");
        Assert.Equal(@"C:\Program Files\app\run.exe", file);
        Assert.Equal(@"--in C:\p.json", args);
    }

    // --- ExternalCommandAdapter ---

    [Fact]
    public void External_Status_Unavailable_WhenNoCommand()
    {
        var adapter = new ExternalCommandAdapter("", 5000);
        Assert.False(adapter.GetStatus().Available);
        Assert.False(adapter.Import(DropPayloadFactory.Create("a.wav", null, null, 1, false)).Success);
    }

    [Fact]
    public void External_Import_Succeeds_OnExitCodeZero()
    {
        var adapter = new ExternalCommandAdapter("cmd /c exit 0", 5000);
        var result = adapter.Import(DropPayloadFactory.Create("a.wav", null, null, 1, false));
        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public void External_Import_Fails_OnNonZeroExit()
    {
        var adapter = new ExternalCommandAdapter("cmd /c exit 1", 5000);
        Assert.False(adapter.Import(DropPayloadFactory.Create("a.wav", null, null, 1, false)).Success);
    }

    [Fact]
    public void External_Import_PrefersStdoutJson()
    {
        // stdout に success:false を返すと、exit 0 でも失敗扱いになる。
        var adapter = new ExternalCommandAdapter("cmd /c echo {\"success\": false, \"message\": \"NG\"}", 5000);
        var result = adapter.Import(DropPayloadFactory.Create("a.wav", null, null, 1, false));
        Assert.False(result.Success);
        Assert.Equal("NG", result.Error);
    }

    // --- helpers ---

    /// <summary>WavInfo がパースできる最小 WAV（fmt+data ヘッダのみ、実 PCM は書かない）を生成。</summary>
    private static string WriteWav(int byteRate, long dataSize)
    {
        var path = Path.Combine(Path.GetTempPath(), $"vadapter-test-{Guid.NewGuid():N}.wav");
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs, Encoding.ASCII);
        w.Write("RIFF".ToCharArray());
        w.Write(36 + 0);                 // RIFF サイズ（実データを書かないため概算）
        w.Write("WAVE".ToCharArray());
        w.Write("fmt ".ToCharArray());
        w.Write(16);                     // fmt サイズ
        w.Write((ushort)1);              // audioFormat = PCM
        w.Write((ushort)2);              // channels
        w.Write(8000);                   // sampleRate
        w.Write(byteRate);               // byteRate
        w.Write((ushort)4);              // blockAlign
        w.Write((ushort)16);             // bitsPerSample
        w.Write("data".ToCharArray());
        w.Write((uint)dataSize);         // data チャンクサイズ（実 PCM は省略）
        return path;
    }
}
