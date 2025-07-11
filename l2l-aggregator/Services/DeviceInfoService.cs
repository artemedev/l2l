using l2l_aggregator.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace l2l_aggregator.Services
{
    public class DeviceInfoService
    {
        private readonly IConfiguration _configuration;
        public DeviceInfoService(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        /// <summary>
        /// Получает версию ядра/операционной системы
        /// </summary>
        public static string GetKernelVersion()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // На Linux читаем актуальную версию ядра
                    if (File.Exists("/proc/version"))
                    {
                        var versionInfo = File.ReadAllText("/proc/version");
                        var parts = versionInfo.Split(' ');
                        if (parts.Length > 2)
                            return parts[2]; // Например: "5.15.0-72-generic"
                    }
                }

                return Environment.OSVersion.Version.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка получения версии ядра: {ex.Message}");
                return Environment.OSVersion.Version.ToString();
            }
        }

        /// <summary>
        /// Получает информацию об архитектуре и платформе
        /// </summary>
        public static string GetHardwareVersion()
        {
            try
            {
                var processArch = RuntimeInformation.ProcessArchitecture; // X64, X86, Arm, Arm64
                var osArch = RuntimeInformation.OSArchitecture;

                var platform = GetPlatformName();

                return $"{processArch}/{osArch} ({platform})";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка получения архитектуры: {ex.Message}");
                return Environment.OSVersion.Platform.ToString();
            }
        }

        /// <summary>
        /// Получает название платформы
        /// </summary>
        public static string GetPlatformName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Windows";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return GetLinuxDistribution();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "macOS";
            else
                return "Unknown";
        }

        /// <summary>
        /// Получает название дистрибутива Linux
        /// </summary>
        private static string GetLinuxDistribution()
        {
            try
            {
                if (File.Exists("/etc/os-release"))
                {
                    var osRelease = File.ReadAllText("/etc/os-release");
                    var lines = osRelease.Split('\n');

                    foreach (var line in lines)
                    {
                        if (line.StartsWith("PRETTY_NAME="))
                        {
                            var name = line.Substring(12).Trim('"');
                            return name; // Например: "Ubuntu 20.04.5 LTS"
                        }
                    }
                }

                return "Linux";
            }
            catch
            {
                return "Linux";
            }
        }

        /// <summary>
        /// Получает детальную информацию о системе
        /// </summary>
        public static string GetDetailedSystemInfo()
        {
            try
            {
                var framework = RuntimeInformation.FrameworkDescription;
                var runtimeId = RuntimeInformation.RuntimeIdentifier;

                return $"{framework} ({runtimeId})";
            }
            catch
            {
                return ".NET Runtime";
            }
        }

        /// <summary>
        /// Получает MAC-адрес
        /// </summary>
        public static string GetMacAddress()
        {
            try
            {
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

                // Ищем активный сетевой интерфейс
                foreach (var network in networkInterfaces
                    .Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    .OrderBy(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 0 : 1))
                {
                    var mac = network.GetPhysicalAddress().ToString();
                    if (!string.IsNullOrEmpty(mac) && mac != "000000000000")
                    {
                        // Форматируем MAC-адрес с двоеточиями
                        return string.Join(":", Enumerable.Range(0, mac.Length / 2)
                            .Select(i => mac.Substring(i * 2, 2)));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка получения MAC-адреса: {ex.Message}");
            }

            return "00:00:00:00:00:00";
        }

        /// <summary>
        /// Получает локальный IP-адрес (улучшенная версия)
        /// </summary>
        public static string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());

                // Ищем IPv4 адрес
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка получения IP-адреса: {ex.Message}");
            }

            return "127.0.0.1";
        }

        /// <summary>
        /// Создает запрос регистрации устройства с корректными данными
        /// </summary>
        public ArmDeviceRegistrationRequest CreateRegistrationRequest()
        {
            return new ArmDeviceRegistrationRequest
            {
                NAME = GetDeviceName(),
                MAC_ADDRESS = GetMacAddress(),
                SERIAL_NUMBER = GetSerialNumber(),
                NET_ADDRESS = GetLocalIPAddress(),
                KERNEL_VERSION = GetKernelVersion(),
                HARDWARE_VERSION = GetHardwareVersion(), 
                SOFTWARE_VERSION = GetSoftwareVersion(),
                FIRMWARE_VERSION = GetFirmwareVersion(),
                DEVICE_TYPE = GetDeviceType()
            };
        }

        private static string GetDeviceType()
        {
            var arch = RuntimeInformation.ProcessArchitecture;

            return arch switch
            {
                Architecture.Arm => "ARM32",
                Architecture.Arm64 => "ARM64",
                Architecture.X64 => "X64",
                Architecture.X86 => "X86",
                _ => "UNKNOWN"
            };
        }
        /// <summary>
        /// Получает название устройства из конфигурации с подстановкой переменных
        /// </summary>
        public string GetDeviceName()
        {
            var deviceName = _configuration["DeviceSettings:DeviceName"] ?? "L2L-Aggregator-{HOSTNAME}";

            // Подстановка переменных
            deviceName = deviceName.Replace("{HOSTNAME}", Environment.MachineName);
            deviceName = deviceName.Replace("{MAC}", GetMacAddress().Replace(":", ""));

            return deviceName;
        }
        /// <summary>
        /// Получает серийный номер из конфигурации с подстановкой переменных
        /// </summary>
        public string GetSerialNumber()
        {
            var serialNumber = _configuration["DeviceSettings:SerialNumber"] ?? "SN-{MAC}";

            // Подстановка переменных
            serialNumber = serialNumber.Replace("{HOSTNAME}", Environment.MachineName);
            serialNumber = serialNumber.Replace("{MAC}", GetMacAddress().Replace(":", ""));

            return serialNumber;
        }
        /// <summary>
        /// Получает версию ПО из конфигурации
        /// </summary>
        public string GetSoftwareVersion()
        {
            return _configuration["DeviceSettings:SoftwareVersion"] ?? "1.0.0";
        }

        /// <summary>
        /// Получает версию прошивки из конфигурации
        /// </summary>
        public string GetFirmwareVersion()
        {
            return _configuration["DeviceSettings:FirmwareVersion"] ?? "1.0.0";
        }
    }
}
