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
        // ===== 窗口位置（物理像素，参照同类音量工具的 MainView/SetWindowPosition） =====
        private const double BorderIndent = 11;
        private bool _applyingPlacement;
        private bool _placed;

        private enum TaskBarLocation { TOP, BOTTOM, LEFT, RIGHT }

        private TaskBarLocation GetTaskBarLocation()
        {
            if (SystemParameters.WorkArea.Left > 0) return TaskBarLocation.LEFT;
            if (SystemParameters.WorkArea.Top > 0) return TaskBarLocation.TOP;
            if (SystemParameters.WorkArea.Left == 0 && SystemParameters.WorkArea.Width < SystemParameters.PrimaryScreenWidth) return TaskBarLocation.RIGHT;
            return TaskBarLocation.BOTTOM;
        }

        private double GetScaleFactor()
        {
            try
            {
                System.Windows.Interop.HwndSource source = System.Windows.Interop.HwndSource.FromVisual(this) as System.Windows.Interop.HwndSource;
                if (source != null && source.CompositionTarget != null) return source.CompositionTarget.TransformToDevice.M11;
            }
            catch { }
            return 1.0;
        }

        /// <summary>托盘旁定位：按任务栏方位放在窗口所在屏幕工作区的角落（物理像素）</summary>
        private void SetWindowPosition()
        {
            if (_hwnd == IntPtr.Zero) return;
            IntPtr monitor = MonitorFromWindow(_hwnd, 2 /*MONITOR_DEFAULTTONEAREST*/);
            MONITORINFO mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (!GetMonitorInfo(monitor, ref mi)) return;
            RECT work = mi.rcWork;
            double scale = GetScaleFactor();
            int w = (int)Math.Round(Width * scale);
            int h = (int)Math.Round(Height * scale);
            int indent = (int)BorderIndent;
            int x, y;
            switch (GetTaskBarLocation())
            {
                case TaskBarLocation.TOP:
                    x = work.right - w - indent;
                    y = work.top + indent;
                    break;
                case TaskBarLocation.LEFT:
                    x = work.left + indent;
                    y = work.bottom - h - indent;
                    break;
                default: // BOTTOM / RIGHT
                    x = work.right - w - indent;
                    y = work.bottom - h - indent;
                    break;
            }
            if (y < 0) y = 0;
            MoveTo(x, y);
        }

        private void MoveTo(int x, int y)
        {
            if (_hwnd == IntPtr.Zero) return;
            SetWindowPos(_hwnd, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0040 /*SWP_NOSIZE|SWP_SHOWWINDOW*/);
        }

        /// <summary>记住的位置有效（开关开启、有值、落在某块屏幕上）才返回 true</summary>
        private bool TryGetRememberedPosition(double left, double top, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (!_config.RememberFlyoutPosition || double.IsNaN(left) || double.IsNaN(top)) return false;
            x = (int)Math.Round(left);
            y = (int)Math.Round(top);
            POINT p;
            p.X = x;
            p.Y = y;
            return MonitorFromPoint(p, 0 /*MONITOR_DEFAULTTONULL*/) != IntPtr.Zero;
        }

        /// <summary>显示前定位：有记住的位置就用，否则放在托盘旁（两种模式规则相同）</summary>
        private void PlaceWindow()
        {
            _applyingPlacement = true;
            try
            {
                int x, y;
                if (_config.PopupMode)
                {
                    if (TryGetRememberedPosition(_config.PopupLeft, _config.PopupTop, out x, out y)) MoveTo(x, y);
                    else SetWindowPosition();
                }
                else
                {
                    if (TryGetRememberedPosition(_config.WindowLeft, _config.WindowTop, out x, out y)) MoveTo(x, y);
                    else SetWindowPosition();
                }
            }
            catch (Exception ex) { LogError(ex); }
            finally
            {
                _applyingPlacement = false;
                _placed = true;
            }
        }

        /// <summary>记录当前位置（物理像素）；拖动和位置变化时写内存，退出时落盘</summary>
        private void SaveWindowPlacement()
        {
            if (_applyingPlacement || !_placed || _hwnd == IntPtr.Zero || !IsVisible) return;
            RECT r;
            if (!GetWindowRect(_hwnd, out r)) return;
            if (_config.PopupMode)
            {
                _config.PopupLeft = r.left;
                _config.PopupTop = r.top;
            }
            else if (WindowState == WindowState.Normal)
            {
                _config.WindowLeft = r.left;
                _config.WindowTop = r.top;
                _config.WindowWidth = Width;
                _config.WindowHeight = Height;
            }
        }

        // ===== 弹出模式 =====
        private void ApplyPopupMode()
        {
            try
            {
                bool popup = _config.PopupMode;
                if (popup)
                {
                    WindowStyle = WindowStyle.None;
                    ResizeMode = ResizeMode.NoResize;
                    ShowInTaskbar = false;
                    Topmost = true;
                    TopmostButton.IsChecked = true;
                }
                else
                {
                    WindowStyle = WindowStyle.SingleBorderWindow;
                    ResizeMode = ResizeMode.CanResize;
                    ShowInTaskbar = true;
                    Topmost = _config.TopMost;
                    TopmostButton.IsChecked = _config.TopMost;
                }

                // WindowStyle/ShowInTaskbar 变更会重建 HWND（异步），延迟到句柄稳定后再挂钩子、置顶并定位
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    StabilizeWindow();
                    if (IsVisible) PlaceWindow();
                }), DispatcherPriority.Loaded);
                _config.Save();
            }
            catch (Exception ex) { LogError(ex); }
        }

        private void StabilizeWindow()
        {
            try
            {
                IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                if (hwnd != _hwnd)
                {
                    if (_hwndSource != null) { try { _hwndSource.RemoveHook(WndProc); } catch { } }
                    _hwnd = hwnd;
                    _hwndSource = System.Windows.Interop.HwndSource.FromHwnd(hwnd);
                    if (_hwndSource != null) _hwndSource.AddHook(WndProc);
                    RegisterHotkeys();
                }
                bool top = _config.PopupMode || _config.TopMost;
                SetWindowPos(hwnd, top ? new IntPtr(-1) : new IntPtr(-2), 0, 0, 0, 0, 0x0001 | 0x0002);
                // 弹出模式是无边框窗口，Windows 11 不会自动圆角：设置 DWMWA_WINDOW_CORNER_PREFERENCE
                try
                {
                    int corner = _config.PopupMode ? 2 /*DWMWCP_ROUND*/ : 0 /*DWMWCP_DEFAULT*/;
                    DwmSetWindowAttribute(hwnd, 33, ref corner, sizeof(int));
                }
                catch { }
            }
            catch (Exception ex) { LogError(ex); }
        }

        private void OnMainDeactivated(object sender, EventArgs e)
        {
            if (!_config.PopupMode || _suppressDeactivate) return;
            // 弹出模式下点击托盘会先失焦隐藏，300ms 内不再响应托盘单击（同类音量工具的 trayBlocked）
            BlockTray();
            SaveWindowPlacement();
            Hide();
        }

    }
}
