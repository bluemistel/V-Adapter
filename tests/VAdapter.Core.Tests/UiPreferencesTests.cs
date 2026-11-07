using VAdapter.Core.Models;
using VAdapter.Core.Storage;

namespace VAdapter.Core.Tests;

public class UiPreferencesTests
{
    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), "vadapter-test", "preferences-" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void Missing_ReturnsAutoMode()
    {
        var prefs = new UiPreferencesStore(TempPath()).Load();
        Assert.False(prefs.ManualSendTarget);
        Assert.Null(prefs.SendTargetId);
    }

    [Fact]
    public void ManualTarget_RoundTrips()
    {
        var path = TempPath();
        try
        {
            var store = new UiPreferencesStore(path);
            store.Save(new UiPreferences { ManualSendTarget = true, SendTargetId = "b7e2c1a4d5f6470890a1b2c3d4e5f701" });

            var restored = new UiPreferencesStore(path).Load();
            Assert.True(restored.ManualSendTarget);
            Assert.Equal("b7e2c1a4d5f6470890a1b2c3d4e5f701", restored.SendTargetId);

            // 上書き保存（既存ファイルの置換）でも反映されること。
            store.Save(new UiPreferences { ManualSendTarget = false });
            Assert.False(new UiPreferencesStore(path).Load().ManualSendTarget);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Corrupt_ReturnsDefault()
    {
        var path = TempPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{ not json");
            Assert.False(new UiPreferencesStore(path).Load().ManualSendTarget);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
