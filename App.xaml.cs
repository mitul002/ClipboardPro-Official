using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Forms;
using ClipboardPro.Services;
using ClipboardPro.ViewModels;
using ClipboardPro.Views;
using ClipboardPro.Models;
using System.Runtime.InteropServices;
using System.IO.Pipes;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace ClipboardPro
{
    public partial class App : System.Windows.Application
    {
        private NotifyIcon?            _trayIcon;
        private MainWindow?            _mainWindow;
        private StorageService?        _storage;
        private ClipboardMonitorService? _monitor;
        private HotkeyService?         _hotkeys;
        private MainViewModel?         _vm;
        private static System.Threading.Mutex? _appMutex;
        private const string PipeName = "ClipboardPro_SingleInstance_Pipe";
        public static TextExpanderService? TextExpander { get; private set; }
        public static bool IsShuttingDown { get; set; } = false;

        // Trial expiry watcher — stored as field so GC doesn't collect it
        private System.Windows.Threading.DispatcherTimer? _trialWatchTimer;
        private System.Threading.Timer?                   _licenseCheckTimer;
        private bool _trialLockShown = false;  // Guard: prevent double popup

        public App()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                System.IO.File.WriteAllText(@"D:\SAAS PROJECTS\ClipboardPro\error.txt", "Dispatcher: " + e.Exception.ToString());
                System.Diagnostics.Debug.WriteLine($"UI Exception: {e.Exception}");
                Shutdown();
                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                System.IO.File.WriteAllText(@"D:\SAAS PROJECTS\ClipboardPro\error.txt", "AppDomain: " + ex?.ToString());
            };
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            _appMutex = new System.Threading.Mutex(true, "ClipboardPro_Mutex_Global", out bool createdNew);
            if (!createdNew)
            {
                // Check if we should open in share mode
                string command = "RESTORE";
                foreach (var arg in e.Args)
                {
                    if (arg.Equals("--share", StringComparison.OrdinalIgnoreCase))
                    {
                        command = "SHARE";
                        break;
                    }
                }

                SendRemoteCommand(command);
                Shutdown();
                return;
            }

            // Start pipe server to listen for commands from subsequent instances
            StartPipeServer();

            // Register global handler to prevent ComboBox from consuming scroll when closed
            EventManager.RegisterClassHandler(typeof(System.Windows.Controls.ComboBox), System.Windows.Controls.ComboBox.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnComboBoxPreviewMouseWheel));


            base.OnStartup(e);

            // --- LICENSE & TRIAL GATE ---
            try
            {
                var license = new LicenseService();
                var licStatus = license.GetLicenseStatus();

                // Derive appAllowed directly from licStatus (avoids a duplicate GetLicenseStatus() call)
                bool appAllowed = (licStatus.IsLicensed || !licStatus.TrialExpired) && !licStatus.OfflineExpired;

                if (appAllowed)
                {
                    // Always start the trial expiry watcher for ALL users (trial + licensed)
                    StartTrialExpiryWatcher();

                    // Also start online license checker only for licensed users
                    if (license.ReadLicensePayload() != null)
                        StartLicenseValidationTimer();
                }
                else
                {
                    // Trial expired, not licensed, or offline grace period expired → show gate
                    var startReason = licStatus.OfflineExpired
                        ? LockReason.OfflineExpired
                        : LockReason.TrialExpired;
                    var gate = new TrialGateWindow(startReason);
                    if (gate.ShowDialog() != true)
                    {
                        Shutdown();
                        return;
                    }
                    StartLicenseValidationTimer();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Security check failed: {ex.Message}");
                Shutdown();
                return;
            }

            try
            {
                _storage = new StorageService();
                _monitor = new ClipboardMonitorService(_storage);
                _vm      = new MainViewModel(_storage, _monitor);
                _hotkeys = new HotkeyService();

                ApplyTheme(_vm.Settings);

                // Wait for all data to load from disk before creating/showing the window
                await _vm.InitializeAsync();

                _mainWindow = new MainWindow(_vm, _monitor, _hotkeys);

                // Start monitor IMMEDIATELY after window creation to ensure no SS missed during launch
                try
                {
                    _monitor.Start(_mainWindow);
                    _hotkeys.Register(_mainWindow, _vm.Settings.MainWindowHotkey, _vm.Settings.MiniModeHotkey, _vm.Settings.QuickPasteBarHotkey);
                    UpdateQuickDropState();
                    _storage?.SetLaunchOnStartup(_vm.Settings.LaunchOnStartup, _vm.Settings.StartMinimized);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Service start error: {ex.Message}");
                }

                // Wire monitor → ViewModel
                _monitor.OnClipboardChanged += item =>
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => _vm?.AddItem(item));
                };

                _hotkeys.OnMainWindowHotkey += ToggleMainWindow;
                _hotkeys.OnMiniModeHotkey += ShowMiniMode;
                _hotkeys.OnQuickPasteBarHotkey += () => _mainWindow?.ToggleQuickPasteBar();

                AppSettings currentSettings = _vm.Settings;

                System.ComponentModel.PropertyChangedEventHandler settingsHandler = (s, ev) => {
                    if (ev.PropertyName == nameof(AppSettings.QuickDropAction))
                    {
                        Dispatcher.BeginInvoke(new Action(UpdateQuickDropState));
                    }
                    else if (ev.PropertyName == nameof(AppSettings.EnableTextExpander))
                    {
                        TextExpander?.SetEnabled(_vm.Settings.EnableTextExpander);
                    }
                };

                if (currentSettings != null)
                {
                    currentSettings.PropertyChanged += settingsHandler;
                }

                _vm.PropertyChanged += (s, ev) => {
                    if (ev.PropertyName == nameof(MainViewModel.Settings))
                    {
                        // Settings object replaced (e.g. Reset), reattach handler
                        if (currentSettings != null)
                        {
                            currentSettings.PropertyChanged -= settingsHandler;
                        }
                        currentSettings = _vm.Settings;
                        if (currentSettings != null)
                        {
                            currentSettings.PropertyChanged += settingsHandler;
                        }
                        
                        // Force update states
                        Dispatcher.BeginInvoke(new Action(UpdateQuickDropState));
                        TextExpander?.SetEnabled(currentSettings?.EnableTextExpander ?? false);
                    }
                };

                // TextExpander is initialized AFTER the window shows (deferred)
                // so it doesn't block the cold-boot rendering path.
                // See: StartDeferredServices() below.

                try { SetupTrayIcon(); } catch { /* Ignore tray errors for now to allow app to start */ }

                _mainWindow.Topmost = _vm.Settings.AlwaysOnTop;

                bool startInShareMode = false;
                bool startMinimized = false;
                foreach (var arg in e.Args)
                {
                    if (arg.Equals("--share", StringComparison.OrdinalIgnoreCase)) startInShareMode = true;
                    if (arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase)) startMinimized = true;
                }

                if (startInShareMode)
                {
                    // Share mode: start network immediately so devices are visible right away
                    _vm.EnsureNetworkStarted();

                    _mainWindow.Hide();
                    _mainWindow.ShowInTaskbar = false;
                    _mainWindow.WindowState = WindowState.Minimized;
                    
                    var shareWin = new ShareWindow(_vm);
                    shareWin.Closed += (s, ev) => Shutdown();
                    shareWin.Show();
                }
                else if (startMinimized)
                {
                    _mainWindow.Hide();
                    _mainWindow.WindowState = WindowState.Minimized;
                    _mainWindow.ShowInTaskbar = false;
                    // Defer services — user is not actively watching the screen
                    StartDeferredServices();
                }
                else
                {
                    _mainWindow.Show();
                    _mainWindow.Activate();
                    // Defer network + text expander + maintenance after UI is visible
                    StartDeferredServices();
                }
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText(@"D:\SAAS PROJECTS\ClipboardPro\error.txt", ex.ToString());
                Shutdown();
            }
        }

        // ── Deferred Services: called once window is shown or minimized ──────
        // Network + TextExpander start after a short delay so the first frame
        // paints immediately. Maintenance runs 10 s later.
        private void StartDeferredServices()
        {
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(1500); // Allow UI to paint first

                    // Start network service (LAN discovery & receiving)
                    _vm?.EnsureNetworkStarted();

                    // Initialize TextExpander
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (TextExpander == null && _storage != null)
                        {
                            TextExpander = new TextExpanderService(_storage);
                            if (_vm?.Settings.EnableTextExpander == true)
                                TextExpander.SetEnabled(true);
                        }
                    });

                    // Deferred orphaned-image maintenance (10s total from app start)
                    _vm?.StartDeferredMaintenance();
                }
                catch { }
            });
        }

        private void OnComboBoxPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is System.Windows.Controls.ComboBox cb)
            {
                if (cb.IsDropDownOpen)
                {
                    // Allow the event to pass through to the internal ScrollViewer
                    return;
                }

                // IF CLOSED: Pass the scroll event to the nearest ScrollViewer for smooth navigation
                e.Handled = true;
                var parent = System.Windows.Media.VisualTreeHelper.GetParent(cb);
                while (parent != null && !(parent is System.Windows.Controls.ScrollViewer))
                {
                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                }

                if (parent is System.Windows.Controls.ScrollViewer sv)
                {
                    var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                    {
                        RoutedEvent = System.Windows.UIElement.MouseWheelEvent,
                        Source = sender
                    };
                    sv.RaiseEvent(eventArg);
                }
            }
        }

        private QuickDropWindow? _quickDrop;
        public ClipboardMonitorService? GetMonitor() => _monitor;

        public void UpdateQuickDropState()
        {
            if (_vm != null && _vm.Settings.QuickDropAction > 0)
            {
                if (_quickDrop == null)
                {
                    _quickDrop = new QuickDropWindow(_vm, () => {
                        switch (_vm.Settings.QuickDropAction)
                        {
                            case 2: ShowMiniMode(); break; // Mini Mode (previously Quick Paste)
                            case 3: _mainWindow?.ToggleQuickPasteBar(); break; // Quick Paste Bar
                            case 4: ToggleShareWindow(); break; // Local Share
                            case 5: _quickDrop?.ToggleShelf(); break; // Temporary Shelf
                            default: ToggleMainWindow(); break; // Main Window
                        }
                    });
                    _quickDrop.Show();
                }
                UpdateQuickDropIcon();
            }
            else
            {
                _quickDrop?.Close();
                _quickDrop = null;
            }
        }

        private void UpdateQuickDropIcon()
        {
            if (_quickDrop == null || _vm == null) return;
            string icon = _vm.Settings.QuickDropAction switch
            {
                2 => "\uE71D", // Mini Mode (List)
                3 => "\uE945", // Quick Paste Bar (Thunder)
                4 => "\uEC26", // Local Share (Share)
                5 => "\uE8B7", // Temporary Shelf (Folder)
                _ => "\uE737"  // Main Window (Favicon)
            };
            _quickDrop.UpdateIcon(icon);
        }

        private ShareWindow? _shareWindow;
        private void ToggleShareWindow()
        {
            if (_shareWindow != null && _shareWindow.IsLoaded)
            {
                _shareWindow.Close();
                _shareWindow = null;
            }
            else
            {
                // Ensure network is started before showing share window
                _vm?.EnsureNetworkStarted();
                _shareWindow = new ShareWindow(_vm!);
                _shareWindow.Show();
                _shareWindow.Activate();
            }
        }

        public static void ApplyTheme(ClipboardPro.Models.AppSettings s)
        {
            bool isDark = s.ThemeMode == 1;

            if (s.ThemeMode == 2) // System Mode
            {
                try
                {
                    using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                    var val = key?.GetValue("AppsUseLightTheme");
                    isDark = (val != null && (int)val == 0);
                }
                catch { isDark = true; } // Default to dark on error
            }

            s.IsDarkMode = isDark;
            SetTheme(isDark, s.EnableTransparency);

            // Update global resource for binding
            System.Windows.Application.Current.Resources["GlobalOpacity"] = s.WindowOpacity;

            // Also force update current windows (backup)
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (Window win in System.Windows.Application.Current.Windows)
                {
                    try 
                    { 
                        win.BeginAnimation(Window.OpacityProperty, null); // Stop any running animations
                        win.Opacity = s.WindowOpacity; 
                    } 
                    catch { }
                }
            });
        }

        public static void SetTheme(bool isDark, bool enableTransparency = true)
        {
            try
            {
                var dict = new ResourceDictionary();
                var theme = isDark ? "DarkTheme.xaml" : "LightTheme.xaml";
                dict.Source = new Uri($"pack://application:,,,/Themes/{theme}", UriKind.Absolute);

                if (!enableTransparency)
                {
                    // Force all SolidColorBrush items to 1.0 opacity
                    var keys = new System.Collections.Generic.List<object>();
                    foreach (var key in dict.Keys) keys.Add(key);

                    foreach (var key in keys)
                    {
                        if (dict[key] is System.Windows.Media.SolidColorBrush brush)
                        {
                            var solidBrush = new System.Windows.Media.SolidColorBrush(brush.Color) { Opacity = 1.0 };
                            solidBrush.Freeze();
                            dict[key] = solidBrush;
                        }
                    }
                }

                System.Windows.Application.Current.Resources.MergedDictionaries.Clear();
                System.Windows.Application.Current.Resources.MergedDictionaries.Add(dict);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Theme error: {ex.Message}");
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public void ShowMainWindow(string? filter = null)
        {
            if (_mainWindow == null) return;
            if (filter != null && _vm != null) _vm.ActiveFilter = filter;
            
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.ShowInTaskbar = true;
            _mainWindow.Activate();
            _mainWindow.Focus();
            _mainWindow.AnimateRestore();
        }

        private void ToggleMainWindow()
        {
            if (_mainWindow == null) return;
            if (_mainWindow.IsVisible && _mainWindow.WindowState != WindowState.Minimized)
            {
                _mainWindow.HideWithAnimation();
            }
            else
            {
                ShowMainWindow();
            }
        }

        private MiniModeWindow? _currentMiniMode;
        private void ShowMiniMode()
        {
            // Bug 6 fix: use IsVisible instead of IsLoaded.
            // AnimateClose() calls Hide() before the async Close(), so IsLoaded stays
            // true during the 250 ms animation. IsVisible goes false on Hide() immediately,
            // so pressing the hotkey again during the closing animation opens a fresh window.
            if (_currentMiniMode != null && _currentMiniMode.IsVisible)
            {
                _currentMiniMode.Activate();
                return;
            }

            _currentMiniMode = new MiniModeWindow(_vm!);
            _currentMiniMode.Opacity = _vm.Settings.WindowOpacity;
            _currentMiniMode.Closed += (s, e) => _currentMiniMode = null;
            _currentMiniMode.Show();
            _currentMiniMode.Activate();
        }

        private void SendRemoteCommand(string command)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(2000);
                using var writer = new StreamWriter(client);
                writer.WriteLine(command);
                writer.Flush();
            }
            catch { /* Could not connect to existing instance */ }
        }

        private async void StartPipeServer()
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await Task.Factory.FromAsync(server.BeginWaitForConnection, server.EndWaitForConnection, null);

                    using var reader = new StreamReader(server);
                    string? command = await reader.ReadLineAsync();

                    if (!string.IsNullOrEmpty(command))
                    {
                        Dispatcher.Invoke(() => HandleRemoteCommand(command));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Pipe server error: {ex.Message}");
                    await Task.Delay(1000);
                }
            }
        }

        private void HandleRemoteCommand(string command)
        {
            switch (command.ToUpper())
            {
                case "RESTORE":
                    ToggleMainWindow();
                    break;
                case "SHARE":
                    // Ensure network is started before opening share window
                    _vm?.EnsureNetworkStarted();
                    var shareWin = new ShareWindow(_vm!);
                    shareWin.Show();
                    shareWin.Activate();
                    break;
            }
        }

        private void SetupTrayIcon()
        {
            var iconStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/ClipboardPro.ico"))?.Stream;
            _trayIcon = new NotifyIcon
            {
                Icon    = iconStream != null ? new Icon(iconStream) : Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? ""),
                Visible = true,
                Text    = "ClipboardPro"
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("Open App", null, (s, e) => ToggleMainWindow());
            menu.Items.Add("-");
            menu.Items.Add("Settings", null, (s, e) => {
                var win = new SettingsWindow(_vm!) { Owner = _mainWindow };
                win.ShowDialog();
            });
            menu.Items.Add("Exit", null, (s, e) => { IsShuttingDown = true; Shutdown(); });

            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.MouseClick += (s, e) => {
                if (e.Button == MouseButtons.Left) ToggleMainWindow();
            };
        }

        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            IsShuttingDown = true;
            base.OnSessionEnding(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _vm?.Dispose();
            TextExpander?.Dispose();
            _trayIcon?.Dispose();
            base.OnExit(e);
        }

        // ── Trial Expiry Watcher — DispatcherTimer (UI thread, like OrbitSwipe's QTimer) ──
        private void StartTrialExpiryWatcher()
        {
            // Use DispatcherTimer (runs on UI thread, never GC'd like a local Threading.Timer)
            _trialWatchTimer = new System.Windows.Threading.DispatcherTimer();
            _trialWatchTimer.Interval = TrialService.IsTestingAutoLock ? TimeSpan.FromSeconds(2) : TimeSpan.FromMinutes(5);
            _trialWatchTimer.Tick += (s, e) =>
            {
                // Guard: don't fire twice
                if (_trialLockShown) return;

                var license = new LicenseService();
                if (!license.IsAppAllowed())
                {
                    _trialLockShown = true;
                    _trialWatchTimer.Stop();

                    LockAppAndShowGate(LockReason.TrialExpired);

                    // Reset guard and restart timer in case user re-activates
                    _trialLockShown = false;
                    _trialWatchTimer.Start();
                }
            };
            _trialWatchTimer.Start();
        }

        // ── Background Online License Checker (licensed users only) ────────────
        private void StartLicenseValidationTimer()
        {
            // Run initial silent check after 100ms to avoid freezing boot
            Task.Delay(100).ContinueWith(async _ =>
            {
                await CheckLicenseOnlineSilentAsync();
            });

            // Store as field so GC doesn't collect it
            _licenseCheckTimer = new System.Threading.Timer(async _ =>
            {
                await CheckLicenseOnlineSilentAsync();
            }, null, TimeSpan.FromHours(6), TimeSpan.FromHours(6));
        }

        private async Task CheckLicenseOnlineSilentAsync()
        {
            try
            {
                var license = new LicenseService();

                // Check if trial just expired while app was running
                // Use same guard as DispatcherTimer to prevent double popup
                if (!license.IsAppAllowed() && !_trialLockShown)
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (_trialLockShown) return; // double-check on UI thread
                        _trialLockShown = true;
                        _trialWatchTimer?.Stop();

                        LockAppAndShowGate(LockReason.TrialExpired);

                        _trialLockShown = false;
                        _trialWatchTimer?.Start();
                    });
                    return;
                }

                var result  = await license.CheckLicenseOnlineSilentAsync();

                if (!result.Valid && result.Revoked)
                {
                    // License was revoked server-side → lock the app
                    Dispatcher.Invoke(() =>
                    {
                        LockAppAndShowGate(LockReason.Revoked);
                    });
                }
                else if (!result.Valid && result.OfflineExpired)
                {
                    // 7-day offline grace period expired → require internet to re-verify
                    Dispatcher.Invoke(() =>
                    {
                        LockAppAndShowGate(LockReason.OfflineExpired);
                    });
                }
            }
            catch { }
        }

        public enum LockReason { TrialExpired, Revoked, OfflineExpired }

        private void LockAppAndShowGate(LockReason reason = LockReason.TrialExpired)
        {
            // 1. Suspend clipboard monitor
            try { _monitor?.Stop(); } catch { }

            // 2. Hide MainWindow instead of closing it
            try { _mainWindow?.Hide(); } catch { }

            // 3. Close all other windows except MainWindow and active dialog
            var windows = System.Windows.Application.Current.Windows.Cast<Window>().ToList();
            foreach (var win in windows)
            {
                if (win != _mainWindow && !(win is TrialGateWindow))
                {
                    try { win.Close(); } catch { }
                }
            }

            // 4. Nullify active window references to prevent reuse
            _currentMiniMode = null;
            _quickDrop = null;

            // 5. Show the TrialGateWindow with the appropriate reason
            var gate = new TrialGateWindow(reason);
            if (gate.ShowDialog() == true)
            {
                // 6. Resume clipboard monitor and show MainWindow
                try
                {
                    if (_mainWindow != null)
                    {
                        _monitor?.Start(_mainWindow);
                    }
                }
                catch { }
                _mainWindow?.Show();
                _mainWindow?.Activate();
            }
            else
            {
                Shutdown();
            }
        }
    }
}
