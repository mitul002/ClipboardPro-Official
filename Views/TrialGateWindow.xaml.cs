using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ClipboardPro.Services;
using WpfColor    = System.Windows.Media.Color;
using WpfKey     = System.Windows.Input.KeyEventArgs;

namespace ClipboardPro.Views
{
    public partial class TrialGateWindow : Window
    {
        private readonly LicenseService _license = new();
        private bool _transferRequestSubmitted = false;
        private bool _isRefreshMode = false;

        // ── State Colors ────────────────────────────────────────────────────
        private static readonly SolidColorBrush _green  = new(WpfColor.FromRgb(0x22, 0xC5, 0x5E));
        private static readonly SolidColorBrush _red    = new(WpfColor.FromRgb(0xEF, 0x44, 0x44));
        private static readonly SolidColorBrush _yellow = new(WpfColor.FromRgb(0xFB, 0xBF, 0x24));
        private static readonly SolidColorBrush _muted  = new(WpfColor.FromRgb(0x94, 0xA3, 0xB8));

        // ── Banner color presets ─────────────────────────────────────────────
        // (StartColor, EndColor, IconBadgeColor, icon emoji)
        private static readonly (WpfColor g1, WpfColor g2, WpfColor badge, string icon, string title, string sub) _stateTrialExpired = (
            WpfColor.FromRgb(0x0D, 0x07, 0x2A),
            WpfColor.FromRgb(0x06, 0x14, 0x28),
            WpfColor.FromArgb(0x33, 0x38, 0xBD, 0xF8),
            "⏰",
            "Your Free Trial Has Ended",
            "Your 30-day free trial is complete. Enter your license key below to unlock full access."
        );
        private static readonly (WpfColor g1, WpfColor g2, WpfColor badge, string icon, string title, string sub) _stateRevoked = (
            WpfColor.FromRgb(0x2A, 0x06, 0x06),
            WpfColor.FromRgb(0x18, 0x04, 0x04),
            WpfColor.FromArgb(0x33, 0xEF, 0x44, 0x44),
            "🔑",
            "License Expired or Revoked",
            "Your ClipboardPro license has expired or was deactivated remotely. Please renew your subscription to continue."
        );
        private static readonly (WpfColor g1, WpfColor g2, WpfColor badge, string icon, string title, string sub) _stateOffline = (
            WpfColor.FromRgb(0x1A, 0x14, 0x00),
            WpfColor.FromRgb(0x0C, 0x10, 0x08),
            WpfColor.FromArgb(0x33, 0xFB, 0xBF, 0x24),
            "📶",
            "Offline Verification Required",
            "Your 7-day offline grace period has expired. Please connect to the internet to verify your license key."
        );

        public TrialGateWindow(App.LockReason reason = App.LockReason.TrialExpired)
        {
            InitializeComponent();
            Opacity = 0;

            // Apply banner based on reason
            ApplyBannerState(reason);

            // If there's a cached pending transfer, prefill and show refresh button
            var pending = _license.ReadPendingTransferCache();

            Loaded += async (_, _) =>
            {
                FadeIn();
                if (pending != null && !string.IsNullOrEmpty(pending.Key) && !string.IsNullOrEmpty(pending.Email))
                {
                    await AutoCheckTransferStatusAsync(pending.Key, pending.Email);
                }
            };

            if (pending != null && !string.IsNullOrEmpty(pending.Key) && !string.IsNullOrEmpty(pending.Email))
            {
                TxtKey.Text   = pending.Key;
                TxtEmail.Text = pending.Email;
                ShowTransferRefreshButton();
                SetMsg("⏳  A transfer request is pending admin approval.", _yellow);
            }
        }

