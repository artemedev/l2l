using l2l_aggregator.Models.Configuration;
using l2l_aggregator.Services.Notification.Interface;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace l2l_aggregator.Services.Configuration
{
    public class ConfigurationFileService : IConfigurationFileService
    {
        private readonly string _configFilePath;
        private readonly INotificationService _notificationService;
        private DeviceConfiguration? _cachedConfiguration;
        private readonly object _lock = new object();

        public ConfigurationFileService(INotificationService notificationService)
        {
            _notificationService = notificationService;

            _configFilePath = Path.Combine(AppContext.BaseDirectory, "device-config.json");
        }

        public async Task<DeviceConfiguration> LoadConfigurationAsync()
        {
            try
            {
                lock (_lock)
                {
                    if (_cachedConfiguration != null)
                        return _cachedConfiguration;
                }

                if (!File.Exists(_configFilePath))
                {
                    var defaultConfig = new DeviceConfiguration();
                    await SaveConfigurationAsync(defaultConfig);
                    return defaultConfig;
                }

                var jsonContent = await File.ReadAllTextAsync(_configFilePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                };

                var configuration = JsonSerializer.Deserialize<DeviceConfiguration>(jsonContent, options)
                                   ?? new DeviceConfiguration();

                lock (_lock)
                {
                    _cachedConfiguration = configuration;
                }

                return configuration;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка загрузки конфигурации: {ex.Message}", NotificationType.Error);
                return new DeviceConfiguration();
            }
        }

        public async Task SaveConfigurationAsync(DeviceConfiguration configuration)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                };

                var jsonContent = JsonSerializer.Serialize(configuration, options);
                await File.WriteAllTextAsync(_configFilePath, jsonContent);

                lock (_lock)
                {
                    _cachedConfiguration = configuration;
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка сохранения конфигурации: {ex.Message}", NotificationType.Error);
                throw;
            }
        }

        public async Task<string?> GetConfigValueAsync(string key)
        {
            var config = await LoadConfigurationAsync();

            return key switch
            {
                "PrinterIP" => config.Printer.IP,
                "PrinterModel" => config.Printer.Model,
                "CameraIP" => config.Camera.IP,
                "CameraModel" => config.Camera.Model,
                "ControllerIP" => config.Controller.IP,
                "ScannerCOMPort" => config.Scanner.Port,
                "ScannerModel" => config.Scanner.Model,
                "EnableVirtualKeyboard" => config.UI.EnableVirtualKeyboard.ToString(),
                "CheckCamera" => config.Validation.CheckCamera.ToString(),
                "CheckPrinter" => config.Validation.CheckPrinter.ToString(),
                "CheckController" => config.Validation.CheckController.ToString(),
                "CheckScanner" => config.Validation.CheckScanner.ToString(),
                "DeviceId" => config.Device.DeviceId,
                "DeviceName" => config.Device.DeviceName,
                "Device_NAME" => config.Device.Name,
                "Device_MAC_ADDRESS" => config.Device.MacAddress,
                "Device_SERIAL_NUMBER" => config.Device.SerialNumber,
                "Device_NET_ADDRESS" => config.Device.NetAddress,
                "Device_KERNEL_VERSION" => config.Device.KernelVersion,
                "Device_HADWARE_VERSION" => config.Device.HardwareVersion,
                "Device_SOFTWARE_VERSION" => config.Device.SoftwareVersion,
                "Device_FIRMWARE_VERSION" => config.Device.FirmwareVersion,
                "Device_DEVICE_TYPE" => config.Device.DeviceType,
                "Device_DEVICEID" => config.Device.DeviceId,
                "Device_DEVICE_NAME" => config.Device.DeviceName,
                _ => null
            };
        }

        public async Task SetConfigValueAsync(string key, string? value)
        {
            var config = await LoadConfigurationAsync();

            switch (key)
            {
                case "PrinterIP":
                    config.Printer.IP = value;
                    break;
                case "PrinterModel":
                    config.Printer.Model = value;
                    break;
                case "CameraIP":
                    config.Camera.IP = value;
                    break;
                case "CameraModel":
                    config.Camera.Model = value;
                    break;
                case "ControllerIP":
                    config.Controller.IP = value;
                    break;
                case "ScannerCOMPort":
                    config.Scanner.Port = value;
                    break;
                case "ScannerModel":
                    config.Scanner.Model = value;
                    break;
                case "EnableVirtualKeyboard":
                    config.UI.EnableVirtualKeyboard = bool.TryParse(value, out var vkResult) && vkResult;
                    break;
                case "CheckCamera":
                    config.Validation.CheckCamera = bool.TryParse(value, out var ccResult) && ccResult;
                    break;
                case "CheckPrinter":
                    config.Validation.CheckPrinter = bool.TryParse(value, out var cpResult) && cpResult;
                    break;
                case "CheckController":
                    config.Validation.CheckController = bool.TryParse(value, out var cctrlResult) && cctrlResult;
                    break;
                case "CheckScanner":
                    config.Validation.CheckScanner = bool.TryParse(value, out var csResult) && csResult;
                    break;
                case "DeviceId":
                    config.Device.DeviceId = value;
                    break;
                case "DeviceName":
                    config.Device.DeviceName = value;
                    break;
                case "Device_NAME":
                    config.Device.Name = value;
                    break;
                case "Device_MAC_ADDRESS":
                    config.Device.MacAddress = value;
                    break;
                case "Device_SERIAL_NUMBER":
                    config.Device.SerialNumber = value;
                    break;
                case "Device_NET_ADDRESS":
                    config.Device.NetAddress = value;
                    break;
                case "Device_KERNEL_VERSION":
                    config.Device.KernelVersion = value;
                    break;
                case "Device_HADWARE_VERSION":
                    config.Device.HardwareVersion = value;
                    break;
                case "Device_SOFTWARE_VERSION":
                    config.Device.SoftwareVersion = value;
                    break;
                case "Device_FIRMWARE_VERSION":
                    config.Device.FirmwareVersion = value;
                    break;
                case "Device_DEVICE_TYPE":
                    config.Device.DeviceType = value;
                    break;
                case "Device_DEVICEID":
                    config.Device.DeviceId = value;
                    break;
                case "Device_DEVICE_NAME":
                    config.Device.DeviceName = value;
                    break;
            }

            await SaveConfigurationAsync(config);
        }
    }
}