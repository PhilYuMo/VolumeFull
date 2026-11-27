using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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
    public partial class MainWindow : Window
    {
        private static void SetRowDevice(AppRowUi row, string deviceId)
        {
            if (row.ProcessIds.Count == 0)
            {
                try { AudioRouting.SetAppDevice(row.ProcessId, deviceId); } catch { }
                return;
            }
            foreach (int pid in row.ProcessIds)
            {
                try { AudioRouting.SetAppDevice(pid, deviceId); } catch { }
            }
        }

        /// <summary>按应用设置输入设备（录音方向，同 Win11「音量合成器」的输入设备下拉）。</summary>
        private static void SetRowInputDevice(AppRowUi row, string deviceId)
        {
            if (row.ProcessIds.Count == 0)
            {
                try { AudioRouting.SetAppDevice(row.ProcessId, deviceId, EDataFlow.eCapture); } catch { }
                return;
            }
            foreach (int pid in row.ProcessIds)
            {
                try { AudioRouting.SetAppDevice(pid, deviceId, EDataFlow.eCapture); } catch { }
            }
        }

        private void UpdateAppRow(AppRowUi row, List<SessionInfo> group)
        {
            SessionInfo info = group[0];
            ReleaseRowSessions(row);
            FillRowSessions(row, group);
            row.ProcessId = info.ProcessId;
            row.DisplayName = info.Name;
            row.Muted = info.IsMuted;
            UpdateMuteVisual(row);

            VolumeLock lk;
            int mode = 0;
            if (!string.IsNullOrEmpty(info.ProcessName) && _config.LockedVolumes.TryGetValue(info.ProcessName, out lk)) mode = lk.Mode;
            if (row.LockMode != mode)
            {
                row.LockMode = mode;
                UpdateLockVisual(row);
            }
            row.Name.Text = info.Name;

            if (row.DeviceButtonText != null)
            {
                string label = DeviceDisplayText(row, false);
                if (row.DeviceButtonText.Text != label) row.DeviceButtonText.Text = label;
            }
            if (row.InputButtonText != null)
            {
                string label = DeviceDisplayText(row, true);
                if (row.InputButtonText.Text != label) row.InputButtonText.Text = label;
            }

            _restoring = true;
            try
            {
                row.Volume.Value = Math.Round(info.Volume * 100);
                row.VolumeText.Text = ((int)Math.Round(info.Volume * 100)) + "%";
            }
            finally { _restoring = false; }
        }

        /// <summary>展开 / 收起应用行的设置区（输出设备 + 锁定音量），带高度与淡入过渡。</summary>
        private void ToggleRowExpanded(AppRowUi row)
        {
            row.Expanded = !row.Expanded;
            if (row.ChevronText != null)
                row.ChevronText.Text = row.Expanded ? "\uE70E" : "\uE70D";

            Border panel = row.SettingsPanel;
            if (panel == null) return;
            panel.ClipToBounds = true;

            if (!SystemParameters.ClientAreaAnimation)
            {
                panel.BeginAnimation(FrameworkElement.HeightProperty, null);
                panel.BeginAnimation(UIElement.OpacityProperty, null);
                panel.Height = double.NaN;
                panel.Opacity = row.Expanded ? 1 : 0;
                panel.Visibility = row.Expanded ? Visibility.Visible : Visibility.Collapsed;
                return;
            }

            CubicEase ease = new CubicEase();
            ease.EasingMode = EasingMode.EaseOut;

            if (row.Expanded)
            {
                panel.BeginAnimation(FrameworkElement.HeightProperty, null);
                panel.BeginAnimation(UIElement.OpacityProperty, null);
                panel.Height = double.NaN;
                panel.Opacity = 0;
                panel.Visibility = Visibility.Visible;
                // 等布局算出真实高度后再做 0 → 高度 的动画（Height=Auto 不能直接动画）
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    double target = panel.ActualHeight;
                    if (target <= 0) { panel.Opacity = 1; return; }
                    DoubleAnimation h = new DoubleAnimation(0.0, target, new Duration(TimeSpan.FromMilliseconds(180)));
                    h.EasingFunction = ease;
                    h.Completed += delegate
                    {
                        panel.BeginAnimation(FrameworkElement.HeightProperty, null);
                        panel.Height = double.NaN;
                    };
                    panel.BeginAnimation(FrameworkElement.HeightProperty, h);
                    DoubleAnimation f = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(160)));
                    f.EasingFunction = ease;
                    panel.BeginAnimation(UIElement.OpacityProperty, f);
                }), DispatcherPriority.Loaded);
            }
            else
            {
                double current = panel.ActualHeight;
                panel.BeginAnimation(FrameworkElement.HeightProperty, null);
                panel.Height = current;
                DoubleAnimation h = new DoubleAnimation(current, 0.0, new Duration(TimeSpan.FromMilliseconds(150)));
                h.EasingFunction = ease;
                h.Completed += delegate
                {
                    panel.BeginAnimation(FrameworkElement.HeightProperty, null);
                    panel.Height = double.NaN;
                    panel.Opacity = 0;
                    panel.Visibility = Visibility.Collapsed;
                };
                panel.BeginAnimation(FrameworkElement.HeightProperty, h);
                DoubleAnimation f = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(120)));
                f.EasingFunction = ease;
                panel.BeginAnimation(UIElement.OpacityProperty, f);
            }
        }

        /// <summary>0 = 关，VolumeLock.ModeUntilClose = 直到关闭，VolumeLock.ModeAlways = 始终。</summary>
        private void SetRowLock(AppRowUi row, int mode)
        {
            if (string.IsNullOrEmpty(row.ProcessName)) return;
            if (mode == 0)
            {
                _config.LockedVolumes.Remove(row.ProcessName);
            }
            else
            {
                VolumeLock lk = new VolumeLock();
                lk.Volume = (float)(row.Volume.Value / 100.0);
                lk.Mode = mode;
                _config.LockedVolumes[row.ProcessName] = lk;
            }
            row.LockMode = mode;
            UpdateLockVisual(row);
            string baseName = string.IsNullOrEmpty(row.DisplayName) ? row.ProcessName : row.DisplayName;
            row.Name.Text = baseName;
            _config.Save();
        }

        private void UpdateLockVisual(AppRowUi row)
        {
            bool dark = _config.DarkTheme;
            if (row.LockOffButton != null)
            {
                Brush activeBg = dark ? new SolidColorBrush(Color.FromRgb(0x4C, 0xC2, 0xFF)) : new SolidColorBrush(Color.FromRgb(0x00, 0x5F, 0xB8));
                Brush activeFg = dark ? Brushes.Black : Brushes.White;
                Brush normalFg = dark ? Brushes.White : Brushes.Black;
                StyleLockButton(row.LockOffButton, row.LockMode == 0, activeBg, activeFg, normalFg);
                StyleLockButton(row.LockUntilButton, row.LockMode == VolumeLock.ModeUntilClose, activeBg, activeFg, normalFg);
                StyleLockButton(row.LockAlwaysButton, row.LockMode == VolumeLock.ModeAlways, activeBg, activeFg, normalFg);
            }

            // 名称旁的锁定徽章（参照同类音量工具）
            if (row.LockBadge != null)
            {
                if (row.LockMode == 0)
                {
                    row.LockBadge.Visibility = Visibility.Collapsed;
                }
                else
                {
                    row.LockBadge.Visibility = Visibility.Visible;
                    bool untilClose = row.LockMode == VolumeLock.ModeUntilClose;
                    row.LockBadgeIcon.Text = untilClose ? "\uE72E" : "\uE718";
                    row.LockBadgeText.Text = untilClose ? "直到关闭" : "始终";
                }
            }

            // 锁定后滑块不可拖，数值变灰（参照同类音量工具：IsEnabled = !Lock.IsOn）
            bool locked = row.LockMode != 0;
            if (row.Volume != null) row.Volume.IsEnabled = !locked;
            if (row.VolumeText != null) row.VolumeText.Foreground = locked ? _gray : (dark ? Brushes.White : Brushes.Black);
        }

        private static void StyleLockButton(Button b, bool active, Brush activeBg, Brush activeFg, Brush normalFg)
        {
            if (b == null) return;
            Brush fg = active ? activeFg : normalFg;
            b.Background = active ? activeBg : Brushes.Transparent;
            b.Foreground = fg;
            b.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            // Button.Foreground 不会自动传到自定义内容里的 TextBlock，这里显式设置
            StackPanel sp = b.Content as StackPanel;
            if (sp != null)
            {
                foreach (object child in sp.Children)
                {
                    TextBlock tb = child as TextBlock;
                    if (tb != null) tb.Foreground = fg;
                }
            }
        }

        /// <summary>「直到关闭」的锁定：应用进程退出后自动解除（参照同类音量工具的 UntilProcessExit）。</summary>
        private void SweepUntilCloseLocks()
        {
            List<string> drop = null;
            foreach (KeyValuePair<string, VolumeLock> kv in _config.LockedVolumes)
            {
                if (kv.Value.Mode != VolumeLock.ModeUntilClose) continue;
                bool alive = true;
                try { alive = System.Diagnostics.Process.GetProcessesByName(kv.Key).Length > 0; } catch { }
                if (alive) continue;
                if (drop == null) drop = new List<string>();
                drop.Add(kv.Key);
            }
            if (drop == null) return;
            foreach (string k in drop) _config.LockedVolumes.Remove(k);
            _config.Save();
            foreach (DeviceCardUi card in _cards.Values)
            {
                foreach (AppRowUi r in card.Rows.Values)
                {
                    if (r.ProcessName == null || !drop.Contains(r.ProcessName)) continue;
                    r.LockMode = 0;
                    UpdateLockVisual(r);
                    r.Name.Text = r.DisplayName;
                }
            }
        }

        private string DefaultDeviceName()        {
            foreach (DeviceInfo d in _devices) { if (d.IsDefault) return d.Name; }
            return null;
        }

        private string DeviceNameOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (DeviceInfo d in _devices) { if (d.Id == id) return d.Name; }
            return null;
        }

        private string DeviceDisplayText(AppRowUi row, bool input)
        {
            if (input)
            {
                if (!string.IsNullOrEmpty(row.InputDeviceId))
                {
                    string n = InputDeviceNameOf(row.InputDeviceId);
                    if (!string.IsNullOrEmpty(n)) return n;
                }
                string dname = DefaultInputDeviceName();
                return string.IsNullOrEmpty(dname) ? "默认输入设备" : "默认输入设备 · " + dname;
            }
            if (!string.IsNullOrEmpty(row.DeviceId))
            {
                string n = DeviceNameOf(row.DeviceId);
                if (!string.IsNullOrEmpty(n)) return n;
            }
            string def = DefaultDeviceName();
            if (string.IsNullOrEmpty(def)) return "默认设备";
            return "默认设备 · " + def;
        }

        private string InputDeviceNameOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (DeviceInfo d in _inputDevices) { if (d.Id == id) return d.Name; }
            return null;
        }

        private string DefaultInputDeviceName()
        {
            string id = Audio.GetDefaultInputDeviceId();
            if (!string.IsNullOrEmpty(id))
            {
                string n = InputDeviceNameOf(id);
                if (!string.IsNullOrEmpty(n)) return n;
            }
            return null;
        }

        /// <summary>应用行的设备下拉：input=false 为输出设备，true 为输入设备（麦克风）。</summary>
        private void ShowDeviceMenu(AppRowUi row, Button anchor, bool input)
        {
            List<DeviceInfo> devices = input ? _inputDevices : _devices;
            string selectedId = input ? row.InputDeviceId : row.DeviceId;
            DeviceInfo def = null;
            foreach (DeviceInfo d in devices) { if (d.IsDefault) { def = d; break; } }

            Popup pop = new Popup();
            pop.PlacementTarget = anchor;
            pop.Placement = PlacementMode.Bottom;
            pop.StaysOpen = false;
            pop.AllowsTransparency = true;
            _suppressDeactivate = true;
            pop.Closed += delegate { _suppressDeactivate = false; };

            Border bd = new Border();
            bd.Width = Math.Max(240, anchor.ActualWidth);
            bd.CornerRadius = new CornerRadius(8);
            bd.BorderThickness = new Thickness(1);
            bd.Padding = new Thickness(4);
            bd.SetResourceReference(Border.BackgroundProperty, "MenuBackground");
            bd.SetResourceReference(Border.BorderBrushProperty, "MenuBorder");

            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.MaxHeight = 300;

            StackPanel sp = new StackPanel();

            StackPanel defContent = new StackPanel();
            TextBlock defTitle = new TextBlock();
            defTitle.Text = (string.IsNullOrEmpty(selectedId) ? "\u2713  " : "") + (input ? "默认输入设备" : "默认设备");
            defTitle.FontSize = 12;
            defTitle.FontWeight = FontWeights.SemiBold;
            defTitle.SetResourceReference(TextBlock.ForegroundProperty, "MenuText");
            defContent.Children.Add(defTitle);
            TextBlock defSub = new TextBlock();
            defSub.Text = def != null ? def.Name : "";
            defSub.FontSize = 11;
            defSub.Margin = new Thickness(0, 1, 0, 0);
            defSub.Opacity = 0.6;
            defSub.SetResourceReference(TextBlock.ForegroundProperty, "MenuText");
            defContent.Children.Add(defSub);
            sp.Children.Add(MakeDeviceRow(defContent, pop, delegate
            {
                if (input)
                {
                    row.InputDeviceId = null;
                    SetRowInputDevice(row, null);
                    if (row.InputButtonText != null) row.InputButtonText.Text = DeviceDisplayText(row, true);
                }
                else
                {
                    row.DeviceId = null;
                    SetRowDevice(row, null);
                    if (row.DeviceButtonText != null) row.DeviceButtonText.Text = DeviceDisplayText(row, false);
                }
            }));

            Border line = new Border();
            line.Height = 1;
            line.Margin = new Thickness(10, 4, 10, 4);
            line.SetResourceReference(Border.BackgroundProperty, "MenuBorder");
            sp.Children.Add(line);

            foreach (DeviceInfo d in devices)
            {
                if (def != null && d.Id == def.Id) continue;
                DeviceInfo captured = d;
                bool selected = selectedId == d.Id;
                TextBlock t = new TextBlock();
                t.Text = (selected ? "\u2713  " : "") + d.Name;
                t.FontSize = 12;
                t.TextTrimming = TextTrimming.CharacterEllipsis;
                t.SetResourceReference(TextBlock.ForegroundProperty, "MenuText");
                sp.Children.Add(MakeDeviceRow(t, pop, delegate
                {
                    if (input)
                    {
                        row.InputDeviceId = captured.Id;
                        SetRowInputDevice(row, captured.Id);
                        if (row.InputButtonText != null) row.InputButtonText.Text = DeviceDisplayText(row, true);
                    }
                    else
                    {
                        row.DeviceId = captured.Id;
                        SetRowDevice(row, captured.Id);
                        if (row.DeviceButtonText != null) row.DeviceButtonText.Text = DeviceDisplayText(row, false);
                    }
                }));
            }

            sv.Content = sp;
            bd.Child = sv;
            pop.Child = bd;
            pop.IsOpen = true;
        }

        private Border MakeDeviceRow(UIElement content, Popup pop, Action action)
        {
            Border b = new Border();
            b.CornerRadius = new CornerRadius(4);
            b.Padding = new Thickness(10, 6, 10, 6);
            b.Cursor = Cursors.Hand;
            b.Background = Brushes.Transparent;
            b.Child = content;
            b.MouseEnter += delegate { b.Background = (Brush)Application.Current.Resources["MenuHover"]; };
            b.MouseLeave += delegate { b.Background = Brushes.Transparent; };
            b.MouseLeftButtonUp += delegate
            {
                pop.IsOpen = false;
                try { action(); } catch { }
            };
            return b;
        }

        private void ToggleMute(AppRowUi row)
        {
            if (row.Controls.Count == 0) return;
            bool target = !row.Muted;
            foreach (ISimpleAudioVolume c in row.Controls)
            {
                try { Guid g = EmptyGuid; c.SetMute(target ? 1 : 0, ref g); } catch { }
            }
            row.Muted = target;
            UpdateMuteVisual(row);
            if (!string.IsNullOrEmpty(row.ProcessName))
            {
                _config.Mutes[row.ProcessName] = row.Muted;
                _config.Save();
            }
        }

        private void UpdateMuteVisual(AppRowUi row)
        {
            if (row.MuteButton == null) return;
            row.MuteButton.Content = row.Muted ? "\uE74F" : "\uE995";
            row.MuteButton.Foreground = _config.DarkTheme ? Brushes.White : Brushes.Black;
        }

        /// <summary>峰值条宽度按父容器可用宽度自适应（窗口宽度可配置）。</summary>
        private static void SetPeakWidth(Rectangle bar, float peak)
        {
            if (bar == null) return;
            FrameworkElement parent = bar.Parent as FrameworkElement;
            double avail = 240;
            if (parent != null && parent.ActualWidth > 1)
                avail = parent.ActualWidth - bar.Margin.Left - bar.Margin.Right;
            bar.Width = Math.Max(0, Math.Min(1, peak)) * Math.Max(0, avail);
        }

        private void UpdatePeaks()
        {
            if ((DateTime.Now - _lastPeakLog).TotalSeconds > 3)
            {
                _lastPeakLog = DateTime.Now;
                try
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    sb.Append(DateTime.Now.ToString("HH:mm:ss") + " ");
                    foreach (DeviceCardUi c2 in _cards.Values)
                    {
                        foreach (AppRowUi r2 in c2.Rows.Values)
                        {
                            float pk = MaxPeak(r2);
                            sb.Append(r2.Name.Text + "=" + pk.ToString("F2") + "[" + (r2.PeakBar != null ? r2.PeakBar.Width.ToString("F0") : "null") + "] ");
                        }
                    }
                    File.AppendAllText(System.IO.Path.Combine(Config.ConfigDir, "peak.log"), sb.ToString() + "\r\n");
                }
                catch { }
            }
            foreach (DeviceCardUi card in _cards.Values)
            {
                try
                {
                    if (card.Meter != null && card.PeakBar != null)
                    {
                        float peak = card.Meter.GetPeakValue();
                        SetPeakWidth(card.PeakBar, peak);
                    }
                }
                catch { }
                foreach (AppRowUi row in card.Rows.Values)
                {
                    try
                    {
                        if (row.PeakBar != null)
                        {
                            float peak = MaxPeak(row);
                            SetPeakWidth(row.PeakBar, peak);
                        }
                    }
                    catch { }
                }
            }
        }

        private ImageSource GetProcessIcon(int pid)
        {
            if (pid <= 0) return null;
            ImageSource cached;
            if (_iconCache.TryGetValue(pid, out cached)) return cached;
            try
            {
                using (System.Diagnostics.Process p = System.Diagnostics.Process.GetProcessById(pid))
                {
                    string path = p.MainModule.FileName;
                    using (System.Drawing.Icon icon = System.Drawing.Icon.ExtractAssociatedIcon(path))
                    {
                        if (icon == null) return null;
                        BitmapSource bs = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                            icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        bs.Freeze();
                        _iconCache[pid] = bs;
                        return bs;
                    }
                }
            }
            catch { return null; }
        }

        private void OnFilterChanged(object sender, TextChangedEventArgs e)
        {
            TextBox box = sender as TextBox;
            string filter = box == null || box.Text == null ? "" : box.Text.Trim();
            foreach (DeviceCardUi card in _cards.Values)
            {
                foreach (AppRowUi row in card.Rows.Values)
                {
                    row.Row.Visibility = (filter.Length == 0 || row.Name.Text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private Popup _searchPopup;
        private TextBox _searchBox;
        private ScrollBar _vScrollBar;
        private DispatcherTimer _scrollFadeTimer;

        /// <summary>滚动时才显示滚动条（覆盖式，不占布局宽度），停手约 0.9 秒后淡出。</summary>
        private void OnDevicesScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_vScrollBar == null && DevicesScroll != null)
            {
                _vScrollBar = DevicesScroll.Template.FindName("PART_VerticalScrollBar", DevicesScroll) as ScrollBar;
            }
            if (_vScrollBar == null) return;

            _vScrollBar.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(120))));

            if (_scrollFadeTimer == null)
            {
                _scrollFadeTimer = new DispatcherTimer();
                _scrollFadeTimer.Interval = TimeSpan.FromMilliseconds(900);
                _scrollFadeTimer.Tick += delegate
                {
                    _scrollFadeTimer.Stop();
                    if (_vScrollBar != null)
                        _vScrollBar.BeginAnimation(UIElement.OpacityProperty,
                            new DoubleAnimation(0.0, new Duration(TimeSpan.FromMilliseconds(260))));
                };
            }
            _scrollFadeTimer.Stop();
            _scrollFadeTimer.Start();
        }

        /// <summary>工具栏搜索：点图标弹出一个小输入框，实时过滤应用行。</summary>
        private void OnSearchClick(object sender, RoutedEventArgs e)
        {
            if (_searchPopup == null)
            {
                _searchPopup = new Popup();
                _searchPopup.PlacementTarget = SearchButton;
                _searchPopup.Placement = PlacementMode.Bottom;
                _searchPopup.StaysOpen = false;
                _searchPopup.AllowsTransparency = true;
                _searchPopup.PopupAnimation = PopupAnimation.Fade;
                _searchPopup.Closed += delegate
                {
                    if (_searchBox != null) _searchBox.Text = "";
                };

                Border bd = new Border();
                bd.Width = 260;
                bd.CornerRadius = new CornerRadius(8);
                bd.BorderThickness = new Thickness(1);
                bd.Padding = new Thickness(8);
                bd.SetResourceReference(Border.BackgroundProperty, "MenuBackground");
                bd.SetResourceReference(Border.BorderBrushProperty, "MenuBorder");
                bd.SetValue(System.Windows.Documents.TextElement.FontFamilyProperty, Application.Current.FindResource("AppFont"));
                bd.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 16,
                    ShadowDepth = 2,
                    Opacity = 0.16,
                    Color = Colors.Black
                };

                _searchBox = new TextBox();
                _searchBox.Height = 30;
                _searchBox.FontSize = 13;
                _searchBox.VerticalContentAlignment = VerticalAlignment.Center;
                _searchBox.Padding = new Thickness(8, 0, 8, 0);
                _searchBox.BorderThickness = new Thickness(1);
                _searchBox.SetResourceReference(TextBox.BackgroundProperty, "MenuBackground");
                _searchBox.SetResourceReference(TextBox.ForegroundProperty, "MenuText");
                _searchBox.SetResourceReference(TextBox.BorderBrushProperty, "MenuBorder");
                _searchBox.TextChanged += OnFilterChanged;

                bd.Child = _searchBox;
                _searchPopup.Child = bd;
            }
            _searchPopup.IsOpen = true;
            Dispatcher.BeginInvoke(new Action(delegate
            {
                if (_searchBox != null) _searchBox.Focus();
            }), DispatcherPriority.Input);
        }

        private void OnCollapseClick(object sender, RoutedEventArgs e)
        {
            _allCollapsed = !_allCollapsed;
            CollapseButton.Content = _allCollapsed ? "\uE70D" : "\uE70E";
            CollapseButton.ToolTip = _allCollapsed ? "全部展开" : "全部折叠";
            foreach (DeviceCardUi card in _cards.Values)
            {
                card.Expanded = !_allCollapsed;
                card.Apps.Visibility = card.Expanded ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>工具栏「切换设备」下拉（照同类音量工具的输出设备列表：图标 + 名称，当前默认项左侧强调竖条 +「默认」）。</summary>
        private void OnSwitchDeviceClick(object sender, RoutedEventArgs e)
        {
            string defaultId = Audio.GetDefaultOutputDeviceId();
            List<DeviceInfo> devices;
            try { devices = Audio.GetOutputDevices(); } catch { return; }
            ShowToolbarDeviceMenu(devices, defaultId, false, SwitchDeviceButton);
        }

        /// <summary>工具栏「切换输入设备」（麦克风）：和输出设备同样的下拉。</summary>
        private void OnSwitchInputDeviceClick(object sender, RoutedEventArgs e)
        {
            string defaultId = Audio.GetDefaultInputDeviceId();
            List<DeviceInfo> devices;
            try { devices = Audio.GetInputDevices(); } catch { return; }
            ShowToolbarDeviceMenu(devices, defaultId, true, SwitchInputButton);
        }

        private void ShowToolbarDeviceMenu(List<DeviceInfo> devices, string defaultId, bool input, Button anchor)
        {
            Popup pop = new Popup();
            pop.PlacementTarget = anchor;
            pop.Placement = PlacementMode.Bottom;
            pop.StaysOpen = false;
            pop.AllowsTransparency = true;
            _suppressDeactivate = true;
            pop.Closed += delegate { _suppressDeactivate = false; };

            Border bd = new Border();
            bd.Width = 322;
            bd.CornerRadius = new CornerRadius(8);
            bd.BorderThickness = new Thickness(1);
            bd.Padding = new Thickness(6);
            bd.SetResourceReference(Border.BackgroundProperty, "MenuBackground");
            bd.SetResourceReference(Border.BorderBrushProperty, "MenuBorder");
            // Popup 是独立的视觉树，字体不会从主窗口继承，显式用全局字体（HarmonyOS Sans SC）
            bd.SetValue(System.Windows.Documents.TextElement.FontFamilyProperty, Application.Current.FindResource("AppFont"));
            bd.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 18,
                ShadowDepth = 2,
                Opacity = 0.16,
                Color = Colors.Black
            };

            StackPanel sp = new StackPanel();
            string empty = input ? "没有可用的录音设备" : "没有可用的输出设备";
            if (devices == null || devices.Count == 0)
            {
                TextBlock none = new TextBlock();
                none.Text = empty;
                none.FontSize = 12;
                none.Margin = new Thickness(10, 8, 10, 8);
                none.Opacity = 0.6;
                none.SetResourceReference(TextBlock.ForegroundProperty, "MenuText");
                sp.Children.Add(none);
            }
            else
            {
                foreach (DeviceInfo d in devices)
                {
                    sp.Children.Add(MakeSwitchDeviceRow(d, d.Id == defaultId, pop, input));
                }
            }
            bd.Child = sp;
            pop.Child = bd;
            pop.IsOpen = true;
        }

        /// <summary>设备类型的图标：输入设备统一麦克风；输出设备按名称区分耳机 / 扬声器。</summary>
        private static string DeviceGlyph(string name, bool input)
        {
            if (input) return "\uE720";
            if (string.IsNullOrEmpty(name)) return "\uE7F5";
            if (name.IndexOf("耳机", StringComparison.Ordinal) >= 0
                || name.IndexOf("headphone", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("headset", StringComparison.OrdinalIgnoreCase) >= 0)
                return "\uE7F6";
            return "\uE7F5";
        }

        private Border MakeSwitchDeviceRow(DeviceInfo device, bool isDefault, Popup pop, bool input)
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });   // 强调竖条
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });    // 设备图标
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });    // 默认

            Rectangle bar = new Rectangle();
            bar.Width = 3;
            bar.Height = 18;
            bar.RadiusX = 1.5;
            bar.RadiusY = 1.5;
            bar.HorizontalAlignment = HorizontalAlignment.Left;
            bar.VerticalAlignment = VerticalAlignment.Center;
            bar.Fill = (Brush)Application.Current.Resources["AccentBar"];
            bar.Visibility = isDefault ? Visibility.Visible : Visibility.Collapsed;
            g.Children.Add(bar);

            TextBlock icon = new TextBlock();
            icon.Text = DeviceGlyph(device.Name, input);
            icon.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            icon.FontSize = 15;
            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.Margin = new Thickness(4, 0, 10, 0);
            icon.Foreground = _gray;
            Grid.SetColumn(icon, 1);
            g.Children.Add(icon);

            TextBlock name = new TextBlock();
            name.Text = device.Name;
            name.FontSize = 12;
            name.VerticalAlignment = VerticalAlignment.Center;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.SetResourceReference(TextBlock.ForegroundProperty, "MenuText");
            Grid.SetColumn(name, 2);
            g.Children.Add(name);

            TextBlock badge = new TextBlock();
            badge.Text = "默认";
            badge.FontSize = 11;
            badge.VerticalAlignment = VerticalAlignment.Center;
            badge.Margin = new Thickness(8, 0, 2, 0);
            badge.Opacity = 0.55;
            badge.SetResourceReference(TextBlock.ForegroundProperty, "MenuText");
            badge.Visibility = isDefault ? Visibility.Visible : Visibility.Collapsed;
            Grid.SetColumn(badge, 3);
            g.Children.Add(badge);

            Border row = new Border();
            row.CornerRadius = new CornerRadius(4);
            row.Padding = new Thickness(4, 8, 8, 8);
            row.Cursor = Cursors.Hand;
            row.Background = Brushes.Transparent;
            row.Child = g;
            row.MouseEnter += delegate { row.Background = (Brush)Application.Current.Resources["MenuHover"]; };
            row.MouseLeave += delegate { row.Background = Brushes.Transparent; };
            row.MouseLeftButtonUp += delegate
            {
                pop.IsOpen = false;
                try
                {
                    Audio.SetDefaultDevice(device.Id);
                    RefreshAll(true);
                    StatusText.Text = (input ? "默认输入设备：" : "默认输出设备：") + device.Name;
                }
                catch { }
            };
            return row;
        }

        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            RefreshAll(true);
        }

        private void OnTopmostClick(object sender, RoutedEventArgs e)
        {
            if (_config.PopupMode)
            {
                TopmostButton.IsChecked = true;
                Topmost = true;
                return;
            }
            _config.TopMost = TopmostButton.IsChecked == true;
            Topmost = _config.TopMost;
            _config.Save();
        }

        private void SelectRow(AppRowUi row)
        {
            if (_selectedCard != null && _selectedCard.Card != null)
            {
                _selectedCard.Card.Background = new SolidColorBrush(_config.DarkTheme ? Color.FromRgb(0x2C, 0x2C, 0x2C) : Color.FromArgb(0xD9, 0xFF, 0xFF, 0xFF));
                _selectedCard = null;
            }
            if (row == null && _selectedRow == null) return;
            if (_selectedRow != null && _selectedRow.Row != null)
            {
                _selectedRow.Row.Background = Brushes.Transparent;
            }
            _selectedRow = row;
            if (row != null && row.Row != null)
            {
                row.Row.Background = new SolidColorBrush(_config.DarkTheme
                    ? Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)
                    : Color.FromArgb(0x1A, 0x00, 0x5F, 0xB8));
            }
        }

        private void SelectCard(DeviceCardUi card)
        {
            if (_selectedRow != null && _selectedRow.Row != null)
            {
                _selectedRow.Row.Background = Brushes.Transparent;
                _selectedRow = null;
            }
            if (_selectedCard != null && _selectedCard.Card != null)
            {
                _selectedCard.Card.Background = new SolidColorBrush(_config.DarkTheme ? Color.FromRgb(0x2C, 0x2C, 0x2C) : Color.FromArgb(0xD9, 0xFF, 0xFF, 0xFF));
            }
            _selectedCard = card;
            if (card != null && card.Card != null)
            {
                card.Card.Background = new SolidColorBrush(_config.DarkTheme ? Color.FromRgb(0x3A, 0x3A, 0x3A) : Color.FromArgb(0xF0, 0xF0, 0xF6, 0xFC));
            }
        }

        private void ShowBalance(AppRowUi row)
        {
            if (row.Channels == null) return;
            float left = 1, right = 1;
            try { row.Channels.GetChannelVolume(0, out left); } catch { }
            try { row.Channels.GetChannelVolume(1, out right); } catch { }
            BalanceWindow win = new BalanceWindow(row.Name.Text, row.Channels, 0, left, right, _config.DarkTheme);
            win.Owner = this;
            _suppressDeactivate = true;
            try
            {
                if (win.ShowDialog() == true)
                {
                    Guid g = EmptyGuid;
                    foreach (IChannelAudioVolume ch in row.ChannelsList)
                    {
                        try
                        {
                            ch.SetChannelVolume(0, win.LeftVolume, ref g);
                            ch.SetChannelVolume(1, win.RightVolume, ref g);
                        }
                        catch { }
                    }
                }
            }
            finally
            {
                _suppressDeactivate = false;
            }
        }

    }
}
