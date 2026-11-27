using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VolumeCtrlWpf
{
    internal class Scene
    {
        public string Name;
        public string DeviceId;
        public readonly Dictionary<string, float> Volumes = new Dictionary<string, float>();
        public readonly Dictionary<string, bool> Mutes = new Dictionary<string, bool>();
    }

    /// <summary>
    /// 应用音量锁定（参照同类音量工具的 LockVolume）：
    /// Mode = 0 表示不锁（不存入字典），1 = 直到应用关闭，2 = 始终（重启后仍锁）。
    /// </summary>
    internal class VolumeLock
    {
        public const int ModeUntilClose = 1;
        public const int ModeAlways = 2;

        public float Volume;
        public int Mode = ModeAlways;
    }

    internal class Config
    {
        public bool Startup;
        public bool RememberVolumes = true;
        public bool EnableHotkeys = false;
        public bool ExclusiveHotkeys = false;
        public bool EnableWheel = false;
        public bool TopMost = false;
        public double WindowLeft = double.NaN;
        public double WindowTop = double.NaN;
        public double WindowWidth = 367;
        public double WindowHeight = 800;
        public bool DarkTheme = false;
        public int ThemeMode = 0;
        public int TrayIconKind = 1;
        public bool PopupMode = false;
        public bool RememberFlyoutPosition = false;
        public double PopupLeft = double.NaN;
        public double PopupTop = double.NaN;
        public bool LimitStartupVolume = false;
        public int MaxStartupVolume = 40;
        public int VolumeStep = 1;
        public readonly Dictionary<string, VolumeLock> LockedVolumes = new Dictionary<string, VolumeLock>();
        public string HotkeyVolumeUp = "Ctrl+Alt+Up";
        public string HotkeyVolumeDown = "Ctrl+Alt+Down";
        public string HotkeyMute = "Ctrl+Alt+M";
        public string HotkeyPanel = "Ctrl+Alt+Shift+V";
        public string HotkeyNextDevice = "Ctrl+Alt+Shift+D";
        public readonly Dictionary<string, float> Volumes = new Dictionary<string, float>();
        public readonly Dictionary<string, bool> Mutes = new Dictionary<string, bool>();
        public readonly List<Scene> Scenes = new List<Scene>();

        public static string ConfigDir
        {
            get
            {
                try
                {
                    string baseDir = AppContext.BaseDirectory;
                    if (!string.IsNullOrEmpty(baseDir))
                    {
                        string dir = Path.Combine(baseDir, "data");
                        Directory.CreateDirectory(dir);
                        return dir;
                    }
                }
                catch { }
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VolumeCtrlLite");
            }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(ConfigDir, "config.ini"); }
        }

        public static Config Load()
        {
            Config c = new Config();
            try
            {
                if (!File.Exists(ConfigPath)) return c;
                string section = "";
                foreach (string raw in File.ReadAllLines(ConfigPath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";")) continue;
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        section = line.Substring(1, line.Length - 2);
                        if (section.StartsWith("Scene:"))
                        {
                            Scene s = new Scene();
                            s.Name = section.Substring(6);
                            c.Scenes.Add(s);
                        }
                        continue;
                    }
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim();
                    if (section == "General")
                    {
                        if (key == "Startup") c.Startup = value == "1";
                        else if (key == "RememberVolumes") c.RememberVolumes = value == "1";
                        else if (key == "EnableHotkeys") c.EnableHotkeys = value == "1";
                        else if (key == "ExclusiveHotkeys") c.ExclusiveHotkeys = value == "1";
                        else if (key == "EnableWheel") c.EnableWheel = value == "1";
                        else if (key == "TopMost") c.TopMost = value == "1";
                        else if (key == "WindowLeft") { double d; if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) c.WindowLeft = d; }
                        else if (key == "WindowTop") { double d; if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) c.WindowTop = d; }
                        else if (key == "WindowWidth") { double d; if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) c.WindowWidth = d; }
                        else if (key == "WindowHeight") { double d; if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) c.WindowHeight = d; }
                        else if (key == "DarkTheme") c.DarkTheme = value == "1";
                        else if (key == "ThemeMode") { int m; if (int.TryParse(value, out m)) c.ThemeMode = m; }
                        else if (key == "TrayIconKind") { int m; if (int.TryParse(value, out m)) c.TrayIconKind = m; }
                        else if (key == "PopupMode") c.PopupMode = value == "1";
                        else if (key == "RememberFlyoutPosition") c.RememberFlyoutPosition = value == "1";
                        else if (key == "PopupLeft") { double d; if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) c.PopupLeft = d; }
                        else if (key == "PopupTop") { double d; if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) c.PopupTop = d; }
                        else if (key == "LimitStartupVolume") c.LimitStartupVolume = value == "1";
                        else if (key == "MaxStartupVolume") { int m; if (int.TryParse(value, out m)) c.MaxStartupVolume = m; }
                        else if (key == "VolumeStep") { int m; if (int.TryParse(value, out m)) c.VolumeStep = m; }
                    }
                    else if (section == "Hotkeys")
                    {
                        if (key == "VolumeUp") c.HotkeyVolumeUp = value;
                        else if (key == "VolumeDown") c.HotkeyVolumeDown = value;
                        else if (key == "Mute") c.HotkeyMute = value;
                        else if (key == "Panel") c.HotkeyPanel = value;
                        else if (key == "NextDevice") c.HotkeyNextDevice = value;
                    }
                    else if (section.StartsWith("Scene:"))
                    {
                        if (c.Scenes.Count == 0) continue;
                        Scene s = c.Scenes[c.Scenes.Count - 1];
                        if (s.Name != section.Substring(6)) continue;
                        if (key == "Device") s.DeviceId = value;
                        else if (key.StartsWith("Vol:"))
                        {
                            float v;
                            if (float.TryParse(value, out v)) s.Volumes[key.Substring(4)] = v;
                        }
                        else if (key.StartsWith("Mute:")) s.Mutes[key.Substring(5)] = value == "1";
                    }
                    else if (section == "Volumes")
                    {
                        float v;
                        if (float.TryParse(value, out v)) c.Volumes[key] = v;
                    }
                    else if (section == "Mutes")
                    {
                        c.Mutes[key] = value == "1";
                    }
                    else if (section == "Locked")
                    {
                        // 新格式 volume|mode；旧格式只有 volume（视为「始终」）
                        string[] parts = value.Split('|');
                        float v;
                        if (float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v))
                        {
                            VolumeLock lk = new VolumeLock();
                            lk.Volume = v;
                            lk.Mode = VolumeLock.ModeAlways;
                            int mode;
                            if (parts.Length > 1 && int.TryParse(parts[1], out mode) && (mode == VolumeLock.ModeUntilClose || mode == VolumeLock.ModeAlways))
                                lk.Mode = mode;
                            c.LockedVolumes[key] = lk;
                        }
                    }
                }
            }
            catch { }
            return c;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("[General]");
                sb.AppendLine("Startup=" + (Startup ? "1" : "0"));
                sb.AppendLine("RememberVolumes=" + (RememberVolumes ? "1" : "0"));
                sb.AppendLine("EnableHotkeys=" + (EnableHotkeys ? "1" : "0"));
                sb.AppendLine("ExclusiveHotkeys=" + (ExclusiveHotkeys ? "1" : "0"));
                sb.AppendLine("EnableWheel=" + (EnableWheel ? "1" : "0"));
                sb.AppendLine("TopMost=" + (TopMost ? "1" : "0"));
                sb.AppendLine("WindowLeft=" + WindowLeft.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("WindowTop=" + WindowTop.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("WindowWidth=" + WindowWidth.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("WindowHeight=" + WindowHeight.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("DarkTheme=" + (DarkTheme ? "1" : "0"));
                sb.AppendLine("ThemeMode=" + ThemeMode);
                sb.AppendLine("TrayIconKind=" + TrayIconKind);
                sb.AppendLine("PopupMode=" + (PopupMode ? "1" : "0"));
                sb.AppendLine("RememberFlyoutPosition=" + (RememberFlyoutPosition ? "1" : "0"));
                sb.AppendLine("PopupLeft=" + PopupLeft.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("PopupTop=" + PopupTop.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine("LimitStartupVolume=" + (LimitStartupVolume ? "1" : "0"));
                sb.AppendLine("MaxStartupVolume=" + MaxStartupVolume);
                sb.AppendLine("VolumeStep=" + VolumeStep);
                sb.AppendLine();
                sb.AppendLine("[Hotkeys]");
                sb.AppendLine("VolumeUp=" + HotkeyVolumeUp);
                sb.AppendLine("VolumeDown=" + HotkeyVolumeDown);
                sb.AppendLine("Mute=" + HotkeyMute);
                sb.AppendLine("Panel=" + HotkeyPanel);
                sb.AppendLine("NextDevice=" + HotkeyNextDevice);
                sb.AppendLine();
                sb.AppendLine("[Volumes]");
                foreach (KeyValuePair<string, float> kv in Volumes)
                {
                    sb.AppendLine(kv.Key + "=" + kv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                sb.AppendLine();
                sb.AppendLine("[Mutes]");
                foreach (KeyValuePair<string, bool> kv in Mutes)
                {
                    sb.AppendLine(kv.Key + "=" + (kv.Value ? "1" : "0"));
                }
                sb.AppendLine();
                sb.AppendLine("[Locked]");
                foreach (KeyValuePair<string, VolumeLock> kv in LockedVolumes)
                {
                    sb.AppendLine(kv.Key + "=" + kv.Value.Volume.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + kv.Value.Mode);
                }
                foreach (Scene s in Scenes)
                {
                    sb.AppendLine();
                    sb.AppendLine("[Scene:" + s.Name + "]");
                    sb.AppendLine("Device=" + (s.DeviceId == null ? "" : s.DeviceId));
                    foreach (KeyValuePair<string, float> kv in s.Volumes)
                    {
                        sb.AppendLine("Vol:" + kv.Key + "=" + kv.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    foreach (KeyValuePair<string, bool> kv in s.Mutes)
                    {
                        sb.AppendLine("Mute:" + kv.Key + "=" + (kv.Value ? "1" : "0"));
                    }
                }
                File.WriteAllText(ConfigPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }
    }
}







