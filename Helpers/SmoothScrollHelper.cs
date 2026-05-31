using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ClipboardPro.Helpers
{
    public static class SmoothScrollHelper
    {
        public static readonly DependencyProperty IsSmoothScrollEnabledProperty =
            DependencyProperty.RegisterAttached("IsSmoothScrollEnabled", typeof(bool), typeof(SmoothScrollHelper),
                new PropertyMetadata(false, OnIsSmoothScrollEnabledChanged));

        public static bool GetIsSmoothScrollEnabled(DependencyObject obj) => (bool)obj.GetValue(IsSmoothScrollEnabledProperty);
        public static void SetIsSmoothScrollEnabled(DependencyObject obj, bool value) => obj.SetValue(IsSmoothScrollEnabledProperty, value);

        // Internal attached properties to store state per ScrollViewer
        private static readonly DependencyProperty TargetOffsetProperty =
            DependencyProperty.RegisterAttached("TargetOffset", typeof(double), typeof(SmoothScrollHelper), new PropertyMetadata(0.0));

        private static readonly DependencyProperty CurrentOffsetProperty =
            DependencyProperty.RegisterAttached("CurrentOffset", typeof(double), typeof(SmoothScrollHelper), new PropertyMetadata(0.0));

        private static readonly DependencyProperty IsScrollingProperty =
            DependencyProperty.RegisterAttached("IsScrolling", typeof(bool), typeof(SmoothScrollHelper), new PropertyMetadata(false));

        private static void OnIsSmoothScrollEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer sv)
            {
                if ((bool)e.NewValue)
                {
                    sv.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
                    // Support for touch/manipulation can be added here if needed
                }
                else
                {
                    sv.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
                }
            }
        }

        private static readonly List<ScrollViewer> ActiveScrollViewers = new List<ScrollViewer>();

        private static void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                e.Handled = true;

                double targetOffset = (bool)sv.GetValue(IsScrollingProperty) 
                    ? (double)sv.GetValue(TargetOffsetProperty) 
                    : sv.VerticalOffset;

                targetOffset -= (e.Delta * 0.8);
                targetOffset = Math.Max(0, Math.Min(targetOffset, sv.ScrollableHeight));

                sv.SetValue(TargetOffsetProperty, targetOffset);

                if (!(bool)sv.GetValue(IsScrollingProperty))
                {
                    sv.SetValue(CurrentOffsetProperty, sv.VerticalOffset);
                    sv.SetValue(IsScrollingProperty, true);
                    
                    lock (ActiveScrollViewers)
                    {
                        if (ActiveScrollViewers.Count == 0)
                        {
                            CompositionTarget.Rendering += CompositionTarget_Rendering;
                        }
                        if (!ActiveScrollViewers.Contains(sv))
                        {
                            ActiveScrollViewers.Add(sv);
                        }
                    }
                }
            }
        }

        private static void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            List<ScrollViewer> toRemove = new List<ScrollViewer>();

            lock (ActiveScrollViewers)
            {
                foreach (var sv in ActiveScrollViewers)
                {
                    double targetOffset = (double)sv.GetValue(TargetOffsetProperty);
                    double currentOffset = (double)sv.GetValue(CurrentOffsetProperty);

                    double diff = targetOffset - currentOffset;

                    if (Math.Abs(diff) < 0.1)
                    {
                        sv.ScrollToVerticalOffset(targetOffset);
                        sv.SetValue(IsScrollingProperty, false);
                        toRemove.Add(sv);
                    }
                    else
                    {
                        currentOffset += diff * 0.15; // Slightly faster for responsiveness
                        sv.SetValue(CurrentOffsetProperty, currentOffset);
                        sv.ScrollToVerticalOffset(currentOffset);
                    }
                }

                foreach (var sv in toRemove)
                {
                    ActiveScrollViewers.Remove(sv);
                }

                if (ActiveScrollViewers.Count == 0)
                {
                    CompositionTarget.Rendering -= CompositionTarget_Rendering;
                }
            }
        }
    }
}
