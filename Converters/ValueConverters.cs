using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClipboardPro.Converters
{
    /// <summary>true → Visible, false → Collapsed</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility.Visible;
    }

    /// <summary>Converts a Base64 PNG string to a BitmapImage for the thumbnail.</summary>
    public class Base64ToImageConverter : IValueConverter
    {
        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string b64 || string.IsNullOrEmpty(b64)) return null;
            try
            {
                var bytes = System.Convert.FromBase64String(b64);
                using var ms = new System.IO.MemoryStream(bytes);
                var img   = new System.Windows.Media.Imaging.BitmapImage();
                img.BeginInit();
                img.StreamSource    = ms;
                img.CacheOption     = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                return img;
            }
            catch { return null; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Loads image directly from disk with caching and small thumbnails for low RAM usage.</summary>
    public class ImagePathToImageConverter : IValueConverter
    {
        // Weak-reference cache to avoid loading the same thumbnail multiple times
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, WeakReference<System.Windows.Media.Imaging.BitmapImage>> _cache = new();

        // Strong-reference MRU cache to keep the last 100 images in memory (preventing GC cleanups for virtualized scroll)
        private static readonly System.Collections.Generic.List<System.Windows.Media.Imaging.BitmapImage> _mruCache = new();
        private static readonly object _mruLock = new();
        private const int MaxMruCount = 100;

        private static void AddToMru(System.Windows.Media.Imaging.BitmapImage img)
        {
            lock (_mruLock)
            {
                _mruCache.Remove(img);
                _mruCache.Add(img);
                if (_mruCache.Count > MaxMruCount)
                {
                    _mruCache.RemoveAt(0); // Remove oldest
                }
            }
        }

        public static void ClearCache(string? fullPath)
        {
            if (!string.IsNullOrEmpty(fullPath))
            {
                _cache.TryRemove(fullPath, out var weakRef);
                if (weakRef != null && weakRef.TryGetTarget(out var img))
                {
                    lock (_mruLock)
                    {
                        _mruCache.Remove(img);
                    }
                }
            }
        }

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string path || string.IsNullOrEmpty(path)) return null;

            // Resolve relative paths (filenames)
            string fullPath = path;
            if (!System.IO.Path.IsPathRooted(path))
            {
                var appData = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClipboardPro", "Images");
                fullPath = System.IO.Path.Combine(appData, path);
            }

            if (!System.IO.File.Exists(fullPath)) return null;

            // Check cache first
            if (_cache.TryGetValue(fullPath, out var weakRef) && weakRef.TryGetTarget(out var cached))
            {
                AddToMru(cached);
                return cached;
            }

            try
            {
                // Load from file stream to avoid locking the file
                using var fs = new System.IO.FileStream(fullPath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
                var img = new System.Windows.Media.Imaging.BitmapImage();
                img.BeginInit();
                img.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                img.StreamSource = fs;
                img.DecodePixelWidth = 100; // Smaller thumbnail = less RAM (~40% savings vs 150px)
                img.EndInit();
                img.Freeze();

                // Store in weak-reference cache
                _cache[fullPath] = new WeakReference<System.Windows.Media.Imaging.BitmapImage>(img);
                AddToMru(img);
                return img;
            }
            catch { return null; }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }


    /// <summary>int > 0 → Visible, 0 → Collapsed</summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count) return count > 0 ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Visibility.Collapsed;
            if (value is string s && string.IsNullOrEmpty(s)) return Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Returns Collapsed if value is NOT null/empty, otherwise Visible.</summary>
    public class InverseNullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Visibility.Visible;
            if (value is string s && string.IsNullOrEmpty(s)) return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Compares two values and returns true if they are equal.</summary>
    public class ComparisonConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return false;
            
            // Handle cases where values might be null or different types
            string? val1 = values[0]?.ToString();
            string? val2 = values[1]?.ToString();

            if (val1 == null || val2 == null) return false;

            // Trim and compare case-insensitively to be safe
            return string.Equals(val1.Trim(), val2.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class TypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return Visibility.Collapsed;
            var targetTypes = parameter.ToString()?.Split('|');
            if (targetTypes != null)
            {
                string valStr = value.ToString() ?? "";
                foreach (var t in targetTypes)
                {
                    if (valStr.Equals(t.Trim(), StringComparison.OrdinalIgnoreCase)) return Visibility.Visible;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : false;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : false;
    }

    public class GridWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width)
            {
                // Account for scrollbar and margins
                double availableWidth = width - 40;
                
                int columns = 1;
                if (availableWidth > 1200) columns = 4;
                else if (availableWidth > 800) columns = 3;
                else if (availableWidth > 450) columns = 2;
                
                return (availableWidth / columns) - 4; // Subtract margin
            }
            return 300.0;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
