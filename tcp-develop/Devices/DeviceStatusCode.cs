namespace MD.Aggregation.Devices.Tcp;

/// <summary>
/// Статус код устройства.
/// <para>
/// Это набор флагов, допускается установка сразу нескольких значений.
/// </para>
/// </summary>
[Flags]
public enum DeviceStatusCode
{
    /// <summary>
    /// Неизвестное состояние
    /// Или не инициализировано
    /// </summary>
    Unknow = 1,
    /// <summary>
    /// устройство готово к работе
    /// </summary>
    Ready = 2,
    /// <summary>
    /// Устройство запускается
    /// </summary>
    StartingUp = 4,
    /// <summary>
    /// Устройство работает
    /// </summary>
    Run = 8,
    /// <summary>
    /// Устройство останавливается
    /// </summary>
    Stops = 16,
    /// <summary>
    /// Устройство инициализировано
    /// находится в режиме ожидания
    /// работу не выполняет.
    /// </summary>
    Inactive = 32,
    /// <summary>
    /// Отказ устройства
    /// </summary>
    Fail = 64,
    /// <summary>
    /// Переподсоединение
    /// </summary>
    Reconnect = 128,
}

