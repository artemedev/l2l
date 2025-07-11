using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace l2l_aggregator.Converters
{
    public class BooleanToAggregationStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (bool)value ? "Агрегировано" : "Ожидает агрегации";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
