using FastReport;
using FastReport.Export.Zpl;
using l2l_aggregator.Services.Notification.Interface;
using MD.Aggregation.Devices.Printer.ZPL;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace l2l_aggregator.Services.Printing
{
    public class PrintingServiceSecond
    {
        private readonly INotificationService _notificationService;
        private readonly SessionService _sessionService;
        private ILogger logger;
        private const string TEST_TEMPLATE_BASE64 = "";
        private volatile bool statusReceived = false;
        private LabelPrinter _labelPrinter;
        private readonly object _printerLock = new object();
        private bool _disposed = false;

        public PrintingServiceSecond(
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

        public void CheckConnectPrinter(string printerIP, string printerModel)
        {
            try
            {
                if (printerModel == "Zebra")
                {
                    var device = ConnectToZebraPrinter(printerIP);
                    DisconnectToZebraPrinter(device);
                }
                else
                {
                    _notificationService.ShowMessage($"Модель принтера '{printerModel}' не поддерживается.");
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка подключения к принтеру: {ex.Message}");
                throw;
            }
        }

        private LabelPrinter ConnectToZebraPrinter(string printerIP)
        {
            lock (_printerLock)
            {
                if (_labelPrinter == null)
                {
                    var printerLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("ZebraPrinter");

                    var connection = new MD.Aggregation.Devices.Tcp.Configuration()
                    {
                        Ip = printerIP,
                        Port = 9100
                    };

                    IConfiguration conf = new ConfigurationBuilder()
                        .AddObject(connection, "Connection")
                        .Build();

                    _labelPrinter = new LabelPrinter("ZebraPrinter", printerLogger);

                    try
                    {
                        _labelPrinter.StatusReceived += Printer_StatusReceived;
                        _labelPrinter.Configure(conf);
                        _labelPrinter.StartWork();
                        _notificationService.ShowMessage("> Ожидание запуска принтера...");

                        Thread.Sleep(500);

                        _notificationService.ShowMessage("> Принтер успешно запущен");
                        return _labelPrinter;
                    }
                    catch (Exception ex)
                    {
                        _notificationService.ShowMessage($"Ошибка подключения к принтеру: {ex.Message}");
                        CleanupPrinter();
                        throw;
                    }
                }
                else
                {
                    return _labelPrinter;
                }
            }
        }

        private void Printer_StatusReceived(object? sender, MD.Aggregation.Devices.DeviceStatusEventArgs e)
        {
            statusReceived = true;
        }


        private void DisconnectToZebraPrinter(LabelPrinter device)
        {
            if (device == null) return;

            try
            {
                _notificationService.ShowMessage("> Ожидание остановки принтера...");

                // Останавливаем работу принтера
                device.StopWork();

                // Отписываемся от событий
                device.StatusReceived -= Printer_StatusReceived;

                // Освобождаем ресурсы
                device.Release();

                _notificationService.ShowMessage("> Принтер остановлен");
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка остановки принтера: {ex.Message}");
                logger.LogError(ex, "Ошибка при остановке принтера");
                throw;
            }
        }



        private void PrintToZebraPrinter(byte[] frxBytes, bool typePrint)
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

            LabelPrinter? device = null;
            try
            {
                device = ConnectToZebraPrinter(_sessionService.PrinterIP);
                PrintZpl(device, zplBytes);
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка принтера: {ex.Message}");
                logger.LogError(ex, "Ошибка при печати на Zebra принтере");

                // При критической ошибке очищаем принтер для переподключения
                CleanupPrinter();
            }
            finally
            {
                // НЕ отключаем принтер после каждой печати для повышения производительности
                // Принтер будет переиспользоваться для следующих задач печати
            }
        }

        private void PrintZpl(LabelPrinter device, byte[] zplBytes)
        {
            try
            {
                // Преобразуем bytes в string для отправки ZPL команд
                string zplString = Encoding.UTF8.GetString(zplBytes);
                //while (device.Status == MD.Aggregation.Devices.DeviceStatusCode.StartingUp)
                //{
                //    Thread.Sleep(1000);
                //}
                // Печать
                device.SendLabel(zplString);

                //Thread.Sleep(1000); // подождать для завершения отправки
                _notificationService.ShowMessage($"> Данные отправлены на принтер");
                _notificationService.ShowMessage($"> Состояние устройства: {device.Status}");
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

            // Регистрируем данные в отчете
            report.RegisterData(new List<object> { labelData }, "LabelQry");
            report.GetDataSource("LabelQry").Enabled = true;

            // Подготавливаем отчет
            report.Prepare();

            var exporter = new ZplExport();
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

            // Регистрируем данные в отчете
            report.RegisterData(new List<object> { labelData }, "LabelQry");
            report.GetDataSource("LabelQry").Enabled = true;

            // Подготавливаем отчет
            report.Prepare();

            var exporter = new ZplExport();
            using var exportStream = new MemoryStream();
            exporter.Export(report, exportStream);

            return exportStream.ToArray();
        }
        /// <summary>
        /// Функция тестовой печати с использованием встроенного шаблона
        /// </summary>
        public void PrintTestLabel()
        {
            try
            {
                if (_sessionService.PrinterModel == "Zebra")
                {
                    _notificationService.ShowMessage("Запуск тестовой печати...");

                    // Получаем шаблон из файла или base64
                    byte[] templateBytes = GetTestTemplate();

                    if (templateBytes != null && templateBytes.Length > 0)
                    {
                        PrintTestToZebraPrinter(templateBytes);
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
        /// <summary>
        /// Получение тестового шаблона из файла проекта или base64
        /// </summary>
        private byte[] GetTestTemplate()
        {
            try
            {
                // Вариант 1: Из файла в проекте (раскомментировать если нужно)
                // string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "TestTemplate.frx");
                // if (File.Exists(templatePath))
                // {
                //     return File.ReadAllBytes(templatePath);
                // }

                // Вариант 2: Из base64 строки
                if (!string.IsNullOrEmpty(TEST_TEMPLATE_BASE64))
                {
                    byte[] bytes = Convert.FromBase64String(TEST_TEMPLATE_BASE64);
                    string decodedString = Encoding.UTF8.GetString(bytes);
                    return Encoding.UTF8.GetBytes(decodedString);
                }

                //// Вариант 3: Из ресурсов сборки (если шаблон встроен как ресурс)
                //var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                //using var stream = assembly.GetManifestResourceStream("l2l_aggregator.Templates.TestTemplate.frx");
                //if (stream != null)
                //{
                //    using var memoryStream = new MemoryStream();
                //    stream.CopyTo(memoryStream);
                //    return memoryStream.ToArray();
                //}

                return null;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка при получении тестового шаблона");
                return null;
            }
        }

        /// <summary>
        /// Печать тестовой этикетки на Zebra принтер
        /// </summary>
        private void PrintTestToZebraPrinter(byte[] frxBytes)
        {
            byte[] zplBytes = GenerateZplFromTestReport(frxBytes);

            LabelPrinter? device = null;
            try
            {
                device = ConnectToZebraPrinter(_sessionService.PrinterIP);
                PrintZpl(device, zplBytes);
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка принтера при тестовой печати: {ex.Message}");
                logger.LogError(ex, "Ошибка при тестовой печати на Zebra принтере");

                // При критической ошибке очищаем принтер
                CleanupPrinter();
            }
        }

        /// <summary>
        /// Генерация ZPL из тестового отчета с тестовыми данными
        /// </summary>
        private byte[] GenerateZplFromTestReport(byte[] frxBytes)
        {
            using var report = new Report();

            using (var ms = new MemoryStream(frxBytes))
            {
                report.Load(ms);
            }

            // Тестовые данные
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

            // Регистрируем данные в отчете
            report.RegisterData(new List<object> { labelData }, "LabelQry");
            report.GetDataSource("LabelQry").Enabled = true;

            // Подготавливаем отчет
            report.Prepare();

            var exporter = new ZplExport();
            exporter.Density = ZplExport.ZplDensity.d24_dpmm_600_dpi;
            using var exportStream = new MemoryStream();
            exporter.Export(report, exportStream);

            byte[] zplBytes = exportStream.ToArray();
            string zplString = Encoding.UTF8.GetString(zplBytes);


            return exportStream.ToArray();
        }

        /// <summary>
        /// Полная очистка принтера с освобождением всех ресурсов
        /// </summary>
        private void CleanupPrinter()
        {
            lock (_printerLock)
            {
                if (_labelPrinter != null)
                {
                    try
                    {
                        // Отписываемся от событий
                        _labelPrinter.StatusReceived -= Printer_StatusReceived;

                        // Останавливаем работу
                        _labelPrinter.StopWork();

                        // Освобождаем ресурсы
                        _labelPrinter.Release();

                        // Если принтер реализует IDisposable
                        if (_labelPrinter is IDisposable disposablePrinter)
                        {
                            disposablePrinter.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Ошибка при очистке принтера");
                    }
                    finally
                    {
                        _labelPrinter = null;
                    }
                }
            }
        }

        /// <summary>
        /// Реализация IDisposable для корректной очистки ресурсов
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                CleanupPrinter();
                _disposed = true;
            }
        }

        ~PrintingServiceSecond()
        {
            Dispose(false);
        }
        /// <summary>
        /// Принудительное переподключение к принтеру
        /// </summary>
        public void ReconnectPrinter()
        {
            try
            {
                CleanupPrinter();
                _notificationService.ShowMessage("Переподключение к принтеру...");

                // Небольшая задержка перед переподключением
                Thread.Sleep(1000);

                // При следующем обращении принтер будет создан заново
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка переподключения: {ex.Message}");
                logger.LogError(ex, "Ошибка при переподключении к принтеру");
            }
        }
        /// <summary>
        /// Проверка состояния принтера
        /// </summary>
        public bool IsPrinterConnected()
        {
            lock (_printerLock)
            {
                return _labelPrinter != null &&
                       _labelPrinter.Status == MD.Aggregation.Devices.DeviceStatusCode.Ready;
            }
        }
    }
}