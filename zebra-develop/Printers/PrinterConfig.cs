
using MD.Aggregation.Devices.Printer.Settings;

namespace MD.Aggregation.Devices.Printer;

/// <summary>
/// Конфигурация принтера
/// </summary>
public class PrinterConfig : MD.Aggregation.Devices.DeviceConfig
{
    public LabelSettings LabelSettings { get; set; } = new LabelSettings();
}
