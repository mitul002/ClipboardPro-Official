using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using WpfApp = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfStackPanel = System.Windows.Controls.StackPanel;
using WpfGrid = System.Windows.Controls.Grid;
using WpfBorder = System.Windows.Controls.Border;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColors = System.Windows.Media.Colors;

namespace ClipboardPro.Views
{
    public class InputDialog : System.Windows.Window
    {
        public string Result { get; private set; } = string.Empty;
        public string SelectedColor { get; private set; } = "#3498db";
        public string SelectedIcon { get; private set; } = "\uE8EC";

        private readonly WpfTextBox _input;

        public InputDialog(string title, string prompt, string defaultName = "", string defaultColor = "#3498db", string defaultIcon = "\uE8EC", string actionText = "CREATE")
        {
            this.Title = title;
            this.Width = 400;
            this.SizeToContent = SizeToContent.Height;
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = WpfBrushes.Transparent;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.ResizeMode = ResizeMode.NoResize;
            this.ShowInTaskbar = false;
            this.Owner = WpfApp.Current.MainWindow;

            this.SelectedColor = defaultColor;
            this.SelectedIcon = defaultIcon;

            // Bind Opacity
            this.SetResourceReference(Window.OpacityProperty, "GlobalOpacity");

            // Shadow Wrapper
            var rootBorder = new WpfBorder { Padding = new Thickness(15) };

            var shadowBorder = new WpfBorder
            {
                Background = (WpfBrush)WpfApp.Current.Resources["BgSidebar"],
                CornerRadius = new CornerRadius(16),
                BorderBrush = (WpfBrush)new BrushConverter().ConvertFromString("#44888888"),
                BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 15,
                    ShadowDepth = 2,
                    Opacity = 0.15,
                    Color = WpfColors.Black
                }
            };

            var mainStack = new WpfStackPanel { Margin = new Thickness(24) };

            // Prompt
            mainStack.Children.Add(new WpfTextBlock
            {
                Text = prompt,
                Foreground = (WpfBrush)WpfApp.Current.Resources["TextPrimary"],
                FontSize = 16,
                Margin = new Thickness(0, 8, 0, 20),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            });

            // Compact Input Row
            var inputWrapper = new WpfBorder
            {
                Background = (WpfBrush)WpfApp.Current.Resources["BgCard"],
                CornerRadius = new CornerRadius(12),
                BorderBrush = (WpfBrush)WpfApp.Current.Resources["BorderBrush"],
                BorderThickness = new Thickness(1),
                Height = 48
            };

            var inputGrid = new WpfGrid();
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Icon Picker with Slim Scrollbar
            var options = new[] 
            {
                new { Icon = "\uE8EC", Color = "#3498db" }, // Tag
                new { Icon = "\uE8B7", Color = "#f1c40f" }, // Folder
                new { Icon = "\uE700", Color = "#95a5a6" }, // Menu
                new { Icon = "\uEB51", Color = "#e74c3c" }, // Heart
                new { Icon = "\uE734", Color = "#f39c12" }, // Star
                new { Icon = "\uE753", Color = "#3498db" }, // Cloud
                new { Icon = "\uE943", Color = "#2ecc71" }, // Code
                new { Icon = "\uE719", Color = "#16a085" }, // Globe
                new { Icon = "\uE715", Color = "#9b59b6" }, // Email
                new { Icon = "\uE71B", Color = "#1abc9c" }, // Link
                new { Icon = "\uE77B", Color = "#e67e22" }, // Person
                new { Icon = "\uE712", Color = "#7f8c8d" }, // Cog
                new { Icon = "\uE8A5", Color = "#2c3e50" }, // Message
                new { Icon = "\uE179", Color = "#2980b9" }, // List
                new { Icon = "\uE8A7", Color = "#8e44ad" }, // Tiles
                new { Icon = "\uE80F", Color = "#e74c3c" }, // Home
                new { Icon = "\uE710", Color = "#2ecc71" }, // Plus
                new { Icon = "\uE717", Color = "#3498db" }, // Phone
                new { Icon = "\uE71A", Color = "#f1c40f" }, // Map
                new { Icon = "\uE720", Color = "#e67e22" }, // Save
                new { Icon = "\uE724", Color = "#9b59b6" }, // Lock
                new { Icon = "\uE735", Color = "#f1c40f" }, // Shopping
                new { Icon = "\uE74E", Color = "#34495e" }, // Work
                new { Icon = "\uE7B5", Color = "#e74c3c" }, // Camera
                new { Icon = "\uE8D6", Color = "#2ecc71" }, // Music
                new { Icon = "\uE902", Color = "#3498db" }, // Game
                new { Icon = "\uE945", Color = "#f1c40f" }, // Lightbulb
                new { Icon = "\uEB4E", Color = "#e67e22" }, // Rocket
                new { Icon = "\uEC05", Color = "#9b59b6" }, // Trophy
                new { Icon = "\uED54", Color = "#1abc9c" }, // Diamond
                new { Icon = "\uE7C4", Color = "#3498db" }, // Task View (Win 10)
                new { Icon = "\uE8A7", Color = "#2ecc71" }  // Task View (Win 11)
            };

