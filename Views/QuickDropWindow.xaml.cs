using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using ClipboardPro.ViewModels;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfButton = System.Windows.Controls.Button;
using WpfDataObject = System.Windows.DataObject;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDragDropEffects = System.Windows.DragDropEffects;

namespace ClipboardPro.Views
{
    public partial class QuickDropWindow : Window
    {
        private readonly MainViewModel _vm;
        private readonly Action _onClick;
        private System.Windows.Point _startScreenPos;
        private double _lastMouseY;
        private bool _isDragging;
        private bool _isShelfOpen;
        private DateTime _lastClosedTime = DateTime.MinValue;
        private double _dockedLeftPosition;
        private bool _isMouseOverBubble = false;

        public QuickDropWindow(MainViewModel vm, Action onClick)
        {
            InitializeComponent();
            _vm = vm;
            _onClick = onClick;

            // Set manual startup location
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            this.ShowActivated = false;

            // Position initially on the right edge of the screen
            var workArea = SystemParameters.WorkArea;
            double initialWidth = 50; // Match Width in XAML
            double initialHeight = 50; // Match Height in XAML
            
            // Standard Right Dock position (half-hidden by default)
            _dockedLeftPosition = workArea.Right - initialWidth + 22; 
            this.Left = _dockedLeftPosition;
            this.Top = (workArea.Height / 2) - (initialHeight / 2);

            _vm.ShelfPaths.CollectionChanged += ShelfPaths_CollectionChanged;
            _vm.ShelfSelectionChanged += ShelfSelectionChangedHandler;
            this.Closed += (s, e) => {
                _vm.ShelfPaths.CollectionChanged -= ShelfPaths_CollectionChanged;
                _vm.ShelfSelectionChanged -= ShelfSelectionChangedHandler;
                // Clean up global handlers
                try { Mouse.RemovePreviewMouseDownHandler(System.Windows.Application.Current.MainWindow, OnGlobalMouseDownOutside); } catch { }
                try { System.Windows.Application.Current.Deactivated -= OnAppDeactivated; } catch { }
            };

            new ClipboardPro.Helpers.LassoSelectionHelper(ShelfSelectionGrid, SelectionBox, ShelfItemsPanel, _vm.SelectedShelfPaths, _vm);

            this.Loaded += (s, e) =>
            {
                BadgeBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                RefreshShelfUI();
                
                // Add app-wide deactivation to dismiss shelf popup
                System.Windows.Application.Current.Deactivated += OnAppDeactivated;
            };
        }

