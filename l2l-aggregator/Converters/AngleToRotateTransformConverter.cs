using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace l2l_aggregator.Converters
{
    public class AngleToRotateTransformConverter : IValueConverter
    {
        public static readonly AngleToRotateTransformConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double angle)
                return new RotateTransform { Angle = angle };
            return AvaloniaProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
