using Avalonia.SimpleRouter;
using l2l_aggregator.Services;
using l2l_aggregator.Services.AggregationService;
using l2l_aggregator.Services.Configuration;
using l2l_aggregator.Services.ControllerService;
using l2l_aggregator.Services.Database;
using l2l_aggregator.Services.DmProcessing;
using l2l_aggregator.Services.Notification;
using l2l_aggregator.Services.Notification.Interface;
using l2l_aggregator.Services.Printing;
using l2l_aggregator.Services.ScannerService;
using l2l_aggregator.Services.ScannerService.Interfaces;
using l2l_aggregator.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
            services.AddSingleton<ImageProcessorService>();
            services.AddSingleton<INotificationService, NotificationService>();

            // Регистрируем главную VM (она требует HistoryRouter)
            services.AddSingleton<MainWindowViewModel>();

            // Регистрация ViewModels (они зависят от HistoryRouter)
            services.AddTransient<AuthViewModel>();
            services.AddTransient<TaskListViewModel>();
            services.AddTransient<TaskDetailsViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<AggregationViewModel>();
            services.AddTransient<CameraSettingsViewModel>();


            // Регистрируем HistoryRouter перед ViewModels
            services.AddSingleton<HistoryRouter<ViewModelBase>>(s =>
                new HistoryRouter<ViewModelBase>(t => (ViewModelBase)s.GetRequiredService(t)));


            services.AddSingleton<IConfigurationFileService, ConfigurationFileService>();

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
