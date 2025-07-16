namespace MD.Aggregation.Devices.Tcp;

/// <summary>
/// Аргументы события обновления статуса устройства.
/// </summary>
public class DeviceStatusEventArgs : EventArgs
{
    /// <summary>
    /// Предыдущий статус устройства
    /// </summary>
    public DeviceStatusCode OldStatus = DeviceStatusCode.Unknow;

    /// <summary>
    /// Новый статус устройства
    /// </summary>
    public required DeviceStatusCode NewStatus;
}
