using VolumeCtrlWpf;

namespace VolumeFull.Tests;

[TestClass]
public sealed class HotKeyHelperTests
{
    [TestMethod]
    public void TryParse_DefaultHotkey()
    {
        Assert.IsTrue(HotKeyHelper.TryParse("Ctrl+Alt+Up", out uint mods, out uint vk));
        Assert.AreEqual(0x2u | 0x1u, mods);
        Assert.AreEqual(0x26u, vk);
    }

    [TestMethod]
    public void TryParse_Letter()
    {
        Assert.IsTrue(HotKeyHelper.TryParse("Ctrl+Shift+M", out uint mods, out uint vk));
        Assert.AreEqual(0x2u | 0x4u, mods);
        Assert.AreEqual((uint)'M', vk);
    }

    [TestMethod]
    public void TryParse_FunctionKey()
    {
        Assert.IsTrue(HotKeyHelper.TryParse("Alt+F13", out uint mods, out uint vk));
        Assert.AreEqual(0x1u, mods);
        Assert.AreEqual(0x7Cu, vk);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("M")]
    [DataRow("Ctrl+")]
    [DataRow("Ctrl+UnknownKey")]
    public void TryParse_Invalid_ReturnsFalse(string text)
    {
        Assert.IsFalse(HotKeyHelper.TryParse(text, out _, out _));
    }
}
