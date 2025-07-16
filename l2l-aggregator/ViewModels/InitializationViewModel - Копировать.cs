//using Avalonia.SimpleRouter;
//using CommunityToolkit.Mvvm.ComponentModel;
//using CommunityToolkit.Mvvm.Input;
//using l2l_aggregator.Models;
//using l2l_aggregator.Services;
//using l2l_aggregator.Services.Database;
//using l2l_aggregator.Services.Notification.Interface;
//using Refit;
//using System;
//using System.Net.Http;
//using System.Threading.Tasks;

//namespace l2l_aggregator.ViewModels
//{
//    public partial class InitializationViewModel : ViewModelBase
//    {
//        [ObservableProperty]
//        private string _infoMessage = "Проверка подключения к базе данных...";

//        [ObservableProperty]
//        private string _nameDevice = Environment.MachineName; // Используем имя компьютера по умолчанию

//        [ObservableProperty] private bool _isDeviceCheckInProgress;

//        [ObservableProperty]
//        private string _databaseInfo = "База данных: 172.16.3.237:3050";

//        private readonly HistoryRouter<ViewModelBase> _router;
//        private readonly DatabaseService _databaseService;
//        private readonly INotificationService _notificationService;
//        private readonly SessionService _sessionService;
//        private readonly DatabaseDataService _databaseDataService;
//        private readonly DeviceInfoService _deviceInfoService;

//        public InitializationViewModel(
//            DatabaseService databaseService,
//            HistoryRouter<ViewModelBase> router,
//            DatabaseDataService databaseDataService,
//            INotificationService notificationService,
//            DeviceInfoService deviceInfoService,
//            SessionService sessionService)
//        {
//            _notificationService = notificationService;
//            _databaseService = databaseService;
//            _router = router;
//            _sessionService = sessionService;
//            _databaseDataService = databaseDataService;
//            _deviceInfoService = deviceInfoService;
//        }

//        [RelayCommand]
//        public async Task CheckDatabaseConnectionAsync()
//        {
           
//            InfoMessage = "Проверка подключения к базе данных...";

//            try
//            {
//                bool isConnected = await _databaseDataService.TestConnectionAsync();

//                if (isConnected)
//                {
//                    InfoMessage = "Подключение установлено. Регистрация устройства...";

//                    // Создаем запрос с корректными данными для всех платформ
//                    var request = _deviceInfoService.CreateRegistrationRequest();

//                    var deviceRegistered = await _databaseDataService.RegisterDeviceAsync(request);

//                    if (deviceRegistered != null)
//                    {
//                        await _sessionService.SaveDeviceInfoAsync(request, deviceRegistered);

//                        InfoMessage = "Устройство зарегистрировано. Переход к авторизации...";
//                        _notificationService.ShowMessage("Инициализация завершена успешно", NotificationType.Success);

//                        _router.GoTo<AuthViewModel>();
//                    }
//                    else
//                    {
//                        InfoMessage = "Ошибка регистрации устройства в базе данных";
//                        _notificationService.ShowMessage(InfoMessage, NotificationType.Error);
//                    }
//                }
//                else
//                {
//                    InfoMessage = "Не удалось подключиться к базе данных";
//                    _notificationService.ShowMessage(InfoMessage, NotificationType.Error);
//                }
//            }
//            catch (Exception ex)
//            {
//                InfoMessage = $"Ошибка при инициализации: {ex.Message}";
//                _notificationService.ShowMessage(InfoMessage, NotificationType.Error);
//            }

//        }
//        [RelayCommand]
//        public async Task RetryConnectionAsync()
//        {
//            await CheckDatabaseConnectionAsync();
//        }

        
//    }
//}