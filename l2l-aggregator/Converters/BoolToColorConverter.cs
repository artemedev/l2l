using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace l2l_aggregator.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isConnected)
            {
                string colors = parameter as string ?? "Green,Red";
                string[] colorParts = colors.Split(',');

                string colorName = isConnected ? colorParts[0] : colorParts[1];
                return SolidColorBrush.Parse(colorName);
            }

            return SolidColorBrush.Parse("Gray");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
