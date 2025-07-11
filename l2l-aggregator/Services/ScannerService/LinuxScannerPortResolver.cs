using l2l_aggregator.Services.ScannerService.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace l2l_aggregator.Services.ScannerService
{
    public class LinuxScannerPortResolver : IScannerPortResolver
    {
        private readonly ILogger<LinuxScannerPortResolver>? _logger;

        public LinuxScannerPortResolver(ILogger<LinuxScannerPortResolver>? logger = null)
        {
            _logger = logger;
        }

        public IEnumerable<string> GetHoneywellScannerPorts()
        {
            var result = new List<string>();

            try
            {
                // Используем команду udevadm для получения информации об устройствах
                result.AddRange(GetDevicesViaUdevadm());

                // Если udevadm не работает, используем прямое сканирование
                if (result.Count == 0)
                {
                    result.AddRange(GetDevicesViaSysfs());
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error scanning for Honeywell scanner ports");
            }

            return result.Distinct();
        }

        private IEnumerable<string> GetDevicesViaUdevadm()
        {
            var devices = new List<string>();

            try
            {
                // Используем udevadm для получения списка tty устройств
                var startInfo = new ProcessStartInfo
                {
                    FileName = "udevadm",
                    Arguments = "info --export-db",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    var currentDevice = "";
                    var currentVendorId = "";
                    var currentDevNode = "";

                    foreach (var line in output.Split('\n'))
                    {
                        var trimmedLine = line.Trim();

                        if (trimmedLine.StartsWith("P: "))
                        {
                            // Новое устройство
                            if (!string.IsNullOrEmpty(currentDevNode) &&
                                currentVendorId.Equals("0c2e", StringComparison.OrdinalIgnoreCase))
                            {
                                devices.Add(currentDevNode);
                            }

                            currentDevice = trimmedLine.Substring(3);
                            currentVendorId = "";
                            currentDevNode = "";
                        }
                        else if (trimmedLine.StartsWith("E: ID_VENDOR_ID="))
                        {
                            currentVendorId = trimmedLine.Substring(16);
                        }
                        else if (trimmedLine.StartsWith("E: DEVNAME="))
                        {
                            currentDevNode = trimmedLine.Substring(11);
                        }
                    }

                    // Проверяем последнее устройство
                    if (!string.IsNullOrEmpty(currentDevNode) &&
                        currentVendorId.Equals("0c2e", StringComparison.OrdinalIgnoreCase))
                    {
                        devices.Add(currentDevNode);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "udevadm approach failed, falling back to sysfs");
            }

            return devices;
        }

        private IEnumerable<string> GetDevicesViaSysfs()
        {
            var devices = new List<string>();

            try
            {
                var devicePatterns = new[]
                {
                    "/dev/ttyACM*",
                    "/dev/rfcomm*",
                    "/dev/ttyAM*",
                    "/dev/ttyUSB*",
                    "/dev/serial*"
                };

                foreach (var pattern in devicePatterns)
                {
                    var directory = Path.GetDirectoryName(pattern);
                    var filePattern = Path.GetFileName(pattern);

                    if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                        continue;

                    var foundDevices = Directory.GetFiles(directory, filePattern);

                    foreach (var device in foundDevices)
                    {
                        if (CheckDeviceVendorId(device))
                        {
                            devices.Add(device);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error scanning devices via sysfs");
            }

            return devices;
        }

        private bool CheckDeviceVendorId(string devicePath)
        {
            try
            {
                var deviceName = Path.GetFileName(devicePath);

                // Ищем idVendor, начиная от device и поднимаясь вверх
                var deviceDir = $"/sys/class/tty/{deviceName}/device";

                if (!Directory.Exists(deviceDir))
                    return false;

                // Поднимаемся по дереву устройств и ищем idVendor
                var currentDir = new DirectoryInfo(deviceDir);
                for (int level = 0; level < 5; level++) // максимум 5 уровней вверх
                {
                    if (currentDir?.Parent == null)
                        break;

                    currentDir = currentDir.Parent;
                    var vendorFile = Path.Combine(currentDir.FullName, "idVendor");

                    if (File.Exists(vendorFile))
                    {
                        var vid = File.ReadAllText(vendorFile).Trim().ToLower();
                        return vid == "0c2e"; // Honeywell Vendor ID
                    }
                }

                // Для ttyACM устройств можем считать их потенциально подходящими
                return deviceName.StartsWith("ttyACM");
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Error checking vendor ID for {Device}", devicePath);
                return false;
            }
        }
    }
}