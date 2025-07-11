using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace l2l_aggregator.Converters
{
    public class OneThirdWidthConverter: IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width)
                return width / 3;
            return 200;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
