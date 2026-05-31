using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ClipboardPro.Models;
using ClipboardPro.ViewModels;

namespace ClipboardPro.Helpers
{
    public static class PasteHelper
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        
        private const int KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;
        private const byte VK_MENU = 0x12; // Alt
        private const byte VK_TAB = 0x09;

        public static async Task PasteToActiveWindow(ClipboardItem item, MainViewModel vm, System.Windows.Window currentWindow)
        {
            // 1. Copy item to clipboard
            vm.CopyItem(item);

            // 2. Give the clipboard a moment to settle (critical for first-time use)
            await Task.Delay(100);

            // 3. Temporarily set Topmost=False on the window so focus can leave it
            bool originalTopmost = currentWindow.Topmost;
            currentWindow.Topmost = false;

            // 4. Hide/Deactivate current window briefly if it's the main window to ensure focus return
            // For the Quick Paste Bar, we just let the Alt+Tab handle it.
            
            // 5. Alt+Tab to return focus to the previous application
            keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
            keybd_event(VK_TAB, 0, 0, UIntPtr.Zero);
            await Task.Delay(20);
            keybd_event(VK_TAB, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            // 6. Increase delay to let Windows process the focus switch reliably (from 100ms to 400ms)
            await Task.Delay(400);

            // 7. Send Ctrl+V
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_V, 0, 0, UIntPtr.Zero);
            await Task.Delay(60);
            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            // 8. Restore window's original state and bring focus back
            await Task.Delay(300);
            currentWindow.Topmost = originalTopmost;
            currentWindow.Activate();
        }
    }
}
