using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Logging;
using Avalonia.SimpleRouter;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DM_wraper_NS;
using l2l_aggregator.Models;
using l2l_aggregator.Services;
using l2l_aggregator.Services.ControllerService;
using l2l_aggregator.Services.Database;
using l2l_aggregator.Services.DmProcessing;
using l2l_aggregator.Services.Notification.Interface;
using l2l_aggregator.Services.Printing;
using l2l_aggregator.Services.ScannerService.Interfaces;
using l2l_aggregator.ViewModels.VisualElements;
using Microsoft.Extensions.Logging;
using Refit;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace l2l_aggregator.ViewModels
{
    public partial class SettingsViewModel : ViewModelBase
    {
        [ObservableProperty] private string _databaseUri;
        [ObservableProperty] private string _cameraIP;
        [ObservableProperty] private string _serverIP;
        [ObservableProperty] private string _printerIP;
        [ObservableProperty] private string _controllerIP;
        [ObservableProperty] private string _licenseNumber = "XXXX-XXXX-XXXX-XXXX";
        [ObservableProperty] private bool _checkForUpdates;
        [ObservableProperty] private string _infoMessage;
        [ObservableProperty] private bool _disableVirtualKeyboard;
        [ObservableProperty] private string _selectedCameraModel;
        [ObservableProperty] private ObservableCollection<string> _printerModels = new() { "Zebra" };
        [ObservableProperty] private string _selectedPrinterModel;
        [ObservableProperty] private ObservableCollection<string> _cameraModels = new() { "Basler" };
        [ObservableProperty] private ObservableCollection<string> _scannerModels = new() { "Honeywell" };
        [ObservableProperty] private string _selectedScannerModel;
        [ObservableProperty] private bool isConnectedCamera;
        [ObservableProperty] private CameraViewModel _camera = new();
        [ObservableProperty] private ObservableCollection<ScannerDevice> _availableScanners = new();
        [ObservableProperty] private ScannerDevice _selectedScanner;
        [ObservableProperty] private string _scannerCOMPort;
        [ObservableProperty] private bool _checkCameraBeforeAggregation = true;
        [ObservableProperty] private bool _checkPrinterBeforeAggregation = true;
        [ObservableProperty] private bool _checkControllerBeforeAggregation = true;
        [ObservableProperty] private bool _checkScannerBeforeAggregation = true;
        // Свойство для отслеживания состояния подключения контроллера
        [ObservableProperty] private bool _isControllerConnected = false;
        // Computed property для доступности кнопки настроек камеры
        public bool IsCameraSettingsEnabled => Camera.IsConnected && IsControllerConnected;

        private readonly HistoryRouter<ViewModelBase> _router;
        private readonly INotificationService _notificationService;
        private readonly DatabaseService _databaseService;
        private readonly SessionService _sessionService;
        private readonly IScannerPortResolver _scannerResolver;
        private readonly DmScanService _dmScanService;
        private readonly ConfigurationLoaderService _configLoader;
        private readonly PrintingService _printingService;

        private readonly ILogger<PcPlcConnectionService> _logger;

        // Свойство для отслеживания состояния принтера
        [ObservableProperty] private bool _isPrinterConnected = false;


        // Свойство для отслеживания состояния сканера
        [ObservableProperty] private bool _isScannerConnected = false;

        // Свойство для отображения результата тестового сканирования
        [ObservableProperty] private string _testScanResult = "";
        public SettingsViewModel(DatabaseService databaseService,
            HistoryRouter<ViewModelBase> router,
            INotificationService notificationService,
            SessionService sessionService,
            IScannerPortResolver scannerResolver,
            DmScanService dmScanService,
            ConfigurationLoaderService configLoader,
            PrintingService printingService,
            ILogger<PcPlcConnectionService> logger)
        {
            _configLoader = configLoader;
            _notificationService = notificationService;
            _databaseService = databaseService;
            _router = router;
            _sessionService = sessionService;
            _scannerResolver = scannerResolver;
            _dmScanService = dmScanService;
            _printingService = printingService;
            _logger = logger;
            _ = InitializeAsync();
        }
        // Метод для уведомления об изменении computed property
        partial void OnIsControllerConnectedChanged(bool value)
        {
            OnPropertyChanged(nameof(IsCameraSettingsEnabled));
        }
        // Обработчик изменения состояния камеры
        partial void OnCameraChanged(CameraViewModel value)
        {
            if (value != null)
            {
                value.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(CameraViewModel.IsConnected))
                    {
                        OnPropertyChanged(nameof(IsCameraSettingsEnabled));
                    }
                };
            }
        }

        private async Task InitializeAsync()
        {
            await LoadAvailableScannersAsync();
            await LoadSettingsAsync();
        }

        public async Task LoadAvailableScannersAsync()
        {
            AvailableScanners.Clear();

            var ports = _scannerResolver.GetHoneywellScannerPorts(); // список COM-портов

            foreach (var port in ports)
            {
                AvailableScanners.Add(new ScannerDevice { Id = port });
            }

            // Подгружаем сохранённый COM-порт и выбираем нужный сканер
            string savedPort = _sessionService.ScannerPort;
            SelectedScannerModel = _sessionService.ScannerModel;
            // Найти сканер в списке
            var foundScanner = AvailableScanners.FirstOrDefault(x => x.Id == savedPort);

            if (foundScanner != null)
            {
                SelectedScanner = foundScanner;
                //session.ScannerPort = SelectedScanner?.Id;
                //session.ScannerModel = SelectedScannerModel;
            }
            else
            {
                // Очистка, если не найден
                SelectedScanner = null;
                SelectedScannerModel = null;

                // Очистка в сессии
                _sessionService.ScannerPort = null;
                _sessionService.ScannerModel = null;
            }
        }

        private async Task LoadSettingsAsync()
        {
            //var (camera, disableVK) = await _configLoader.LoadSettingsToSessionAsync();

            Camera = new CameraViewModel
            {
                CameraIP = _sessionService.CameraIP,
                SelectedCameraModel = _sessionService.CameraModel
            };
            DisableVirtualKeyboard = _sessionService.DisableVirtualKeyboard;
            //DatabaseUri = _sessionService.DatabaseUri;
            PrinterIP = _sessionService.PrinterIP;
            SelectedPrinterModel = _sessionService.PrinterModel;
            ControllerIP = _sessionService.ControllerIP;
            SelectedCameraModel = _sessionService.CameraModel;
            CheckCameraBeforeAggregation = _sessionService.CheckCamera;
            CheckPrinterBeforeAggregation = _sessionService.CheckPrinter;
            CheckControllerBeforeAggregation = _sessionService.CheckController;
            CheckScannerBeforeAggregation = _sessionService.CheckScanner;
            SelectedScannerModel = _sessionService.ScannerModel;
        }

        [RelayCommand]
        private async Task ToggleDisableVirtualKeyboardAsync()
        {
            // await _databaseService.Config.SetConfigValueAsync("DisableVirtualKeyboard", DisableVirtualKeyboard.ToString());
            _sessionService.DisableVirtualKeyboard = DisableVirtualKeyboard;
            InfoMessage = "Настройка клавиатуры сохранена.";
            _notificationService.ShowMessage(InfoMessage);
        }
        partial void OnCheckControllerBeforeAggregationChanged(bool value)
        {
            //_ = _databaseService.Config.SetConfigValueAsync("CheckController", value.ToString());
            _sessionService.CheckController = value;
        }
        partial void OnCheckCameraBeforeAggregationChanged(bool value)
        {
            //_ = _databaseService.Config.SetConfigValueAsync("CheckCamera", value.ToString());
            _sessionService.CheckCamera = value;
        }
        partial void OnCheckPrinterBeforeAggregationChanged(bool value)
        {
            //_ = _databaseService.Config.SetConfigValueAsync("CheckPrinter", value.ToString());
            _sessionService.CheckPrinter = value;
        }
        partial void OnCheckScannerBeforeAggregationChanged(bool value)
        {
            // _ = _databaseService.Config.SetConfigValueAsync("CheckScanner", value.ToString());
            _sessionService.CheckScanner = value;
        }

        [RelayCommand]
        private void OpenCameraSettings()
        {
            // Передаём CameraIP или другие параметры
            _router.GoTo<CameraSettingsViewModel>();
        }
        private void LoadCameras()
        {

        }

        //[RelayCommand]
        //public async Task CheckAndSaveUriAsync()
        //{
        //    if (!string.IsNullOrWhiteSpace(DatabaseUri))
        //    {
        //        try
        //        {
        //            var request = new ArmDeviceRegistrationRequest
        //            {
        //                NAME = "test",
        //                MAC_ADDRESS = "test",
        //                SERIAL_NUMBER = "test",
        //                NET_ADDRESS = "test",
        //                KERNEL_VERSION = "test",
        //                HADWARE_VERSION = "test",
        //                SOFTWARE_VERSION = "test",
        //                FIRMWARE_VERSION = "test",
        //                DEVICE_TYPE = "test"
        //            };
        //            var handler = new HttpClientHandler
        //            {
        //                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        //            };
        //            var httpClient = new HttpClient(handler)
        //            {
        //                BaseAddress = new Uri(DatabaseUri)
        //            };
        //            httpClient.DefaultRequestHeaders.Add("MTDApikey", "e2fbe0f4fbe2e0fbf4ecf7f1ece5e8f020fbe2e0eff0eae5f020edeeede320fceee8ec343533343536333435212121de2cc1de"); // если нужно

        //            var authClient = RestService.For<IAuthApi>(httpClient);
        //            var response = await authClient.RegisterDevice(request);

        //            await _databaseService.RegistrationDevice.SaveRegistrationAsync(response);

        //            //await _databaseService.Config.SetConfigValueAsync("ServerUri", ServerUri);
        //            //_sessionService.DatabaseUri = DatabaseUri;
        //            InfoMessage = "URI успешно сохранён!";
        //            _notificationService.ShowMessage(InfoMessage);

        //        }
        //        catch (Exception ex)
        //        {
        //            InfoMessage = $"Ошибка: {ex.Message}";
        //            _notificationService.ShowMessage($"Ошибка: {ex.Message}");

        //        }
        //    }


        //}



        //[RelayCommand]
        //private void TestServerConnection() { /* ... */ }







        [RelayCommand]
        private async void SaveSettings()
        {
            //await _databaseService.Config.SetConfigValueAsync("DisableVirtualKeyboard", DisableVirtualKeyboard.ToString());
            _sessionService.DisableVirtualKeyboard = DisableVirtualKeyboard;
            InfoMessage = "Настройки успешно сохранены!";
            _notificationService.ShowMessage(InfoMessage);

        }


        [RelayCommand]
        private void GetArchive() { /* ... */ }

        //Контроллер - проверка соединения
        [RelayCommand]
        public async Task TestControllerConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(ControllerIP))
            {
                InfoMessage = "Введите IP контроллера!";
                _notificationService.ShowMessage(InfoMessage);
                return;
            }

            try
            {

                // Создать службу подключения PLC
                var plcService = new PcPlcConnectionService(_logger); // Внедрение ILogger
                bool connected = await plcService.ConnectAsync(ControllerIP);
                if (!connected)
                {
                    InfoMessage = "Не удалось подключиться к контроллеру!";
                    _notificationService.ShowMessage(InfoMessage);
                    IsControllerConnected = false;
                    return;
                }

                // Одиночный тест пинг-понга
                bool pingPongResult = await plcService.TestConnectionAsync();

                if (pingPongResult)
                {
                    // Сохранение настроек, если подключение успешно
                    _sessionService.ControllerIP = ControllerIP;
                    _sessionService.CheckController = CheckControllerBeforeAggregation;
                    IsControllerConnected = true;
                    InfoMessage = "Контроллер успешно проверен и сохранён!";
                    _notificationService.ShowMessage(InfoMessage);
                }
                else
                {
                    IsControllerConnected = false;
                    InfoMessage = "Контроллер не прошёл проверку ping-pong!";
                    _notificationService.ShowMessage(InfoMessage);
                }

                // Закрыть подключение 
                plcService.Disconnect();
                plcService.Dispose();
            }
            catch (Exception ex)
            {
                IsControllerConnected = false;
                InfoMessage = $"Ошибка: {ex.Message}";
                _notificationService.ShowMessage(InfoMessage);
            }
        }
        //public async Task TestControllerConnectionAsync()
        //{
        //    if (string.IsNullOrWhiteSpace(ControllerIP))
        //    {
        //        InfoMessage = "Введите IP контроллера!";
        //        _notificationService.ShowMessage(InfoMessage);
        //        return;
        //    }

        //    try
        //    {
        //        await Task.Delay(300);

        //        //await _databaseService.Config.SetConfigValueAsync("ControllerIP", ControllerIP);
        //        //await _databaseService.Config.SetConfigValueAsync("CheckController", CheckControllerBeforeAggregation.ToString());

        //        _sessionService.ControllerIP = ControllerIP;
        //        _sessionService.CheckController = CheckControllerBeforeAggregation;

        //        InfoMessage = "Контроллер успешно сохранён!";
        //        _notificationService.ShowMessage(InfoMessage);
        //    }
        //    catch (Exception ex)
        //    {
        //        InfoMessage = $"Ошибка: {ex.Message}";
        //        _notificationService.ShowMessage(InfoMessage);
        //    }
        //}

        //Камера - проверка соединения
        [RelayCommand]
        public async Task TestCameraConnectionAsync(CameraViewModel camera)
        {
            if (string.IsNullOrWhiteSpace(camera.CameraIP))
            {
                InfoMessage = "Введите IP камеры!";
                _notificationService.ShowMessage(InfoMessage);
                return;
            }

            try
            {
                // Настроить параметры камеры для библиотеки
                var recognParams = new recogn_params
                {
                    CamInterfaces = "File", // или USB, File, в зависимости от вашей конфигурации
                    cameraName = camera.CameraIP,
                    _Preset = new camera_preset(camera.SelectedCameraModel),
                    softwareTrigger = true,
                    hardwareTrigger = false,
                    DMRecogn = false
                };

                // Установить параметры в обёртку
                bool success = _dmScanService.ConfigureParams(recognParams);
                if (!success)
                    _notificationService.ShowMessage("Не удалось применить параметры камеры");

                camera.IsConnected = true;

                //await _databaseService.Config.SetConfigValueAsync("CameraIP", camera.CameraIP);
                //await _databaseService.Config.SetConfigValueAsync("CameraModel", camera.SelectedCameraModel);
                //await _databaseService.Config.SetConfigValueAsync("CheckCamera", CheckCameraBeforeAggregation.ToString());

                _sessionService.CameraIP = camera.CameraIP;
                _sessionService.CameraModel = camera.SelectedCameraModel;
                _sessionService.CheckCamera = CheckCameraBeforeAggregation;

                InfoMessage = $"Камера {camera.CameraIP} сохранена!";
                _notificationService.ShowMessage(InfoMessage);
            }
            catch (Exception ex)
            {
                camera.IsConnected = false;
                InfoMessage = $"Ошибка: {ex.Message}";
                _notificationService.ShowMessage(InfoMessage);
            }
        }

        //Принтер - проверка соединения
        [RelayCommand]
        public async Task TestPrinterConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(PrinterIP) && string.IsNullOrWhiteSpace(SelectedPrinterModel))
            {
                InfoMessage = "Введите IP принтера или выведите модель!";
                _notificationService.ShowMessage(InfoMessage);
                return;
            }

            try
            {
                _printingService.CheckConnectPrinter(PrinterIP, SelectedPrinterModel);

                // сохраняем в БД
                //await _databaseService.Config.SetConfigValueAsync("PrinterIP", PrinterIP);
                //await _databaseService.Config.SetConfigValueAsync("PrinterModel", SelectedPrinterModel);
                //await _databaseService.Config.SetConfigValueAsync("CheckPrinter", CheckPrinterBeforeAggregation.ToString());

                _sessionService.PrinterIP = PrinterIP;
                _sessionService.PrinterModel = SelectedPrinterModel;
                _sessionService.CheckPrinter = CheckPrinterBeforeAggregation;
                IsPrinterConnected = true;
                InfoMessage = "Принтер успешно сохранён и проверен!";
                _notificationService.ShowMessage(InfoMessage);
            }
            catch (Exception ex)
            {
                InfoMessage = $"Ошибка проверки принтера: {ex.Message}";
                IsPrinterConnected = false;
                _notificationService.ShowMessage(InfoMessage);
                return;

            }
        }

        //Сканер - проверка соединения
        [RelayCommand]
        public async Task TestScannerConnectionAsync()
        {
            if (SelectedScanner == null)
            {
                InfoMessage = "Сканер не выбран!";
                _notificationService.ShowMessage(InfoMessage);
                return;
            }

            try
            {
                if (SelectedScannerModel == "Honeywell")
                {
                    //await _databaseService.Config.SetConfigValueAsync("ScannerCOMPort", SelectedScanner.Id);
                    //await _databaseService.Config.SetConfigValueAsync("ScannerModel", SelectedScannerModel);
                    //await _databaseService.Config.SetConfigValueAsync("CheckScanner", CheckScannerBeforeAggregation.ToString());

                    _sessionService.ScannerPort = SelectedScanner.Id;
                    _sessionService.CheckScanner = CheckScannerBeforeAggregation;
                    _sessionService.ScannerModel = SelectedScannerModel;
                    // Установить состояние подключения
                    IsScannerConnected = true;
                    //var mainWindow = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                    //? desktop.MainWindow
                    //: null;
                    //if (mainWindow?.DataContext is MainWindowViewModel mainVM)
                    //{
                    //    await mainVM.ReinitializeScannerAsync();
                    //}
                    InfoMessage = $"Сканер '{SelectedScanner.Id}' сохранён!";
                    _notificationService.ShowMessage(InfoMessage);
                }
                else
                {
                    IsScannerConnected = false;
                    InfoMessage = $"Модель сканера '{SelectedScannerModel}' пока не поддерживается.";
                    _notificationService.ShowMessage(InfoMessage);
                }
            }
            catch (Exception ex)
            {
                IsScannerConnected = false;
                InfoMessage = $"Ошибка: {ex.Message}";
                _notificationService.ShowMessage(InfoMessage);
            }
        }




        //не нужное в данном проекте, возможно пригодится
        [RelayCommand]
        public void AddCamera()
        {
            //Cameras.Add(new CameraViewModel());
        }

        [RelayCommand]
        public void RemoveCamera(CameraViewModel camera)
        {
            //if (Cameras.Count > 1) // Ensure at least one camera remains
            //{
            //    Cameras.Remove(camera);
            //}
            //else
            //{
            //    InfoMessage = "Как минимум одна камера должна быть настроена";
            //    _notificationService.ShowMessage(InfoMessage);
            //}
        }

        // Метод для тестовой печати
        [RelayCommand]
        public async Task TestPrintAsync()
        {
            if (!IsPrinterConnected)
            {
                InfoMessage = "Сначала проверьте подключение принтера!";
                _notificationService.ShowMessage(InfoMessage);
                return;
            }

            try
            {
                _printingService.PrintTestLabel();
                InfoMessage = "Тестовая печать выполнена успешно!";
                _notificationService.ShowMessage(InfoMessage);
            }
            catch (Exception ex)
            {
                InfoMessage = $"Ошибка тестовой печати: {ex.Message}";
                _notificationService.ShowMessage(InfoMessage);
            }
        }
        // Методы для сброса состояния принтера при изменении IP или модели
        partial void OnPrinterIPChanged(string value)
        {
            IsPrinterConnected = false;
        }

        partial void OnSelectedPrinterModelChanged(string value)
        {
            IsPrinterConnected = false;
        }

        [RelayCommand]
        public async Task TestScannerAsync()
        {
            if (!IsScannerConnected)
            {
                InfoMessage = "Сначала проверьте подключение сканера!";
                _notificationService.ShowMessage(InfoMessage);
                return;
            }

            try
            {
                TestScanResult = "Ожидание сканирования...";
                _notificationService.ShowMessage("Отсканируйте штрих-код для проверки", NotificationType.Info);

                // Здесь можно добавить таймер для сброса ожидания через определенное время
                await Task.Delay(100); // Небольшая задержка для UI

                InfoMessage = "Готов к тестовому сканированию. Отсканируйте штрих-код.";
            }
            catch (Exception ex)
            {
                InfoMessage = $"Ошибка подготовки к тестовому сканированию: {ex.Message}";
                _notificationService.ShowMessage(InfoMessage);
            }
        }
        // Метод для обработки результата тестового сканирования (вызывается из MainWindowViewModel)
        public void HandleTestScanResult(string barcode)
        {
            try
            {
                TestScanResult = $"Отсканировано: {barcode}";
                InfoMessage = $"Тестовое сканирование успешно! Код: {barcode}";
                _notificationService.ShowMessage(InfoMessage, NotificationType.Success);
            }
            catch (Exception ex)
            {
                InfoMessage = $"Ошибка обработки тестового сканирования: {ex.Message}";
                _notificationService.ShowMessage(InfoMessage, NotificationType.Error);
            }
        }
        // Методы для сброса состояния сканера при изменении порта или модели
        partial void OnSelectedScannerChanged(ScannerDevice value)
        {
            IsScannerConnected = false;
            TestScanResult = "";
        }

        partial void OnSelectedScannerModelChanged(string value)
        {
            IsScannerConnected = false;
            TestScanResult = "";
        }
    }
}
