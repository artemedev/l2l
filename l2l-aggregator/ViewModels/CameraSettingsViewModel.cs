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

        static result_data dmrData;

        //сервис работы с сессией
        private readonly SessionService _sessionService;

        //сервис нотификаций
        private readonly INotificationService _notificationService;


        private PcPlcConnectionService _plcConnection;

        private readonly ILogger<PcPlcConnectionService> _logger;

        //для отслеживания состояния загрузки камеры
        [ObservableProperty] private bool canScan = true;

        public CameraSettingsViewModel(HistoryRouter<ViewModelBase> router, DmScanService dmScanService, SessionService sessionService, INotificationService notificationService, ILogger<PcPlcConnectionService> logger)
        {
            _sessionService = sessionService;
            _notificationService = notificationService;
            //_plcConnection = plcConnection;
            _router = router;
            _dmScanService = dmScanService;

            ImageSizeChangedCommand = new RelayCommand<SizeChangedEventArgs>(OnImageSizeChanged);
            _ = InitializeAsync();
            _logger = logger;
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
                _plcConnection = new PcPlcConnectionService(_logger);
                bool connected = await _plcConnection.ConnectAsync(_sessionService.ControllerIP);

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
            if (_plcConnection?.IsConnected != true) return;

            try
            {
                // Загрузка настроек позиционирования
                var positioningSettings = await _plcConnection.GetPositioningSettingsAsync();
                RetreatZeroHomePosition = positioningSettings.RetreatZeroHomePosition;
                ZeroPositioningTime = positioningSettings.ZeroPositioning;
                EstimatedZeroHomeDistance = positioningSettings.EstimatedZeroHomeDistance;
                DirectionChangeTime = positioningSettings.TimeBetweenDirectionsChange;
                CamMovementVelocity = positioningSettings.CamMovementVelocity;

                // Загрузка настроек освещения
                var lightingSettings = await _plcConnection.GetLightingSettingsAsync();
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
            if (_plcConnection?.IsConnected != true)
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

                await _plcConnection.SetPositioningSettingsAsync(settings);
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
            if (_plcConnection?.IsConnected != true)
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

                await _plcConnection.SetLightingSettingsAsync(settings);
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
            if (_plcConnection?.IsConnected != true)
            {
                _notificationService.ShowMessage("Нет подключения к контроллеру!");
                return;
            }

            try
            {
                await _plcConnection.ForcePositioningAsync();
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
            if (_plcConnection?.IsConnected != true)
            {
                _notificationService.ShowMessage("Нет подключения к контроллеру!");
                return;
            }

            try
            {
                await _plcConnection.GrantPositioningPermissionAsync();
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
            _plcConnection?.Disconnect();
            _plcConnection?.Dispose();
            // Переход на страницу назад
            _router.GoTo<SettingsViewModel>();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _plcConnection?.Disconnect();
                _plcConnection?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
