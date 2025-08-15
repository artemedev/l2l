using Avalonia.SimpleRouter;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DM_wraper_NS;
using l2l_aggregator.Models;
using l2l_aggregator.Services;
using l2l_aggregator.Services.Configuration;
using l2l_aggregator.Services.ControllerService;
using l2l_aggregator.Services.Database;
using l2l_aggregator.Services.DmProcessing;
using l2l_aggregator.Services.Notification.Interface;
using l2l_aggregator.Services.Printing;
using l2l_aggregator.Services.ScannerService.Interfaces;
using l2l_aggregator.ViewModels.VisualElements;
using System;
using System.Collections.ObjectModel;
using System.Linq;
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
        [ObservableProperty] private bool _enableVirtualKeyboard;
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
        private readonly SessionService _sessionService;
        private readonly IScannerPortResolver _scannerResolver;
        private readonly DmScanService _dmScanService;
        private readonly PrintingService _printingService;

        // Свойство для отслеживания состояния принтера
        [ObservableProperty] private bool _isPrinterConnected = false;


        // Свойство для отслеживания состояния сканера
        [ObservableProperty] private bool _isScannerConnected = false;

        // Свойство для отображения результата тестового сканирования
        [ObservableProperty] private string _testScanResult = "";

        private PcPlcConnectionService _plcConnectionService;

        public SettingsViewModel(HistoryRouter<ViewModelBase> router,
            INotificationService notificationService,
            SessionService sessionService,
            IScannerPortResolver scannerResolver,
            DmScanService dmScanService,
            PrintingService printingService,
            PcPlcConnectionService plcConnectionService)
        {
            _notificationService = notificationService;
            _router = router;
            _sessionService = sessionService;
            _scannerResolver = scannerResolver;
            _dmScanService = dmScanService;
            _printingService = printingService;
            _plcConnectionService = plcConnectionService;
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
            Camera = new CameraViewModel
            {
                CameraIP = _sessionService.CameraIP,
                SelectedCameraModel = _sessionService.CameraModel
            };
            EnableVirtualKeyboard = _sessionService.EnableVirtualKeyboard;
            PrinterIP = _sessionService.PrinterIP;
            SelectedPrinterModel = _sessionService.PrinterModel;
            ControllerIP = _sessionService.ControllerIP;
            SelectedCameraModel = _sessionService.CameraModel;
            CheckCameraBeforeAggregation = _sessionService.CheckCamera;
            CheckPrinterBeforeAggregation = _sessionService.CheckPrinter;
            CheckControllerBeforeAggregation = _sessionService.CheckController;
            CheckScannerBeforeAggregation = _sessionService.CheckScanner;
            SelectedScannerModel = _sessionService.ScannerModel;
            // Обновляем состояние подключений после загрузки настроек
            UpdatePrinterConnectionState();
            UpdateScannerConnectionState();
        }

        [RelayCommand]
        private async Task ToggleEnableVirtualKeyboardAsync()
        {
            _sessionService.EnableVirtualKeyboard = EnableVirtualKeyboard;
            InfoMessage = "Настройка клавиатуры сохранена.";
            _notificationService.ShowMessage(InfoMessage);
        }
        partial void OnCheckControllerBeforeAggregationChanged(bool value)
        {
            _sessionService.CheckController = value;
        }
        partial void OnCheckCameraBeforeAggregationChanged(bool value)
        {
            _sessionService.CheckCamera = value;
        }
        partial void OnCheckPrinterBeforeAggregationChanged(bool value)
        {
            _sessionService.CheckPrinter = value;
        }
        partial void OnCheckScannerBeforeAggregationChanged(bool value)
        {
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


        [RelayCommand]
        private async void SaveSettings()
        {
            _sessionService.EnableVirtualKeyboard = EnableVirtualKeyboard;
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
                bool connected = await _plcConnectionService.ConnectAsync(ControllerIP);
                if (!connected)
                {
                    InfoMessage = "Не удалось подключиться к контроллеру!";
                    _notificationService.ShowMessage(InfoMessage);
                    IsControllerConnected = false;
                    return;
                }

                // Одиночный тест пинг-понга
                bool pingPongResult = await _plcConnectionService.TestConnectionAsync();

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
                _plcConnectionService.Disconnect();
                _plcConnectionService.Dispose();
            }
            catch (Exception ex)
            {
                IsControllerConnected = false;
                InfoMessage = $"Ошибка: {ex.Message}";
                _notificationService.ShowMessage(InfoMessage);
            }
            finally
            {
                // Безопасное отключение в блоке finally
                try
                {
                    if (_plcConnectionService.IsConnected)
                    {
                        // Сначала отключаем управление соединением, пока соединение активно
                        await _plcConnectionService.EnableConnectionControlAsync(false);

                        // Затем отключаем соединение
                        _plcConnectionService.Disconnect();
                    }
                }
                catch (Exception ex)
                {
                    // Логируем ошибку отключения, но не показываем пользователю
                    // так как основная операция уже выполнена
                    System.Diagnostics.Debug.WriteLine($"Warning during disconnect: {ex.Message}");
                }
            }
        }


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
            if (string.IsNullOrWhiteSpace(camera.SelectedCameraModel))
            {
                InfoMessage = "Введите модель камеры!";
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
                await _printingService.CheckConnectPrinterAsync(PrinterIP, SelectedPrinterModel);

                // сохраняем в БД
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
                    _sessionService.ScannerPort = SelectedScanner.Id;
                    _sessionService.CheckScanner = CheckScannerBeforeAggregation;
                    _sessionService.ScannerModel = SelectedScannerModel;
                    // Установить состояние подключения
                    IsScannerConnected = true;
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
        }

        [RelayCommand]
        public void RemoveCamera(CameraViewModel camera)
        {
          
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
                await _printingService.PrintTestLabel();
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
            UpdatePrinterConnectionState();
        }

        partial void OnSelectedPrinterModelChanged(string value)
        {
            UpdatePrinterConnectionState();
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

                // Таймер для сброса ожидания через определенное время
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
            //IsScannerConnected = false;
            UpdateScannerConnectionState();
            TestScanResult = "";
        }

        partial void OnSelectedScannerModelChanged(string value)
        {
            UpdateScannerConnectionState();
            TestScanResult = "";
        }

        // Метод для обновления состояния сканера
        private void UpdateScannerConnectionState()
        {
            IsScannerConnected = SelectedScanner != null && !string.IsNullOrWhiteSpace(SelectedScannerModel);
        }

        // Метод для обновления состояния принтера  
        private void UpdatePrinterConnectionState()
        {
            IsPrinterConnected = !string.IsNullOrWhiteSpace(PrinterIP) && !string.IsNullOrWhiteSpace(SelectedPrinterModel);
        }


    }
}
