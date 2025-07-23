using Avalonia.SimpleRouter;
using l2l_aggregator.Helpers.AggregationHelpers;
using l2l_aggregator.Infrastructure.OsIntegration.Firebird;
using l2l_aggregator.Services;
using l2l_aggregator.Services.AggregationService;
using l2l_aggregator.Services.ControllerService;
using l2l_aggregator.Services.Database;
using l2l_aggregator.Services.Database.Repositories;
using l2l_aggregator.Services.Database.Repositories.Interfaces;
using l2l_aggregator.Services.DmProcessing;
using l2l_aggregator.Services.Notification;
using l2l_aggregator.Services.Notification.Interface;
using l2l_aggregator.Services.Printing;
using l2l_aggregator.Services.ScannerService;
using l2l_aggregator.Services.ScannerService.Interfaces;
using l2l_aggregator.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

namespace l2l_aggregator
{
    public static class ServiceConfigurator
    {
        public static void ConfigureServices(IServiceCollection services, IConfiguration? configuration = null)
        {
            // Регистрация базовых сервисов
            services.AddSingleton<SessionService>();
            services.AddSingleton<DmScanService>();
            services.AddSingleton<TemplateService>();
            services.AddSingleton<ScannerListenerService>();
            services.AddSingleton<ScannerInputService>();
            //services.AddSingleton<DMProcessingService>();
            services.AddSingleton<ImageHelper>();
            services.AddSingleton<INotificationService, NotificationService>();
            services.AddSingleton<INotificationLogRepository, NotificationLogRepository>();

            // Регистрируем главную VM (она требует HistoryRouter)
            services.AddSingleton<MainWindowViewModel>();

            // Регистрация ViewModels (они зависят от HistoryRouter)
            services.AddTransient<InitializationViewModel>();
            services.AddTransient<AuthViewModel>();
            services.AddTransient<TaskListViewModel>();
            services.AddTransient<TaskDetailsViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<AggregationViewModel>();
            services.AddTransient<CameraSettingsViewModel>();


            // Регистрируем HistoryRouter перед ViewModels
            services.AddSingleton<HistoryRouter<ViewModelBase>>(s =>
                new HistoryRouter<ViewModelBase>(t => (ViewModelBase)s.GetRequiredService(t)));
            
            // Регистрируем работу с бд
            services.AddSingleton<IConfigRepository, ConfigRepository>();
            services.AddSingleton<IAggregationStateRepository, AggregationStateRepository>();



            services.AddSingleton<DatabaseService>();
            services.AddSingleton<DatabaseInitializer>(sp =>
            {
                string databasePath;
                if (OperatingSystem.IsWindows())
                {
                    databasePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TEST.fdb");
                }
                else
                {
                    LinuxFirebirdPermissionFixer.EnsureFirebirdDirectoryAccess();
                    // Используем системную директорию, куда firebird точно имеет доступ
                    databasePath = "/var/lib/l2l_aggregator/TEST.fdb";

                }
                return new DatabaseInitializer(databasePath);
            });
            // Регистрируем работу с api
            services.AddSingleton<DeviceCheckService>(); 
            services.AddSingleton<ConfigurationLoaderService>(); 
            services.AddSingleton<PrintingService>();
            services.AddSingleton<IScannerPortResolver>(PlatformResolverFactory.CreateScannerResolver());

            // Регистрируем работу с контроллером
            services.AddTransient<PcPlcConnectionService>();
            services.AddSingleton<DatabaseDataService>();
            services.AddSingleton<RemoteDatabaseService>();
            services.AddSingleton<DeviceInfoService>();

            services.AddSingleton<IDialogService, DialogService>();
        }
    }
}
