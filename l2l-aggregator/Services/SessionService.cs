using l2l_aggregator.Models;
using l2l_aggregator.Models.AggregationModels;
using l2l_aggregator.Services.Database;
using l2l_aggregator.Services.Database.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace l2l_aggregator.Services
{
    public class SessionService
    {
        private static SessionService? _instance;
        public static SessionService Instance => _instance ??= new SessionService();

        private static IConfigRepository? _configRepo;

        public void SetConfigRepository(IConfigRepository config)
        {
            _configRepo = config;
        }

        private async void SaveSettingToDb(string key, string? value)
        {
            if (_configRepo != null && value != null)
            {
                await _configRepo.SetConfigValueAsync(key, value);
            }
        }

        private T SetAndSave<T>(ref T field, T value, string key)
        {
            field = value;
            SaveSettingToDb(key, value?.ToString());
            return field;
        }

        // ---------------- Device Settings ----------------
        private bool _disableVirtualKeyboard;
        public bool DisableVirtualKeyboard
        {
            get => _disableVirtualKeyboard;
            set => SetAndSave(ref _disableVirtualKeyboard, value, "DisableVirtualKeyboard");
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
        /// Статичный адрес базы данных (только для чтения)
        /// </summary>
        public string DatabaseUri { get; set; }

        /// <summary>
        /// Отображаемая информация о базе данных
        /// </summary>
        public string DatabaseDisplayInfo => "172.16.3.237:3050";

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
        public ArmJobSgtinRecord? SelectedTaskSgtin { get; set; }
        // ---------------- Cached Data for Aggregation ----------------
        /// <summary>
        /// Кэшированные данные SSCC, загруженные в TaskDetailsViewModel
        /// </summary>
        public ArmJobSsccResponse? CachedSsccResponse { get; set; }

        /// <summary>
        /// Кэшированные данные SGTIN, загруженные в TaskDetailsViewModel
        /// </summary>
        public ArmJobSgtinResponse? CachedSgtinResponse { get; set; }

        // ---------------- Aggregation State ----------------
        public AggregationState? AggregationState { get; set; }

        /// <summary>
        /// Проверяет, есть ли незавершенная агрегация
        /// </summary>
        public bool HasUnfinishedAggregation => SelectedTaskInfo != null &&
            AggregationState != null &&
            !string.IsNullOrWhiteSpace(AggregationState.TemplateJson) &&
            !string.IsNullOrWhiteSpace(AggregationState.ProgressJson);

        // ---------------- Session Management ----------------
        public long? CurrentSessionId { get; set; }

        /// <summary>
        /// Инициализация настроек сессии
        /// </summary>
        public async Task InitializeAsync(DatabaseService db)
        {
            SetConfigRepository(db.Config);
            await InitializeCheckFlagsAsync(db);

            var config = db.Config;

            // Загружаем настройки устройств
            PrinterIP = await config.GetConfigValueAsync("PrinterIP");
            PrinterModel = await config.GetConfigValueAsync("PrinterModel");
            ControllerIP = await config.GetConfigValueAsync("ControllerIP");
            CameraIP = await config.GetConfigValueAsync("CameraIP");
            CameraModel = await config.GetConfigValueAsync("CameraModel");
            ScannerPort = await config.GetConfigValueAsync("ScannerCOMPort");
            ScannerModel = await config.GetConfigValueAsync("ScannerModel");
            DisableVirtualKeyboard = bool.TryParse(await config.GetConfigValueAsync("DisableVirtualKeyboard"), out var vkParsed) && vkParsed;

            // Загружаем информацию об устройстве
            DeviceId = await config.GetConfigValueAsync("DeviceId");
            DeviceName = await config.GetConfigValueAsync("DeviceName");

            //// Загружаем последнего авторизованного пользователя
            //var users = await db.UserAuth.GetUserAuthAsync();
            //if (users != null && users.Count > 0)
            //{
            //    User = users.FirstOrDefault();

            //    // Если есть пользователь, загружаем его состояние агрегации
            //    if (User != null)
            //    {
            //        await LoadAggregationStateAsync(db);
            //    }
            //}
        }

        /// <summary>
        /// Инициализация флагов проверки устройств
        /// </summary>
        private async Task InitializeCheckFlagsAsync(DatabaseService db)
        {
            var config = db.Config;

            CheckCamera = await LoadOrInitBool(config, "CheckCamera", true);
            CheckPrinter = await LoadOrInitBool(config, "CheckPrinter", true);
            CheckController = await LoadOrInitBool(config, "CheckController", true);
            CheckScanner = await LoadOrInitBool(config, "CheckScanner", true);
        }

        /// <summary>
        /// Загружает или инициализирует булево значение в конфигурации
        /// </summary>
        private async Task<bool> LoadOrInitBool(IConfigRepository config, string key, bool defaultValue)
        {
            var value = await config.GetConfigValueAsync(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                await config.SetConfigValueAsync(key, defaultValue.ToString());
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
                if (_configRepo != null)
                {
                    // Сохраняем все свойства запроса регистрации
                    await _configRepo.SetConfigValueAsync("Device_NAME", request.NAME ?? string.Empty);
                    await _configRepo.SetConfigValueAsync("Device_MAC_ADDRESS", request.MAC_ADDRESS ?? string.Empty);
                    await _configRepo.SetConfigValueAsync("Device_SERIAL_NUMBER", request.SERIAL_NUMBER ?? string.Empty);
                    await _configRepo.SetConfigValueAsync("Device_NET_ADDRESS", request.NET_ADDRESS ?? string.Empty);
                    await _configRepo.SetConfigValueAsync("Device_KERNEL_VERSION", request.KERNEL_VERSION ?? string.Empty);
                    await _configRepo.SetConfigValueAsync("Device_HADWARE_VERSION", request.HARDWARE_VERSION ?? string.Empty);
                    await _configRepo.SetConfigValueAsync("Device_SOFTWARE_VERSION", request.SOFTWARE_VERSION ?? string.Empty);
                    await _configRepo.SetConfigValueAsync("Device_FIRMWARE_VERSION", request.FIRMWARE_VERSION ?? string.Empty);
                    await _configRepo.SetConfigValueAsync("Device_DEVICE_TYPE", request.DEVICE_TYPE ?? string.Empty);

                    // Сохраняем все свойства ответа регистрации
                    await _configRepo.SetConfigValueAsync("Device_DEVICEID", deviceRegistered.DEVICEID ?? string.Empty);
                    await _configRepo.SetConfigValueAsync("Device_DEVICE_NAME", deviceRegistered.DEVICE_NAME ?? string.Empty);
                    await _configRepo.SetConfigValueAsync("Device_LICENSE_DATA", deviceRegistered.LICENSE_DATA ?? string.Empty);
                    await _configRepo.SetConfigValueAsync("Device_SETTINGS_DATA", deviceRegistered.SETTINGS_DATA ?? string.Empty);

                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при сохранении информации о регистрации устройства: {ex.Message}", ex);
            }
        }
        /// <summary>
        /// Получает сохраненные данные устройства из конфигурации
        /// </summary>
        public async Task<ArmDeviceRegistrationRequest?> GetSavedDeviceDataAsync()
        {
            if (_configRepo == null) return null;

            try
            {
                return new ArmDeviceRegistrationRequest
                {
                    NAME = await _configRepo.GetConfigValueAsync("Device_NAME"),
                    MAC_ADDRESS = await _configRepo.GetConfigValueAsync("Device_MAC_ADDRESS"),
                    SERIAL_NUMBER = await _configRepo.GetConfigValueAsync("Device_SERIAL_NUMBER"),
                    NET_ADDRESS = await _configRepo.GetConfigValueAsync("Device_NET_ADDRESS"),
                    KERNEL_VERSION = await _configRepo.GetConfigValueAsync("Device_KERNEL_VERSION"),
                    HARDWARE_VERSION = await _configRepo.GetConfigValueAsync("Device_HADWARE_VERSION"),
                    SOFTWARE_VERSION = await _configRepo.GetConfigValueAsync("Device_SOFTWARE_VERSION"),
                    FIRMWARE_VERSION = await _configRepo.GetConfigValueAsync("Device_FIRMWARE_VERSION"),
                    DEVICE_TYPE = await _configRepo.GetConfigValueAsync("Device_DEVICE_TYPE")
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при получении сохраненных данных устройства: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Проверяет, изменились ли данные устройства по сравнению с сохраненными
        /// </summary>
        public async Task<bool> HasDeviceDataChangedAsync(ArmDeviceRegistrationRequest currentRequest)
        {
            if (_configRepo == null) return true; // Если нет доступа к конфигурации, считаем что данные изменились

            try
            {
                var savedData = await GetSavedDeviceDataAsync();
                if (savedData == null) return true; // Если нет сохраненных данных, требуется регистрация

                return savedData.NAME != currentRequest.NAME ||
                       savedData.MAC_ADDRESS != currentRequest.MAC_ADDRESS ||
                       savedData.SERIAL_NUMBER != currentRequest.SERIAL_NUMBER ||
                       savedData.NET_ADDRESS != currentRequest.NET_ADDRESS ||
                       savedData.KERNEL_VERSION != currentRequest.KERNEL_VERSION ||
                       savedData.HARDWARE_VERSION != currentRequest.HARDWARE_VERSION ||
                       savedData.SOFTWARE_VERSION != currentRequest.SOFTWARE_VERSION ||
                       savedData.FIRMWARE_VERSION != currentRequest.FIRMWARE_VERSION ||
                       savedData.DEVICE_TYPE != currentRequest.DEVICE_TYPE;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при проверке изменений данных устройства: {ex.Message}", ex);
            }
        }
        //хранение кодов в сессии
        public HashSet<string> AllScannedDmCodes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public void ClearScannedCodes()
        {
            AllScannedDmCodes.Clear();
        }
    }
}