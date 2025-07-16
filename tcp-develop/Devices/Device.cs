
using System.ComponentModel;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RM = MD.Aggregation.Devices.Tcp.Resources.Device;

namespace MD.Aggregation.Devices.Tcp;

/// <summary>
/// Объект подключения по TCP
/// </summary>
public class Device : IDisposable
{
    /// <summary>
    /// Счетчик попыток восстановления подключения
    /// </summary>
    private int CurrentRetryCount = 0;
    /// <inheritdoc/>
    public event EventHandler<DeviceFatalErrorEventArgs>? FatalError;

    /// <inheritdoc/>
    public event EventHandler<DeviceStatusEventArgs>? StatusReceived;

    /// <inheritdoc/>
    public event EventHandler<DeviceStatusEventArgs>? StatusChanged;

    /// <summary>
    /// TCP Клиент
    /// </summary>
    public required TcpClient Client;

    /// <summary>
    /// Конфигурация TCP
    /// </summary>
    public required Configuration Config;

    /// <summary>
    /// Журнал для записи событий
    /// </summary>
    public ILogger? Logger;

    private NetworkStream? tcpStream;

    /// <summary>
    /// Поток для чтения и записи TCP сокета
    /// </summary>
    public NetworkStream TcpStream
    {
        get
        {
            if (tcpStream == null)
            {
                Logger?.LogDebug(RM.CreateStream);
                tcpStream = Client.GetStream();
            }
            return tcpStream;
        }
    }

    /// <summary>
    /// Переменная для хранения статуса устройства
    /// </summary>
    private DeviceStatusCode statusCode = DeviceStatusCode.Unknow;

    /// <summary>
    /// Время когда статус был обновлен
    /// </summary>
    private DateTime statusUpdated = DateTime.Now;

    /// <inheritdoc/>
    public DeviceStatusCode Status
    {
        get
        {
            return statusCode;
        }
        protected set
        {
            var oldStatus = statusCode;
            statusUpdated = DateTime.Now;
            if (statusCode != value)
            {
                statusCode = value;
                StatusChanged?.Invoke(this,
                    new DeviceStatusEventArgs()
                    {
                        NewStatus = statusCode,
                        OldStatus = oldStatus
                    });
            }
            StatusReceived?.Invoke(this,
                new DeviceStatusEventArgs()
                {
                    NewStatus = statusCode,
                    OldStatus = oldStatus
                });
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (tcpStream != null)
        {
            if (Logger != null)
            {
                if (tcpStream.DataAvailable)
                {
                    Span<byte> buffer = [];
                    var bytes = tcpStream.Read(buffer);
                    Logger.LogDebug(RM.StreamBuffer, bytes);
                }
                Logger.LogDebug(RM.CloseStream);
            }
            tcpStream.Dispose();
        }
        else
        {
            Logger?.LogDebug(RM.StreamNotOpen);
        }
        Logger?.LogDebug(RM.DisposeTcp);
        Client.Dispose();
    }

    /// <summary>
    /// Кол-во байт доступны для чтения.
    /// Если вернется -1 значит было исключение
    /// при попытке чтения
    /// </summary>
    public int Available
    {
        get
        {
            try
            {
                return Client.Available;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, RM.ExceptErrorTcp, ex.Message);
                return -1;
            }
        }
    }
    // => Client.Available;

    /// <summary>
    /// Признак что есть доступные байты 
    /// для чтения
    /// </summary>
    public bool IsAvailable => Available > 0;

