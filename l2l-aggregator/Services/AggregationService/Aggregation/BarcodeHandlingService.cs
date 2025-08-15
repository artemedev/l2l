using l2l_aggregator.Models;
using l2l_aggregator.Services.GS1ParserService;
using l2l_aggregator.Services.Notification.Interface;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace l2l_aggregator.Services.AggregationService
{
    public class BarcodeHandlingService
    {
        private readonly SessionService _sessionService;
        private readonly DatabaseDataService _databaseDataService;
        private readonly INotificationService _notificationService;
        private readonly TextGenerationService _textGenerationService;
        private readonly IDialogService _dialogService;

        public event Action<string>? InfoModeTextUpdated;
        public event Action<string>? DisaggregationModeTextUpdated;
        public event Action? BoxAggregationCompleted;
        public event Action? DisaggregationCompleted;

        public BarcodeHandlingService(
            SessionService sessionService,
            DatabaseDataService databaseDataService,
            INotificationService notificationService,
            TextGenerationService textGenerationService,
            IDialogService dialogService)
        {
            _sessionService = sessionService;
            _databaseDataService = databaseDataService;
            _notificationService = notificationService;
            _textGenerationService = textGenerationService;
            _dialogService = dialogService;
        }

        public async Task HandleInfoModeBarcodeAsync(string barcode)
        {
            try
            {
                var ssccRecord = _databaseDataService.FindSsccCode(barcode);
                if (ssccRecord != null)
                {
                    var infoText = _textGenerationService.BuildSsccInfo(ssccRecord);
                    InfoModeTextUpdated?.Invoke(infoText);
                    _notificationService.ShowMessage($"Найден SSCC код: {GetSsccTypeDescription(ssccRecord.TYPEID)}", NotificationType.Success);
                    return;
                }

                var gS1Parser = new GS1Parser();
                var newGS = gS1Parser.ParseGTIN(barcode);
                var parsedData = newGS.SerialNumber;

                var unRecord = _databaseDataService.FindUnCode(parsedData);
                if (unRecord != null)
                {
                    var infoText = _textGenerationService.BuildUnInfo(unRecord);
                    InfoModeTextUpdated?.Invoke(infoText);
                    _notificationService.ShowMessage($"Найден UN код: {GetUnTypeDescription(unRecord.UN_TYPE)}", NotificationType.Success);
                    return;
                }

                var notFoundText = _textGenerationService.BuildCodeNotFoundInfo(barcode);
                InfoModeTextUpdated?.Invoke(notFoundText);
                _notificationService.ShowMessage($"Код {barcode} не найден в базе данных", NotificationType.Warning);
            }
            catch (Exception ex)
            {
                var errorText = _textGenerationService.BuildErrorInfo(barcode, ex.Message);
                InfoModeTextUpdated?.Invoke(errorText);
                _notificationService.ShowMessage($"Ошибка поиска кода: {ex.Message}", NotificationType.Error);
            }
        }

        public async Task HandleDisaggregationModeBarcodeAsync(string barcode)
        {
            try
            {
                var validation = ValidateSessionData();
                if (!validation.IsValid)
                {
                    var errorText = _textGenerationService.BuildErrorInfo(barcode, validation.ErrorMessage);
                    DisaggregationModeTextUpdated?.Invoke(errorText);
                    return;
                }

                var boxRecord = _sessionService.CachedSsccResponse?.RECORDSET
                    .Where(r => r.TYPEID == (int)SsccType.Box)
                    .FirstOrDefault(r => r.CHECK_BAR_CODE == barcode);

                if (boxRecord != null)
                {
                    await ProcessDisaggregationRequestAsync(barcode, boxRecord);
                }
                else
                {
                    var notFoundText = _textGenerationService.BuildBoxNotFoundInfo(barcode);
                    DisaggregationModeTextUpdated?.Invoke(notFoundText);
                    _notificationService.ShowMessage($"Код коробки {barcode} не найден", NotificationType.Warning);
                }
            }
            catch (Exception ex)
            {
                var errorText = _textGenerationService.BuildErrorInfo(barcode, ex.Message);
                DisaggregationModeTextUpdated?.Invoke(errorText);
                _notificationService.ShowMessage($"Ошибка очистки короба: {ex.Message}", NotificationType.Error);
            }
        }

        public void HandleNormalModeBarcode(string barcode, AggregationStep currentStep)
        {
            if (currentStep != AggregationStep.BoxAggregation)
                return;

            var validation = ValidateSessionData();
            if (!validation.IsValid)
            {
                _notificationService.ShowMessage(validation.ErrorMessage, NotificationType.Error);
                return;
            }

            ProcessNormalModeBarcode(barcode);
        }

        private void ProcessNormalModeBarcode(string barcode)
        {
            var foundRecord = _sessionService.CachedSsccResponse?.RECORDSET
                .Where(r => r.TYPEID == (int)SsccType.Box)
                .FirstOrDefault(r => r.CHECK_BAR_CODE == barcode);

            if (foundRecord != null)
            {
                HandleFoundBarcode(barcode, foundRecord);
            }
            else
            {
                _notificationService.ShowMessage($"ШК {barcode} не найден в списке!", NotificationType.Error);
            }
        }

        private void HandleFoundBarcode(string barcode, ArmJobSsccRecord foundRecord)
        {
            // Проверяем, есть ли уже агрегированные коды в отсканированной коробке
            if (foundRecord.QTY > 0)
            {
                _notificationService.ShowMessage($"Коробка с ШК {barcode} уже содержит {foundRecord.QTY} агрегированных кодов!", NotificationType.Error);
                return;
            }

            // Используем ОТСКАНИРОВАННУЮ коробку
            _sessionService.SelectedTaskSscc = foundRecord;
            _notificationService.ShowMessage($"Коробка с ШК {barcode} готова для агрегации", NotificationType.Success);

            // Сохраняем агрегацию в ОТСКАНИРОВАННУЮ коробку
            if (SaveAllDmCells())
            {
                ProcessSuccessfulAggregation();
            }
        }

        private bool SaveAllDmCells()
        {
            try
            {
                var aggregationData = new System.Collections.Generic.List<(string UNID, string CHECK_BAR_CODE)>();
                var gS1Parser = new GS1Parser();

                foreach (var dmCode in _sessionService.CurrentBoxDmCodes.Where(c => !string.IsNullOrWhiteSpace(c)))
                {
                    var parsedData = gS1Parser.ParseGTIN(dmCode);

                    if (!string.IsNullOrWhiteSpace(parsedData.SerialNumber))
                    {
                        aggregationData.Add((parsedData.SerialNumber, _sessionService.SelectedTaskSscc.CHECK_BAR_CODE));
                    }
                    else
                    {
                        _notificationService.ShowMessage($"Не найден SGTIN для серийного номера: {parsedData.SerialNumber}", NotificationType.Warning);
                    }
                }

                if (aggregationData.Count > 0)
                {
                    var success = _databaseDataService.LogAggregationCompletedBatch(aggregationData);

                    if (success)
                    {
                        _notificationService.ShowMessage($"Сохранено {aggregationData.Count} кодов агрегации", NotificationType.Success);
                        return true;
                    }
                    else
                    {
                        _notificationService.ShowMessage("Ошибка при сохранении кодов агрегации", NotificationType.Error);
                        return false;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка сохранения кодов агрегации: {ex.Message}", NotificationType.Error);
                return false;
            }
        }

        private void ProcessSuccessfulAggregation()
        {
            var gS1Parser = new GS1Parser();

            foreach (var code in _sessionService.CurrentBoxDmCodes.Where(c => !string.IsNullOrWhiteSpace(c)))
            {
                var parsedData = gS1Parser.ParseGTIN(code);
                if (!string.IsNullOrWhiteSpace(parsedData.SerialNumber))
                {
                    _sessionService.AllScannedDmCodes.Add(parsedData.SerialNumber);
                }
            }

            _sessionService.ClearCurrentBoxCodes();
            BoxAggregationCompleted?.Invoke();
        }

        private async Task ProcessDisaggregationRequestAsync(string barcode, ArmJobSsccRecord boxRecord)
        {
            var confirmed = await _dialogService.ShowCustomConfirmationAsync(
                "Подтверждение очистки короба",
                $"Выполнить очистку короба коробки с кодом {barcode}?",
                Material.Icons.MaterialIconKind.PackageVariantClosed,
                Avalonia.Media.Brushes.Orange,
                Avalonia.Media.Brushes.Orange,
                "Да,очистить короб",
                "Отмена"
            );

            if (confirmed)
            {
                await ExecuteDisaggregationAsync(barcode, boxRecord);
            }
            else
            {
                var cancelledText = BuildDisaggregationCancelledInfo(barcode);
                DisaggregationModeTextUpdated?.Invoke(cancelledText);
            }
        }

        private async Task ExecuteDisaggregationAsync(string barcode, ArmJobSsccRecord boxRecord)
        {
            var success = _databaseDataService.ClearBoxAggregation(boxRecord.CHECK_BAR_CODE);

            if (success)
            {
                var successText = _textGenerationService.BuildDisaggregationSuccessInfo(barcode, boxRecord);
                DisaggregationModeTextUpdated?.Invoke(successText);
                _notificationService.ShowMessage($"Коробка с кодом {barcode} успешно разагрегирована", NotificationType.Success);
                DisaggregationCompleted?.Invoke();
            }
            else
            {
                var failureText = _textGenerationService.BuildDisaggregationFailureInfo(barcode, boxRecord);
                DisaggregationModeTextUpdated?.Invoke(failureText);
                _notificationService.ShowMessage($"Ошибка очистки короба, коробки с кодом {barcode}", NotificationType.Error);
            }
        }

        private string BuildDisaggregationCancelledInfo(string barcode)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Очистка короба отменена пользователем.");
            sb.AppendLine();
            sb.AppendLine($"Отсканированный код: {barcode}");
            return sb.ToString();
        }

        private (bool IsValid, string ErrorMessage) ValidateSessionData()
        {
            if (_sessionService.SelectedTaskInfo == null)
                return (false, "Отсутствует информация о задании.");

            if (_sessionService.CachedSsccResponse?.RECORDSET == null || !_sessionService.CachedSsccResponse.RECORDSET.Any())
                return (false, "Данные SSCC отсутствуют.");

            return (true, "");
        }

        private static string GetSsccTypeDescription(int typeId) => typeId switch
        {
            (int)SsccType.Box => "Коробка",
            (int)SsccType.Pallet => "Паллета",
            _ => $"Неизвестный тип ({typeId})"
        };

        private static string GetUnTypeDescription(int? typeId) => typeId switch
        {
            1 => "Потребительская упаковка",
            _ => $"Неизвестный тип ({typeId})"
        };
    }
}