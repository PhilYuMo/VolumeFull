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
        // ===== 全局热键：低级键盘钩子 =====
        // 照同类音量工具的 SharpHookHotkeyManager 做法：不用 RegisterHotKey（组合被别的程序占用时会注册失败，
        // 而且只能独占），改为 WH_KEYBOARD_LL 低级键盘钩子——永远能收到按键，也不吞掉别的程序的快捷键。
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private sealed class HotkeyDef
        {
            public uint Mods;
            public uint Vk;
            public string Text;
        }

        private IntPtr _keyboardHook = IntPtr.Zero;
        private LowLevelKeyboardProc _keyboardProc; // 必须保持引用，否则委托会被 GC 回收导致钩子回调崩溃
        private readonly object _hotkeyLock = new object();
        private readonly Dictionary<int, HotkeyDef> _hotkeys = new Dictionary<int, HotkeyDef>();

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // 兼容旧的 WM_HOTKEY 路径（现在热键走键盘钩子，这里保留不冲突）
            if (msg == 0x0312)
            {
                HandleHotkey(wParam.ToInt32());
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void RegisterHotkeys()
        {
            lock (_hotkeyLock)
            {
                _hotkeys.Clear();
                if (_config.EnableHotkeys)
                {
                    CacheHotkey(HotkeyIdVolumeUp, _config.HotkeyVolumeUp);
                    CacheHotkey(HotkeyIdVolumeDown, _config.HotkeyVolumeDown);
                    CacheHotkey(HotkeyIdMute, _config.HotkeyMute);
                    CacheHotkey(HotkeyIdPanel, _config.HotkeyPanel);
                    CacheHotkey(HotkeyIdNextDevice, _config.HotkeyNextDevice);
                }
            }
            if (_config.EnableHotkeys) InstallKeyboardHook();
        }

        private void CacheHotkey(int id, string text)
        {
            uint mods, vk;
            if (!HotKeyHelper.TryParse(text, out mods, out vk)) return;
            HotkeyDef def = new HotkeyDef();
            def.Mods = mods;
            def.Vk = vk;
            def.Text = text;
            _hotkeys[id] = def;
        }

        private void InstallKeyboardHook()
        {
            if (_keyboardHook != IntPtr.Zero) return;
            try
            {
                _keyboardProc = KeyboardHookCallback;
                IntPtr hMod = GetModuleHandle(null);
                _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
                if (_keyboardHook == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    LogError(new InvalidOperationException("SetWindowsHookEx(WH_KEYBOARD_LL) 失败，错误码 " + err));
                }
            }
            catch (Exception ex) { LogError(ex); }
        }

        private void UninstallKeyboardHook()
        {
            if (_keyboardHook == IntPtr.Zero) return;
            try { UnhookWindowsHookEx(_keyboardHook); } catch { }
            _keyboardHook = IntPtr.Zero;
            _keyboardProc = null;
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            bool swallow = false;
            try
            {
                if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
                {
                    KBDLLHOOKSTRUCT info = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                    int id = MatchHotkey(info.vkCode);
                    if (id != 0)
                    {
                        // 钩子在输入线程上跑，动作交回 UI 线程（同同类音量工具的 SynchronizationContext.Post）
                        Dispatcher.BeginInvoke(new Action(delegate { HandleHotkey(id); }));
                        // 独占模式：吞掉按键，其他程序（比如也用 Ctrl+Alt+Up 的网易云音乐）收不到
                        swallow = _config.ExclusiveHotkeys;
                    }
                }
            }
            catch { }
            if (swallow) return (IntPtr)1;
            return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        /// <summary>按当前按下的修饰键匹配热键，返回热键 id；不匹配返回 0。</summary>
        private int MatchHotkey(uint vk)
        {
            lock (_hotkeyLock)
            {
                if (_hotkeys.Count == 0) return 0;
                uint mods = 0;
                if (IsKeyDown(0x11)) mods |= 0x2;                    // Ctrl
                if (IsKeyDown(0x12)) mods |= 0x1;                    // Alt
                if (IsKeyDown(0x10)) mods |= 0x4;                    // Shift
                if (IsKeyDown(0x5B) || IsKeyDown(0x5C)) mods |= 0x8; // Win
                foreach (KeyValuePair<int, HotkeyDef> kv in _hotkeys)
                {
                    if (kv.Value.Mods == mods && kv.Value.Vk == vk) return kv.Key;
                }
                return 0;
            }
        }

        private static bool IsKeyDown(int vk)
        {
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        private void UnregisterHotkeys()
        {
            lock (_hotkeyLock) { _hotkeys.Clear(); }
        }

        private void HandleHotkey(int id)
        {
            float volumeStep = Math.Max(1, Math.Min(100, _config.VolumeStep)) / 100f;
            if (id == HotkeyIdVolumeUp) ChangeDefaultVolume(volumeStep);
            else if (id == HotkeyIdVolumeDown) ChangeDefaultVolume(-volumeStep);
            else if (id == HotkeyIdMute) ToggleDefaultMute();
            else if (id == HotkeyIdPanel) ShowPanel();
            else if (id == HotkeyIdNextDevice) SwitchToNextDevice();
        }

        private void ChangeDefaultVolume(float delta)
        {
            string deviceId = Audio.GetDefaultOutputDeviceId();
            IAudioEndpointVolume ep = Audio.GetEndpointVolumeForDevice(deviceId);
            if (ep == null) return;
            try
            {
                float cur = Audio.GetEndpointVolumeScalar(ep);
                Audio.SetEndpointVolumeScalar(ep, cur + delta);
            }
            finally { try { Marshal.ReleaseComObject(ep); } catch { } }
            RefreshAll(false);
        }

        private void ToggleDefaultMute()
        {
            string deviceId = Audio.GetDefaultOutputDeviceId();
            IAudioEndpointVolume ep = Audio.GetEndpointVolumeForDevice(deviceId);
            if (ep == null) return;
            try
            {
                bool mute = Audio.GetEndpointMute(ep);
                Audio.SetEndpointMute(ep, !mute);
            }
            finally { try { Marshal.ReleaseComObject(ep); } catch { } }
            RefreshAll(false);
        }

        private void ToggleMicMute()
        {
            IAudioEndpointVolume mic = Audio.GetDefaultInputEndpointVolume();
            if (mic == null) return;
            try
            {
                bool mute = Audio.GetEndpointMute(mic);
                Audio.SetEndpointMute(mic, !mute);
            }
            finally { try { Marshal.ReleaseComObject(mic); } catch { } }
        }

        private void SwitchToNextDevice()
        {
            if (_devices.Count < 2) return;
            string current = Audio.GetDefaultOutputDeviceId();
            int index = 0;
            for (int i = 0; i < _devices.Count; i++)
            {
                if (_devices[i].Id == current) { index = i; break; }
            }
            int next = (index + 1) % _devices.Count;
            Audio.SetDefaultDevice(_devices[next].Id);
            RefreshAll(true);
        }

    }
}
