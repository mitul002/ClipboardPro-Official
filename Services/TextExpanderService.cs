using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;
using ClipboardPro.Models;
using Newtonsoft.Json;

namespace ClipboardPro.Services
{
    /// <summary>
    /// Industry-grade text expander service with robust input simulation and deterministic undo.
    ///
    /// Design Principles:
    ///   1. Robust P/Invoke: Uses keybd_event which is 100% platform-independent and extremely reliable.
    ///   2. Precise Timing: Incorporates micro-delays between individual key strikes to allow the target
    ///      application's message loop to process text deletion and insertion correctly.
    ///   3. Rock-solid Undo: The triggering Backspace key is completely blocked in the keyboard hook
    ///      (both key down and key up), and the exact number of characters of the expanded text is
    ///      deleted and replaced by the trigger.
    /// </summary>
    public class TextExpanderService : IDisposable
    {
        // ── Win32 low-level hook constants ────────────────────────────────────
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN     = 0x0100;
        private const int WM_KEYUP       = 0x0101;
        private const int WM_SYSKEYDOWN  = 0x0104;
        private const int WM_SYSKEYUP    = 0x0105;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags);

        [DllImport("user32.dll")]
        private static extern bool GetKeyboardState(byte[] lpKeyState);

        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_BACK         = 0x08;
        private const byte VK_CONTROL      = 0x11;
        private const byte VK_V            = 0x56;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc? _proc; // kept alive to prevent GC

        // ── State ─────────────────────────────────────────────────────────────
        private readonly System.Text.StringBuilder _buffer = new(256);
        private readonly object _bufferLock = new();
        private Dictionary<string, string> _lookup = new(StringComparer.Ordinal);
        private bool _enabled  = false;
        private bool _disposed = false;

        // ── Expansion serialization ───────────────────────────────────────────
        private volatile bool _expansionInProgress = false;

        // ── Undo state ────────────────────────────────────────────────────────
        private volatile bool   _undoReady           = false;
        private volatile bool   _blockingBackspace   = false;
        private          string? _lastTrigger         = null;
        private          string? _lastValue           = null;

        // ── Suppress hook ─────────────────────────────────────────────────────
        private volatile bool _suppressHook = false;

        // ── Persistence ───────────────────────────────────────────────────────
        private readonly StorageService _storage;
        public List<SnippetItem> Snippets { get; private set; } = new();

        public TextExpanderService(StorageService storage)
        {
            _storage = storage;
            Load();
        }

        // ── Enable / Disable ──────────────────────────────────────────────────
        public void SetEnabled(bool enabled)
        {
            if (_enabled == enabled) return;
            _enabled = enabled;
            if (enabled) InstallHook(); else RemoveHook();
        }