            var iconCombo = new WpfComboBox
            {
                Style = (Style)WpfApp.Current.Resources["DarkComboBox"],
                BorderThickness = new Thickness(0),
                Background = WpfBrushes.Transparent,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                Padding = new Thickness(12, 0, 0, 0)
            };
            
            // Apply Slim Scrollbar to ComboBox Dropdown
            iconCombo.Resources.Add(typeof(ScrollViewer), WpfApp.Current.Resources["SlimScrollViewer"]);

            int defaultIndex = 0;
            for (int i = 0; i < options.Length; i++)
            {
                var opt = options[i];
                var iconText = new WpfTextBlock
                {
                    Text = opt.Icon,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                    FontSize = 18,
                    Foreground = (WpfBrush)new BrushConverter().ConvertFromString(opt.Color),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };
                iconCombo.Items.Add(new ComboBoxItem { Content = iconText, Tag = opt, Padding = new Thickness(8) });
                
                if (opt.Icon == defaultIcon && opt.Color.Equals(defaultColor, StringComparison.OrdinalIgnoreCase))
                {
                    defaultIndex = i;
                }
            }
            iconCombo.SelectedIndex = defaultIndex;
            iconCombo.SelectionChanged += (s, e) => {
                if (iconCombo.SelectedItem is ComboBoxItem selected && selected.Tag != null)
                {
                    dynamic opt = selected.Tag;
                    SelectedIcon = opt.Icon;
                    SelectedColor = opt.Color;
                }
            };

            WpfGrid.SetColumn(iconCombo, 0);
            inputGrid.Children.Add(iconCombo);

            // TextBox
            _input = new WpfTextBox
            {
                Background = WpfBrushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = (WpfBrush)WpfApp.Current.Resources["TextPrimary"],
                VerticalContentAlignment = System.Windows.VerticalAlignment.Center,
                Padding = new Thickness(8, 0, 16, 0),
                FontSize = 15,
                Tag = "Category name...",
                Text = defaultName
            };
            WpfGrid.SetColumn(_input, 1);
            inputGrid.Children.Add(_input);

            inputWrapper.Child = inputGrid;
            mainStack.Children.Add(inputWrapper);

            // Action Buttons
            var btnRow = new WpfStackPanel { 
                Orientation = WpfOrientation.Horizontal, 
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center, 
                Margin = new Thickness(0, 24, 0, 0) 
            };
            
            var btnCancel = new WpfButton { 
                Content = "CANCEL", 
                Style = (Style)WpfApp.Current.Resources["SecondaryButton"],
                Width = 110, 
                Height = 44, 
                Margin = new Thickness(0, 0, 12, 0)
            };
            btnCancel.Click += (s, e) => { this.DialogResult = false; this.Close(); };
            
            var btnCreate = new WpfButton { 
                Content = actionText, 
                Style = (Style)WpfApp.Current.Resources["PrimaryButton"], 
                Width = 120, 
                Height = 44 
            };
            btnCreate.Click += (s, e) => { 
                if (!string.IsNullOrWhiteSpace(_input.Text)) {
                    this.Result = _input.Text; 
                    this.DialogResult = true; 
                    this.Close(); 
                }
            };
            
            btnRow.Children.Add(btnCancel);
            btnRow.Children.Add(btnCreate);
            mainStack.Children.Add(btnRow);

            shadowBorder.Child = mainStack;
            rootBorder.Child = shadowBorder;
            this.Content = rootBorder;

            _input.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) btnCreate.RaiseEvent(new RoutedEventArgs(WpfButton.ClickEvent)); };
            shadowBorder.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) this.DragMove(); };
            this.Loaded += (s, e) => {
                _input.Focus();
                if (!string.IsNullOrEmpty(_input.Text))
                {
                    _input.SelectAll();
                }
            };
        }
    }
}
