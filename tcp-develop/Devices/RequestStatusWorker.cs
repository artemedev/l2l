
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Net.Sockets;

using RM = MD.Aggregation.Devices.Tcp.Resources.RequestStatusWorker;

namespace MD.Aggregation.Devices.Tcp;

/// <summary>
/// Worker проверяет доступность устройства по TCP.
/// Если устройство не доступно или произошла, то в e.Result возвращается
/// строка с ошибкой. Если же устройство доступно, то возвращается null.
/// </summary>
/// <param name="logger">Логгер</param>
/// <param name="tcp">TCP клиент</param>
/// <param name="config">конфигурация</param>
public class RequestStatusWorker(
    ILogger? logger, TcpClient tcp, Configuration config) : BackgroundWorker
{
    protected override void OnDoWork(DoWorkEventArgs e)
    {
        try
        {
            if (logger != null)
                logger.LogDebug(
                    RM.DbgTestConnection, config.Ip, config.Port);

            if (!tcp.Connected)
            {
                var errMsg = string.Format(
                    RM.DbgNoConnection, config.Ip, config.Port);
                if (logger != null)
                    logger.LogDebug(errMsg);
                e.Result = errMsg;
                return;
            }

            if (tcp.Client.Poll(config.RequestStatusTimeOut, SelectMode.SelectRead))
            {
                if (tcp.Client.Available == 0)
                {
                    var errMsg = string.Format(
                        RM.DbgConnectionClosed, config.Ip, config.Port);
                    if (logger != null)
                        logger.LogDebug(errMsg);
                    e.Result = errMsg;
                    return;
                }
            }

            if (logger != null)
                logger.LogDebug(
                RM.DbgConnectionOk, config.Ip, config.Port);
            e.Result = null;
        }
        catch (Exception ex)
        {
            var errMsg = string.Format(
                RM.ExceptErrorRequestStatus,
                config.Ip,
                config.Port,
                ex.Message);

            if (logger != null)
                logger.LogError(ex, errMsg);
            e.Result = errMsg;
        }
    }
}
