using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipboardPro.Models;
using ClipboardPro.ViewModels;
using System.Linq;

namespace ClipboardPro.Views
{
    public partial class EditClipWindow : Window
    {
        private readonly ClipboardItem _item;
        public bool Deleted { get; private set; }

        private System.Windows.Point _startPoint;
        private System.Windows.Shapes.Shape? _currentShape;
        private bool _isDrawingShapes = false;
        private System.Windows.Ink.DrawingAttributes? DefaultAttributes => DrawCanvas?.DefaultDrawingAttributes;

        private readonly Stack<object> _undoStack = new();

        private double _zoomScale = 1.0;
        public double ZoomScale
        {
            get => _zoomScale;
            set
            {
                _zoomScale = Math.Clamp(value, 0.1, 5.0);
                ApplyZoom();
            }
        }

        public EditClipWindow(ClipboardItem item, bool isReadOnly = false)
        {
            InitializeComponent();
            _item = item;

            // Set Title based on mode
            TxtTitle.Text = isReadOnly ? "View" : "Edit";
            if (isReadOnly)
            {
                BtnSave.Visibility = Visibility.Collapsed;
                ImageTools.Visibility = Visibility.Collapsed;
                TxtContent.IsReadOnly = true;
                DrawCanvas.EditingMode = System.Windows.Controls.InkCanvasEditingMode.None;
                DrawCanvas.IsHitTestVisible = false;
                ShapeCanvas.IsHitTestVisible = false;
                DrawingGrid.Cursor = System.Windows.Input.Cursors.Arrow;
                BtnSwitchToEdit.Visibility = Visibility.Visible;
            }

            ChkPinned.IsChecked = item.IsPinned;
            ChkFavorite.IsChecked = item.IsFavorite;

            ChkPinned.Click += (s, e) => {
                _item.IsPinned = ChkPinned.IsChecked ?? false;
            };
            ChkFavorite.Click += (s, e) => {
                _item.IsFavorite = ChkFavorite.IsChecked ?? false;
            };

            // Constrain window size to screen work area
            var workArea = SystemParameters.WorkArea;
            this.MaxWidth = workArea.Width * 0.9;
            this.MaxHeight = workArea.Height * 0.9;

            if (item.Type == ClipboardItemType.Image)
            {
                TextContainer.Visibility = Visibility.Collapsed;
                ImageContainer.Visibility = Visibility.Visible;
                if (!isReadOnly) ImageTools.Visibility = Visibility.Visible;
                
                if (!string.IsNullOrEmpty(item.ImagePath))
                {
                    try
                    {
                        // Resolve relative path if needed
                        string fullPath = item.ImagePath;
                        if (!System.IO.Path.IsPathRooted(fullPath))
                        {
                            var appData = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipboardPro", "Images");
                            fullPath = System.IO.Path.Combine(appData, fullPath);
                        }

                        if (System.IO.File.Exists(fullPath))
                        {
                            var bmi = new BitmapImage();
                            bmi.BeginInit();
                            bmi.UriSource = new Uri(fullPath, UriKind.Absolute);
                            bmi.CacheOption = BitmapCacheOption.OnLoad;
                            bmi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                            bmi.EndInit();
                            ImgDisplay.Source = bmi;

                            DrawingGrid.Width = bmi.PixelWidth;
                            DrawingGrid.Height = bmi.PixelHeight;
                            DrawCanvas.Width = bmi.PixelWidth;
                            DrawCanvas.Height = bmi.PixelHeight;
                            ShapeCanvas.Width = bmi.PixelWidth;
                            ShapeCanvas.Height = bmi.PixelHeight;

                            // Set window size based on image, but cap it to screen area
                            this.Width = Math.Min(this.MaxWidth, Math.Max(600, bmi.PixelWidth + 60));
                            this.Height = Math.Min(this.MaxHeight, Math.Max(600, bmi.PixelHeight + 160));
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading image: {ex.Message}");
                    }
                }
            }
            else
            {
                TxtContent.Text = item.Content;
                // For text, set a reasonable default size
                this.Width = Math.Min(this.MaxWidth, 700);
                this.Height = Math.Min(this.MaxHeight, 550);
            }
            
            this.MouseDown += (s, e) => { 
                if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && 
                    e.OriginalSource is not System.Windows.Controls.TextBox && 
                    e.OriginalSource is not System.Windows.Controls.InkCanvas && 
                    e.OriginalSource is not System.Windows.Controls.Canvas &&
                    e.OriginalSource is not System.Windows.Controls.Image &&
                    e.OriginalSource is not System.Windows.Controls.Grid) 
                {
                    DragMove(); 
                }
            };

            ToolPencil.Checked += (s, e) => UpdateToolStates();
            ToolEraser.Checked += (s, e) => UpdateToolStates();
            ToolLine.Checked   += (s, e) => UpdateToolStates();
            ToolBox.Checked    += (s, e) => UpdateToolStates();
            ToolCircle.Checked += (s, e) => UpdateToolStates();
            ToolArrow.Checked  += (s, e) => UpdateToolStates();

            this.Loaded += (s, e) =>
            {
                if (FindResource("AccentPrimary") is SolidColorBrush brush && DefaultAttributes != null) DefaultAttributes.Color = brush.Color;
                if (item.Type != ClipboardItemType.Image) { TxtContent.Focus(); TxtContent.Select(TxtContent.Text.Length, 0); }
                if (!isReadOnly) UpdateToolStates();
            };

            DrawCanvas.StrokeCollected += (s, e) => _undoStack.Push(e.Stroke);

            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Z && System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
                {
                    PerformUndo(); e.Handled = true;
                }
            };
        }

