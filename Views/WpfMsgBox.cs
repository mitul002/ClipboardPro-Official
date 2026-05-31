using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfApplication = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;

namespace ClipboardPro.Views
{
    public enum WpfMsgBoxBtn { OK, YesNo }
    public enum WpfMsgBoxImg { Information, Warning, Question, Error }
    public enum WpfMsgBoxResult { OK, Yes, No, Cancel }

    public static class WpfMsgBox
    {
        public static WpfMsgBoxResult Show(string message, string title = "ClipboardPro", 
            WpfMsgBoxBtn buttons = WpfMsgBoxBtn.OK, WpfMsgBoxImg image = WpfMsgBoxImg.Information)
        {
            var win = new Window
            {
                Title = title,
                Width = 420,
                MaxWidth = 420,
                MaxHeight = 550,
                SizeToContent = SizeToContent.Height,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = WpfBrushes.Transparent,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = WpfApplication.Current.MainWindow,
                ShowInTaskbar = false
            };
            win.SetResourceReference(Window.OpacityProperty, "GlobalOpacity");

            // Container to hold the shadow margin
            var rootBorder = new Border { Padding = new Thickness(15) };

            var shadowBorder = new Border
            {
                Background = (WpfBrush)WpfApplication.Current.Resources["BgSidebar"],
                CornerRadius = new CornerRadius(16),
                BorderBrush = (WpfBrush)new BrushConverter().ConvertFromString("#44888888"),
                BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 15,
                    ShadowDepth = 2,
                    Opacity = 0.15,
                    Color = System.Windows.Media.Colors.Black
                }
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var header = new Border { Padding = new Thickness(24, 20, 24, 0) };
            var titleText = new TextBlock
            {
                Text = title.ToUpper(),
                Foreground = (WpfBrush)WpfApplication.Current.Resources["AccentPrimary"],
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Opacity = 0.8
            };
            header.Child = titleText;
            Grid.SetRow(header, 0);
            mainGrid.Children.Add(header);

            // Content
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(24, 16, 24, 20)
            };
            
            var msgText = new TextBlock
            {
                Text = message,
                Foreground = (WpfBrush)WpfApplication.Current.Resources["TextPrimary"],
                FontSize = 15,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                LineHeight = 22
            };
            scroll.Content = msgText;
            Grid.SetRow(scroll, 1);
            mainGrid.Children.Add(scroll);

            // Buttons Area
            var btnArea = new Border
            {
                Padding = new Thickness(24, 0, 24, 24),
                HorizontalAlignment = WpfHorizontalAlignment.Center
            };
            var btnRow = new StackPanel { Orientation = WpfOrientation.Horizontal };
            
            WpfMsgBoxResult result = WpfMsgBoxResult.Cancel;

            if (buttons == WpfMsgBoxBtn.YesNo)
            {
                var btnYes = new WpfButton 
                { 
                    Content = "YES", 
                    Style = (Style)WpfApplication.Current.Resources["PrimaryButton"], 
                    Width = 110, 
                    Height = 44,
                    Margin = new Thickness(0,0,12,0)
                };
                btnYes.Click += (s, e) => { result = WpfMsgBoxResult.Yes; win.DialogResult = true; win.Close(); };
                
                var btnNo = new WpfButton 
                { 
                    Content = "NO", 
                    Style = (Style)WpfApplication.Current.Resources["SecondaryButton"],
                    Width = 110,
                    Height = 44
                };
                btnNo.Click += (s, e) => { result = WpfMsgBoxResult.No; win.DialogResult = false; win.Close(); };
                
                btnRow.Children.Add(btnYes);
                btnRow.Children.Add(btnNo);
            }
            else
            {
                var btnOk = new WpfButton 
                { 
                    Content = "OK", 
                    Style = (Style)WpfApplication.Current.Resources["PrimaryButton"], 
                    Width = 120, 
                    Height = 44 
                };
                btnOk.Click += (s, e) => { result = WpfMsgBoxResult.OK; win.DialogResult = true; win.Close(); };
                btnRow.Children.Add(btnOk);
            }

            btnArea.Child = btnRow;
            Grid.SetRow(btnArea, 2);
            mainGrid.Children.Add(btnArea);

            shadowBorder.Child = mainGrid;
            rootBorder.Child = shadowBorder;
            win.Content = rootBorder;

            shadowBorder.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) win.DragMove(); };

            win.ShowDialog();
            return result;
        }
    }
}


