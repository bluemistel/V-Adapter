using VAdapter.Core.Update;

namespace VAdapter.Core.Tests;

public class UpdateCheckTests
{
    [Theory]
    [InlineData("v0.0.4", 0, 0, 4)]
    [InlineData("0.0.4", 0, 0, 4)]
    [InlineData("v0.0.4-alpha", 0, 0, 4)]
    [InlineData("V1.2", 1, 2, 0)]
    [InlineData(" v0.0.4 ", 0, 0, 4)]
    public void ParseTag_ParsesCommonForms(string tag, int major, int minor, int build)
    {
        var v = UpdateCheck.ParseTag(tag);
        Assert.NotNull(v);
        Assert.Equal(new Version(major, minor, build), v);
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("release")]
    [InlineData(null)]
    public void ParseTag_ReturnsNull_ForNonVersion(string? tag)
    {
        Assert.Null(UpdateCheck.ParseTag(tag));
    }

    [Fact]
    public void SelectLatest_PicksHighest()
    {
        var tags = new[] { "v0.0.2", "v0.0.10", "v0.0.4", "nightly", "v0.0.9" };
        Assert.Equal(new Version(0, 0, 10), UpdateCheck.SelectLatest(tags));
    }

    [Fact]
    public void SelectLatest_Empty_ReturnsNull()
    {
        Assert.Null(UpdateCheck.SelectLatest(new string?[] { "x", null, "" }));
    }

    [Fact]
    public void IsUpdateAvailable_TrueWhenLatestNewer()
    {
        Assert.True(UpdateCheck.IsUpdateAvailable(new Version(0, 0, 4), new Version(0, 0, 5)));
        Assert.True(UpdateCheck.IsUpdateAvailable(new Version(0, 0, 4), new Version(0, 1, 0)));
    }

    [Fact]
    public void IsUpdateAvailable_FalseWhenSameOrOlderOrNull()
    {
        Assert.False(UpdateCheck.IsUpdateAvailable(new Version(0, 0, 4), new Version(0, 0, 4)));
        Assert.False(UpdateCheck.IsUpdateAvailable(new Version(0, 0, 4), new Version(0, 0, 3)));
        Assert.False(UpdateCheck.IsUpdateAvailable(new Version(0, 0, 4), null));
    }

    [Fact]
    public void IsUpdateAvailable_IgnoresRevision()
    {
        // 0.0.4.0 と 0.0.4.9 は同一扱い。
        Assert.False(UpdateCheck.IsUpdateAvailable(new Version(0, 0, 4, 0), new Version(0, 0, 4, 9)));
    }
}