        private void UpdateToolStates()
        {
            if (DrawCanvas == null || ShapeCanvas == null) return;
            
            bool isEraser = ToolEraser.IsChecked == true;
            bool isInk = ToolPencil.IsChecked == true || isEraser;
            
            DrawCanvas.IsHitTestVisible = isInk;
            DrawCanvas.EditingMode = isEraser ? System.Windows.Controls.InkCanvasEditingMode.EraseByStroke : System.Windows.Controls.InkCanvasEditingMode.Ink;
            
            // Allow clicking shapes only when eraser is active
            ShapeCanvas.IsHitTestVisible = isEraser;
        }

        private void DrawingGrid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
            
            if (ToolEraser.IsChecked == true)
            {
                // Use VisualTreeHelper to find if we clicked a shape on the ShapeCanvas
                var hitResult = VisualTreeHelper.HitTest(ShapeCanvas, e.GetPosition(ShapeCanvas));
                if (hitResult?.VisualHit is System.Windows.Shapes.Shape shape)
                {
                    ShapeCanvas.Children.Remove(shape);
                    return;
                }
                return;
            }

            if (ToolPencil.IsChecked == true || DefaultAttributes == null) return;
            
            var pos = e.GetPosition(DrawingGrid);
            if (pos.X < 0 || pos.Y < 0 || pos.X > DrawingGrid.Width || pos.Y > DrawingGrid.Height) return;

            _startPoint = pos;
            _isDrawingShapes = true;
            
            var brush = new SolidColorBrush(DefaultAttributes.Color);
            var thickness = DefaultAttributes.Width;

            if (ToolLine.IsChecked == true)
            {
                _currentShape = new System.Windows.Shapes.Line { Stroke = brush, StrokeThickness = thickness, X1 = _startPoint.X, Y1 = _startPoint.Y, X2 = _startPoint.X, Y2 = _startPoint.Y, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            }
            else if (ToolBox.IsChecked == true)
            {
                _currentShape = new System.Windows.Shapes.Rectangle { Stroke = brush, StrokeThickness = thickness };
                System.Windows.Controls.Canvas.SetLeft(_currentShape, _startPoint.X);
                System.Windows.Controls.Canvas.SetTop(_currentShape, _startPoint.Y);
            }
            else if (ToolCircle.IsChecked == true)
            {
                _currentShape = new System.Windows.Shapes.Ellipse { Stroke = brush, StrokeThickness = thickness };
                System.Windows.Controls.Canvas.SetLeft(_currentShape, _startPoint.X);
                System.Windows.Controls.Canvas.SetTop(_currentShape, _startPoint.Y);
            }
            else if (ToolArrow.IsChecked == true)
            {
                _currentShape = CreateArrowShape(brush, thickness);
            }

            if (_currentShape != null)
            {
                ShapeCanvas.Children.Add(_currentShape);
                DrawingGrid.CaptureMouse();
                e.Handled = true;
            }
        }

