using VAdapter.Core.Models;

namespace VAdapter.Core.Tests;

public class KeyCombinationTests
{
    [Fact]
    public void PhysicalVirtualKeys_ModifiersThenMainKey()
    {
        var combo = new KeyCombination(KeyModifiers.Control | KeyModifiers.Shift, 0x4A, "J");
        Assert.Equal(new[] { 0x11, 0x10, 0x4A }, combo.PhysicalVirtualKeys());
    }

    [Fact]
    public void PhysicalVirtualKeys_WinIncludesBothSides()
    {
        var combo = new KeyCombination(KeyModifiers.Win | KeyModifiers.Alt, 0x74, "F5");
        Assert.Equal(new[] { 0x12, 0x5B, 0x5C, 0x74 }, combo.PhysicalVirtualKeys());
    }

    [Fact]
    public void PhysicalVirtualKeys_UnsetMainKey_OnlyModifiers()
    {
        var combo = new KeyCombination { Modifiers = KeyModifiers.Control };
        Assert.Equal(new[] { 0x11 }, combo.PhysicalVirtualKeys());
    }
}
