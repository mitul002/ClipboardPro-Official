using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClipboardPro.ViewModels;
using ClipboardPro.Models;
using ClipboardPro.Services;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;
using WpfApp = System.Windows.Application;

namespace ClipboardPro.Views
{
    public partial class MainWindow : System.Windows.Window
    {
        private readonly MainViewModel _vm;
        private readonly ClipboardMonitorService _monitor;
        private readonly HotkeyService _hotkeys;
        private ShareWindow? _shareWindow;

        public MainWindow(MainViewModel vm, ClipboardMonitorService monitor, HotkeyService hotkeys)
        {
            InitializeComponent();
            _vm = vm;
            _monitor = monitor;
            _hotkeys = hotkeys;
            
            DataContext = _vm;
            this.Topmost = _vm.Settings.AlwaysOnTop;
            
            // Set initial state for entrance animation in constructor to prevent startup flash/stutter
            this.Opacity = 0;
            RootScale.ScaleX = 0.2;
            RootScale.ScaleY = 0.2;
            RootTranslate.Y = 350;

            UpdateThemeIcon();
            UpdateQuickPasteBarState();

            this.Loaded += (s, e) => 
            {
                var listBox = FindVisualChild<System.Windows.Controls.ListBox>(this);
                if (listBox != null) Helpers.ScrollAnimationHelper.ApplyScrollEffects(listBox);
                AnimateRestore();
            };


        }

        public void UpdateHotkeys()
        {
            _hotkeys.Unregister();
            _hotkeys.Register(this, _vm.Settings.MainWindowHotkey, _vm.Settings.MiniModeHotkey, _vm.Settings.QuickPasteBarHotkey);
        }

        public void UpdateThemeIcon()
        {
            if (TxtThemeIcon != null)
            {
                switch (_vm.Settings.ThemeMode)
                {
                    case 0: TxtThemeIcon.Text = "\uE706"; break; // Sun
                    case 1: TxtThemeIcon.Text = "\uE708"; break; // Moon
                    case 2: TxtThemeIcon.Text = "\uE770"; break; // PC
                }
            }
            ApplyCurrentTheme();
        }

        private void BtnThemeToggle_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _vm.Settings.ThemeMode = (_vm.Settings.ThemeMode + 1) % 3;
            _vm.SaveSettings();
            UpdateThemeIcon();
        }

        private void ApplyCurrentTheme()
        {
            App.ApplyTheme(_vm.Settings);
        }

