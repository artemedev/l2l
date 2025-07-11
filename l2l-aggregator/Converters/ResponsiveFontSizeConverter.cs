using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace l2l_aggregator.Converters
{
    public class ResponsiveFontSizeConverter : IValueConverter
    {
        public double DefaultMinSize { get; set; } = 12;
        public double DefaultMaxSize { get; set; } = 36;
        public double DefaultScaleFactor { get; set; } = 0.02;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double width)
                return DefaultMinSize;

            double scaleFactor = DefaultScaleFactor;
            double minSize = DefaultMinSize;
            double maxSize = DefaultMaxSize;

            if (parameter is string paramStr)
            {
                var parts = paramStr.Split(';');
                if (parts.Length >= 1 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedScale))
                    scaleFactor = parsedScale;

                if (parts.Length >= 2 && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMin))
                    minSize = parsedMin;

                if (parts.Length >= 3 && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMax))
                    maxSize = parsedMax;
            }

            double size = width * scaleFactor;
            return Math.Clamp(size, minSize, maxSize);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
