using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.SimpleRouter;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DM_wraper_NS;
using l2l_aggregator.Services;
using l2l_aggregator.Services.ControllerService;
using l2l_aggregator.Services.DmProcessing;
using l2l_aggregator.Services.Notification.Interface;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace l2l_aggregator.ViewModels
{
    public partial class CameraSettingsViewModel : ViewModelBase
    {
        public string Title { get; set; } = "Настройка нулевой точки";

        private readonly DmScanService _dmScanService;
        private readonly HistoryRouter<ViewModelBase> _router;

        [ObservableProperty] private Bitmap scannedImage;
        public IRelayCommand<SizeChangedEventArgs> ImageSizeChangedCommand { get; }

        [ObservableProperty] private double imageWidth;
        [ObservableProperty] private double imageHeight;

        // Свойства PLC настроек
        [ObservableProperty] private bool forcePositioning;
        [ObservableProperty] private bool positioningPermit;
        [ObservableProperty] private ushort retreatZeroHomePosition = 70;
        [ObservableProperty] private ushort zeroPositioningTime = 10000;
        [ObservableProperty] private ushort estimatedZeroHomeDistance = 252;
        [ObservableProperty] private ushort directionChangeTime = 500;
        [ObservableProperty] private ushort camMovementVelocity = 20;
        [ObservableProperty] private ushort camBoxMinDistance = 500;
        [ObservableProperty] private ushort lightLevel = 100;
        [ObservableProperty] private ushort lightDelay = 1000;
        [ObservableProperty] private ushort lightExposure = 4000;
        [ObservableProperty] private ushort camDelay = 1000;
        [ObservableProperty] private ushort camExposure = 30;
        [ObservableProperty] private bool continuousLightMode = false;

        [ObservableProperty] private string positioningStatusText = "Готов к позиционированию";
        [ObservableProperty] private bool isPositioningInProgress = false;
        [ObservableProperty] private bool canStartPositioning = true;
        [ObservableProperty] private string positioningProgress = "";

        static result_data dmrData;

        //сервис работы с сессией
        private readonly SessionService _sessionService;

        //сервис нотификаций
        private readonly INotificationService _notificationService;


        private PcPlcConnectionService _plcConnectionService;

        private readonly ILogger<CameraSettingsViewModel> _logger;

        //для отслеживания состояния загрузки камеры
        [ObservableProperty] private bool canScan = true;
        // Токен отмены для позиционирования
        private CancellationTokenSource _positioningCancellationTokenSource;
        public CameraSettingsViewModel(HistoryRouter<ViewModelBase> router, 
                                        DmScanService dmScanService, 
                                        SessionService sessionService, 
                                        INotificationService notificationService, 
                                        ILogger<CameraSettingsViewModel> logger, 
                                        PcPlcConnectionService plcConnectionService)
        {
            _sessionService = sessionService;
            _notificationService = notificationService;
            _plcConnectionService = plcConnectionService;
            _router = router;
            _dmScanService = dmScanService;

            ImageSizeChangedCommand = new RelayCommand<SizeChangedEventArgs>(OnImageSizeChanged);
            InitializeAsync();
            _logger = logger;
        }

        private void OnPositioningStatusChanged(PositioningStatus status)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                switch (status)
                {
                    case PositioningStatus.Started:
                        PositioningStatusText = "Запуск позиционирования...";
                        PositioningProgress = "1/7";
                        break;
                    case PositioningStatus.ParametersSet:
                        PositioningStatusText = "Параметры установлены";
                        PositioningProgress = "2/7";
                        break;
                    case PositioningStatus.ForcePositioningSet:
                        PositioningStatusText = "Бит принудительного позиционирования установлен";
                        PositioningProgress = "3/7";
                        break;
                    case PositioningStatus.PlcRequestReceived:
                        PositioningStatusText = "Получен запрос от ПЛК";
                        PositioningProgress = "4/7";
                        break;
                    case PositioningStatus.ForcePositioningReset:
                        PositioningStatusText = "Бит принудительного позиционирования сброшен";
                        PositioningProgress = "5/7";
                        break;
                    case PositioningStatus.PermissionGranted:
                        PositioningStatusText = "Разрешение на позиционирование выдано";
                        PositioningProgress = "6/7";
                        break;
                    case PositioningStatus.SystemPositioned:
                        PositioningStatusText = "Система спозиционирована";
                        PositioningProgress = "7/7";
                        break;
                    case PositioningStatus.Completed:
                        PositioningStatusText = "Позиционирование завершено успешно";
                        PositioningProgress = "Завершено";
                        IsPositioningInProgress = false;
                        CanStartPositioning = true;
                        break;
                    case PositioningStatus.Error:
                        PositioningStatusText = "Ошибка позиционирования";
                        PositioningProgress = "Ошибка";
                        IsPositioningInProgress = false;
                        CanStartPositioning = true;
                        break;
                }
            });
        }
        private async Task InitializeAsync()
        {
            CanScan = false;
            await ConnectToPlcAsync();
            await LoadSettingsFromPlcAsync();
            _dmScanService.StopScan();
            var recognParams = new recogn_params
            {
                CamInterfaces = "GigEVision2",
                cameraName = _sessionService.CameraIP,
                _Preset = new camera_preset(_sessionService.CameraModel),
                softwareTrigger = true, //поменять на false
            };
            _dmScanService.ConfigureParams(recognParams);
            _dmScanService.StartScan();

            await Task.Delay(10000);
            CanScan = true;
        }

        private async Task ConnectToPlcAsync()
        {
            if (string.IsNullOrWhiteSpace(_sessionService.ControllerIP))
            {
                _notificationService.ShowMessage("IP контроллера не задан!");
                return;
            }

            try
            {
                //_plcConnection = new PcPlcConnectionService(_logger);
                bool connected = await _plcConnectionService.ConnectAsync(_sessionService.ControllerIP);

                if (!connected)
                {
                    _notificationService.ShowMessage("Не удалось подключиться к контроллеру!");
                    return;
                }

                _notificationService.ShowMessage("Подключение к контроллеру установлено");
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка подключения к контроллеру: {ex.Message}");
            }
        }

        private async Task LoadSettingsFromPlcAsync()
        {
            if (_plcConnectionService?.IsConnected != true) return;

            try
            {
                // Загрузка настроек позиционирования
                var positioningSettings = await _plcConnectionService.GetPositioningSettingsAsync();
                RetreatZeroHomePosition = positioningSettings.RetreatZeroHomePosition;
                ZeroPositioningTime = positioningSettings.ZeroPositioning;
                EstimatedZeroHomeDistance = positioningSettings.EstimatedZeroHomeDistance;
                DirectionChangeTime = positioningSettings.TimeBetweenDirectionsChange;
                CamMovementVelocity = positioningSettings.CamMovementVelocity;

                // Загрузка настроек освещения
                var lightingSettings = await _plcConnectionService.GetLightingSettingsAsync();
                LightLevel = lightingSettings.LightLevel;
                LightDelay = lightingSettings.LightDelay;
                LightExposure = lightingSettings.LightExposure;
                CamDelay = lightingSettings.CamDelay;
                CamExposure = lightingSettings.CamExposure;
                ContinuousLightMode = lightingSettings.ContinuousLightMode;

                _notificationService.ShowMessage("Настройки загружены из контроллера");
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка загрузки настроек: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task SavePositioningSettings()
        {
            if (_plcConnectionService?.IsConnected != true)
            {
                _notificationService.ShowMessage("Нет подключения к контроллеру!");
                return;
            }

            try
            {
                var settings = new PositioningSettings
                {
                    RetreatZeroHomePosition = RetreatZeroHomePosition,
                    ZeroPositioning = ZeroPositioningTime,
                    EstimatedZeroHomeDistance = EstimatedZeroHomeDistance,
                    TimeBetweenDirectionsChange = DirectionChangeTime,
                    CamMovementVelocity = CamMovementVelocity
                };

                await _plcConnectionService.SetPositioningSettingsAsync(settings);
                _notificationService.ShowMessage("Настройки позиционирования сохранены");
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка сохранения настроек: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task SaveLightingSettings()
        {
            if (_plcConnectionService?.IsConnected != true)
            {
                _notificationService.ShowMessage("Нет подключения к контроллеру!");
                return;
            }

            try
            {
                var settings = new LightingSettings
                {
                    LightLevel = LightLevel,
                    LightDelay = LightDelay,
                    LightExposure = LightExposure,
                    CamDelay = CamDelay,
                    CamExposure = CamExposure,
                    ContinuousLightMode = ContinuousLightMode
                };

                await _plcConnectionService.SetLightingSettingsAsync(settings);
                _notificationService.ShowMessage("Настройки освещения сохранены");
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка сохранения настроек: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task ForcePositioningCommand()
        {
            if (_plcConnectionService?.IsConnected != true)
            {
                _notificationService.ShowMessage("Нет подключения к контроллеру!");
                return;
            }

            try
            {
                await _plcConnectionService.ForcePositioningAsync();
                _notificationService.ShowMessage("Принудительное позиционирование запущено");
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка позиционирования: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task GrantPositioningPermission()
        {
            if (_plcConnectionService?.IsConnected != true)
            {
                _notificationService.ShowMessage("Нет подключения к контроллеру!");
                return;
            }

            try
            {
                await _plcConnectionService.GrantPositioningPermissionAsync();
                _notificationService.ShowMessage("Разрешение на позиционирование выдано");
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка выдачи разрешения: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task Scan()
        {
            //старое
            //_dmScanService.getScan();
            //новое
            //await _dmScanService.WaitForStartOkAsync();
            //_dmScanService.StopScan();
            //var recognParams = new recogn_params
            //{
            //    CamInterfaces = "GigEVision2",
            //    cameraName = _sessionService.CameraIP,
            //    _Preset = new camera_preset(_sessionService.CameraModel),
            //    softwareTrigger = true, //поменять на false
            //};
            //_dmScanService.ConfigureParams(recognParams);
            //_dmScanService.StartScan();

            //await Task.Delay(600000);
            _dmScanService.startShot();
            dmrData = await _dmScanService.WaitForResultAsync();
            using (var ms = new MemoryStream())
            {
                dmrData.rawImage.SaveAsBmp(ms);
                ms.Seek(0, SeekOrigin.Begin);
                ScannedImage = new Bitmap(ms);
            }
        }
        private void OnImageSizeChanged(SizeChangedEventArgs e)
        {
            imageWidth = e.NewSize.Width;
            imageHeight = e.NewSize.Height;
        }
        [RelayCommand]
        public void GoBack()
        {
            _plcConnectionService?.Disconnect();
            _plcConnectionService?.Dispose();
            // Переход на страницу назад
            _router.GoTo<SettingsViewModel>();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _plcConnectionService?.Disconnect();
                _plcConnectionService?.Dispose();
            }
            base.Dispose(disposing);
        }


        /// <summary>
        /// Выполняет полную последовательность позиционирования согласно протоколу
        /// </summary>
        [RelayCommand]
        public async Task ExecuteFullPositioning()
        {
            if (_plcConnectionService?.IsConnected != true)
            {
                _notificationService.ShowMessage("Нет подключения к контроллеру!");
                return;
            }

            if (IsPositioningInProgress)
            {
                _notificationService.ShowMessage("Позиционирование уже выполняется!");
                return;
            }

            try
            {
                IsPositioningInProgress = true;
                CanStartPositioning = false;
                PositioningStatusText = "Подготовка к позиционированию...";
                PositioningProgress = "0/7";

                // Создаем токен отмены
                _positioningCancellationTokenSource = new CancellationTokenSource();

                // Создаем настройки позиционирования из текущих значений
                var settings = new PositioningSettings
                {
                    RetreatZeroHomePosition = RetreatZeroHomePosition,
                    ZeroPositioning = ZeroPositioningTime,
                    EstimatedZeroHomeDistance = EstimatedZeroHomeDistance,
                    TimeBetweenDirectionsChange = DirectionChangeTime,
                    CamMovementVelocity = CamMovementVelocity
                };

                // Выполняем полную последовательность
                var result = await _plcConnectionService.ExecuteFullPositioningSequenceAsync(
                    settings,
                    _positioningCancellationTokenSource.Token);

                if (result.Success)
                {
                    _notificationService.ShowMessage("Позиционирование выполнено успешно!");
                }
                else
                {
                    _notificationService.ShowMessage($"Ошибка позиционирования: {result.ErrorMessage}");
                    PositioningStatusText = $"Ошибка: {result.ErrorMessage}";
                    PositioningProgress = "Ошибка";
                }
            }
            catch (OperationCanceledException)
            {
                _notificationService.ShowMessage("Позиционирование отменено пользователем");
                PositioningStatusText = "Позиционирование отменено";
                PositioningProgress = "Отменено";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during positioning");
                _notificationService.ShowMessage($"Неожиданная ошибка: {ex.Message}");
                PositioningStatusText = $"Неожиданная ошибка: {ex.Message}";
                PositioningProgress = "Ошибка";
            }
            finally
            {
                IsPositioningInProgress = false;
                CanStartPositioning = true;
                _positioningCancellationTokenSource?.Dispose();
                _positioningCancellationTokenSource = null;
            }
        }
        /// <summary>
        /// Отменяет выполняющееся позиционирование
        /// </summary>
        [RelayCommand]
        public void CancelPositioning()
        {
            if (_positioningCancellationTokenSource != null && !_positioningCancellationTokenSource.Token.IsCancellationRequested)
            {
                _positioningCancellationTokenSource.Cancel();
                _notificationService.ShowMessage("Запрос на отмену позиционирования отправлен");
            }
        }
    }
}
