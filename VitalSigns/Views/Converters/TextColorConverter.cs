using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VitalSigns.Views
{
    class TextColorConverter : IValueConverter
    {
        private Color ParseColor(string colorName)
        {
            switch (colorName.ToLower())
            {
                case "red":
                    return Colors.Red;
                case "orange":
                    return Colors.Orange;
                case "yellow":
                    return Colors.Yellow;
                case "green":
                    return Colors.Green;
                case "blue":
                    return Colors.Blue;
                case "indigo":
                    return Colors.Indigo;
                case "violet":
                    return Colors.Violet;
                default:
                    return Colors.Black;
            }
        }

        private object GetColor(object value, string positiveColor, string negativeColor)
        {
            // Figure out which two colors these are
            Color firstColor = ParseColor(positiveColor);
            Color secondColor = ParseColor(negativeColor);

            if (value is bool)
            {
                bool objValue = (bool)value;
                return objValue ? new SolidColorBrush(firstColor) : new SolidColorBrush(secondColor);
            }
            else if (value is int)
            {
                int objValue = (int)value;
                return objValue >= 0 ? new SolidColorBrush(firstColor) : new SolidColorBrush(secondColor);
            }
            return value;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string param = parameter as string;
            if (param != null)
            {
                string[] colors = param.Split(new char[] { ';' });
                if (colors.Length < 2)
                {
                    return value;
                }
                return GetColor(value, colors[0], colors[1]);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}