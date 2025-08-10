using System.Text.Json.Serialization;

namespace l2l_aggregator.Models.Configuration
{
    public class DeviceConfiguration
    {
        [JsonPropertyName("printer")]
        public PrinterConfig Printer { get; set; } = new();

        [JsonPropertyName("camera")]
        public CameraConfig Camera { get; set; } = new();

        [JsonPropertyName("controller")]
        public ControllerConfig Controller { get; set; } = new();

        [JsonPropertyName("scanner")]
        public ScannerConfig Scanner { get; set; } = new();

        [JsonPropertyName("ui")]
        public UIConfig UI { get; set; } = new();

        [JsonPropertyName("device")]
        public DeviceInfo Device { get; set; } = new();

        [JsonPropertyName("validation")]
        public ValidationConfig Validation { get; set; } = new();
    }

    public class PrinterConfig
    {
        [JsonPropertyName("ip")]
        public string? IP { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }
    }

    public class CameraConfig
    {
        [JsonPropertyName("ip")]
        public string? IP { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }
    }

    public class ControllerConfig
    {
        [JsonPropertyName("ip")]
        public string? IP { get; set; }
    }

    public class ScannerConfig
    {
        [JsonPropertyName("port")]
        public string? Port { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }
    }

    public class UIConfig
    {
        [JsonPropertyName("enableVirtualKeyboard")]
        public bool EnableVirtualKeyboard { get; set; } = false;
    }

    public class DeviceInfo
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("macAddress")]
        public string? MacAddress { get; set; }

        [JsonPropertyName("serialNumber")]
        public string? SerialNumber { get; set; }

        [JsonPropertyName("netAddress")]
        public string? NetAddress { get; set; }

        [JsonPropertyName("kernelVersion")]
        public string? KernelVersion { get; set; }

        [JsonPropertyName("hardwareVersion")]
        public string? HardwareVersion { get; set; }

        [JsonPropertyName("softwareVersion")]
        public string? SoftwareVersion { get; set; }

        [JsonPropertyName("firmwareVersion")]
        public string? FirmwareVersion { get; set; }

        [JsonPropertyName("deviceType")]
        public string? DeviceType { get; set; }

        [JsonPropertyName("deviceId")]
        public string? DeviceId { get; set; }

        [JsonPropertyName("deviceName")]
        public string? DeviceName { get; set; }
    }

    public class ValidationConfig
    {
        [JsonPropertyName("checkCamera")]
        public bool CheckCamera { get; set; } = true;

        [JsonPropertyName("checkPrinter")]
        public bool CheckPrinter { get; set; } = true;

        [JsonPropertyName("checkController")]
        public bool CheckController { get; set; } = true;

        [JsonPropertyName("checkScanner")]
        public bool CheckScanner { get; set; } = true;
    }
}
