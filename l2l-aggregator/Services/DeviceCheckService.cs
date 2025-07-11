using DM_wraper_NS;
using l2l_aggregator.Services.ControllerService;
using l2l_aggregator.Services.DmProcessing;
using l2l_aggregator.Services.Printing;
using l2l_aggregator.Services.ScannerService.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace l2l_aggregator.Services
{
    public class DeviceCheckService
    {
        private readonly DmScanService _dmScanService;
        private readonly IScannerPortResolver _scannerPortResolver;
        private readonly PrintingService _printingService;
        private readonly ILogger<PcPlcConnectionService> _logger;

        public DeviceCheckService(DmScanService dmScanService, IScannerPortResolver scannerPortResolver, PrintingService printingService, ILogger<PcPlcConnectionService> logger)
        {
            _dmScanService = dmScanService;
            _scannerPortResolver = scannerPortResolver;
            _printingService = printingService;//-----------------------------------
            _logger = logger;
        }

        public async Task<(bool Success, string Message)> CheckCameraAsync(SessionService session)
        {
            if (!session.CheckCamera)
                return (true, null);

            if (string.IsNullOrWhiteSpace(session.CameraIP))
                return (false, "IP камеры не задан!");

            try
            {
                var recognParams = new recogn_params
                {
                    CamInterfaces = "GigEVision2",
                    cameraName = session.CameraIP,
                    _Preset = new camera_preset(session.CameraModel),
                    softwareTrigger = true,
                    hardwareTrigger = false,
                    DMRecogn = true,
                    OCRRecogn = true,
                    packRecogn = true
                };

                var success = _dmScanService.ConfigureParams(recognParams);
                if (!success)
                    return (false, "Не удалось настроить камеру");

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка настройки камеры: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> CheckPrinterAsync(SessionService sessionService)
        {
            if (!sessionService.CheckPrinter)
                return (true, null);

            if (string.IsNullOrWhiteSpace(sessionService.PrinterIP))
                return (false, "IP принтера не задан!");

            try
            {
                _printingService.CheckConnectPrinter(sessionService.PrinterIP, sessionService.PrinterModel);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка принтера: {ex.Message}");
            }

        }

        public async Task<(bool Success, string Message)> CheckControllerAsync(SessionService session)
        {
            if (!session.CheckController)
                return (true, null);

            if (string.IsNullOrWhiteSpace(session.ControllerIP))
                return (false, "IP контроллера не задан!");


             try
            {
                // Создать службу подключения PLC
                var plcService = new PcPlcConnectionService(_logger); // Внедрение ILogger

                bool connected = await plcService.ConnectAsync(session.ControllerIP);
                if (!connected)
                {
                    return (false, $"Не удалось подключиться к контроллеру {session.ControllerIP}!");
                }

                // Одиночный тест пинг-понга
                bool pingPongResult = await plcService.TestConnectionAsync();

                // Закрыть подключение 
                plcService.Disconnect();
                plcService.Dispose();

                return pingPongResult
                            ? (true, null)
                    : (false, $"Контроллер {session.ControllerIP} не отвечает (пинг-понг)!");
            }
            catch
            {
                return (false, $"Контроллер {session.ControllerIP} недоступен!");
            }
        }

        public Task<(bool Success, string Message)> CheckScannerAsync(SessionService session)
        {
            if (!session.CheckScanner)
                return Task.FromResult((true, (string)null));

            if (string.IsNullOrWhiteSpace(session.ScannerPort))
                return Task.FromResult((false, "Порт сканера не задан!"));

            if (session.ScannerModel != "Honeywell")
                return Task.FromResult((false, $"Сканер модели '{session.ScannerModel}' не поддерживается."));

            var availablePorts = _scannerPortResolver.GetHoneywellScannerPorts();
            return Task.FromResult(availablePorts.Contains(session.ScannerPort)
                ? (true, (string)null)
                : (false, $"Сканер на порту {session.ScannerPort} недоступен!"));
        }
    }

}
