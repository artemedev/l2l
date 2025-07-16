
namespace MD.Aggregation.Devices.Tcp;

/// <summary>
/// Конфигурация соединения с устройством по TCP
/// </summary>
public class Configuration
{
    /// <summary>
    /// IP адрес ПЛК
    /// </summary>
    public string Ip { get; set; } = "";

    /// <summary>
    /// Порт соединения с ПЛК
    /// </summary>
    public int Port { get; set; } = 0;

    /// <summary>
    /// Таймаут подключения, миллисекунд
    /// </summary>
    public int ConnectTimeout { get; set; } = 5000;

    /// <summary>
    /// Таймаут чтения, миллисекунд
    /// </summary>
    public int ReceiveTimeout { get; set; } = 1000;

    /// <summary>
    /// Таймаут записи, миллисекунд
    /// </summary>
    public int SendTimeout { get; set; } = 1000;

    /// <summary>
    /// Время ожидания ответа от устройства при проверке
    /// установленного соединения (в микросекундах).
    /// </summary>
    public int RequestStatusTimeOut { get; set; } = 1000;

    /// <summary>
    /// Счетчик попыток переподсоединения
    /// </summary>
    public int ReconnectRetryCount { get; set; } = 3;

    /// <summary>
    /// Начальное время для задержки перед подсоединением
    /// </summary>
    public int ReconnectRetryDelay { get; set; } = 1000;

    /// <summary>
    /// Время ожидания до начала отправки пакетов Keep-Alive (в секундах).
    /// </summary>
    public int TcpKeepAliveTime { get; set; } = 2;

    /// <summary>
    /// Интервал между отправкой пакетов Keep-Alive (в секундах).
    /// </summary>
    public int TcpKeepAliveInterval { get; set; } = 1;

    /// <summary>
    /// Количество попыток отправки пакетов Keep-Alive.
    /// </summary>
    public int TcpKeepAliveRetryCount { get; set; } = 2;

    /// <summary>
    /// Включение механизма Keep-Alive.
    /// </summary>
    public bool KeepAliveEnable { get; set; } = true;
}
