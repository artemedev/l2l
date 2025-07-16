
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Net.Sockets;
using RM = MD.Aggregation.Devices.Tcp.Resources.Builder;

namespace MD.Aggregation.Devices.Tcp;

/// <summary>
/// Построитель объекта TCP соединения.
/// Этот класс отвечает за создание и настройку TCP-соединения на основе 
/// предоставленной конфигурации. Он выполняется в фоновом режиме и 
/// обрабатывает процесс подключения, включая тайм-ауты и обработку ошибок.
/// Результат (объект `Device`) будет доступен в событии `RunWorkerCompleted`.
/// </summary>
/// <param name="logger">Логгер для записи информации о процессе создания 
/// соединения.</param>
public class Builder(ILogger logger) : BackgroundWorker
{
    /// <inheritdoc/>
    protected override void OnDoWork(DoWorkEventArgs e)
    {

        if (e.Argument is IConfiguration configuration)
        {
            logger.LogDebug(RM.CreateTcp);
            var tcp = new Device()
            {
                Client = new(),
                Config = new(),
                Logger = logger
            };
            configuration.Bind(tcp.Config);
            logger.LogInformation(RM.OpenTcp, tcp.Config.Ip, tcp.Config.Port);
            try
            {
                var connectTask = tcp.Client.ConnectAsync(tcp.Config.Ip, tcp.Config.Port);
                if (connectTask.Wait(tcp.Config.ConnectTimeout))
                {
                    logger.LogInformation(RM.ConnectSucces);
                    tcp.Client.ReceiveTimeout = tcp.Config.ReceiveTimeout;
                    tcp.Client.SendTimeout = tcp.Client.SendTimeout;

                    tcp.Client.Client.SetSocketOption(
                        SocketOptionLevel.Tcp,
                        SocketOptionName.TcpKeepAliveInterval,
                        tcp.Config.TcpKeepAliveInterval);
                    tcp.Client.Client.SetSocketOption(
                        SocketOptionLevel.Tcp,
                        SocketOptionName.TcpKeepAliveTime,
                        tcp.Config.TcpKeepAliveTime);
                    tcp.Client.Client.SetSocketOption(
                        SocketOptionLevel.Tcp,
                        SocketOptionName.TcpKeepAliveRetryCount,
                        tcp.Config.TcpKeepAliveRetryCount);
                    tcp.Client.Client.SetSocketOption(
                        SocketOptionLevel.Socket,
                        SocketOptionName.KeepAlive,
                        tcp.Config.KeepAliveEnable);

                    e.Result = tcp;
                }
                else
                {
                    logger.LogError(RM.ConnectTimeOut, tcp.Config.ConnectTimeout);
                    tcp.Dispose();
                }

            }
            catch (Exception ex)
            {
                logger.LogError(ex, RM.ExpOpen, ex.Message);
            }
        }
        else
        {
            logger.LogError(RM.ErrArg, e.Argument != null ? e.Argument.GetType() : "null");
        }
    }

}
