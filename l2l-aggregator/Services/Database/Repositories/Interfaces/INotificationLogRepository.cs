using System.Threading.Tasks;
using static l2l_aggregator.Services.Notification.NotificationService;

namespace l2l_aggregator.Services.Database.Repositories.Interfaces
{
    public interface INotificationLogRepository
    {
        Task SaveNotificationAsync(NotificationItem item);
    }
}
