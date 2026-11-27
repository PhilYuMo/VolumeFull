using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using H.NotifyIcon;
using VolumeCtrlLite;
using Microsoft.Win32;

namespace VolumeCtrlWpf
{
    internal class AppRowUi
    {
        public string Id;
        public string ProcessName;
        public string DisplayName;
        public int ProcessId;
        public Grid Row;
        public TextBlock Name;
        public Slider Volume;
        public TextBlock VolumeText;
        public Button MuteButton;
        public Rectangle PeakBar;
        public bool Muted;

        // 同一程序可能有多个音频会话（例如网易云音乐的多进程会话），合并成一行时
        // 这里保存该程序在系统里的全部会话，音量/静音/峰值都同时作用于它们。
        public readonly List<int> ProcessIds = new List<int>();
        public readonly List<ISimpleAudioVolume> Controls = new List<ISimpleAudioVolume>();
        public readonly List<IChannelAudioVolume> ChannelsList = new List<IChannelAudioVolume>();
        public readonly List<IAudioMeterInformation> Meters = new List<IAudioMeterInformation>();

        public ISimpleAudioVolume Control { get { return Controls.Count > 0 ? Controls[0] : null; } }
        public IChannelAudioVolume Channels { get { return ChannelsList.Count > 0 ? ChannelsList[0] : null; } }
        public IAudioMeterInformation Meter { get { return Meters.Count > 0 ? Meters[0] : null; } }

        // 展开行：输出设备选择器 + 音量锁定（参照同类音量工具的 AppVolumeView）
        public bool Expanded;
        public Border SettingsPanel;
        public TextBlock ChevronText;
        public Button DeviceButton;
        public TextBlock DeviceButtonText;
        public string DeviceId;
        public Button InputButton;
        public TextBlock InputButtonText;
        public string InputDeviceId;
        public Button LockOffButton;
        public Button LockUntilButton;
        public Button LockAlwaysButton;
        public int LockMode;
        public Border LockBadge;
        public TextBlock LockBadgeIcon;
        public TextBlock LockBadgeText;
    }

    internal class DeviceCardUi
    {
        public string DeviceId;
        public DeviceInfo Info;
        public Border Card;
        public TextBlock Icon;
        public TextBlock Name;
        public TextBlock Subtitle;
        public Border Badge;
        public Slider Volume;
        public TextBlock VolumeText;
        public Button MuteButton;
        public StackPanel Apps;
        public Rectangle PeakBar;
        public IAudioEndpointVolume Endpoint;
        public IAudioMeterInformation Meter;
        public bool Expanded = true;
        public readonly Dictionary<string, AppRowUi> Rows = new Dictionary<string, AppRowUi>();
    }

    public partial class MainWindow : Window
    {
        private Config _config;
        private DispatcherTimer _timer;
        private TaskbarIcon _tray;
        private IntPtr _hwnd;
        private System.Windows.Interop.HwndSource _hwndSource;
        private bool _restoring;
        private bool _allCollapsed;
        private bool _exiting;
        private bool _suppressDeactivate;
        private bool _peakLogged;
        private AppRowUi _selectedRow;
        private DeviceCardUi _selectedCard;
        private DateTime _lastPeakLog;
        private DateTime _lastSave;
        private DateTime _lastLockSweep;
        private bool _closingAnimationStarted;
        private readonly Dictionary<string, DeviceCardUi> _cards = new Dictionary<string, DeviceCardUi>();
        private List<DeviceInfo> _devices = new List<DeviceInfo>();
        private List<DeviceInfo> _inputDevices = new List<DeviceInfo>();
        private readonly HashSet<string> _restoredSessions = new HashSet<string>();
        private readonly Dictionary<int, ImageSource> _iconCache = new Dictionary<int, ImageSource>();
        private readonly SolidColorBrush _accent = new SolidColorBrush(Color.FromRgb(0, 95, 184));
        private readonly SolidColorBrush _gray = new SolidColorBrush(Color.FromRgb(120, 120, 120));
        private readonly SolidColorBrush _green = new SolidColorBrush(Color.FromRgb(46, 139, 87));
        private readonly SolidColorBrush _cardBorder = new SolidColorBrush(Color.FromRgb(228, 228, 228));
        private const int HotkeyIdVolumeUp = 1;
        private const int HotkeyIdVolumeDown = 2;
        private const int HotkeyIdMute = 3;
        private const int HotkeyIdPanel = 4;
        private const int HotkeyIdNextDevice = 5;
        private static readonly Guid EmptyGuid = Guid.Empty;