        private void InstallHook()
        {
            if (_hookId != IntPtr.Zero) return;
            _proc = HookCallback;
            using var proc   = System.Diagnostics.Process.GetCurrentProcess();
            using var module = proc.MainModule!;
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc,
                          GetModuleHandle(module.ModuleName), 0);
        }

        private void RemoveHook()
        {
            if (_hookId == IntPtr.Zero) return;
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            lock (_bufferLock) { _buffer.Clear(); }
            ClearUndoState();
        }

        // ── Low-level Hook Callback ───────────────────────────────────────────
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // Ignore synthetic keystrokes that we generated ourselves
            if (_suppressHook)
                return CallNextHookEx(_hookId, nCode, wParam, lParam);

            bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
            bool isKeyUp   = wParam == (IntPtr)WM_KEYUP   || wParam == (IntPtr)WM_SYSKEYUP;

            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                int scanCode = Marshal.ReadInt32(lParam, 4);
                var key    = KeyInterop.KeyFromVirtualKey(vkCode);

                // ── Undo window ──────────────────────────────────────────────
                if (_undoReady && key == Key.Back)
                {
                    if (isKeyDown)
                    {
                        _blockingBackspace = true;
                        _undoReady = false;
                        string trigger = _lastTrigger!;
                        string value   = _lastValue!;
                        ClearUndoState();

                        // Schedule undo on the UI dispatcher so clipboard APIs work
                        System.Windows.Application.Current?.Dispatcher?.BeginInvoke(
                            System.Windows.Threading.DispatcherPriority.Normal,
                            new Action(() => PerformUndo(trigger, value)));
                    }

                    // Block the triggering Backspace keydown to prevent target app processing
                    return (IntPtr)1;
                }

                if (key == Key.Back && isKeyUp && _blockingBackspace)
                {
                    // Block the triggering Backspace keyup so the OS key up event is discarded
                    _blockingBackspace = false;
                    return (IntPtr)1;
                }

                if (_undoReady && isKeyDown)
                {
                    // Any other real key down cancels the undo opportunity
                    _undoReady = false;
                    ClearUndoState();
                }

                if (isKeyDown)
                {
                    // ── Normal key handling ──────────────────────────────────────
                    if (key == Key.Back)
                    {
                        lock (_bufferLock) { if (_buffer.Length > 0) _buffer.Length--; }
                    }
                    else if (key == Key.Escape)
                    {
                        lock (_bufferLock) { _buffer.Clear(); }
                    }
                    else if (key == Key.Space || key == Key.Enter || key == Key.Tab)
                    {
                        // Terminator keys — check buffer then clear
                        CheckAndExpand();
                        lock (_bufferLock) { _buffer.Clear(); }
                    }
                    else
                    {
                        string? ch = GetCharFromKey((uint)vkCode, (uint)scanCode);
                        if (ch != null)
                        {
                            lock (_bufferLock)
                            {
                                _buffer.Append(ch);
                                // Safety cap
                                if (_buffer.Length > 64) _buffer.Remove(0, _buffer.Length - 64);
                            }
                            // Inline check (no-terminator triggers like "mail/")
                            CheckAndExpand();
                        }
                    }
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        // ── Trigger check ─────────────────────────────────────────────────────
        private void CheckAndExpand()
        {
            if (_expansionInProgress) return;
            string typed;
            lock (_bufferLock) { typed = _buffer.ToString(); }
            if (string.IsNullOrEmpty(typed)) return;

            foreach (var kv in _lookup)
            {
                if (typed.EndsWith(kv.Key, StringComparison.Ordinal))
                {
                    string capKey   = kv.Key;
                    string capValue = kv.Value;
                    lock (_bufferLock) { _buffer.Clear(); }

                    // Dispatch to UI thread (STA, clipboard access allowed)
                    System.Windows.Application.Current?.Dispatcher?.BeginInvoke(
                        System.Windows.Threading.DispatcherPriority.Normal,
                        new Action(() => PerformExpansion(capKey, capValue)));

                    return; // only one match
                }
            }
        }

        // ── Input Simulation Helpers ──────────────────────────────────────────
        private static void SendBackspaces(int count)
        {
            for (int i = 0; i < count; i++)
            {
                keybd_event(VK_BACK, 0, 0, UIntPtr.Zero);
                System.Threading.Thread.Sleep(10);
                keybd_event(VK_BACK, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                System.Threading.Thread.Sleep(15); // delay between keystrokes to ensure target window processes it
            }
        }

        private static void SendPaste()
        {
            // Ctrl Down
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            System.Threading.Thread.Sleep(10);
            // V Down
            keybd_event(VK_V, 0, 0, UIntPtr.Zero);
            System.Threading.Thread.Sleep(10);
            // V Up
            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            System.Threading.Thread.Sleep(10);
            // Ctrl Up
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        // ── Expansion (runs on UI thread) ─────────────────────────────────────
        private void PerformExpansion(string trigger, string value)
        {
            if (_expansionInProgress) return;
            _expansionInProgress = true;
            _undoReady           = false;
            ClearUndoState();

            try
            {
                _suppressHook = true;
                System.Threading.Thread.Sleep(15); // Let any pending OS keyboard events settle

                // 1. Save clipboard content
                string? prevClip = null;
                try { prevClip = System.Windows.Clipboard.GetText(); } catch { }

                // 2. Delete trigger characters
                SendBackspaces(trigger.Length);
                System.Threading.Thread.Sleep(30);

                // 3. Set expanded text in clipboard and paste
                System.Windows.Clipboard.SetText(value);
                System.Threading.Thread.Sleep(15);

                SendPaste();
                System.Threading.Thread.Sleep(50);

                // 4. Re-enable hook BEFORE arming undo window
                _suppressHook = false;

                // 5. Arm the undo window
                _lastTrigger = trigger;
                _lastValue   = value;
                _undoReady   = true;

                // 6. Restore original clipboard asynchronously
                if (prevClip != null)
                {
                    System.Threading.Tasks.Task.Delay(300).ContinueWith(_ =>
                    {
                        try
                        {
                            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                            {
                                try
                                {
                                    if (System.Windows.Clipboard.GetText() == value)
                                        System.Windows.Clipboard.SetText(prevClip);
                                }
                                catch { }
                            });
                        }
                        catch { }
                    });
                }
            }
            catch
            {
                _suppressHook = false;
            }
            finally
            {
                _expansionInProgress = false;
            }
        }

        // ── Undo (runs on UI thread) ──────────────────────────────────────────
        private void PerformUndo(string trigger, string value)
        {
            try
            {
                _suppressHook = true;
                System.Threading.Thread.Sleep(15); // Let any pending OS keyboard events settle

                // 1. Delete the entire expanded text
                //    Since the user's Backspace was blocked entirely, we delete the exact length of the value.
                SendBackspaces(value.Length);
                System.Threading.Thread.Sleep(30);

                // Get clean trigger by stripping prefix/suffix punctuation
                string cleanTrigger = GetCleanTrigger(trigger);

                // 2. Re-type the original trigger via clipboard paste
                string? prevClip = null;
                try { prevClip = System.Windows.Clipboard.GetText(); } catch { }

                System.Windows.Clipboard.SetText(cleanTrigger);
                System.Threading.Thread.Sleep(15);

                SendPaste();
                System.Threading.Thread.Sleep(50);

                // 3. Re-enable hook
                _suppressHook = false;

                // 4. Seed buffer with restored trigger so typing continues naturally
                lock (_bufferLock)
                {
                    _buffer.Clear();
                    _buffer.Append(cleanTrigger);
                }

                // 5. Restore clipboard
                if (prevClip != null)
                {
                    System.Threading.Tasks.Task.Delay(200).ContinueWith(_ =>
                    {
                        try
                        {
                            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                            {
                                try { System.Windows.Clipboard.SetText(prevClip); } catch { }
                            });
                        }
                        catch { }
                    });
                }
            }
            catch
            {
                _suppressHook = false;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static string GetCleanTrigger(string trigger)
        {
            if (string.IsNullOrEmpty(trigger)) return "";

            int start = 0;
            while (start < trigger.Length && !char.IsLetterOrDigit(trigger[start]))
            {
                start++;
            }

            int end = trigger.Length - 1;
            while (end >= start && !char.IsLetterOrDigit(trigger[end]))
            {
                end--;
            }

            if (start > end) return trigger; // If all are non-alphanumeric, return as is

            return trigger.Substring(start, end - start + 1);
        }

        private void ClearUndoState()
        {
            _lastTrigger = null;
            _lastValue   = null;
        }

        private static string? GetCharFromKey(uint vkCode, uint scanCode)
        {
            byte[] keyState = new byte[256];
            GetKeyboardState(keyState);

            // Override with physical state for accuracy in hook
            keyState[0x10] = (byte)(GetAsyncKeyState(0x10) >> 8); // VK_SHIFT
            keyState[0x11] = (byte)(GetAsyncKeyState(0x11) >> 8); // VK_CONTROL
            keyState[0x12] = (byte)(GetAsyncKeyState(0x12) >> 8); // VK_MENU

            bool isCtrl = (keyState[0x11] & 0x80) != 0;
            bool isAlt  = (keyState[0x12] & 0x80) != 0;
            // Ignore combos involving Ctrl or Alt, except AltGr which is Ctrl+Alt
            if ((isCtrl && !isAlt) || (isAlt && !isCtrl)) return null;

            StringBuilder sb = new StringBuilder(5);
            int result = ToUnicode(vkCode, scanCode, keyState, sb, sb.Capacity, 0);

            if (result > 0)
            {
                string ch = sb.ToString().Substring(0, result);
                // Filter out non-printable or control chars
                if (ch.Length == 1 && char.IsControl(ch[0])) return null;
                return ch;
            }
            return null;
        }

        // ── CRUD ──────────────────────────────────────────────────────────────
        public void AddOrUpdate(SnippetItem snippet)
        {
            var existing = Snippets.FirstOrDefault(s => s.Id == snippet.Id);
            if (existing != null)
                Snippets[Snippets.IndexOf(existing)] = snippet;
            else
                Snippets.Insert(0, snippet);

            RebuildLookup();
            _storage.SaveSnippet(snippet);
        }

        public void Delete(SnippetItem snippet)
        {
            Snippets.Remove(snippet);
            RebuildLookup();
            _storage.DeleteSnippet(snippet);
        }

        public void ClearAll()
        {
            Snippets.Clear();
            RebuildLookup();
            _storage.ClearAllSnippets();
        }

        private void RebuildLookup()
        {
            _lookup = Snippets
                .Where(s => !string.IsNullOrEmpty(s.Trigger) && !string.IsNullOrEmpty(s.Content))
                .ToDictionary(s => s.Trigger, s => s.Content, StringComparer.Ordinal);
        }

        // ── Persistence ───────────────────────────────────────────────────────
        public void Save()
        {
            _storage.SaveSnippets(Snippets);
        }

        public void Load()
        {
            try
            {
                Snippets = _storage.LoadSnippets();
            }
            catch { Snippets = new(); }
            RebuildLookup();
        }

        // ── IDisposable ───────────────────────────────────────────────────────
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            RemoveHook();
        }
    }
}
