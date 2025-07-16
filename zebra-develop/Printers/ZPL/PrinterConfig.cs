
namespace MD.Aggregation.Devices.Printer.ZPL;

/// <summary>
/// Конфигурация принтера
/// </summary>
public class PrinterConfig : MD.Aggregation.Devices.Printer.PrinterConfig
{
    /// <summary>
    /// Конфигурация
    /// </summary>
    public MD.Aggregation.Devices.Tcp.Configuration Connection { get; set; } = new();

    /// <summary>
    /// Попытаться прочитать данные из буфера TCP
    /// перед отправкой кода в буфер принтера.
    /// </summary>
    public bool ReadBeforeSend { get; set; } = false;

    /// <summary>
    /// Настройки печати из подготовленных
    /// файлов
    /// </summary>
    public ImagePrint ImageMode { get; set; } = new();
}
