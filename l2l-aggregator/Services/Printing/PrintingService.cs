using FastReport;
using FastReport.Export.Zpl;
using l2l_aggregator.Services.Notification.Interface;
using MD.Aggregation.Devices.Printer.ZPL;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace l2l_aggregator.Services.Printing
{
    public class PrintingService : IDisposable
    {
        private readonly INotificationService _notificationService;
        private readonly SessionService _sessionService;
        private ILogger logger;
        private const string TEST_TEMPLATE_BASE64 = "";
        private LabelPrinter? _labelPrinter;
        private bool _disposed = false;

        public PrintingService(
            INotificationService notificationService,
            SessionService sessionService)
        {
            _notificationService = notificationService;
            _sessionService = sessionService;
            logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("PrintingService");
        }

        public void PrintReport(byte[] frxBytes, bool typePrint)
        {
            if (_sessionService.PrinterModel == "Zebra")
            {
                PrintToZebraPrinter(frxBytes, typePrint);
            }
            else
            {
                _notificationService.ShowMessage($"Модель принтера '{_sessionService.PrinterModel}' не поддерживается.");
            }
        }
        public void PrintReportTEST(byte[] frxBytes, bool typePrint)
        {
            PrintToZebraPrinterTEST(frxBytes, typePrint);
            //if (_sessionService.PrinterModel == "Zebra")
            //{
            //    PrintToZebraPrinterTEST(frxBytes, typePrint);
            //}
            //else
            //{
            //    _notificationService.ShowMessage($"Модель принтера '{_sessionService.PrinterModel}' не поддерживается.");
            //}
        }
        private async void PrintToZebraPrinterTEST(byte[] frxBytes, bool typePrint)
        {
            try
            {
                byte[] zplBytes;
                if (typePrint)
                {
                    zplBytes = GenerateZplFromReportBOX(frxBytes);
                }
                else
                {
                    zplBytes = GenerateZplFromReportPALLET(frxBytes);
                }
                string zplString = Encoding.UTF8.GetString(zplBytes);
                _notificationService.ShowMessage($"сформирован zplString");
                //PrintZpl(zplBytes);
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка принтера: {ex.Message}");
                logger.LogError(ex, "Ошибка при печати на Zebra принтере");
            }
        }
        public async Task<bool> CheckConnectPrinterAsync(string printerIP, string printerModel)
        {
            try
            {
                if (printerModel == "Zebra")
                {
                    return await EnsurePrinterConnectedAsync(printerIP);
                }
                else
                {
                    _notificationService.ShowMessage($"Модель принтера '{printerModel}' не поддерживается.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка подключения к принтеру: {ex.Message}");
                logger.LogError(ex, "Ошибка при проверке подключения к принтеру");
                return false;
            }
        }

        //Убеждаемся, что принтер подключен
        private async Task<bool> EnsurePrinterConnectedAsync(string printerIP)
        {
            // Если принтер уже создан и подключен к другому IP, отключаем его
            if (_labelPrinter != null && _sessionService.PrinterIP != printerIP)
            {
                DisconnectPrinter();
            }

            // Если принтер не создан или отключен, создаем новое подключение
            if (_labelPrinter == null && _sessionService.PrinterIP != null)
            {
                return await ConnectToPrinterAsync(printerIP);
            }

            // Проверяем статус существующего подключения
            return await CheckPrinterStatusAsync();
        }

        private async Task<bool> ConnectToPrinterAsync(string printerIP)
        {
            try
            {
                var printerLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("ZebraPrinter");

                var connection = new MD.Aggregation.Devices.Tcp.Configuration()
                {
                    Ip = printerIP,
                    Port = 9100 // порт для Zebra принтеров
                };

                IConfiguration conf = new ConfigurationBuilder()
                    .AddObject(connection, "Connection")
                    .Build();

                _labelPrinter = new LabelPrinter("ZebraPrinter", printerLogger);
                _labelPrinter.StatusReceived += Printer_StatusReceived;

                _notificationService.ShowMessage("Подключение к принтеру...");

                _labelPrinter.Configure(conf);
                _labelPrinter.StartWork();

                // Ждем инициализации с таймаутом
                var timeout = TimeSpan.FromSeconds(10);
                var startTime = DateTime.Now;

                while (_labelPrinter.Status == MD.Aggregation.Devices.DeviceStatusCode.StartingUp &&
                       DateTime.Now - startTime < timeout)
                {
                    await Task.Delay(100);
                }

                if (_labelPrinter.Status == MD.Aggregation.Devices.DeviceStatusCode.Ready)
                {
                    _notificationService.ShowMessage("Принтер успешно подключен");
                    return true;
                }
                else
                {
                    _notificationService.ShowMessage($"Ошибка подключения. Статус: {_labelPrinter.Status}");
                    DisconnectPrinter();
                    return false;
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка подключения к принтеру: {ex.Message}");
                logger.LogError(ex, "Ошибка при подключении к принтеру");
                DisconnectPrinter();
                return false;
            }
        }

        private async Task<bool> CheckPrinterStatusAsync()
        {
            if (_labelPrinter == null) return false;

            try
            {
                // Запрашиваем актуальный статус
                _labelPrinter.RequestStatus();

                // Даем время на получение ответа
                await Task.Delay(500);

                switch (_labelPrinter.Status)
                {
                    case MD.Aggregation.Devices.DeviceStatusCode.Ready:
                        return true;

                    case MD.Aggregation.Devices.DeviceStatusCode.StartingUp:
                        return false;

                    case MD.Aggregation.Devices.DeviceStatusCode.Fail:
                        _notificationService.ShowMessage("Принтер недоступен, переподключение...");
                        DisconnectPrinter();
                        return await ConnectToPrinterAsync(_sessionService.PrinterIP);

                    case MD.Aggregation.Devices.DeviceStatusCode.Unknow:
                        _notificationService.ShowMessage("Неизвестное состояние принтера");
                        return false;

                    default:
                        _notificationService.ShowMessage($"Статус принтера: {_labelPrinter.Status}");
                        return false;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при проверке статуса принтера");
                return false;
            }
        }

        private void Printer_StatusReceived(object? sender, MD.Aggregation.Devices.DeviceStatusEventArgs e)
        {
            logger.LogDebug($"Получен статус принтера: {e.NewStatus}");

            // Можно добавить дополнительную логику обработки изменения статуса
            switch (e.NewStatus)
            {
                case MD.Aggregation.Devices.DeviceStatusCode.Fail:
                    _notificationService.ShowMessage("Принтер недоступен");
                    DisconnectPrinter();
                    break;
                case MD.Aggregation.Devices.DeviceStatusCode.Ready:
                    logger.LogDebug("Принтер готов к работе");
                    break;
            }
        }

        private void DisconnectPrinter()
        {
            if (_labelPrinter != null)
            {
                try
                {
                    _labelPrinter.StatusReceived -= Printer_StatusReceived;
                    _labelPrinter.StopWork();
                    _labelPrinter.Release();
                    _labelPrinter = null;
                    _notificationService.ShowMessage("Принтер отключен");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Ошибка при отключении принтера");
                    _labelPrinter = null;
                }
            }
        }

        private async void PrintToZebraPrinter(byte[] frxBytes, bool typePrint)
        {
            try
            {
                // Убеждаемся, что принтер подключен
                if (!await EnsurePrinterConnectedAsync(_sessionService.PrinterIP))
                {
                    _notificationService.ShowMessage("Не удалось подключиться к принтеру");
                    return;
                }

                // Ждем готовности принтера
                if (!await WaitForPrinterReadyAsync())
                {
                    _notificationService.ShowMessage("Принтер не готов к печати");
                    return;
                }

                byte[] zplBytes;
                if (typePrint)
                {
                    zplBytes = GenerateZplFromReportBOX(frxBytes);
                }
                else
                {
                    zplBytes = GenerateZplFromReportPALLET(frxBytes);
                }

                PrintZpl(zplBytes);
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка принтера: {ex.Message}");
                logger.LogError(ex, "Ошибка при печати на Zebra принтере");
            }
        }

        private async Task<bool> WaitForPrinterReadyAsync(int timeoutSeconds = 30)
        {
            if (_labelPrinter == null) return false;

            var timeout = TimeSpan.FromSeconds(timeoutSeconds);
            var startTime = DateTime.Now;

            while (DateTime.Now - startTime < timeout)
            {
                await CheckPrinterStatusAsync();

                if (_labelPrinter?.Status == MD.Aggregation.Devices.DeviceStatusCode.Ready)
                {
                    return true;
                }

                if (_labelPrinter?.Status == MD.Aggregation.Devices.DeviceStatusCode.Fail)
                {
                    return false;
                }

                await Task.Delay(1000);
            }

            return false;
        }

        private void PrintZpl(byte[] zplBytes)
        {
            if (_labelPrinter == null)
            {
                _notificationService.ShowMessage("Принтер не подключен");
                return;
            }

            try
            {
                string zplString = Encoding.UTF8.GetString(zplBytes);

                if (_labelPrinter.SendLabel(zplString))
                {
                    _notificationService.ShowMessage($"Данные отправлены на принтер");
                    _notificationService.ShowMessage($"Состояние устройства: {_labelPrinter.Status}");
                }
                else
                {
                    _notificationService.ShowMessage($"Ошибка отправки данных на принтер");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка печати: {ex.Message}");
                logger.LogError(ex, "Ошибка при отправке ZPL команд на принтер");
                throw;
            }
        }

        private byte[] GenerateZplFromReportBOX(byte[] frxBytes)
        {
            using var report = new Report();
            using (var ms = new MemoryStream(frxBytes))
            {
                report.Load(ms);
            }

            var labelData = new
            {
                DISPLAY_BAR_CODE = _sessionService.SelectedTaskSscc.DISPLAY_BAR_CODE,
                IN_BOX_QTY = _sessionService.SelectedTaskInfo.IN_BOX_QTY,
                MNF_DATE = _sessionService.SelectedTaskInfo.MNF_DATE_VAL,
                EXPIRE_DATE = _sessionService.SelectedTaskInfo.EXPIRE_DATE_VAL,
                SERIES_NAME = _sessionService.SelectedTaskInfo.SERIES_NAME,
                LEVEL_QTY = 0,
                CNT = 0
            };

            report.RegisterData(new List<object> { labelData }, "LabelQry");
            report.GetDataSource("LabelQry").Enabled = true;
            report.Prepare();

            var exporter = new ZplExport();
            exporter.Density = ZplExport.ZplDensity.d12_dpmm_300_dpi;
            using var exportStream = new MemoryStream();
            exporter.Export(report, exportStream);

            return exportStream.ToArray();
        }

        private byte[] GenerateZplFromReportPALLET(byte[] frxBytes)
        {
            using var report = new Report();
            using (var ms = new MemoryStream(frxBytes))
            {
                report.Load(ms);
            }

            var labelData = new
            {
                DISPLAY_BAR_CODE = _sessionService.SelectedTaskSscc.DISPLAY_BAR_CODE,
                SERIES_NAME = _sessionService.SelectedTaskInfo.SERIES_NAME,
                CNT = 0
            };

            report.RegisterData(new List<object> { labelData }, "LabelQry");
            report.GetDataSource("LabelQry").Enabled = true;
            report.Prepare();

            var exporter = new ZplExport();
            exporter.Density = ZplExport.ZplDensity.d12_dpmm_300_dpi;
            using var exportStream = new MemoryStream();
            exporter.Export(report, exportStream);

            return exportStream.ToArray();
        }
        private async Task<byte[]> LoadTestTemplateAsync()
        {
            try
            {
                // Попытка загрузить из встроенного ресурса
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "l2l_aggregator.Resources.TestTemplate.txt";

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            string base64Template = await reader.ReadToEndAsync();
                            if (!string.IsNullOrEmpty(base64Template))
                            {
                                byte[] bytes = Convert.FromBase64String(base64Template.Trim());
                                string decodedString = Encoding.UTF8.GetString(bytes);
                                return Encoding.UTF8.GetBytes(decodedString);
                            }
                        }
                    }
                }

                // Если встроенный ресурс не найден, попытка загрузить из файла
                string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "TestTemplate.txt");

                if (File.Exists(templatePath))
                {
                    string base64Template = await File.ReadAllTextAsync(templatePath);
                    if (!string.IsNullOrEmpty(base64Template))
                    {
                        byte[] bytes = Convert.FromBase64String(base64Template);
                        string decodedString = Encoding.UTF8.GetString(bytes);
                        return Encoding.UTF8.GetBytes(decodedString);
                    }
                }

                logger.LogWarning("Тестовый шаблон не найден ни в ресурсах, ни в файле");
                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при загрузке тестового шаблона");
                return null;
            }
        }
        private byte[] GetTestTemplate()
        {
            try
            {
                if (!string.IsNullOrEmpty(TEST_TEMPLATE_BASE64))
                {
                    byte[] bytes = Convert.FromBase64String(TEST_TEMPLATE_BASE64);
                    string decodedString = Encoding.UTF8.GetString(bytes);
                    return Encoding.UTF8.GetBytes(decodedString);
                }

                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при получении тестового шаблона");
                return null;
            }
        }
        public async Task PrintTestLabel()
        {
            try
            {
                if (_sessionService.PrinterModel == "Zebra")
                {
                    _notificationService.ShowMessage("Запуск тестовой печати...");

                    // Убеждаемся, что принтер подключен
                    if (!await EnsurePrinterConnectedAsync(_sessionService.PrinterIP))
                    {
                        _notificationService.ShowMessage("Не удалось подключиться к принтеру для тестовой печати");
                        return;
                    }

                    byte[] templateBytes = await LoadTestTemplateAsync();

                    if (templateBytes != null && templateBytes.Length > 0)
                    {
                        await PrintTestToZebraPrinterAsync(templateBytes);
                        _notificationService.ShowMessage("Тестовая печать завершена");
                    }
                    else
                    {
                        _notificationService.ShowMessage("Ошибка: не удалось загрузить тестовый шаблон");
                    }
                }
                else
                {
                    _notificationService.ShowMessage($"Модель принтера '{_sessionService.PrinterModel}' не поддерживается для тестовой печати.");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка тестовой печати: {ex.Message}");
                logger.LogError(ex, "Ошибка при тестовой печати");
            }
        }



        private async Task PrintTestToZebraPrinterAsync(byte[] frxBytes)
        {
            try
            {
                // Ждем готовности принтера
                if (!await WaitForPrinterReadyAsync())
                {
                    _notificationService.ShowMessage("Принтер не готов к тестовой печати");
                    return;
                }

                byte[] zplBytes = GenerateZplFromTestReport(frxBytes);
                PrintZpl(zplBytes);
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка принтера при тестовой печати: {ex.Message}");
                logger.LogError(ex, "Ошибка при тестовой печати на Zebra принтере");
            }
        }

        private byte[] GenerateZplFromTestReport(byte[] frxBytes)
        {
            using var report = new Report();

            using (var ms = new MemoryStream(frxBytes))
            {
                report.Load(ms);
            }

            var labelData = new
            {
                DISPLAY_BAR_CODE = "(00)046039059900003727",
                IN_BOX_QTY = "*1*",
                MNF_DATE = "*06 25*",
                EXPIRE_DATE = "*06 28*",
                SERIES_NAME = "*TEST30Х30*",
                LEVEL_QTY = 0,
                CNT = 0
            };

            report.RegisterData(new List<object> { labelData }, "LabelQry");
            report.GetDataSource("LabelQry").Enabled = true;
            report.Prepare();

            var exporter = new ZplExport();
            exporter.Density = ZplExport.ZplDensity.d12_dpmm_300_dpi;
            using var exportStream = new MemoryStream();
            exporter.Export(report, exportStream);

            return exportStream.ToArray();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                DisconnectPrinter();
                _disposed = true;
            }
        }
    }
}