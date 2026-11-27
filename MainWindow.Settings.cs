using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using H.NotifyIcon;
using VolumeCtrlLite;
using Microsoft.Win32;

namespace VolumeCtrlWpf
{
    public partial class MainWindow : Window
    {
        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            OpenSettings();
        }

        private void OpenSettings()
        {
            SettingsWindow win = new SettingsWindow(_config);
            win.Owner = this;
            _suppressDeactivate = true;
            try
            {
                win.ShowDialog();
                // 开关是即时保存的：无论确定/取消/直接关闭，都要让窗口状态跟上配置，
                // 否则关掉「弹出模式」后窗口会卡在无边框状态（无法拖动、无法关闭）
                _config.Save();
                ApplyStartup();
                ApplyPopupMode();
                RegisterHotkeys();
                _config.DarkTheme = ResolveDark(_config.ThemeMode);
                UpdateTrayIcon();
                ApplyTheme();
                if (IsVisible) PlaceWindow();
            }
            finally
            {
                _suppressDeactivate = false;
            }
        }

        private void ApplyStartup()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (_config.Startup)
                        key.SetValue("VolumeCtrlWpf", "\"" + System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName + "\"");
                    else
                        key.DeleteValue("VolumeCtrlWpf", false);
                }
            }
            catch { }
        }

        private void SaveScene()
        {
            string name = "场景 " + DateTime.Now.ToString("MM-dd HH:mm");
            Scene scene = new Scene();
            scene.Name = name;
            scene.DeviceId = Audio.GetDefaultOutputDeviceId();
            List<SessionInfo> sessions = Audio.GetSessions();
            foreach (SessionInfo info in sessions)
            {
                if (info.IsSystemSounds || string.IsNullOrEmpty(info.ProcessName)) continue;
                scene.Volumes[info.ProcessName] = info.Volume;
                scene.Mutes[info.ProcessName] = info.IsMuted;
            }
            _config.Scenes.RemoveAll(delegate (Scene s) { return s.Name == name; });
            _config.Scenes.Add(scene);
            _config.Save();
            StatusText.Text = "已保存场景：" + name;
        }

        private void ApplyScene(Scene scene)
        {
            if (!string.IsNullOrEmpty(scene.DeviceId)) Audio.SetDefaultDevice(scene.DeviceId);
            List<SessionInfo> sessions = Audio.GetSessions();
            foreach (SessionInfo info in sessions)
            {
                if (info.IsSystemSounds || string.IsNullOrEmpty(info.ProcessName)) continue;
                float volume;
                bool muted;
                bool hasVolume = scene.Volumes.TryGetValue(info.ProcessName, out volume);
                bool hasMute = scene.Mutes.TryGetValue(info.ProcessName, out muted);
                if (!hasVolume && !hasMute) continue;
                try
                {
                    Guid g = EmptyGuid;
                    if (hasVolume) info.Control.SetMasterVolume(volume, ref g);
                    if (hasMute) info.Control.SetMute(muted ? 1 : 0, ref g);
                }
                catch { }
            }
            RefreshAll(true);
        }
    }
}
