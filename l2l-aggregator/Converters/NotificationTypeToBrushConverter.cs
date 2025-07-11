using Avalonia.Data.Converters;
using Avalonia.Media;
using l2l_aggregator.Services.Notification.Interface;
using System;
using System.Globalization;

namespace l2l_aggregator.Converters
{
    public class NotificationTypeToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                NotificationType.Info => new SolidColorBrush(Color.Parse("#DCEEFF")),
                NotificationType.Warning => new SolidColorBrush(Color.Parse("#FFF3CD")),
                NotificationType.Error => new SolidColorBrush(Color.Parse("#F8D7DA")),
                _ => Brushes.White
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
