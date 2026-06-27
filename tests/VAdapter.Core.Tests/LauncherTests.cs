using VAdapter.Core.Launch;
using VAdapter.Core.Models;
using VAdapter.Core.Serialization;

namespace VAdapter.Core.Tests;

public class LauncherTests
{
    [Fact]
    public void Launch_Fails_WhenPathEmpty()
    {
        var r = AppLauncher.Launch("", skipIfRunning: false);
        Assert.False(r.Success);
        Assert.NotNull(r.Error);
    }

    [Fact]
    public void Launch_Fails_WhenFileMissing()
    {
        var bogus = Path.Combine(Path.GetTempPath(), "vadapter-missing-" + Guid.NewGuid().ToString("N") + ".exe");
        var r = AppLauncher.Launch(bogus, skipIfRunning: false);
        Assert.False(r.Success);
        Assert.Contains("見つかりません", r.Error);
    }

    [Fact]
    public void IsRunning_False_ForBogusName()
    {
        Assert.False(AppLauncher.IsRunning("vadapter-no-such-process-" + Guid.NewGuid().ToString("N")));
        Assert.False(AppLauncher.IsRunning(null));
    }

    [Fact]
    public void LaunchAppInstruction_RoundTrips_AsPolymorphic()
    {
        Instruction instr = new LaunchAppInstruction
        {
            ExecutablePath = @"C:\Program Files\VOICEPEAK\voicepeak.exe",
            Arguments = "--foo",
            SkipIfRunning = false,
        };

        var json = VAdapterJson.Serialize(instr);
        Assert.Contains("\"kind\": \"launchapp\"", json);

        var restored = VAdapterJson.Deserialize<Instruction>(json);
        var launch = Assert.IsType<LaunchAppInstruction>(restored);
        Assert.Equal(@"C:\Program Files\VOICEPEAK\voicepeak.exe", launch.ExecutablePath);
        Assert.Equal("--foo", launch.Arguments);
        Assert.False(launch.SkipIfRunning);
    }

    [Fact]
    public void LaunchAppInstruction_Summary_ReflectsState()
    {
        Assert.Equal("対象アプリを起動", new LaunchAppInstruction().Summary);
        Assert.Equal("起動: voicepeak.exe",
            new LaunchAppInstruction { ExecutablePath = @"C:\x\voicepeak.exe" }.Summary);
    }

    [Fact]
    public void EditorPathFor_ReturnsPerModePath()
    {
        var s = new IntegrationSettings
        {
            MacroEditorPath = @"C:\ymm\YukkuriMovieMaker.exe",
        };
        s.AviUtl.EditorPath = @"C:\aviutl\aviutl.exe";
        s.AviUtl2.EditorPath = @"C:\aviutl2\aviutl2.exe";
        s.External.EditorPath = @"C:\ext\editor.exe";

        Assert.Equal(@"C:\ymm\YukkuriMovieMaker.exe", s.EditorPathFor(IntegrationMode.MacroOnly));
        Assert.Equal(@"C:\aviutl\aviutl.exe", s.EditorPathFor(IntegrationMode.AviUtl));
        Assert.Equal(@"C:\aviutl2\aviutl2.exe", s.EditorPathFor(IntegrationMode.AviUtl2));
        Assert.Equal(@"C:\ext\editor.exe", s.EditorPathFor(IntegrationMode.External));
    }

    [Fact]
    public void IntegrationSettings_RoundTrips_EditorPaths()
    {
        var s = new IntegrationSettings { MacroEditorPath = @"C:\ymm\ymm.exe" };
        s.AviUtl.EditorPath = @"C:\aviutl\aviutl.exe";

        var restored = VAdapterJson.Deserialize<IntegrationSettings>(VAdapterJson.Serialize(s))!;
        Assert.Equal(@"C:\ymm\ymm.exe", restored.MacroEditorPath);
        Assert.Equal(@"C:\aviutl\aviutl.exe", restored.AviUtl.EditorPath);
    }

    [Fact]
    public void TargetApplication_RoundTrips_ExecutablePath()
    {
        var t = new TargetApplication { Name = "VOICEPEAK", ExecutablePath = @"C:\VOICEPEAK\voicepeak.exe" };
        var restored = VAdapterJson.Deserialize<TargetApplication>(VAdapterJson.Serialize(t))!;
        Assert.Equal(@"C:\VOICEPEAK\voicepeak.exe", restored.ExecutablePath);
    }
}
