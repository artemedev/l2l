using l2l_aggregator.Models;
using l2l_aggregator.Models.AggregationModels;
using l2l_aggregator.Services.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace l2l_aggregator.Services
{
    public class SessionService : INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static SessionService? _instance;
        public static SessionService Instance => _instance ??= new SessionService();

        private static IConfigurationFileService? _configService;

        public void SetConfigurationService(IConfigurationFileService configService)
        {
            _configService = configService;
        }

        private async void SaveSettingToFile(string key, string? value)
        {
            if (_configService != null && value != null)
            {
                await _configService.SetConfigValueAsync(key, value);
            }
        }

        private T SetAndSave<T>(ref T field, T value, string key)
        {
            field = value;
            SaveSettingToFile(key, value?.ToString());
            return field;
        }

        // ---------------- Device Settings ----------------
        private bool _enableVirtualKeyboard;
        public bool EnableVirtualKeyboard
        {
            get => _enableVirtualKeyboard;
            set
            {
                if (_enableVirtualKeyboard != value)
                {
                    _enableVirtualKeyboard = value;
                    OnPropertyChanged();
                    SaveSettingToFile("EnableVirtualKeyboard", value.ToString());
                }
            }
        }

        private string? _scannerPort;
        public string? ScannerPort
        {
            get => _scannerPort;
            set => SetAndSave(ref _scannerPort, value, "ScannerCOMPort");
        }

        private string? _cameraIP;
        public string? CameraIP
        {
            get => _cameraIP;
            set => SetAndSave(ref _cameraIP, value, "CameraIP");
        }

        private string? _cameraModel;
        public string? CameraModel
        {
            get => _cameraModel;
            set => SetAndSave(ref _cameraModel, value, "CameraModel");
        }

        private string? _printerIP;
        public string? PrinterIP
        {
            get => _printerIP;
            set => SetAndSave(ref _printerIP, value, "PrinterIP");
        }

        private string? _printerModel;
        public string? PrinterModel
        {
            get => _printerModel;
            set => SetAndSave(ref _printerModel, value, "PrinterModel");
        }

        private string? _controllerIP;
        public string? ControllerIP
        {
            get => _controllerIP;
            set => SetAndSave(ref _controllerIP, value, "ControllerIP");
        }

        private string? _scannerModel;
        public string? ScannerModel
        {
            get => _scannerModel;
            set => SetAndSave(ref _scannerModel, value, "ScannerModel");
        }

        // ---------------- Device Check Flags ----------------
        private bool _checkCamera;
        public bool CheckCamera
        {
            get => _checkCamera;
            set => SetAndSave(ref _checkCamera, value, "CheckCamera");
        }

        private bool _checkPrinter;
        public bool CheckPrinter
        {
            get => _checkPrinter;
            set => SetAndSave(ref _checkPrinter, value, "CheckPrinter");
        }

        private bool _checkController;
        public bool CheckController
        {
            get => _checkController;
            set => SetAndSave(ref _checkController, value, "CheckController");
        }

        private bool _checkScanner;
        public bool CheckScanner
        {
            get => _checkScanner;
            set => SetAndSave(ref _checkScanner, value, "CheckScanner");
        }

        // ---------------- Database Connection (Read-only) ----------------

        /// <summary>
        /// Отображаемая информация о базе данных
        /// </summary>

        // ---------------- Device Registration Info ----------------
        public string? DeviceId { get; set; }
        public string? DeviceName { get; set; }

        // ---------------- User Session Data ----------------
        public UserAuthResponse? User { get; set; }
        public bool IsAdmin { get; set; }

        // ---------------- Current Task Data ----------------
        public ArmJobRecord? SelectedTask { get; set; }
        public ArmJobInfoRecord? SelectedTaskInfo { get; set; }
        public ArmJobSsccRecord? SelectedTaskSscc { get; set; }

        /// <summary>
        /// Кэшированные данные SSCC, загруженные в TaskDetailsViewModel
        /// </summary>
        public ArmJobSsccResponse? CachedSsccResponse { get; set; }

        /// <summary>
        /// Кэшированные данные SGTIN, загруженные в TaskDetailsViewModel
        /// </summary>
        public ArmJobSgtinResponse? CachedSgtinResponse { get; set; }


        /// <summary>
        /// Инициализация настроек сессии
        /// </summary>
        public async Task InitializeAsync(IConfigurationFileService configService)
        {
            _configService = configService;


            // Загружаем настройки устройств
            PrinterIP = await _configService.GetConfigValueAsync("PrinterIP");
            PrinterModel = await _configService.GetConfigValueAsync("PrinterModel");
            ControllerIP = await _configService.GetConfigValueAsync("ControllerIP");
            CameraIP = await _configService.GetConfigValueAsync("CameraIP");
            CameraModel = await _configService.GetConfigValueAsync("CameraModel");
            ScannerPort = await _configService.GetConfigValueAsync("ScannerCOMPort");
            ScannerModel = await _configService.GetConfigValueAsync("ScannerModel");
            EnableVirtualKeyboard = bool.TryParse(await _configService.GetConfigValueAsync("EnableVirtualKeyboard"), out var vkParsed) && vkParsed;

            // Загружаем информацию об устройстве
            DeviceId = await _configService.GetConfigValueAsync("DeviceId");
            DeviceName = await _configService.GetConfigValueAsync("DeviceName");

            // Инициализируем флаги проверки
            CheckCamera = await LoadOrInitBool("CheckCamera", true);
            CheckPrinter = await LoadOrInitBool("CheckPrinter", true);
            CheckController = await LoadOrInitBool("CheckController", true);
            CheckScanner = await LoadOrInitBool("CheckScanner", true);
        }


        /// <summary>
        /// Загружает или инициализирует булево значение в конфигурации
        /// </summary>
        private async Task<bool> LoadOrInitBool(string key, bool defaultValue)
        {
            var value = await _configService.GetConfigValueAsync(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                await _configService.SetConfigValueAsync(key, defaultValue.ToString());
                return defaultValue;
            }

            return bool.TryParse(value, out var parsed) && parsed;
        }

 
        /// <summary>
        /// Очищает кэшированные данные агрегации
        /// </summary>
        public void ClearCachedAggregationData()
        {
            CachedSsccResponse = null;
            CachedSgtinResponse = null;
        }
        /// <summary>
        /// Сохраняет информацию об устройстве в настройках
        /// </summary>
        public async Task SaveDeviceInfoAsync(ArmDeviceRegistrationRequest request, ArmDeviceRegistrationResponse deviceRegistered)
        {
            try
            {
                // Сохраняем в базу данных
                if (_configService != null)
                {
                    // Сохраняем все свойства запроса регистрации
                    await _configService.SetConfigValueAsync("Device_NAME", request.NAME ?? string.Empty);
                    await _configService.SetConfigValueAsync("Device_MAC_ADDRESS", request.MAC_ADDRESS ?? string.Empty);
                    await _configService.SetConfigValueAsync("Device_SERIAL_NUMBER", request.SERIAL_NUMBER ?? string.Empty);
                    await _configService.SetConfigValueAsync("Device_NET_ADDRESS", request.NET_ADDRESS ?? string.Empty);
                    await _configService.SetConfigValueAsync("Device_KERNEL_VERSION", request.KERNEL_VERSION ?? string.Empty);
                    await _configService.SetConfigValueAsync("Device_HADWARE_VERSION", request.HARDWARE_VERSION ?? string.Empty);
                    await _configService.SetConfigValueAsync("Device_SOFTWARE_VERSION", request.SOFTWARE_VERSION ?? string.Empty);
                    await _configService.SetConfigValueAsync("Device_FIRMWARE_VERSION", request.FIRMWARE_VERSION ?? string.Empty);
                    await _configService.SetConfigValueAsync("Device_DEVICE_TYPE", request.DEVICE_TYPE ?? string.Empty);

                    // Сохраняем все свойства ответа регистрации
                    await _configService.SetConfigValueAsync("Device_DEVICEID", deviceRegistered.DEVICEID ?? string.Empty);
                    await _configService.SetConfigValueAsync("Device_DEVICE_NAME", deviceRegistered.DEVICE_NAME ?? string.Empty);
                    await _configService.SetConfigValueAsync("Device_LICENSE_DATA", deviceRegistered.LICENSE_DATA ?? string.Empty);
                    await _configService.SetConfigValueAsync("Device_SETTINGS_DATA", deviceRegistered.SETTINGS_DATA ?? string.Empty);

                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при сохранении информации о регистрации устройства: {ex.Message}", ex);
            }
        }
        //хранение кодов в сессии
        public HashSet<string> AllScannedDmCodes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public void ClearScannedCodes()
        {
            AllScannedDmCodes.Clear();
        }

        // Хранение кодов текущей коробки (по слоям)
        public List<string> CurrentBoxDmCodes { get; } = new List<string>();

        // Метод для очистки кодов текущей коробки
        public void ClearCurrentBoxCodes()
        {
            CurrentBoxDmCodes.Clear();
        }

        // Метод для добавления кодов слоя
        public void AddLayerCodes(IEnumerable<string> codes)
        {
            CurrentBoxDmCodes.AddRange(codes);
        }
    }
}