        private void ApplyBannerState(App.LockReason reason)
        {
            var state = reason switch
            {
                App.LockReason.Revoked        => _stateRevoked,
                App.LockReason.OfflineExpired => _stateOffline,
                _                             => _stateTrialExpired
            };

            BannerGrad1.Color   = state.g1;
            BannerGrad2.Color   = state.g2;
            IconBadgeBg.Color   = state.badge;
            TxtBannerIcon.Text  = state.icon;
            TxtTitle.Text       = state.title;
            TxtSubHeader.Text   = state.sub;

            // Also tint border for revoked
            if (reason == App.LockReason.Revoked)
                RootBorder.BorderBrush = _yellow;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Animations
        // ══════════════════════════════════════════════════════════════════════
        private void FadeIn()
        {
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(OpacityProperty, anim);
        }

        private void FadeOut(Action onComplete)
        {
            var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            anim.Completed += (_, _) => onComplete();
            BeginAnimation(OpacityProperty, anim);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Window Controls
        // ══════════════════════════════════════════════════════════════════════
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
                try { DragMove(); } catch { }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        private void BtnClose_Click(object sender, RoutedEventArgs e) =>
            FadeOut(() => Dispatcher.Invoke(() => { DialogResult = false; Close(); }));

        private void LinkBuy_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
            catch { }
            e.Handled = true;
        }

        private void TxtKey_KeyDown(object sender, WpfKey e)
        {
            if (e.Key == Key.Return) TxtEmail.Focus();
        }
        private void TxtEmail_KeyDown(object sender, WpfKey e)
        {
            if (e.Key == Key.Return) BtnActivate_Click(BtnActivate, new RoutedEventArgs());
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Main Activation
        // ══════════════════════════════════════════════════════════════════════
        private async void BtnActivate_Click(object sender, RoutedEventArgs e)
        {
            var key   = TxtKey.Text.Trim().ToUpper();
            var email = TxtEmail.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(key))
            {
                SetMsg("⚠️  Please enter your license key.", _yellow); return;
            }
            if (string.IsNullOrEmpty(email) || !email.Contains('@') || !email.Contains('.'))
            {
                SetMsg("⚠️  Please enter a valid email address.", _yellow); return;
            }

            SetActivating(true);

            var result = _isRefreshMode
                ? await _license.RefreshTransferStatusAsync(key, email)
                : await _license.ActivateLicenseAsync(key, email);

            SetActivating(false);

            if (result.Valid)
            {
                SetMsg("✨  License Activated Successfully!", _green);
                _license.DeletePendingTransferCache();

                var timer = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromMilliseconds(1400) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    FadeOut(() => Dispatcher.Invoke(() => { DialogResult = true; Close(); }));
                };
                timer.Start();
                return;
            }

            if (result.TransferPending)
            {
                SetMsg("⏳  Transfer request is pending admin approval (usually within 24h).", _yellow);
                return;
            }

            if (result.CanRequestTransfer && !_isRefreshMode)
            {
                SetMsg("❌  " + result.Message, _red);
                ShowTransferRequestButton();
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

            if (_isRefreshMode && isDeclined)
            {
                SetMsg("❌  Transfer was rejected by admin. You may submit a new request.", _red);
                _license.DeletePendingTransferCache();
                _transferRequestSubmitted = false;
                _isRefreshMode = false;
                BtnTransfer.Content = "🔄  REQUEST TRANSFER";
                BtnTransfer.Style = (Style)FindResource("PrimaryBtn");
                return;
            }

            SetMsg("❌  " + result.Message, _red);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Transfer Request / Refresh
        // ══════════════════════════════════════════════════════════════════════
        private async void BtnTransfer_Click(object sender, RoutedEventArgs e)
        {
            if (_isRefreshMode)
            {
                BtnActivate_Click(sender, e);
                return;
            }

            var key   = TxtKey.Text.Trim().ToUpper();
            var email = TxtEmail.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(email))
            {
                SetMsg("⚠️  Please enter your key and email first.", _yellow); return;
            }

            SetTransferLoading(true);
            var result = await _license.RequestTransferAsync(key, email);
            SetTransferLoading(false);

            if (result.Valid && result.TransferRequestSubmitted)
            {
                SetMsg("🎉  Transfer request submitted! Awaiting admin approval (usually within 24h).", _green);
                ShowTransferRefreshButton();
                return;
            }

            SetMsg("❌  " + result.Message, _red);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  UI State Helpers
        // ══════════════════════════════════════════════════════════════════════
        private void SetMsg(string text, SolidColorBrush color)
        {
            TxtMsg.Text = text;
            TxtMsg.Foreground = color;
        }

        private void SetActivating(bool loading)
        {
            BtnActivate.IsEnabled = !loading;
            BtnActivate.Content   = loading ? "⌛  VALIDATING..." : "✔  Activate";
            if (_isRefreshMode)
            {
                BtnTransfer.IsEnabled = !loading;
                BtnTransfer.Content   = loading ? "⌛  REFRESHING..." : "🔄  REFRESH STATUS";
            }
        }

        private void SetTransferLoading(bool loading)
        {
            BtnTransfer.IsEnabled = !loading;
            BtnTransfer.Content   = loading ? "⌛  SUBMITTING..." : "🔄  REQUEST TRANSFER";
            BtnActivate.IsEnabled = !loading;
        }

        private void ShowTransferRequestButton()
        {
            _isRefreshMode = false;
            BtnTransfer.Style      = (Style)FindResource("PrimaryBtn");
            BtnTransfer.Content    = "🔄  REQUEST TRANSFER";
            BtnTransfer.Visibility = Visibility.Visible;
        }

        private void ShowTransferRefreshButton()
        {
            _transferRequestSubmitted = true;
            _isRefreshMode = true;
            BtnTransfer.Style      = (Style)FindResource("TransferBtn");
            BtnTransfer.Content    = "🔄  REFRESH STATUS";
            BtnTransfer.Visibility = Visibility.Visible;
        }

        private async System.Threading.Tasks.Task AutoCheckTransferStatusAsync(string key, string email)
        {
            SetActivating(true);
            SetMsg("⏳  Checking transfer status...", _yellow);

            var result = await _license.RefreshTransferStatusAsync(key, email);

            SetActivating(false);

            if (result.Valid)
            {
                SetMsg("✨  License Activated Successfully!", _green);
                _license.DeletePendingTransferCache();

                var timer = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromMilliseconds(1400) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    FadeOut(() => Dispatcher.Invoke(() => { DialogResult = true; Close(); }));
                };
                timer.Start();
                return;
            }

            if (result.TransferPending)
            {
                SetMsg("⏳  Transfer request is pending admin approval (usually within 24h).", _yellow);
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
                SetMsg("❌  Transfer was rejected by admin. You may submit a new request.", _red);
                _license.DeletePendingTransferCache();
                _transferRequestSubmitted = false;
                _isRefreshMode = false;
                BtnTransfer.Content = "🔄  REQUEST TRANSFER";
                BtnTransfer.Style = (Style)FindResource("PrimaryBtn");
                return;
            }

            SetMsg("❌  " + result.Message, _red);
        }
    }
}
