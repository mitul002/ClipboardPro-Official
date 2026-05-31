using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClipboardPro.Helpers
{
    public static class ScrollAnimationHelper
    {
        public static void ApplyScrollEffects(System.Windows.Controls.ListBox listBox)
        {
            if (listBox == null) return;

            // Find the ScrollViewer
            var scrollViewer = FindVisualChild<ScrollViewer>(listBox);
            if (scrollViewer == null) return;

            // Use ScrollChanged for immediate reaction
            scrollViewer.ScrollChanged += (s, e) => UpdateItems(listBox, scrollViewer);
            
            // Also hook into CompositionTarget.Rendering for super-smooth continuous updates
            CompositionTarget.Rendering += (s, e) => UpdateItems(listBox, scrollViewer);
            
            listBox.Loaded += (s, e) => UpdateItems(listBox, scrollViewer);
        }

        private static void UpdateItems(System.Windows.Controls.ListBox listBox, ScrollViewer sv)
        {
            double viewportHeight = sv.ViewportHeight;
            if (viewportHeight <= 0) return;

            // Use a try-catch because items might be in a state of flux during virtualization
            try
            {
                for (int i = 0; i < listBox.Items.Count; i++)
                {
                    var container = listBox.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                    if (container == null) continue;

                    // Get position relative to ScrollViewer
                    var transform = container.TransformToAncestor(sv);
                    System.Windows.Point relativePoint = transform.Transform(new System.Windows.Point(0, 0));
                    double top = relativePoint.Y;
                    double height = container.ActualHeight;
                    double bottom = top + height;

                    // --- REVEAL LOGIC (Bottom Entry) ---
                    // Items near the bottom of the viewport start small and fade in
                    double bottomThreshold = 200.0; 
                    double progress = 1.0;

                    if (top > viewportHeight - bottomThreshold)
                    {
                        // Item is entering from the bottom
                        double distanceIntoThreshold = top - (viewportHeight - bottomThreshold);
                        progress = 1.0 - Math.Clamp(distanceIntoThreshold / bottomThreshold, 0, 1);
                    }
                    else if (top < 0)
                    {
                        // Item is exiting from the top (optional: fade out at top too)
                        double topThreshold = 100.0;
                        if (top > -topThreshold)
                        {
                            progress = 1.0 - Math.Clamp(Math.Abs(top) / topThreshold, 0, 1);
                        }
                        else
                        {
                            progress = 0;
                        }
                    }

                    // Calculate transformations
                    // Scale: 0.8 to 1.0
                    double scale = 0.8 + (0.2 * progress);
                    // Opacity: 0 to 1.0
                    double opacity = progress;
                    // Y-Offset: 20px down to 0 (optional "float up" effect)
                    double translateY = 15 * (1.0 - progress);

                    // Find the presenter to animate
                    if (container is ListBoxItem lbi)
                    {
                        var presenter = FindVisualChild<ContentPresenter>(lbi);
                        if (presenter != null)
                        {
                            if (presenter.RenderTransform is not TransformGroup group)
                            {
                                group = new TransformGroup();
                                group.Children.Add(new ScaleTransform());
                                group.Children.Add(new TranslateTransform());
                                presenter.RenderTransform = group;
                                presenter.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
                            }

                            var st = (ScaleTransform)group.Children[0];
                            var tt = (TranslateTransform)group.Children[1];

                            st.ScaleX = scale;
                            st.ScaleY = scale;
                            tt.Y = translateY;
                            presenter.Opacity = opacity;
                        }
                    }
                }
            }
            catch { /* Virtualization in progress */ }
        }

        private static T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child is T t) return t;
                T? childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }
    }
}
