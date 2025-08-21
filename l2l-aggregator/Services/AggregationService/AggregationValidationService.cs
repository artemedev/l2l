using l2l_aggregator.Models;
using MD.Aggregation.Marking.Job;
using System.Collections.Generic;
using System.Linq;
using static l2l_aggregator.ViewModels.AggregationViewModel;

namespace l2l_aggregator.Services.AggregationService
{
    public record ValidationResult(bool IsValid, string? ErrorMessage)
    {
        public static ValidationResult Success() => new(true, null);
        public static ValidationResult Error(string message) => new(false, message);
    }
    public record AggregationMetrics(
        int ValidCount,
        int DuplicatesInCurrentScan,
        int DuplicatesInAllScans,
        int TotalCells
    );
    public class AggregationValidationService
    {
        private readonly SessionService _sessionService;

        public AggregationValidationService(SessionService sessionService)
        {
            _sessionService = sessionService;
        }

        public ValidationResult AllTaskValidate()
        {
            var taskValidation = ValidateTaskInfo();
            if (!taskValidation.IsValid)
                return taskValidation;

            var layerValidation = ValidateLayerParameters();
            if (!layerValidation.IsValid)
                return layerValidation;

            var positionValidation = ValidatePositioningParameters();
            if (!positionValidation.IsValid)
                return positionValidation;

            return ValidationResult.Success();
        }

        public ValidationResult ValidateTaskInfo()
        {
            if (_sessionService.SelectedTaskInfo == null)
                return ValidationResult.Error("Отсутствует информация о задании.");

            if (_sessionService.SelectedTaskInfo.BOX_TEMPLATE == null || _sessionService.SelectedTaskInfo.BOX_TEMPLATE.Length == 0)
            {
                return ValidationResult.Error("Ошибка: шаблон коробки отсутствует.");
            }

            if (_sessionService.SelectedTaskInfo.UN_TEMPLATE_FR == null || _sessionService.SelectedTaskInfo.UN_TEMPLATE_FR.Length == 0)
            {
                return ValidationResult.Error("Ошибка: шаблон распознавания отсутствует.");
            }

            if ((_sessionService.SelectedTaskInfo.DOCID ?? 0) == 0)
            {
                return ValidationResult.Error("Ошибка: отсутствует информация о задании для загрузки отсканированных кодов.");
            }

            return ValidationResult.Success();
        }
        public ValidationResult ValidateSessionData()
        {
            if (_sessionService.CachedSsccResponse?.RECORDSET == null || !_sessionService.CachedSsccResponse.RECORDSET.Any())
                return ValidationResult.Error("Данные SSCC отсутствуют.");

            return ValidationResult.Success();
        }
        public ValidationResult ValidateLayerParameters()
        {

            var inBoxQty = _sessionService.SelectedTaskInfo.IN_BOX_QTY ?? 0;
            var layersQty = _sessionService.SelectedTaskInfo.LAYERS_QTY ?? 0;

            if (layersQty <= 0)
                return ValidationResult.Error("Некорректное количество слоев (LAYERS_QTY).");

            if (inBoxQty <= 0)
                return ValidationResult.Error("Некорректное количество пачек в коробке (IN_BOX_QTY).");
            // Проверяем, что количество пачек нацело делится на количество слоев
            if (inBoxQty % layersQty != 0)
                return ValidationResult.Error($"Количество пачек в коробке ({inBoxQty}) должно нацело делиться на количество слоев ({layersQty}).");

            // Проверяем, что результат деления больше 0
            var packsPerLayer = inBoxQty / layersQty;
            if (packsPerLayer <= 0)
                return ValidationResult.Error($"Количество пачек в слое должно быть больше 0. Текущее значение: {packsPerLayer}.");


            return ValidationResult.Success();
        }
        public ValidationResult ValidatePositioningParameters()
        {

            var packHeight = _sessionService.SelectedTaskInfo.PACK_HEIGHT ?? 0;

            if (packHeight == 0)
                return ValidationResult.Error("Не задана высота слоя (PACK_HEIGHT).");

            return ValidationResult.Success();
        }

        //-----------

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



        public bool IsLastLayerCompleted(AggregationMetrics metrics, int currentLayers)
        {
            return currentLayers == _sessionService.SelectedTaskInfo?.LAYERS_QTY &&
                   metrics.ValidCount == metrics.TotalCells &&
                   metrics.TotalCells > 0;
        }

        public bool IsLayerCompleted(AggregationMetrics metrics, int numberOfLayers, int currentLayer)
        {
            return currentLayer < _sessionService.SelectedTaskInfo?.LAYERS_QTY &&
                   metrics.ValidCount == numberOfLayers &&
                   metrics.TotalCells > 0;
        }

        public bool HasValidCodes(AggregationMetrics metrics, int currentLayer)
        {
            return currentLayer < _sessionService.SelectedTaskInfo?.LAYERS_QTY &&
                   metrics.ValidCount == metrics.TotalCells &&
                   metrics.TotalCells > 0;
        }
        public AggregationMetrics CalculateMetrics(IEnumerable<dynamic> cells)
        {
            var cellsList = cells.ToList();
            return new AggregationMetrics(
                ValidCount: cellsList.Count(c => c.IsValid),
                DuplicatesInCurrentScan: cellsList.Count(c => c.IsDuplicateInCurrentScan),
                DuplicatesInAllScans: cellsList.Count(c => c.IsDuplicateInAllScans),
                TotalCells: cellsList.Count
            );
        }
        public DuplicateInformation BuildDuplicateInfo(AggregationMetrics metrics)
        {
            return new DuplicateInformation(metrics.DuplicatesInCurrentScan, metrics.DuplicatesInAllScans);
        }
    }
}