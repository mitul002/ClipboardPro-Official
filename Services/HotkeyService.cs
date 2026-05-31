using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ClipboardPro.Services
{
    public class HotkeyService : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_MAIN   = 9001;
        private const int HOTKEY_MINI_MODE  = 9002;
        private const int HOTKEY_QUICK_PASTE_BAR   = 9003;

        // Modifier flags
        private const uint MOD_ALT     = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT   = 0x0004;
        private const uint MOD_WIN     = 0x0008;

        private HwndSource? _source;

        public event Action? OnMainWindowHotkey;
        public event Action? OnMiniModeHotkey;
        public event Action? OnQuickPasteBarHotkey;

        public void Register(Window window, string mainHotkey, string miniModeHotkey, string quickPasteBarHotkey)
        {
            var helper = new WindowInteropHelper(window);
            helper.EnsureHandle(); // Ensure handle is created
            _source = HwndSource.FromHwnd(helper.Handle);
            _source?.AddHook(HwndHook);

            RegisterStringHotkey(helper.Handle, HOTKEY_MAIN, mainHotkey);
            RegisterStringHotkey(helper.Handle, HOTKEY_MINI_MODE, miniModeHotkey);
            RegisterStringHotkey(helper.Handle, HOTKEY_QUICK_PASTE_BAR, quickPasteBarHotkey);
        }

        private void RegisterStringHotkey(IntPtr handle, int id, string hotkey)
        {
            if (string.IsNullOrEmpty(hotkey) || hotkey == "None") return;

            uint modifiers = 0;
            uint key = 0;

            var parts = hotkey.Split('+');
            foreach (var part in parts)
            {
                if (part == "Ctrl") modifiers |= MOD_CONTROL;
                else if (part == "Alt") modifiers |= MOD_ALT;
                else if (part == "Shift") modifiers |= MOD_SHIFT;
                else if (part == "Win") modifiers |= MOD_WIN;
                else
                {
                    if (Enum.TryParse<System.Windows.Forms.Keys>(part, true, out var k))
                        key = (uint)k;
                }
            }

            if (key != 0) RegisterHotKey(handle, id, modifiers, key);
        }

        public void Unregister()
        {
            if (_source == null) return;
            UnregisterHotKey(_source.Handle, HOTKEY_MAIN);
            UnregisterHotKey(_source.Handle, HOTKEY_MINI_MODE);
            UnregisterHotKey(_source.Handle, HOTKEY_QUICK_PASTE_BAR);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_MAIN)  { OnMainWindowHotkey?.Invoke(); handled = true; }
                else if (id == HOTKEY_MINI_MODE) { OnMiniModeHotkey?.Invoke(); handled = true; }
                else if (id == HOTKEY_QUICK_PASTE_BAR)  { OnQuickPasteBarHotkey?.Invoke(); handled = true; }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister();
            _source?.RemoveHook(HwndHook);
        }
    }
}
