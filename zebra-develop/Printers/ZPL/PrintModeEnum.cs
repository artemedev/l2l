

namespace MD.Aggregation.Devices.Printer.ZPL;

/// <summary>
/// Режим печати
/// </summary>
public enum PrintModeEnum
{
    /// <summary>
    /// Перемотка
    /// </summary>
    Rewind = 0,

    /// <summary>
    /// Отделение
    /// </summary>
    Peel_Off = 1,

    /// <summary>
    /// Отрыв
    /// </summary>
    Tear_Off =2,

    /// <summary>
    /// Нарезка
    /// </summary>
    Cutter = 3,

    /// <summary>
    /// Аппликатор
    /// </summary>
    Applicator = 4,
}
