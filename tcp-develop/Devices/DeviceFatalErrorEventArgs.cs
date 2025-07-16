namespace MD.Aggregation.Devices.Tcp;

/// <summary>
/// Аргументы вызова события фатальная ошибка <see cref="IDevice.FatalError"/>
/// </summary>
public class DeviceFatalErrorEventArgs : EventArgs
{
    /// <summary>
    /// Текст ошибки. Что именно привело к фатальной
    /// ошибке
    /// </summary>
    public string Message = string.Empty;
}