        private T? FindVisualChild<T>(System.Windows.DependencyObject obj) where T : System.Windows.DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                System.Windows.DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                if (child is T t) return t;
                T? childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) ToggleMaximize();
            else DragMove();
        }

        private void SetAnimationCenter()
        {
            if (RootScale != null)
            {
                RootScale.CenterX = this.ActualWidth > 0 ? this.ActualWidth / 2 : 600;
                RootScale.CenterY = this.ActualHeight > 0 ? this.ActualHeight / 2 : 375;
            }
        }

        private async void BtnMinimize_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            SetAnimationCenter();
            var fadeAnim = new System.Windows.Media.Animation.DoubleAnimation(0, System.TimeSpan.FromMilliseconds(300));
            var scaleAnim = new System.Windows.Media.Animation.DoubleAnimation(0.2, System.TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new System.Windows.Media.Animation.QuarticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
            };
            var slideAnim = new System.Windows.Media.Animation.DoubleAnimation(350, System.TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new System.Windows.Media.Animation.QuarticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
            };

            this.BeginAnimation(System.Windows.Window.OpacityProperty, fadeAnim);
            RootScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnim);
            RootScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
            RootTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideAnim);

            await System.Threading.Tasks.Task.Delay(300);
            
            // Clear the animation BEFORE minimizing but leave the window transparent and shrunken
            // so it doesn't flash at 100% scale before going to the taskbar, and stays invisible
            // when it first restores before the restore animation takes over.
            this.BeginAnimation(System.Windows.Window.OpacityProperty, null);
            RootScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
            RootScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
            RootTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);

            this.Opacity = 0;
            RootScale.ScaleX = 0.2;
            RootScale.ScaleY = 0.2;
            RootTranslate.Y = 350;
            this.WindowState = System.Windows.WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, System.Windows.RoutedEventArgs e) => ToggleMaximize();

        private void ToggleMaximize()
        {
            if (this.WindowState == System.Windows.WindowState.Maximized)
            {
                this.WindowState = System.Windows.WindowState.Normal;
                BtnMaximize.Content = "\uE922";
            }
            else
            {
                this.WindowState = System.Windows.WindowState.Maximized;
                BtnMaximize.Content = "\uE923";
            }
        }

        protected override void OnActivated(System.EventArgs e)
        {
            base.OnActivated(e);
            // Don't call ApplyFilter() here - it rebuilds the entire list and blocks UI,
            // causing the "ding" sound and double-click-to-restore issue.
            // Filter is already applied when items are added/deleted.
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(System.IntPtr process, System.IntPtr minimumWorkingSetSize, System.IntPtr maximumWorkingSetSize);

        private void FlushMemory()
        {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            if (System.Environment.OSVersion.Platform == System.PlatformID.Win32NT)
                SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle, (System.IntPtr)(-1), (System.IntPtr)(-1));
        }

        private System.Windows.WindowState _previousState = System.Windows.WindowState.Normal;

        protected override void OnStateChanged(System.EventArgs e)
        {
            base.OnStateChanged(e);
            
            if (this.WindowState == System.Windows.WindowState.Minimized)
            {
                // Ensure the window is fully transparent when in the taskbar.
                // This completely prevents the 1-frame flash when Windows restores it,
                // because it will be restored invisibly before AnimateRestore takes over.
                this.Opacity = 0;
            }
            else if (this.WindowState == System.Windows.WindowState.Normal && _previousState == System.Windows.WindowState.Minimized)
            {
                // Only animate if restoring from taskbar, not when un-maximizing.
                AnimateRestore();
            }
            
            _previousState = this.WindowState;
        }

        public void AnimateRestore()
        {
            SetAnimationCenter();
            this.Opacity = 0;
            RootScale.ScaleX = 0.2;
            RootScale.ScaleY = 0.2;
            RootTranslate.Y = 350;

            double targetOpacity = _vm?.Settings?.WindowOpacity ?? 1.0;
            var fadeAnim = new System.Windows.Media.Animation.DoubleAnimation(targetOpacity, System.TimeSpan.FromMilliseconds(250));
            fadeAnim.Completed += (s, e) => {
                this.BeginAnimation(System.Windows.Window.OpacityProperty, null);
                this.Opacity = targetOpacity;
            };

            var scaleAnim = new System.Windows.Media.Animation.DoubleAnimation(1.0, System.TimeSpan.FromMilliseconds(350))
            {
                EasingFunction = new System.Windows.Media.Animation.QuarticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            var slideAnim = new System.Windows.Media.Animation.DoubleAnimation(0, System.TimeSpan.FromMilliseconds(350))
            {
                EasingFunction = new System.Windows.Media.Animation.QuarticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };

            this.BeginAnimation(System.Windows.Window.OpacityProperty, fadeAnim);
            RootScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnim);
            RootScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
            RootTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideAnim);
        }

        private void AnimateClose(Action onCompleted)
        {
            SetAnimationCenter();
            var fadeAnim = new System.Windows.Media.Animation.DoubleAnimation(0, System.TimeSpan.FromMilliseconds(300));
            var scaleAnim = new System.Windows.Media.Animation.DoubleAnimation(0.2, System.TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new System.Windows.Media.Animation.QuarticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
            };
            var slideAnim = new System.Windows.Media.Animation.DoubleAnimation(350, System.TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new System.Windows.Media.Animation.QuarticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
            };

            fadeAnim.Completed += (s, e) =>
            {
                this.BeginAnimation(System.Windows.Window.OpacityProperty, null);
                RootScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
                RootScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
                RootTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
                onCompleted?.Invoke();
            };

            this.BeginAnimation(System.Windows.Window.OpacityProperty, fadeAnim);
            RootScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnim);
            RootScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleAnim);
            RootTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideAnim);
        }

        public void HideWithAnimation()
        {
            AnimateClose(() =>
            {
                this.Hide();
                FlushMemory();
            });
        }

        protected override void OnDeactivated(System.EventArgs e)
        {
            base.OnDeactivated(e);
            // Don't flush memory on every deactivation - it evicts pages from RAM
            // and makes re-activation slow, causing the ding/delay issue.
            _vm.TrimMemory();
        }

        private void BtnAlwaysOnTop_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _vm.Settings.AlwaysOnTop = !_vm.Settings.AlwaysOnTop;
            _vm.SaveSettings();
            this.Topmost = _vm.Settings.AlwaysOnTop;
        }

        private void BtnClose_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_vm.Settings.MinimizeToTray)
            {
                e.Cancel = true;
                AnimateClose(() =>
                {
                    this.Hide();
                    FlushMemory();
                });
            }
            else
            {
                ClipboardPro.App.IsShuttingDown = true;
                base.OnClosing(e);
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void NavButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement btn && btn.Tag != null)
            {
                string filter = btn.Tag.ToString() ?? "All Items";

                // ── Snippets view toggle ──────────────────────────────────────
                if (filter == "Snippets")
                {
                    bool isSnippets = (PanelSnippets.Visibility == System.Windows.Visibility.Visible);
                    PanelSnippets.Visibility = System.Windows.Visibility.Visible;
                    MainListBox.Visibility   = System.Windows.Visibility.Collapsed;
                    TxtPageTitle.Text        = "Expender";
                    _vm.ActiveFilter         = "Snippets";
                    return;
                }

                // ── Regular clipboard navigation ───────────────────────────────
                PanelSnippets.Visibility = System.Windows.Visibility.Collapsed;
                MainListBox.Visibility   = System.Windows.Visibility.Visible;
                TxtPageTitle.Text        = filter.StartsWith("cat:") ? filter.Substring(4) : filter;

                if (!filter.StartsWith("cat:") && !new[] { "All Items", "Favorites", "Pinned", "URL", "Email", "Code", "Phone", "Image", "Color", "Path", "Directory", "Private" }.Contains(filter))
                {
                    if (_vm.Settings.CustomCategories.Any(c => c.Name == filter))
                        filter = "cat:" + filter;
                }
                _vm.ActiveFilter = filter;
            }
        }

        private void CmbDate_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_vm != null && CmbDate.SelectedItem is System.Windows.Controls.ComboBoxItem item)
                _vm.DateFilter = item.Content?.ToString() ?? "All Dates";
        }

        private void CmbType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_vm != null && CmbType.SelectedItem is System.Windows.Controls.ComboBoxItem item)
                _vm.TypeFilter = item.Content?.ToString() ?? "All Types";
        }

        private void CmbSort_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_vm != null && CmbSort.SelectedItem is System.Windows.Controls.ComboBoxItem item)
                _vm.SortOrder = item.Content?.ToString() ?? "Newest First";
        }

        private async void BtnSend_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is ClipboardItem item)
            {
                var peers = _vm.ActivePeers.ToList();
                if (peers.Count == 0)
                {
                    ShowFeedback(btn, "No devices found", (System.Windows.Media.Brush)FindResource("DangerBrush"));
                    return;
                }

                var menu = new System.Windows.Controls.ContextMenu();

                foreach (var peer in peers)
                {
                    var mi = new System.Windows.Controls.MenuItem { 
                        Header = $"Send to {peer.Name}",
                        Icon = new System.Windows.Controls.TextBlock { 
                            Text = "\uE724", // Changed to send icon
                            FontSize = 12,
                            VerticalAlignment = VerticalAlignment.Center,
                            FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets") 
                        }
                    };
                    mi.Click += async (s, ex) => {
                        var success = await _vm.SendToDevice(item, peer);
                        ShowFeedback(btn, success ? "Sent!" : "Failed", (System.Windows.Media.Brush)FindResource(success ? "SuccessBrush" : "DangerBrush"));
                    };
                    menu.Items.Add(mi);
                }

                btn.ContextMenu = menu;
                menu.IsOpen = true;
            }
        }

        private void BtnShare_Click(object sender, RoutedEventArgs e)
        {
            if (_shareWindow == null)
            {
                _shareWindow = new ShareWindow(_vm);
            }
            
            if (_shareWindow.IsVisible)
            {
                _shareWindow.Activate();
            }
            else
            {
                _shareWindow.Show();
                _shareWindow.Activate();
            }
        }

        private void BtnRefresh_Click(object sender, System.Windows.RoutedEventArgs e) => _vm.ApplyFilter();

        private void BtnClearAll_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_vm.ActiveFilter == "Snippets")
            {
                if (WpfMsgBox.Show("Delete all text expander snippets?", "Clear Snippets", WpfMsgBoxBtn.YesNo) == WpfMsgBoxResult.Yes)
                {
                    _vm.ClearAllSnippets();
                }
                return;
            }

            var msg = _vm.ActiveFilter switch
            {
                "URL"       => "Clear all non-pinned URLs?",
                "Image"     => "Clear all non-pinned images?",
                "Email"     => "Clear all non-pinned emails?",
                "Code"      => "Clear all non-pinned code snippets?",
                "Phone"     => "Clear all non-pinned phone numbers?",
                "Color"     => "Clear all non-pinned colors?",
                "Path"      => "Clear all non-pinned files?",
                "Directory" => "Clear all non-pinned directories?",
                "Private"   => "Clear all non-pinned private items?",
                "Favorites" => "Clear all non-pinned favorites?",
                _ => _vm.ActiveFilter.StartsWith("cat:") 
                    ? $"Clear all items in category '{_vm.ActiveFilter.Substring(4)}'?" 
                    : "Clear all non-pinned items?"
            };

            if (WpfMsgBox.Show(msg, "Clear History", WpfMsgBoxBtn.YesNo) == WpfMsgBoxResult.Yes)
            {
                _vm.ClearAll();
            }
        }

        private void BtnEditCategory_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            e.Handled = true; // Prevent parent category button click from executing
            if (sender is System.Windows.Controls.Button btn && btn.Tag is CategoryInfo catInfo)
            {
                var menu = new System.Windows.Controls.ContextMenu();

                // Rename Item
                var renameMi = new System.Windows.Controls.MenuItem
                {
                    Header = "Rename Category",
                    Icon = new System.Windows.Controls.TextBlock
                    {
                        Text = "\uE70F", // Pencil Icon
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets")
                    }
                };
                renameMi.Click += (s, ev) =>
                {
                    var dlg = new InputDialog("Rename Category", "Edit category name:", catInfo.Name, catInfo.Color, catInfo.Icon, "SAVE");
                    dlg.Owner = this;
                    if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Result))
                    {
                        string newName = dlg.Result.Trim();
                        // Check if another category already has this name
                        if (!newName.Equals(catInfo.Name, StringComparison.OrdinalIgnoreCase) && 
                            _vm.Settings.CustomCategories.Any(c => c.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
                        {
                            WpfMsgBox.Show("Category name already exists!", "Error", WpfMsgBoxBtn.OK, WpfMsgBoxImg.Error);
                            return;
                        }
                        _vm.RenameCategory(catInfo.Name, newName, dlg.SelectedColor, dlg.SelectedIcon);
                    }
                };

                // Delete Item
                var deleteMi = new System.Windows.Controls.MenuItem
                {
                    Header = "Delete Category",
                    Icon = new System.Windows.Controls.TextBlock
                    {
                        Text = "\uE74D", // Trash Icon
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush"),
                        FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets")
                    }
                };
                deleteMi.Click += (s, ev) =>
                {
                    if (WpfMsgBox.Show($"Remove category '{catInfo.Name}'? Items will remain but category assignment will be removed.", "Remove Category", WpfMsgBoxBtn.YesNo) == WpfMsgBoxResult.Yes)
                    {
                        _vm.DeleteCategory(catInfo.Name);
                    }
                };

                menu.Items.Add(renameMi);
                menu.Items.Add(deleteMi);

                menu.PlacementTarget = btn;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        private void BtnPin_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is ClipboardItem item) _vm.TogglePin(item);
        }

        private void BtnEdit_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement el && el.Tag is ClipboardItem item)
            {
                try
                {
                    var editWin = new EditClipWindow(item, false) { Owner = this };
                    editWin.ShowDialog();
                    if (editWin.Deleted)
                    {
                        _vm.DeleteItem(item);
                    }
                    else
                    {
                        _vm.ApplyFilter();
                        _vm.SaveItems();
                    }
                }
                catch (System.Exception ex) { System.Windows.MessageBox.Show("Error: " + ex.Message); }
            }
        }

        private void BtnView_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (e != null) e.Handled = true; // Prevent bubbling to card copy logic
            if (sender is System.Windows.FrameworkElement el && el.Tag is ClipboardItem item)
            {
                ShowViewWindow(item);
            }
        }

        private void BtnFavorite_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is ClipboardItem item) _vm.ToggleFavorite(item);
        }

        private void BtnToggleMask_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is ClipboardItem item) _vm.ToggleMask(item);
        }

        private async void BtnCopy_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement el && el.Tag is ClipboardItem item)
            {
                _vm.CopyItem(item);
                if (sender is System.Windows.Controls.Button btn) ShowFeedback(btn, "Copied!", (System.Windows.Media.Brush)FindResource("SuccessBrush"));
                
                // Show feedback overlay if available (for both Button and Border/Card clicks)
                var parent = System.Windows.Media.VisualTreeHelper.GetParent(el) as System.Windows.FrameworkElement;
                if (parent != null)
                {
                    for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
                    {
                        if (System.Windows.Media.VisualTreeHelper.GetChild(parent, i) is System.Windows.Controls.Border toast && (toast.Name == "CopyFeedbackList" || toast.Name == "CopyFeedbackGrid"))
                        {
                            toast.Opacity = 1;
                            await System.Threading.Tasks.Task.Delay(2000);
                            toast.BeginAnimation(System.Windows.UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, System.TimeSpan.FromMilliseconds(500)));
                            break;
                        }
                    }
                }
            }
        }

        private async void ShowFeedback(System.Windows.Controls.Button btn, string message, System.Windows.Media.Brush feedbackBrush)
        {
            var oldTooltip = btn.ToolTip;
            var oldForeground = btn.Foreground;
            btn.ToolTip = message;
            btn.Foreground = feedbackBrush;
            await System.Threading.Tasks.Task.Delay(1500);
            btn.ToolTip = oldTooltip;
            btn.Foreground = oldForeground;
        }

        private void BtnPastePlain_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is ClipboardItem item)
            {
                _vm.PastePlainText(item);
                ShowFeedback(btn, "Copied Plain!", (System.Windows.Media.Brush)FindResource("SuccessBrush"));
                if (_vm.Settings.CloseAfterPasting) this.Hide();
            }
        }



        private async void BtnDelete_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement el && el.Tag is ClipboardItem item)
            {
                System.Windows.FrameworkElement parent = el;
                while (parent != null && !(parent is System.Windows.Controls.Border && ((System.Windows.Controls.Border)parent).Margin.Bottom == 12))
                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent) as System.Windows.FrameworkElement;

                if (parent is System.Windows.Controls.Border card)
                {
                    var anim = new System.Windows.Media.Animation.DoubleAnimation { To = 600, Duration = System.TimeSpan.FromMilliseconds(400), EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn } };
                    var opacityAnim = new System.Windows.Media.Animation.DoubleAnimation { To = 0, Duration = System.TimeSpan.FromMilliseconds(300) };
                    card.RenderTransform = new System.Windows.Media.TranslateTransform();
                    card.RenderTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, anim);
                    card.BeginAnimation(System.Windows.UIElement.OpacityProperty, opacityAnim);
                    await System.Threading.Tasks.Task.Delay(400);
                }
                _vm.DeleteItem(item);
                
                // Reset animation state: always use a fresh unfrozen transform
                // (the recycled container may hold a frozen XAML-defined transform)
                if (parent is System.Windows.Controls.Border c)
                {
                    c.BeginAnimation(System.Windows.UIElement.OpacityProperty, null);
                    c.Opacity = 1.0;
                    c.RenderTransform = new System.Windows.Media.TranslateTransform();
                }
            }
        }

        private void BtnAddCategory_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dlg = new InputDialog("New Category", "Enter category name:");
            dlg.Owner = this;
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Result))
            {
                string name = dlg.Result.Trim();
                if (_vm.Settings.CustomCategories.Any(c => c.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase)))
                {
                    WpfMsgBox.Show("Category already exists!", "Error", WpfMsgBoxBtn.OK, WpfMsgBoxImg.Error);
                    return;
                }
                _vm.Settings.CustomCategories.Add(new CategoryData { Name = name, Color = dlg.SelectedColor, Icon = dlg.SelectedIcon });
                _vm.SaveSettings();
                _vm.UpdateCategoryCounts();
            }
        }

        private void BtnExportFull_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "ZIP Archive|*.zip", FileName = "ClipboardPro_Backup" };
            if (dlg.ShowDialog() == true) _vm.ExportZip(dlg.FileName);
        }

        private void BtnImportFull_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "ZIP Archive|*.zip" };
            if (dlg.ShowDialog() == true) _vm.ImportZip(dlg.FileName);
        }

        private void BtnSettings_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var win = new SettingsWindow(_vm) { Owner = this };
            win.ShowDialog();
        }

        private void BtnPrettify_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is ClipboardItem item)
            {
                _vm.PrettifyJson(item);
                ShowFeedback(btn, "Formatted!", (System.Windows.Media.Brush)FindResource("SuccessBrush"));
            }
        }

        private void BtnAddCategory_Item_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement el && el.Tag is ClipboardItem item)
            {
                var menu = new System.Windows.Controls.ContextMenu();
                string activeFilter = _vm.ActiveFilter ?? "All Items";

                // 1. Status Targets
                if (activeFilter != "Pinned" && !item.IsPinned) 
                    AddMenuItem(menu, "\uE718", "Pin Item", "#e67e22", () => { item.IsPinned = true; _vm.SaveItems(); _vm.ApplyFilter(); });
                
                if (activeFilter != "Favorites" && !item.IsFavorite) 
                    AddMenuItem(menu, "\uE735", "Mark Favorite", "#f1c40f", () => { item.IsFavorite = true; _vm.SaveItems(); _vm.ApplyFilter(); });
                
                // 2. Private Target
                if (activeFilter != "Private" && !item.IsSensitive)
                {
                    AddMenuItem(menu, "\uE72E", "Move to Private", "#e74c3c", () => { 
                        item.IsSensitive = true; 
                        _vm.UpdateItemCategory(item, "Private"); 
                    });
                }

                if (menu.Items.Count > 0) menu.Items.Add(new System.Windows.Controls.Separator());

                // 3. Custom Categories
                if (_vm.Settings?.CustomCategories != null)
                {
                    foreach (var cat in _vm.Settings.CustomCategories)
                    {
                        // Skip if we are already viewing this category or it's already assigned
                        if (activeFilter == "cat:" + cat.Name || activeFilter == cat.Name || item.Category == cat.Name) continue;
                        
                        AddMenuItem(menu, cat.Icon, cat.Name, cat.Color, () => { 
                            item.IsSensitive = false;
                            _vm.UpdateItemCategory(item, cat.Name); 
                        });
                    }
                }

                // 4. "Clear" / Move to All Items
                if (activeFilter != "All Items" && (!string.IsNullOrEmpty(item.Category) || item.IsSensitive))
                {
                    if (menu.Items.Count > 0) menu.Items.Add(new System.Windows.Controls.Separator());
                    AddMenuItem(menu, "\uE711", "Remove (Move to All Items)", "#95a5a6", () => { 
                        item.IsSensitive = false;
                        _vm.UpdateItemCategory(item, ""); 
                    });
                }

                menu.PlacementTarget = el;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        private void AddMenuItem(System.Windows.Controls.ContextMenu menu, string icon, string text, string color, Action onClick)
        {
            var sp = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            sp.Children.Add(new System.Windows.Controls.TextBlock { 
                Text = icon, 
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"), 
                Margin = new System.Windows.Thickness(0, 0, 10, 0), 
                Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(color) 
            });
            sp.Children.Add(new System.Windows.Controls.TextBlock { 
                Text = text, 
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI")
            });
            
            var mi = new System.Windows.Controls.MenuItem { Header = sp };
            mi.Click += (s, e) => { onClick(); _vm.UpdateCategoryCounts(); };
            menu.Items.Add(mi);
        }

        private void BtnVisitFolder_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement el && el.Tag is ClipboardItem item)
            {
                string? path = null;
                if (item.Type == ClipboardItemType.Image && !string.IsNullOrEmpty(item.ImagePath))
                {
                    path = _vm.GetFullImagePath(item.ImagePath);
                }
                else if ((item.Type == ClipboardItemType.Path || item.Type == ClipboardItemType.Directory) && !string.IsNullOrEmpty(item.Content))
                {
                    path = item.Content;
                }

                if (!string.IsNullOrEmpty(path))
                {
                    if (System.IO.File.Exists(path))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                    }
                    else if (System.IO.Directory.Exists(path))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");
                    }
                }
            }
        }

        private bool _isDragging = false;
        private System.Windows.Point _dragStartPoint;

        private async void PasteItemToPreviousWindow(ClipboardItem item)
        {
            await ClipboardPro.Helpers.PasteHelper.PasteToActiveWindow(item, _vm, this);
        }

        private void CardBorder_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(this);
            _isDragging = false;
        }

        private void CardBorder_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isDragging) return;
            
            // Check if we clicked a button
            var parent = e.OriginalSource as System.Windows.DependencyObject;
            while (parent != null && parent != sender as System.Windows.DependencyObject)
            {
                if (parent is System.Windows.Controls.Primitives.ButtonBase) return;
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }

            if (sender is System.Windows.FrameworkElement el && el.DataContext is ClipboardItem item)
            {
                // Check if we clicked the thumbnail
                bool isThumbnail = false;
                var source = e.OriginalSource as System.Windows.DependencyObject;
                var p = source;
                while (p != null && p != el)
                {
                    if (p is System.Windows.FrameworkElement fe && (fe.Tag?.ToString() == "Thumbnail" || fe.Name == "ListThumbnail" || fe.Name == "GridThumbnail"))
                    {
                        isThumbnail = true;
                        break;
                    }
                    p = System.Windows.Media.VisualTreeHelper.GetParent(p);
                }

                if (isThumbnail)
                {
                    // Open View Window
                    ShowViewWindow(item);
                }
                else
                {
                    // Paste to previous window
                    PasteItemToPreviousWindow(item);
                }
                e.Handled = true;
            }
        }

        private void ShowViewWindow(ClipboardItem item)
        {
            try
            {
                var viewWin = new EditClipWindow(item, true) { Owner = this };
                viewWin.ShowDialog();
                _vm.SaveItems();
                _vm.ApplyFilter(); // Refresh list so Pin/Favorite changes reflect immediately
            }
            catch (System.Exception ex) { System.Windows.MessageBox.Show("Error: " + ex.Message); }
        }

        private void CardBorder_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && !_isDragging)
            {
                System.Windows.Point mousePos = e.GetPosition(this);
                System.Windows.Vector diff = _dragStartPoint - mousePos;

                if (System.Math.Abs(diff.X) > System.Windows.SystemParameters.MinimumHorizontalDragDistance ||
                    System.Math.Abs(diff.Y) > System.Windows.SystemParameters.MinimumVerticalDragDistance)
                {
                    if (sender is System.Windows.FrameworkElement el && el.DataContext is ClipboardItem item)
                    {
                        // Check if we are over a button - don't drag if we are
                        var result = System.Windows.Media.VisualTreeHelper.HitTest(el, mousePos);
                        if (result != null)
                        {
                            var parent = result.VisualHit;
                            while (parent != null && parent != el)
                            {
                                if (parent is System.Windows.Controls.Primitives.ButtonBase) return;
                                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                            }
                        }

                        _isDragging = true;
                        var dragData = new System.Windows.DataObject();
                        dragData.SetData("ClipboardItemId", item.Id); // Use ID for reliability
                        dragData.SetData(typeof(ClipboardItem), item);
                        
                        // Add Text support for Notepad and other apps
                        if (!string.IsNullOrEmpty(item.Content))
                        {
                            dragData.SetText(item.Content.TrimStart());
                        }
                        
                        // Add File support if it's an image
                        if (!string.IsNullOrEmpty(item.ImagePath))
                        {
                            string fullPath = _vm.GetFullImagePath(item.ImagePath);
                            if (System.IO.File.Exists(fullPath))
                            {
                                var files = new System.Collections.Specialized.StringCollection();
                                files.Add(fullPath);
                                dragData.SetFileDropList(files);

                                // Add Bitmap data to ensure correct sizing in apps like OneNote
                                try
                                {
                                    var bitmapSource = ClipboardPro.Helpers.ImageHelper.CreateNativeBitmapSource(fullPath);
                                    dragData.SetImage(bitmapSource);
                                }
                                catch { }
                            }
                        }
                        
                        try
                        {
                            System.Windows.DragDrop.DoDragDrop(el, dragData, System.Windows.DragDropEffects.Copy | System.Windows.DragDropEffects.Move);
                        }
                        catch (Exception) { /* Ignore COM drag errors */ }
                        finally { _isDragging = false; }
                    }
                }
            }
        }

        private void SidebarButton_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("ClipboardItemId") || e.Data.GetDataPresent(typeof(ClipboardItem))) 
            { 
                e.Effects = System.Windows.DragDropEffects.Move; 
                e.Handled = true; 
            }
        }

        private void SidebarButton_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("ClipboardItemId") || e.Data.GetDataPresent(typeof(ClipboardItem))) 
            { 
                e.Effects = System.Windows.DragDropEffects.Move; 
                e.Handled = true; 
            }
        }

        private void SidebarButton_Drop(object sender, System.Windows.DragEventArgs e)
        {
            try
            {
                ClipboardItem? item = null;

                if (e.Data.GetDataPresent("ClipboardItemId"))
                {
                    var id = e.Data.GetData("ClipboardItemId") as string;
                    item = _vm.GetItemById(id);
                }
                else if (e.Data.GetDataPresent(typeof(ClipboardItem)))
                {
                    item = e.Data.GetData(typeof(ClipboardItem)) as ClipboardItem;
                }

                if (item != null && sender is System.Windows.Controls.Button btn)
                {
                    string tag = btn.Tag?.ToString() ?? "";
                    
                    if (tag == "Favorites") item.IsFavorite = true;
                    else if (tag == "Pinned") item.IsPinned = true;
                    else if (tag == "Private") 
                    {
                        item.IsSensitive = true;
                        _vm.UpdateItemCategory(item, "Private"); // Also set name for consistency
                    }
                    else if (tag == "All Items" || tag == "All") 
                    {
                        item.IsFavorite = false;
                        item.IsPinned = false;
                        item.IsSensitive = false;
                        _vm.UpdateItemCategory(item, "");
                    }
                    else if (tag.StartsWith("cat:") || (_vm.Settings?.CustomCategories?.Any(c => string.Equals(c.Name, tag, StringComparison.OrdinalIgnoreCase)) ?? false)) 
                    {
                        string catName = tag.StartsWith("cat:") ? tag.Substring(4) : tag;
                        item.IsSensitive = false; // Move out of private when moving to custom cat
                        _vm.UpdateItemCategory(item, catName);
                    }
                    else if (new[] { "URL", "Email", "Code", "Phone", "Image", "Color", "Path" }.Contains(tag)) 
                    {
                        item.IsSensitive = false; // Move out of private when moving back to stock type
                        _vm.UpdateItemCategory(item, ""); // Move back to main flow
                    }
                    
                    _vm.SaveItems();
                    e.Handled = true;
                    
                    // Add visual feedback for drop
                    var oldBg = btn.Background;
                    btn.Background = (System.Windows.Media.Brush)FindResource("AccentLight");
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(200) };
                    timer.Tick += (s, ev) => { btn.Background = oldBg; timer.Stop(); };
                    timer.Start();

                    // Optional: Diagnostic message to confirm it worked
                    // System.Windows.MessageBox.Show($"Dropped into {tag}");
                }
            }
            catch (System.Exception ex) 
            { 
                System.Windows.MessageBox.Show($"Drop Error: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private QuickPasteBarWindow? _quickPasteBar;

        private void BtnQuickPasteBar_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _vm.Settings.EnableQuickPasteBar = !_vm.Settings.EnableQuickPasteBar;
            _vm.SaveSettings();
            UpdateQuickPasteBarState();
        }

        public void ToggleQuickPasteBar()
        {
            _vm.Settings.EnableQuickPasteBar = !_vm.Settings.EnableQuickPasteBar;
            _vm.SaveSettings();
            UpdateQuickPasteBarState();
            if (_vm.Settings.EnableQuickPasteBar && _quickPasteBar != null) _quickPasteBar.Activate();
        }

        public void UpdateQuickPasteBarState(bool silent = false)
        {
            if (_vm.Settings.EnableQuickPasteBar)
            {
                if (_quickPasteBar == null || !_quickPasteBar.IsLoaded)
                {
                    _quickPasteBar = new QuickPasteBarWindow(_vm);
                }
                _quickPasteBar.Show();
                
                if (!silent && this.WindowState == System.Windows.WindowState.Minimized)
                {
                    this.WindowState = System.Windows.WindowState.Normal;
                }
            }
            else
            {
                _quickPasteBar?.Close();
                _quickPasteBar = null;
            }
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            var batch = btn?.Tag as MainViewModel.UndoBatch;
            _vm.UndoDelete(batch);
        }

        private void BtnAddSnippet_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var dlg = new AddSnippetDialog { Owner = this };
            if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Trigger))
            {
                var item = new ClipboardPro.Models.SnippetItem
                {
                    Trigger = dlg.Trigger.Trim(),
                    Content = dlg.Content,
                    Description = dlg.Description,
                };
                _vm.AddOrUpdateSnippet(item);
            }
        }

        private void BtnEditSnippet_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement el && el.Tag is ClipboardPro.Models.SnippetItem existing)
            {
                var dlg = new AddSnippetDialog(existing) { Owner = this };
                if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Trigger))
                {
                    existing.Trigger = dlg.Trigger.Trim();
                    existing.Content = dlg.Content;
                    existing.Description = dlg.Description;
                    _vm.AddOrUpdateSnippet(existing);
                }
            }
        }

        private async void BtnCopySnippet_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement el && el.Tag is ClipboardPro.Models.SnippetItem snippet)
            {
                System.Windows.Clipboard.SetText(snippet.Content);
                if (sender is System.Windows.Controls.Button btn) ShowFeedback(btn, "Copied!", (System.Windows.Media.Brush)FindResource("SuccessBrush"));

                // Show feedback overlay if available
                var parent = System.Windows.Media.VisualTreeHelper.GetParent(el) as System.Windows.FrameworkElement;
                if (parent != null)
                {
                    for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
                    {
                        var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                        if (child is System.Windows.Controls.Border toast && (toast.Name == "CopyFeedbackList" || toast.Name == "CopyFeedbackGrid"))
                        {
                            toast.Opacity = 1;
                            await System.Threading.Tasks.Task.Delay(2000);
                            toast.BeginAnimation(System.Windows.UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, System.TimeSpan.FromMilliseconds(500)));
                            break;
                        }
                    }
                }
            }
        }

        private void BtnDeleteSnippet_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement el && el.Tag is ClipboardPro.Models.SnippetItem snippet)
            {
                if (WpfMsgBox.Show($"Delete snippet '{snippet.Trigger}'?", "Delete Snippet", WpfMsgBoxBtn.YesNo) == WpfMsgBoxResult.Yes)
                {
                    _vm.DeleteSnippet(snippet);
                }
            }
        }

        private void CardBorder_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.Border card)
            {
                // Stop any running opacity animation and reset to full opacity
                card.BeginAnimation(System.Windows.UIElement.OpacityProperty, null);
                card.Opacity = 1.0;

                // IMPORTANT: The XAML DataTemplate defines <TranslateTransform X="0"/> on CardBorder.
                // WPF freezes (seals) DataTemplate resource objects so they can be shared across
                // recycled VirtualizingStackPanel containers. Calling BeginAnimation() or setting
                // properties on a frozen Animatable throws InvalidOperationException.
                // Fix: always replace with a fresh, unfrozen instance instead of mutating the existing one.
                card.RenderTransform = new System.Windows.Media.TranslateTransform();
            }
        }
    }
}
