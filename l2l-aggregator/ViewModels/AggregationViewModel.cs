using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.SimpleRouter;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DM_wraper_NS;
using l2l_aggregator.Models;
using l2l_aggregator.Services;
using l2l_aggregator.Services.AggregationService;
using l2l_aggregator.Services.ControllerService;
using l2l_aggregator.Services.DmProcessing;
using l2l_aggregator.Services.GS1ParserService;
using l2l_aggregator.Services.Notification.Interface;
using l2l_aggregator.Services.Printing;
using l2l_aggregator.ViewModels.VisualElements;
using l2l_aggregator.Views.Popup;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace l2l_aggregator.ViewModels
{
    // Константы для улучшения читаемости
    internal static class AggregationConstants
    {
        public const int CONTROLLER_PING_INTERVAL = 10000;
        public const int UI_DELAY = 100;
        public const int BASE_CAMERA_DISTANCE = 450;
        public const int MIN_CAMERA_DISTANCE = 500;

        public const string GIGE_VISION_INTERFACE = "GigEVision2";
        public const string OCR_MEMO_VIEW = "TfrxMemoView";
        public const string OCR_TEMPLATE_MEMO_VIEW = "TfrxTemplateMemoView";
        public const string DM_BARCODE_VIEW = "TfrxBarcode2DView";
        public const string DM_TEMPLATE_BARCODE_VIEW = "TfrxTemplateBarcode2DView";
    }

    // Перечисления для типобезопасности
    internal enum AggregationStep
    {
        PackAggregation = 1,
        BoxAggregation = 2,
        PalletAggregation = 3,
        BoxScanning = 4,
        PalletScanning = 5,
        InfoMode = 6,
        DisaggregationMode = 7
    }

    internal enum SsccType
    {
        Box = 0,
        Pallet = 1
    }

    internal enum CodeState
    {
        NotUsed = 0,
        Active = 1,
        Blocked = 2
    }

    internal enum UnType
    {
        ConsumerPackage = 1
    }

    // Вспомогательные record-ы
    internal record ValidationResult(bool IsValid, string? ErrorMessage)
    {
        public static ValidationResult Success() => new(true, null);
        public static ValidationResult Error(string message) => new(false, message);
    }

    internal record CameraConfiguration(
        string CameraName,
        string CameraModel,
        bool SoftwareTrigger = true,
        bool HardwareTrigger = true
    );

    internal record RecognitionSettings(
        bool OCRRecogn,
        bool PackRecogn,
        bool DMRecogn,
        int CountOfDM
    );

    internal record BoxWorkConfiguration(
        ushort CamBoxDistance,
        ushort BoxHeight,
        ushort LayersQtty,
        ushort CamBoxMinDistance
    );

    public partial class AggregationViewModel : ViewModelBase
    {
        #region Private Fields

        //сервис работы с сессией
        private readonly SessionService _sessionService;

        //сервис кропа изображения выбранной ячейки пользователем
        private readonly ImageProcessorService _imageProcessingService;

        //сервис обработки шаблона, после выбора пользователя элементов в ui. Для дальнейшей отправки в библиотеку распознавания
        private readonly TemplateService _templateService;

        //сервис нотификаций
        private readonly INotificationService _notificationService;

        //сервис роутинга
        private readonly HistoryRouter<ViewModelBase> _router;

        //сервис принтера
        private readonly PrintingService _printingService;

        private readonly DatabaseDataService _databaseDataService;

        //сервис обработки и работы с библиотекой распознавания
        private readonly DmScanService _dmScanService;
        //сервис для генерации текстовых сообщений в UI
        private readonly TextGenerationService _textGenerationService;

        private readonly IDialogService _dialogService;

        private readonly ILogger<PcPlcConnectionService> _logger;

        //переменные для высчитывания разницы между кропнутым изображением и изображением из интерфейса
        private double scaleX, scaleY, scaleXObrat, scaleYObrat;

        //переменная для сохранение шаблона при показе информацию из ячейки
        private string? _lastUsedTemplateJson;

        //переменная для колличества слоёв всего
        private int numberOfLayers;

        //переменная для шаблона коробки, для печати
        private byte[] frxBoxBytes;

        //данные распознавания
        static result_data dmrData;

        //поле для запоминания предыдущего значения информации о агрегации для выхода из информации для клика по ячейке
        private string _previousAggregationSummaryText;

        private int minX;
        private int minY;
        private int maxX;
        private int maxY;

        private Image<Rgba32> _croppedImageRaw;

        private PcPlcConnectionService _plcConnection;

        //переменная для отслеживания состояния шаблона
        private bool templateOk = false;

        private AggregationStep CurrentStepIndex = AggregationStep.PackAggregation;
        private AggregationStep PreviousStepIndex = AggregationStep.PackAggregation;

        //переменная для сообщений нотификации
        private string InfoMessage;

        // Предыдущие значения состояния кнопок для восстановления
        private bool _previousCanScan;
        private bool _previousCanScanHardware;
        private bool _previousCanOpenTemplateSettings;
        private bool _previousCanPrintBoxLabel;
        private bool _previousCanClearBox;
        private bool _previousCanCompleteAggregation;
        private string _previousInfoLayerText;

        // Предыдущие значения для восстановления при выходе из режима разагрегации
        private bool _previousCanScanDisaggregation;
        private bool _previousCanScanHardwareDisaggregation;
        private bool _previousCanOpenTemplateSettingsDisaggregation;
        private bool _previousCanPrintBoxLabelDisaggregation;
        private bool _previousCanClearBoxDisaggregation;
        private bool _previousCanCompleteAggregationDisaggregation;
        private string _previousInfoLayerTextDisaggregation;

        // Поля для сохранения состояния до активации режимов
        private string _normalModeInfoLayerText;
        private string _normalModeAggregationSummaryText;
        private bool _isNormalStateDataSaved = false;
        #endregion

        #region Observable Properties

        //для обновления размеров ячейки, UI
        public IRelayCommand<SizeChangedEventArgs> ImageSizeChangedCommand { get; }
        public IRelayCommand<SizeChangedEventArgs> ImageSizeCellChangedCommand { get; }
        [ObservableProperty] private Avalonia.Size imageSize;
        [ObservableProperty] private Avalonia.Size imageSizeCell;
        [ObservableProperty] private double imageWidth;
        [ObservableProperty] private double imageHeight;
        [ObservableProperty] private double imageCellWidth;
        [ObservableProperty] private double imageCellHeight;

        //валидация ячейки будет она красная или зеленая, UI
        [ObservableProperty] private bool isValid;

        //изображение слоя, UI
        [ObservableProperty] private Bitmap scannedImage;

        //Данные ячеек, UI
        [ObservableProperty] private ObservableCollection<DmCellViewModel> dMCells = new();

        //переменная для открытия всплывающего окна с изображением выбранной ячейки
        [ObservableProperty] private bool isPopupOpen;

        //перемення для отображения элементов в выбранной ячейке
        [ObservableProperty] private DmCellViewModel selectedDmCell;
        //изображение выбранной ячейки
        [ObservableProperty] private Bitmap selectedSquareImage;

        //состояние кнопок
        //Кнопока сканировать
        //для отслеживания состояния загрузки камеры и шаблона
        [ObservableProperty] private bool canScan = true;
        //Кнопока настройки шаблона
        [ObservableProperty] private bool canOpenTemplateSettings = true;
        //Доступ к кнопоке печать этикетки коробки
        [ObservableProperty] private bool canPrintBoxLabel = false;
        //Доступ к кнопоке печать этикетки паллеты
        [ObservableProperty] private bool сanPrintPalletLabel = false;
        //Очистить короб
        [ObservableProperty] private bool canClearBox = false;

        //Завершить агрегацию
        [ObservableProperty] private bool canCompleteAggregation = true;
        //Остановить сессию
        [ObservableProperty] private bool canStopSession = false;

        //элементы шаболона в список всплывающего окна 
        public ObservableCollection<TemplateParserService> TemplateFields { get; } = new();

        //текущий слой
        [ObservableProperty] private int currentLayer = 1;
        //текущая коробка
        [ObservableProperty] private int currentBox = 1;
        //текущая паллета
        [ObservableProperty] private int currentPallet = 1;

        // Добавление опции "распознавание коробки" в настройки распознавания
        [ObservableProperty] private bool recognizePack = true;

        //информационное текстовое поле выше изображения 
        [ObservableProperty] private string infoLayerText = "Выберите элементы шаблона для агрегации и нажмите кнопку сканировать!";

        //информационное текстовое поле справа изображения
        [ObservableProperty] private string aggregationSummaryText = "Результат агрегации пока не рассчитан.";

        [ObservableProperty] private bool isControllerAvailable = true; // по умолчанию доступен

        //Кнопка сканировать (hardware trigger)
        [ObservableProperty] private bool canScanHardware = false;

        //Автоматическая печать этикетки коробки
        [ObservableProperty] private bool isAutoPrintEnabled = true;

        // Режим информации
        [ObservableProperty] private bool isInfoMode = false;
        // Текст кнопки режима информации
        [ObservableProperty] private string infoModeButtonText = "Режим информации";

        // Режим разагрегации
        [ObservableProperty] private bool isDisaggregationMode = false;
        // Текст кнопки режима разагрегации
        [ObservableProperty] private string disaggregationModeButtonText = "Режим очистки короба";
        // Доступность кнопки режима разагрегации
        [ObservableProperty] private bool canDisaggregation = false;

#if DEBUG
        [ObservableProperty] private bool isDebugMode = true;
#else
        [ObservableProperty] private bool isDebugMode = false;
#endif

        #endregion

        #region Constructor

        public AggregationViewModel(
            ImageProcessorService imageProcessingService,
            SessionService sessionService,
            TemplateService templateService,
            DmScanService dmScanService,
            DatabaseDataService databaseDataService,
            INotificationService notificationService,
            HistoryRouter<ViewModelBase> router,
            PrintingService printingService,
            ILogger<PcPlcConnectionService> logger,
            TextGenerationService textGenerationService,
            IDialogService dialogService
            )
        {
            _sessionService = sessionService;
            _imageProcessingService = imageProcessingService;
            _templateService = templateService;
            _dmScanService = dmScanService;
            _notificationService = notificationService;
            _router = router;
            _printingService = printingService;
            _logger = logger;
            _dialogService = dialogService;
            _databaseDataService = databaseDataService;
            _textGenerationService = textGenerationService;

            ImageSizeChangedCommand = new RelayCommand<SizeChangedEventArgs>(OnImageSizeChanged);
            ImageSizeCellChangedCommand = new RelayCommand<SizeChangedEventArgs>(OnImageSizeCellChanged);

            InitializeAsync();
        }

        #endregion

        #region Initialization Methods

        private async void InitializeAsync()
        {
            InitializeNumberOfLayers();
            await GetCurrentBox();
            InitializeTemplate();
            InitializeControllerPing();
            InitializeUpdateInfoAndUI();
        }
        private void InitializeNumberOfLayers()
        {
            var inBoxQty = _sessionService.SelectedTaskInfo.IN_BOX_QTY ?? 0;
            var layersQty = _sessionService.SelectedTaskInfo.LAYERS_QTY ?? 0;
            numberOfLayers = inBoxQty / layersQty; //колличество пачек в слое
        }

        private void InitializeTemplate()
        {
            TemplateFields.Clear();

            var loadedFields = _templateService.LoadTemplate(_sessionService.SelectedTaskInfo.UN_TEMPLATE_FR);
            foreach (var f in loadedFields)
                TemplateFields.Add(f);

            UpdateScanAvailability();
        }
        private async void InitializeControllerPing()
        {
            if (!_sessionService.CheckController || string.IsNullOrWhiteSpace(_sessionService.ControllerIP))
                return;

            try
            {
                _plcConnection = new PcPlcConnectionService(_logger);
                bool connected = await _plcConnection.ConnectAsync(_sessionService.ControllerIP);

                if (connected)
                {
                    _plcConnection.StartPingPong(AggregationConstants.CONTROLLER_PING_INTERVAL);
                    _plcConnection.ConnectionStatusChanged += OnPlcConnectionStatusChanged;
                    _plcConnection.ErrorsReceived += OnPlcErrorsReceived;

                    IsControllerAvailable = true;
                    ShowMessage("Контроллер подключен и мониторинг активен", NotificationType.Success);
                }
                else
                {
                    IsControllerAvailable = false;
                    ShowMessage("Не удалось подключиться к контроллеру", NotificationType.Error);
                }
            }
            catch (Exception ex)
            {
                IsControllerAvailable = false;
                ShowMessage($"Ошибка инициализации контроллера: {ex.Message}", NotificationType.Error);
            }
        }

        private void InitializeUpdateInfoAndUI()
        {
            // Проверяем, является ли это началом новой агрегации
            bool isNewAggregation = CurrentBox == 1 && CurrentLayer == 1 && !_sessionService.AllScannedDmCodes.Any();

            if (isNewAggregation)
            {
                InfoLayerText = _textGenerationService.GetInitialText();
            }
            else
            {
                InfoLayerText = _textGenerationService.GetContinueAggregationText();
            }
            AggregationSummaryText = _textGenerationService.BuildInitialAggregationSummary(CurrentBox, CurrentLayer);
        }
        #endregion

        #region Helper Methods
        private void ShowMessage(string message, NotificationType type = NotificationType.Info)
        {
            InfoMessage = message;
            _notificationService.ShowMessage(InfoMessage, type);
        }


        private AggregationMetrics CalculateAggregationMetrics()
        {
            return new AggregationMetrics(
                ValidCount: DMCells.Count(c => c.IsValid),
                DuplicatesInCurrentScan: DMCells.Count(c => c.IsDuplicateInCurrentScan),
                DuplicatesInAllScans: DMCells.Count(c => c.IsDuplicateInAllScans),
                TotalCells: DMCells.Count
            );
        }

        #endregion

        #region Event Handlers

        partial void OnIsControllerAvailableChanged(bool value)
        {
            UpdateScanAvailability();
        }

        partial void OnIsPopupOpenChanged(bool value)
        {
            if (!value && !IsDisaggregationMode)
            {
                AggregationSummaryText = _previousAggregationSummaryText;
            }
        }

        partial void OnIsInfoModeChanged(bool value)
        {
            if (value)
            {
                // Если включается режим информации, отключаем режим очистки короба
                if (IsDisaggregationMode)
                {
                    IsDisaggregationMode = false; // Это вызовет ExitDisaggregationMode()
                }
                EnterInfoMode();
            }
            else
            {
                ExitInfoMode();
            }
        }

        partial void OnIsDisaggregationModeChanged(bool value)
        {
            if (value)
            {
                // Если включается режим очистки короба, отключаем режим информации
                if (IsInfoMode)
                {
                    IsInfoMode = false; // Это вызовет ExitInfoMode()
                }
                EnterDisaggregationMode();
            }
            else
            {
                ExitDisaggregationMode();
            }
        }

        private void OnPlcConnectionStatusChanged(bool isConnected)
        {
            IsControllerAvailable = isConnected;

            if (!isConnected)
            {
                ShowMessage("Потеряно соединение с контроллером!", NotificationType.Error);
            }
            else
            {
                ShowMessage("Соединение с контроллером восстановлено", NotificationType.Success);
            }
        }

        private void OnPlcErrorsReceived(PlcErrors errors)
        {
            string errorMessage = errors.GetErrorDescription();
            ShowMessage($"Ошибки контроллера: {errorMessage}", NotificationType.Error);
        }

        private void OnImageSizeChanged(SizeChangedEventArgs e)
        {
            imageWidth = e.NewSize.Width;
            imageHeight = e.NewSize.Height;
        }

        private void OnImageSizeCellChanged(SizeChangedEventArgs e)
        {
            imageCellWidth = e.NewSize.Width;
            imageCellHeight = e.NewSize.Height;
        }

        #endregion

        #region Scanning Software

        /// <summary>
        /// Выполняет программное сканирование слоя с распознаванием кодов
        /// </summary>
        /// <returns>Задача, представляющая асинхронную операцию сканирования</returns>
        [RelayCommand]
        public async Task ScanSoftware()
        {
            try
            {
                templateOk = SendTemplateToRecognizer();
            }
            catch (Exception ex)
            {
                ShowMessage($"Ошибка инициализации задания: {ex.Message}", NotificationType.Error);
                templateOk = false;
                return;
            }
            finally
            {
                UpdateScanAvailability();
            }

            if (!await MoveCameraToCurrentLayerAsync())
                return;

            await StartScanningSoftwareAsync();
        }
        #region SendTemplateToRecognizer
        /// <summary>
        /// Отправка шаболона в библиотеку распознавания
        /// </summary>
        public bool SendTemplateToRecognizer()
        {
            var currentTemplate = _templateService.GenerateTemplate(TemplateFields.ToList());

            if (_lastUsedTemplateJson != currentTemplate)
            {
                var settings = CreateRecognitionSettings();
                var camera = CreateCameraConfiguration();
                var recognParams = CreateRecognitionParams(settings, camera);

                _dmScanService.StopScan();
                _dmScanService.ConfigureParams(recognParams);

                try
                {
                    _dmScanService.StartScan(currentTemplate);
                    _lastUsedTemplateJson = currentTemplate;
                    ShowMessage("Шаблон распознавания успешно настроен", NotificationType.Success);
                    return true;
                }
                catch (Exception ex)
                {
                    ShowMessage("Ошибка настройки шаблона распознавания", NotificationType.Error);
                    return false;
                }
            }

            return true;
        }
        #region CreateRecognitionSettings
        private RecognitionSettings CreateRecognitionSettings()
        {
            bool hasOcr = TemplateFields.Any(f => f.IsSelected && IsOcrElement(f.Element.Name.LocalName));
            bool hasDm = TemplateFields.Any(f => f.IsSelected && IsDmElement(f.Element.Name.LocalName));

            return new RecognitionSettings(
                OCRRecogn: hasOcr,
                PackRecogn: RecognizePack,
                DMRecogn: hasDm,
                CountOfDM: numberOfLayers
            );
        }
        private static bool IsOcrElement(string elementName) =>
            elementName is AggregationConstants.OCR_MEMO_VIEW or AggregationConstants.OCR_TEMPLATE_MEMO_VIEW;

        private static bool IsDmElement(string elementName) =>
            elementName is AggregationConstants.DM_BARCODE_VIEW or AggregationConstants.DM_TEMPLATE_BARCODE_VIEW;
        #endregion
        private CameraConfiguration CreateCameraConfiguration()
        {
            return new CameraConfiguration(
                CameraName: _sessionService.CameraIP,
                CameraModel: _sessionService.CameraModel
            );
        }

        private recogn_params CreateRecognitionParams(RecognitionSettings settings, CameraConfiguration camera)
        {
            return new recogn_params
            {
                countOfDM = settings.CountOfDM,
                CamInterfaces = AggregationConstants.GIGE_VISION_INTERFACE,
                cameraName = camera.CameraName,
                _Preset = new camera_preset(camera.CameraModel),
                softwareTrigger = camera.SoftwareTrigger,
                hardwareTrigger = camera.HardwareTrigger,
                OCRRecogn = settings.OCRRecogn,
                packRecogn = settings.PackRecogn,
                DMRecogn = settings.DMRecogn
            };
        }
        #endregion

        #region Move Camera To Current Layer Method
        private async Task<bool> MoveCameraToCurrentLayerAsync()
        {
            if (!_sessionService.CheckController)
                return true;

            if (!IsControllerAvailable)
            {
                ShowMessage("Контроллер недоступен!", NotificationType.Error);
                return false;
            }


            var packHeight = _sessionService.SelectedTaskInfo.PACK_HEIGHT ?? 0;
            var layersQty = _sessionService.SelectedTaskInfo.LAYERS_QTY ?? 0;

            if (packHeight == 0)
            {
                ShowMessage("Ошибка: не задана высота слоя в задании.", NotificationType.Error);
                return false;
            }

            if (layersQty == 0)
            {
                ShowMessage("Ошибка: не задано количество слоёв в задании.", NotificationType.Error);
                return false;
            }

            try
            {
                var boxConfig = CreateBoxWorkConfiguration();
                var boxSettings = new BoxWorkSettings
                {
                    CamBoxDistance = boxConfig.CamBoxDistance,
                    BoxHeight = boxConfig.BoxHeight,
                    LayersQtty = boxConfig.LayersQtty,
                    CamBoxMinDistance = boxConfig.CamBoxMinDistance
                };

                await _plcConnection.SetBoxWorkSettingsAsync(boxSettings);
                await _plcConnection.StartCycleStepAsync((ushort)CurrentLayer);
                return true;
            }
            catch (Exception ex)
            {
                ShowMessage($"Ошибка позиционирования: {ex.Message}", NotificationType.Error);
                return false;
            }
        }
        private BoxWorkConfiguration CreateBoxWorkConfiguration()
        {
            var packHeight = _sessionService.SelectedTaskInfo.PACK_HEIGHT ?? 0;
            var layersQty = _sessionService.SelectedTaskInfo.LAYERS_QTY ?? 0;

            return new BoxWorkConfiguration(
                CamBoxDistance: (ushort)(AggregationConstants.BASE_CAMERA_DISTANCE - ((CurrentLayer - 1) * packHeight)),
                BoxHeight: (ushort)packHeight,
                LayersQtty: (ushort)layersQty,
                CamBoxMinDistance: AggregationConstants.MIN_CAMERA_DISTANCE
            );
        }
        #endregion

        #region Scanning Software
        private async Task StartScanningSoftwareAsync()
        {
            if (_lastUsedTemplateJson == null)
            {
                ShowMessage("Шаблон не отправлен. Сначала выполните отправку шаблона.", NotificationType.Error);
                return;
            }

            GetCurrentBoxRecord();

            if (!await TryReceiveScanDataSoftwareAsync() ||
                !await TryCropImageAsync() ||
                !await TryBuildCellsAsync())
                return;

            await UpdateInfoAndUI();
        }
        //Загрузка свободной коробки
        private async Task GetCurrentBoxRecord()
        {
            try
            {
                var freeBox = await _databaseDataService.ReserveFreeBox();
                if (freeBox != null)
                {
                    _sessionService.SelectedTaskSscc = freeBox;
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Ошибка загрузки текущей коробки {ex.Message}", NotificationType.Error);
            }

        }

        private async Task<bool> TryReceiveScanDataSoftwareAsync()
        {
            try
            {
                CanOpenTemplateSettings = false;
                _dmScanService.startShot();
                dmrData = await _dmScanService.WaitForResultAsync();
                return true;
            }
            catch (Exception ex)
            {
                ShowMessage($"Ошибка распознавания: {ex.Message}", NotificationType.Error);
                return false;
            }
        }

        #region TryCropImageAsync
        //Кроп изображения
        private async Task<bool> TryCropImageAsync()
        {
            if (dmrData.rawImage == null)
            {
                ShowMessage("Изображение из распознавания не получено.", NotificationType.Error);
                return false;
            }

            CalculateCropBounds();
            CropAndProcessImage();
            await CalculateScaleFactors();

            return true;
        }

        private void CalculateCropBounds()
        {
            double boxRadius = Math.Sqrt(dmrData.BOXs[0].height * dmrData.BOXs[0].height +
                                         dmrData.BOXs[0].width * dmrData.BOXs[0].width) / 2;

            minX = Math.Max(0, (int)dmrData.BOXs.Min(d => d.poseX - boxRadius));
            minY = Math.Max(0, (int)dmrData.BOXs.Min(d => d.poseY - boxRadius));
            maxX = Math.Min(dmrData.rawImage.Width, (int)dmrData.BOXs.Max(d => d.poseX + boxRadius));
            maxY = Math.Min(dmrData.rawImage.Height, (int)dmrData.BOXs.Max(d => d.poseY + boxRadius));
        }
        private void CropAndProcessImage()
        {
            _croppedImageRaw = _imageProcessingService.GetCroppedImage(dmrData, minX, minY, maxX, maxY);

            ScannedImage?.Dispose();
            ScannedImage = _imageProcessingService.ConvertToAvaloniaBitmap(_croppedImageRaw);
        }
        private async Task CalculateScaleFactors()
        {
            await Task.Delay(AggregationConstants.UI_DELAY);

            scaleX = imageSize.Width / ScannedImage.PixelSize.Width;
            scaleY = imageSize.Height / ScannedImage.PixelSize.Height;
            scaleXObrat = ScannedImage.PixelSize.Width / imageSize.Width;
            scaleYObrat = ScannedImage.PixelSize.Height / imageSize.Height;
        }
        #endregion

        private async Task<bool> TryBuildCellsAsync()
        {
            var docId = _sessionService.SelectedTaskInfo.DOCID;
            if (docId == 0)
            {
                ShowMessage("Ошибка: некорректный ID документа.", NotificationType.Error);
                return false;
            }

            if (_sessionService.CachedSgtinResponse == null)
            {
                ShowMessage("Ошибка загрузки данных SGTIN.", NotificationType.Error);
                return false;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                DMCells.Clear();
                var cells = _imageProcessingService.BuildCellViewModels(
                    dmrData, scaleX, scaleY, _sessionService, TemplateFields,
                    _sessionService.CachedSgtinResponse, this, minX, minY);

                foreach (var cell in cells)
                {
                    DMCells.Add(cell);
                }
            });

            return true;
        }
        #region UpdateInfoAndUI
        public record DuplicateInformation(int InCurrentScan, int InAllScans)
        {
            public bool HasDuplicates => InCurrentScan > 0 || InAllScans > 0;

            public string GetDisplayText()
            {
                if (!HasDuplicates) return "";

                var sb = new StringBuilder($"\nДубликаты в текущем скане: {InCurrentScan}");
                if (InAllScans > 0)
                    sb.Append($"\nДубликаты из предыдущих сканов: {InAllScans}");
                return sb.ToString();
            }
        }
        private async Task UpdateInfoAndUI()
        {
            var metrics = CalculateAggregationMetrics();
            var duplicateInfo = BuildDuplicateInfo(metrics);

            UpdateInfoLayerText(metrics);
            UpdateAggregationSummaryText(metrics, duplicateInfo);
            UpdateButtonStates();

            var validCodes = DMCells.Where(c => c.IsValid && !string.IsNullOrWhiteSpace(c.Dm_data?.Data))
                                   .Select(c => c.Dm_data.Data).ToList();

            if (IsLastLayerCompleted(metrics))
            {
                await HandleLastLayerCompletion(validCodes);
            }
            else if (IsLayerCompleted(metrics))
            {
                await HandleLayerCompletion(validCodes);
            }
            else if (HasValidCodes(metrics))
            {
                HandlePartialLayerCompletion(validCodes);
            }
        }
        private DuplicateInformation BuildDuplicateInfo(AggregationMetrics metrics)
        {
            return new DuplicateInformation(metrics.DuplicatesInCurrentScan, metrics.DuplicatesInAllScans);
        }

        private void UpdateInfoLayerText(AggregationMetrics metrics)
        {
            InfoLayerText = $"Слой {CurrentLayer} из {_sessionService.SelectedTaskInfo.LAYERS_QTY}. Распознано {metrics.ValidCount} из {numberOfLayers}";
        }
        private void UpdateAggregationSummaryText(AggregationMetrics metrics, DuplicateInformation duplicateInfo)
        {
            InfoLayerText = _textGenerationService.BuildInfoLayerText(CurrentLayer, _sessionService.SelectedTaskInfo?.LAYERS_QTY ?? 0, metrics.ValidCount, numberOfLayers);
            AggregationSummaryText = _textGenerationService.BuildAggregationSummary(metrics, duplicateInfo, CurrentBox, CurrentLayer, numberOfLayers);
        }
        private void UpdateButtonStates()
        {
            CanScan = true;
            CanOpenTemplateSettings = true;
        }
        private bool IsLastLayerCompleted(AggregationMetrics metrics)
        {
            return CurrentLayer == _sessionService.SelectedTaskInfo.LAYERS_QTY &&
                   metrics.ValidCount == metrics.TotalCells &&
                   metrics.TotalCells > 0;
        }
        private async Task HandleLastLayerCompletion(List<string> validCodes)
        {
            _sessionService.AddLayerCodes(validCodes);

            CanOpenTemplateSettings = false;
            CanPrintBoxLabel = true;
            CurrentStepIndex = AggregationStep.BoxAggregation;


            if (IsAutoPrintEnabled && validCodes.Count == numberOfLayers)
            {
                await PrintBoxLabel();
            }

            await ConfirmPhotoToPlcAsync();
        }
        private bool IsLayerCompleted(AggregationMetrics metrics)
        {
            return CurrentLayer < _sessionService.SelectedTaskInfo.LAYERS_QTY &&
                   metrics.ValidCount == numberOfLayers &&
                   metrics.TotalCells > 0;
        }
        private async Task HandleLayerCompletion(List<string> validCodes)
        {
            _sessionService.AddLayerCodes(validCodes);
            CurrentLayer++;
            await ConfirmPhotoToPlcAsync();
        }
        private bool HasValidCodes(AggregationMetrics metrics)
        {
            return CurrentLayer < _sessionService.SelectedTaskInfo.LAYERS_QTY &&
                   metrics.ValidCount == metrics.TotalCells &&
                   metrics.TotalCells > 0;
        }
        private void HandlePartialLayerCompletion(List<string> validCodes)
        {
            _sessionService.AddLayerCodes(validCodes);
        }
        #region Confirm Photo To Plc
        private async Task ConfirmPhotoToPlcAsync()
        {
            if (_plcConnection?.IsConnected == true)
            {
                try
                {
                    await _plcConnection.ConfirmPhotoProcessedAsync();
                }
                catch (Exception ex)
                {
                    ShowMessage($"Ошибка подтверждения фото: {ex.Message}", NotificationType.Error);
                }
            }
        }
        #endregion
        #endregion
        #endregion
        #endregion

        #region Scanning Hardware
        /// <summary>
        /// Выполняет аппаратное сканирование через контроллер
        /// </summary>
        /// <returns>Задача, представляющая асинхронную операцию сканирования</returns>
        [RelayCommand]
        public async Task ScanHardware()
        {
            //var validation = ValidateTaskInfo();
            //if (!validation.IsValid)
            //{
            //    ShowErrorMessage(validation.ErrorMessage);
            //    return;
            //}

            //if (!templateOk)
            //{
            //    ShowErrorMessage("Задание не инициализировано. Сначала нажмите 'Начать задание'.");
            //    return;
            //}

            //if (!await MoveCameraToCurrentLayerAsync())
            //    return;

            //try
            //{
            //    await _plcConnection.TriggerPhotoAsync();
            //    await StartScanningHardwareAsync();
            //}
            //catch (Exception ex)
            //{
            //    ShowErrorMessage($"Ошибка hardware trigger: {ex.Message}");
            //}
        }
        #endregion
        
        #region Print Box Label Methods
        /// <summary>
        /// Печатает этикетку коробки
        /// </summary>
        [RelayCommand]
        public async Task PrintBoxLabel()
        {
            var metrics = CalculateAggregationMetrics();

            if (ShouldPrintFullBox(metrics))
            {
                await PrintBoxLabelInternal();
            }
            else if (ShouldShowPartialBoxConfirmation(metrics))
            {
                await HandlePartialBoxPrinting();
            }
        }
        private bool ShouldPrintFullBox(AggregationMetrics metrics)
        {
            return metrics.ValidCount == metrics.TotalCells &&
                   metrics.TotalCells > 0 &&
                   metrics.ValidCount == numberOfLayers;
        }
        private async Task PrintBoxLabelInternal()
        {
            // Используем текущий зарезервированный короб из сессии
            _printingService.PrintReport(_sessionService.SelectedTaskInfo.BOX_TEMPLATE, true);
            ShowMessage($"Этикетка короба {_sessionService.SelectedTaskSscc.CHECK_BAR_CODE} отправлена на печать", NotificationType.Success);
        }
        private bool ShouldShowPartialBoxConfirmation(AggregationMetrics metrics)
        {
            return metrics.ValidCount == metrics.TotalCells &&
                   metrics.TotalCells > 0 &&
                   metrics.ValidCount < numberOfLayers;
        }
        private async Task HandlePartialBoxPrinting()
        {
            bool confirmed = await _dialogService.ShowCustomConfirmationAsync(
                "Подтверждение печати этикетки",
                "Распечатать этикетку на не полный короб?",
                Material.Icons.MaterialIconKind.Printer,
                Avalonia.Media.Brushes.MediumSeaGreen,
                Avalonia.Media.Brushes.MediumSeaGreen,
                "Да",
                "Нет"
            );

            if (confirmed)
            {
                await PrintBoxLabelInternal();
            }
        }
        #endregion

        #region Complete Aggregation
        /// <summary>
        /// Завершает агрегацию и закрывает задание
        /// </summary>
        [RelayCommand]
        public async Task CompleteAggregation()
        {
            bool confirmed = await _dialogService.ShowCustomConfirmationAsync(
                "Завершение агрегации",
                "Завершить агрегацию и закрыть задание?",
                Material.Icons.MaterialIconKind.ContentSaveAlert,
                Avalonia.Media.Brushes.MediumSeaGreen,
                Avalonia.Media.Brushes.MediumSeaGreen,
                "Да",
                "Нет"
            );

            if (confirmed)
            {
                await CompleteAggregationInternal();
            }
        }
        private async Task CompleteAggregationInternal()
        {
            _dmScanService.StopScan();
            await _databaseDataService.CloseAggregationSession();
            await _databaseDataService.CloseJob();

            _sessionService.ClearCachedAggregationData();
            _sessionService.ClearScannedCodes();
            _sessionService.ClearCurrentBoxCodes();

            ShowMessage("Агрегация завершена.", NotificationType.Success);
            _router.GoTo<TaskListViewModel>();
        }
        #endregion

        #region Open Template Settings
        /// <summary>
        /// Открывает окно настроек шаблона
        /// </summary>
        [RelayCommand]
        public void OpenTemplateSettings()
        {
            var window = new TemplateSettingsWindow
            {
                DataContext = this
            };

            if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                window.ShowDialog(desktop.MainWindow);
            }
        }
        #endregion

        #region Barcode Handling Methods
        /// <summary>
        /// Обрабатывает отсканированный штрих-код в зависимости от текущего режима работы
        /// </summary>
        /// <param name="barcode">Отсканированный штрих-код</param>
        public void HandleScannedBarcode(string barcode)
        {
            if (IsInfoMode)
            {
                HandleInfoModeBarcode(barcode);
                return;
            }

            if (IsDisaggregationMode)
            {
                HandleDisaggregationModeBarcode(barcode);
                return;
            }

            if (CurrentStepIndex != AggregationStep.BoxAggregation && CurrentStepIndex != AggregationStep.PalletAggregation)
                return;

            HandleNormalModeBarcode(barcode);
        }
        #region Barcode Handling Info Methods
        private async Task HandleInfoModeBarcode(string barcode)
        {
            if (!IsInfoMode)
                return;

            try
            {
                var ssccRecord = await _databaseDataService.FindSsccCode(barcode);
                if (ssccRecord != null)
                {
                    AggregationSummaryText = _textGenerationService.BuildSsccInfo(ssccRecord);
                    return;
                }

                var gS1Parser = new GS1Parser();
                var newGS = gS1Parser.ParseGTIN(barcode);
                var parsedData = newGS.SerialNumber;

                var unRecord = await _databaseDataService.FindUnCode(parsedData);
                if (unRecord != null)
                {
                    AggregationSummaryText = _textGenerationService.BuildUnInfo(unRecord);
                    return;
                }

                AggregationSummaryText = _textGenerationService.BuildCodeNotFoundInfo(barcode);
            }
            catch (Exception ex)
            {
                AggregationSummaryText = _textGenerationService.BuildErrorInfo(barcode, ex.Message);
            }
        }
        #endregion

        #region Barcode Handling Disaggregation Methods
        private async Task HandleDisaggregationModeBarcode(string barcode)
        {
            if (!IsDisaggregationMode)
                return;

            try
            {
                var boxRecord = _sessionService.CachedSsccResponse.RECORDSET
                    .Where(r => r.TYPEID == (int)SsccType.Box)
                    .FirstOrDefault(r => r.CHECK_BAR_CODE == barcode);

                if (boxRecord != null)
                {
                    await ProcessDisaggregationRequest(barcode, boxRecord);
                }
                else
                {
                    AggregationSummaryText = _textGenerationService.BuildBoxNotFoundInfo(barcode);
                }
            }
            catch (Exception ex)
            {
                AggregationSummaryText = _textGenerationService.BuildErrorInfo(barcode, ex.Message);
            }
        }

        private async Task ProcessDisaggregationRequest(string barcode, ArmJobSsccRecord boxRecord)
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
                await ExecuteDisaggregation(barcode, boxRecord);
            }
            else
            {
                AggregationSummaryText = _textGenerationService.BuildDisaggregationCancelledInfo(barcode);
            }
        }

        private async Task ExecuteDisaggregation(string barcode, ArmJobSsccRecord boxRecord)
        {
            var success = await _databaseDataService.ClearBoxAggregation(boxRecord.CHECK_BAR_CODE);

            if (success)
            {
                AggregationSummaryText = _textGenerationService.BuildDisaggregationSuccessInfo(barcode, boxRecord);
                //UpdateScannedCodesAfterDisaggregation();
                UpdateDisaggregationAvailability();
            }
            else
            {
                AggregationSummaryText = _textGenerationService.BuildDisaggregationFailureInfo(barcode, boxRecord);
            }
        }
        private async Task UpdateDisaggregationAvailability()
        {
            try
            {
                var countersResponse = await _databaseDataService.GetArmCounters();

                if (countersResponse?.RECORDSET != null)
                {
                    var hasAggregatedBoxes = _sessionService.CachedSsccResponse?.RECORDSET?
                        .Where(r => r.TYPEID == (int)SsccType.Box)
                        .Any(r => r.QTY > 0);

                    CanDisaggregation = hasAggregatedBoxes == true;
                }
                else
                {
                    CanDisaggregation = false;
                }
            }
            catch (Exception ex)
            {
                CanDisaggregation = false;
                ShowMessage($"Ошибка проверки доступности очистки короба: {ex.Message}", NotificationType.Error);
            }
        }
        #endregion

        #region Barcode Handling Normal Methods

        private void HandleNormalModeBarcode(string barcode)
        {
            if (CurrentStepIndex != AggregationStep.BoxAggregation)
                return;

            ProcessNormalModeBarcode(barcode);
        }

        private void ProcessNormalModeBarcode(string barcode)
        {
            var foundRecord = _sessionService.CachedSsccResponse.RECORDSET
                .Where(r => r.TYPEID == (int)SsccType.Box)
                .FirstOrDefault(r => r.CHECK_BAR_CODE == barcode);

            if (foundRecord != null)
            {
                HandleFoundBarcode(barcode, foundRecord);
            }
            else
            {
                ShowMessage($"ШК {barcode} не найден в списке!", NotificationType.Error);
            }
        }
        private async Task HandleFoundBarcode(string barcode, ArmJobSsccRecord foundRecord)
        {
            // Проверяем, есть ли уже агрегированные коды в отсканированной коробке
            if (foundRecord.QTY > 0)
            {
                ShowMessage($"Коробка с ШК {barcode} уже содержит {foundRecord.QTY} агрегированных кодов!", NotificationType.Warning);
                return;
            }

            // 2. Используем ОТСКАНИРОВАННУЮ коробку, а не получаем новую свободную
            _sessionService.SelectedTaskSscc = foundRecord;

            ShowMessage($"Коробка с ШК {barcode} готова для агрегации", NotificationType.Success);

            // 3. Сохраняем агрегацию в ОТСКАНИРОВАННУЮ коробку
            if (await SaveAllDmCells())
            {
                await ProcessSuccessfulAggregation();
            }
        }
        private async Task<bool> SaveAllDmCells()
        {
            try
            {
                var aggregationData = new List<(string UNID, string CHECK_BAR_CODE)>();
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
                        ShowMessage($"Не найден SGTIN для серийного номера: {parsedData.SerialNumber}", NotificationType.Error);
                    }
                }

                if (aggregationData.Count > 0)
                {
                    var success = await _databaseDataService.LogAggregationCompletedBatch(aggregationData);

                    if (success)
                    {
                        ShowMessage($"Сохранено {aggregationData.Count} кодов агрегации", NotificationType.Success);
                        return true;
                    }
                    else
                    {
                        ShowMessage("Ошибка при сохранении кодов агрегации", NotificationType.Error);
                        return false;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                ShowMessage($"Ошибка сохранения кодов агрегации: {ex.Message}", NotificationType.Error);
                return false;
            }
        }
        private async Task ProcessSuccessfulAggregation()
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
            CanPrintBoxLabel = false;
            CanScan = true;
            // Обновляем CurrentBox на основе реального количества агрегированных коробов
            await GetCurrentBox();

            //CurrentBox++;
            CurrentLayer = 1;
            CurrentStepIndex = AggregationStep.PackAggregation;
            // Обновляем информацию об агрегации после успешного завершения коробки
            AggregationSummaryText = _textGenerationService.BuildAggregationSummaryAfterBoxCompletion(CurrentBox, CurrentLayer);
            InfoLayerText = _textGenerationService.GetNewBoxText(CurrentBox);
        }
        #endregion
        #endregion

        #region Cell Processing Methods
        /// <summary>
        /// Обрабатывает клик по ячейке и отображает детальную информацию
        /// </summary>
        /// <param name="cell">Выбранная ячейка</param>
        public async void OnCellClicked(DmCellViewModel cell)
        {
            _previousAggregationSummaryText = AggregationSummaryText;

            await ProcessCellImage(cell);
            UpdateCellPopupData(cell);
            DisplayCellInformation(cell);

            IsPopupOpen = true;
        }


        private async Task ProcessCellImage(DmCellViewModel cell)
        {
            double boxRadius = Math.Sqrt(dmrData.BOXs[0].height * dmrData.BOXs[0].height +
                         dmrData.BOXs[0].width * dmrData.BOXs[0].width) / 2;

            int minX = (int)dmrData.BOXs.Min(d => d.poseX - boxRadius);
            int minY = (int)dmrData.BOXs.Min(d => d.poseY - boxRadius);

            var cropped = _imageProcessingService.CropImage(
                _croppedImageRaw, cell.X, cell.Y, cell.SizeWidth, cell.SizeHeight,
                scaleXObrat, scaleYObrat, (float)cell.Angle);

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                SelectedSquareImage = _imageProcessingService.ConvertToAvaloniaBitmap(cropped);
                await Task.Delay(AggregationConstants.UI_DELAY);
            });
        }
        #region UpdateCellPopupData
        private void UpdateCellPopupData(DmCellViewModel cell)
        {
            var scaleXCell = ImageSizeCell.Width / SelectedSquareImage.PixelSize.Width;
            var scaleYCell = ImageSizeCell.Height / SelectedSquareImage.PixelSize.Height;

            var newOcrList = CreateScaledOcrList(cell, scaleXCell, scaleYCell);

            cell.OcrCellsInPopUp.Clear();
            foreach (var newOcr in newOcrList)
                cell.OcrCellsInPopUp.Add(newOcr);
        }
        private ObservableCollection<SquareCellViewModel> CreateScaledOcrList(DmCellViewModel cell, double scaleXCell, double scaleYCell)
        {
            var newOcrList = new ObservableCollection<SquareCellViewModel>();

            // Добавляем OCR элементы
            foreach (var ocr in cell.OcrCells)
            {
                newOcrList.Add(new SquareCellViewModel
                {
                    X = ocr.X * scaleXCell,
                    Y = ocr.Y * scaleYCell,
                    SizeWidth = ocr.SizeWidth * scaleXCell,
                    SizeHeight = ocr.SizeHeight * scaleYCell,
                    IsValid = ocr.IsValid,
                    Angle = ocr.Angle,
                    OcrName = ocr.OcrName,
                    OcrText = ocr.OcrText
                });
            }

            // Добавляем DM элемент (если есть)
            if (cell.Dm_data.Data != null)
            {
                newOcrList.Add(new SquareCellViewModel
                {
                    X = cell.Dm_data.X * scaleXCell,
                    Y = cell.Dm_data.Y * scaleYCell,
                    SizeWidth = cell.Dm_data.SizeWidth * scaleYCell,
                    SizeHeight = cell.Dm_data.SizeHeight * scaleYCell,
                    IsValid = cell.Dm_data.IsValid,
                    Angle = cell.Dm_data.Angle,
                    OcrName = "DM",
                    OcrText = cell.Dm_data.Data ?? "пусто"
                });
            }

            return newOcrList;
        }
        #endregion
        #region DisplayCellInformation
        private void DisplayCellInformation(DmCellViewModel cell)
        {
            var (gtin, serialNumber, duplicateStatus) = ExtractCellData(cell);

            AggregationSummaryText = _textGenerationService.BuildCellInfoSummary(cell);
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
        #endregion
        #endregion
        
        # region Mode Methods
        #region Info Mode Methods
        #region Enter Info Mode Methods
        private void EnterInfoMode()
        {
            PreviousStepIndex = CurrentStepIndex;
            CurrentStepIndex = AggregationStep.InfoMode;
            InfoModeButtonText = "Выйти из режима";

            // Сохраняем нормальное состояние только если еще не сохранено
            if (!_isNormalStateDataSaved)
            {
                SaveNormalModeState();
            }

            SaveCurrentButtonStates();
            UpdateScanAvailability();

            InfoLayerText = "Режим информации: отсканируйте код для получения информации";
            AggregationSummaryText = "Режим информации активен. \nОтсканируйте код для получения подробной информации о нем.";

            ShowMessage("Активирован режим информации", NotificationType.Info);
        }
        private void SaveCurrentButtonStates()
        {
            _previousCanScan = CanScan;
            _previousCanScanHardware = CanScanHardware;
            _previousCanOpenTemplateSettings = CanOpenTemplateSettings;
            _previousCanPrintBoxLabel = CanPrintBoxLabel;
            _previousCanClearBox = CanClearBox;
            _previousCanCompleteAggregation = CanCompleteAggregation;
            _previousInfoLayerText = InfoLayerText;
        }

        #endregion
        #region Exit Info Mode Methods
        private void ExitInfoMode()
        {
            CurrentStepIndex = PreviousStepIndex;
            InfoModeButtonText = "Режим информации";

            RestoreButtonStates();
            // Восстанавливаем нормальное состояние только если не активен другой режим
            if (!IsDisaggregationMode)
            {
                RestoreNormalModeState();
            }
            //AggregationSummaryText = _previousAggregationSummaryText;

            ShowMessage("Режим информации деактивирован", NotificationType.Info);
        }
        private void RestoreButtonStates()
        {
            CanScan = _previousCanScan;
            CanScanHardware = _previousCanScanHardware;
            CanOpenTemplateSettings = _previousCanOpenTemplateSettings;
            CanPrintBoxLabel = _previousCanPrintBoxLabel;
            CanClearBox = _previousCanClearBox;
            CanCompleteAggregation = _previousCanCompleteAggregation;
            InfoLayerText = _previousInfoLayerText;
        }
        private void RestoreNormalModeState()
        {
            if (_isNormalStateDataSaved)
            {
                InfoLayerText = _normalModeInfoLayerText;
                AggregationSummaryText = _normalModeAggregationSummaryText;
                _isNormalStateDataSaved = false;
            }
        }
        #endregion
        #endregion

        #region Disaggregation Mode Methods
        #region Enter Disaggregation Mode
        private void EnterDisaggregationMode()
        {
            PreviousStepIndex = CurrentStepIndex;
            CurrentStepIndex = AggregationStep.DisaggregationMode;
            DisaggregationModeButtonText = "Выйти из режима";


            // Сохраняем нормальное состояние только если еще не сохранено
            if (!_isNormalStateDataSaved)
            {
                SaveNormalModeState();
            }
            SaveDisaggregationButtonStates();
            UpdateScanAvailability();
            InfoLayerText = _textGenerationService.GetDisaggregationModeText();
            AggregationSummaryText = _textGenerationService.GetDisaggregationModeMessage();

            ShowMessage("Активирован режим очистки короба", NotificationType.Info);
        }
        private void SaveDisaggregationButtonStates()
        {
            _previousCanScanDisaggregation = CanScan;
            _previousCanScanHardwareDisaggregation = CanScanHardware;
            _previousCanOpenTemplateSettingsDisaggregation = CanOpenTemplateSettings;
            _previousCanPrintBoxLabelDisaggregation = CanPrintBoxLabel;
            _previousCanClearBoxDisaggregation = CanClearBox;
            _previousCanCompleteAggregationDisaggregation = CanCompleteAggregation;
            _previousInfoLayerTextDisaggregation = InfoLayerText;
        }
        #endregion
        #region Exit Disaggregation Mode
        private void ExitDisaggregationMode()
        {
            CurrentStepIndex = PreviousStepIndex;
            DisaggregationModeButtonText = "Режим очистки короба";
            RestoreDisaggregationButtonStates();

            // Обновляем отсканированные коды после выхода из режима разагрегации
            UpdateScannedCodesAfterDisaggregation();
            // Восстанавливаем нормальное состояние только если не активен другой режим
            //if (!IsInfoMode)
            //{
            //    RestoreNormalModeState();
            //}

            ShowMessage("Режим очистки короба деактивирован", NotificationType.Info);
        }
        // Методы для сохранения и восстановления нормального состояния

        private void RestoreDisaggregationButtonStates()
        {
            CanScan = _previousCanScanDisaggregation;
            CanScanHardware = _previousCanScanHardwareDisaggregation;
            CanOpenTemplateSettings = _previousCanOpenTemplateSettingsDisaggregation;
            CanPrintBoxLabel = _previousCanPrintBoxLabelDisaggregation;
            CanClearBox = _previousCanClearBoxDisaggregation;
            CanCompleteAggregation = _previousCanCompleteAggregationDisaggregation;
            InfoLayerText = _previousInfoLayerTextDisaggregation;
        }
        private async Task UpdateScannedCodesAfterDisaggregation()
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

                    ShowMessage($"Обновлено {aggregatedCodes.Count} кодов после очистки короба", NotificationType.Info);
                }
                else
                {
                    _sessionService.ClearScannedCodes();
                    ShowMessage("Все коды разагрегированы", NotificationType.Info);
                }

                // Обновляем CurrentBox после разагрегации
                await GetCurrentBox();

                InfoLayerText = _textGenerationService.BuildInfoLayerText(CurrentLayer, _sessionService.SelectedTaskInfo?.LAYERS_QTY ?? 0, 0, numberOfLayers);
                AggregationSummaryText = _textGenerationService.BuildInitialAggregationSummary(CurrentBox, CurrentLayer);
            }
            catch (Exception ex)
            {
                ShowMessage($"Ошибка обновления кодов после очистки короба: {ex.Message}", NotificationType.Error);
            }
        }
        #endregion
        #endregion
        private void SaveNormalModeState()
        {
            _normalModeInfoLayerText = InfoLayerText;
            _normalModeAggregationSummaryText = AggregationSummaryText;
            _isNormalStateDataSaved = true;
        }
        #endregion

        #region UI Update Methods

        private void UpdateScanAvailability()
        {
            if (IsInfoMode)
            {
                DisableAllButtonsForInfoMode();
            }
            else if (IsDisaggregationMode)
            {
                DisableAllButtonsForDisaggregationMode();
            }
            else
            {
                EnableNormalModeButtons();
            }
        }

        private void DisableAllButtonsForInfoMode()
        {
            CanScan = false;
            CanScanHardware = false;
            CanOpenTemplateSettings = false;
            CanPrintBoxLabel = false;
            CanClearBox = false;
            CanCompleteAggregation = false;
        }

        private void DisableAllButtonsForDisaggregationMode()
        {
            CanScan = false;
            CanScanHardware = false;
            CanOpenTemplateSettings = false;
            CanPrintBoxLabel = false;
            CanClearBox = false;
        }

        private void EnableNormalModeButtons()
        {
            CanScan = IsControllerAvailable && TemplateFields.Count > 0;
            CanScanHardware = IsControllerAvailable && TemplateFields.Count > 0;
        }
        #endregion
        
        #region Get Current Box
        // Метод для получения CurrentBox
        private async Task GetCurrentBox()
        {
            try
            {
                var aggregatedBoxesCount = await _databaseDataService.GetAggregatedBoxesCount();
                CurrentBox = aggregatedBoxesCount + 1;
                if (aggregatedBoxesCount > 0)
                {
                    ShowMessage($"Продолжение агрегации с короба №{CurrentBox} (агрегировано: {aggregatedBoxesCount})", NotificationType.Info);
                }
                else
                {
                    ShowMessage("Начинаем новую агрегацию с короба №1", NotificationType.Info);
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"Ошибка обновления CurrentBox: {ex.Message}", NotificationType.Error);
            }
        }
        #endregion

        #region Cleanup
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _sessionService.ClearCurrentBoxCodes();
                //await _databaseDataService.CloseAggregationSession();
                _plcConnection?.StopPingPong();
                _plcConnection?.Disconnect();
                _plcConnection?.Dispose();
                _dmScanService?.Dispose();
            }
            //base.Dispose(disposing);
        }
        #endregion
    }
}

