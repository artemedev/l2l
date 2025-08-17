using DM_wraper_NS;
using l2l_aggregator.Services.ControllerService;
using l2l_aggregator.Services.DmProcessing;
using l2l_aggregator.Services.Notification.Interface;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace l2l_aggregator.Services.AggregationService
{
    public record CameraConfiguration(
        string CameraName,
        string CameraModel,
        bool SoftwareTrigger = true,
        bool HardwareTrigger = true
    );

    public record RecognitionSettings(
        bool OCRRecogn,
        bool PackRecogn,
        bool DMRecogn,
        int CountOfDM
    );

    public record BoxWorkConfiguration(
        ushort CamBoxDistance,
        ushort BoxHeight,
        ushort LayersQty,
        ushort CamBoxMinDistance
    );

    public record ScanResult(
        result_data DmrData,
        Image<Rgba32> CroppedImage,
        int MinX,
        int MinY,
        int MaxX,
        int MaxY,
        double ScaleX,
        double ScaleY,
        double ScaleXObrat,
        double ScaleYObrat
    );

    public class ScanningService
    {
        private const string GIGE_VISION_INTERFACE = "GigEVision2";
        private const string OCR_MEMO_VIEW = "TfrxMemoView";
        private const string OCR_TEMPLATE_MEMO_VIEW = "TfrxTemplateMemoView";
        private const string DM_BARCODE_VIEW = "TfrxBarcode2DView";
        private const string DM_TEMPLATE_BARCODE_VIEW = "TfrxTemplateBarcode2DView";
        private const int BASE_CAMERA_DISTANCE = 450;
        private const int MIN_CAMERA_DISTANCE = 500;
        private const int UI_DELAY = 100;

        private readonly DmScanService _dmScanService;
        private readonly ImageProcessorService _imageProcessingService;
        private readonly TemplateService _templateService;
        private readonly INotificationService _notificationService;
        private readonly SessionService _sessionService;

        private string? _lastUsedTemplateJson;
        private bool _templateOk = false;

        public ScanningService(
            DmScanService dmScanService,
            ImageProcessorService imageProcessingService,
            TemplateService templateService,
            INotificationService notificationService,
            SessionService sessionService)
        {
            _dmScanService = dmScanService;
            _imageProcessingService = imageProcessingService;
            _templateService = templateService;
            _notificationService = notificationService;
            _sessionService = sessionService;
        }

        public bool SendTemplateToRecognizer(System.Collections.Generic.List<TemplateParserService> templateFields)
        {
            var currentTemplate = _templateService.GenerateTemplate(templateFields);

            if (_lastUsedTemplateJson != currentTemplate)
            {
                var settings = CreateRecognitionSettings(templateFields);
                var camera = CreateCameraConfiguration();
                var recognParams = CreateRecognitionParams(settings, camera);

                _dmScanService.StopScan();
                _dmScanService.ConfigureParams(recognParams);

                try
                {
                    _dmScanService.StartScan(currentTemplate);
                    Thread.Sleep(10000);
                    _lastUsedTemplateJson = currentTemplate;
                    _notificationService.ShowMessage("Шаблон распознавания успешно настроен", NotificationType.Success);
                    _templateOk = true;
                    return true;
                }
                catch (Exception ex)
                {
                    _notificationService.ShowMessage("Ошибка настройки шаблона распознавания", NotificationType.Error);
                    _templateOk = false;
                    return false;
                }
            }

            return _templateOk;
        }

        public async Task<ScanResult?> PerformSoftwareScanAsync(int currentLayer, Avalonia.Size imageSize)
        {
            if (!_templateOk)
            {
                _notificationService.ShowMessage("Шаблон не отправлен. Сначала выполните отправку шаблона.");
                return null;
            }

            try
            {
                _dmScanService.startShot();
                var dmrData = await _dmScanService.WaitForResultAsync();

                return await ProcessScanResult(dmrData, imageSize);
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка распознавания: {ex.Message}");
                return null;
            }
        }

        public async Task<ScanResult?> PerformHardwareScanAsync(Avalonia.Size imageSize)
        {
            if (!_templateOk)
            {
                _notificationService.ShowMessage("Шаблон не отправлен. Сначала выполните отправку шаблона.");
                return null;
            }

            try
            {
                var dmrData = await _dmScanService.WaitForResultAsync();

                return await ProcessScanResult(dmrData, imageSize);
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка распознавания: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> MoveCameraToCurrentLayerAsync(
            int currentLayer,
            PcPlcConnectionService? plcConnection)
        {
            if (!_sessionService.CheckController || plcConnection == null)
                return true;

            if (!plcConnection.IsConnected)
            {
                _notificationService.ShowMessage("Контроллер недоступен!");
                return false;
            }

            var taskInfo = _sessionService.SelectedTaskInfo;
            if (taskInfo == null)
            {
                _notificationService.ShowMessage("Информация о задании отсутствует.");
                return false;
            }

            var packHeight = taskInfo.PACK_HEIGHT ?? 0;
            var layersQty = taskInfo.LAYERS_QTY ?? 0;

            if (packHeight == 0)
            {
                _notificationService.ShowMessage("Ошибка: не задана высота слоя (PACK_HEIGHT).");
                return false;
            }

            if (layersQty == 0)
            {
                _notificationService.ShowMessage("Ошибка: не задано количество слоёв (LAYERS_QTY).");
                return false;
            }

            try
            {
                var boxConfig = CreateBoxWorkConfiguration(currentLayer, packHeight, layersQty);
                var boxSettings = new BoxWorkSettings
                {
                    CamBoxDistance = boxConfig.CamBoxDistance,
                    BoxHeight = boxConfig.BoxHeight,
                    LayersQtty = boxConfig.LayersQty,
                    CamBoxMinDistance = boxConfig.CamBoxMinDistance
                };

                await plcConnection.SetBoxWorkSettingsAsync(boxSettings);
                await plcConnection.StartCycleStepAsync((ushort)currentLayer);
                return true;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка позиционирования: {ex.Message}");
                return false;
            }
        }

        public async Task ConfirmPhotoToPlcAsync(PcPlcConnectionService? plcConnection)
        {
            if (plcConnection?.IsConnected == true)
            {
                try
                {
                    await plcConnection.ConfirmPhotoProcessedAsync();
                }
                catch (Exception ex)
                {
                    _notificationService.ShowMessage($"Ошибка подтверждения фото: {ex.Message}");
                }
            }
        }

        public void StopScanning()
        {
            _dmScanService.StopScan();
        }

        private async Task<ScanResult> ProcessScanResult(result_data dmrData, Avalonia.Size imageSize)
        {
            if (dmrData.rawImage == null)
            {
                throw new InvalidOperationException("Изображение из распознавания не получено.");
            }

            var (minX, minY, maxX, maxY) = CalculateCropBounds(dmrData);
            var croppedImage = _imageProcessingService.GetCroppedImage(dmrData, minX, minY, maxX, maxY);

            await Task.Delay(UI_DELAY);

            // Возвращаем временные значения масштаба, они будут пересчитаны в ViewModel
            return new ScanResult(
                dmrData,
                croppedImage,
                minX,
                minY,
                maxX,
                maxY,
                ScaleX: 1.0, // Временное значение
                ScaleY: 1.0, // Временное значение
                ScaleXObrat: 1.0, // Временное значение
                ScaleYObrat: 1.0  // Временное значение
            );
        }

        private (int minX, int minY, int maxX, int maxY) CalculateCropBounds(result_data dmrData)
        {
            double boxRadius = Math.Sqrt(dmrData.BOXs[0].height * dmrData.BOXs[0].height +
                                       dmrData.BOXs[0].width * dmrData.BOXs[0].width) / 2;

            int minX = Math.Max(0, (int)dmrData.BOXs.Min(d => d.poseX - boxRadius));
            int minY = Math.Max(0, (int)dmrData.BOXs.Min(d => d.poseY - boxRadius));
            int maxX = Math.Min(dmrData.rawImage.Width, (int)dmrData.BOXs.Max(d => d.poseX + boxRadius));
            int maxY = Math.Min(dmrData.rawImage.Height, (int)dmrData.BOXs.Max(d => d.poseY + boxRadius));

            return (minX, minY, maxX, maxY);
        }

        private CameraConfiguration CreateCameraConfiguration()
        {
            return new CameraConfiguration(
                CameraName: _sessionService.CameraIP,
                CameraModel: _sessionService.CameraModel
            );
        }

        private RecognitionSettings CreateRecognitionSettings(System.Collections.Generic.List<TemplateParserService> templateFields)
        {
            bool hasOcr = templateFields.Any(f => f.IsSelected && IsOcrElement(f.Element.Name.LocalName));
            bool hasDm = templateFields.Any(f => f.IsSelected && IsDmElement(f.Element.Name.LocalName));

            return new RecognitionSettings(
                OCRRecogn: hasOcr,
                PackRecogn: true, // Будет браться из UI
                DMRecogn: hasDm,
                CountOfDM: GetNumberOfLayers()
            );
        }

        private recogn_params CreateRecognitionParams(RecognitionSettings settings, CameraConfiguration camera)
        {
            return new recogn_params
            {
                countOfDM = settings.CountOfDM,
                CamInterfaces = GIGE_VISION_INTERFACE,
                cameraName = camera.CameraName,
                _Preset = new camera_preset(camera.CameraModel),
                softwareTrigger = camera.SoftwareTrigger,
                hardwareTrigger = camera.HardwareTrigger,
                OCRRecogn = settings.OCRRecogn,
                packRecogn = settings.PackRecogn,
                DMRecogn = settings.DMRecogn
            };
        }

        private BoxWorkConfiguration CreateBoxWorkConfiguration(int currentLayer, int packHeight, int layersQty)
        {
            return new BoxWorkConfiguration(
                CamBoxDistance: (ushort)(BASE_CAMERA_DISTANCE - ((currentLayer - 1) * packHeight)),
                BoxHeight: (ushort)packHeight,
                LayersQty: (ushort)layersQty,
                CamBoxMinDistance: MIN_CAMERA_DISTANCE
            );
        }

        private int GetNumberOfLayers()
        {
            var inBoxQty = _sessionService.SelectedTaskInfo?.IN_BOX_QTY ?? 0;
            var layersQty = _sessionService.SelectedTaskInfo?.LAYERS_QTY ?? 0;

            return layersQty > 0 ? inBoxQty / layersQty : 0;
        }

        private static bool IsOcrElement(string elementName) =>
            elementName is OCR_MEMO_VIEW or OCR_TEMPLATE_MEMO_VIEW;

        private static bool IsDmElement(string elementName) =>
            elementName is DM_BARCODE_VIEW or DM_TEMPLATE_BARCODE_VIEW;
    }
}