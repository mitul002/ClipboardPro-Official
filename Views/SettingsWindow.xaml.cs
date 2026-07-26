using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClipboardPro.ViewModels;
using ClipboardPro.Services;
using WpfButton = System.Windows.Controls.Button;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace ClipboardPro.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly MainViewModel _vm;
        private bool _isInitializing = true;

        public SettingsWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = _vm;
            LoadSettings();
            try
            {
                TxtMachineId.Text = LicenseService.GetMachineId();
                UpdateLicenseUi();
            }
            catch { }
            _isInitializing = false;
        }

        private void LoadSettings()
        {
            _isInitializing = true;
            var s = _vm.Settings;
            TxtHotkeyMain.Text      = s.MainWindowHotkey;
            TxtHotkeyPopup.Text     = s.MiniModeHotkey;
            TxtHotkeyQuickPasteBar.Text      = s.QuickPasteBarHotkey;
            TxtMaxDays.Text         = s.MaxHistoryDays.ToString();
            TxtMaxItems.Text        = s.MaxHistoryItems.ToString();
            
            ChkStartup.IsChecked          = s.LaunchOnStartup;
            ChkMinimizeToTray.IsChecked   = s.MinimizeToTray;
            ChkAutoDelete.IsChecked       = s.AutoDeleteOldItems;
            ChkMergeDupes.IsChecked       = s.MergeConsecutiveDuplicates;
            ChkAlwaysOnTop.IsChecked      = s.AlwaysOnTop;
            ChkTransparency.IsChecked     = s.EnableTransparency;
            ChkQuickPasteBar.IsChecked         = s.EnableQuickPasteBar;
            ChkMasking.IsChecked          = s.EnableSensitiveMasking;
            ChkTextExpander.IsChecked     = s.EnableTextExpander;
            CmbThemeMode.SelectedIndex    = s.ThemeMode;
            CmbQuickDropAction.SelectedIndex = s.QuickDropAction;
            SldOpacity.Value              = s.WindowOpacity;
            TxtOpacityValue.Text          = $"{(int)(s.WindowOpacity * 100)}%";

            // Stats
            TxtStatsTotal.Text = $"Total History Items: {_vm.TotalCount}";
            TxtStatsTypes.Text = $"Pinned: {_vm.PinnedCount} | Favorites: {_vm.FavoriteCount}";
            _isInitializing = false;
        }

        private void SaveCurrentSettings()
        {
            if (_isInitializing || _vm == null || TxtHotkeyMain == null || TxtHotkeyQuickPasteBar == null) return;

            var s = _vm.Settings;
            s.MainWindowHotkey = TxtHotkeyMain.Text;
            s.MiniModeHotkey = TxtHotkeyPopup.Text;
            s.QuickPasteBarHotkey = TxtHotkeyQuickPasteBar.Text;
            
            if (int.TryParse(TxtMaxDays.Text, out int days))  s.MaxHistoryDays = days;
            if (int.TryParse(TxtMaxItems.Text, out int items)) s.MaxHistoryItems = items;
            
            s.LaunchOnStartup            = ChkStartup.IsChecked ?? false;
            s.MinimizeToTray             = ChkMinimizeToTray.IsChecked ?? true;
            s.AutoDeleteOldItems         = ChkAutoDelete.IsChecked ?? true;
            s.MergeConsecutiveDuplicates = ChkMergeDupes.IsChecked ?? true;
            s.AlwaysOnTop                = ChkAlwaysOnTop.IsChecked ?? false;
            s.EnableTransparency         = ChkTransparency.IsChecked ?? true;
            s.EnableQuickPasteBar             = ChkQuickPasteBar.IsChecked ?? false;
            s.EnableSensitiveMasking     = ChkMasking.IsChecked ?? true;
            s.EnableTextExpander         = ChkTextExpander.IsChecked ?? false;
            s.ThemeMode                  = CmbThemeMode.SelectedIndex;
            s.QuickDropAction            = CmbQuickDropAction.SelectedIndex;
            s.WindowOpacity              = SldOpacity.Value;

            _vm.SaveSettings();
            
            // Apply immediate changes
            App.ApplyTheme(s);
            
            this.Opacity = s.WindowOpacity;
            if (Owner is MainWindow main) 
            {
                main.Topmost = s.AlwaysOnTop;
                main.Opacity = s.WindowOpacity;
                main.UpdateHotkeys();
                main.UpdateQuickPasteBarState();
                main.UpdateThemeIcon(); // Ensure icon sync
            }
            
            var storage = new StorageService();
            storage.SaveSettings(s);
        }

        private void OnPreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (IsAnyComboBoxOpen(this))
            {
                e.Handled = true;
            }
        }

        private bool IsAnyComboBoxOpen(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.ComboBox cb && cb.IsDropDownOpen) return true;
                if (IsAnyComboBoxOpen(child)) return true;
            }
            return false;
        }

        private async void BtnOptimizeData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TxtOptimizeStatus.Text = "Wait...";
                TxtOptimizeStatus.Foreground = (System.Windows.Media.Brush)FindResource("AccentPrimary");
                
                await Task.Run(() => {
                    try {
                        _vm.OptimizeDatabase();
                        // Also clear orphaned images
                        string imgDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipboardPro", "Images");
                        if (Directory.Exists(imgDir))
                        {
                            var activeImages = _vm.AllItems
                                .Where(i => !string.IsNullOrEmpty(i.ImagePath))
                                .Select(i => Path.GetFileName(i.ImagePath).ToLower())
                                .ToHashSet();

                            var files = Directory.GetFiles(imgDir);
                            foreach (var file in files)
                            {
                                if (!activeImages.Contains(Path.GetFileName(file).ToLower()))
                                {
                                    try { File.Delete(file); } catch { }
                                }
                            }
                        }
                    } catch { }
                });

                TxtOptimizeStatus.Text = "Done";
                TxtOptimizeStatus.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
                
                await Task.Delay(3000);
                if (TxtOptimizeStatus != null) TxtOptimizeStatus.Text = "";
            }
            catch (Exception)
            {
                if (TxtOptimizeStatus != null)
                {
                    TxtOptimizeStatus.Text = "Error";
                    TxtOptimizeStatus.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
                }
            }
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn)
            {
                if (BtnNavGeneral == null || BtnNavShortcuts == null || BtnNavHistory == null || BtnNavData == null || BtnNavLicense == null || BtnNavAbout == null) return;
                BtnNavGeneral.Tag = null;
                BtnNavShortcuts.Tag = null;
                BtnNavHistory.Tag = null;
                BtnNavData.Tag    = null;
                BtnNavLicense.Tag = null;
                BtnNavAbout.Tag   = null;
                btn.Tag = "Active";

                if (PanelGeneral == null || PanelShortcuts == null || PanelHistory == null || PanelData == null || PanelLicense == null || PanelAbout == null) return;
                PanelGeneral.Visibility = Visibility.Collapsed;
                PanelShortcuts.Visibility = Visibility.Collapsed;
                PanelHistory.Visibility = Visibility.Collapsed;
                PanelData.Visibility    = Visibility.Collapsed;
                PanelLicense.Visibility = Visibility.Collapsed;
                PanelAbout.Visibility   = Visibility.Collapsed;

                var name = btn.Name ?? "";
                var title = name.Replace("BtnNav", "");
                if (TxtTitle != null) TxtTitle.Text = title;

                switch (title)
                {
                    case "General":   PanelGeneral.Visibility = Visibility.Visible; break;
                    case "Shortcuts": PanelShortcuts.Visibility = Visibility.Visible; break;
                    case "History":   PanelHistory.Visibility = Visibility.Visible; break;
                    case "Data":      PanelData.Visibility    = Visibility.Visible; break;
                    case "License":   PanelLicense.Visibility = Visibility.Visible; UpdateLicenseUi(); break;
                    case "About":     PanelAbout.Visibility   = Visibility.Visible; break;
                }
            }
        }

        public void SelectTab(string tabName)
        {
            WpfButton? targetBtn = tabName.ToLower() switch
            {
                "about" => BtnNavAbout,
                "shortcuts" => BtnNavShortcuts,
                "history" => BtnNavHistory,
                "data" => BtnNavData,
                "license" => BtnNavLicense,
                _ => BtnNavGeneral
            };

            if (targetBtn != null)
            {
                Nav_Click(targetBtn, new RoutedEventArgs());
            }
        }

        public void OpenAboutTabAndCheckUpdate()
        {
            SelectTab("About");
            BtnCheckUpdate_Click(BtnCheckUpdate, new RoutedEventArgs());
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
        
        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) 
            {
                try { DragMove(); } catch { }
            }
        }

        private void Hotkey_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;
            if (sender is WpfTextBox tb)
            {
                if (e.Key == System.Windows.Input.Key.Escape || e.Key == System.Windows.Input.Key.Back)
                {
                    tb.Text = "None";
                }
                else
                {
                    var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
                    if (key != System.Windows.Input.Key.LeftCtrl && key != System.Windows.Input.Key.RightCtrl &&
                        key != System.Windows.Input.Key.LeftAlt && key != System.Windows.Input.Key.RightAlt &&
                        key != System.Windows.Input.Key.LeftShift && key != System.Windows.Input.Key.RightShift)
                    {
                        var modifiers = System.Windows.Input.Keyboard.Modifiers;
                        var hotkey = "";
                        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)) hotkey += "Ctrl+";
                        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt)) hotkey += "Alt+";
                        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift)) hotkey += "Shift+";
                        hotkey += key.ToString();
                        tb.Text = hotkey;
                        SaveCurrentSettings();
                    }
                }
            }
        }

        private void BtnClearAll_Click(object sender, EventArgs e)
        {
            if (WpfMsgBox.Show("Delete all history permanently?", "Confirm Clear", WpfMsgBoxBtn.YesNo, WpfMsgBoxImg.Warning) == WpfMsgBoxResult.Yes)
            {
                _vm.ClearAll();
                LoadSettings();
                if (sender is FrameworkElement fe) ShowFeedback(fe, "Cleared!", (System.Windows.Media.Brush)FindResource("SuccessBrush"));
            }
        }

        private void BtnOptimize_Click(object sender, EventArgs e)
        {
            _vm.OptimizeHistory();
            if (sender is FrameworkElement fe) ShowFeedback(fe, "History Optimized!", (System.Windows.Media.Brush)FindResource("SuccessBrush"));
        }

        private void Settings_Changed(object sender, RoutedEventArgs e) => SaveCurrentSettings();

        private void SldOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing || TxtOpacityValue == null) return;
            TxtOpacityValue.Text = $"{(int)(e.NewValue * 100)}%";
            SaveCurrentSettings();
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            if (WpfMsgBox.Show("Reset all settings to defaults?", "Reset Settings", WpfMsgBoxBtn.YesNo, WpfMsgBoxImg.Warning) == WpfMsgBoxResult.Yes)
            {
                _vm.ResetSettings();
                LoadSettings();
                SaveCurrentSettings();
                if (sender is FrameworkElement fe) ShowFeedback(fe, "Settings Reset!", (System.Windows.Media.Brush)FindResource("SuccessBrush"));
            }
        }

        private void BtnResetAll_Click(object sender, EventArgs e)
        {
            if (WpfMsgBox.Show("DANGER: This will permanently wipe all history and settings. This cannot be undone.\n\nProceed?", "Factory Reset", WpfMsgBoxBtn.YesNo, WpfMsgBoxImg.Error) == WpfMsgBoxResult.Yes)
            {
                _vm.ResetTotalApp();
                LoadSettings();
                SaveCurrentSettings();
                WpfMsgBox.Show("Application has been reset to factory defaults.", "Reset Complete", WpfMsgBoxBtn.OK, WpfMsgBoxImg.Information);
            }
        }

        private void BtnImportZip_Click(object sender, EventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Zip files (*.zip)|*.zip" };
            if (dialog.ShowDialog() == true)
            {
                _vm.ImportZip(dialog.FileName);
                LoadSettings(); // Reload in case settings were imported
                SaveCurrentSettings(); // Apply theme and UI states immediately
            }
        }

        private void BtnExportZip_Click(object sender, EventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Zip files (*.zip)|*.zip", FileName = $"ClipboardPro_Backup_{DateTime.Now:yyyyMMdd}.zip" };
            if (dialog.ShowDialog() == true)
            {
                _vm.ExportZip(dialog.FileName);
            }
        }

        private async void ShowFeedback(FrameworkElement fe, string message, System.Windows.Media.Brush feedbackBrush)
        {
            string oldTooltip = fe.ToolTip?.ToString() ?? "";
            System.Windows.Media.Brush? oldForeground = null;

            if (fe is System.Windows.Controls.Control control) oldForeground = control.Foreground;
            else if (fe is TextBlock tb) oldForeground = tb.Foreground;

            fe.ToolTip = message;
            if (fe is System.Windows.Controls.Control c) c.Foreground = feedbackBrush;
            else if (fe is TextBlock t) t.Foreground = feedbackBrush;

            await System.Threading.Tasks.Task.Delay(1500);

            fe.ToolTip = string.IsNullOrEmpty(oldTooltip) ? null : oldTooltip;
            if (fe is System.Windows.Controls.Control c2 && oldForeground != null) c2.Foreground = oldForeground;
            else if (fe is TextBlock t2 && oldForeground != null) t2.Foreground = oldForeground;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  License Tab — Full Enterprise Logic
        // ══════════════════════════════════════════════════════════════════════
        private readonly LicenseService _licSvc = new();
        private bool _licTransferMode = false;     // true = refresh mode
        private bool _licTransferSubmitted = false;
        private bool _isCheckingTransferStatus = false;

        private static readonly System.Windows.Media.SolidColorBrush _licGreen  = new(System.Windows.Media.Color.FromRgb(0x22, 0xC5, 0x5E));
        private static readonly System.Windows.Media.SolidColorBrush _licRed    = new(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44));
        private static readonly System.Windows.Media.SolidColorBrush _licYellow = new(System.Windows.Media.Color.FromRgb(0xFB, 0xBF, 0x24));
        private static readonly System.Windows.Media.SolidColorBrush _licMuted  = new(System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));

        private void UpdateLicenseUi()
        {
            try
            {
                var status = _licSvc.GetLicenseStatus();

                if (status.IsLicensed)
                {
                    TxtLicenseStatus.Text       = $"✅  Pro License Active — {status.Plan}";
                    TxtLicenseStatus.Foreground = _licGreen;

                    // Show Annual vs Lifetime in badge
                    var licType = status.LicenseType ?? "lifetime";
                    var licTypeLabel = licType == "annual"
                        ? $"Annual License (needs renewal yearly)"
                        : "Lifetime License";

                    TxtLicenseBadge.Text        = status.KeyPreview ?? "";
                    BadgeLicense.Background     = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x0D, 0x2B, 0x18));
                    TxtLicenseBadge.Foreground  = _licGreen;
                    BadgeLicense.Visibility     = Visibility.Visible;

                    PanelActivationInput.Visibility = Visibility.Collapsed;
                    PanelDeactivation.Visibility    = Visibility.Visible;
                    if (PanelTrialProgress != null) PanelTrialProgress.Visibility = Visibility.Collapsed;

                    // Show email + license type in subtitle
                    var subtitle = licTypeLabel;
                    if (!string.IsNullOrEmpty(status.Email))
                        subtitle += $"  ·  {status.Email}";
                    TxtLicenseStatus.Text += $"\n{subtitle}";
                }
                else
                {
                    var remaining = status.TrialRemaining;

                    if (status.TrialExpired)
                    {
                        TxtLicenseStatus.Text       = "🔒  Trial Period Expired";
                        TxtLicenseStatus.Foreground = _licRed;
                        
                        TxtLicenseBadge.Text = "Expired";
                        TxtLicenseBadge.Foreground = _licRed;
                        BadgeLicense.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3B, 0x0D, 0x0D));
                        BadgeLicense.Visibility = Visibility.Visible;

                        if (PanelTrialProgress != null)
                        {
                            PanelTrialProgress.Visibility = Visibility.Visible;
                            TrialProgressBar.Value = 0;
                        }
                    }
                    else
                    {
                        TxtLicenseStatus.Text       = "⏳  Free Trial Mode";
                        TxtLicenseStatus.Foreground = _licYellow;
                        
                        string detailedStr = remaining.TotalDays >= 1
                            ? $"{(int)remaining.TotalDays}d {remaining.Hours}h {remaining.Minutes}m remaining"
                            : $"{remaining.Hours}h {remaining.Minutes}m remaining";
                            
                        TxtLicenseBadge.Text = detailedStr;
                        try
                        {
                            TxtLicenseBadge.Foreground = (System.Windows.Media.Brush)FindResource("AccentPrimary");
                            BadgeLicense.Background = (System.Windows.Media.Brush)FindResource("BadgeBg");
                        }
                        catch { }
                        
                        BadgeLicense.Visibility = Visibility.Visible;

                        if (PanelTrialProgress != null)
                        {
                            PanelTrialProgress.Visibility = Visibility.Visible;
                            var trialSvc = new TrialService();
                            TrialProgressBar.Value = trialSvc.GetTrialPercentUsed();
                        }
                    }

                    PanelActivationInput.Visibility = Visibility.Visible;
                    PanelDeactivation.Visibility    = Visibility.Collapsed;

                    // Auto-prefill pending transfer cache
                    var pending = _licSvc.ReadPendingTransferCache();
                    if (pending != null && TxtLicenseKeyInput != null && TxtLicenseEmailInput != null)
                    {
                        TxtLicenseKeyInput.Text   = pending.Key;
                        TxtLicenseEmailInput.Text = pending.Email;
                        SetLicMsg("⏳  Transfer request pending admin approval.", _licYellow);
                        ShowLicRefreshButton();
                        _ = AutoCheckPendingTransferStatusAsync(pending.Key, pending.Email);
                    }
                }
            }
            catch { }
        }

        private void SetLicMsg(string msg, SolidColorBrush color)
        {
            if (TxtLicenseMsg == null) return;
            TxtLicenseMsg.Text       = msg;
            TxtLicenseMsg.Foreground = color;
            TxtLicenseMsg.Visibility = string.IsNullOrEmpty(msg) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ShowLicTransferButton()
        {
            _licTransferMode = false;
            if (BtnLicTransfer == null) return;
            BtnLicTransfer.Content    = "🔄  Request Transfer";
            BtnLicTransfer.Visibility = Visibility.Visible;
        }

        private void ShowLicRefreshButton()
        {
            _licTransferMode     = true;
            _licTransferSubmitted = true;
            if (BtnLicTransfer == null) return;
            BtnLicTransfer.Content    = "🔄  Refresh Status";
            BtnLicTransfer.Visibility = Visibility.Visible;
        }

        private void BtnCopyMachineId_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(LicenseService.GetMachineId());
                ShowFeedback(BtnCopyMachineId, "Copied!", _licGreen);
            }
            catch { }
        }

        private async void BtnActivateLicense_Click(object sender, RoutedEventArgs e)
        {
            var key   = TxtLicenseKeyInput?.Text?.Trim()?.ToUpper() ?? "";
            var email = TxtLicenseEmailInput?.Text?.Trim()?.ToLower() ?? "";

            if (string.IsNullOrEmpty(key))
            { SetLicMsg("⚠️  Please enter your license key.", _licYellow); return; }
            if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            { SetLicMsg("⚠️  Please enter a valid email address.", _licYellow); return; }

            BtnActivateLicense.IsEnabled = false;
            BtnActivateLicense.Content   = "Validating...";
            if (BtnLicTransfer != null) BtnLicTransfer.IsEnabled = false;

            var result = _licTransferMode
                ? await _licSvc.RefreshTransferStatusAsync(key, email)
                : await _licSvc.ActivateLicenseAsync(key, email);

            BtnActivateLicense.IsEnabled = true;
            BtnActivateLicense.Content   = "✅  Activate License";
            if (BtnLicTransfer != null) BtnLicTransfer.IsEnabled = true;

            if (result.Valid)
            {
                SetLicMsg("✨  License activated successfully!", _licGreen);
                _licSvc.DeletePendingTransferCache();
                UpdateLicenseUi();
                if (TxtLicenseKeyInput   != null) TxtLicenseKeyInput.Text   = "";
                if (TxtLicenseEmailInput != null) TxtLicenseEmailInput.Text = "";
                if (BtnLicTransfer != null) BtnLicTransfer.Visibility = Visibility.Collapsed;
                return;
            }

            if (result.TransferPending)
            { SetLicMsg("⏳  Transfer request pending admin approval (usually 24h).", _licYellow); return; }

            if (_licTransferMode && (result.CanRequestTransfer
                                  || result.Message.Contains("registered to a different device", StringComparison.OrdinalIgnoreCase)
                                  || result.Message.Contains("Request a transfer", StringComparison.OrdinalIgnoreCase)
                                  || result.Message.Contains("registered to another PC", StringComparison.OrdinalIgnoreCase)
                                  || result.Message.Contains("submit a transfer request", StringComparison.OrdinalIgnoreCase)
                                  || result.Message.Contains("already in use", StringComparison.OrdinalIgnoreCase)))
            {
                SetLicMsg("❌  Transfer was rejected by admin. You may submit a new request.", _licRed);
                _licSvc.DeletePendingTransferCache();
                ShowLicTransferButton();
                return;
            }

            if (result.CanRequestTransfer)
            { SetLicMsg("❌  " + result.Message, _licRed); ShowLicTransferButton(); return; }

            SetLicMsg("❌  " + result.Message, _licRed);
        }

        private async void BtnLicTransfer_Click(object sender, RoutedEventArgs e)
        {
            if (_licTransferMode) { BtnActivateLicense_Click(sender, e); return; }

            var key   = TxtLicenseKeyInput?.Text?.Trim()?.ToUpper() ?? "";
            var email = TxtLicenseEmailInput?.Text?.Trim()?.ToLower() ?? "";

            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(email))
            { SetLicMsg("⚠️  Enter key and email before requesting a transfer.", _licYellow); return; }

            if (BtnLicTransfer != null) { BtnLicTransfer.IsEnabled = false; BtnLicTransfer.Content = "Submitting..."; }
            BtnActivateLicense.IsEnabled = false;

            var result = await _licSvc.RequestTransferAsync(key, email);

            if (BtnLicTransfer != null) BtnLicTransfer.IsEnabled = true;
            BtnActivateLicense.IsEnabled = true;

            if (result.Valid && result.TransferRequestSubmitted)
            { SetLicMsg("🎉  Transfer submitted! Awaiting admin approval.", _licGreen); ShowLicRefreshButton(); return; }

            SetLicMsg("❌  " + result.Message, _licRed);
        }

        private void BtnDeactivateLicense_Click(object sender, RoutedEventArgs e)
        {
            if (WpfMsgBox.Show("Deactivate this license from your PC?\n\nYou can re-activate with the same key later.",
                "Confirm Deactivation", WpfMsgBoxBtn.YesNo, WpfMsgBoxImg.Warning) == WpfMsgBoxResult.Yes)
            {
                _licSvc.DeactivateLicense();
                UpdateLicenseUi();
                SetLicMsg("License removed. Reverted to trial mode.", _licMuted);
            }
        }

        private async Task AutoCheckPendingTransferStatusAsync(string key, string email)
        {
            if (_isCheckingTransferStatus) return;
            _isCheckingTransferStatus = true;
            try
            {
                SetLicMsg("⏳  Checking transfer status...", _licYellow);
                if (BtnActivateLicense != null) BtnActivateLicense.IsEnabled = false;
                if (BtnLicTransfer != null) BtnLicTransfer.IsEnabled = false;

                var result = await _licSvc.RefreshTransferStatusAsync(key, email);

                if (result.Valid)
                {
                    SetLicMsg("✨  License activated successfully!", _licGreen);
                    _licSvc.DeletePendingTransferCache();
                    UpdateLicenseUi();
                    if (TxtLicenseKeyInput   != null) TxtLicenseKeyInput.Text   = "";
                    if (TxtLicenseEmailInput != null) TxtLicenseEmailInput.Text = "";
                    if (BtnLicTransfer != null) BtnLicTransfer.Visibility = Visibility.Collapsed;
                    return;
                }

                if (result.TransferPending)
                {
                    SetLicMsg("⏳  Transfer request pending admin approval (usually 24h).", _licYellow);
                    return;
                }

                string msgLower = result.Message.ToLower();
                bool isDeclined = result.CanRequestTransfer
                               || msgLower.Contains("decline", StringComparison.OrdinalIgnoreCase)
                               || msgLower.Contains("reject", StringComparison.OrdinalIgnoreCase)
                               || msgLower.Contains("registered to a different device", StringComparison.OrdinalIgnoreCase)
                               || msgLower.Contains("request a transfer", StringComparison.OrdinalIgnoreCase)
                               || msgLower.Contains("registered to another pc", StringComparison.OrdinalIgnoreCase)
                               || msgLower.Contains("submit a transfer request", StringComparison.OrdinalIgnoreCase)
                               || msgLower.Contains("already in use", StringComparison.OrdinalIgnoreCase);

                if (isDeclined)
                {
                    SetLicMsg("❌  Transfer was rejected by admin. You may submit a new request.", _licRed);
                    _licSvc.DeletePendingTransferCache();
                    ShowLicTransferButton();
                    return;
                }

                SetLicMsg("❌  " + result.Message, _licRed);
            }
            catch { }
            finally
            {
                _isCheckingTransferStatus = false;
                if (BtnActivateLicense != null) BtnActivateLicense.IsEnabled = true;
                if (BtnLicTransfer != null) BtnLicTransfer.IsEnabled = true;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  About Tab — Auto Update Logic (OrbitSwipe style)
        // ══════════════════════════════════════════════════════════════════════
        private System.Windows.Threading.DispatcherTimer? _updSpinnerTimer;
        private string? _pendingDownloadUrl;

        private void StartUpdSpinner()
        {
            if (UpdSpinner == null || UpdSpinnerRotate == null) return;
            UpdSpinner.Visibility = Visibility.Visible;
            if (_updSpinnerTimer == null)
            {
                _updSpinnerTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(30)
                };
                _updSpinnerTimer.Tick += (s, e) =>
                {
                    UpdSpinnerRotate.Angle = (UpdSpinnerRotate.Angle + 12) % 360;
                };
            }
            _updSpinnerTimer.Start();
        }

        private void StopUpdSpinner()
        {
            _updSpinnerTimer?.Stop();
            if (UpdSpinner != null) UpdSpinner.Visibility = Visibility.Collapsed;
        }

        private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_pendingDownloadUrl))
            {
                _ = DownloadAndUpdateAsync(_pendingDownloadUrl);
                return;
            }

            BtnCheckUpdate.IsEnabled = false;
            BtnCheckUpdate.Content = "Checking for updates...";
            StartUpdSpinner();
            UpdProgContainer.Visibility = Visibility.Collapsed;

            var result = await UpdateService.CheckForUpdatesAsync();

            StopUpdSpinner();

            if (!string.IsNullOrEmpty(result.Error))
            {
                BtnCheckUpdate.Content = "Check failed";
                try
                {
                    BtnCheckUpdate.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
                }
                catch { }
                await Task.Delay(3000);
                ResetUpdateUi();
                return;
            }

            if (result.Available)
            {
                _pendingDownloadUrl = result.DownloadUrl;
                BtnCheckUpdate.Content = $"New version available (v{result.Version}) — Click to Install";
                try
                {
                    BtnCheckUpdate.Foreground = (System.Windows.Media.Brush)FindResource("AccentPrimary");
                }
                catch { }
                BtnCheckUpdate.IsEnabled = true;
            }
            else
            {
                BtnCheckUpdate.Content = "You are on the latest version";
                try
                {
                    BtnCheckUpdate.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
                }
                catch { }
                await Task.Delay(3000);
                ResetUpdateUi();
            }
        }

        private async Task DownloadAndUpdateAsync(string url)
        {
            BtnCheckUpdate.IsEnabled = false;
            BtnCheckUpdate.Content = "Downloading...";
            UpdProgContainer.Visibility = Visibility.Visible;

            var progress = new Progress<(long downloaded, long total, int percent)>(p =>
            {
                PbarUpdate.Value = p.percent;
                double mb = p.downloaded / (1024.0 * 1024.0);
                TxtUpdStatus.Text = $"Downloading update... {mb:F1} MB ({p.percent}%)";
            });

            try
            {
                await UpdateService.DownloadAndInstallAsync(url, progress);
            }
            catch
            {
                TxtUpdStatus.Text = "Download failed!";
                try
                {
                    TxtUpdStatus.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
                }
                catch { }
                await Task.Delay(3000);
                UpdProgContainer.Visibility = Visibility.Collapsed;
                ResetUpdateUi();
            }
        }

        private void ResetUpdateUi()
        {
            _pendingDownloadUrl = null;
            BtnCheckUpdate.Content = "Check for updates";
            try
            {
                BtnCheckUpdate.Foreground = (System.Windows.Media.Brush)FindResource("TextMuted");
            }
            catch { }
            BtnCheckUpdate.IsEnabled = true;
        }
    }
}
