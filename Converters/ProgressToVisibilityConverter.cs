using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClipboardPro.Converters
{
    public class ProgressToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int progress && parameter is string targetStr)
            {
                if (int.TryParse(targetStr, out int target))
                {
                    return progress < target ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
