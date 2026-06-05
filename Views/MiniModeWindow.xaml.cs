using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClipboardPro.Models;
using ClipboardPro.ViewModels;
using System.Windows.Media;
using System.ComponentModel;
using System;

// Explicit WPF aliases to avoid WinForms conflicts
using WpfKey    = System.Windows.Input.Key;
using WpfKeyArgs = System.Windows.Input.KeyEventArgs;
using WpfApp    = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using System.Windows.Interop;

namespace ClipboardPro.Views
{
    public partial class MiniModeWindow : Window
    {
        private readonly MainViewModel _vm;
        private bool _isClosing = false;
        private readonly System.Collections.Generic.List<string> _availableCategories = new();
        private int _currentCategoryIndex = 0;
        private System.DateTime _lastCategorySwitch = System.DateTime.MinValue;

        private System.Collections.Specialized.NotifyCollectionChangedEventHandler? _collectionChangedHandler;
        private PropertyChangedEventHandler? _propertyChangedHandler;

        public MiniModeWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = _vm;
            Opacity = _vm.Settings.WindowOpacity;

            // DPI-Aware Positioning
            var pos = System.Windows.Forms.Cursor.Position;
            var screen = System.Windows.Forms.Screen.FromPoint(pos);
            var area = screen.WorkingArea;

            var dpi = VisualTreeHelper.GetDpi(this);
            double scaleX = dpi.DpiScaleX;
            double scaleY = dpi.DpiScaleY;

            double mouseXLogical = pos.X / scaleX;
            double mouseYLogical = pos.Y / scaleY;
            
            double areaLeftLogical = area.Left / scaleX;
            double areaTopLogical = area.Top / scaleY;
            double areaRightLogical = area.Right / scaleX;
            double areaBottomLogical = area.Bottom / scaleY;

            double w = this.Width;
            double h = this.Height;

            double left = mouseXLogical - (w / 2);
            double top = mouseYLogical - (h / 2);

            double padding = 8.0;
            if (left < areaLeftLogical + padding) 
                left = areaLeftLogical + padding;
            
            if (left + w > areaRightLogical - padding) 
                left = areaRightLogical - w - padding;
            
            if (top < areaTopLogical + padding) 
                top = areaTopLogical + padding;
            
            if (top + h > areaBottomLogical - padding) 
                top = areaBottomLogical - h - padding;

            this.Left = left;
            this.Top = top;

            InitializeCategories();
            LoadItems(string.Empty);
            Deactivated += (_, _) => { if (!_isClosing && !_vm.Settings.AlwaysOnTop) AnimateClose(); };

            TxtMiniSearch.TextChanged += (_, _) =>
                MiniSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtMiniSearch.Text)
                    ? Visibility.Visible : Visibility.Collapsed;

            this.Loaded += (s, e) => 
            {
                var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                source?.AddHook(WndProc);

                TxtMiniSearch.Focus();
                AnimateOpen();
                
                _collectionChangedHandler = (s2, e2) => LoadItems(TxtMiniSearch.Text);
                _vm.FilteredItems.CollectionChanged += _collectionChangedHandler;
                
                _propertyChangedHandler = (s2, e2) => {
                    if (e2.PropertyName == nameof(MainViewModel.FilteredItems)) {
                        LoadItems(TxtMiniSearch.Text);
                    }
                    if (e2.PropertyName == nameof(MainViewModel.ActiveFilter)) {
                         int index = _availableCategories.IndexOf(_vm.ActiveFilter);
                         if (index >= 0) _currentCategoryIndex = index;
                         UpdateCategoryUI();
                    }
                };
                _vm.PropertyChanged += _propertyChangedHandler;
            };

