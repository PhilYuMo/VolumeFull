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
        private bool _trayBlocked;
        private DispatcherTimer _trayBlockTimer;

        private void BlockTray()
        {
            _trayBlocked = true;
            if (_trayBlockTimer == null)
            {
                _trayBlockTimer = new DispatcherTimer();
                _trayBlockTimer.Interval = TimeSpan.FromMilliseconds(300);
                _trayBlockTimer.Tick += delegate
                {
                    _trayBlocked = false;
                    _trayBlockTimer.Stop();
                };
            }
            _trayBlockTimer.Stop();
            _trayBlockTimer.Start();
        }

        /// <summary>单击托盘：不可见就显示，可见就隐藏（照同类音量工具 TrayClick/OpenCommand）</summary>
        private void OnTrayClick()
        {
            if (_trayBlocked) return;
            try
            {
                if (IsVisible)
                {
                    SaveWindowPlacement();
                    Hide();
                }
                else
                {
                    ShowPanel();
                }
            }
            catch (Exception ex) { LogError(ex); }
        }

        private void OnPopupDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 参照同类音量工具：两种模式都能拖动窗口（点在非控件区域时），拖动后记录位置
            if (e.ButtonState != System.Windows.Input.MouseButtonState.Pressed) return;
            if (IsInteractiveSource(e.OriginalSource as DependencyObject)) return;
            try
            {
                DragMove();
                SaveWindowPlacement();
            }
            catch { }
        }

        private bool IsInteractiveSource(DependencyObject d)
        {
            while (d != null && d != this)
            {
                if (d is Slider || d is Button || d is System.Windows.Controls.Primitives.ToggleButton || d is TextBox || d is CheckBox
                    || d is ComboBox || d is System.Windows.Controls.Primitives.Thumb
                    || d is System.Windows.Controls.Primitives.ScrollBar) return true;
                d = VisualTreeHelper.GetParent(d);
            }
            return false;
        }

        private void SetupTray()
        {
            try
            {
                _tray = new TaskbarIcon();
                string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, _config.TrayIconKind == 1 ? "app.ico" : "tray-volume.ico");
                if (!File.Exists(iconPath)) iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "app.ico");
                BitmapImage img = new BitmapImage();
                if (File.Exists(iconPath))
                {
                    img.BeginInit();
                    img.UriSource = new Uri(iconPath);
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.EndInit();
                }
                else
                {
                    using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(32, 32))
                    {
                        using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
                        {
                            using (System.Drawing.Icon icon = System.Drawing.SystemIcons.Application)
                            {
                                g.DrawIcon(icon, new System.Drawing.Rectangle(0, 0, 32, 32));
                            }
                        }
                        MemoryStream ms = new MemoryStream();
                        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        ms.Position = 0;
                        img.BeginInit();
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        img.StreamSource = ms;
                        img.EndInit();
                    }
                }
                img.Freeze();
                _tray.IconSource = img;
                _tray.ToolTipText = "VolumeFull";
                _tray.ContextMenu = BuildTrayMenu();
                // 照同类音量工具：单击托盘即切换显示（NoLeftClickDelay，不等双击判定）
                _tray.NoLeftClickDelay = true;
                _tray.LeftClickCommand = new DelegateCommand(OnTrayClick);
                _tray.ForceCreate();
                LogTray("WPF TaskbarIcon created");
            }
            catch (Exception ex)
            {
                LogTray("tray error: " + ex);
            }
        }

        private static void LogSound(string msg)
        {
            try
            {
                File.AppendAllText(System.IO.Path.Combine(Config.ConfigDir, "sound.log"), DateTime.Now.ToString("HH:mm:ss") + " " + msg + "\r\n");
            }
            catch { }
        }

        private void UpdateTrayIcon()
        {
            try
            {
                if (_tray == null) return;
                string iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, _config.TrayIconKind == 1 ? "app.ico" : "tray-volume.ico");
                if (!System.IO.File.Exists(iconPath)) iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "app.ico");
                BitmapImage img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(iconPath);
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                _tray.IconSource = img;
            }
            catch { }
        }

        private static void LogTray(string msg)
        {
            try
            {
                File.AppendAllText(System.IO.Path.Combine(Config.ConfigDir, "tray.log"), DateTime.Now.ToString("HH:mm:ss") + " " + msg + "\r\n");
            }
            catch { }
        }

        private bool IsDefaultMuted()
        {
            try
            {
                string deviceId = Audio.GetDefaultOutputDeviceId();
                IAudioEndpointVolume ep = Audio.GetEndpointVolumeForDevice(deviceId);
                if (ep == null) return false;
                try { return Audio.GetEndpointMute(ep); }
                finally { try { Marshal.ReleaseComObject(ep); } catch { } }
            }
            catch { return false; }
        }

        private static TextBlock MenuIcon(string glyph)
        {
            TextBlock tb = new TextBlock();
            tb.Text = glyph;
            tb.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            tb.FontSize = 14;
            tb.SetResourceReference(TextBlock.ForegroundProperty, "MenuText");
            return tb;
        }

        private ContextMenu BuildTrayMenu()
        {
            ContextMenu menu = new ContextMenu();
            menu.MinWidth = 220;

            MenuItem show = new MenuItem();
            show.Header = "打开合成器";
            show.FontWeight = FontWeights.SemiBold;
            show.Icon = MenuIcon("\uE9D5");
            show.Click += delegate { ShowPanel(); };
            menu.Items.Add(show);

            MenuItem settings = new MenuItem();
            settings.Header = "设置";
            settings.Icon = MenuIcon("\uE713");
            settings.Click += delegate
            {
                if (!IsVisible) ShowPanel();
                OpenSettings();
            };
            menu.Items.Add(settings);

            bool muted = IsDefaultMuted();
            MenuItem mute = new MenuItem();
            mute.Header = muted ? "取消静音" : "静音";
            mute.Icon = MenuIcon(muted ? "\uE74F" : "\uE995");
            mute.Click += delegate
            {
                ToggleDefaultMute();
                bool nowMuted = IsDefaultMuted();
                try { if (_tray != null) _tray.ToolTipText = nowMuted ? "VolumeCtrl（已静音）" : "VolumeCtrl"; } catch { }
                try { _tray.ContextMenu = BuildTrayMenu(); } catch { }
            };
            menu.Items.Add(mute);

            MenuItem devicesItem = new MenuItem();
            devicesItem.Header = "输出设备";
            devicesItem.Icon = MenuIcon("\uE7F6");
            try
            {
                string defaultId = Audio.GetDefaultOutputDeviceId();
                foreach (DeviceInfo d in Audio.GetOutputDevices())
                {
                    DeviceInfo captured = d;
                    MenuItem item = new MenuItem();
                    item.Header = d.Name;
                    item.Icon = MenuIcon(d.Id == defaultId ? "\uE73E" : "\uE995");
                    item.Click += delegate
                    {
                        try
                        {
                            Audio.SetDefaultDevice(captured.Id);
                            RefreshAll(true);
                            StatusText.Text = "默认输出设备：" + captured.Name;
                        }
                        catch { }
                    };
                    devicesItem.Items.Add(item);
                }
            }
            catch { }
            menu.Items.Add(devicesItem);

            MenuItem inputsItem = new MenuItem();
            inputsItem.Header = "输入设备";
            inputsItem.Icon = MenuIcon("\uE720");
            try
            {
                string defaultInputId = Audio.GetDefaultInputDeviceId();
                List<DeviceInfo> inputs = Audio.GetInputDevices();
                if (inputs.Count == 0)
                {
                    MenuItem none = new MenuItem();
                    none.Header = "没有可用的录音设备";
                    none.IsEnabled = false;
                    inputsItem.Items.Add(none);
                }
                foreach (DeviceInfo d in inputs)
                {
                    DeviceInfo captured = d;
                    MenuItem item = new MenuItem();
                    item.Header = d.Name;
                    item.Icon = MenuIcon(d.Id == defaultInputId ? "\uE73E" : "\uE720");
                    item.Click += delegate
                    {
                        try
                        {
                            Audio.SetDefaultDevice(captured.Id);
                            RefreshAll(true);
                            StatusText.Text = "默认输入设备：" + captured.Name;
                        }
                        catch { }
                    };
                    inputsItem.Items.Add(item);
                }
            }
            catch { }
            menu.Items.Add(inputsItem);

            menu.Items.Add(new Separator());

            MenuItem soundPanel = new MenuItem();
            soundPanel.Header = "声音控制面板";
            soundPanel.Icon = MenuIcon("\uE995");
            soundPanel.Click += delegate
            {
                LogSound("click soundPanel");
                try
                {
                    System.Diagnostics.Process.Start("rundll32.exe", "shell32.dll,Control_RunDLL mmsys.cpl,,0");
                    LogSound("started mmsys.cpl");
                }
                catch (Exception ex) { LogSound("error: " + ex); }
            };
            menu.Items.Add(soundPanel);

            MenuItem soundSettings = new MenuItem();
            soundSettings.Header = "Windows 声音设置";
            soundSettings.Icon = MenuIcon("\uE8A7");
            soundSettings.Click += delegate
            {
                LogSound("click soundSettings");
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:sound") { UseShellExecute = true });
                    System.Threading.Tasks.Task.Run(async delegate
                    {
                        for (int attempt = 0; attempt < 8; attempt++)
                        {
                            await System.Threading.Tasks.Task.Delay(700);
                            try
                            {
                                IntPtr hwnd = FindWindow(null, "设置");
                                if (hwnd == IntPtr.Zero) hwnd = FindWindow(null, "Settings");
                                if (hwnd != IntPtr.Zero)
                                {
                                    SetWindowPos(hwnd, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002);
                                    SetForegroundWindow(hwnd);
                                    LogSound("activated settings window attempt=" + attempt);
                                }
                            }
                            catch { }
                        }
                    });
                    LogSound("started ms-settings:sound");
                }
                catch (Exception ex) { LogSound("error: " + ex); }
            };
            menu.Items.Add(soundSettings);

            menu.Items.Add(new Separator());

            MenuItem exit = new MenuItem();
            exit.Header = "退出";
            exit.Icon = MenuIcon("\uE7E8");
            exit.Click += delegate { ExitApp(); };
            menu.Items.Add(exit);
            return menu;
        }

    }
}
