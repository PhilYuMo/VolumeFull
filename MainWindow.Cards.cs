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
        private void RefreshAll(bool force)
        {
            try
            {
                RefreshAllCore(force);
            }
            catch (Exception ex) { LogError(ex); }
        }

        private void RefreshAllCore(bool force)
        {
            if (_restoring) return;
            List<DeviceInfo> devices;
            try { devices = Audio.GetOutputDevices(); }
            catch { return; }

            List<DeviceInfo> ordered = new List<DeviceInfo>();
            DeviceInfo def = null;
            foreach (DeviceInfo d in devices) { if (d.IsDefault) { def = d; break; } }
            if (def != null) ordered.Add(def);
            foreach (DeviceInfo d in devices) { if (d != def) ordered.Add(d); }
            _devices = ordered;
            try { _inputDevices = Audio.GetInputDevices(); } catch { }

            if (_config.RememberVolumes && (DateTime.Now - _lastSave).TotalSeconds > 10)
            {
                _lastSave = DateTime.Now;
                _config.Save();
            }

            if ((DateTime.Now - _lastLockSweep).TotalSeconds > 5)
            {
                _lastLockSweep = DateTime.Now;
                SweepUntilCloseLocks();
            }

            List<string> removed = new List<string>();
            foreach (string id in _cards.Keys)
            {
                bool exists = false;
                foreach (DeviceInfo d in ordered) { if (d.Id == id) { exists = true; break; } }
                if (!exists) removed.Add(id);
            }
            foreach (string id in removed)
            {
                DeviceCardUi card = _cards[id];
                DevicesPanel.Children.Remove(card.Card);
                if (card.Endpoint != null) { try { Marshal.ReleaseComObject(card.Endpoint); } catch { } }
                foreach (AppRowUi row in card.Rows.Values)
                {
                    ReleaseRowSessions(row);
                }
                _cards.Remove(id);
            }

            foreach (DeviceInfo device in ordered)
            {
                DeviceCardUi card;
                if (!_cards.TryGetValue(device.Id, out card))
                {
                    card = CreateCard(device);
                    _cards[device.Id] = card;
                    DevicesPanel.Children.Add(card.Card);
                }
                UpdateCard(card, device);
            }

            for (int i = 0; i < ordered.Count && i < DevicesPanel.Children.Count; i++)
            {
                DeviceCardUi card;
                if (_cards.TryGetValue(ordered[i].Id, out card))
                {
                    int idx = DevicesPanel.Children.IndexOf(card.Card);
                    if (idx >= 0 && idx != i)
                    {
                        DevicesPanel.Children.Remove(card.Card);
                        DevicesPanel.Children.Insert(Math.Min(i, DevicesPanel.Children.Count), card.Card);
                    }
                }
            }

            int total = 0;
            foreach (DeviceCardUi c in _cards.Values) total += c.Rows.Count;
            StatusText.Text = "共 " + ordered.Count + " 个输出设备，" + total + " 个音频应用";
            UpdatePeaks();
        }

        private DeviceCardUi CreateCard(DeviceInfo device)
        {
            DeviceCardUi ui = new DeviceCardUi();
            ui.DeviceId = device.Id;

            bool darkTheme = _config.DarkTheme;
            Border card = new Border();
            card.CornerRadius = new CornerRadius(12);
            card.Background = new SolidColorBrush(darkTheme ? Color.FromRgb(0x2C, 0x2C, 0x2C) : Color.FromArgb(0xD9, 0xFF, 0xFF, 0xFF));
            card.BorderBrush = darkTheme ? new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40)) : new SolidColorBrush(Color.FromArgb(0x1A, 0x00, 0x00, 0x00));
            card.BorderThickness = new Thickness(1);
            card.Margin = new Thickness(0, 0, 0, 12);
            card.Padding = new Thickness(14, 10, 14, 10);
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 0, Opacity = 0.10, Color = Colors.Black };
            card.Tag = ui;

            ContextMenu cardMenu = new ContextMenu();
            MenuItem setDefaultItem = new MenuItem();
            setDefaultItem.Header = "设为默认输出设备";
            DeviceInfo cardDevice = device;
            setDefaultItem.Click += delegate { try { Audio.SetDefaultDevice(cardDevice.Id); RefreshAll(true); } catch { } };
            cardMenu.Items.Add(setDefaultItem);
            card.ContextMenu = cardMenu;

            StackPanel content = new StackPanel();

            Grid header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock icon = new TextBlock();
            icon.Text = "🔊";
            icon.FontSize = 20;
            icon.VerticalAlignment = VerticalAlignment.Center;
            icon.Margin = new Thickness(0, 0, 10, 0);
            header.Children.Add(icon);

            StackPanel title = new StackPanel();
            title.VerticalAlignment = VerticalAlignment.Center;
            TextBlock name = new TextBlock();
            name.FontWeight = FontWeights.SemiBold;
            name.FontSize = 14;
            title.Children.Add(name);
            StackPanel sub = new StackPanel();
            sub.Orientation = Orientation.Horizontal;
            TextBlock subtitle = new TextBlock();
            subtitle.FontSize = 11;
            subtitle.Foreground = _gray;
            sub.Children.Add(subtitle);
            Border badge = new Border();
            badge.Background = _green;
            badge.CornerRadius = new CornerRadius(4);
            badge.Padding = new Thickness(6, 1, 6, 1);
            badge.Margin = new Thickness(8, 0, 0, 0);
            badge.Visibility = Visibility.Collapsed;
            TextBlock badgeText = new TextBlock();
            badgeText.Text = "默认";
            badgeText.FontSize = 10;
            badgeText.Foreground = Brushes.White;
            badge.Child = badgeText;
            sub.Children.Add(badge);
            title.Children.Add(sub);
            Grid.SetColumn(title, 1);
            header.Children.Add(title);

            Button moreBtn = new Button();
            moreBtn.Content = "⋯";
            moreBtn.FontSize = 16;
            moreBtn.FontWeight = FontWeights.Bold;
            moreBtn.Foreground = _config.DarkTheme ? Brushes.White : Brushes.Black;
            moreBtn.Padding = new Thickness(6, 0, 6, 0);
            moreBtn.Background = Brushes.Transparent;
            moreBtn.BorderThickness = new Thickness(0);
            moreBtn.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(moreBtn, 2);
            header.Children.Add(moreBtn);

            Button expandBtn = new Button();
            expandBtn.Content = "⌃";
            expandBtn.FontSize = 14;
            expandBtn.FontWeight = FontWeights.Bold;
            expandBtn.Foreground = _config.DarkTheme ? Brushes.White : Brushes.Black;
            expandBtn.Padding = new Thickness(6, 0, 6, 0);
            expandBtn.Background = Brushes.Transparent;
            expandBtn.BorderThickness = new Thickness(0);
            expandBtn.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(expandBtn, 3);
            header.Children.Add(expandBtn);

            content.Children.Add(header);

            Grid volRow = new Grid();
            volRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            volRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            volRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            volRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            volRow.Margin = new Thickness(0, 8, 0, 0);

            TextBlock speaker = new TextBlock();
            speaker.Text = "🔈";
            speaker.FontSize = 13;
            speaker.VerticalAlignment = VerticalAlignment.Center;
            speaker.Margin = new Thickness(0, 0, 8, 0);
            volRow.Children.Add(speaker);

            Slider slider = new Slider();
            slider.Minimum = 0;
            slider.Maximum = 100;
            slider.VerticalAlignment = VerticalAlignment.Center;
            slider.Style = (Style)FindResource("Win11Slider");
            Grid.SetColumn(slider, 1);
            volRow.Children.Add(slider);

            TextBlock volumeText = new TextBlock();
            volumeText.Width = 42;
            volumeText.TextAlignment = TextAlignment.Right;
            volumeText.VerticalAlignment = VerticalAlignment.Center;
            volumeText.FontSize = 12;
            Grid.SetColumn(volumeText, 2);
            volRow.Children.Add(volumeText);

            Button muteBtn = new Button();
            muteBtn.Content = "\uE995";
            muteBtn.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            muteBtn.FontSize = 16;
            muteBtn.Width = 32;
            muteBtn.Height = 32;
            muteBtn.Padding = new Thickness(0);
            muteBtn.Background = Brushes.Transparent;
            muteBtn.BorderThickness = new Thickness(0);
            muteBtn.VerticalAlignment = VerticalAlignment.Center;
            muteBtn.Margin = new Thickness(8, 0, 0, 0);
            muteBtn.Foreground = _config.DarkTheme ? Brushes.White : Brushes.Black;
            muteBtn.ToolTip = "静音 / 取消静音";
            muteBtn.Click += delegate { ToggleDeviceMute(ui); };
            Grid.SetColumn(muteBtn, 3);
            volRow.Children.Add(muteBtn);
            ui.MuteButton = muteBtn;

            content.Children.Add(volRow);

            Rectangle peakBar = new Rectangle();
            peakBar.Height = 2;
            peakBar.Fill = _accent;
            peakBar.HorizontalAlignment = HorizontalAlignment.Left;
            peakBar.Width = 0;
            peakBar.Margin = new Thickness(26, 2, 0, 0);
            content.Children.Add(peakBar);

            StackPanel apps = new StackPanel();
            apps.Margin = new Thickness(0, 4, 0, 0);
            content.Children.Add(apps);

            card.Child = content;
            if (darkTheme) SetTextColor(card, Brushes.White);

            ui.Card = card;
            ui.Icon = icon;
            ui.Name = name;
            ui.Subtitle = subtitle;
            ui.Badge = badge;
            ui.Volume = slider;
            ui.VolumeText = volumeText;
            ui.Apps = apps;
            ui.PeakBar = peakBar;

            slider.ValueChanged += delegate
            {
                if (_restoring) return;
                if (ui.Endpoint == null) return;
                Audio.SetEndpointVolumeScalar(ui.Endpoint, (float)(slider.Value / 100.0));
                volumeText.Text = ((int)Math.Round(slider.Value)) + "%";
            };

            moreBtn.Click += delegate
            {
                cardMenu.PlacementTarget = moreBtn;
                cardMenu.IsOpen = true;
            };
            expandBtn.Click += delegate
            {
                ui.Expanded = !ui.Expanded;
                apps.Visibility = ui.Expanded ? Visibility.Visible : Visibility.Collapsed;
                expandBtn.Content = ui.Expanded ? "⌃" : "⌄";
            };

            return ui;
        }
        private void UpdateCard(DeviceCardUi card, DeviceInfo device)
        {
            card.Info = device;
            card.Name.Text = device.Name;
            card.Icon.Text = (device.Name.Contains("耳机") || device.Name.Contains("Headphone")) ? "🎧" : "🔊";
            card.Badge.Visibility = device.IsDefault ? Visibility.Visible : Visibility.Collapsed;

            if (card.Endpoint == null)
            {
                try { card.Endpoint = Audio.GetEndpointVolumeForDevice(device.Id); } catch { }
            }
            if (card.Meter == null)
            {
                try { card.Meter = Audio.GetDeviceMeter(device.Id); } catch { }
            }
            if (card.Endpoint != null)
            {
                try
                {
                    float level;
                    card.Endpoint.GetMasterVolumeLevelScalar(out level);
                    int mute = card.Endpoint.GetMute();
                    _restoring = true;
                    try
                    {
                        card.Volume.Value = Math.Round(level * 100);
                        card.VolumeText.Text = mute != 0 ? "静音" : ((int)Math.Round(level * 100)) + "%";
                        if (card.MuteButton != null)
                        {
                            card.MuteButton.Content = mute != 0 ? "\uE74F" : "\uE995";
                            card.MuteButton.Foreground = _config.DarkTheme ? Brushes.White : Brushes.Black;
                        }
                    }
                    finally { _restoring = false; }
                }
                catch { }
            }

            SetTextColor(card.Card, _config.DarkTheme ? Brushes.White : Brushes.Black);
            RefreshCardSessions(card);
            card.Subtitle.Text = card.Rows.Count + " 个应用";
            card.Apps.Visibility = card.Expanded ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ToggleDeviceMute(DeviceCardUi card)
        {
            if (card.Endpoint == null) return;
            try
            {
                bool muted = Audio.GetEndpointMute(card.Endpoint);
                Audio.SetEndpointMute(card.Endpoint, !muted);
            }
            catch { }
            RefreshAll(false);
        }

        // 会话分组合并键：同一进程名（同一程序）在系统里可能注册了多个音频会话，
        // 合并成一行显示，与 Win11「音量合成器」按应用分组的行为一致。
        private static string SessionKey(SessionInfo info)
        {
            if (info.IsSystemSounds) return "#system";
            if (!string.IsNullOrEmpty(info.ProcessName)) return "app:" + info.ProcessName.ToLowerInvariant();
            return "pid:" + info.ProcessId;
        }

        private static void ReleaseRowSessions(AppRowUi row)
        {
            foreach (ISimpleAudioVolume c in row.Controls) { try { Marshal.ReleaseComObject(c); } catch { } }
            foreach (IChannelAudioVolume ch in row.ChannelsList) { try { Marshal.ReleaseComObject(ch); } catch { } }
            foreach (IAudioMeterInformation m in row.Meters) { try { Marshal.ReleaseComObject(m); } catch { } }
            row.Controls.Clear();
            row.ChannelsList.Clear();
            row.Meters.Clear();
        }

        private static void FillRowSessions(AppRowUi row, List<SessionInfo> group)
        {
            row.Controls.Clear();
            row.ChannelsList.Clear();
            row.Meters.Clear();
            row.ProcessIds.Clear();
            foreach (SessionInfo s in group)
            {
                if (s.Control != null) row.Controls.Add(s.Control);
                if (s.Channels != null) row.ChannelsList.Add(s.Channels);
                if (s.Meter != null) row.Meters.Add(s.Meter);
                if (s.ProcessId > 0 && !row.ProcessIds.Contains(s.ProcessId)) row.ProcessIds.Add(s.ProcessId);
            }
        }

        private static float MaxPeak(AppRowUi row)
        {
            float best = -1;
            foreach (IAudioMeterInformation m in row.Meters)
            {
                if (m == null) continue;
                try { float p = m.GetPeakValue(); if (p > best) best = p; } catch { }
            }
            return best;
        }

        private void RefreshCardSessions(DeviceCardUi card)
        {
            List<SessionInfo> sessions;
            try { sessions = Audio.GetSessionsForDevice(card.DeviceId); }
            catch { return; }

            Dictionary<string, List<SessionInfo>> grouped = new Dictionary<string, List<SessionInfo>>();
            List<string> order = new List<string>();
            foreach (SessionInfo s in sessions)
            {
                string k = SessionKey(s);
                List<SessionInfo> bucket;
                if (!grouped.TryGetValue(k, out bucket))
                {
                    bucket = new List<SessionInfo>();
                    grouped[k] = bucket;
                    order.Add(k);
                }
                bucket.Add(s);
            }

            HashSet<string> seen = new HashSet<string>();
            foreach (string key in order)
            {
                List<SessionInfo> group = grouped[key];
                SessionInfo info = group[0];
                seen.Add(key);
                AppRowUi row;
                if (!card.Rows.TryGetValue(key, out row))
                {
                    row = CreateAppRow(card, key, group);
                    card.Rows[key] = row;
                    card.Apps.Children.Add(row.Row);
                }
                else
                {
                    UpdateAppRow(row, group);
                }

                foreach (SessionInfo s in group)
                {
                    VolumeLock locked;
                    if (!string.IsNullOrEmpty(s.ProcessName) && _config.LockedVolumes.TryGetValue(s.ProcessName, out locked))
                    {
                        try { Guid lg = EmptyGuid; s.Control.SetMasterVolume(locked.Volume, ref lg); } catch { }
                    }

                    if (!s.IsSystemSounds && !string.IsNullOrEmpty(s.ProcessName))
                    {
                        string mk = s.ProcessName;
                        bool firstSeen = !_restoredSessions.Contains(s.Id);
                        bool restored = false;
                        if (firstSeen)
                        {
                            _restoredSessions.Add(s.Id);
                            if (_config.RememberVolumes && _config.Volumes.ContainsKey(mk))
                            {
                                try
                                {
                                    Guid g = EmptyGuid;
                                    s.Control.SetMasterVolume(_config.Volumes[mk], ref g);
                                    bool muted = _config.Mutes.ContainsKey(mk) && _config.Mutes[mk];
                                    s.Control.SetMute(muted ? 1 : 0, ref g);
                                    restored = true;
                                }
                                catch { }
                            }
                            if (_config.LimitStartupVolume)
                            {
                                float limit = Math.Max(0, Math.Min(100, _config.MaxStartupVolume)) / 100f;
                                float effective = restored ? _config.Volumes[mk] : s.Volume;
                                if (effective > limit + 0.0005f)
                                {
                                    try { Guid g = EmptyGuid; s.Control.SetMasterVolume(limit, ref g); } catch { }
                                }
                            }
                            if (!restored && _config.RememberVolumes)
                            {
                                _config.Volumes[mk] = s.Volume;
                                _config.Mutes[mk] = s.IsMuted;
                            }
                        }
                        else if (_config.RememberVolumes)
                        {
                            _config.Volumes[mk] = s.Volume;
                            _config.Mutes[mk] = s.IsMuted;
                        }
                    }
                }
            }

            if (!_peakLogged && sessions.Count > 0)
            {
                _peakLogged = true;
                int meters = 0;
                foreach (AppRowUi r in card.Rows.Values) { if (r.Meters.Count > 0) meters++; }
                try { File.AppendAllText(System.IO.Path.Combine(Config.ConfigDir, "peak.log"), "sessions=" + sessions.Count + " meters=" + meters + "\r\n"); } catch { }
            }

            List<string> stale = new List<string>();
            foreach (string id in card.Rows.Keys) { if (!seen.Contains(id)) stale.Add(id); }
            foreach (string id in stale)
            {
                AppRowUi row = card.Rows[id];
                card.Apps.Children.Remove(row.Row);
                ReleaseRowSessions(row);
                card.Rows.Remove(id);
            }
        }

        private AppRowUi CreateAppRow(DeviceCardUi card, string key, List<SessionInfo> group)
        {
            SessionInfo info = group[0];
            AppRowUi row = new AppRowUi();
            row.Id = key;
            row.ProcessName = info.ProcessName;
            row.ProcessId = info.ProcessId;
            row.DisplayName = info.Name;
            FillRowSessions(row, group);
            // 系统声音没有真实进程：不可路由、不可锁定，也不显示展开徽章（参照同类音量工具的 AppOpens）
            bool canRoute = !string.IsNullOrEmpty(info.ProcessName);

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Margin = new Thickness(0, 9, 0, 9);

            // ── 名称行：应用图标（24px，右下角展开徽章）/ 名称 + 锁定徽章 / ⋯ ──
            Grid nameRow = new Grid();
            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Button iconBtn = new Button();
            iconBtn.Style = (Style)FindResource("IconToggle");
            iconBtn.Width = 34;
            iconBtn.Height = 34;
            iconBtn.Margin = new Thickness(0, 0, 8, 0);
            iconBtn.ToolTip = "展开 / 收起设置";
            iconBtn.VerticalAlignment = VerticalAlignment.Center;

            Grid iconGrid = new Grid();
            iconGrid.Width = 34;
            iconGrid.Height = 34;

            Image iconImage = new Image();
            iconImage.Width = 24;
            iconImage.Height = 24;
            iconImage.HorizontalAlignment = HorizontalAlignment.Center;
            iconImage.VerticalAlignment = VerticalAlignment.Center;
            iconImage.Source = GetProcessIcon(info.ProcessId);
            if (iconImage.Source != null)
            {
                iconGrid.Children.Add(iconImage);
            }
            else
            {
                // 系统声音等无进程图标的会话，用系统音频图标
                TextBlock iconGlyph = new TextBlock();
                iconGlyph.Text = "\uE7F5";
                iconGlyph.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
                iconGlyph.FontSize = 20;
                iconGlyph.HorizontalAlignment = HorizontalAlignment.Center;
                iconGlyph.VerticalAlignment = VerticalAlignment.Center;
                iconGlyph.Foreground = _gray;
                iconGrid.Children.Add(iconGlyph);
            }

            Border openBadge = new Border();
            openBadge.Width = 16;
            openBadge.Height = 16;
            openBadge.CornerRadius = new CornerRadius(8);
            openBadge.BorderThickness = new Thickness(1);
            openBadge.HorizontalAlignment = HorizontalAlignment.Right;
            openBadge.VerticalAlignment = VerticalAlignment.Bottom;
            openBadge.Background = new SolidColorBrush(_config.DarkTheme ? Color.FromRgb(0x2C, 0x2C, 0x2C) : Colors.White);
            openBadge.SetResourceReference(Border.BorderBrushProperty, "MenuBorder");

            TextBlock chevronText = new TextBlock();
            chevronText.Text = "\uE70D";
            chevronText.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            chevronText.FontSize = 9;
            chevronText.Foreground = _gray;
            chevronText.HorizontalAlignment = HorizontalAlignment.Center;
            chevronText.VerticalAlignment = VerticalAlignment.Center;
            openBadge.Child = chevronText;
            iconGrid.Children.Add(openBadge);

            iconBtn.Content = iconGrid;
            if (canRoute) iconBtn.Click += delegate { ToggleRowExpanded(row); };
            nameRow.Children.Add(iconBtn);
            row.ChevronText = chevronText;
            if (!canRoute) openBadge.Visibility = Visibility.Collapsed;

            StackPanel nameStack = new StackPanel();
            nameStack.Orientation = Orientation.Horizontal;
            nameStack.VerticalAlignment = VerticalAlignment.Center;

            TextBlock name = new TextBlock();
            name.FontSize = 12;
            name.Text = info.Name;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.VerticalAlignment = VerticalAlignment.Center;
            nameStack.Children.Add(name);

            // 锁定徽章（参照同类音量工具名称旁的 lock badge）：图标 + 模式文字
            Border lockBadge = new Border();
            lockBadge.Height = 18;
            lockBadge.Padding = new Thickness(6, 0, 6, 0);
            lockBadge.CornerRadius = new CornerRadius(9);
            lockBadge.BorderThickness = new Thickness(1);
            lockBadge.Margin = new Thickness(8, 0, 0, 0);
            lockBadge.VerticalAlignment = VerticalAlignment.Center;
            lockBadge.Visibility = Visibility.Collapsed;
            lockBadge.SetResourceReference(Border.BorderBrushProperty, "MenuBorder");
            lockBadge.SetResourceReference(Border.BackgroundProperty, "MenuBackground");

            StackPanel lockBadgeContent = new StackPanel();
            lockBadgeContent.Orientation = Orientation.Horizontal;
            TextBlock lockBadgeIcon = new TextBlock();
            lockBadgeIcon.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            lockBadgeIcon.FontSize = 11;
            lockBadgeIcon.VerticalAlignment = VerticalAlignment.Center;
            lockBadgeIcon.Margin = new Thickness(0, 0, 4, 0);
            lockBadgeIcon.Foreground = _gray;
            lockBadgeContent.Children.Add(lockBadgeIcon);
            TextBlock lockBadgeText = new TextBlock();
            lockBadgeText.FontSize = 11;
            lockBadgeText.VerticalAlignment = VerticalAlignment.Center;
            lockBadgeText.Foreground = _gray;
            lockBadgeContent.Children.Add(lockBadgeText);
            lockBadge.Child = lockBadgeContent;
            nameStack.Children.Add(lockBadge);
            row.LockBadge = lockBadge;
            row.LockBadgeIcon = lockBadgeIcon;
            row.LockBadgeText = lockBadgeText;

            Grid.SetColumn(nameStack, 1);
            nameRow.Children.Add(nameStack);

            Button moreBtn = new Button();
            moreBtn.Content = "⋯";
            moreBtn.FontSize = 15;
            moreBtn.FontWeight = FontWeights.Bold;
            moreBtn.Foreground = _config.DarkTheme ? Brushes.White : Brushes.Black;
            moreBtn.Padding = new Thickness(6, 0, 6, 0);
            moreBtn.Background = Brushes.Transparent;
            moreBtn.BorderThickness = new Thickness(0);
            moreBtn.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(moreBtn, 2);
            nameRow.Children.Add(moreBtn);

            Grid.SetRow(nameRow, 0);
            grid.Children.Add(nameRow);

            Grid volRow = new Grid();
            volRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            volRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            volRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Slider slider = new Slider();
            slider.Minimum = 0;
            slider.Maximum = 100;
            slider.Style = (Style)FindResource("Win11Slider");
            slider.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(slider, 0);
            volRow.Children.Add(slider);

            TextBlock volText = new TextBlock();
            volText.Width = 40;
            volText.FontSize = 11;
            volText.TextAlignment = TextAlignment.Right;
            volText.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(volText, 1);
            volRow.Children.Add(volText);

            // 静音按钮：照同类音量工具的 IconButton + MuteIcon（未静音 E995 / 静音 E74F）
            Button muteBtn = new Button();
            muteBtn.Content = info.IsMuted ? "\uE74F" : "\uE995";
            muteBtn.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            muteBtn.FontSize = 16;
            muteBtn.Width = 32;
            muteBtn.Height = 32;
            muteBtn.Padding = new Thickness(0);
            muteBtn.Background = Brushes.Transparent;
            muteBtn.BorderThickness = new Thickness(0);
            muteBtn.VerticalAlignment = VerticalAlignment.Center;
            muteBtn.Margin = new Thickness(8, 0, 0, 0);
            muteBtn.Foreground = _config.DarkTheme ? Brushes.White : Brushes.Black;
            muteBtn.ToolTip = "静音 / 取消静音";
            muteBtn.Click += delegate { ToggleMute(row); };
            Grid.SetColumn(muteBtn, 2);
            volRow.Children.Add(muteBtn);
            row.MuteButton = muteBtn;

            Grid.SetRow(volRow, 1);
            grid.Children.Add(volRow);

            Rectangle peakBar = new Rectangle();
            peakBar.Height = 2;
            peakBar.Width = 0;
            peakBar.Fill = _accent;
            peakBar.HorizontalAlignment = HorizontalAlignment.Left;
            peakBar.Margin = new Thickness(34, 2, 0, 0);
            Grid.SetRow(peakBar, 2);
            grid.Children.Add(peakBar);

            // ── 展开区（参照同类音量工具的 AppVolumeView）：输出设备下拉 + 锁定音量三段 ──
            Border settings = new Border();
            settings.Visibility = Visibility.Collapsed;
            settings.Margin = new Thickness(34, 4, 0, 0);
            settings.Padding = new Thickness(12, 2, 0, 2);
            settings.BorderThickness = new Thickness(1, 0, 0, 0);
            settings.SetResourceReference(Border.BorderBrushProperty, "MenuBorder");

            StackPanel settingsStack = new StackPanel();

            Button devBtn = new Button();
            devBtn.Style = (Style)FindResource("RowDeviceButton");
            devBtn.HorizontalContentAlignment = HorizontalAlignment.Stretch;

            Grid devGrid = new Grid();
            devGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            devGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            devGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock devIcon = new TextBlock();
            devIcon.Text = "\uE7F6";
            devIcon.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            devIcon.FontSize = 14;
            devIcon.VerticalAlignment = VerticalAlignment.Center;
            devIcon.Margin = new Thickness(0, 0, 8, 0);
            devIcon.Foreground = _gray;
            devGrid.Children.Add(devIcon);

            TextBlock devText = new TextBlock();
            devText.FontSize = 12;
            devText.VerticalAlignment = VerticalAlignment.Center;
            devText.TextTrimming = TextTrimming.CharacterEllipsis;
            // 在 Button 内，SetTextColor 会跳过 Button 子树，必须显式着色（否则深色下是黑字黑底）
            devText.Foreground = _config.DarkTheme ? Brushes.White : Brushes.Black;
            Grid.SetColumn(devText, 1);
            devGrid.Children.Add(devText);

            TextBlock devArrow = new TextBlock();
            devArrow.Text = "\uE70D";
            devArrow.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            devArrow.FontSize = 10;
            devArrow.VerticalAlignment = VerticalAlignment.Center;
            devArrow.Foreground = _gray;
            devArrow.Margin = new Thickness(6, 0, 0, 0);
            Grid.SetColumn(devArrow, 2);
            devGrid.Children.Add(devArrow);

            devBtn.Content = devGrid;
            devBtn.Margin = new Thickness(0, 8, 0, 0);
            devBtn.Click += delegate { ShowDeviceMenu(row, devBtn, false); };
            settingsStack.Children.Add(devBtn);
            row.DeviceButton = devBtn;
            row.DeviceButtonText = devText;

            // 输入设备（麦克风）下拉：同 Win11「音量合成器」每个应用的输入设备
            Button inBtn = new Button();
            inBtn.Style = (Style)FindResource("RowDeviceButton");
            inBtn.HorizontalContentAlignment = HorizontalAlignment.Stretch;

            Grid inGrid = new Grid();
            inGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock inIcon = new TextBlock();
            inIcon.Text = "\uE720";
            inIcon.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            inIcon.FontSize = 14;
            inIcon.VerticalAlignment = VerticalAlignment.Center;
            inIcon.Margin = new Thickness(0, 0, 8, 0);
            inIcon.Foreground = _gray;
            inGrid.Children.Add(inIcon);

            TextBlock inText = new TextBlock();
            inText.FontSize = 12;
            inText.VerticalAlignment = VerticalAlignment.Center;
            inText.TextTrimming = TextTrimming.CharacterEllipsis;
            inText.Foreground = _config.DarkTheme ? Brushes.White : Brushes.Black;
            Grid.SetColumn(inText, 1);
            inGrid.Children.Add(inText);

            TextBlock inArrow = new TextBlock();
            inArrow.Text = "\uE70D";
            inArrow.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            inArrow.FontSize = 10;
            inArrow.VerticalAlignment = VerticalAlignment.Center;
            inArrow.Foreground = _gray;
            inArrow.Margin = new Thickness(6, 0, 0, 0);
            Grid.SetColumn(inArrow, 2);
            inGrid.Children.Add(inArrow);

            inBtn.Content = inGrid;
            inBtn.Margin = new Thickness(0, 6, 0, 0);
            inBtn.Click += delegate { ShowDeviceMenu(row, inBtn, true); };
            settingsStack.Children.Add(inBtn);
            row.InputButton = inBtn;
            row.InputButtonText = inText;

            StackPanel lockStack = new StackPanel();

            TextBlock lockHint = new TextBlock();
            lockHint.Text = "锁定音量";
            lockHint.FontSize = 11;
            lockHint.Foreground = _gray;
            lockHint.Margin = new Thickness(2, 12, 0, 6);
            lockStack.Children.Add(lockHint);

            Border lockBox = new Border();
            lockBox.Height = 30;
            lockBox.CornerRadius = new CornerRadius(6);
            lockBox.BorderThickness = new Thickness(1);
            lockBox.SetResourceReference(Border.BorderBrushProperty, "SegmentBorder");
            lockBox.SetResourceReference(Border.BackgroundProperty, "MenuBackground");

            Grid segGrid = new Grid();
            segGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            segGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            segGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            segGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            segGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Button offBtn = MakeSegment("关", "\uE785");
            Button untilBtn = MakeSegment("直到关闭", "\uE72E");
            Button alwaysBtn = MakeSegment("始终", "\uE718");
            offBtn.Click += delegate { SetRowLock(row, 0); };
            untilBtn.Click += delegate { SetRowLock(row, VolumeLock.ModeUntilClose); };
            alwaysBtn.Click += delegate { SetRowLock(row, VolumeLock.ModeAlways); };
            segGrid.Children.Add(offBtn);
            Grid.SetColumn(offBtn, 0);
            Border sep1 = MakeSegmentSeparator();
            Grid.SetColumn(sep1, 1);
            segGrid.Children.Add(sep1);
            segGrid.Children.Add(untilBtn);
            Grid.SetColumn(untilBtn, 2);
            Border sep2 = MakeSegmentSeparator();
            Grid.SetColumn(sep2, 3);
            segGrid.Children.Add(sep2);
            segGrid.Children.Add(alwaysBtn);
            Grid.SetColumn(alwaysBtn, 4);

            lockBox.Child = segGrid;
            lockStack.Children.Add(lockBox);
            settingsStack.Children.Add(lockStack);
            row.LockOffButton = offBtn;
            row.LockUntilButton = untilBtn;
            row.LockAlwaysButton = alwaysBtn;

            // 系统声音没有真实进程：参照同类音量工具，路由与锁定整块隐藏
            if (!canRoute)
            {
                devBtn.Visibility = Visibility.Collapsed;
                inBtn.Visibility = Visibility.Collapsed;
                lockStack.Visibility = Visibility.Collapsed;
            }

            settings.Child = settingsStack;
            Grid.SetRow(settings, 3);
            grid.Children.Add(settings);
            row.SettingsPanel = settings;

            row.Row = grid;
            grid.Tag = row;
            row.Name = name;
            row.Volume = slider;
            row.VolumeText = volText;
            row.PeakBar = peakBar;
            row.Muted = info.IsMuted;

            ContextMenu rowMenu = new ContextMenu();
            MenuItem muteItem = new MenuItem();
            muteItem.Header = info.IsMuted ? "取消静音" : "静音";
            muteItem.Click += delegate
            {
                ToggleMute(row);
                muteItem.Header = row.Muted ? "取消静音" : "静音";
            };
            rowMenu.Items.Add(muteItem);
            MenuItem balanceItem = new MenuItem();
            balanceItem.Header = "声道平衡...";
            balanceItem.Click += delegate { ShowBalance(row); };
            rowMenu.Items.Add(balanceItem);
            grid.ContextMenu = rowMenu;
            moreBtn.Click += delegate
            {
                rowMenu.PlacementTarget = moreBtn;
                rowMenu.IsOpen = true;
            };

            _restoring = true;
            try
            {
                slider.Value = Math.Round(info.Volume * 100);
                volText.Text = ((int)Math.Round(info.Volume * 100)) + "%";
            }
            finally { _restoring = false; }

            slider.ValueChanged += delegate
            {
                if (_restoring || row.Muted) return;
                try
                {
                    Guid g = EmptyGuid;
                    float v = (float)(slider.Value / 100.0);
                    foreach (ISimpleAudioVolume c in row.Controls) { try { c.SetMasterVolume(v, ref g); } catch { } }
                    volText.Text = ((int)Math.Round(slider.Value)) + "%";
                    // 已锁定时拖动滑块 = 更新锁定值
                    VolumeLock lk;
                    if (!string.IsNullOrEmpty(row.ProcessName) && _config.LockedVolumes.TryGetValue(row.ProcessName, out lk))
                    {
                        lk.Volume = v;
                    }
                }
                catch { }
            };

            VolumeLock initialLock;
            row.LockMode = (!string.IsNullOrEmpty(info.ProcessName) && _config.LockedVolumes.TryGetValue(info.ProcessName, out initialLock)) ? initialLock.Mode : 0;
            UpdateLockVisual(row);
            if (row.DeviceButtonText != null) row.DeviceButtonText.Text = DeviceDisplayText(row, false);
            if (row.InputButtonText != null) row.InputButtonText.Text = DeviceDisplayText(row, true);
            return row;
        }

        private Button MakeSegment(string text, string glyph)
        {
            Button b = new Button();
            b.Style = (Style)FindResource("SegmentButton");
            System.Windows.Automation.AutomationProperties.SetName(b, text);
            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;
            sp.HorizontalAlignment = HorizontalAlignment.Center;
            TextBlock ic = new TextBlock();
            ic.Text = glyph;
            ic.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
            ic.FontSize = 13;
            ic.Margin = new Thickness(0, 0, 6, 0);
            ic.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.Add(ic);
            TextBlock tx = new TextBlock();
            tx.Text = text;
            tx.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.Add(tx);
            b.Content = sp;
            return b;
        }

        private Border MakeSegmentSeparator()
        {
            Border sep = new Border();
            sep.Width = 1;
            sep.Margin = new Thickness(0, 5, 0, 5);
            sep.SetResourceReference(Border.BackgroundProperty, "SegmentBorder");
            return sep;
        }

    }
}
