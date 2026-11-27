using System.IO;
using System.Text;
using VolumeCtrlWpf;

namespace VolumeFull.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ConfigTests
{
    private string _dataDir;
    private string _configPath;
    private string _backup;

    [TestInitialize]
    public void Setup()
    {
        _dataDir = Config.ConfigDir;
        _configPath = Config.ConfigPath;
        _backup = File.Exists(_configPath) ? File.ReadAllText(_configPath, Encoding.UTF8) : null;
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_backup != null)
        {
            Directory.CreateDirectory(_dataDir);
            File.WriteAllText(_configPath, _backup, Encoding.UTF8);
        }
        else if (File.Exists(_configPath))
        {
            File.Delete(_configPath);
        }
    }

    [TestMethod]
    public void Defaults_AreExpected()
    {
        var c = new Config();
        Assert.IsTrue(c.RememberVolumes);
        Assert.AreEqual(1, c.VolumeStep);
        Assert.AreEqual(367, c.WindowWidth);
        Assert.AreEqual(800, c.WindowHeight);
        Assert.AreEqual(0, c.ThemeMode);
        Assert.IsFalse(c.PopupMode);
        Assert.AreEqual("Ctrl+Alt+Up", c.HotkeyVolumeUp);
        Assert.AreEqual("Ctrl+Alt+Down", c.HotkeyVolumeDown);
        Assert.AreEqual("Ctrl+Alt+M", c.HotkeyMute);
        Assert.AreEqual("Ctrl+Alt+Shift+V", c.HotkeyPanel);
        Assert.AreEqual("Ctrl+Alt+Shift+D", c.HotkeyNextDevice);
    }

    [TestMethod]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        var c = new Config
        {
            Startup = true,
            EnableHotkeys = true,
            EnableWheel = true,
            ThemeMode = 2,
            VolumeStep = 5,
            PopupMode = true,
            RememberFlyoutPosition = true,
            PopupLeft = 123.5,
            PopupTop = 456.25,
            HotkeyMute = "Ctrl+Shift+M",
            MaxStartupVolume = 30,
            LimitStartupVolume = true
        };
        c.Volumes["chrome"] = 0.42f;
        c.Mutes["chrome"] = true;
        c.LockedVolumes["spotify"] = new VolumeLock { Volume = 0.8f, Mode = VolumeLock.ModeAlways };

        var scene = new Scene { Name = "夜间模式", DeviceId = "dev-1" };
        scene.Volumes["game"] = 0.3f;
        scene.Mutes["game"] = true;
        c.Scenes.Add(scene);
        c.Save();

        Config loaded = Config.Load();
        Assert.IsTrue(loaded.Startup);
        Assert.IsTrue(loaded.EnableHotkeys);
        Assert.IsTrue(loaded.EnableWheel);
        Assert.AreEqual(2, loaded.ThemeMode);
        Assert.AreEqual(5, loaded.VolumeStep);
        Assert.IsTrue(loaded.PopupMode);
        Assert.IsTrue(loaded.RememberFlyoutPosition);
        Assert.AreEqual(123.5, loaded.PopupLeft, 0.0001);
        Assert.AreEqual(456.25, loaded.PopupTop, 0.0001);
        Assert.AreEqual("Ctrl+Shift+M", loaded.HotkeyMute);
        Assert.AreEqual(30, loaded.MaxStartupVolume);
        Assert.IsTrue(loaded.LimitStartupVolume);
        Assert.AreEqual(0.42f, loaded.Volumes["chrome"], 0.0001f);
        Assert.IsTrue(loaded.Mutes["chrome"]);
        Assert.AreEqual(0.8f, loaded.LockedVolumes["spotify"].Volume, 0.0001f);
        Assert.AreEqual(VolumeLock.ModeAlways, loaded.LockedVolumes["spotify"].Mode);

        Assert.AreEqual(1, loaded.Scenes.Count);
        Assert.AreEqual("夜间模式", loaded.Scenes[0].Name);
        Assert.AreEqual("dev-1", loaded.Scenes[0].DeviceId);
        Assert.AreEqual(0.3f, loaded.Scenes[0].Volumes["game"], 0.0001f);
        Assert.IsTrue(loaded.Scenes[0].Mutes["game"]);
    }

    [TestMethod]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        if (File.Exists(_configPath)) File.Delete(_configPath);

        Config loaded = Config.Load();
        Assert.IsFalse(loaded.Startup);
        Assert.AreEqual(1, loaded.VolumeStep);
        Assert.AreEqual(0, loaded.Scenes.Count);
    }
}
