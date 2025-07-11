using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace l2l_aggregator.Converters
{
    public class CenterToLeftConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is double center && values[1] is double size)
                return center - size / 2;

            return 0;
        }

        public object ConvertBack(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CenterToTopConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[0] is double center && values[1] is double size)
                return center - size / 2;

            return 0;
        }

        public object ConvertBack(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