        private System.Windows.Shapes.Path CreateArrowShape(System.Windows.Media.Brush brush, double thickness)
        {
            var path = new System.Windows.Shapes.Path { Stroke = brush, StrokeThickness = thickness, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            var group = new GeometryGroup();
            group.Children.Add(new LineGeometry(new System.Windows.Point(0, 0), new System.Windows.Point(1, 1))); 
            path.Data = group;
            return path;
        }

        private void UpdateArrow(System.Windows.Shapes.Path path, System.Windows.Point start, System.Windows.Point end)
        {
            var group = (GeometryGroup)path.Data;
            group.Children.Clear();
            group.Children.Add(new LineGeometry(start, end));

            double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
            double headLen = 15 + DefaultAttributes!.Width * 2;
            double headAngle = Math.PI / 6; 

            System.Windows.Point p1 = new System.Windows.Point(end.X - headLen * Math.Cos(angle - headAngle), end.Y - headLen * Math.Sin(angle - headAngle));
            System.Windows.Point p2 = new System.Windows.Point(end.X - headLen * Math.Cos(angle + headAngle), end.Y - headLen * Math.Sin(angle + headAngle));

            group.Children.Add(new LineGeometry(end, p1));
            group.Children.Add(new LineGeometry(end, p2));
        }

        private void DrawingGrid_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isDrawingShapes || _currentShape == null) return;
            var pos = e.GetPosition(DrawingGrid);
            
