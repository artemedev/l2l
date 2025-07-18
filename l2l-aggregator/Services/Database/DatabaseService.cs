using l2l_aggregator.Services.Database.Repositories.Interfaces;

namespace l2l_aggregator.Services.Database
{
    public class DatabaseService
    {
        public IConfigRepository Config { get; }
        public INotificationLogRepository NotificationLog { get; }

        public IAggregationStateRepository AggregationState { get; }
        public DatabaseService(
            IConfigRepository config,
            INotificationLogRepository notificationLog,
            IAggregationStateRepository aggregationState)
        {
            Config = config;
            NotificationLog = notificationLog;
            AggregationState = aggregationState;
        }
    }
}
