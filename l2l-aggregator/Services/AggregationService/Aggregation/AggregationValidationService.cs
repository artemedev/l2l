using l2l_aggregator.Models;
using System.Linq;

namespace l2l_aggregator.Services.AggregationService
{
    public record ValidationResult(bool IsValid, string? ErrorMessage)
    {
        public static ValidationResult Success() => new(true, null);
        public static ValidationResult Error(string message) => new(false, message);
    }

    public class AggregationValidationService
    {
        private readonly SessionService _sessionService;

        public AggregationValidationService(SessionService sessionService)
        {
            _sessionService = sessionService;
        }

        public ValidationResult ValidateTaskInfo()
        {
            if (_sessionService.SelectedTaskInfo == null)
                return ValidationResult.Error("Отсутствует информация о задании.");

            return ValidationResult.Success();
        }

        public ValidationResult ValidateSessionData()
        {
            var taskValidation = ValidateTaskInfo();
            if (!taskValidation.IsValid)
                return taskValidation;

            if (_sessionService.CachedSsccResponse?.RECORDSET == null || !_sessionService.CachedSsccResponse.RECORDSET.Any())
                return ValidationResult.Error("Данные SSCC отсутствуют.");

            return ValidationResult.Success();
        }

        public ValidationResult ValidateBoxLabelPrinting(byte[] frxBoxBytes)
        {
            if (frxBoxBytes == null || frxBoxBytes.Length == 0)
            {
                return ValidationResult.Error("Шаблон коробки не загружен.");
            }

            if (_sessionService.CachedSsccResponse == null)
            {
                return ValidationResult.Error("SSCC данные не загружены.");
            }

            return ValidationResult.Success();
        }

        public ValidationResult ValidateScanningParameters()
        {
            var taskValidation = ValidateTaskInfo();
            if (!taskValidation.IsValid)
                return taskValidation;

            if (string.IsNullOrWhiteSpace(_sessionService.CameraIP))
                return ValidationResult.Error("IP камеры не задан.");

            if (string.IsNullOrWhiteSpace(_sessionService.CameraModel))
                return ValidationResult.Error("Модель камеры не задана.");

            return ValidationResult.Success();
        }

        public ValidationResult ValidateControllerConnection()
        {
            if (!_sessionService.CheckController)
                return ValidationResult.Success();

            if (string.IsNullOrWhiteSpace(_sessionService.ControllerIP))
                return ValidationResult.Error("IP контроллера не задан.");

            return ValidationResult.Success();
        }

        public ValidationResult ValidateAggregationCompletion()
        {
            var taskValidation = ValidateTaskInfo();
            if (!taskValidation.IsValid)
                return taskValidation;

            if (_sessionService.CurrentBoxDmCodes == null || !_sessionService.CurrentBoxDmCodes.Any())
                return ValidationResult.Error("Нет кодов для агрегации.");

            return ValidationResult.Success();
        }

        public ValidationResult ValidateLayerParameters()
        {
            var taskValidation = ValidateTaskInfo();
            if (!taskValidation.IsValid)
                return taskValidation;

            var inBoxQty = _sessionService.SelectedTaskInfo.IN_BOX_QTY ?? 0;
            var layersQty = _sessionService.SelectedTaskInfo.LAYERS_QTY ?? 0;

            if (layersQty <= 0)
                return ValidationResult.Error("Некорректное количество слоев (LAYERS_QTY).");

            if (inBoxQty <= 0)
                return ValidationResult.Error("Некорректное количество пачек в коробке (IN_BOX_QTY).");

            return ValidationResult.Success();
        }

        public ValidationResult ValidatePositioningParameters()
        {
            var taskValidation = ValidateTaskInfo();
            if (!taskValidation.IsValid)
                return taskValidation;

            var packHeight = _sessionService.SelectedTaskInfo.PACK_HEIGHT ?? 0;
            var layersQty = _sessionService.SelectedTaskInfo.LAYERS_QTY ?? 0;

            if (packHeight == 0)
                return ValidationResult.Error("Не задана высота слоя (PACK_HEIGHT).");

            if (layersQty == 0)
                return ValidationResult.Error("Не задано количество слоёв (LAYERS_QTY).");

            return ValidationResult.Success();
        }

        public bool ShouldPrintFullBox(AggregationMetrics metrics, int numberOfLayers)
        {
            return metrics.ValidCount == metrics.TotalCells &&
                   metrics.TotalCells > 0 &&
                   metrics.ValidCount == numberOfLayers;
        }

        public bool ShouldShowPartialBoxConfirmation(AggregationMetrics metrics, int numberOfLayers)
        {
            return metrics.ValidCount == metrics.TotalCells &&
                   metrics.TotalCells > 0 &&
                   metrics.ValidCount < numberOfLayers;
        }
    }
}