        private void ShelfPaths_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Dispatch UI updates safely to the UI thread
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(RefreshShelfUI));
        }

        public void UpdateIcon(string glyph)
        {
            IconTxt.Text = glyph;
        }

        private void UpdateBadge()
        {
            int count = _vm.ShelfPaths.Count;
            if (count > 0)
            {
                BadgeBorder.Visibility = Visibility.Visible;
                TxtBadge.Text = count.ToString();
            }
            else
            {
                BadgeBorder.Visibility = Visibility.Collapsed;
                // If shelf gets empty and action isn't Temporary Shelf, close the shelf
                if (_isShelfOpen && _vm.Settings.QuickDropAction != 5)
                {
                    CloseShelf();
                }
            }
        }

        // ── edge-docking Hover Animations ─────────────────────────────────

        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _isMouseOverBubble = true;
            if (Resources["OnMouseEnter"] is Storyboard sb)
            {
                sb.Begin(this);
            }
            SlideOutFromEdge();
        }

        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _isMouseOverBubble = false;
            if (Resources["OnMouseLeave"] is Storyboard sb)
            {
                sb.Begin(this);
            }
            
            // Only dock/slide back into edge if the shelf popup is NOT currently open
            if (!_isShelfOpen)
            {
                SlideIntoEdge();
            }
        }

        private void SlideOutFromEdge()
        {
            var workArea = SystemParameters.WorkArea;
            double targetLeft;
            double centerX = this.Left + (this.Width / 2);

            if (centerX < workArea.Width / 2)
            {
                // Left Edge: fully reveal
                targetLeft = workArea.Left - 10;
            }
            else
            {
                // Right Edge: fully reveal
                targetLeft = workArea.Right - this.Width + 10;
            }

            AnimatePosition(targetLeft);
        }

        private void SlideIntoEdge()
        {
            var workArea = SystemParameters.WorkArea;
            double targetLeft;
            double centerX = this.Left + (this.Width / 2);

            if (centerX < workArea.Width / 2)
            {
                // Dock Left (half-hidden)
                targetLeft = workArea.Left - (this.Width / 2) - 4;
            }
            else
            {
                // Dock Right (half-hidden)
                targetLeft = workArea.Right - (this.Width / 2) + 4;
            }

            _dockedLeftPosition = targetLeft;
            AnimatePosition(targetLeft);
        }

        private void AnimatePosition(double targetLeft)
        {
            this.BeginAnimation(Window.LeftProperty, new DoubleAnimation
            {
                To = targetLeft,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }

        // ── Drag and Position Logic ───────────────────────────────────────

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            try
            {
                _startScreenPos = PointToScreen(e.GetPosition(this));
                _lastMouseY = _startScreenPos.Y;
                CaptureMouse();
            }
            catch { }
        }

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && IsMouseCaptured)
            {
                try
                {
                    var currentScreenPos = PointToScreen(e.GetPosition(this));
                    if (!_isDragging && Math.Abs(currentScreenPos.Y - _startScreenPos.Y) > 8)
                    {
                        _isDragging = true;
                        if (_isShelfOpen) CloseShelf();
                    }

                    if (_isDragging)
                    {
                        this.Top += currentScreenPos.Y - _lastMouseY;
                        _lastMouseY = currentScreenPos.Y;
                    }
                }
                catch { }
            }
        }

        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }

            if (!_isDragging)
            {
                var pos = e.GetPosition(BubbleBorder);
                bool onBubble = pos.X >= 0 && pos.X <= BubbleBorder.ActualWidth &&
                                pos.Y >= 0 && pos.Y <= BubbleBorder.ActualHeight;
                if (onBubble)
                {
                    ToggleShelf();
                }
            }
            else
            {
                SnapToEdge();
            }
        }

        private void SnapToEdge()
        {
            var workArea = SystemParameters.WorkArea;
            double centerX = this.Left + (this.Width / 2);
            double targetLeft;

            if (centerX < workArea.Width / 2)
            {
                targetLeft = workArea.Left - (this.Width / 2) - 4;
                BadgeBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            }
            else
            {
                targetLeft = workArea.Right - (this.Width / 2) + 4;
                BadgeBorder.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            }

            _dockedLeftPosition = targetLeft;

            this.BeginAnimation(Window.LeftProperty, new DoubleAnimation
            {
                To = targetLeft,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }

        // ── Drag & Drop on Bubble Window ──────────────────────────────────

        private void Window_DragEnter(object sender, WpfDragEventArgs e)
        {
            if (HasDroppableContent(e))
            {
                e.Effects = WpfDragDropEffects.Copy;
                e.Handled = true;
                OpenShelf();
            }
        }

        private void Window_DragOver(object sender, WpfDragEventArgs e)
        {
            if (HasDroppableContent(e))
            {
                e.Effects = WpfDragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void Window_DragLeave(object sender, WpfDragEventArgs e)
        {
            // Close empty shelf when drag leaves
            if (_vm.ShelfPaths.Count == 0 && _isShelfOpen)
            {
                CloseShelf();
            }
        }

        private void Window_Drop(object sender, WpfDragEventArgs e)
        {
            try 
            { 
                AddDroppedItems(e); 
            }
            catch { }
            e.Handled = true;
            OpenShelf();
        }

        // ── Shelf Management & Click-Outside Dismissal ───────────────────

        public void OpenShelf()
        {
            if (_isShelfOpen)
            {
                if (!ShelfPopup.IsOpen) ShelfPopup.IsOpen = true;
                return;
            }

            _isShelfOpen = true;

            // Slide out fully so it doesn't hide when shelf is open
            SlideOutFromEdge();

            // Set placement direction dynamically based on bubble position
            var workArea = SystemParameters.WorkArea;
            double centerX = this.Left + (this.Width / 2);
            ShelfPopup.Placement = centerX < workArea.Width / 2
                ? System.Windows.Controls.Primitives.PlacementMode.Right
                : System.Windows.Controls.Primitives.PlacementMode.Left;

            ShelfPopup.IsOpen = true;
            RefreshShelfUI();
        }

        public void CloseShelf()
        {
            if (!_isShelfOpen) return;
            _isShelfOpen = false;
            ShelfPopup.IsOpen = false;

            // Slide back to edge if the mouse is not currently hovering over the bubble
            if (!_isMouseOverBubble)
            {
                SlideIntoEdge();
            }
        }

        public void ToggleShelf()
        {
            if ((DateTime.Now - _lastClosedTime).TotalMilliseconds < 250) return;

            if (_isShelfOpen)
            {
                CloseShelf();
            }
            else
            {
                if (_vm.Settings.QuickDropAction == 5)
                {
                    OpenShelf();
                }
                else
                {
                    // Trigger custom configured action from settings
                    _onClick?.Invoke();
                }
            }
        }

        private void ShelfPopup_Opened(object? sender, EventArgs e)
        {
            // Register global mouse event handler on MainWindow to catch clicks outside the popup
            if (System.Windows.Application.Current.MainWindow != null)
            {
                Mouse.AddPreviewMouseDownHandler(System.Windows.Application.Current.MainWindow, OnGlobalMouseDownOutside);
            }
        }

        private void ShelfPopup_Closed(object? sender, EventArgs e)
        {
            _isShelfOpen = false;
            _lastClosedTime = DateTime.Now;

            // De-register global click handlers
            if (System.Windows.Application.Current.MainWindow != null)
            {
                Mouse.RemovePreviewMouseDownHandler(System.Windows.Application.Current.MainWindow, OnGlobalMouseDownOutside);
            }
        }

        private void OnGlobalMouseDownOutside(object sender, MouseButtonEventArgs e)
        {
            if (!_isShelfOpen) return;

            // Check if click happened inside the popup content boundaries
            var child = ShelfPopup.Child as FrameworkElement;
            if (child != null)
            {
                var ptPopup = e.GetPosition(child);
                bool hitPopup = ptPopup.X >= 0 && ptPopup.X <= child.ActualWidth &&
                                ptPopup.Y >= 0 && ptPopup.Y <= child.ActualHeight;

                // Check if click happened inside the bubble window itself
                var ptBubble = e.GetPosition(BubbleBorder);
                bool hitBubble = ptBubble.X >= 0 && ptBubble.X <= BubbleBorder.ActualWidth &&
                                 ptBubble.Y >= 0 && ptBubble.Y <= BubbleBorder.ActualHeight;

                if (!hitPopup && !hitBubble)
                {
                    CloseShelf();
                }
            }
        }

        private void OnAppDeactivated(object? sender, EventArgs e)
        {
            // Automatically close the temporary shelf if the entire application loses focus/deactivates
            if (_isShelfOpen)
            {
                CloseShelf();
            }
        }

        private void PopupContentBorder_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Prevents mouse click inside popup from bubbling up and triggering close events
            e.Handled = false;
        }

        // ── Shelf UI Population ──────────────────────────────────────────

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
                UpdateBadge();
                int count = _vm.ShelfPaths.Count;
                
                // Clear selections for items that no longer exist on the shelf
                _vm.SelectedShelfPaths.RemoveWhere(path => !_vm.ShelfPaths.Contains(path));
                
                ShelfEmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
                ShelfItemsPanel.Children.Clear();

                // Safe iteration over cloned collection
                foreach (var path in _vm.ShelfPaths.ToList())
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

        // ── Shelf Drop Zone Event Handlers ───────────────────────────────

        private void Shelf_DragEnter(object sender, WpfDragEventArgs e)
        {
            if (HasDroppableContent(e))
            {
                e.Effects = WpfDragDropEffects.Copy;
                IconTxt.Text = "\uE896"; // Download Cloud Icon
                IconTxt.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "AccentPrimary");
                e.Handled = true;
            }
        }

        private void Shelf_DragOver(object sender, WpfDragEventArgs e)
        {
            if (HasDroppableContent(e))
            {
                e.Effects = WpfDragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void Shelf_DragLeave(object sender, WpfDragEventArgs e)
        {
            IconTxt.Text = "\uE8B7"; // Folder Icon
            IconTxt.ClearValue(System.Windows.Controls.TextBlock.ForegroundProperty);
        }

        private void Shelf_Drop(object sender, WpfDragEventArgs e)
        {
            IconTxt.Text = "\uE8B7";
            IconTxt.ClearValue(System.Windows.Controls.TextBlock.ForegroundProperty);
            
            try 
            { 
                AddDroppedItems(e); 
            }
            catch { }
            e.Handled = true;
            
            if (!ShelfPopup.IsOpen) 
            {
                ShelfPopup.IsOpen = true;
            }
        }

        // ── Drag & Drop Utilities ────────────────────────────────────────

        private static bool HasDroppableContent(WpfDragEventArgs e)
        {
            return e.Data.GetDataPresent(WpfDataFormats.FileDrop)
                || e.Data.GetDataPresent(WpfDataFormats.UnicodeText)
                || e.Data.GetDataPresent(WpfDataFormats.Text);
        }

        private void AddDroppedItems(WpfDragEventArgs e)
        {
            if (e.Data.GetDataPresent(WpfDataFormats.FileDrop))
            {
                var files = e.Data.GetData(WpfDataFormats.FileDrop) as string[];
                if (files != null)
                {
                    foreach (var file in files)
                    {
                        if (!string.IsNullOrEmpty(file) && !_vm.ShelfPaths.Contains(file))
                        {
                            _vm.ShelfPaths.Add(file);
                        }
                    }
                }
            }
            else
            {
                var text = (e.Data.GetData(WpfDataFormats.UnicodeText) ?? e.Data.GetData(WpfDataFormats.Text)) as string;
                if (!string.IsNullOrWhiteSpace(text) && (text.StartsWith("http") || text.StartsWith("www")))
                {
                    if (!_vm.ShelfPaths.Contains(text))
                    {
                        _vm.ShelfPaths.Add(text);
                    }
                }
            }
        }

        // ── Badge Navigation ─────────────────────────────────────────────

        private void BadgeBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void BadgeBorder_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            // ALWAYS open or close the temporary shelf directly when clicking the red badge dot
            if (_isShelfOpen) CloseShelf();
            else OpenShelf();
        }

    }
}
