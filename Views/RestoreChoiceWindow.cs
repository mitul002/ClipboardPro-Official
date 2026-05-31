using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using WpfApp = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfStackPanel = System.Windows.Controls.StackPanel;
using WpfGrid = System.Windows.Controls.Grid;
using WpfBorder = System.Windows.Controls.Border;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using ClipboardPro.Models;

namespace ClipboardPro.Views
{
    public class RestoreChoiceWindow : Window
    {
        public ImportMode SelectedMode { get; private set; } = ImportMode.Cancel;

        public RestoreChoiceWindow()
        {
            this.Title = "Restore Backup";
            this.Width = 460;
            this.SizeToContent = SizeToContent.Height;
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = System.Windows.Media.Brushes.Transparent;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.ResizeMode = ResizeMode.NoResize;
            this.ShowInTaskbar = false;
            this.Owner = WpfApp.Current.MainWindow;

            this.SetResourceReference(Window.OpacityProperty, "GlobalOpacity");

            var rootBorder = new WpfBorder { Padding = new Thickness(15) };
            var shadowBorder = new WpfBorder
            {
                Background = (System.Windows.Media.Brush)WpfApp.Current.Resources["BgDeep"],
                CornerRadius = new CornerRadius(12),
                BorderBrush = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#33888888"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(24),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 15,
                    ShadowDepth = 2,
                    Opacity = 0.2,
                    Color = System.Windows.Media.Colors.Black
                }
            };

            var mainGrid = new WpfGrid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header Row
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Content Row
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons Row

            // 1. Top Header Label (e.g., FACTORY RESET style)
            var topLabel = new WpfTextBlock
            {
                Text = "RESTORE DATA",
                Foreground = (System.Windows.Media.Brush)WpfApp.Current.Resources["AccentPrimary"],
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20)
            };
            WpfGrid.SetRow(topLabel, 0);
            mainGrid.Children.Add(topLabel);

            // Close Button (Top Right)
            var btnClose = new WpfButton
            {
                Content = "\uE711",
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 12,
                Style = (Style)WpfApp.Current.Resources["IconButtonStyle"],
                Width = 32,
                Height = 32,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Margin = new Thickness(0, -10, -10, 0)
            };
            btnClose.Click += (s, e) => { SelectedMode = ImportMode.Cancel; this.DialogResult = false; this.Close(); };
            WpfGrid.SetRow(btnClose, 0);
            mainGrid.Children.Add(btnClose);

            // 2. Middle Content
            var contentStack = new WpfStackPanel { Margin = new Thickness(0, 0, 0, 30) };
            
            var mainMsg = new WpfTextBlock
            {
                Text = "Choose your restoration strategy.",
                Foreground = (System.Windows.Media.Brush)WpfApp.Current.Resources["TextPrimary"],
                FontSize = 18,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };
            contentStack.Children.Add(mainMsg);

            var subMsg = new WpfTextBlock
            {
                Text = "Merge adds items without affecting settings. Replace overwrites everything with the backup data.",
                Foreground = (System.Windows.Media.Brush)WpfApp.Current.Resources["TextMuted"],
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };
            contentStack.Children.Add(subMsg);

            WpfGrid.SetRow(contentStack, 1);
            mainGrid.Children.Add(contentStack);

            // 3. Bottom Buttons (Prominent side by side)
            var btnGrid = new WpfGrid();
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var mergeBtn = new WpfButton
            {
                Content = "MERGE",
                Style = (Style)WpfApp.Current.Resources["PrimaryButton"],
                Height = 48,
                FontWeight = FontWeights.Bold
            };
            mergeBtn.Click += (s, e) => { SelectedMode = ImportMode.Merge; this.DialogResult = true; this.Close(); };
            WpfGrid.SetColumn(mergeBtn, 0);
            btnGrid.Children.Add(mergeBtn);

            var replaceBtn = new WpfButton
            {
                Content = "REPLACE",
                Style = (Style)WpfApp.Current.Resources["SecondaryButton"],
                Height = 48,
                FontWeight = FontWeights.Bold,
                Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#15FFFFFF") // Subtle light bg
            };
            replaceBtn.Click += (s, e) => { SelectedMode = ImportMode.Replace; this.DialogResult = true; this.Close(); };
            WpfGrid.SetColumn(replaceBtn, 2);
            btnGrid.Children.Add(replaceBtn);

            WpfGrid.SetRow(btnGrid, 2);
            mainGrid.Children.Add(btnGrid);

            shadowBorder.Child = mainGrid;
            rootBorder.Child = shadowBorder;
            this.Content = rootBorder;

            shadowBorder.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) this.DragMove(); };
        }
    }
}
