using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipboardPro.ViewModels;
using System.Windows.Input;
using Button = System.Windows.Controls.Button;
using DataObject = System.Windows.DataObject;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using FontFamily = System.Windows.Media.FontFamily;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Brushes = System.Windows.Media.Brushes;

namespace ClipboardPro.Helpers
{
    public static class UIHelper
    {
        public static UIElement BuildShelfCard(string path, MainViewModel vm, System.Collections.Generic.HashSet<string> selectedPaths, Action onSelectionChanged = null)
        {
            bool isWeb = path.StartsWith("http") || path.StartsWith("www");
            bool isDirectory = false;
            string name;
            try
            {
                isDirectory = !isWeb && Directory.Exists(path);
                name = isWeb ? "Web Image/Link" : System.IO.Path.GetFileName(path);
                if (string.IsNullOrEmpty(name)) name = path;
                if (isWeb && path.Length > 30) name = path.Substring(0, 27) + "...";
            }
            catch
            {
                // Path contains characters illegal for System.IO (e.g. shell virtual paths)
                name = path.Length > 40 ? path.Substring(0, 37) + "..." : path;
            }

            var card = new Border
            {
                CornerRadius    = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Margin          = new Thickness(0, 0, 0, 6),
                Padding         = new Thickness(10, 8, 10, 8),
                Cursor          = System.Windows.Input.Cursors.Hand,
                AllowDrop       = true,
                Tag             = path
            };
            card.SetResourceReference(Border.BackgroundProperty, "BgCard");
            card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            card.MouseEnter += (s, e) => card.SetResourceReference(Border.BackgroundProperty, "BgCardHover");
            card.MouseLeave += (s, e) => card.SetResourceReference(Border.BackgroundProperty, "BgCard");

            // Allow dragging back out (multi-select aware)
            card.MouseMove += (s, e) =>
            {
                if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                {
                    DataObject dragData = new DataObject();
                    if (selectedPaths != null && selectedPaths.Contains(path))
                    {
                        var files = selectedPaths.Where(p => !p.StartsWith("http") && !p.StartsWith("www")).ToArray();
                        var webs = selectedPaths.Where(p => p.StartsWith("http") || p.StartsWith("www")).ToArray();

                        if (files.Length > 0)
                            dragData.SetData(DataFormats.FileDrop, files);
                        if (webs.Length > 0)
                            dragData.SetData(DataFormats.Text, string.Join(Environment.NewLine, webs));
                    }
                    else
                    {
                        if (isWeb) dragData.SetData(DataFormats.Text, path);
                        else dragData.SetData(DataFormats.FileDrop, new[] { path });
                    }
                    
                    try { DragDrop.DoDragDrop(card, dragData, DragDropEffects.Copy); } catch { }
                }
            };

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 0: Checkbox
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 1: Icon
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 2: Info
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 3: Open
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 4: Copy
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 5: Remove

            // Checkbox for Multi-Select
            var chk = new System.Windows.Controls.CheckBox
            {
                IsChecked = selectedPaths != null && selectedPaths.Contains(path),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            chk.Checked += (s, e) =>
            {
                selectedPaths?.Add(path);
                onSelectionChanged?.Invoke();
            };
            chk.Unchecked += (s, e) =>
            {
                selectedPaths?.Remove(path);
                onSelectionChanged?.Invoke();
            };
            Grid.SetColumn(chk, 0);
            row.Children.Add(chk);

            // File icon / thumbnail
            var iconPanel = new Border
            {
                Width = 36, Height = 36,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true,
            };
            iconPanel.SetResourceReference(Border.BackgroundProperty, "BadgeBg");

            if (isWeb)
            {
                iconPanel.Child = MakeShelfIcon("\uE71B"); // Globe icon
            }
            else
            {
                // Try image thumbnail for image files
                try
                {
                    var ext = string.Empty;
                    bool isImage = false;
                    try
                    {
                        ext = System.IO.Path.GetExtension(path).ToLower();
                        isImage = ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
                    }
                    catch { }

                    if (isImage && File.Exists(path))
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource        = new Uri(path, UriKind.Absolute);
                        bmp.DecodePixelWidth = 36;
                        bmp.CacheOption      = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                        iconPanel.Child = new System.Windows.Controls.Image
                        {
                            Source  = bmp,
                            Stretch = Stretch.UniformToFill,
                        };
                    }
                    else
                    {
                        iconPanel.Child = MakeShelfIcon(isDirectory ? "\uE8B7" : "\uE8A5");
                    }
                }
                catch
                {
                    iconPanel.Child = MakeShelfIcon(isDirectory ? "\uE8B7" : "\uE8A5");
                }
            }

            Grid.SetColumn(iconPanel, 1);
            row.Children.Add(iconPanel);

            // File name + size/url
            var infoPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var nameTb = new TextBlock
            {
                Text         = name,
                FontSize     = 12,
                FontWeight   = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            nameTb.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
            infoPanel.Children.Add(nameTb);

            if (isWeb)
            {
                var urlTb = new TextBlock { Text = "Web Link", FontSize = 10, Opacity = 0.5 };
                urlTb.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
                infoPanel.Children.Add(urlTb);
            }
            else if (File.Exists(path))
            {
                try
                {
                    var size = new FileInfo(path).Length;
                    string sizeStr = size < 1024 ? $"{size} B"
                                   : size < 1024 * 1024 ? $"{size / 1024:0.#} KB"
                                   : $"{size / (1024 * 1024):0.#} MB";
                    var sizeTb = new TextBlock { Text = sizeStr, FontSize = 10, Opacity = 0.5 };
                    sizeTb.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
                    infoPanel.Children.Add(sizeTb);
                }
                catch { }
            }

            Grid.SetColumn(infoPanel, 2);
            row.Children.Add(infoPanel);

            // Open button
            var btnOpen = MakeShelfActionBtn(isWeb ? "\uE71B" : "\uE838", isWeb ? "Open Link" : "Open in Explorer");
            btnOpen.Click += (s, e) =>
            {
                try
                {
                    if (isWeb)
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                    else if (File.Exists(path))
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
                    else if (Directory.Exists(path))
                        System.Diagnostics.Process.Start("explorer.exe", $"\"{path}\"");
                }
                catch { }
            };
            Grid.SetColumn(btnOpen, 3);
            row.Children.Add(btnOpen);

            // Copy Link/Path button
            var btnCopy = MakeShelfActionBtn("\uE16F", isWeb ? "Copy Link" : "Copy Path");
            btnCopy.Click += (s, e) =>
            {
                try
                {
                    System.Windows.Clipboard.SetText(path);
                }
                catch { }
            };
            Grid.SetColumn(btnCopy, 4);
            row.Children.Add(btnCopy);

            // Remove button
            var btnRemove = MakeShelfActionBtn("\uE711", "Remove");
            btnRemove.SetResourceReference(Button.ForegroundProperty, "DangerBrush");
            btnRemove.Click += (s, e) =>
            {
                // Use BeginInvoke to defer removal so the current UI update chain completes first.
                System.Windows.Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(() => vm.ShelfPaths.Remove(path)));
            };
            Grid.SetColumn(btnRemove, 5);
            row.Children.Add(btnRemove);

            card.Child = row;
            return card;
        }

