using Dapper;
using l2l_aggregator.Services.Database.Repositories.Interfaces;
using System.Threading.Tasks;
using static l2l_aggregator.Services.Notification.NotificationService;

namespace l2l_aggregator.Services.Database.Repositories
{
    public class NotificationLogRepository : BaseRepository, INotificationLogRepository
    {
        public NotificationLogRepository(DatabaseInitializer dbService) : base(dbService) { }

        public Task SaveNotificationAsync(NotificationItem item)
        {
            return WithConnectionAsync(conn =>
                        conn.ExecuteAsync(
                            @"INSERT INTO NOTIFICATION_LOG (MESSAGE, TYPE, USERNAME, CREATED_AT) 
                              VALUES (@Message, @Type, @UserName, @CreatedAt)",
                            new
                            {
                                item.Message,
                                Type = item.Type.ToString(),
                                item.UserName,
                                item.CreatedAt
                            }));
        }
    }
}
