using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace VolumeCtrlWpf
{
    public partial class SettingsWindow : Window
    {
        private readonly Config _config;
        private readonly CheckBox _startup;
        private readonly CheckBox _remember;
        private readonly CheckBox _hotkeys;
        private readonly CheckBox _exclusiveHotkeys;
        private readonly CheckBox _wheel;
        private readonly ComboBox _themeCombo;
        private readonly ComboBox _trayCombo;
        private readonly TextBox[] _hotkeyBoxes;
        private readonly CheckBox _popupMode;
        private readonly CheckBox _rememberFlyout;
        private readonly CheckBox _limitStartup;
        private readonly TextBox _maxVolumeBox;
        private readonly TextBox _stepBox;
        private readonly List<Border> _cards = new List<Border>();

        internal SettingsWindow(Config config)
        {
            _config = config;
            InitializeComponent();

            // ===== 常规 =====
            _startup = CreateToggle(_config.Startup);
            AddCard(PageGeneral, "\uE7E8", "开机自动启动", "登录 Windows 时自动运行", _startup);

            _themeCombo = CreateCombo(new string[] { "浅色", "深色", "跟随系统" }, Math.Min(2, Math.Max(0, _config.ThemeMode)));
            AddCard(PageGeneral, "\uE790", "主题", "窗口和控件的配色", _themeCombo);

            _trayCombo = CreateCombo(new string[] { "音量图标", "应用图标" }, Math.Min(1, Math.Max(0, _config.TrayIconKind)));
            AddCard(PageGeneral, "\uE7F4", "托盘图标", "通知区域显示哪个图标", _trayCombo);

            _popupMode = CreateToggle(_config.PopupMode);
            AddCard(PageGeneral, "\uE8A7", "弹出模式", "在托盘上方打开，点击别处时隐藏。关闭时为普通窗口。", _popupMode);

            _rememberFlyout = CreateToggle(_config.RememberFlyoutPosition);
            AddCard(PageGeneral, "\uE81D", "记住合成器的位置", "在上次离开的位置打开。关闭时在托盘旁打开。", _rememberFlyout);
            _popupMode.Click += delegate { SaveFlags(); };

            // ===== 应用音量 =====
            _remember = CreateToggle(_config.RememberVolumes);
            AddCard(PageApps, "\uE81C", "记住每个应用的音量", "以记忆音量启动", _remember);

            _maxVolumeBox = new TextBox();
            _maxVolumeBox.Text = _config.MaxStartupVolume.ToString();
            _maxVolumeBox.Width = 64;
            _maxVolumeBox.Height = 30;
            _maxVolumeBox.VerticalContentAlignment = VerticalAlignment.Center;
            _maxVolumeBox.TextAlignment = TextAlignment.Center;
            _maxVolumeBox.Margin = new Thickness(0, 0, 6, 0);
            _maxVolumeBox.TextChanged += delegate { SaveFlags(); };
            TextBlock pct = new TextBlock();
            pct.Text = "%";
            pct.FontSize = 12;
            pct.VerticalAlignment = VerticalAlignment.Center;
            pct.Margin = new Thickness(0, 0, 10, 0);
            StackPanel volumeExtra = new StackPanel();
            volumeExtra.Orientation = Orientation.Horizontal;
            volumeExtra.Children.Add(_maxVolumeBox);
            volumeExtra.Children.Add(pct);
            _limitStartup = CreateToggle(_config.LimitStartupVolume);
            volumeExtra.Children.Add(_limitStartup);
            AddCard(PageApps, "\uE767", "降低以最大音量启动的应用", "改为以此音量启动", volumeExtra);

            _stepBox = new TextBox();
            _stepBox.Text = Math.Max(1, Math.Min(100, _config.VolumeStep)).ToString();
            _stepBox.Width = 64;
            _stepBox.Height = 30;
            _stepBox.VerticalContentAlignment = VerticalAlignment.Center;
            _stepBox.TextAlignment = TextAlignment.Center;
            _stepBox.Margin = new Thickness(0, 0, 6, 0);
            _stepBox.TextChanged += delegate { SaveFlags(); };
            TextBlock stepPct = new TextBlock();
            stepPct.Text = "%";
            stepPct.FontSize = 12;
            stepPct.VerticalAlignment = VerticalAlignment.Center;
            stepPct.Margin = new Thickness(0, 0, 10, 0);
            _wheel = CreateToggle(_config.EnableWheel);
            StackPanel wheelExtra = new StackPanel();
            wheelExtra.Orientation = Orientation.Horizontal;
            wheelExtra.Children.Add(_stepBox);
            wheelExtra.Children.Add(stepPct);
            wheelExtra.Children.Add(_wheel);
            AddCard(PageApps, "\uE962", "启用滚轮调节音量", "步长（滚轮与快捷键共用）", wheelExtra);

            // ===== 快捷键 =====
            _hotkeys = CreateToggle(_config.EnableHotkeys);
            AddCard(PageHotkeys, "\uE765", "启用全局快捷键", "关闭后所有热键不响应", _hotkeys);

            _exclusiveHotkeys = CreateToggle(_config.ExclusiveHotkeys);
            AddCard(PageHotkeys, "\uE72E", "独占快捷键",
                "勾选后其他程序收不到这些组合（例如网易云音乐也用 Ctrl+Alt+Up 时，只调系统音量）", _exclusiveHotkeys);

            _hotkeyBoxes = new TextBox[5];
            TextBlock[] hotkeyHints = new TextBlock[5];
            string[] names = new string[] { "音量增加", "音量减小", "静音切换", "显示面板", "切换输出设备" };
            string[] glyphs = new string[] { "\uE767", "\uE767", "\uE74F", "\uE8A7", "\uE772" };
            string[] values = new string[]
            {
                _config.HotkeyVolumeUp, _config.HotkeyVolumeDown, _config.HotkeyMute,
                _config.HotkeyPanel, _config.HotkeyNextDevice
            };
            for (int i = 0; i < names.Length; i++)
            {
                TextBox box = new TextBox();
                box.Text = values[i];
                box.Width = 168;
                box.Height = 30;
                box.VerticalContentAlignment = VerticalAlignment.Center;
                box.TextAlignment = TextAlignment.Center;
                _hotkeyBoxes[i] = box;
                TextBlock hint;
                AddCardWithHint(PageHotkeys, glyphs[i], names[i], "格式：Ctrl+Alt+Up，留空则不注册", box, out hint);
                hotkeyHints[i] = hint;
                TextBox capturedBox = box;
                TextBlock capturedHint = hint;
                box.TextChanged += delegate { UpdateHotkeyHint(capturedBox, capturedHint); };
                UpdateHotkeyHint(box, hint);
            }

            // ===== 关于 =====
            TextBlock versionText = new TextBlock();
            versionText.Text = GetVersionText();
            versionText.FontSize = 12;
            versionText.VerticalAlignment = VerticalAlignment.Center;
            versionText.Tag = "muted";
            AddCard(PageAbout, "\uE946", "VolumeFull", "音量控制工具（Windows 11 风格）", versionText);

            AddLinkCard(PageAbout, "\uE943", "源代码", "github.com/PhilYuMo/VolumeFull", "https://github.com/PhilYuMo/VolumeFull");
            AddLinkCard(PageAbout, "\uE7BA", "报告问题或建议功能", "GitHub Issues", "https://github.com/PhilYuMo/VolumeFull/issues");

            ApplyThemeColors();
            SetPage(PageGeneral);
            SourceInitialized += OnSourceInitialized;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            try
            {
                IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int flag = _config.DarkTheme ? 1 : 0;
                DwmSetWindowAttribute(hwnd, 20, ref flag, sizeof(int));
            }
            catch { }
        }

        // ===== 页面切换 =====
        private void OnNavClick(object sender, RoutedEventArgs e)
        {
            if (sender == NavGeneral) SetPage(PageGeneral);
            else if (sender == NavApps) SetPage(PageApps);
            else if (sender == NavAbout) SetPage(PageAbout);
            else SetPage(PageHotkeys);
        }

        private void SetPage(StackPanel page)
        {
            NavGeneral.IsChecked = page == PageGeneral;
            NavApps.IsChecked = page == PageApps;
            NavHotkeys.IsChecked = page == PageHotkeys;
            NavAbout.IsChecked = page == PageAbout;
            PageGeneral.Visibility = page == PageGeneral ? Visibility.Visible : Visibility.Collapsed;
            PageApps.Visibility = page == PageApps ? Visibility.Visible : Visibility.Collapsed;
            PageHotkeys.Visibility = page == PageHotkeys ? Visibility.Visible : Visibility.Collapsed;
            PageAbout.Visibility = page == PageAbout ? Visibility.Visible : Visibility.Collapsed;

            // 页面切换的淡入过渡（Win11 观感）
            if (SystemParameters.ClientAreaAnimation && page != null)
            {
                page.BeginAnimation(UIElement.OpacityProperty, null);
                page.Opacity = 0;
                CubicEase ease = new CubicEase();
                ease.EasingMode = EasingMode.EaseOut;
                DoubleAnimation fade = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(130)));
                fade.EasingFunction = ease;
                page.BeginAnimation(UIElement.OpacityProperty, fade);
            }
            ApplyNavColors();
        }

        /// <summary>「关于」页的链接卡片：整张卡片可点，右侧箭头（照同类音量工具的链接分区）。</summary>
        private void AddLinkCard(StackPanel page, string glyph, string title, string subtitle, string url)
        {
            TextBlock arrow = new TextBlock();
            arrow.Text = "\uE76C";
            arrow.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            arrow.FontSize = 12;
            arrow.VerticalAlignment = VerticalAlignment.Center;
            arrow.Margin = new Thickness(8, 0, 2, 0);
            arrow.Tag = "muted";

            Border card = CreateCard(glyph, title, subtitle, arrow);
            card.Cursor = System.Windows.Input.Cursors.Hand;
            card.MouseLeftButtonUp += delegate { OpenUrl(url); };
            page.Children.Add(card);
        }

        private static void OpenUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }

        private static string GetVersionText()
        {
            try
            {
                Version v = typeof(SettingsWindow).Assembly.GetName().Version;
                if (v == null) return "";
                return "v" + v.ToString(3);
            }
            catch { return ""; }
        }

        // ===== 卡片 =====
        private void AddCard(StackPanel page, string glyph, string title, string subtitle, FrameworkElement right)
        {
            page.Children.Add(CreateCard(glyph, title, subtitle, right));
        }

        private void AddCardWithHint(StackPanel page, string glyph, string title, string subtitle, FrameworkElement right, out TextBlock hint)
        {
            page.Children.Add(CreateCard(glyph, title, subtitle, right, out hint));
        }

        /// <summary>热键输入框下方的实时提示：格式错误 / 已被其他程序占用。</summary>
        private void UpdateHotkeyHint(TextBox box, TextBlock hint)
        {
            if (hint == null || box == null) return;
            string text = box.Text == null ? "" : box.Text.Trim();
            uint mods, vk;
            if (text.Length == 0)
            {
                hint.Visibility = Visibility.Collapsed;
                return;
            }
            if (!HotKeyHelper.TryParse(text, out mods, out vk))
            {
                hint.Text = "格式无效：应为 Ctrl+Alt+Up 这类组合（至少一个修饰键）";
                hint.Foreground = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));
                hint.Visibility = Visibility.Visible;
                return;
            }
            if (HotKeyHelper.IsRegisteredElsewhere(mods, vk))
            {
                hint.Text = "该组合已被其他程序占用（按 RegisterHotKey 探测），按下时可能同时触发对方功能";
                hint.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0x70, 0x00));
                hint.Visibility = Visibility.Visible;
                return;
            }
            hint.Visibility = Visibility.Collapsed;
        }

        private Border CreateCard(string glyph, string title, string subtitle, FrameworkElement right)
        {
            TextBlock unused;
            return CreateCard(glyph, title, subtitle, right, out unused);
        }

        private Border CreateCard(string glyph, string title, string subtitle, FrameworkElement right, out TextBlock hint)
        {
            Border card = new Border();
            card.CornerRadius = new CornerRadius(8);
            card.BorderThickness = new Thickness(1);
            card.Margin = new Thickness(0, 0, 0, 8);
            card.Padding = new Thickness(14, 12, 14, 12);
            _cards.Add(card);

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            ColumnDefinition c0 = new ColumnDefinition();
            c0.Width = new GridLength(38);
            ColumnDefinition c1 = new ColumnDefinition();
            c1.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition c2 = new ColumnDefinition();
            c2.Width = GridLength.Auto;
            grid.ColumnDefinitions.Add(c0);
            grid.ColumnDefinitions.Add(c1);
            grid.ColumnDefinitions.Add(c2);

            TextBlock icon = new TextBlock();
            icon.Text = glyph;
            icon.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            icon.FontSize = 18;
            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.HorizontalAlignment = HorizontalAlignment.Left;
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);

            StackPanel text = new StackPanel();
            text.VerticalAlignment = VerticalAlignment.Center;
            text.Margin = new Thickness(0, 0, 10, 0);
            TextBlock t = new TextBlock();
            t.Text = title;
            t.FontSize = 13;
            t.TextWrapping = TextWrapping.Wrap;
            text.Children.Add(t);
            if (!string.IsNullOrEmpty(subtitle))
            {
                TextBlock st = new TextBlock();
                st.Text = subtitle;
                st.FontSize = 11;
                st.Tag = "muted";
                st.TextWrapping = TextWrapping.Wrap;
                st.Margin = new Thickness(0, 2, 0, 0);
                text.Children.Add(st);
            }
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            if (right != null)
            {
                right.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(right, 2);
                grid.Children.Add(right);
            }

            hint = new TextBlock();
            hint.FontSize = 11;
            hint.TextWrapping = TextWrapping.Wrap;
            hint.Margin = new Thickness(38, 6, 0, 0);
            hint.Visibility = Visibility.Collapsed;
            hint.Tag = "hint"; // ApplyDark 不要覆盖它的红/橙提示色
            Grid.SetRow(hint, 1);
            Grid.SetColumn(hint, 0);
            Grid.SetColumnSpan(hint, 3);
            grid.Children.Add(hint);

            card.Child = grid;
            return card;
        }

        private CheckBox CreateToggle(bool value)
        {
            CheckBox cb = new CheckBox();
            cb.IsChecked = value;
            cb.VerticalAlignment = VerticalAlignment.Center;
            cb.Click += delegate { SaveFlags(); };
            return cb;
        }

        private ComboBox CreateCombo(string[] items, int selected)
        {
            ComboBox combo = new ComboBox();
            foreach (string item in items) combo.Items.Add(item);
            combo.SelectedIndex = selected;
            combo.Width = 132;
            combo.Margin = new Thickness(0, 0, 0, 0);
            combo.SelectionChanged += delegate { SaveFlags(); };
            return combo;
        }

        // ===== 主题 =====
        private void ApplyThemeColors()
        {
            bool dark = _config.DarkTheme;
            Background = new SolidColorBrush(dark ? Color.FromRgb(0x20, 0x20, 0x20) : Color.FromRgb(0xF3, 0xF4, 0xF6));
            Foreground = dark ? Brushes.White : Brushes.Black;

            Brush cardBg = new SolidColorBrush(dark ? Color.FromRgb(0x2D, 0x2D, 0x2D) : Colors.White);
            Brush cardBorder = new SolidColorBrush(dark ? Color.FromRgb(0x3A, 0x3A, 0x3A) : Color.FromRgb(0xE5, 0xE5, 0xE5));
            foreach (Border b in _cards)
            {
                b.Background = cardBg;
                b.BorderBrush = cardBorder;
            }

            ApplyDark(this, dark);
            ApplyNavColors();

            foreach (FrameworkElement fe in FindButtons(this))
            {
                Button btn = fe as Button;
                if (btn != null)
                {
                    btn.Background = new SolidColorBrush(dark ? Color.FromRgb(0x2D, 0x2D, 0x2D) : Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));
                    btn.Foreground = dark ? Brushes.White : Brushes.Black;
                    btn.BorderBrush = dark ? new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)) : new SolidColorBrush(Color.FromArgb(0x1A, 0x00, 0x00, 0x00));
                }
            }
        }

        private void ApplyNavColors()
        {
            if (NavGeneral == null) return;
            bool dark = _config.DarkTheme;
            ToggleButton[] navs = new ToggleButton[] { NavGeneral, NavApps, NavHotkeys, NavAbout };
            foreach (ToggleButton tb in navs)
            {
                // 选中态背景/竖条由 NavButton 模板触发器处理，这里只设文字色
                tb.Foreground = dark ? Brushes.White : Brushes.Black;
            }
        }

        private static System.Collections.Generic.IEnumerable<FrameworkElement> FindButtons(DependencyObject root)
        {
            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                DependencyObject d = child as DependencyObject;
                if (d == null) continue;
                FrameworkElement fe = d as FrameworkElement;
                if (fe != null) yield return fe;
                foreach (FrameworkElement inner in FindButtons(d)) yield return inner;
            }
        }

        private static void ApplyDark(DependencyObject root, bool dark)
        {
            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                DependencyObject d = child as DependencyObject;
                if (d == null) continue;
                TextBlock tb = d as TextBlock;
                if (tb != null)
                {
                    string tag = tb.Tag as string;
                    if (tag == "hint") continue; // 热键提示自带红/橙色，别覆盖
                    if (string.Equals(tag, "muted"))
                        tb.Foreground = new SolidColorBrush(dark ? Color.FromRgb(0xA0, 0xA0, 0xA0) : Color.FromRgb(0x6B, 0x6B, 0x6B));
                    else
                        tb.Foreground = dark ? Brushes.White : Brushes.Black;
                }
                TextBox box = d as TextBox;
                if (box != null)
                {
                    box.Background = new SolidColorBrush(dark ? Color.FromRgb(0x22, 0x22, 0x22) : Colors.White);
                    box.Foreground = dark ? Brushes.White : Brushes.Black;
                    box.BorderBrush = dark ? new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)) : new SolidColorBrush(Color.FromArgb(0x1A, 0x00, 0x00, 0x00));
                }
                CheckBox cb = d as CheckBox;
                if (cb != null) cb.Foreground = dark ? Brushes.White : Brushes.Black;
                ComboBox combo = d as ComboBox;
                if (combo != null)
                {
                    combo.Background = new SolidColorBrush(dark ? Color.FromRgb(0x2D, 0x2D, 0x2D) : Color.FromRgb(0xFA, 0xFA, 0xFB));
                    combo.Foreground = dark ? Brushes.White : Brushes.Black;
                    combo.BorderBrush = dark ? new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)) : new SolidColorBrush(Color.FromArgb(0x1A, 0x00, 0x00, 0x00));
                }
                ApplyDark(d, dark);
            }
        }

        private void ReadNewOptions()
        {
            if (_popupMode != null) _config.PopupMode = _popupMode.IsChecked == true;
            if (_rememberFlyout != null) _config.RememberFlyoutPosition = _rememberFlyout.IsChecked == true;
            if (_limitStartup != null) _config.LimitStartupVolume = _limitStartup.IsChecked == true;
            if (_maxVolumeBox != null)
            {
                int v;
                if (int.TryParse(_maxVolumeBox.Text, out v))
                    _config.MaxStartupVolume = Math.Max(0, Math.Min(100, v));
            }
            if (_stepBox != null)
            {
                int v;
                if (int.TryParse(_stepBox.Text, out v))
                    _config.VolumeStep = Math.Max(1, Math.Min(100, v));
            }
        }

        private void SaveFlags()
        {
            ReadNewOptions();
            _config.Startup = _startup.IsChecked == true;
            _config.RememberVolumes = _remember.IsChecked == true;
            _config.EnableHotkeys = _hotkeys.IsChecked == true;
            _config.ExclusiveHotkeys = _exclusiveHotkeys.IsChecked == true;
            _config.EnableWheel = _wheel.IsChecked == true;
            _config.ThemeMode = _themeCombo.SelectedIndex;
            _config.TrayIconKind = _trayCombo.SelectedIndex;
            _config.Save();
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            ReadNewOptions();
            _config.Startup = _startup.IsChecked == true;
            _config.RememberVolumes = _remember.IsChecked == true;
            _config.EnableHotkeys = _hotkeys.IsChecked == true;
            _config.ExclusiveHotkeys = _exclusiveHotkeys.IsChecked == true;
            _config.EnableWheel = _wheel.IsChecked == true;
            _config.ThemeMode = _themeCombo.SelectedIndex;
            _config.TrayIconKind = _trayCombo.SelectedIndex;
            _config.HotkeyVolumeUp = _hotkeyBoxes[0].Text;
            _config.HotkeyVolumeDown = _hotkeyBoxes[1].Text;
            _config.HotkeyMute = _hotkeyBoxes[2].Text;
            _config.HotkeyPanel = _hotkeyBoxes[3].Text;
            _config.HotkeyNextDevice = _hotkeyBoxes[4].Text;
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
