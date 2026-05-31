using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipboardPro.ViewModels;
using ClipboardPro.Models;
using System.Windows.Input;
using WpfDragEventArgs  = System.Windows.DragEventArgs;
using WpfButton         = System.Windows.Controls.Button;
using WpfDataFormats    = System.Windows.DataFormats;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfDataObject     = System.Windows.DataObject;
using WpfFontFamily     = System.Windows.Media.FontFamily;
using WpfHA             = System.Windows.HorizontalAlignment;
using WpfVA             = System.Windows.VerticalAlignment;

namespace ClipboardPro.Views
{
    public partial class QuickPasteBarWindow : Window
    {
        private readonly MainViewModel _vm;
        private bool _isInitialized = false;

        public QuickPasteBarWindow(MainViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = _vm;
            
            _vm.ShelfPaths.CollectionChanged += ShelfPaths_CollectionChanged;
            _vm.ShelfSelectionChanged += ShelfSelectionChangedHandler;
            this.Closed += (s, e) => {
                _vm.ShelfPaths.CollectionChanged -= ShelfPaths_CollectionChanged;
                _vm.ShelfSelectionChanged -= ShelfSelectionChangedHandler;
            };
            
            new ClipboardPro.Helpers.LassoSelectionHelper(ShelfSelectionGrid, SelectionBox, ShelfItemsPanel, _vm.SelectedShelfPaths, _vm);
            
            this.LocationChanged += (s, e) => {
                if (_isInitialized && this.WindowState == WindowState.Normal)
                {
                    _vm.Settings.QuickPasteBarX = this.Left;
                    _vm.Settings.QuickPasteBarY = this.Top;
                    
                    // Debounce saving to disk
                    SavePositionDebounced();
                }
            };
        }

