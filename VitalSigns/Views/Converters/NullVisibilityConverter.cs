using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VitalSigns.Views
{
    public class NullVisibilityConverter : IValueConverter
    {
        private object GetVisibility(object value)
        {
            if (value != null)
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return GetVisibility(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
