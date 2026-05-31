using System.Windows;
using System.Windows.Input;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfButton  = System.Windows.Controls.Button;
using WpfCursors = System.Windows.Input.Cursors;
using WpfDock    = System.Windows.Controls.Dock;
using WpfHA      = System.Windows.HorizontalAlignment;
using System;

namespace ClipboardPro.Views
{
    /// <summary>
    /// A minimal, premium dialog for adding or editing a text-expander snippet.
    /// </summary>
    public class AddSnippetDialog : Window
    {
        public string Trigger         { get; private set; } = string.Empty;
        public new string Content     { get; private set; } = string.Empty;
        public string Description     { get; private set; } = string.Empty;

        private readonly WpfTextBox _txTrigger;
        private readonly WpfTextBox _txContent;
        private readonly WpfTextBox _txDesc;

        public AddSnippetDialog(ClipboardPro.Models.SnippetItem? existing = null)
        {
            Title                  = existing == null ? "Add Snippet" : "Edit Snippet";
            Width                  = 500;
            SizeToContent          = SizeToContent.Height;
            WindowStyle            = WindowStyle.None;
            AllowsTransparency     = true;
            Background             = System.Windows.Media.Brushes.Transparent;
            WindowStartupLocation  = WindowStartupLocation.CenterOwner;
            ResizeMode             = ResizeMode.NoResize;

            // Root border (matches app style)
            var root = new System.Windows.Controls.Border
            {
                CornerRadius    = new CornerRadius(16),
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(28, 24, 28, 24),
            };
            root.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "BgDeep");
            root.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, "BorderBrush");
            root.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 20, ShadowDepth = 4, Opacity = 0.25,
                Color = System.Windows.Media.Colors.Black
            };
            root.MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };

            var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(0) };

            // Title row
            var titleRow = new System.Windows.Controls.DockPanel { LastChildFill = false, Margin = new Thickness(0, 0, 0, 20) };
            var titleBlock = new System.Windows.Controls.TextBlock
            {
                Text       = existing == null ? "New Snippet" : "Edit Snippet",
                FontSize   = 20,
                FontWeight = FontWeights.Bold,
            };
            titleBlock.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextPrimary");
            System.Windows.Controls.DockPanel.SetDock(titleBlock, WpfDock.Left);
            titleRow.Children.Add(titleBlock);

            var closeBtn = new WpfButton
            {
                Content     = "\uE711",
                FontFamily  = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize    = 12,
                Width       = 28, Height = 28,
                Cursor      = WpfCursors.Hand,
                BorderThickness = new Thickness(0),
                Background  = System.Windows.Media.Brushes.Transparent,
            };
            closeBtn.SetResourceReference(WpfButton.ForegroundProperty, "DangerBrush");
            closeBtn.Click += (s, e) => DialogResult = false;
            System.Windows.Controls.DockPanel.SetDock(closeBtn, WpfDock.Right);
            titleRow.Children.Add(closeBtn);
            panel.Children.Add(titleRow);

            // Subtitle
            var sub = new System.Windows.Controls.TextBlock
            {
                Text         = "Trigger must start or end with a special character (e.g. ;em, /addr, add#, ty;).\nAllowed special characters: ;  .  /  !  @  #",
                FontSize     = 12,
                Margin       = new Thickness(0, 0, 0, 20),
                TextWrapping = TextWrapping.Wrap,
            };
            sub.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextMuted");
            panel.Children.Add(sub);

            // Trigger field
            panel.Children.Add(MakeLabel("Shortcut (Trigger)"));
            _txTrigger = MakeTextBox(false);
            _txTrigger.Margin = new Thickness(0, 6, 0, 16);
            panel.Children.Add(_txTrigger);

            // Content field
            panel.Children.Add(MakeLabel("Expanded Text"));
            _txContent = MakeTextBox(true);
            _txContent.MinHeight    = 90;
            _txContent.MaxHeight    = 200;
            _txContent.AcceptsReturn = true;
            _txContent.TextWrapping  = TextWrapping.Wrap;
            _txContent.Margin = new Thickness(0, 6, 0, 16);
            panel.Children.Add(_txContent);

            // Description field (optional)
            panel.Children.Add(MakeLabel("Label / Description (optional)"));
            _txDesc = MakeTextBox(false);
            _txDesc.Margin = new Thickness(0, 6, 0, 24);
            panel.Children.Add(_txDesc);

            // Error label
            var errLabel = new System.Windows.Controls.TextBlock { FontSize = 12, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 12) };
            errLabel.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "DangerBrush");
            panel.Children.Add(errLabel);

            // Buttons row
            var btnRow = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = WpfHA.Right };

            var btnCancel = new WpfButton
            {
                Width    = 100, Height = 38,
                Margin   = new Thickness(0, 0, 10, 0),
                Cursor   = WpfCursors.Hand,
                FontSize = 13,
            };
            var cancelContent = new System.Windows.Controls.TextBlock { Text = "Cancel", FontSize = 13 };
            cancelContent.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextSecondary");
            btnCancel.Content = cancelContent;
            btnCancel.SetResourceReference(WpfButton.StyleProperty, "IconButtonStyle");
            btnCancel.Click += (s, e) => DialogResult = false;
            btnRow.Children.Add(btnCancel);

            var btnSave = new WpfButton
            {
                Width       = 130, Height = 38,
                FontWeight  = FontWeights.Bold,
                FontSize    = 13,
                Cursor      = WpfCursors.Hand,
                BorderThickness = new Thickness(0),
            };
            btnSave.SetResourceReference(WpfButton.BackgroundProperty, "AccentPrimary");
            btnSave.Foreground = System.Windows.Media.Brushes.White;
            var saveText = existing == null ? "Save Snippet" : "Update Snippet";
            var saveContent = new System.Windows.Controls.TextBlock { Text = saveText, Foreground = System.Windows.Media.Brushes.White };
            btnSave.Content = saveContent;
            btnSave.Resources.Add(typeof(System.Windows.Controls.Border), new Style(typeof(System.Windows.Controls.Border))
            {
                Setters = { new Setter(System.Windows.Controls.Border.CornerRadiusProperty, new CornerRadius(10)) }
            });
            btnSave.Click += (s, e) =>
            {
                var rawTrigger = _txTrigger.Text.Trim();

                if (string.IsNullOrWhiteSpace(rawTrigger))
                {
                    errLabel.Text       = "⚠  Please enter a trigger shortcut.";
                    errLabel.Visibility = Visibility.Visible;
                    _txTrigger.Focus();
                    return;
                }

                // ── Forced prefix / suffix rule ────────────────────────────
                if (!HasValidDelimiter(rawTrigger))
                {
                    errLabel.Text = "⚠  Trigger must start or end with a special character (;  .  /  !  @  #).\n" +
                                    "Example: ;em  or  add#";
                    errLabel.Visibility = Visibility.Visible;
                    _txTrigger.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(_txContent.Text))
                {
                    errLabel.Text       = "⚠  Please enter the expanded text.";
                    errLabel.Visibility = Visibility.Visible;
                    _txContent.Focus();
                    return;
                }

                Trigger      = rawTrigger;
                Content      = _txContent.Text;
                Description  = _txDesc.Text.Trim();
                DialogResult = true;
            };
            btnRow.Children.Add(btnSave);
            panel.Children.Add(btnRow);

            root.Child = panel;
            base.Content = root;

            if (existing != null)
            {
                _txTrigger.Text = existing.Trigger;
                _txContent.Text = existing.Content;
                _txDesc.Text    = existing.Description ?? "";
            }

            // Focus trigger field when shown
            Loaded += (s, e) => _txTrigger.Focus();
        }

        // ── Prefix / Suffix validation ─────────────────────────────────────
        /// <summary>
        /// Returns true when <paramref name="trigger"/> starts or ends with one of the
        /// allowed special characters ( ; . / ! @ # ).
        /// This is enforced so that triggers never conflict with everyday words.
        /// </summary>
        private static bool HasValidDelimiter(string trigger)
        {
            if (string.IsNullOrEmpty(trigger)) return false;

            char[] allowedSymbols = { ';', '.', '/', '!', '@', '#' };
            
            // Check prefix
            if (Array.IndexOf(allowedSymbols, trigger[0]) >= 0) return true;

            // Check suffix
            if (Array.IndexOf(allowedSymbols, trigger[trigger.Length - 1]) >= 0) return true;

            return false;
        }

        private static System.Windows.Controls.TextBlock MakeLabel(string text)
        {
            var tb = new System.Windows.Controls.TextBlock { Text = text, FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0) };
            tb.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextPrimary");
            return tb;
        }

        private static WpfTextBox MakeTextBox(bool isMultiLine)
        {
            var tb = new WpfTextBox
            {
                FontSize        = 13,
                Height          = isMultiLine ? double.NaN : 38,
                Padding         = new Thickness(12, 8, 12, 8),
                BorderThickness = new Thickness(1),
            };
            tb.SetResourceReference(WpfTextBox.BackgroundProperty,  "BgSidebar");
            tb.SetResourceReference(WpfTextBox.ForegroundProperty,  "TextPrimary");
            tb.SetResourceReference(WpfTextBox.BorderBrushProperty, "BorderBrush");
            tb.SetResourceReference(WpfTextBox.CaretBrushProperty,  "AccentPrimary");
            tb.Resources.Add(typeof(System.Windows.Controls.Border), new Style(typeof(System.Windows.Controls.Border))
            {
                Setters = { new Setter(System.Windows.Controls.Border.CornerRadiusProperty, new CornerRadius(8)) }
            });
            return tb;
        }
    }
}
