using l2l_aggregator.Services.Database.Repositories.Interfaces;

namespace l2l_aggregator.Services.Database
{
    public class DatabaseService
    {
        public IConfigRepository Config { get; }
        public INotificationLogRepository NotificationLog { get; }

        public DatabaseService(
            IConfigRepository config,
            INotificationLogRepository notificationLog)
        {
            Config = config;
            NotificationLog = notificationLog;
        }
    }
}
