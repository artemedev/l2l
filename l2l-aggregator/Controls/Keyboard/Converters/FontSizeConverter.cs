using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace l2l_aggregator.Controls.Keyboard.Converters
{
    public class FontSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // value — ширина или высота вьюпорта
            double size = System.Convert.ToDouble(value);
            double factor = System.Convert.ToDouble(parameter); // Например, 25 = шрифт будет 1/25 от ширины
            return size / factor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
