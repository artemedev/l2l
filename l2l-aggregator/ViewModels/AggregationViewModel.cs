using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.SimpleRouter;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DM_wraper_NS;
using l2l_aggregator.Helpers.AggregationHelpers;
using l2l_aggregator.Models;
using l2l_aggregator.Services;
using l2l_aggregator.Services.AggregationService;
using l2l_aggregator.Services.ControllerService;
using l2l_aggregator.Services.Database;
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
        public const int PACK_AGGREGATION_STEP = 1;
        public const int BOX_AGGREGATION_STEP = 2;
        public const int PALLET_AGGREGATION_STEP = 3;
        public const int BOX_SCANNING_STEP = 4;
        public const int PALLET_SCANNING_STEP = 5;
        public const int INFO_MODE_STEP = 6;
        public const int DISAGGREGATION_MODE_STEP = 7;

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
        PackAggregation = AggregationConstants.PACK_AGGREGATION_STEP,
        BoxAggregation = AggregationConstants.BOX_AGGREGATION_STEP,
        PalletAggregation = AggregationConstants.PALLET_AGGREGATION_STEP,
        BoxScanning = AggregationConstants.BOX_SCANNING_STEP,
        PalletScanning = AggregationConstants.PALLET_SCANNING_STEP,
        InfoMode = AggregationConstants.INFO_MODE_STEP,
        DisaggregationMode = AggregationConstants.DISAGGREGATION_MODE_STEP
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

    internal record AggregationMetrics(
        int ValidCount,
        int DuplicatesInCurrentScan,
        int DuplicatesInAllScans,
        int TotalCells
    );

    internal record DuplicateInformation(int InCurrentScan, int InAllScans)
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
        private readonly ImageHelper _imageProcessingService;

        //сервис обработки шаблона, после выбора пользователя элементов в ui. Для дальнейшей отправки в библиотеку распознавания
        private readonly TemplateService _templateService;

        //сервис работы с бд
        private readonly DatabaseService _databaseService;

        //сервис нотификаций
        private readonly INotificationService _notificationService;

        //сервис роутинга
        private readonly HistoryRouter<ViewModelBase> _router;

        //сервис принтера
        private readonly PrintingService _printingService;

        private readonly DatabaseDataService _databaseDataService;

        //сервис обработки и работы с библиотекой распознавания
        private readonly DmScanService _dmScanService;

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
        public ObservableCollection<TemplateField> TemplateFields { get; } = new();

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

        #region Properties

        //свойство для получения данных SSCC из кэша сессии
        private ArmJobSsccResponse? ResponseSscc => _sessionService.CachedSsccResponse;

        #endregion

        #region Constructor

        public AggregationViewModel(
            ImageHelper imageProcessingService,
            SessionService sessionService,
            TemplateService templateService,
            DmScanService dmScanService,
            ScannerListenerService scannerListener,
            DatabaseService databaseService,
            DatabaseDataService databaseDataService,
            ScannerInputService scannerInputService,
            INotificationService notificationService,
            HistoryRouter<ViewModelBase> router,
            PrintingService printingService,
            ILogger<PcPlcConnectionService> logger,
            IDialogService dialogService
            )
        {
            _sessionService = sessionService;
            _imageProcessingService = imageProcessingService;
            _templateService = templateService;
            _dmScanService = dmScanService;
            _databaseService = databaseService;
            _notificationService = notificationService;
            _router = router;
            _printingService = printingService;
            _logger = logger;
            _dialogService = dialogService;
            _databaseDataService = databaseDataService;

            ImageSizeChangedCommand = new RelayCommand<SizeChangedEventArgs>(OnImageSizeChanged);
            ImageSizeCellChangedCommand = new RelayCommand<SizeChangedEventArgs>(OnImageSizeCellChanged);

            InitializeAsync();
        }

        #endregion

        #region Initialization Methods

        private async void InitializeAsync()
        {
            InitializeBoxTemplate();
            InitializeNumberOfLayers();
            InitializeCurrentBoxFromCounters();
            InitializeSscc();
            InitializeTemplate();
            InitializeControllerPing();
            InitializeSession();
            InitializeScannedCodes();
            InitializeUpdateInfoAndUI();
        }

        private void InitializeUpdateInfoAndUI()
        {
            // Проверяем, является ли это началом новой агрегации
            bool isNewAggregation = CurrentBox == 1 && CurrentLayer == 1 && !_sessionService.AllScannedDmCodes.Any();

            if (isNewAggregation)
            {
                InfoLayerText = "Выберите элементы шаблона для агрегации и нажмите кнопку сканировать!";
            }
            else
            {
                InfoLayerText = "Продолжаем агрегацию!";
            }
            AggregationSummaryText = BuildInitialAggregationSummary();
        }

        private string BuildInitialAggregationSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Агрегируемая серия: {_sessionService.SelectedTaskInfo.RESOURCEID}");
            sb.AppendLine($"Количество собранных коробов: {CurrentBox - 1}");
            sb.AppendLine($"Номер собираемого короба: {CurrentBox}");
            sb.AppendLine($"Номер слоя: {CurrentLayer}");
            sb.AppendLine($"Количество слоев в коробе: {_sessionService.SelectedTaskInfo.LAYERS_QTY}");
            return sb.ToString();
        }

        private void InitializeBoxTemplate()
        {
            if (_sessionService.SelectedTaskInfo.BOX_TEMPLATE != null)
            {
                frxBoxBytes = _sessionService.SelectedTaskInfo.BOX_TEMPLATE;
            }
            else
            {
                ShowErrorMessage("Ошибка: шаблон коробки отсутствует.", NotificationType.Warning);
            }
        }

        private void InitializeNumberOfLayers()
        {
            var validation = ValidateTaskInfo();
            if (!validation.IsValid)
            {
                ShowErrorMessage(validation.ErrorMessage);
                return;
            }

            var inBoxQty = _sessionService.SelectedTaskInfo.IN_BOX_QTY ?? 0;
            var layersQty = _sessionService.SelectedTaskInfo.LAYERS_QTY ?? 0;

            if (layersQty > 0)
            {
                numberOfLayers = inBoxQty / layersQty; //колличество пачек в слое
            }
            else
            {
                numberOfLayers = 0;
                ShowErrorMessage("Ошибка: некорректное количество слоев (LAYERS_QTY).");
            }
        }

        private void InitializeSscc()
        {
            if (ResponseSscc == null)
            {
                ShowErrorMessage("Ошибка: SSCC данные не загружены.");
                return;
            }

            var freeBox = GetCurrentFreeBox();
            if (freeBox != null)
            {
                _sessionService.SelectedTaskSscc = freeBox;
            }
            else
            {
                ShowErrorMessage("Не удалось получить свободный короб для агрегации.");
                // Fallback: используем первый доступный короб из ResponseSscc
               // _sessionService.SelectedTaskSscc = ResponseSscc.RECORDSET.FirstOrDefault();
            }
        }

        private void InitializeCurrentBoxFromCounters()
        {
            try
            {
                var aggregatedBoxesCount = _databaseDataService.GetAggregatedBoxesCount();

                // CurrentBox = количество агрегированных коробов + 1 (следующий короб для агрегации)
                CurrentBox = aggregatedBoxesCount + 1;

                if (aggregatedBoxesCount > 0)
                {
                    ShowInfoMessage($"Продолжение агрегации с короба №{CurrentBox} (агрегировано: {aggregatedBoxesCount})");
                }
                else
                {
                    ShowInfoMessage("Начинаем новую агрегацию с короба №1");
                }
            }
            catch (Exception ex)
            {
                CurrentBox = 1;
                ShowErrorMessage($"Ошибка инициализации CurrentBox: {ex.Message}", NotificationType.Error);
            }
        }

        private void InitializeTemplate()
        {
            TemplateFields.Clear();

            if (_sessionService.SelectedTaskInfo?.UN_TEMPLATE_FR != null)
            {
                var loadedFields = _templateService.LoadTemplate(_sessionService.SelectedTaskInfo.UN_TEMPLATE_FR);
                foreach (var f in loadedFields)
                    TemplateFields.Add(f);
            }
            else
            {
                ShowErrorMessage("Ошибка: шаблон распознавания отсутствует.");
            }

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
                    ShowSuccessMessage("Контроллер подключен и мониторинг активен");
                }
                else
                {
                    IsControllerAvailable = false;
                    ShowErrorMessage("Не удалось подключиться к контроллеру", NotificationType.Error);
                }
            }
            catch (Exception ex)
            {
                IsControllerAvailable = false;
                ShowErrorMessage($"Ошибка инициализации контроллера: {ex.Message}", NotificationType.Error);
            }
        }

        private void InitializeSession()
        {
            var validation = ValidateTaskInfo();
            if (!validation.IsValid)
            {
                ShowErrorMessage(validation.ErrorMessage);
            }
        }

        private void InitializeScannedCodes()
        {
            try
            {
                if (_sessionService.SelectedTaskInfo?.DOCID == null)
                {
                    ShowErrorMessage("Ошибка: отсутствует информация о задании для загрузки отсканированных кодов.", NotificationType.Warning);
                    return;
                }

                var aggregatedCodes = _databaseDataService.GetAggregatedUnCodes();

                if (aggregatedCodes?.Any() == true)
                {
                    _sessionService.ClearScannedCodes();

                    foreach (var code in aggregatedCodes.Where(c => !string.IsNullOrWhiteSpace(c)))
                    {
                        _sessionService.AllScannedDmCodes.Add(code);
                    }

                    ShowInfoMessage($"Загружено {aggregatedCodes.Count} ранее отсканированных кодов");
                }
                else
                {
                    _sessionService.ClearScannedCodes();
                    ShowInfoMessage("Начинаем новую агрегацию - ранее отсканированных кодов не найдено");
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка загрузки отсканированных кодов: {ex.Message}", NotificationType.Error);
                _sessionService.ClearScannedCodes();
            }
        }

        #endregion

        #region Validation Methods

        private ValidationResult ValidateTaskInfo()
        {
            if (_sessionService.SelectedTaskInfo == null)
                return ValidationResult.Error("Отсутствует информация о задании.");

            return ValidationResult.Success();
        }

        private ValidationResult ValidateSessionData()
        {
            var taskValidation = ValidateTaskInfo();
            if (!taskValidation.IsValid)
                return taskValidation;

            //if (ResponseSscc?.RECORDSET == null || !ResponseSscc.RECORDSET.Any())
            //    return ValidationResult.Error("Данные SSCC отсутствуют.");

            return ValidationResult.Success();
        }

        #endregion

        #region Helper Methods

        private void ShowErrorMessage(string message, NotificationType type = NotificationType.Error)
        {
            InfoMessage = message;
            _notificationService.ShowMessage(InfoMessage, type);
        }

        private void ShowSuccessMessage(string message)
        {
            _notificationService.ShowMessage(message, NotificationType.Success);
        }

        private void ShowInfoMessage(string message)
        {
            _notificationService.ShowMessage(message, NotificationType.Info);
        }

        //private ArmJobSsccRecord? FindBoxRecord(int boxIndex)
        //{
        //    return ResponseSscc.RECORDSET
        //        .Where(r => r.TYPEID == (int)SsccType.Box)
        //        .ElementAtOrDefault(boxIndex - 1);
        //}
        private ArmJobSsccRecord? GetCurrentFreeBox()
        {
            try
            {
                var freeBox = _databaseDataService.ReserveFreeBox();

                if (freeBox != null)
                {
                    ShowInfoMessage($"Зарезервирован короб: {freeBox.CHECK_BAR_CODE}");
                    return freeBox;
                }
                else
                {
                    ShowErrorMessage("Нет доступных свободных коробов для агрегации", NotificationType.Warning);
                    return null;
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка получения свободного короба: {ex.Message}", NotificationType.Error);
                return null;
            }
        }

        private IEnumerable<string> GetValidCodesFromCells()
        {
            return DMCells
                .Where(c => c.IsValid && !string.IsNullOrWhiteSpace(c.Dm_data?.Data))
                .Select(c => c.Dm_data.Data);
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

        private DuplicateInformation BuildDuplicateInfo(AggregationMetrics metrics)
        {
            return new DuplicateInformation(metrics.DuplicatesInCurrentScan, metrics.DuplicatesInAllScans);
        }

        private string BuildAggregationSummary(AggregationMetrics metrics, DuplicateInformation duplicateInfo)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Агрегируемая серия: {_sessionService.SelectedTaskInfo.RESOURCEID}");
            sb.AppendLine($"Количество собранных коробов: {CurrentBox - 1}");
            sb.AppendLine($"Номер собираемого короба: {CurrentBox}");
            sb.AppendLine($"Номер слоя: {CurrentLayer}");
            sb.AppendLine($"Количество слоев в коробе: {_sessionService.SelectedTaskInfo.LAYERS_QTY}");
            sb.AppendLine($"Количество СИ, распознанное в слое: {metrics.ValidCount}");
            sb.AppendLine($"Количество СИ, считанное в слое: {metrics.TotalCells}");
            sb.AppendLine($"Количество СИ, ожидаемое в слое: {numberOfLayers}{duplicateInfo.GetDisplayText()}");
            sb.AppendLine($"Всего СИ в коробе: {_sessionService.CurrentBoxDmCodes.Count}");
            return sb.ToString();
        }

        private bool IsLastLayerCompleted(AggregationMetrics metrics)
        {
            return CurrentLayer == _sessionService.SelectedTaskInfo.LAYERS_QTY &&
                   metrics.ValidCount == metrics.TotalCells &&
                   metrics.TotalCells > 0;
        }

        private bool IsLayerCompleted(AggregationMetrics metrics)
        {
            return CurrentLayer < _sessionService.SelectedTaskInfo.LAYERS_QTY &&
                   metrics.ValidCount == numberOfLayers &&
                   metrics.TotalCells > 0;
        }

        private bool HasValidCodes(AggregationMetrics metrics)
        {
            return CurrentLayer < _sessionService.SelectedTaskInfo.LAYERS_QTY &&
                   metrics.ValidCount == metrics.TotalCells &&
                   metrics.TotalCells > 0;
        }

        private CameraConfiguration CreateCameraConfiguration()
        {
            return new CameraConfiguration(
                CameraName: _sessionService.CameraIP,
                CameraModel: _sessionService.CameraModel
            );
        }

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

        private static bool IsOcrElement(string elementName) =>
            elementName is AggregationConstants.OCR_MEMO_VIEW or AggregationConstants.OCR_TEMPLATE_MEMO_VIEW;

        private static bool IsDmElement(string elementName) =>
            elementName is AggregationConstants.DM_BARCODE_VIEW or AggregationConstants.DM_TEMPLATE_BARCODE_VIEW;

        private async Task<bool> ExecuteWithErrorHandling(Func<Task> action, string operationName)
        {
            try
            {
                await action();
                return true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка {operationName}: {ex.Message}");
                return false;
            }
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
                ShowErrorMessage("Потеряно соединение с контроллером!", NotificationType.Error);
            }
            else
            {
                ShowSuccessMessage("Соединение с контроллером восстановлено");
            }
        }

        private void OnPlcErrorsReceived(PlcErrors errors)
        {
            string errorMessage = errors.GetErrorDescription();
            ShowErrorMessage($"Ошибки контроллера: {errorMessage}", NotificationType.Error);
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

        #region Public Methods

        /// <summary>
        /// Выполняет программное сканирование слоя с распознаванием кодов
        /// </summary>
        /// <returns>Задача, представляющая асинхронную операцию сканирования</returns>
        [RelayCommand]
        public async Task ScanSoftware()
        {
            var validation = ValidateTaskInfo();
            if (!validation.IsValid)
            {
                ShowErrorMessage(validation.ErrorMessage);
                return;
            }

            try
            {
                templateOk = SendTemplateToRecognizer();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка инициализации задания: {ex.Message}", NotificationType.Error);
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

        /// <summary>
        /// Выполняет аппаратное сканирование через контроллер
        /// </summary>
        /// <returns>Задача, представляющая асинхронную операцию сканирования</returns>
        [RelayCommand]
        public async Task ScanHardware()
        {
            var validation = ValidateTaskInfo();
            if (!validation.IsValid)
            {
                ShowErrorMessage(validation.ErrorMessage);
                return;
            }

            if (!templateOk)
            {
                ShowErrorMessage("Задание не инициализировано. Сначала нажмите 'Начать задание'.");
                return;
            }

            if (!await MoveCameraToCurrentLayerAsync())
                return;

            try
            {
                await _plcConnection.TriggerPhotoAsync();
                await StartScanningHardwareAsync();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка hardware trigger: {ex.Message}");
            }
        }

        /// <summary>
        /// Печатает этикетку коробки
        /// </summary>
        [RelayCommand]
        public async Task PrintBoxLabel()
        {
            if (!ValidateBoxLabelPrinting())
                return;

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

        #endregion

        #region Private Scanning Methods

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
                    ShowSuccessMessage("Шаблон распознавания успешно настроен");
                    return true;
                }
                catch (Exception ex)
                {
                    ShowErrorMessage("Ошибка настройки шаблона распознавания", NotificationType.Error);
                    return false;
                }
            }

            return true;
        }

        private async Task<bool> MoveCameraToCurrentLayerAsync()
        {
            if (!_sessionService.CheckController)
                return true;

            if (!IsControllerAvailable)
            {
                ShowErrorMessage("Контроллер недоступен!");
                return false;
            }

            var validation = ValidateTaskInfo();
            if (!validation.IsValid)
            {
                ShowErrorMessage("Информация о задании отсутствует.");
                return false;
            }

            var packHeight = _sessionService.SelectedTaskInfo.PACK_HEIGHT ?? 0;
            var layersQty = _sessionService.SelectedTaskInfo.LAYERS_QTY ?? 0;

            if (packHeight == 0)
            {
                ShowErrorMessage("Ошибка: не задана высота слоя (PACK_HEIGHT).");
                return false;
            }

            if (layersQty == 0)
            {
                ShowErrorMessage("Ошибка: не задано количество слоёв (LAYERS_QTY).");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_sessionService.ControllerIP))
            {
                ShowErrorMessage("IP контроллера не задан.");
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
                ShowErrorMessage($"Ошибка позиционирования: {ex.Message}");
                return false;
            }
        }

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
                    ShowErrorMessage($"Ошибка подтверждения фото: {ex.Message}");
                }
            }
        }

        private async Task StartScanningSoftwareAsync()
        {
            if (_lastUsedTemplateJson == null)
            {
                ShowErrorMessage("Шаблон не отправлен. Сначала выполните отправку шаблона.");
                return;
            }

            SetCurrentBoxRecord();

            if (!await TryReceiveScanDataSoftwareAsync() ||
                !await TryCropImageAsync() ||
                !await TryBuildCellsAsync())
                return;

            await UpdateInfoAndUI();
        }

        private async Task StartScanningHardwareAsync()
        {
            if (_lastUsedTemplateJson == null)
            {
                ShowErrorMessage("Шаблон не отправлен. Сначала выполните отправку шаблона.");
                return;
            }

            if (!await TryReceiveScanDataHardwareAsync() ||
                !await TryCropImageAsync() ||
                !await TryBuildCellsAsync())
                return;

            await UpdateInfoAndUI();
        }

        private void SetCurrentBoxRecord()
        {
            var freeBox = GetCurrentFreeBox();
            if (freeBox != null)
            {
                _sessionService.SelectedTaskSscc = freeBox;
            }
            else
            {
                ShowErrorMessage("Не удалось получить свободный короб для агрегации.");
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
                ShowErrorMessage($"Ошибка распознавания: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TryReceiveScanDataHardwareAsync()
        {
            try
            {
                CanOpenTemplateSettings = false;
                dmrData = await _dmScanService.WaitForResultAsync();
                return true;
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка распознавания: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TryCropImageAsync()
        {
            if (dmrData.rawImage == null)
            {
                ShowErrorMessage("Изображение из распознавания не получено.");
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

        private async Task<bool> TryBuildCellsAsync()
        {
            var validation = ValidateTaskInfo();
            if (!validation.IsValid)
            {
                ShowErrorMessage(validation.ErrorMessage);
                return false;
            }

            var docId = _sessionService.SelectedTaskInfo.DOCID;
            if (docId == 0)
            {
                ShowErrorMessage("Ошибка: некорректный ID документа.");
                return false;
            }

            var responseSgtin = _sessionService.CachedSgtinResponse;
            if (responseSgtin == null)
            {
                ShowErrorMessage("Ошибка загрузки данных SGTIN.");
                return false;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                DMCells.Clear();
                var cells = _imageProcessingService.BuildCellViewModels(
                    dmrData, scaleX, scaleY, _sessionService, TemplateFields,
                    responseSgtin, this, minX, minY);

                foreach (var cell in cells)
                {
                    DMCells.Add(cell);
                }
            });

            return true;
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

        private async Task UpdateInfoAndUI()
        {
            var metrics = CalculateAggregationMetrics();
            var duplicateInfo = BuildDuplicateInfo(metrics);

            UpdateInfoLayerText(metrics);
            UpdateAggregationSummaryText(metrics, duplicateInfo);
            UpdateButtonStates();

            var validCodes = GetValidCodesFromCells().ToList();

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

        private void UpdateInfoLayerText(AggregationMetrics metrics)
        {
            InfoLayerText = $"Слой {CurrentLayer} из {_sessionService.SelectedTaskInfo.LAYERS_QTY}. Распознано {metrics.ValidCount} из {numberOfLayers}";
        }

        private void UpdateAggregationSummaryText(AggregationMetrics metrics, DuplicateInformation duplicateInfo)
        {
            AggregationSummaryText = BuildAggregationSummary(metrics, duplicateInfo);
        }

        private void UpdateButtonStates()
        {
            CanScan = true;
            CanOpenTemplateSettings = true;
        }

        private async Task HandleLastLayerCompletion(List<string> validCodes)
        {
            _sessionService.AddLayerCodes(validCodes);

            CanOpenTemplateSettings = false;
            CanPrintBoxLabel = true;
            CurrentStepIndex = AggregationStep.BoxAggregation;

            _printingService.PrintReportTEST(frxBoxBytes, true);

            if (IsAutoPrintEnabled && validCodes.Count == numberOfLayers)
            {
                await PrintBoxLabel();
            }

            await ConfirmPhotoToPlcAsync();
        }

        private async Task HandleLayerCompletion(List<string> validCodes)
        {
            _sessionService.AddLayerCodes(validCodes);
            CurrentLayer++;
            await ConfirmPhotoToPlcAsync();
        }

        private void HandlePartialLayerCompletion(List<string> validCodes)
        {
            _sessionService.AddLayerCodes(validCodes);
        }

        #endregion

        #region Printing Methods

        private bool ValidateBoxLabelPrinting()
        {
            if (frxBoxBytes == null || frxBoxBytes.Length == 0)
            {
                ShowErrorMessage("Шаблон коробки не загружен.");
                return false;
            }

            if (ResponseSscc == null)
            {
                ShowErrorMessage("SSCC данные не загружены.");
                return false;
            }

            return true;
        }

        private bool ShouldPrintFullBox(AggregationMetrics metrics)
        {
            return metrics.ValidCount == metrics.TotalCells &&
                   metrics.TotalCells > 0 &&
                   metrics.ValidCount == numberOfLayers;
        }

        private bool ShouldShowPartialBoxConfirmation(AggregationMetrics metrics)
        {
            return metrics.ValidCount == metrics.TotalCells &&
                   metrics.TotalCells > 0 &&
                   metrics.ValidCount < numberOfLayers;
        }

        private async Task PrintBoxLabelInternal()
        {
            // Используем текущий зарезервированный короб из сессии
            if (_sessionService.SelectedTaskSscc != null)
            {
                _printingService.PrintReport(frxBoxBytes, true);
                ShowSuccessMessage($"Этикетка короба {_sessionService.SelectedTaskSscc.CHECK_BAR_CODE} отправлена на печать");
            }
            else
            {
                // Если по какой-то причине текущий короб не установлен, получаем новый
                var currentFreeBox = GetCurrentFreeBox();
                if (currentFreeBox != null)
                {
                    _sessionService.SelectedTaskSscc = currentFreeBox;
                    _printingService.PrintReport(frxBoxBytes, true);
                    ShowSuccessMessage($"Этикетка короба {currentFreeBox.CHECK_BAR_CODE} отправлена на печать");
                }
                else
                {
                    ShowErrorMessage("Не удалось получить данные короба для печати этикетки.");
                }
            }
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

        private async Task CompleteAggregationInternal()
        {
            _dmScanService.StopScan();
            _databaseDataService.CloseAggregationSession();
            _databaseDataService.CloseJob();

            _sessionService.ClearCachedAggregationData();
            _sessionService.ClearScannedCodes();
            _sessionService.ClearCurrentBoxCodes();

            ShowSuccessMessage("Агрегация завершена.");
            _router.GoTo<TaskListViewModel>();
        }

        #endregion

        #region Barcode Handling Methods

        private void HandleNormalModeBarcode(string barcode)
        {
            if (CurrentStepIndex != AggregationStep.BoxAggregation)
                return;

            var validation = ValidateSessionData();
            if (!validation.IsValid)
            {
                ShowErrorMessage(validation.ErrorMessage);
                return;
            }

            if (_sessionService.SelectedTaskSscc == null)
            {
                ShowErrorMessage("Данные SSCC отсутствуют.");
                return;
            }

            ProcessNormalModeBarcode(barcode);
        }

        private void ProcessNormalModeBarcode(string barcode)
        {
            var foundRecord = ResponseSscc.RECORDSET
                .Where(r => r.TYPEID == (int)SsccType.Box)
                .FirstOrDefault(r => r.CHECK_BAR_CODE == barcode);

            if (foundRecord != null)
            {
                HandleFoundBarcode(barcode, foundRecord);
            }
            else
            {
                ShowErrorMessage($"ШК {barcode} не найден в списке!");
            }
        }

        private void HandleFoundBarcode(string barcode, ArmJobSsccRecord foundRecord)
        {
            // Проверяем, есть ли уже агрегированные коды в отсканированной коробке
            if (foundRecord.QTY > 0)
            {
                ShowErrorMessage($"Коробка с ШК {barcode} уже содержит {foundRecord.QTY} агрегированных кодов!");
                return;
            }

            // 2. Используем ОТСКАНИРОВАННУЮ коробку, а не получаем новую свободную
            _sessionService.SelectedTaskSscc = foundRecord;

            ShowSuccessMessage($"Коробка с ШК {barcode} готова для агрегации");

            // 3. Сохраняем агрегацию в ОТСКАНИРОВАННУЮ коробку
            if (SaveAllDmCells())
            {
                ProcessSuccessfulAggregation();
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
            CanPrintBoxLabel = false;
            CanScan = true;
            // Обновляем CurrentBox на основе реального количества агрегированных коробов
            UpdateCurrentBox();

            //CurrentBox++;
            CurrentLayer = 1;
            CurrentStepIndex = AggregationStep.PackAggregation;
            // Обновляем информацию об агрегации после успешного завершения коробки
            UpdateAggregationSummaryAfterBoxCompletion();
        }
        /// <summary>
        /// Обновляет информацию об агрегации после завершения коробки
        /// </summary>
        private void UpdateAggregationSummaryAfterBoxCompletion()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Агрегируемая серия: {_sessionService.SelectedTaskInfo.RESOURCEID}");
            sb.AppendLine($"Количество собранных коробов: {CurrentBox - 1}");
            sb.AppendLine($"Номер собираемого короба: {CurrentBox}");
            sb.AppendLine($"Номер слоя: {CurrentLayer}");
            sb.AppendLine($"Количество слоев в коробе: {_sessionService.SelectedTaskInfo.LAYERS_QTY}");
            sb.AppendLine($"Количество СИ, ожидаемое в слое: {numberOfLayers}");
            sb.AppendLine($"Всего агрегированных СИ: {_sessionService.AllScannedDmCodes.Count}");
            sb.AppendLine();
            sb.AppendLine("Коробка успешно агрегирована!");
            sb.AppendLine("Готов к сканированию.");

            AggregationSummaryText = sb.ToString();

            // Обновляем также информационный текст слоя
            InfoLayerText = $"Коробка {CurrentBox - 1} завершена. Начинаем новую коробку {CurrentBox}. Выберите элементы шаблона для агрегации и нажмите кнопку сканировать!";
        }
        #endregion

        #region Cell Processing Methods

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

        private void DisplayCellInformation(DmCellViewModel cell)
        {
            var (gtin, serialNumber, duplicateStatus) = ExtractCellData(cell);

            AggregationSummaryText = BuildCellInfoSummary(cell, gtin, serialNumber, duplicateStatus);
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

        private string BuildCellInfoSummary(DmCellViewModel cell, string gtin, string serialNumber, string duplicateStatus)
        {
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

        #endregion

        #region Data Persistence Methods

        private bool SaveAllDmCells()
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
                        ShowErrorMessage($"Не найден SGTIN для серийного номера: {parsedData.SerialNumber}", NotificationType.Warning);
                    }
                }

                if (aggregationData.Count > 0)
                {
                    var success = _databaseDataService.LogAggregationCompletedBatch(aggregationData);

                    if (success)
                    {
                        ShowSuccessMessage($"Сохранено {aggregationData.Count} кодов агрегации");
                        return true;
                    }
                    else
                    {
                        ShowErrorMessage("Ошибка при сохранении кодов агрегации", NotificationType.Error);
                        return false;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка сохранения кодов агрегации: {ex.Message}", NotificationType.Error);
                return false;
            }
        }

        #endregion

        #region Info Mode Methods

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
            DisableAllButtonsForInfoMode();

            InfoLayerText = "Режим информации: отсканируйте код для получения информации";
            AggregationSummaryText = "Режим информации активен. \nОтсканируйте код для получения подробной информации о нем.";

            ShowInfoMessage("Активирован режим информации");
        }

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

            ShowInfoMessage("Режим информации деактивирован");
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

        private void HandleInfoModeBarcode(string barcode)
        {
            if (!IsInfoMode)
                return;

            try
            {
                var ssccRecord = _databaseDataService.FindSsccCode(barcode);
                if (ssccRecord != null)
                {
                    DisplaySsccInfo(ssccRecord);
                    return;
                }

                var gS1Parser = new GS1Parser();
                var newGS = gS1Parser.ParseGTIN(barcode);
                var parsedData = newGS.SerialNumber;

                var unRecord = _databaseDataService.FindUnCode(parsedData);
                if (unRecord != null)
                {
                    DisplayUnInfo(unRecord);
                    return;
                }

                DisplayCodeNotFound(barcode);
            }
            catch (Exception ex)
            {
                DisplayErrorInfo(barcode, ex.Message);
            }
        }

        private void DisplaySsccInfo(ArmJobSsccRecord ssccRecord)
        {
            string typeDescription = ssccRecord.TYPEID switch
            {
                (int)SsccType.Box => "Коробка",
                (int)SsccType.Pallet => "Паллета",
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

            AggregationSummaryText = sb.ToString();
            ShowSuccessMessage($"Найден SSCC код: {typeDescription}");
        }

        private void DisplayUnInfo(ArmJobSgtinRecord unRecord)
        {
            string typeDescription = unRecord.UN_TYPE switch
            {
                (int)UnType.ConsumerPackage => "Потребительская упаковка",
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

            AggregationSummaryText = sb.ToString();
            ShowSuccessMessage($"Найден UN код: {typeDescription}");
        }

        private void DisplayCodeNotFound(string barcode)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Код не найден в базе данных!");
            sb.AppendLine();
            sb.AppendLine($"Отсканированный код: {barcode}");
            sb.AppendLine("Статус: Код не найден в системе");
            sb.AppendLine();
            sb.AppendLine("Проверьте правильность кода или обратитесь к администратору.");

            AggregationSummaryText = sb.ToString();
            ShowErrorMessage($"Код {barcode} не найден в базе данных", NotificationType.Warning);
        }

        private void DisplayErrorInfo(string barcode, string errorMessage)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Ошибка поиска кода!");
            sb.AppendLine();
            sb.AppendLine($"Отсканированный код: {barcode}");
            sb.AppendLine($"Ошибка: {errorMessage}");

            AggregationSummaryText = sb.ToString();
            ShowErrorMessage($"Ошибка поиска кода: {errorMessage}", NotificationType.Error);
        }

        #endregion

        #region Disaggregation Mode Methods

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
            DisableAllButtonsForDisaggregationMode();
            InfoLayerText = "Режим очистки короба: отсканируйте код коробки для очистки короба";
            AggregationSummaryText = "Режим очистки короба активен. \nОтсканируйте код коробки для выполнения очистки короба.";

            ShowInfoMessage("Активирован режим очистки короба");
        }

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

            ShowInfoMessage("Режим очистки короба деактивирован");
        }
        // Методы для сохранения и восстановления нормального состояния
        private void SaveNormalModeState()
        {
            _normalModeInfoLayerText = InfoLayerText;
            _normalModeAggregationSummaryText = AggregationSummaryText;
            _isNormalStateDataSaved = true;
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

        private async Task HandleDisaggregationModeBarcode(string barcode)
        {
            if (!IsDisaggregationMode)
                return;

            try
            {
                var validation = ValidateSessionData();
                if (!validation.IsValid)
                {
                    DisplayDisaggregationError(barcode, validation.ErrorMessage);
                    return;
                }

                var boxRecord = ResponseSscc.RECORDSET
                    .Where(r => r.TYPEID == (int)SsccType.Box)
                    .FirstOrDefault(r => r.CHECK_BAR_CODE == barcode);

                if (boxRecord != null)
                {
                    await ProcessDisaggregationRequest(barcode, boxRecord);
                }
                else
                {
                    DisplayBoxNotFound(barcode);
                }
            }
            catch (Exception ex)
            {
                DisplayDisaggregationError(barcode, ex.Message);
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
                DisplayDisaggregationCancelled(barcode);
            }
        }

        private async Task ExecuteDisaggregation(string barcode, ArmJobSsccRecord boxRecord)
        {
            var success = _databaseDataService.ClearBoxAggregation(boxRecord.CHECK_BAR_CODE);

            if (success)
            {
                DisplayDisaggregationSuccess(barcode, boxRecord);
                //UpdateScannedCodesAfterDisaggregation();
                UpdateDisaggregationAvailability();
            }
            else
            {
                DisplayDisaggregationFailure(barcode, boxRecord);
            }
        }

        private void DisplayDisaggregationSuccess(string barcode, ArmJobSsccRecord boxRecord)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Очистка короба выполнена успешно!");
            sb.AppendLine();
            sb.AppendLine($"Отсканированный код: {barcode}");
            sb.AppendLine($"SSCC ID: {boxRecord.SSCCID}");
            sb.AppendLine($"CHECK_BAR_CODE: {boxRecord.CHECK_BAR_CODE}");
            sb.AppendLine("Статус: Коробка успешно разагрегирована");

            AggregationSummaryText = sb.ToString();
            ShowSuccessMessage($"Коробка с кодом {barcode} успешно разагрегирована");
        }

        private void DisplayDisaggregationFailure(string barcode, ArmJobSsccRecord boxRecord)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Ошибка очистки короба!");
            sb.AppendLine();
            sb.AppendLine($"Отсканированный код: {barcode}");
            sb.AppendLine($"SSCC ID: {boxRecord.SSCCID}");
            sb.AppendLine("Статус: Не удалось выполнить очистку короба");

            AggregationSummaryText = sb.ToString();
            ShowErrorMessage($"Ошибка очистки короба, коробки с кодом {barcode}", NotificationType.Error);
        }

        private void DisplayDisaggregationCancelled(string barcode)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Очистка короба отменена пользователем.");
            sb.AppendLine();
            sb.AppendLine($"Отсканированный код: {barcode}");

            AggregationSummaryText = sb.ToString();
        }

        private void DisplayBoxNotFound(string barcode)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Код коробки не найден!");
            sb.AppendLine();
            sb.AppendLine($"Отсканированный код: {barcode}");
            sb.AppendLine("Статус: Код не найден среди доступных коробок");
            sb.AppendLine();
            sb.AppendLine("Проверьте правильность кода или убедитесь, что коробка существует в текущем задании.");

            AggregationSummaryText = sb.ToString();
            ShowErrorMessage($"Код коробки {barcode} не найден", NotificationType.Warning);
        }

        private void DisplayDisaggregationError(string barcode, string errorMessage)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Ошибка при выполнении очистки короба!");
            sb.AppendLine();
            sb.AppendLine($"Отсканированный код: {barcode}");
            sb.AppendLine($"Ошибка: {errorMessage}");

            AggregationSummaryText = sb.ToString();
            ShowErrorMessage($"Ошибка очистки короба: {errorMessage}", NotificationType.Error);
        }
        // Метод для обновления CurrentBox
        private void UpdateCurrentBox()
        {
            try
            {
                var aggregatedBoxesCount = _databaseDataService.GetAggregatedBoxesCount();
                CurrentBox = aggregatedBoxesCount + 1;
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка обновления CurrentBox: {ex.Message}", NotificationType.Error);
            }
        }
        private void UpdateScannedCodesAfterDisaggregation()
        {
            try
            {
                var aggregatedCodes = _databaseDataService.GetAggregatedUnCodes();

                if (aggregatedCodes?.Any() == true)
                {
                    _sessionService.ClearScannedCodes();

                    foreach (var code in aggregatedCodes.Where(c => !string.IsNullOrWhiteSpace(c)))
                    {
                        _sessionService.AllScannedDmCodes.Add(code);
                    }

                    ShowInfoMessage($"Обновлено {aggregatedCodes.Count} кодов после очистки короба");
                }
                else
                {
                    _sessionService.ClearScannedCodes();
                    ShowInfoMessage("Все коды разагрегированы");
                }

                // Обновляем CurrentBox после разагрегации
                UpdateCurrentBox();

                // Обновляем информационный текст
                InfoLayerText = $"Слой {CurrentLayer} из {_sessionService.SelectedTaskInfo.LAYERS_QTY}. Распознано 0 из {numberOfLayers}";

                // Обновляем сводку агрегации
                var sb = new StringBuilder();
                sb.AppendLine($"Агрегируемая серия: {_sessionService.SelectedTaskInfo.RESOURCEID}");
                sb.AppendLine($"Количество собранных коробов: {CurrentBox - 1}");
                sb.AppendLine($"Номер собираемого короба: {CurrentBox}");
                sb.AppendLine($"Номер слоя: {CurrentLayer}");
                sb.AppendLine($"Количество слоев в коробе: {_sessionService.SelectedTaskInfo.LAYERS_QTY}");
                AggregationSummaryText = sb.ToString();
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Ошибка обновления кодов после очистки короба: {ex.Message}", NotificationType.Error);
            }
        }

        private void UpdateDisaggregationAvailability()
        {
            try
            {
                var countersResponse = _databaseDataService.GetArmCounters();

                if (countersResponse?.RECORDSET != null)
                {
                    var hasAggregatedBoxes = ResponseSscc?.RECORDSET?
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
                ShowErrorMessage($"Ошибка проверки доступности очистки короба: {ex.Message}", NotificationType.Error);
            }
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _sessionService.ClearCurrentBoxCodes();
                _databaseDataService.CloseAggregationSession();
                _plcConnection?.StopPingPong();
                _plcConnection?.Disconnect();
                _plcConnection?.Dispose();
                _dmScanService?.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}

// Extension method для улучшения читаемости
internal static class ObjectExtensions
{
    public static TResult Let<T, TResult>(this T obj, Func<T, TResult> func) => func(obj);
}