        public static TextBlock MakeShelfIcon(string glyph)
        {
            var tb = new TextBlock
            {
                Text              = glyph,
                FontFamily        = new FontFamily("Segoe MDL2 Assets"),
                FontSize          = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "AccentPrimary");
            return tb;
        }

        public static Button MakeShelfActionBtn(string glyph, string tip)
        {
            var btn = new Button
            {
                Width   = 26, Height = 26,
                Margin  = new Thickness(2, 0, 0, 0),
                ToolTip = tip,
                Cursor  = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0),
                Background      = Brushes.Transparent,
            };
            btn.SetResourceReference(Button.StyleProperty, "IconButtonStyle");
            var tb = new TextBlock
            {
                Text       = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize   = 11,
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
            btn.Content = tb;
            return btn;
        }
    }

    public class LassoSelectionHelper
    {
        private readonly Grid _selectionGrid;
        private readonly System.Windows.Shapes.Rectangle _selectionBox;
        private readonly System.Windows.Controls.Panel _itemsPanel;
        private readonly HashSet<string> _selectedPaths;
        private readonly MainViewModel _vm;
        
        private bool _isLassoing;
        private System.Windows.Point _lassoStartPoint;

        public LassoSelectionHelper(Grid selectionGrid, System.Windows.Shapes.Rectangle selectionBox, System.Windows.Controls.Panel itemsPanel, HashSet<string> selectedPaths, MainViewModel vm)
        {
            _selectionGrid = selectionGrid;
            _selectionBox = selectionBox;
            _itemsPanel = itemsPanel;
            _selectedPaths = selectedPaths;
            _vm = vm;

            _selectionGrid.PreviewMouseLeftButtonDown += Grid_PreviewMouseLeftButtonDown;
            _selectionGrid.PreviewMouseMove += Grid_PreviewMouseMove;
            _selectionGrid.PreviewMouseLeftButtonUp += Grid_PreviewMouseLeftButtonUp;
        }

