using System;
using System.Runtime.InteropServices;

namespace VolumeCtrlWpf
{
    internal static class HotKeyHelper
    {
        private const int ProbeId = 0x5F5E;
        private const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        /// <summary>
        /// 照同类音量工具的 HotkeyProbe：用 RegisterHotKey 试注册一下，失败且错误码为 1409 说明组合已被
        /// 其他程序（通过 RegisterHotKey）占用。必须在有消息队列的线程（UI 线程）调用。
        /// </summary>
        public static bool IsRegisteredElsewhere(uint modifiers, uint vk)
        {
            try
            {
                if (RegisterHotKey(IntPtr.Zero, ProbeId, modifiers | 0x4000 /*MOD_NOREPEAT*/, vk))
                {
                    UnregisterHotKey(IntPtr.Zero, ProbeId);
                    return false;
                }
                return Marshal.GetLastWin32Error() == ERROR_HOTKEY_ALREADY_REGISTERED;
            }
            catch { return false; }
        }

        public static bool IsRegisteredElsewhere(string text)
        {
            uint mods, vk;
            if (!TryParse(text, out mods, out vk)) return false;
            return IsRegisteredElsewhere(mods, vk);
        }

        public static bool TryParse(string text, out uint modifiers, out uint vk)
        {
            modifiers = 0;
            vk = 0;
            if (string.IsNullOrEmpty(text)) return false;
            string[] parts = text.Split('+');
            string keyName = null;
            foreach (string raw in parts)
            {
                string part = raw.Trim().ToUpperInvariant();
                if (part.Length == 0) continue;
                if (part == "CTRL" || part == "CONTROL") modifiers |= 0x2;
                else if (part == "ALT") modifiers |= 0x1;
                else if (part == "SHIFT") modifiers |= 0x4;
                else if (part == "WIN") modifiers |= 0x8;
                else keyName = part;
            }
            if (keyName == null || modifiers == 0) return false;
            vk = ToVk(keyName);
            return vk != 0;
        }

        private static uint ToVk(string key)
        {
            if (key.Length == 1)
            {
                char c = key[0];
                if (c >= 'A' && c <= 'Z') return c;
                if (c >= '0' && c <= '9') return c;
            }
            if (key.StartsWith("F") && key.Length <= 3)
            {
                int n;
                if (int.TryParse(key.Substring(1), out n) && n >= 1 && n <= 24) return (uint)(0x70 + n - 1);
            }
            switch (key)
            {
                case "UP": return 0x26;
                case "DOWN": return 0x28;
                case "LEFT": return 0x25;
                case "RIGHT": return 0x27;
                case "SPACE": return 0x20;
                case "TAB": return 0x09;
                case "ENTER": return 0x0D;
                case "ESC":
                case "ESCAPE": return 0x1B;
                case "HOME": return 0x24;
                case "END": return 0x23;
                case "PAGEUP": return 0x21;
                case "PAGEDOWN": return 0x22;
                case "INSERT": return 0x2D;
                case "DELETE": return 0x2E;
                case "PAUSE": return 0x13;
                default: return 0;
            }
        }
    }
}

