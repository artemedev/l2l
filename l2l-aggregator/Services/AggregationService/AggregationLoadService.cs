using l2l_aggregator.Services.Notification.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace l2l_aggregator.Services.AggregationService
{
    public class AggregationLoadService
    {
        private readonly DeviceCheckService _deviceCheckService;
        private readonly SessionService _sessionService;
        private readonly INotificationService _notificationService;
        private readonly DatabaseDataService _databaseDataService;
        private readonly AggregationValidationService _validationService;

        public AggregationLoadService(
            DeviceCheckService deviceCheckService,
            SessionService sessionService,
            INotificationService notificationService,
            DatabaseDataService databaseDataService,
            AggregationValidationService validationService)
        {
            _deviceCheckService = deviceCheckService;
            _sessionService = sessionService;
            _notificationService = notificationService;
            _databaseDataService = databaseDataService;
            _validationService = validationService;
        }

        public async Task<bool> LoadAggregation(long? currentTaskId)
        {
            if (currentTaskId == null || currentTaskId == 0)
            {
                return false;
            }

            var results = new List<(bool Success, string Message)>
            {
                await _deviceCheckService.CheckCameraAsync(_sessionService),
                await _deviceCheckService.CheckPrinterAsync(_sessionService),
                await _deviceCheckService.CheckControllerAsync(_sessionService),
                await _deviceCheckService.CheckScannerAsync(_sessionService)
            };

            var errors = results.Where(r => !r.Success).Select(r => r.Message).ToList();
            if (errors.Any())
            {
                foreach (var msg in errors)
                    _notificationService.ShowMessage(msg);
                return false;
            }

            var infoMessage = "Загружаем детальную информацию о задаче...";
            _notificationService.ShowMessage(infoMessage, NotificationType.Info);

            // Загружаем детальную информацию о задаче
            _sessionService.SelectedTaskInfo = await _databaseDataService.GetJobDetails(currentTaskId.Value);

            var validation = _validationService.AllTaskValidate();
            if (!validation.IsValid)
            {
                _notificationService.ShowMessage(validation.ErrorMessage, NotificationType.Error);
                return false;
            }

            // Загружаем данные SSCC
            if (!await LoadSsccDataAsync(currentTaskId.Value))
            {
                return false;
            }

            // Загружаем данные SGTIN
            if (!await LoadSgtinDataAsync(currentTaskId.Value))
            {
                return false;
            }

            if (!await InitializeScannedCodesAsync())
            {
                return false;
            }

            _notificationService.ShowMessage("Обнаружена незавершённая агрегация. Продолжаем...");
            return true;
        }

        private async Task<bool> LoadSsccDataAsync(long docId)
        {
            try
            {
                _sessionService.CachedSsccResponse = await _databaseDataService.GetSscc(docId);
                if (_validationService.ValidateSessionData().IsValid)
                {
                    // Сохраняем первую запись SSCC в сессию
                    _sessionService.SelectedTaskSscc = await _databaseDataService.ReserveFreeBox();
                    var infoMessage = "SSCC данные загружены успешно.";
                    _notificationService.ShowMessage(infoMessage, NotificationType.Success);
                    return true;
                }
                else
                {
                    var infoMessage = "Не удалось загрузить SSCC данные.";
                    _notificationService.ShowMessage(infoMessage, NotificationType.Warning);
                    return false;
                }
            }
            catch (Exception ex)
            {
                var infoMessage = $"Ошибка загрузки SSCC данных: {ex.Message}";
                _notificationService.ShowMessage(infoMessage, NotificationType.Error);
                return false;
            }
        }

        private async Task<bool> LoadSgtinDataAsync(long docId)
        {
            try
            {
                _sessionService.CachedSgtinResponse = await _databaseDataService.GetSgtin(docId);
                if (_sessionService.CachedSgtinResponse.RECORDSET.Any())
                {
                    var infoMessage = "SGTIN данные загружены успешно.";
                    _notificationService.ShowMessage(infoMessage, NotificationType.Success);
                    return true;
                }
                else
                {
                    var infoMessage = "Не удалось загрузить SGTIN данные.";
                    _notificationService.ShowMessage(infoMessage, NotificationType.Warning);
                    return false;
                }
            }
            catch (Exception ex)
            {
                var infoMessage = $"Ошибка загрузки SGTIN данных: {ex.Message}";
                _notificationService.ShowMessage(infoMessage, NotificationType.Error);
                return false;
            }
        }

        private async Task<bool> InitializeScannedCodesAsync()
        {
            try
            {
                var aggregatedCodes = await _databaseDataService.GetAggregatedUnCodes();

                if (aggregatedCodes?.Any() == true)
                {
                    _sessionService.ClearScannedCodes();

                    foreach (var code in aggregatedCodes.Where(c => !string.IsNullOrWhiteSpace(c)))
                    {
                        _sessionService.AllScannedDmCodes.Add(code);
                    }
                    _notificationService.ShowMessage($"Загружено {aggregatedCodes.Count} ранее отсканированных кодов", NotificationType.Info);
                    return true;
                }
                else
                {
                    _sessionService.ClearScannedCodes();
                    return true;
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка загрузки отсканированных кодов: {ex.Message}", NotificationType.Error);
                _sessionService.ClearScannedCodes();
                return false;
            }
        }
    }
}