        private void Grid_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject src)
            {
                var parentCard = FindParent<Border>(src);
                if (parentCard != null && parentCard.Tag is string) return;
                var scrollBar = FindParent<System.Windows.Controls.Primitives.ScrollBar>(src);
                if (scrollBar != null) return;
            }

            _isLassoing = true;
            _lassoStartPoint = e.GetPosition(_selectionGrid);
            _selectionBox.Width = 0;
            _selectionBox.Height = 0;
            Canvas.SetLeft(_selectionBox, _lassoStartPoint.X);
            Canvas.SetTop(_selectionBox, _lassoStartPoint.Y);
            _selectionBox.Visibility = Visibility.Visible;
            _selectionGrid.CaptureMouse();
            e.Handled = true;

            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                _selectedPaths.Clear();
                foreach (var child in _itemsPanel.Children)
                {
                    if (child is Border card && card.Child is Grid grid && grid.Children.Count > 0 && grid.Children[0] is System.Windows.Controls.CheckBox chk)
                    {
                        chk.IsChecked = false;
                    }
                }
            }
        }

        private void Grid_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isLassoing && _selectionGrid.IsMouseCaptured)
            {
                var pos = e.GetPosition(_selectionGrid);
                var x = Math.Min(pos.X, _lassoStartPoint.X);
                var y = Math.Min(pos.Y, _lassoStartPoint.Y);
                var w = Math.Abs(pos.X - _lassoStartPoint.X);
                var h = Math.Abs(pos.Y - _lassoStartPoint.Y);

                Canvas.SetLeft(_selectionBox, x);
                Canvas.SetTop(_selectionBox, y);
                _selectionBox.Width = w;
                _selectionBox.Height = h;

                UpdateLassoSelection(new Rect(x, y, w, h));
            }
        }

        private void Grid_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isLassoing)
            {
                _isLassoing = false;
                _selectionBox.Visibility = Visibility.Collapsed;
                _selectionGrid.ReleaseMouseCapture();
                _vm.NotifyShelfSelectionChanged();
            }
        }

        private void UpdateLassoSelection(Rect lassoRect)
        {
            foreach (var child in _itemsPanel.Children)
            {
                if (child is Border card && card.Tag is string path)
                {
                    var bounds = card.TransformToAncestor(_selectionGrid).TransformBounds(new Rect(0, 0, card.ActualWidth, card.ActualHeight));
                    bool intersects = lassoRect.IntersectsWith(bounds);
                    
                    if (intersects)
                    {
                        if (_selectedPaths.Add(path))
                        {
                            if (card.Child is Grid grid && grid.Children.Count > 0 && grid.Children[0] is System.Windows.Controls.CheckBox chk)
                                chk.IsChecked = true;
                        }
                    }
                    else if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
                    {
                        if (_selectedPaths.Remove(path))
                        {
                            if (card.Child is Grid grid && grid.Children.Count > 0 && grid.Children[0] is System.Windows.Controls.CheckBox chk)
                                chk.IsChecked = false;
                        }
                    }
                }
            }
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }
    }
}