        private void ShelfPaths_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Dispatch to UI thread to prevent cross-thread crashes during collection modification
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(RefreshShelfUI));
        }

        private System.Windows.Threading.DispatcherTimer? _saveTimer;
        private void SavePositionDebounced()
        {
            if (_saveTimer == null)
            {
                _saveTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                _saveTimer.Tick += (s, e) => {
                    _saveTimer.Stop();
                    _vm.SaveSettings();
                };
            }
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void Border_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) DragMove();
        }

        private void BtnToggleMask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is ClipboardItem item)
            {
                item.IsMasked = !item.IsMasked;
                e.Handled = true; // Critical: Stop event from reaching the main Item_Click
            }
        }

        private async void Item_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is ClipboardItem item)
            {
                await ClipboardPro.Helpers.PasteHelper.PasteToActiveWindow(item, _vm, this);
            }
        }

        private void BtnPrettify_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem mi && mi.Tag is ClipboardItem item)
            {
                _vm.PrettifyJson(item);
            }
        }

        private void BtnPin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem mi && mi.Tag is ClipboardItem item)
            {
                _vm.TogglePin(item);
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem mi && mi.Tag is ClipboardItem item)
            {
                _vm.DeleteItem(item);
            }
        }

        private System.Windows.Point _dragStart;
        private void Item_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && sender is FrameworkElement el && el.Tag is ClipboardItem item)
            {
                var diff = _dragStart - e.GetPosition(null);
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var dragData = new System.Windows.DataObject();
                    dragData.SetData(typeof(ClipboardItem), item);
                    
                    if (!string.IsNullOrEmpty(item.Content))
                    {
                        dragData.SetText(item.Content.TrimStart());
                    }
                    
                    if (!string.IsNullOrEmpty(item.ImagePath))
                    {
                        string fullPath = _vm.GetFullImagePath(item.ImagePath);
                        if (System.IO.File.Exists(fullPath))
                        {
                            var files = new System.Collections.Specialized.StringCollection { fullPath };
                            dragData.SetFileDropList(files);
                            try
                            {
                                var bitmapSource = ClipboardPro.Helpers.ImageHelper.CreateNativeBitmapSource(fullPath);
                                dragData.SetImage(bitmapSource);
                            } catch { }
                        }
                    }
                    
                    try { System.Windows.DragDrop.DoDragDrop(el, dragData, System.Windows.DragDropEffects.Copy); } catch { }
                }
            }
        }

        private void Item_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(null);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitialized)
            {
                _vm.Settings.QuickPasteBarX = this.Left;
                _vm.Settings.QuickPasteBarY = this.Top;
            }
            // _vm.Settings.EnableQuickPasteBar = false; // Removed to prevent permanent off
            _vm.SaveSettings(); // Final save position

            _vm.TrimMemory(); // Force RAM release
            Close();
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var sv = sender as ScrollViewer ?? FindScrollViewer(sender as DependencyObject);
            if (sv != null)
            {
                if (e.Delta > 0)
                    sv.LineLeft();
                else
                    sv.LineRight();
                e.Handled = true;
            }
        }

        private const int WM_MOUSEHWHEEL = 0x020E;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Restore position or center at top
            if (_vm.Settings.QuickPasteBarX > -9000 && _vm.Settings.QuickPasteBarY > -9000)
            {
                this.Left = _vm.Settings.QuickPasteBarX;
                this.Top = _vm.Settings.QuickPasteBarY;
            }
            else
            {
                // Default: Horizontal Center, Top 20
                this.Left = (SystemParameters.PrimaryScreenWidth - 460) / 2;
                this.Top = 20;
            }

            _isInitialized = true;

            var source = System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            source?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEHWHEEL)
            {
                var sv = FindScrollViewer(MainScroll);
                if (sv != null)
                {
                    int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                    if (delta > 0)
                        sv.LineRight();
                    else if (delta < 0)
                        sv.LineLeft();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private ScrollViewer? FindScrollViewer(DependencyObject obj)
        {
            if (obj is ScrollViewer sv) return sv;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var res = FindScrollViewer(VisualTreeHelper.GetChild(obj, i));
                if (res != null) return res;
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // TEMPORARY SHELF — Drag & Drop from Windows Explorer or browser
        // Memory strategy: stores only string file paths (no file content loaded)
        // ─────────────────────────────────────────────────────────────────────

        private void BtnShelf_Click(object sender, RoutedEventArgs e)
        {
            ShelfPopup.IsOpen = !ShelfPopup.IsOpen;
        }

        // --- Drag visual feedback ---
        private void Shelf_DragEnter(object sender, WpfDragEventArgs e)
        {
            if (e.Data.GetDataPresent(WpfDataFormats.FileDrop) || e.Data.GetDataPresent(WpfDataFormats.Text) || e.Data.GetDataPresent(WpfDataFormats.UnicodeText))
            {
                e.Effects = WpfDragDropEffects.Copy;
                TxtShelfIcon.Text = "\uE896"; // cloud download icon = "accepting"
                TxtShelfIcon.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "AccentPrimary");
                e.Handled = true;
            }
        }

        private void Shelf_DragOver(object sender, WpfDragEventArgs e)
        {
            if (e.Data.GetDataPresent(WpfDataFormats.FileDrop) || e.Data.GetDataPresent(WpfDataFormats.Text) || e.Data.GetDataPresent(WpfDataFormats.UnicodeText))
            {
                e.Effects = WpfDragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void Shelf_DragLeave(object sender, WpfDragEventArgs e)
        {
            TxtShelfIcon.Text = "\uE8B7"; // folder icon
            TxtShelfIcon.ClearValue(System.Windows.Controls.TextBlock.ForegroundProperty);
        }

        // --- Accept the drop ---
        private void Shelf_Drop(object sender, WpfDragEventArgs e)
        {
            try
            {
                TxtShelfIcon.Text = "\uE8B7";
                TxtShelfIcon.ClearValue(System.Windows.Controls.TextBlock.ForegroundProperty);

                // 1. Handle Files
                if (e.Data.GetDataPresent(WpfDataFormats.FileDrop))
                {
                    var paths = e.Data.GetData(WpfDataFormats.FileDrop) as string[];
                    if (paths != null)
                    {
                        foreach (var p in paths)
                        {
                            if (!string.IsNullOrEmpty(p) && !_vm.ShelfPaths.Contains(p)) _vm.ShelfPaths.Add(p);
                        }
                    }
                }
                // 2. Handle Web URLs (Images/Links)
                else if (e.Data.GetDataPresent(WpfDataFormats.Text) || e.Data.GetDataPresent(WpfDataFormats.UnicodeText))
                {
                    var text = (e.Data.GetData(WpfDataFormats.UnicodeText) ?? e.Data.GetData(WpfDataFormats.Text)) as string;
                    if (!string.IsNullOrWhiteSpace(text) && (text.StartsWith("http") || text.StartsWith("www")))
                    {
                        if (!_vm.ShelfPaths.Contains(text)) _vm.ShelfPaths.Add(text);
                    }
                }
            }
            catch { }

            RefreshShelfUI();
            e.Handled = true;
            ShelfPopup.IsOpen = true;
        }

        private void ShelfSelectionChangedHandler()
        {
            RefreshShelfUI();
        }

        private void BtnClearShelf_Click(object sender, RoutedEventArgs e)
        {
            _vm.SelectedShelfPaths.Clear();
            _vm.ShelfPaths.Clear();
            _vm.NotifyShelfSelectionChanged();
            RefreshShelfUI();
        }

        private void RefreshShelfUI()
        {
            try
            {
                int count = _vm.ShelfPaths.Count;

                // Update badge
                ShelfBadge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
                TxtShelfCount.Text    = count.ToString();

                // Clear selections for items that no longer exist on the shelf
                _vm.SelectedShelfPaths.RemoveWhere(path => !_vm.ShelfPaths.Contains(path));

                ShelfEmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
                ShelfItemsPanel.Children.Clear();

                foreach (var path in System.Linq.Enumerable.ToList(_vm.ShelfPaths))
                {
                    try
                    {
                        var card = ClipboardPro.Helpers.UIHelper.BuildShelfCard(path, _vm, _vm.SelectedShelfPaths, () => _vm.NotifyShelfSelectionChanged());
                        if (card != null)
                        {
                            ShelfItemsPanel.Children.Add(card);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }


    }
}
