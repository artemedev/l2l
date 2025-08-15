using l2l_aggregator.Models.Configuration;
using l2l_aggregator.Services.Notification.Interface;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;


namespace l2l_aggregator.Services.Configuration
{
    public class ConfigurationFileService : IConfigurationFileService
    {
        private readonly string _configFilePath;
        private readonly INotificationService _notificationService;
        private DeviceConfiguration? _cachedConfiguration;
        private readonly object _lock = new object();

        // Семафор для синхронизации операций с файлом
        private readonly SemaphoreSlim _fileSemaphore = new SemaphoreSlim(1, 1);

        public ConfigurationFileService(INotificationService notificationService)
        {
            _notificationService = notificationService;
            _configFilePath = Path.Combine(AppContext.BaseDirectory, "device-config.json");
        }

        public async Task<DeviceConfiguration> LoadConfigurationAsync()
        {
            await _fileSemaphore.WaitAsync();
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
                    await SaveConfigurationInternalAsync(defaultConfig);
                    return defaultConfig;
                }

                var jsonContent = await ReadFileWithRetryAsync(_configFilePath);
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
            finally
            {
                _fileSemaphore.Release();
            }
        }

        public async Task SaveConfigurationAsync(DeviceConfiguration configuration)
        {
            await _fileSemaphore.WaitAsync();
            try
            {
                await SaveConfigurationInternalAsync(configuration);

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
            finally
            {
                _fileSemaphore.Release();
            }
        }

        private async Task SaveConfigurationInternalAsync(DeviceConfiguration configuration)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };

            var jsonContent = JsonSerializer.Serialize(configuration, options);
            await WriteFileWithRetryAsync(_configFilePath, jsonContent);
        }

        private async Task<string> ReadFileWithRetryAsync(string filePath, int maxRetries = 3)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    // Используем FileShare.Read для возможности одновременного чтения
                    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var reader = new StreamReader(fileStream);
                    return await reader.ReadToEndAsync();
                }
                catch (IOException ex) when (attempt < maxRetries - 1)
                {
                    // Если файл заблокирован, ждем и повторяем попытку
                    await Task.Delay(100 * (attempt + 1)); // Увеличиваем задержку с каждой попыткой
                    continue;
                }
                catch (Exception)
                {
                    throw;
                }
            }

            throw new IOException($"Не удалось прочитать файл {filePath} после {maxRetries} попыток");
        }

        private async Task WriteFileWithRetryAsync(string filePath, string content, int maxRetries = 3)
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    // Метод безопасной записи: сначала во временный файл, затем замена
                    var tempFilePath = filePath + ".tmp";
                    var backupFilePath = filePath + ".bak";

                    // Записываем во временный файл
                    using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(fileStream))
                    {
                        await writer.WriteAsync(content);
                        await writer.FlushAsync();
                    }

                    // Создаем резервную копию существующего файла
                    if (File.Exists(filePath))
                    {
                        if (File.Exists(backupFilePath))
                            File.Delete(backupFilePath);
                        File.Move(filePath, backupFilePath);
                    }

                    // Заменяем основной файл временным
                    File.Move(tempFilePath, filePath);

                    // Удаляем резервную копию при успешной записи
                    if (File.Exists(backupFilePath))
                        File.Delete(backupFilePath);

                    return; // Успешная запись
                }
                catch (IOException ex) when (attempt < maxRetries - 1)
                {
                    // Очищаем временные файлы при ошибке
                    try
                    {
                        var tempFilePath = filePath + ".tmp";
                        if (File.Exists(tempFilePath))
                            File.Delete(tempFilePath);
                    }
                    catch { /* Игнорируем ошибки очистки */ }

                    // Если файл заблокирован, ждем и повторяем попытку
                    await Task.Delay(100 * (attempt + 1));
                    continue;
                }
                catch (Exception)
                {
                    // Очищаем временные файлы при критической ошибке
                    try
                    {
                        var tempFilePath = filePath + ".tmp";
                        if (File.Exists(tempFilePath))
                            File.Delete(tempFilePath);
                    }
                    catch { /* Игнорируем ошибки очистки */ }

                    throw;
                }
            }

            throw new IOException($"Не удалось записать файл {filePath} после {maxRetries} попыток");
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

        public void Dispose()
        {
            _fileSemaphore?.Dispose();
        }
    }
}