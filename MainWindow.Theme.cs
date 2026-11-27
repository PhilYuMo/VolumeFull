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
        private static bool SystemUsesDark()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\\\Microsoft\\\\Windows\\\\CurrentVersion\\\\Themes\\\\Personalize"))
                {
                    if (key != null)
                    {
                        object v = key.GetValue("AppsUseLightTheme");
                        if (v != null) return Convert.ToInt32(v) == 0;
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool ResolveDark(int mode)
        {
            if (mode == 2) return SystemUsesDark();
            return mode == 1;
        }

        private void ApplyTheme()
        {
            bool dark = _config.DarkTheme;
            Background = new SolidColorBrush(dark ? Color.FromRgb(0x23, 0x23, 0x23) : Color.FromRgb(0xED, 0xF1, 0xF7));
            try
            {
                IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int flag = dark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, 20, ref flag, sizeof(int));
            }
            catch { }
            try
            {
                Application.Current.Resources["MenuBackground"] = dark ? new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)) : new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFB));
                Application.Current.Resources["MenuText"] = dark ? Brushes.White : Brushes.Black;
                Application.Current.Resources["MenuBorder"] = dark ? new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A)) : new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
                Application.Current.Resources["MenuHover"] = dark ? new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)) : new SolidColorBrush(Color.FromArgb(0x22, 0x00, 0x00, 0x00));
                Application.Current.Resources["SwitchTrackOff"] = dark ? new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)) : new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFB));
                Application.Current.Resources["SwitchTrackOn"] = dark ? new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF)) : new SolidColorBrush(Color.FromRgb(0x00, 0x67, 0xC0));
                Application.Current.Resources["SwitchThumbOff"] = dark ? new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0)) : new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
                Application.Current.Resources["SwitchThumbOn"] = Brushes.White;
                Application.Current.Resources["AccentBar"] = dark ? new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF)) : new SolidColorBrush(Color.FromRgb(0x00, 0x5F, 0xB8));
                Application.Current.Resources["ToggleCheckedBackground"] = dark ? new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)) : Brushes.White;
                Application.Current.Resources["NavAccent"] = dark ? new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF)) : new SolidColorBrush(Color.FromRgb(0x00, 0x5F, 0xB8));
                Application.Current.Resources["NavActiveBackground"] = dark ? new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)) : Brushes.White;
                Application.Current.Resources["NavActiveBorder"] = dark ? new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)) : new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xE5));
                Application.Current.Resources["SegmentBorder"] = dark ? new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A)) : new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
                Application.Current.Resources["SegmentActiveBackground"] = dark ? new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF)) : new SolidColorBrush(Color.FromRgb(0x00, 0x5F, 0xB8));
                Application.Current.Resources["SegmentActiveForeground"] = dark ? Brushes.Black : Brushes.White;
                Application.Current.Resources["SliderTrack"] = dark ? new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A)) : new SolidColorBrush(Color.FromRgb(0xE5, 0xE5, 0xE5));
                Application.Current.Resources["SliderFill"] = dark ? new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF)) : new SolidColorBrush(Color.FromRgb(0x00, 0x5F, 0xB8));
                Application.Current.Resources["SliderFillDisabled"] = dark ? new SolidColorBrush(Color.FromRgb(0x5A, 0x5A, 0x5A)) : new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0));
                Application.Current.Resources["SliderTrackDisabled"] = dark ? new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38)) : new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));
            }
            catch { }
            StatusText.Foreground = dark ? new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0)) : Brushes.Gray;
            Brush toolbarBg = dark ? new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)) : new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));
            Brush toolbarBorder = dark ? new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A)) : new SolidColorBrush(Color.FromArgb(0x1A, 0x00, 0x00, 0x00));
            SearchButton.Background = toolbarBg;
            SearchButton.BorderBrush = toolbarBorder;
            CollapseButton.Background = toolbarBg;
            CollapseButton.BorderBrush = toolbarBorder;
            RefreshButton.Background = toolbarBg;
            RefreshButton.BorderBrush = toolbarBorder;
            SettingsButton.Background = toolbarBg;
            SettingsButton.BorderBrush = toolbarBorder;
            TopmostButton.Background = toolbarBg;
            TopmostButton.BorderBrush = toolbarBorder;
            Brush toolbarFg = dark ? Brushes.White : Brushes.Black;
            SearchButton.Foreground = toolbarFg;
            CollapseButton.Foreground = toolbarFg;
            RefreshButton.Foreground = toolbarFg;
            SettingsButton.Foreground = toolbarFg;
            TopmostButton.Foreground = toolbarFg;
            SwitchDeviceButton.Background = toolbarBg;
            SwitchDeviceButton.BorderBrush = toolbarBorder;
            SwitchDeviceButton.Foreground = toolbarFg;
            SwitchInputButton.Background = toolbarBg;
            SwitchInputButton.BorderBrush = toolbarBorder;
            SwitchInputButton.Foreground = toolbarFg;
            // 托盘菜单（H.NotifyIcon 的 ContextMenu）不会跟着主题资源刷新，切主题后重建一份
            try
            {
                if (_tray != null) _tray.ContextMenu = BuildTrayMenu();
            }
            catch { }
            foreach (DeviceCardUi card in _cards.Values)
            {
                DevicesPanel.Children.Remove(card.Card);
            }
            _cards.Clear();
            RefreshAll(true);
        }

        private static void SetTextColor(DependencyObject root, Brush brush)
        {
            try
            {
                // 按钮内部控制自己的前景色（锁定三段 / 图标按钮），不要被卡片统一着色覆盖
                if (root is Button) return;
                TextBlock tb = root as TextBlock;
                if (tb != null) { tb.Foreground = brush; return; }
                foreach (object child in LogicalTreeHelper.GetChildren(root))
                {
                    DependencyObject d = child as DependencyObject;
                    if (d != null) SetTextColor(d, brush);
                }
            }
            catch { }
        }
    }
}
