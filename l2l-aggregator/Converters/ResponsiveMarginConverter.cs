using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace l2l_aggregator.Converters
{
    public class ResponsiveMarginConverter : IValueConverter
    {
        // Минимальное значение отступа
        public double MinMargin { get; set; } = 4;

        // Максимальное значение отступа
        public double MaxMargin { get; set; } = 20;

        // Масштабный коэффициент (на сколько процентов от ширины рассчитывается отступ)
        public double ScaleFactor { get; set; } = 0.02; // 2%

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width)
            {
                double min = MinMargin;
                double max = MaxMargin;

                if (parameter is string paramStr)
                {
                    var parts = paramStr.Split(',');
                    if (parts.Length == 2 &&
                        double.TryParse(parts[0], out var parsedMin) &&
                        double.TryParse(parts[1], out var parsedMax))
                    {
                        min = parsedMin;
                        max = parsedMax;
                    }
                }

                double margin = Math.Clamp(width * ScaleFactor, min, max);
                return new Thickness(margin);
            }

            return new Thickness(MinMargin);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