    /// <summary>
    /// Прочитать байты из входного 
    /// потока
    /// </summary>
    /// <returns>
    /// Массив прочитанных байт. 
    /// Если вернулся null значит не смогли прочитать
    /// возможно ошибка чтения, возможно просто нет данных
    /// </returns>
    public byte[]? Read()
    {
        var bufferSize = Available;
        if (Available > 0)
        {
            try
            {
                byte[] buffer = new byte[bufferSize];
                TcpStream.Read(buffer, 0, bufferSize);
                return buffer;

            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, RM.ExceptErrorRead, bufferSize, ex.Message);
            }
        }
        return null;
    }

    public string? ReadString(System.Text.Encoding? encoder = null)
    {
        var buffer = Read();
        if (buffer != null)
        {
            var useEncoder = System.Text.Encoding.Default;
            if (encoder != null)
            {
                useEncoder = encoder;
            }
            try
            {
                return useEncoder.GetString(buffer);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, RM.ExceptErrorEncoding, useEncoder.GetType().Name, buffer.Length, ex.Message);
            }
        }
        return null;
    }

    /// <summary>
    /// Worker проверяет доступность устройства по TCP
    /// </summary>
    private RequestStatusWorker? requestStatusWorker;

    /// <inheritdoc/>
    public bool RequestStatus()
    {
        if (Client == null)
        {
            Logger?.LogError(RM.ExpClientIsNull);
            DoFatalError(RM.ExpClientIsNull);
            Release(DeviceStatusCode.Fail);
            return false;
        }

        requestStatusWorker = new RequestStatusWorker(Logger, Client, Config);
        requestStatusWorker.RunWorkerCompleted += RequestStatusWorkerCompleted;
        requestStatusWorker.RunWorkerAsync();
        return true;
    }

    Builder? _builder;

    /// <summary>
    /// Событие вызывается после выполнения проверки доступности ТСП соединения.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void RequestStatusWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        requestStatusWorker?.Dispose();
        requestStatusWorker = null;

        if ((e.Result is string errMsg) && !string.IsNullOrEmpty(errMsg))
        {
            if (CurrentRetryCount < Config.ReconnectRetryCount)
            {
                CurrentRetryCount++;
                int millisecondTimeout = CurrentRetryCount * Config.ReconnectRetryDelay;
                Logger?.LogInformation(RM.ReconectTry, CurrentRetryCount, millisecondTimeout);
                //проверить
                Thread.Sleep(millisecondTimeout);

                Release(DeviceStatusCode.Reconnect);

                _builder = new Builder(Logger)
                {
                    WorkerReportsProgress = true
                };
                _builder.RunWorkerCompleted += (s, args) =>
                {
                    if (args.Result is Device newDevice)
                    {
                        //проверить
                        Client = newDevice.Client;
                        Config = newDevice.Config;
                        Status = DeviceStatusCode.Run;
                        CurrentRetryCount = 0;
                    }
                    else
                    {
                        RequestStatus();
                    }
                    _builder.Dispose();
                    _builder = null;
                };
                IConfiguration conf = new ConfigurationBuilder()
                .AddObject(Config, "Connection")
                .Build();

                _builder.RunWorkerAsync(conf);
            }
            else
            {
                CurrentRetryCount = 0;
                DoFatalError(errMsg);
                Release(DeviceStatusCode.Fail);
                return;
            }

            return;
        }

        Status = DeviceStatusCode.Run;
    }

    /// <summary>
    /// Метод вызывает событие <see cref="FatalError"/>, 
    /// при этом <see cref="Status"/> устанавливается в значение
    /// <see cref="DeviceStatusCode.Fail"/>
    /// </summary>
    /// <param name="message">Сообщение пользователю, с информацией 
    /// что именно привело к ошибке устройства</param>
    protected void DoFatalError(string message)
    {
        this.Status = DeviceStatusCode.Fail;
        FatalError?.Invoke(this, new DeviceFatalErrorEventArgs() { Message = message });
    }

    /// <summary>
    /// Освободить ресурсы подключения к устройству.
    /// Если были открыты какие-то сетевые подключения
    /// по вызову данной команды необходимо их закрыть
    /// по завершению устройство должно перейти в статус,
    /// который указан в параметре
    /// </summary>
    /// <param name="status">Новый статус устройства</param>
    /// <returns>
    /// `false` - если произошла ошибка и состояние устройства 
    /// становится <see cref="DeviceStatusCode.Fail"/>
    /// </returns>
    public bool Release(DeviceStatusCode status)
    {
        try
        {
            Logger?.LogDebug(RM.DbgRelease, Config.Ip);

            if (tcpStream != null)
            {
                Logger?.LogDebug(RM.DbgCloseStream);
                tcpStream.Dispose();
                tcpStream = null;
            }
            Logger?.LogDebug(RM.DbgDisposeTcp);
            Client?.Close();
            Client?.Dispose();
            Status = status;
            return true;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, RM.ExceptErrorRelease, ex.Message);
            DoFatalError(string.Format(RM.ExceptErrorRelease, ex.Message));
            return false;
        }
    }
}
