using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VolumeCtrlLite;

namespace VolumeCtrlWpf
{
    public partial class BalanceWindow : Window
    {
        private readonly IChannelAudioVolume _channels;
        private readonly uint _count;
        private readonly bool _dark;
        public float LeftVolume { get; private set; }
        public float RightVolume { get; private set; }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        internal BalanceWindow(string appName, IChannelAudioVolume channels, uint count, float left, float right, bool dark)
        {
            InitializeComponent();
            _channels = channels;
            _count = count;
            _dark = dark;
            AppName.Text = appName;
            LeftSlider.Value = Math.Round(left * 100);
            RightSlider.Value = Math.Round(right * 100);
            LeftVolume = left;
            RightVolume = right;
            ApplyTheme();
            SourceInitialized += OnSourceInitialized;
        }

        private void ApplyTheme()
        {
            if (!_dark) return;
            Background = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
            Foreground = Brushes.White;
            AppName.Foreground = Brushes.White;
            LeftLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0));
            RightLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0));
            HintText.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            try
            {
                IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int flag = _dark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, 20, ref flag, sizeof(int));
            }
            catch { }
        }

        private void OnCenter(object sender, RoutedEventArgs e)
        {
            LeftSlider.Value = 100;
            RightSlider.Value = 100;
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            LeftVolume = (float)(LeftSlider.Value / 100.0);
            RightVolume = (float)(RightSlider.Value / 100.0);
            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