        public MainWindow()
        {
            _config = Config.Load();
            _config.DarkTheme = ResolveDark(_config.ThemeMode);
            InitializeComponent();
            try { Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/app.ico")); } catch { }
            Topmost = _config.TopMost;
            TopmostButton.IsChecked = _config.TopMost;
            if (_config.WindowWidth > 200 && _config.WindowHeight > 300)
            {
                Width = _config.WindowWidth;
                Height = _config.WindowHeight;
            }
            PreviewMouseWheel += OnWindowWheel;
            PreviewMouseLeftButtonDown += OnPopupDrag;
            Deactivated += OnMainDeactivated;
            LocationChanged += delegate { SaveWindowPlacement(); };
            Loaded += OnLoaded;
            // 开场动画：先把内容藏起来，避免窗口闪现一帧再淡入
            if (SystemParameters.ClientAreaAnimation) RootGrid.Opacity = 0;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyPopupMode();
                ApplyTheme();
                SetupTray();
                RegisterHotkeys();
                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromSeconds(1);
                _timer.Tick += delegate { try { RefreshAll(false); } catch (Exception ex) { LogError(ex); } };
                _timer.Start();
                PlayShowAnimation();
            }
            catch (Exception ex) { LogError(ex); }
        }

        /// <summary>
        /// 窗口/面板出现的过渡（Win11 观感：轻微放大 + 淡入）。无边框的弹出模式没有 DWM 的开场动画，
        /// 靠这个补上；系统关闭了「动画效果」时直接显示。
        /// </summary>
        private void PlayShowAnimation()
        {
            if (RootGrid == null) return;
            RootGrid.BeginAnimation(UIElement.OpacityProperty, null);
            if (!SystemParameters.ClientAreaAnimation)
            {
                RootGrid.Opacity = 1;
                RootGrid.RenderTransform = Transform.Identity;
                return;
            }

            CubicEase ease = new CubicEase();
            ease.EasingMode = EasingMode.EaseOut;

            RootGrid.Opacity = 0;
            DoubleAnimation fade = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(150)));
            fade.EasingFunction = ease;
            fade.Completed += delegate
            {
                RootGrid.BeginAnimation(UIElement.OpacityProperty, null);
                RootGrid.Opacity = 1;
            };
            RootGrid.BeginAnimation(UIElement.OpacityProperty, fade);

            ScaleTransform scale = new ScaleTransform(0.96, 0.96);
            RootGrid.RenderTransform = scale;
            RootGrid.RenderTransformOrigin = new Point(0.5, 0.5);
            Duration sd = new Duration(TimeSpan.FromMilliseconds(190));
            DoubleAnimation sx = new DoubleAnimation(0.96, 1.0, sd);
            sx.EasingFunction = ease;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
            DoubleAnimation sy = new DoubleAnimation(0.96, 1.0, sd);
            sy.EasingFunction = ease;
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
        }

        private static void LogError(Exception ex)
        {
            try
            {
                File.AppendAllText(System.IO.Path.Combine(Config.ConfigDir, "error.log"), DateTime.Now.ToString("HH:mm:ss") + " " + ex.ToString() + "\r\n\r\n");
            }
            catch { }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized) Hide();
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            try { SaveWindowPlacement(); }
            catch { }
            if (!_exiting)
            {
                e.Cancel = true;
                try { _config.Save(); } catch { }
                Hide();
                return;
            }
            if (!_closingAnimationStarted && SystemParameters.ClientAreaAnimation && RootGrid != null && IsVisible)
            {
                // 退出时先淡出，再真正关闭
                _closingAnimationStarted = true;
                e.Cancel = true;
                CubicEase ease = new CubicEase();
                ease.EasingMode = EasingMode.EaseIn;
                DoubleAnimation fade = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(140)));
                fade.EasingFunction = ease;
                fade.Completed += delegate
                {
                    _exiting = true;
                    Close();
                };
                RootGrid.BeginAnimation(UIElement.OpacityProperty, fade);
                return;
            }
            _timer.Stop();
            UnregisterHotkeys();
            UninstallKeyboardHook();
            try { if (_hwndSource != null) _hwndSource.RemoveHook(WndProc); } catch { }
            try { if (_tray != null) _tray.Dispose(); } catch { }
            _config.Save();
        }

        private void ExitApp()
        {
            _exiting = true;
            _config.Save();
            Close();
        }

        private void ShowPanel()
        {
            bool wasHidden = !IsVisible;
            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            PlaceWindow();
            Activate();
            if (wasHidden) PlayShowAnimation();
        }

    }

    /// <summary>简单的 ICommand 包装（托盘单击命令用）</summary>
    internal sealed class DelegateCommand : System.Windows.Input.ICommand
    {
        private readonly Action _action;
        public DelegateCommand(Action action) { _action = action; }
        public event EventHandler CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object parameter) { return true; }
        public void Execute(object parameter) { _action(); }
    }
}
