
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text;
using RM = MD.Aggregation.Devices.Printer.Resources.ZPL.GetStatusWorker;

namespace MD.Aggregation.Devices.Printer.ZPL;

[Obsolete("На эмуляторах ZPL работает не корректно или вообще не работает",false)]
public class GetStatusWorker(ILogger logger, MD.Aggregation.Devices.Tcp.Device device) : BackgroundWorker
{
    /// <summary>
    /// Команда запроса статуса
    /// </summary>
    public const string Command = "~HS";

    /// <summary>
    /// Длина ответа в байтах от принтера при запросе состояния.
    /// </summary>
    public const int PrinterStatusResponseLength = 82;

    /// <inheritdoc/>
    protected override void OnDoWork(DoWorkEventArgs e)
    {
        try
        {
            logger.LogInformation(RM.StartCmd);
            byte[] dataToSend = Encoding.ASCII.GetBytes(Command + "\r\n");
            device.TcpStream.Write(dataToSend, 0, dataToSend.Length);

            logger.LogDebug(RM.SendRequest);
            byte[] responseBuffer = new byte[100];
            int bytesRead = device.TcpStream.Read(responseBuffer, 0, responseBuffer.Length);
            logger.LogDebug(RM.ResponseRecivied, bytesRead);

            /*if (bytesRead != PrinterStatusResponseLength)
            { 
                logger.LogError(RM.ErrByteBufLen, bytesRead);
                e.Result = DeviceStatusCode.Fail;
                return;
            }*/

            Status state = new(logger);
            /*if (state.IsOk(responseBuffer))
            {
                e.Result = DeviceStatusCode.Ready;
            }
            else
            {
                e.Result = DeviceStatusCode.Fail;
            }*/
            e.Result = DeviceStatusCode.Ready;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, RM.ExcGetStatusFail , ex.Message);
            e.Result = DeviceStatusCode.Fail;
        }
    }
}
