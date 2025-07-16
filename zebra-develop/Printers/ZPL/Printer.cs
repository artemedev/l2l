
using MD.Aggregation.Devices.HMI;
using MD.Aggregation.Devices.Tcp;
using MD.Aggregation.Marking.UN;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using RM = MD.Aggregation.Devices.Printer.Resources.ZPL.Printer;

namespace MD.Aggregation.Devices.Printer.ZPL;

public class Printer(string name, ILogger logger) : MD.Aggregation.Devices.Printer.Printer(name, logger)
{
    /// <summary>
    /// Подключение к принтеру по TCP
    /// </summary>
    private MD.Aggregation.Devices.Tcp.Device? tcpDevice;

    /// <summary>
    /// Конфигурация принтера
    /// </summary>
    private PrinterConfig Config = new();

    // TODO: реализовать
    public override DeviceConfig Configuration => Config;

    public string PreGenerationString
    {
        get
        {
            StringBuilder sb = new StringBuilder();
            PreGenerateString(sb);
            return sb.ToString();
        }
    }

    private IConfigurationSection? configurationSection;

    /// <inheritdoc/>
    public override void Configure(IConfiguration config)
    {
        Logger.LogInformation(RM.StartInit);

        if (configurationSection == null)
        {
            config.Bind(Config);
            configurationSection = config.GetSection(nameof(PrinterConfig.Connection));
        }

        if (tcpDevice != null)
        {
            tcpDevice.Dispose();
        }
        var tcpBuilder = new Builder(Logger);
        tcpBuilder.RunWorkerCompleted += TcpBuilder_RunWorkerCompleted;
        tcpBuilder.RunWorkerAsync(configurationSection);
        StatusCode = DeviceStatusCode.StartingUp;
    }


