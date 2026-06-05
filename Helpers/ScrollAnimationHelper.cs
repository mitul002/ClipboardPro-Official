using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClipboardPro.Helpers
{
    public static class ScrollAnimationHelper
    {
        private class ListBoxCache
        {
            public WeakReference<ItemsPresenter> ItemsPresenter { get; set; } = new(null!);
            public WeakReference<System.Windows.Controls.Panel> Panel { get; set; } = new(null!);
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<System.Windows.Controls.ListBox, ListBoxCache> _cache = new();
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ListBoxItem, ContentPresenter> _presenterCache = new();

        private static readonly Dictionary<System.Windows.Controls.ListBox, ScrollChangedEventHandler> _scrollHandlers = new();
        private static readonly Dictionary<System.Windows.Controls.ListBox, EventHandler> _layoutHandlers = new();

        private static ListBoxCache GetOrCreateCache(System.Windows.Controls.ListBox listBox)
        {
            return _cache.GetValue(listBox, _ => new ListBoxCache());
        }

        public static void ApplyScrollEffects(System.Windows.Controls.ListBox listBox)
        {
            if (listBox == null) return;

            RemoveScrollEffects(listBox);

            var scrollViewer = FindVisualChild<ScrollViewer>(listBox);
            if (scrollViewer == null)
            {
                RoutedEventHandler? loadedHandler = null;
                loadedHandler = (s, e) =>
                {
                    listBox.Loaded -= loadedHandler;
                    ApplyScrollEffects(listBox);
                };
                listBox.Loaded += loadedHandler;
                return;
            }

            ScrollChangedEventHandler scrollHandler = (s, e) => UpdateItems(listBox, scrollViewer);
            scrollViewer.ScrollChanged += scrollHandler;

            EventHandler layoutHandler = (s, e) => UpdateItems(listBox, scrollViewer);
            listBox.LayoutUpdated += layoutHandler;

            RoutedEventHandler? unloadedHandler = null;
            unloadedHandler = (s, e) =>
            {
                listBox.Unloaded -= unloadedHandler;
                scrollViewer.ScrollChanged -= scrollHandler;
                listBox.LayoutUpdated -= layoutHandler;
                _scrollHandlers.Remove(listBox);
                _layoutHandlers.Remove(listBox);
            };
            listBox.Unloaded += unloadedHandler;

            _scrollHandlers[listBox] = scrollHandler;
            _layoutHandlers[listBox] = layoutHandler;

            UpdateItems(listBox, scrollViewer);
        }

        public static void RemoveScrollEffects(System.Windows.Controls.ListBox listBox)
        {
            if (listBox == null) return;

            if (_scrollHandlers.TryGetValue(listBox, out var scrollHandler))
            {
                var scrollViewer = FindVisualChild<ScrollViewer>(listBox);
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollChanged -= scrollHandler;
                }
                _scrollHandlers.Remove(listBox);
            }

            if (_layoutHandlers.TryGetValue(listBox, out var layoutHandler))
            {
                listBox.LayoutUpdated -= layoutHandler;
                _layoutHandlers.Remove(listBox);
            }
        }

        private static void UpdateItems(System.Windows.Controls.ListBox listBox, ScrollViewer sv)
        {
            double viewportHeight = sv.ViewportHeight;
            if (viewportHeight <= 0) return;

            try
            {
                var cache = GetOrCreateCache(listBox);

                if (!cache.ItemsPresenter.TryGetTarget(out var itemsPresenter))
                {
                    itemsPresenter = FindVisualChild<ItemsPresenter>(listBox);
                    if (itemsPresenter != null)
                        cache.ItemsPresenter.SetTarget(itemsPresenter);
                }

                if (itemsPresenter == null) return;

                System.Windows.Controls.Panel? panel;
                if (!cache.Panel.TryGetTarget(out panel))
                {
                    panel = VisualTreeHelper.GetChild(itemsPresenter, 0) as System.Windows.Controls.Panel;
                    if (panel != null)
                        cache.Panel.SetTarget(panel);
                }

                if (panel == null) return;

                foreach (UIElement child in panel.Children)
                {
                    var container = child as FrameworkElement;
                    if (container == null || !container.IsLoaded || !container.IsVisible) continue;

                    // Ensure element has a visual parent to prevent TransformToAncestor exceptions
                    if (VisualTreeHelper.GetParent(container) == null) continue;

                    var transform = container.TransformToAncestor(sv);
                    System.Windows.Point relativePoint = transform.Transform(new System.Windows.Point(0, 0));
                    double top = relativePoint.Y;
                    double height = container.ActualHeight;

                    double bottomThreshold = 200.0;
                    double progress = 1.0;

                    if (top > viewportHeight - bottomThreshold)
                    {
                        double distanceIntoThreshold = top - (viewportHeight - bottomThreshold);
                        progress = 1.0 - Math.Clamp(distanceIntoThreshold / bottomThreshold, 0, 1);
                    }
                    else if (top < 0)
                    {
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

                    double scale = 0.8 + (0.2 * progress);
                    double opacity = progress;
                    double translateY = 15 * (1.0 - progress);

                    if (container is ListBoxItem lbi)
                    {
                        if (!_presenterCache.TryGetValue(lbi, out var presenter))
                        {
                            presenter = FindVisualChild<ContentPresenter>(lbi);
                            if (presenter != null)
                                _presenterCache.Add(lbi, presenter);
                        }

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
            catch { }
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
