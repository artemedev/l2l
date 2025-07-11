using Avalonia.Notification;
using System;
using System.Collections.ObjectModel;
using static l2l_aggregator.Services.Notification.NotificationService;

namespace l2l_aggregator.Services.Notification.Interface
{
    public enum NotificationType
    {
        Info,
        Warning,
        Error,
        Success
    }
    public interface INotificationService
    {
        INotificationMessageManager Manager { get; }

        INotificationMessage ShowMessage(string message, NotificationType type = NotificationType.Info, Action closeAction = null);

        NotificationMessageBuilder Default();

        // Коллекция уведомлений для отображения в Flyout
        ObservableCollection<NotificationItem> Notifications { get; }
    }
}
