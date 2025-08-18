using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.SimpleRouter;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using l2l_aggregator.Services;
using l2l_aggregator.Services.AggregationService;
using l2l_aggregator.Services.ControllerService;
using l2l_aggregator.Services.DmProcessing;
using l2l_aggregator.Services.Notification.Interface;
using l2l_aggregator.Services.Printing;
using l2l_aggregator.ViewModels.VisualElements;
using l2l_aggregator.Views.Popup;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace l2l_aggregator.ViewModels
{
    public partial class AggregationViewModel : ViewModelBase
    {
        #region Services

        private readonly AggregationStateService _stateService;
        private readonly ScanningService _scanningService;
        private readonly TextGenerationService _textGenerationService;
        private readonly BarcodeHandlingService _barcodeHandlingService;
        private readonly CellProcessingService _cellProcessingService;
        private readonly AggregationValidationService _validationService;
        private readonly ImageProcessorService _imageProcessingService;
        private readonly TemplateService _templateService;
        private readonly INotificationService _notificationService;
        private readonly HistoryRouter<ViewModelBase> _router;
        private readonly PrintingService _printingService;
        private readonly DatabaseDataService _databaseDataService;
        private readonly DmScanService _dmScanService;
        private readonly IDialogService _dialogService;
        private readonly SessionService _sessionService;

        #endregion

        #region Private Fields

        private readonly ILogger<PcPlcConnectionService> _logger;
        private PcPlcConnectionService? _plcConnection;
        private string? _previousAggregationSummaryText;
        private ScanResult? _lastScanResult;

        #endregion

        #region Observable Properties

        // UI Update Commands
        public IRelayCommand<SizeChangedEventArgs> ImageSizeChangedCommand { get; }
        public IRelayCommand<SizeChangedEventArgs> ImageSizeCellChangedCommand { get; }

        // Image and UI properties
        [ObservableProperty] private Avalonia.Size imageSize;
        [ObservableProperty] private Avalonia.Size imageSizeCell;
        [ObservableProperty] private Bitmap? scannedImage;
        [ObservableProperty] private ObservableCollection<DmCellViewModel> dMCells = new();
        [ObservableProperty] private bool isPopupOpen;
        [ObservableProperty] private DmCellViewModel? selectedDmCell;
        [ObservableProperty] private Bitmap? selectedSquareImage;

        // Text properties
        [ObservableProperty] private string infoLayerText = "";
        [ObservableProperty] private string aggregationSummaryText = "";

        // Template properties
        public ObservableCollection<TemplateParserService> TemplateFields { get; } = new();
        [ObservableProperty] private bool recognizePack = true;

        // Controller properties
        [ObservableProperty] private bool isControllerAvailable = true;

        // Observable Properties for mode handling (handled manually)
        private bool isInfoMode = false;
        private bool isDisaggregationMode = false;

        // Debug mode
#if DEBUG
        [ObservableProperty] private bool isDebugMode = true;
#else
        [ObservableProperty] private bool isDebugMode = false;
#endif

        #endregion

        #region Bound Properties from State Service

        //Текущий слой
        [ObservableProperty] private int currentLayer = 1;
        //Текущая коробка
        [ObservableProperty] private int currentBox = 1;

        //Доступ к кнопоке "сканировать (software trigger)"
        [ObservableProperty] private bool canScan = true;

        //Доступ к кнопоке "Сканировать (hardware trigger)"
        [ObservableProperty] private bool canScanHardware = false;

        //Доступ к кнопоке "Настройки шаблона"
        [ObservableProperty] private bool canOpenTemplateSettings = true;

        //Доступ к кнопоке "Печать этикетки коробки"
        [ObservableProperty] private bool canPrintBoxLabel = false;

        //Доступ к кнопоке "Очистить короб"
        [ObservableProperty] private bool canClearBox = false;

        //Доступ к кнопоке "Завершить агрегацию"
        [ObservableProperty] private bool canCompleteAggregation = true;

        //Чекбокс "Автоматическая печать этикетки коробки"
        [ObservableProperty] private bool isAutoPrintEnabled = true;

        // Доступ к кнопоке "Режим информации"
        public bool IsInfoMode
        {
            get => isInfoMode;
            set
            {
                if (SetProperty(ref isInfoMode, value))
                {
                    OnPropertyChanged(nameof(InfoModeButtonText));
                    HandleInfoModeChange(value);
                }
            }
        }

        // ToggleButton "Режим разагрегации"
        public bool IsDisaggregationMode
        {
            get => isDisaggregationMode;
            set
            {
                if (SetProperty(ref isDisaggregationMode, value))
                {
                    OnPropertyChanged(nameof(DisaggregationModeButtonText));
                    HandleDisaggregationModeChange(value);
                }
            }
        }

        // Доступ к кнопоке "Режим разагрегации"
        [ObservableProperty] private bool canDisaggregation = false;

        //переменная для колличества слоёв всего
        private int numberOfLayers;


        public string InfoModeButtonText => IsInfoMode ? "Выйти из режима" : "Режим информации";
        public string DisaggregationModeButtonText => IsDisaggregationMode ? "Выйти из режима" : "Режим очистки короба";

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
            AggregationStateService stateService,
            TextGenerationService textGenerationService,
            AggregationValidationService validationService,
            IDialogService dialogService)
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

            // Initialize services
            _stateService = stateService;
            _textGenerationService = textGenerationService;
            _validationService = validationService;
            _scanningService = new ScanningService(dmScanService, imageProcessingService, templateService, notificationService, sessionService);
            _cellProcessingService = new CellProcessingService(imageProcessingService, _textGenerationService);
            _barcodeHandlingService = new BarcodeHandlingService(sessionService, databaseDataService, notificationService, _textGenerationService, dialogService);

            //// Subscribe to service events
            SubscribeToServiceEvents();

            // Initialize commands
            ImageSizeChangedCommand = new RelayCommand<SizeChangedEventArgs>(OnImageSizeChanged);
            ImageSizeCellChangedCommand = new RelayCommand<SizeChangedEventArgs>(OnImageSizeCellChanged);

            // Initialize asynchronously
            InitializeAsync();
        }

        #endregion

        #region Initialization

        private async void InitializeAsync()
        {

            InitializeTemplate();
            _stateService.UpdateScanAvailability();
            InitializeNumberOfLayers();
            await GetCurrentBox();
            await InitializeControllerPingAsync();

            InitializeInfoAndUI();
        }



        private void InitializeTemplate()
        {
            TemplateFields.Clear();

            var loadedFields = _templateService.LoadTemplate(_sessionService.SelectedTaskInfo.UN_TEMPLATE_FR);
            foreach (var f in loadedFields)
                TemplateFields.Add(f);

        }
        //Правильно
        public void InitializeNumberOfLayers()
        {
            var inBoxQty = _sessionService.SelectedTaskInfo.IN_BOX_QTY ?? 0;
            var layersQty = _sessionService.SelectedTaskInfo.LAYERS_QTY ?? 0;
            numberOfLayers = inBoxQty / layersQty;
        }
        public async Task GetCurrentBox()
        {
            try
            {
                // Логика обновления CurrentBox после агрегации
                var aggregatedBoxesCount = await _databaseDataService.GetAggregatedBoxesCount();
                CurrentBox = aggregatedBoxesCount + 1;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка обновления CurrentBox: {ex.Message}", NotificationType.Error);
            }
        }

        private async Task InitializeControllerPingAsync()
        {
            if (!_sessionService.CheckController || string.IsNullOrWhiteSpace(_sessionService.ControllerIP))
                return;

            try
            {
                _plcConnection = new PcPlcConnectionService(_logger);
                bool connected = await _plcConnection.ConnectAsync(_sessionService.ControllerIP);

                if (connected)
                {
                    _plcConnection.StartPingPong(10000);
                    _plcConnection.ConnectionStatusChanged += OnPlcConnectionStatusChanged;
                    _plcConnection.ErrorsReceived += OnPlcErrorsReceived;

                    IsControllerAvailable = true;
                    _notificationService.ShowMessage("Контроллер подключен и мониторинг активен", NotificationType.Success);
                }
                else
                {
                    IsControllerAvailable = false;
                    _notificationService.ShowMessage("Не удалось подключиться к контроллеру", NotificationType.Error);
                }
            }
            catch (Exception ex)
            {
                IsControllerAvailable = false;
                _notificationService.ShowMessage($"Ошибка инициализации контроллера: {ex.Message}", NotificationType.Error);
            }
        }



        #endregion

        #region Service Event Subscriptions

        private void SubscribeToServiceEvents()
        {
            //_stateService.PropertyChanged += OnStateServicePropertyChanged;
            _barcodeHandlingService.InfoModeTextUpdated += OnInfoModeTextUpdated;
            _barcodeHandlingService.DisaggregationModeTextUpdated += OnDisaggregationModeTextUpdated;
            _barcodeHandlingService.BoxAggregationCompleted += OnBoxAggregationCompleted;
            _barcodeHandlingService.DisaggregationCompleted += OnDisaggregationCompleted;
        }

        private void OnStateServicePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Notify property changes for bound properties
            switch (e.PropertyName)
            {
                case nameof(AggregationStateService.CurrentLayer):
                    OnPropertyChanged(nameof(CurrentLayer));
                    break;
                case nameof(AggregationStateService.CurrentBox):
                    OnPropertyChanged(nameof(CurrentBox));
                    break;
                case nameof(AggregationStateService.CanScan):
                    OnPropertyChanged(nameof(CanScan));
                    break;
                case nameof(AggregationStateService.IsInfoMode):
                    // Обновляем локальное поле без вызова HandleInfoModeChange
                    isInfoMode = _stateService.IsInfoMode;
                    OnPropertyChanged(nameof(IsInfoMode));
                    OnPropertyChanged(nameof(InfoModeButtonText));
                    break;
                case nameof(AggregationStateService.IsDisaggregationMode):
                    // Обновляем локальное поле без вызова HandleDisaggregationModeChange
                    isDisaggregationMode = _stateService.IsDisaggregationMode;
                    OnPropertyChanged(nameof(IsDisaggregationMode));
                    OnPropertyChanged(nameof(DisaggregationModeButtonText));
                    break;
                    // Add other property notifications as needed
            }
        }

        private void OnInfoModeTextUpdated(string text)
        {
            AggregationSummaryText = text;
        }

        private void OnDisaggregationModeTextUpdated(string text)
        {
            AggregationSummaryText = text;
        }

        private async void OnBoxAggregationCompleted()
        {
            CanPrintBoxLabel = false;
            CanScan = true;
            await GetCurrentBox();
            CurrentLayer = 1;
            _stateService.CurrentStepIndex = AggregationStep.PackAggregation;

            var summaryText = _textGenerationService.BuildAggregationSummaryAfterBoxCompletion(CurrentBox, CurrentLayer);
            AggregationSummaryText = summaryText;

            var infoText = _textGenerationService.GetNewBoxText(CurrentBox);
            InfoLayerText = infoText;
        }

        private void OnDisaggregationCompleted()
        {
            UpdateScannedCodesAfterDisaggregation();
            UpdateDisaggregationAvailability();
        }

        #endregion

        #region Public Command Methods

        [RelayCommand]
        public async Task ScanSoftware()
        {

            try
            {
                var templateSent = _scanningService.SendTemplateToRecognizer(TemplateFields.ToList());
                if (!templateSent)
                {
                    _stateService.UpdateScanAvailability();
                    return;
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка инициализации задания: {ex.Message}", NotificationType.Error);
                _stateService.UpdateScanAvailability();
                return;
            }

            if (!await _scanningService.MoveCameraToCurrentLayerAsync(CurrentLayer, _plcConnection))
                return;

            await StartScanningSoftwareAsync();
        }

        [RelayCommand]
        public async Task ScanHardware()
        {

            //if (!await _scanningService.MoveCameraToCurrentLayerAsync(CurrentLayer, _plcConnection))
            //    return;

            //try
            //{
            //    if (_plcConnection != null)
            //    {
            //        await _plcConnection.TriggerPhotoAsync();
            //    }
            //    await StartScanningHardwareAsync();
            //}
            //catch (Exception ex)
            //{
            //    _notificationService.ShowMessage($"Ошибка hardware trigger: {ex.Message}");
            //}
        }

        [RelayCommand]
        public async Task PrintBoxLabel()
        {
            AggregationMetrics metrics = _validationService.CalculateMetrics(DMCells);

            if (_validationService.ShouldPrintFullBox(metrics, numberOfLayers))
            {
                await PrintBoxLabelInternal();
            }
            else if (_validationService.ShouldShowPartialBoxConfirmation(metrics, numberOfLayers))
            {
                await HandlePartialBoxPrinting();
            }
        }

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

        [RelayCommand]
        public void OpenTemplateSettings()
        {
            var window = new TemplateSettingsWindow
            {
                DataContext = this
            };

            if (App.Current.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                window.ShowDialog(desktop.MainWindow);
            }
        }

        #endregion

        #region Public Methods

        public void HandleScannedBarcode(string barcode)
        {
            if (IsInfoMode)
            {
                _ = _barcodeHandlingService.HandleInfoModeBarcodeAsync(barcode);
                return;
            }

            if (IsDisaggregationMode)
            {
                _ = _barcodeHandlingService.HandleDisaggregationModeBarcodeAsync(barcode);
                return;
            }

            if (_stateService.CurrentStepIndex != AggregationStep.BoxAggregation && _stateService.CurrentStepIndex != AggregationStep.PalletAggregation)
                return;

            _barcodeHandlingService.HandleNormalModeBarcode(barcode, _stateService.CurrentStepIndex);
        }

        public async void OnCellClicked(DmCellViewModel cell)
        {
            if (_lastScanResult == null) return;

            _previousAggregationSummaryText = AggregationSummaryText;

            SelectedSquareImage = await _cellProcessingService.ProcessCellImageAsync(
                cell, _lastScanResult.CroppedImage, _lastScanResult.ScaleXObrat, _lastScanResult.ScaleYObrat);

            _cellProcessingService.UpdateCellPopupData(cell, SelectedSquareImage, ImageSizeCell);
            AggregationSummaryText = _cellProcessingService.DisplayCellInformation(cell);

            IsPopupOpen = true;
        }

        #endregion

        #region Private Methods
       
        private async Task StartScanningSoftwareAsync()
        {
            var scanResult = await _scanningService.PerformSoftwareScanAsync(CurrentLayer, ImageSize);
            if (scanResult == null) return;

            await ProcessScanResult(scanResult);
        }
        
        //private async Task StartScanningHardwareAsync()
        //{
        //    var scanResult = await _scanningService.PerformHardwareScanAsync(ImageSize);
        //    if (scanResult == null) return;

        //    await ProcessScanResult(scanResult);
        //}

        private async Task ProcessScanResult(ScanResult scanResult)
        {
            _lastScanResult = scanResult;

            // Update UI with scanned image
            ScannedImage?.Dispose();
            ScannedImage = _imageProcessingService.ConvertToAvaloniaBitmap(scanResult.CroppedImage);

            // Ждем пока изображение отобразится и получим актуальные размеры
            await WaitForImageSizeUpdate();

            // Build cell view models с актуальными размерами
            if (!await TryBuildCellsAsync(scanResult))
                return;

            await UpdateAggregationInfoAndUI();
        }

        private async Task WaitForImageSizeUpdate()
        {
            // Ждем несколько циклов UI, чтобы размеры изображения обновились
            for (int i = 0; i < 5; i++)
            {
                await Task.Delay(50);

                // Проверяем, обновились ли размеры
                if (ImageSize.Width > 0 && ImageSize.Height > 0 && ScannedImage != null)
                {
                    // Пересчитываем масштабные коэффициенты с актуальными размерами
                    _lastScanResult = _lastScanResult with
                    {
                        ScaleX = ImageSize.Width / ScannedImage.PixelSize.Width,
                        ScaleY = ImageSize.Height / ScannedImage.PixelSize.Height,
                        ScaleXObrat = ScannedImage.PixelSize.Width / ImageSize.Width,
                        ScaleYObrat = ScannedImage.PixelSize.Height / ImageSize.Height
                    };
                    break;
                }
            }
        }

        private async Task<bool> TryBuildCellsAsync(ScanResult scanResult)
        {
            var validation = _validationService.ValidateTaskInfo();
            if (!validation.IsValid)
            {
                _notificationService.ShowMessage(validation.ErrorMessage, NotificationType.Error);
                return false;
            }

            var docId = _sessionService.SelectedTaskInfo.DOCID;
            if (docId == 0)
            {
                _notificationService.ShowMessage("Ошибка: некорректный ID документа.", NotificationType.Error);
                return false;
            }

            var responseSgtin = _sessionService.CachedSgtinResponse;
            if (responseSgtin == null)
            {
                _notificationService.ShowMessage("Ошибка загрузки данных SGTIN.", NotificationType.Error);
                return false;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                DMCells.Clear();

                // Используем актуальные масштабные коэффициенты из _lastScanResult
                var cells = _imageProcessingService.BuildCellViewModels(
                    _lastScanResult.DmrData,
                    _lastScanResult.ScaleX,
                    _lastScanResult.ScaleY,
                    _sessionService,
                    TemplateFields,
                    responseSgtin,
                    this,
                    _lastScanResult.MinX,
                    _lastScanResult.MinY);

                foreach (var cell in cells)
                {
                    DMCells.Add(cell);
                }
            });

            return true;
        }

        private async Task UpdateAggregationInfoAndUI()
        {
            AggregationMetrics metrics = _validationService.CalculateMetrics(DMCells);
            var duplicateInfo = _validationService.BuildDuplicateInfo(metrics);

            InfoLayerText = _textGenerationService.BuildInfoLayerText(CurrentLayer, _sessionService.SelectedTaskInfo?.LAYERS_QTY ?? 0, metrics.ValidCount, numberOfLayers);
            AggregationSummaryText = _textGenerationService.BuildAggregationSummary(metrics, duplicateInfo, CurrentBox, CurrentLayer, numberOfLayers);

            CanScan = true;
            CanOpenTemplateSettings = true;

            var validCodes = DMCells.Where(c => c.IsValid && !string.IsNullOrWhiteSpace(c.Dm_data?.Data))
                                   .Select(c => c.Dm_data.Data).ToList();

            if (_validationService.IsLastLayerCompleted(metrics, CurrentLayer))
            {
                await HandleLastLayerCompletion(validCodes);
            }
            else if (_validationService.IsLayerCompleted(metrics, numberOfLayers, CurrentLayer))
            {
                await HandleLayerCompletion(validCodes);
            }
            else if (_validationService.HasValidCodes(metrics, CurrentLayer))
            {
                HandlePartialLayerCompletion(validCodes);
            }
        }

        private async Task HandleLastLayerCompletion(List<string> validCodes)
        {
            _sessionService.AddLayerCodes(validCodes);

            CanOpenTemplateSettings = false;
            CanPrintBoxLabel = true;
            _stateService.CurrentStepIndex = AggregationStep.BoxAggregation;

            if (IsAutoPrintEnabled && validCodes.Count == numberOfLayers)
            {
                await PrintBoxLabel();
            }

            await _scanningService.ConfirmPhotoToPlcAsync(_plcConnection);
        }

        private async Task HandleLayerCompletion(List<string> validCodes)
        {
            _sessionService.AddLayerCodes(validCodes);
            CurrentLayer++;
            await _scanningService.ConfirmPhotoToPlcAsync(_plcConnection);
        }

        private void HandlePartialLayerCompletion(List<string> validCodes)
        {
            _sessionService.AddLayerCodes(validCodes);
        }

        private async Task PrintBoxLabelInternal()
        {
            _printingService.PrintReport(_sessionService.SelectedTaskInfo.BOX_TEMPLATE, true);
            _notificationService.ShowMessage($"Этикетка короба {_sessionService.SelectedTaskSscc.CHECK_BAR_CODE} отправлена на печать", NotificationType.Success);
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
            _scanningService.StopScanning();
            _databaseDataService.CloseAggregationSession();
            _databaseDataService.CloseJob();

            _sessionService.ClearCachedAggregationData();
            _sessionService.ClearScannedCodes();
            _sessionService.ClearCurrentBoxCodes();

            _notificationService.ShowMessage("Агрегация завершена.", NotificationType.Success);
            _router.GoTo<TaskListViewModel>();
        }

        private async void UpdateScannedCodesAfterDisaggregation()
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

                    _notificationService.ShowMessage($"Обновлено {aggregatedCodes.Count} кодов после очистки короба", NotificationType.Info);
                }
                else
                {
                    _sessionService.ClearScannedCodes();
                    _notificationService.ShowMessage("Все коды разагрегированы", NotificationType.Info);
                }

                await GetCurrentBox();

                InfoLayerText = _textGenerationService.BuildInfoLayerText(CurrentLayer, _sessionService.SelectedTaskInfo?.LAYERS_QTY ?? 0, 0, numberOfLayers);
                AggregationSummaryText = _textGenerationService.BuildInitialAggregationSummary(CurrentBox, CurrentLayer);
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка обновления кодов после очистки короба: {ex.Message}", NotificationType.Error);
            }
        }

        private async void UpdateDisaggregationAvailability()
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
                _notificationService.ShowMessage($"Ошибка проверки доступности очистки короба: {ex.Message}", NotificationType.Error);
            }
        }
        //правильно работает _textGenerationService отвечает правильно за эту часть
        private void InitializeInfoAndUI()
        {
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

        #region Event Handlers

        partial void OnIsControllerAvailableChanged(bool value)
        {
            _stateService.UpdateScanAvailability();
        }

        partial void OnIsPopupOpenChanged(bool value)
        {
            if (!value && !IsDisaggregationMode && _previousAggregationSummaryText != null)
            {
                AggregationSummaryText = _previousAggregationSummaryText;
            }
        }

        private void HandleInfoModeChange(bool value)
        {
            if (value)
            {
                if (IsDisaggregationMode)
                {
                    isDisaggregationMode = false;
                    OnPropertyChanged(nameof(IsDisaggregationMode));
                    OnPropertyChanged(nameof(DisaggregationModeButtonText));
                }
                _stateService.EnterInfoMode();
                InfoLayerText = _textGenerationService.GetInfoModeText();
                AggregationSummaryText = _textGenerationService.GetInfoModeMessage();
                _notificationService.ShowMessage("Активирован режим информации", NotificationType.Info);
            }
            else
            {
                _stateService.ExitInfoMode();
                _notificationService.ShowMessage("Режим информации деактивирован", NotificationType.Info);
            }
        }

        private void HandleDisaggregationModeChange(bool value)
        {
            if (value)
            {
                if (IsInfoMode)
                {
                    isInfoMode = false;
                    OnPropertyChanged(nameof(IsInfoMode));
                    OnPropertyChanged(nameof(InfoModeButtonText));
                }
                _stateService.EnterDisaggregationMode();
                InfoLayerText = _textGenerationService.GetDisaggregationModeText();
                AggregationSummaryText = _textGenerationService.GetDisaggregationModeMessage();
                _notificationService.ShowMessage("Активирован режим очистки короба", NotificationType.Info);
            }
            else
            {
                _stateService.ExitDisaggregationMode();
                _notificationService.ShowMessage("Режим очистки короба деактивирован", NotificationType.Info);
            }
        }

        private void OnPlcConnectionStatusChanged(bool isConnected)
        {
            IsControllerAvailable = isConnected;

            if (!isConnected)
            {
                _notificationService.ShowMessage("Потеряно соединение с контроллером!", NotificationType.Error);
            }
            else
            {
                _notificationService.ShowMessage("Соединение с контроллером восстановлено", NotificationType.Success);
            }
        }

        private void OnPlcErrorsReceived(PlcErrors errors)
        {
            string errorMessage = errors.GetErrorDescription();
            _notificationService.ShowMessage($"Ошибки контроллера: {errorMessage}", NotificationType.Error);
        }

        private void OnImageSizeChanged(SizeChangedEventArgs e)
        {
            ImageSize = e.NewSize;
        }

        private void OnImageSizeCellChanged(SizeChangedEventArgs e)
        {
            ImageSizeCell = e.NewSize;
        }

        #endregion

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Unsubscribe from service events
                //_stateService.PropertyChanged -= OnStateServicePropertyChanged;
                _barcodeHandlingService.InfoModeTextUpdated -= OnInfoModeTextUpdated;
                _barcodeHandlingService.DisaggregationModeTextUpdated -= OnDisaggregationModeTextUpdated;
                _barcodeHandlingService.BoxAggregationCompleted -= OnBoxAggregationCompleted;
                _barcodeHandlingService.DisaggregationCompleted -= OnDisaggregationCompleted;

                // Cleanup session and connections
                _sessionService.ClearCurrentBoxCodes();
                _databaseDataService.CloseAggregationSession();
                _plcConnection?.StopPingPong();
                _plcConnection?.Disconnect();
                _plcConnection?.Dispose();
                _scanningService.StopScanning();

                // Dispose images
                ScannedImage?.Dispose();
                SelectedSquareImage?.Dispose();
                _lastScanResult?.CroppedImage?.Dispose();
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}