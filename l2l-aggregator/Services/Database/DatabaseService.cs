using l2l_aggregator.Services.Database.Repositories.Interfaces;

namespace l2l_aggregator.Services.Database
{
    public class DatabaseService
    {
        public IUserAuthRepository UserAuth { get; }
        public IConfigRepository Config { get; }
        public INotificationLogRepository NotificationLog { get; }

        public IAggregationStateRepository AggregationState { get; }
        public DatabaseService(
            IUserAuthRepository userAuth,
            IConfigRepository config,
            INotificationLogRepository notificationLog,
            IAggregationStateRepository aggregationState)
        {
            UserAuth = userAuth;
            Config = config;
            NotificationLog = notificationLog;
            AggregationState = aggregationState;
        }
    }
}