            this.Closed += (s, e) =>
            {
                if (_collectionChangedHandler != null)
                {
                    _vm.FilteredItems.CollectionChanged -= _collectionChangedHandler;
                }
                if (_propertyChangedHandler != null)
                {
                    _vm.PropertyChanged -= _propertyChangedHandler;
                }

                try
                {
                    var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
                    source?.RemoveHook(WndProc);
                }
                catch { }
            };
        }

        private const int WM_MOUSEHWHEEL = 0x020E;
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEHWHEEL)
            {
                long wp = wParam.ToInt64();
                int tilt = (short)((wp >> 16) & 0xFFFF);
                if (Math.Abs(tilt) > 10) 
                {
                    SwitchCategory(tilt > 0 ? 1 : -1);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void AnimateOpen()
        {
            Opacity = 0;
            RootTransform.Y = 40;
            
            double targetOpacity = _vm?.Settings?.WindowOpacity ?? 1.0;
            var fadeAnim = new System.Windows.Media.Animation.DoubleAnimation(targetOpacity, TimeSpan.FromMilliseconds(250));
            fadeAnim.Completed += (s, e) => {
                BeginAnimation(OpacityProperty, null); 
                Opacity = targetOpacity;
            };
            var slideAnim = new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new System.Windows.Media.Animation.QuarticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            
            BeginAnimation(OpacityProperty, fadeAnim);
            RootTransform.BeginAnimation(TranslateTransform.YProperty, slideAnim);
        }

        private async void AnimateClose()
        {
            if (_isClosing) return;
            _isClosing = true;

            var fadeAnim = new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
            var slideAnim = new System.Windows.Media.Animation.DoubleAnimation(40, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new System.Windows.Media.Animation.QuarticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
            };

            BeginAnimation(OpacityProperty, fadeAnim);
            RootTransform.BeginAnimation(TranslateTransform.YProperty, slideAnim);

            await System.Threading.Tasks.Task.Delay(250);
            _vm.TrimMemory(); 
            Close();
        }

        private void BtnAlwaysOnTop_Click(object sender, RoutedEventArgs e)
        {
            _vm.Settings.AlwaysOnTop = !_vm.Settings.AlwaysOnTop;
            _vm.SaveSettings();
            this.Topmost = _vm.Settings.AlwaysOnTop;
        }

        private void BtnQuickPasteBar_Click(object sender, RoutedEventArgs e)
        {
            _vm.Settings.EnableQuickPasteBar = !_vm.Settings.EnableQuickPasteBar;
            _vm.SaveSettings();

            if (System.Windows.Application.Current.MainWindow is MainWindow main)
            {
                main.UpdateQuickPasteBarState(true);
            }
        }

        private void InitializeCategories()
        {
            _availableCategories.Clear();
            _availableCategories.Add("All Items");
            _availableCategories.Add("Favorites");
            _availableCategories.Add("Pinned");
            _availableCategories.Add("URL");
            _availableCategories.Add("Email");
            _availableCategories.Add("Code");
            _availableCategories.Add("Phone");
            _availableCategories.Add("Image");
            _availableCategories.Add("Color");
            _availableCategories.Add("Path");
            _availableCategories.Add("Directory");
            _availableCategories.Add("Private");

            if (_vm.Settings?.CustomCategories != null)
            {
                foreach (var cat in _vm.Settings.CustomCategories)
                {
                    _availableCategories.Add("cat:" + cat.Name);
                }
            }
            
            _currentCategoryIndex = _availableCategories.IndexOf(_vm.ActiveFilter);
            if (_currentCategoryIndex < 0) _currentCategoryIndex = 0;
            UpdateCategoryUI();
        }

        private void UpdateCategoryUI()
        {
            if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _availableCategories.Count) return;
            string filter = _availableCategories[_currentCategoryIndex];
            string displayName = filter;
            if (filter.StartsWith("cat:")) displayName = filter.Substring(4);
            else
            {
                displayName = filter switch
                {
                    "Color" => "Colors",
                    "Path" => "File Received",
                    _ => filter
                };
            }
            TxtCurrentCategory.Text = displayName;
        }

        private void SwitchCategory(int direction)
        {
            if (_availableCategories.Count == 0) return;
            
            if ((System.DateTime.Now - _lastCategorySwitch).TotalMilliseconds < 250) return;
            _lastCategorySwitch = System.DateTime.Now;

            _currentCategoryIndex += direction;
            if (_currentCategoryIndex < 0) _currentCategoryIndex = _availableCategories.Count - 1;
            if (_currentCategoryIndex >= _availableCategories.Count) _currentCategoryIndex = 0;
            
            _vm.ActiveFilter = _availableCategories[_currentCategoryIndex];
        }

        private void BtnPrevCategory_Click(object sender, RoutedEventArgs e) => SwitchCategory(-1);
        private void BtnNextCategory_Click(object sender, RoutedEventArgs e) => SwitchCategory(1);

        private void CategorySelector_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            SwitchCategory(e.Delta > 0 ? -1 : 1);
            e.Handled = true;
        }

        private void CategorySelector_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void CategorySelector_ManipulationCompleted(object sender, System.Windows.Input.ManipulationCompletedEventArgs e)
        {
            var deltaX = e.TotalManipulation.Translation.X;
            if (Math.Abs(deltaX) > 60)
            {
                SwitchCategory(deltaX > 0 ? -1 : 1);
                e.Handled = true;
            }
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            var batch = btn?.Tag as MainViewModel.UndoBatch;
            _vm.UndoDelete(batch);
        }

        private void LoadItems(string query)
        {
            bool filterImages = query.Contains("@image", StringComparison.OrdinalIgnoreCase);
            string actualQuery = filterImages ? query.Replace("@image", "", StringComparison.OrdinalIgnoreCase).Trim() : query;

            var items = _vm.FilteredItems
                .Where(i => 
                {
                    bool matchesQuery = string.IsNullOrEmpty(actualQuery) || i.Content.Contains(actualQuery, StringComparison.OrdinalIgnoreCase);
                    bool matchesType = !filterImages || !string.IsNullOrEmpty(i.ImagePath);
                    return matchesQuery && matchesType;
                })
                .ToList();
            PopupList.ItemsSource = items;
        }

        private void TxtMiniSearch_TextChanged(object sender, TextChangedEventArgs e)
            => LoadItems(TxtMiniSearch.Text);

        private void TxtMiniSearch_KeyDown(object sender, WpfKeyArgs e)
        {
            if (e.Key == WpfKey.Escape) { _isClosing = true; Close(); }
            if (e.Key == WpfKey.Enter)
            {
                var first = _vm.FilteredItems.FirstOrDefault();
                if (first != null) PasteAndClose(first);
            }
        }

        private void PopupItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                if (FindParent<WpfButton>(dep) != null) return;
            }

            if (((Border)sender).Tag is ClipboardItem item)
                PasteAndClose(item);
        }

        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }

        private void BtnToggleMask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn && btn.Tag is ClipboardItem item)
            {
                item.IsMasked = !item.IsMasked;
            }
        }

        private void BtnPin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn && btn.Tag is ClipboardItem item)
                _vm.TogglePin(item);
        }

        private void BtnFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn && btn.Tag is ClipboardItem item)
                _vm.ToggleFavorite(item);
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is ClipboardItem item)
            {
                var border = FindParent<Border>(el);
                if (border != null)
                {
                    var anim = new System.Windows.Media.Animation.DoubleAnimation(400, TimeSpan.FromMilliseconds(250))
                    {
                        EasingFunction = new System.Windows.Media.Animation.QuarticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
                    };
                    var trans = new TranslateTransform();
                    border.RenderTransform = trans;
                    trans.BeginAnimation(TranslateTransform.XProperty, anim);
                    border.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(200)));
                    await System.Threading.Tasks.Task.Delay(250);
                }
                
                _vm.DeleteItem(item);
                LoadItems(TxtMiniSearch.Text); 
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, System.UIntPtr dwExtraInfo);
        
        private const int KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;
        private const byte VK_MENU = 0x12; 
        private const byte VK_TAB = 0x09;

        private async void PasteAndClose(ClipboardItem item)
        {
            _vm.CopyItem(item);
            await System.Threading.Tasks.Task.Delay(100);

            bool isPinned = _vm.Settings.AlwaysOnTop;

            if (!isPinned)
            {
                this.Hide();
                AnimateClose();
            }
            
            bool originalTopmost = this.Topmost;
            await System.Threading.Tasks.Task.Delay(400);
            
            keybd_event(VK_CONTROL, 0, 0, System.UIntPtr.Zero);
            keybd_event(VK_V, 0, 0, System.UIntPtr.Zero);
            await System.Threading.Tasks.Task.Delay(60);
            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, System.UIntPtr.Zero);

            if (isPinned && !_isClosing)
            {
                await System.Threading.Tasks.Task.Delay(300); 
                this.Topmost = originalTopmost;
                this.Activate(); 
            }
            else if (!_isClosing)
            {
                await System.Threading.Tasks.Task.Delay(300);
                this.Topmost = originalTopmost;
            }
        }

        private void Border_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Border border)
            {
                border.BeginAnimation(OpacityProperty, null);
                border.Opacity = 1.0;
                if (border.RenderTransform is TranslateTransform tt)
                {
                    tt.BeginAnimation(TranslateTransform.XProperty, null);
                    tt.X = 0;
                }
                else
                {
                    border.RenderTransform = null;
                }
            }
        }
    }
}