    /// <summary>
    /// Обработчик завершения асинхронной операции подключения к 
    /// принтеру по TCP. Сохраняет результат подключения
    /// и инициирует получение статуса устройства при успешном 
    /// подключении.
    /// </summary>
    /// <param name="sender">Объект, вызвавший событие</param>
    /// <param name="e">Аргументы события, содержащие результат 
    /// операции</param>
    private void TcpBuilder_RunWorkerCompleted(object? sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
    {
        KillWorker(sender);

        if (e.Result is MD.Aggregation.Devices.Tcp.Device result)
        {
            tcpDevice = result;
            StatusCode = DeviceStatusCode.Ready;
        }
        else
        {
            Logger.LogError(RM.FailTcp);
            StatusCode = Devices.DeviceStatusCode.Fail;
        }
    }

    /// <summary>
    /// Обработчик завершения асинхронной операции получения 
    /// статуса устройства. Обновляет `StatusCode` принтера на 
    /// основе полученного результата.
    /// </summary>
    /// <param name="sender">Объект, вызвавший событие</param>
    /// <param name="e">Аргументы события, содержащие результат операции</param>
    [Obsolete("Использовался как результат работы GetStatusWorker ", false)]
    private void Status_RunWorkerCompleted(object? sender, RunWorkerCompletedEventArgs e)
    {
        KillWorker(sender);
        if (e.Result is DeviceStatusCode status)
        {
            if (status == DeviceStatusCode.Fail)
            {
                DoFatalError(RM.ErrExecutionCommand);
            }
            else
            {
                StatusCode = status;
            }
        }
    }

    /// <summary>
    /// Я хрен знает зачем это, но вроде воркеров
    /// нужно прихлопывать, но это не точно
    /// </summary>
    /// <param name="sender">возможный воркер</param>
    private void KillWorker(object? sender)
    {
        if (sender is BackgroundWorker worker)
        {
            worker.Dispose();
        }
    }

    /// <inheritdoc/>
    public override bool Release()
    {
        if (tcpDevice != null)
        {
            tcpDevice.FatalError -= TcpDevice_FatalError;
            tcpDevice.Dispose();
            StatusCode = DeviceStatusCode.Ready;
            tcpDevice = null;
        }
        else
        {
            StatusCode = DeviceStatusCode.Unknow;
        }
        return true;
    }

    /// <inheritdoc/>
    public override bool RequestStatus()
    {
        bool res = false;
        if (tcpDevice != null)
        {
            res = tcpDevice.RequestStatus();
            if (res)
            {
                tcpDevice.FatalError -= TcpDevice_FatalError;
                tcpDevice.FatalError += TcpDevice_FatalError;
            }
        }
        return res;
    }

    private void TcpDevice_FatalError(object? sender, Tcp.DeviceFatalErrorEventArgs e)
    {
        DoFatalError(e.Message);
    }

    /// <inheritdoc/>
    public override bool SendToBuffer(CodeInfo codeInfo)
    {
        if (Config.ReadBeforeSend)
        {
            CheckBuffer();
        }
        if (Config.ImageMode.Enable)
        {
            return PrintFile(codeInfo);
        }
        var tCmd = GetCodeString(codeInfo);
        
        if (tCmd != null)
        {
            return SendToBuffer(tCmd);
        }
        return false;
    }

    /// <summary>
    /// Отправляем в принтер команду
    /// </summary>
    /// <param name="tCmd">Команда</param>
    /// <returns></returns>
    protected bool SendToBuffer(string tCmd)
    {
        // TODO: привесли в порядок логи
        Logger.LogDebug($"Device: {Tcp.DeviceStatusCode.Reconnect}");
        if (tcpDevice == null)
        {
            Logger.LogError(RM.DeviceNull);
            return false;
        }

       
        if (tcpDevice != null && tcpDevice.Status == Tcp.DeviceStatusCode.Reconnect)
        {
            Logger.LogWarning("Устройство в реконнекте невозможно послать сообщение в буфер принтера");
            return false;
        }

        byte[] dataToSend = Encoding.ASCII.GetBytes(tCmd);
        try
        {
            tcpDevice.TcpStream.Write(dataToSend, 0, dataToSend.Length);
            bufferSize++;
            Logger.LogDebug($"Отправили на печать {tCmd}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, RM.SendError, ex.Message);
            var msg = string.Format(RM.SendError, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Печатаем из заранее подготовленных кодов
    /// </summary>
    /// <param name="codeInfo"></param>
    /// <returns></returns>
    private bool PrintFile(CodeInfo codeInfo)
    {
        if (tcpDevice == null)
        {
            Logger.LogError(RM.DeviceNull);
            return false;
        }
        var fileDirectory = new DirectoryInfo(Config.ImageMode.Path);
        if (codeInfo.ContainsKey(Config.ImageMode.Key))
        {
            var file = new FileInfo($"{fileDirectory.FullName}\\{codeInfo[Config.ImageMode.Key]}.{Config.ImageMode.Extension}");
            if (file.Exists)
            {
                Logger.LogDebug(RM.DbgReadFile, file.FullName);
                try
                {
                    var start = Stopwatch.StartNew();
                    string fileData = "";
                    using (var stream = file.OpenText())
                    {
                        fileData = stream.ReadToEnd();
                    }
                    if (!string.IsNullOrEmpty(fileData))
                    {
                        byte[] dataToSend = Encoding.ASCII.GetBytes(fileData);
                        tcpDevice.TcpStream.Write(dataToSend, 0, dataToSend.Length);
                        bufferSize++;
                        start.Stop();
                        Logger.LogDebug(RM.DbgPrintTime, start.Elapsed);
                        return true;
                    }
                    else
                    {
                        Logger.LogError(RM.ErrorNotRead, file.FullName);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, RM.ExepReadFile, ex.Message);
                    return false;
                }
            }
            else
            {
                Logger.LogError(RM.NoFile, file.FullName);
                return false;
            }
        }
        else
        {
            Logger.LogError(RM.ImageNoKey, Config.ImageMode.Key);
            return false;
        }
        
    }


    /// <summary>
    /// Проверить буфер обмена с принтером
    /// </summary>
    private void CheckBuffer()
    {
        if (tcpDevice != null)
        {
            if (tcpDevice.TcpStream.CanRead)
            {
                if (tcpDevice.TcpStream.Socket.Available > 0)
                {
                    byte[] buffer = new byte[tcpDevice.TcpStream.Socket.Available];
                    tcpDevice.TcpStream.Read(buffer);
                    if (buffer.Length > 0)
                    {
                        Logger.LogWarning(RM.ReadBuffer, buffer.Length, Encoding.ASCII.GetString(buffer));
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    protected override string GetSymbolGS(TemplateEntityType type)
    {
        switch (type)
        {
            case TemplateEntityType.GS91:
                return Config.LabelSettings.EntityType.GS91;
            case TemplateEntityType.GS92:
                return Config.LabelSettings.EntityType.GS92;
            case TemplateEntityType.GS93:
                return Config.LabelSettings.EntityType.GS93;
        }
        return string.Empty;
    }

    /// <inheritdoc/>
    protected override void PreGenerateString(StringBuilder sb)
    {

        sb.AppendLine("^XA");
        sb.AppendLine($"^FO{Config.LabelSettings.Start.Top},{Config.LabelSettings.Start.Left}");
        sb.AppendLine($"^BX{Config.LabelSettings.DataMatrix.Orientation},{Config.LabelSettings.DataMatrix.Height},{Config.LabelSettings.DataMatrix.Quality},{Config.LabelSettings.DataMatrix.CodingColumns},{Config.LabelSettings.DataMatrix.CodingRows},{Config.LabelSettings.DataMatrix.Format},{Config.LabelSettings.DataMatrix.ControlChar},{Config.LabelSettings.DataMatrix.AspectRatio}");
        sb.Append($"^FD{Config.LabelSettings.FieldData.PrintData}");
    }

    /// <summary>
    /// Добавляет завершающие команды ZPL в конец сгенерированной 
    /// строки.Эти команды завершают формат этикетки и указывают 
    /// на конец ZPL-скрипта.
    /// </summary>
    /// <param name="sb">StringBuilder, содержащий сгенерированную 
    /// ZPL-строку</param>
    protected override void PostGenerateString(StringBuilder sb)
    {
        sb.AppendLine("^FS");
        sb.AppendLine("^XZ");
    }

    Timer? aliveTimer;

    /// <inheritdoc/>
    public override bool StartWork()
    {
        aliveTimer = new Timer(aliveTimeElapsed, null, 0, 2000);

        return true;
    }

    private void aliveTimeElapsed(object? state)
    {
        RequestStatus();
    }

    /// <inheritdoc/>
    public override bool StopWork()
    {
        if (aliveTimer != null)
        {
            aliveTimer.Dispose();
        }
        return true;
    }

    /// <inheritdoc/>
    public override DeviceInfo GetHmiDescription()
    {
        var info = base.GetHmiDescription();
        info.Tags.Add(Devices.HMI.TagNames.Address, Config.Connection.Ip);
        return info;
    }
}