            if (_currentShape is System.Windows.Shapes.Line line) { line.X2 = pos.X; line.Y2 = pos.Y; }
            else if (_currentShape is System.Windows.Shapes.Path arrowPath) { UpdateArrow(arrowPath, _startPoint, pos); }
            else
            {
                var x = Math.Min(_startPoint.X, pos.X);
                var y = Math.Min(_startPoint.Y, pos.Y);
                var w = Math.Abs(_startPoint.X - pos.X);
                var h = Math.Abs(_startPoint.Y - pos.Y);
                _currentShape.Width = Math.Max(0.1, w);
                _currentShape.Height = Math.Max(0.1, h);
                System.Windows.Controls.Canvas.SetLeft(_currentShape, x);
                System.Windows.Controls.Canvas.SetTop(_currentShape, y);
            }
            e.Handled = true;
        }

        private void DrawingGrid_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isDrawingShapes && _currentShape != null)
            {
                bool hasSize = false;
                if (_currentShape is System.Windows.Shapes.Line l) hasSize = Math.Abs(l.X1 - l.X2) > 2 || Math.Abs(l.Y1 - l.Y2) > 2;
                else if (_currentShape is System.Windows.Shapes.Path) hasSize = true; 
                else hasSize = _currentShape.Width > 2 && _currentShape.Height > 2;

                if (hasSize) _undoStack.Push(_currentShape);
                else ShapeCanvas.Children.Remove(_currentShape);

                _currentShape = null;
                _isDrawingShapes = false;
                DrawingGrid.ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void CmbThickness_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CmbThickness == null || DefaultAttributes == null) return;
            if (CmbThickness.SelectedItem is System.Windows.Controls.ComboBoxItem item && double.TryParse(item.Tag?.ToString(), out double t))
            {
                DefaultAttributes.Width = t; DefaultAttributes.Height = t;
            }
        }

        private void BtnUndo_Click(object sender, RoutedEventArgs e) => PerformUndo();

        private void PerformUndo()
        {
            if (_undoStack.Count == 0) return;
            var action = _undoStack.Pop();
            if (action is System.Windows.Ink.Stroke stroke) DrawCanvas.Strokes.Remove(stroke);
            else if (action is UIElement element) ShapeCanvas.Children.Remove(element);
        }

        private void BtnMoreColors_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new System.Windows.Forms.ColorDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK && DefaultAttributes != null)
            {
                var color = System.Windows.Media.Color.FromArgb(dlg.Color.A, dlg.Color.R, dlg.Color.G, dlg.Color.B);
                DefaultAttributes.Color = color;
            }
        }

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.RadioButton rb && rb.Tag is string colorHex && DefaultAttributes != null)
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
                DefaultAttributes.Color = color;
            }
        }

        private void BtnClearDraw_Click(object sender, RoutedEventArgs e)
        {
            DrawCanvas.Strokes.Clear(); ShapeCanvas.Children.Clear(); _undoStack.Clear();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.Handled) return;
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as System.Windows.Controls.Button;
            if (_item.Type == ClipboardItemType.Image) SaveEditedImage();
            else _item.Content = TxtContent.Text;
            
            // Use property setters to ensure PropertyChanged fires correctly
            _item.IsPinned   = ChkPinned.IsChecked ?? false;
            _item.IsFavorite = ChkFavorite.IsChecked ?? false;
            
            if (btn != null) 
            { 
                ShowFeedback(btn, "Saved!", (System.Windows.Media.Brush)FindResource("SuccessBrush")); 
                await System.Threading.Tasks.Task.Delay(600); 
            }

            // Safety check: only set DialogResult if window is still open and was shown as a dialog
            try 
            { 
                if (this.IsLoaded) DialogResult = true; 
            } 
            catch { /* Not shown as dialog or already closing */ }
            
            this.Close();
        }

        private void BtnSwitchToEdit_Click(object sender, RoutedEventArgs e)
        {
            // Switch from View Mode to Edit Mode
            TxtTitle.Text = "Edit";
            BtnSave.Visibility = Visibility.Visible;
            if (_item.Type == ClipboardItemType.Image) ImageTools.Visibility = Visibility.Visible;
            TxtContent.IsReadOnly = false;
            DrawCanvas.EditingMode = System.Windows.Controls.InkCanvasEditingMode.Ink;
            DrawCanvas.IsHitTestVisible = true;
            ShapeCanvas.IsHitTestVisible = true;
            BtnSwitchToEdit.Visibility = Visibility.Collapsed;
            DrawingGrid.Cursor = System.Windows.Input.Cursors.Cross;
            UpdateToolStates();
        }

        private async void ShowFeedback(System.Windows.Controls.Button btn, string message, System.Windows.Media.Brush feedbackBrush)
        {
            var oldTooltip = btn.ToolTip; var oldForeground = btn.Foreground;
            btn.ToolTip = message; btn.Foreground = feedbackBrush;
            await System.Threading.Tasks.Task.Delay(1500);
            btn.ToolTip = oldTooltip; btn.Foreground = oldForeground;
        }

        private void SaveEditedImage()
        {
            try
            {
                // 1. Force 1:1 scale for rendering to capture original resolution
                double currentZoom = ZoomScale;
                ZoomScale = 1.0;
                DrawingGrid.UpdateLayout();

                // Use the explicit Width/Height we set in the constructor (original resolution)
                // instead of ActualWidth/ActualHeight which can be unreliable during layout changes
                var width = (int)DrawingGrid.Width;
                var height = (int)DrawingGrid.Height;
                
                if (width <= 0 || height <= 0) 
                {
                    ZoomScale = currentZoom;
                    return;
                }

                var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                
                // Explicitly render each layer to ensure correct compositing
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    // 1. Draw original image
                    if (ImgDisplay.Source != null)
                    {
                        dc.DrawImage(ImgDisplay.Source, new Rect(0, 0, width, height));
                    }
                }
                rtb.Render(dv);
                
                // 2. Draw Ink (Pencil/Eraser)
                rtb.Render(DrawCanvas);
                
                // 3. Draw Shapes
                rtb.Render(ShapeCanvas);
                
                // Restore user's zoom
                ZoomScale = currentZoom;

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                
                // Create a NEW item for the edited version so original is preserved
                var newItem = new ClipboardItem 
                { 
                    Id = Guid.NewGuid().ToString(),
                    Type = ClipboardItemType.Image,
                    Timestamp = DateTime.Now,
                    Content = "Edited Image",
                    IsPinned = _item.IsPinned,
                    IsFavorite = _item.IsFavorite,
                    Category = _item.Category
                };

                // Save to a NEW file path
                string newFileName = $"img_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
                string appData = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipboardPro", "Images");
                if (!System.IO.Directory.Exists(appData)) System.IO.Directory.CreateDirectory(appData);
                string fullPath = System.IO.Path.Combine(appData, newFileName);

                using (var fs = new System.IO.FileStream(fullPath, System.IO.FileMode.Create))
                {
                    encoder.Save(fs);
                }
                newItem.ImagePath = newFileName;

                // Compute new hash
                try
                {
                    using (var ms = new System.IO.MemoryStream())
                    {
                        encoder.Save(ms);
                        ms.Seek(0, System.IO.SeekOrigin.Begin);
                        using (var img = System.Drawing.Image.FromStream(ms))
                        {
                            var monitor = ((App)System.Windows.Application.Current).GetMonitor();
                            if (monitor != null) newItem.ImageHash = monitor.ComputeImageHash(img);
                        }
                    }
                }
                catch { }

                // Add to ViewModel as a fresh entry
                var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
                if (mainWindow?.DataContext is MainViewModel vm)
                {
                    vm.AddItem(newItem);
                }

                // Close the editor since we've added a new item
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex) { System.Windows.MessageBox.Show("Error saving image: " + ex.Message); }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (WpfMsgBox.Show("Delete this item permanently?", "Confirm Delete", WpfMsgBoxBtn.YesNo, WpfMsgBoxImg.Warning) == WpfMsgBoxResult.Yes)
            {
                Deleted = true;
                try { if (this.IsLoaded) DialogResult = true; } catch { }
                this.Close();
            }
        }

        private void ApplyZoom()
        {
            if (ImageScale == null || TxtZoom == null) return;
            ImageScale.ScaleX = ZoomScale;
            ImageScale.ScaleY = ZoomScale;
            TxtZoom.Text = $"{(int)(ZoomScale * 100)}%";
        }

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e) => ZoomScale += 0.2;
        private void BtnZoomOut_Click(object sender, RoutedEventArgs e) => ZoomScale -= 0.2;
        private void BtnZoomReset_Click(object sender, RoutedEventArgs e) => ZoomScale = 1.0;

        private void ImageScroller_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                if (e.Delta > 0) ZoomScale += 0.1;
                else ZoomScale -= 0.1;
                e.Handled = true;
            }
            else if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Shift)
            {
                // Shift + Wheel = Horizontal Scroll
                if (e.Delta > 0) ImageScroller.LineLeft();
                else ImageScroller.LineRight();
                e.Handled = true;
            }
        }

        private void DrawingGrid_ManipulationDelta(object sender, System.Windows.Input.ManipulationDeltaEventArgs e)
        {
            // Zoom (Pinch)
            if (e.DeltaManipulation.Scale.X != 1.0 || e.DeltaManipulation.Scale.Y != 1.0)
            {
                double scaleFactor = (e.DeltaManipulation.Scale.X + e.DeltaManipulation.Scale.Y) / 2.0;
                ZoomScale *= scaleFactor;
            }

            // Pan (Translation)
            if (e.DeltaManipulation.Translation.X != 0 || e.DeltaManipulation.Translation.Y != 0)
            {
                ImageScroller.ScrollToHorizontalOffset(ImageScroller.HorizontalOffset - e.DeltaManipulation.Translation.X);
                ImageScroller.ScrollToVerticalOffset(ImageScroller.VerticalOffset - e.DeltaManipulation.Translation.Y);
            }
            
            e.Handled = true;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            source?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_MOUSEHWHEEL = 0x020E;
            if (msg == WM_MOUSEHWHEEL)
            {
                // Use unchecked to prevent overflow exceptions on 64-bit systems
                // The delta is stored in the high-order word of wParam
                int delta = unchecked((short)((long)wParam >> 16));
                if (delta > 0) ImageScroller.LineRight();
                else ImageScroller.LineLeft();
                handled = true;
            }
            return IntPtr.Zero;
        }
    }
}
