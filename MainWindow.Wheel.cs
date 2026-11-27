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
        private void OnWindowWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            LogWheel("wheel delta=" + e.Delta + " src=" + (e.OriginalSource == null ? "null" : e.OriginalSource.GetType().Name) + " enabled=" + _config.EnableWheel);
            if (!_config.EnableWheel) return;
            int step = Math.Max(1, Math.Min(100, _config.VolumeStep));
            int delta = e.Delta > 0 ? step : -step;
            DependencyObject d = e.OriginalSource as DependencyObject;
            while (d != null && d != this)
            {
                Slider slider = d as Slider;
                if (slider != null)
                {
                    slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, slider.Value + delta));
                    LogWheel("slider found newValue=" + slider.Value);
                    e.Handled = true;
                    return;
                }

                d = VisualTreeHelper.GetParent(d);
            }
            LogWheel("no slider (scroll)");
            // 非滑块区域：让 ScrollViewer 正常滚动列表
        }

        private static void LogWheel(string msg)
        {
            try
            {
                File.AppendAllText(System.IO.Path.Combine(Config.ConfigDir, "wheel.log"), DateTime.Now.ToString("HH:mm:ss") + " " + msg + "\r\n");
            }
            catch { }
        }

    }
}
