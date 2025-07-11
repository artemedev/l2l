using Avalonia;
using Avalonia.Data.Converters;
using System;

namespace l2l_aggregator.Controls.Keyboard.Converters
{
    public class ResponsiveMarginConverter : IValueConverter 
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double width)
            {
                // логика расчета margin
                return new Thickness(width * 0.01); 
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
