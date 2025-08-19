using l2l_aggregator.Models;
using l2l_aggregator.Services.GS1ParserService;
using l2l_aggregator.ViewModels.VisualElements;
using System;
using System.Linq;
using System.Text;

namespace l2l_aggregator.Services.AggregationService
{
    public class TextGenerationService
    {
        private readonly SessionService _sessionService;

        public TextGenerationService(SessionService sessionService)
        {
            _sessionService = sessionService;
        }
        //Использовано
        public string BuildInitialAggregationSummary(int currentBox, int currentLayer)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Агрегируемая серия: {_sessionService.SelectedTaskInfo?.RESOURCEID}");
            sb.AppendLine($"Количество собранных коробов: {currentBox - 1}");
            sb.AppendLine($"Номер собираемого короба: {currentBox}");
            sb.AppendLine($"Номер слоя: {currentLayer}");
            sb.AppendLine($"Количество слоев в коробе: {_sessionService.SelectedTaskInfo?.LAYERS_QTY}");
            return sb.ToString();
        }

        public string BuildAggregationSummary(
            AggregationMetrics metrics,
            DuplicateInformation duplicateInfo,
            int currentBox,
            int currentLayer,
            int numberOfLayers)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Агрегируемая серия: {_sessionService.SelectedTaskInfo?.RESOURCEID}");
            sb.AppendLine($"Количество собранных коробов: {currentBox - 1}");
            sb.AppendLine($"Номер собираемого короба: {currentBox}");
            sb.AppendLine($"Номер слоя: {currentLayer}");
            sb.AppendLine($"Количество слоев в коробе: {_sessionService.SelectedTaskInfo?.LAYERS_QTY}");
            sb.AppendLine($"Количество СИ, распознанное в слое: {metrics.ValidCount}");
            sb.AppendLine($"Количество СИ, считанное в слое: {metrics.TotalCells}");
            sb.AppendLine($"Количество СИ, ожидаемое в слое: {numberOfLayers}{duplicateInfo.GetDisplayText()}");
            sb.AppendLine($"Всего СИ в коробе: {_sessionService.CurrentBoxDmCodes.Count}");
            return sb.ToString();
        }
        /// <summary>
        /// Обновляет информацию об агрегации после завершения коробки
        /// </summary>
        public string BuildAggregationSummaryAfterBoxCompletion(int currentBox, int currentLayer)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Агрегируемая серия: {_sessionService.SelectedTaskInfo?.RESOURCEID}");
            sb.AppendLine($"Количество собранных коробов: {currentBox - 1}");
            sb.AppendLine($"Номер собираемого короба: {currentBox}");
            sb.AppendLine($"Номер слоя: {currentLayer}");
            sb.AppendLine($"Количество слоев в коробе: {_sessionService.SelectedTaskInfo?.LAYERS_QTY}");
            sb.AppendLine($"Всего агрегированных СИ: {_sessionService.AllScannedDmCodes.Count}");
            sb.AppendLine();
            sb.AppendLine("Коробка успешно агрегирована!");
            sb.AppendLine("Готов к сканированию.");
            return sb.ToString();
        }

        public string BuildInfoLayerText(int currentLayer, int layersQty, int validCount, int numberOfLayers)
        {
            return $"Слой {currentLayer} из {layersQty}. Распознано {validCount} из {numberOfLayers}";
        }

        public string BuildCellInfoSummary(DmCellViewModel cell)
        {
            var (gtin, serialNumber, duplicateStatus) = ExtractCellData(cell);

            var sb = new StringBuilder();
            sb.AppendLine($"GTIN-код: {(string.IsNullOrWhiteSpace(gtin) ? "нет данных" : gtin)}");
            sb.AppendLine($"SerialNumber-код: {(string.IsNullOrWhiteSpace(serialNumber) ? "нет данных" : serialNumber)}");
            sb.AppendLine($"Валидность: {(cell.Dm_data?.IsValid == true ? "Да" : "Нет")}");
            sb.AppendLine($"Дубликат: {duplicateStatus}");
            sb.AppendLine($"Координаты: {(cell.Dm_data is { } dm1 ? $"({dm1.X:0.##}, {dm1.Y:0.##})" : "нет данных")}");
            sb.AppendLine($"Размер: {(cell.Dm_data is { } dm ? $"({dm.SizeWidth:0.##} x {dm.SizeHeight:0.##})" : "нет данных")}");
            sb.AppendLine($"Угол: {(cell.Dm_data?.Angle is double a ? $"{a:0.##}°" : "нет данных")}");
            sb.AppendLine("OCR:");

            if (cell.OcrCells.Count > 0)
            {
                foreach (var ocr in cell.OcrCells)
                {
                    sb.AppendLine($"- {(string.IsNullOrWhiteSpace(ocr.OcrName) ? "нет данных" : ocr.OcrName)}: {(string.IsNullOrWhiteSpace(ocr.OcrText) ? "нет данных" : ocr.OcrText)} ({(ocr.IsValid ? "валид" : "не валид")})");
                }
            }
            else
            {
                sb.AppendLine("- нет данных");
            }

            return sb.ToString();
        }

        public string BuildSsccInfo(ArmJobSsccRecord ssccRecord)
        {
            string typeDescription = ssccRecord.TYPEID switch
            {
                0 => "Коробка", // SsccType.Box
                1 => "Паллета", // SsccType.Pallet
                _ => $"Неизвестный тип ({ssccRecord.TYPEID})"
            };

            string stateDescription = ssccRecord.CODE_STATE switch
            {
                "0" => "Не используется",
                "1" => "Активен",
                "2" => "Заблокирован",
                _ => $"Неизвестное состояние ({ssccRecord.CODE_STATE})"
            };

            var sb = new StringBuilder();
            sb.AppendLine("ИНФОРМАЦИЯ О SSCC КОДЕ");
            sb.AppendLine();
            sb.AppendLine($"SSCC ID: {ssccRecord.SSCCID}");
            sb.AppendLine($"SSCC код: {ssccRecord.SSCC_CODE ?? "нет данных"}");
            sb.AppendLine($"SSCC: {ssccRecord.SSCC ?? "нет данных"}");
            sb.AppendLine($"Тип: {typeDescription}");
            sb.AppendLine($"Состояние ID: {ssccRecord.STATEID}");
            sb.AppendLine($"Штрих-код (отображение): {ssccRecord.DISPLAY_BAR_CODE ?? "нет данных"}");
            sb.AppendLine($"Штрих-код (проверка): {ssccRecord.CHECK_BAR_CODE ?? "нет данных"}");
            sb.AppendLine($"Количество: {ssccRecord.QTY}");
            sb.AppendLine($"Родительский SSCC ID: {ssccRecord.PARENT_SSCCID ?? null}");

            return sb.ToString();
        }

        public string BuildUnInfo(ArmJobSgtinRecord unRecord)
        {
            string typeDescription = unRecord.UN_TYPE switch
            {
                1 => "Потребительская упаковка", // UnType.ConsumerPackage
                _ => $"Неизвестный тип ({unRecord.UN_TYPE})"
            };

            var sb = new StringBuilder();
            sb.AppendLine("ИНФОРМАЦИЯ О UN КОДЕ (SGTIN)");
            sb.AppendLine();
            sb.AppendLine($"UN ID: {unRecord.UNID}");
            sb.AppendLine($"UN код: {unRecord.UN_CODE ?? "нет данных"}");
            sb.AppendLine($"Тип: {typeDescription}");
            sb.AppendLine($"Состояние ID: {unRecord.STATEID}");
            sb.AppendLine($"GS1 поле 91: {unRecord.GS1FIELD91 ?? "нет данных"}");
            sb.AppendLine($"GS1 поле 92: {unRecord.GS1FIELD92 ?? "нет данных"}");
            sb.AppendLine($"GS1 поле 93: {unRecord.GS1FIELD93 ?? "нет данных"}");
            sb.AppendLine($"Родительский SSCC ID: {unRecord.PARENT_SSCCID ?? null}");
            sb.AppendLine($"Родительский UN ID: {unRecord.PARENT_UNID ?? null}");

            return sb.ToString();
        }

        public string BuildCodeNotFoundInfo(string barcode)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Код не найден в базе данных!");
            sb.AppendLine();
            sb.AppendLine($"Отсканированный код: {barcode}");
            sb.AppendLine("Статус: Код не найден в системе");
            sb.AppendLine();
            sb.AppendLine("Проверьте правильность кода или обратитесь к администратору.");

            return sb.ToString();
        }

        public string BuildErrorInfo(string barcode, string errorMessage)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Ошибка поиска кода!");
            sb.AppendLine();
            sb.AppendLine($"Отсканированный код: {barcode}");
            sb.AppendLine($"Ошибка: {errorMessage}");

            return sb.ToString();
        }

        public string BuildDisaggregationSuccessInfo(string barcode, ArmJobSsccRecord boxRecord)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Очистка короба выполнена успешно!");
            sb.AppendLine();
            sb.AppendLine($"Отсканированный код: {barcode}");
            sb.AppendLine($"SSCC ID: {boxRecord.SSCCID}");
            sb.AppendLine($"CHECK_BAR_CODE: {boxRecord.CHECK_BAR_CODE}");
            sb.AppendLine("Статус: Коробка успешно разагрегирована");

            return sb.ToString();
        }
        public string BuildDisaggregationCancelledInfo(string barcode)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Очистка короба отменена пользователем.");
            sb.AppendLine();
            sb.AppendLine($"Отсканированный код: {barcode}");
            return sb.ToString();
        }
        public string BuildDisaggregationFailureInfo(string barcode, ArmJobSsccRecord boxRecord)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Ошибка очистки короба!");
            sb.AppendLine();
            sb.AppendLine($"Отсканированный код: {barcode}");
            sb.AppendLine($"SSCC ID: {boxRecord.SSCCID}");
            sb.AppendLine("Статус: Не удалось выполнить очистку короба");

            return sb.ToString();
        }

        public string BuildBoxNotFoundInfo(string barcode)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Код коробки не найден!");
            sb.AppendLine();
            sb.AppendLine($"Отсканированный код: {barcode}");
            sb.AppendLine("Статус: Код не найден среди доступных коробок");
            sb.AppendLine();
            sb.AppendLine("Проверьте правильность кода или убедитесь, что коробка существует в текущем задании.");

            return sb.ToString();
        }

        public string GetInfoModeText()
        {
            return "Режим информации: отсканируйте код для получения информации";
        }

        public string GetInfoModeMessage()
        {
            return "Режим информации активен. \nОтсканируйте код для получения подробной информации о нем.";
        }

        public string GetDisaggregationModeText()
        {
            return "Режим очистки короба: отсканируйте код коробки для очистки короба";
        }

        public string GetDisaggregationModeMessage()
        {
            return "Режим очистки короба активен. \nОтсканируйте код коробки для выполнения очистки короба.";
        }

        public string GetInitialText()
        {
            return "Выберите элементы шаблона для агрегации и нажмите кнопку сканировать!";
        }

        public string GetContinueAggregationText()
        {
            return "Продолжаем агрегацию!";
        }

        public string GetNewBoxText(int currentBox)
        {
            return $"Коробка {currentBox - 1} завершена. Начинаем новую коробку {currentBox}. Выберите элементы шаблона для агрегации и нажмите кнопку сканировать!";
        }

        private (string gtin, string serialNumber, string duplicateStatus) ExtractCellData(DmCellViewModel cell)
        {
            string gtin = "";
            string serialNumber = "";

            if (cell.Dm_data?.Data != null)
            {
                var gS1Parser = new GS1Parser();
                var newGS = gS1Parser.ParseGTIN(cell.Dm_data.Data);
                gtin = newGS.GTIN;
                serialNumber = newGS.SerialNumber;
            }

            string duplicateStatus = "Нет";
            if (cell.IsDuplicateInCurrentScan)
            {
                duplicateStatus = "Да (в текущем скане)";
            }
            else if (cell.IsDuplicateInAllScans)
            {
                duplicateStatus = "Да (в предыдущих сканах)";
            }

            return (gtin, serialNumber, duplicateStatus);
        }
    }
}