using l2l_aggregator.Services.Notification.Interface;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace l2l_aggregator.Services.AggregationService
{
    public enum AggregationStep
    {
        PackAggregation = 1,
        BoxAggregation = 2,
        PalletAggregation = 3,
        BoxScanning = 4,
        PalletScanning = 5,
        InfoMode = 6,
        DisaggregationMode = 7
    }

    public enum SsccType
    {
        Box = 0,
        Pallet = 1
    }

    public record AggregationMetrics(
        int ValidCount,
        int DuplicatesInCurrentScan,
        int DuplicatesInAllScans,
        int TotalCells
    );

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

    public class AggregationStateService : INotifyPropertyChanged
    {
        private readonly SessionService _sessionService;
        private readonly INotificationService _notificationService;

        public event PropertyChangedEventHandler? PropertyChanged;

        private AggregationStep _currentStepIndex = AggregationStep.PackAggregation;
        private AggregationStep _previousStepIndex = AggregationStep.PackAggregation;
        private int _currentLayer = 1;
        private int _currentBox = 1;
        private int _currentPallet = 1;
        private int _numberOfLayers;
        private bool _isInfoMode = false;
        private bool _isDisaggregationMode = false;
        private bool _canDisaggregation = false;

        // Состояние UI
        private bool _canScan = true;
        private bool _canScanHardware = false;
        private bool _canOpenTemplateSettings = true;
        private bool _canPrintBoxLabel = false;
        private bool _canPrintPalletLabel = false;
        private bool _canClearBox = false;
        private bool _canCompleteAggregation = true;
        private bool _canStopSession = false;
        private bool _isAutoPrintEnabled = true;

        // Сохраненное состояние для режимов
        private bool _isNormalStateDataSaved = false;
        private string _normalModeInfoLayerText = "";
        private string _normalModeAggregationSummaryText = "";

        public AggregationStateService(SessionService sessionService, INotificationService notificationService)
        {
            _sessionService = sessionService;
            _notificationService = notificationService;
        }

        #region Properties

        public AggregationStep CurrentStepIndex
        {
            get => _currentStepIndex;
            set => SetProperty(ref _currentStepIndex, value);
        }

        public AggregationStep PreviousStepIndex
        {
            get => _previousStepIndex;
            set => SetProperty(ref _previousStepIndex, value);
        }

        public int CurrentLayer
        {
            get => _currentLayer;
            set => SetProperty(ref _currentLayer, value);
        }

        public int CurrentBox
        {
            get => _currentBox;
            set => SetProperty(ref _currentBox, value);
        }

        public int CurrentPallet
        {
            get => _currentPallet;
            set => SetProperty(ref _currentPallet, value);
        }

        public int NumberOfLayers
        {
            get => _numberOfLayers;
            set => SetProperty(ref _numberOfLayers, value);
        }

        public bool IsInfoMode
        {
            get => _isInfoMode;
            set => SetProperty(ref _isInfoMode, value);
        }

        public bool IsDisaggregationMode
        {
            get => _isDisaggregationMode;
            set => SetProperty(ref _isDisaggregationMode, value);
        }

        public bool CanDisaggregation
        {
            get => _canDisaggregation;
            set => SetProperty(ref _canDisaggregation, value);
        }

        // UI State Properties
        public bool CanScan
        {
            get => _canScan;
            set => SetProperty(ref _canScan, value);
        }

        public bool CanScanHardware
        {
            get => _canScanHardware;
            set => SetProperty(ref _canScanHardware, value);
        }

        public bool CanOpenTemplateSettings
        {
            get => _canOpenTemplateSettings;
            set => SetProperty(ref _canOpenTemplateSettings, value);
        }

        public bool CanPrintBoxLabel
        {
            get => _canPrintBoxLabel;
            set => SetProperty(ref _canPrintBoxLabel, value);
        }

        public bool CanPrintPalletLabel
        {
            get => _canPrintPalletLabel;
            set => SetProperty(ref _canPrintPalletLabel, value);
        }

        public bool CanClearBox
        {
            get => _canClearBox;
            set => SetProperty(ref _canClearBox, value);
        }

        public bool CanCompleteAggregation
        {
            get => _canCompleteAggregation;
            set => SetProperty(ref _canCompleteAggregation, value);
        }

        public bool CanStopSession
        {
            get => _canStopSession;
            set => SetProperty(ref _canStopSession, value);
        }

        public bool IsAutoPrintEnabled
        {
            get => _isAutoPrintEnabled;
            set => SetProperty(ref _isAutoPrintEnabled, value);
        }

        #endregion

        #region Public Methods

        public void Initialize()
        {
            InitializeNumberOfLayers();
            InitializeCurrentBoxFromCounters();
        }

        public void InitializeNumberOfLayers()
        {
            var inBoxQty = _sessionService.SelectedTaskInfo.IN_BOX_QTY ?? 0;
            var layersQty = _sessionService.SelectedTaskInfo.LAYERS_QTY ?? 0;

            if (layersQty > 0)
            {
                NumberOfLayers = inBoxQty / layersQty;
            }
            else
            {
                NumberOfLayers = 0;
                _notificationService.ShowMessage("Ошибка: некорректное количество слоев (LAYERS_QTY).", NotificationType.Error);
            }
        }

        public void InitializeCurrentBoxFromCounters()
        {
            try
            {
                // Здесь должна быть логика получения количества агрегированных коробов
                // CurrentBox = количество агрегированных коробов + 1
                CurrentBox = 1; // Placeholder
            }
            catch (Exception ex)
            {
                CurrentBox = 1;
                _notificationService.ShowMessage($"Ошибка инициализации CurrentBox: {ex.Message}", NotificationType.Error);
            }
        }

        public void UpdateCurrentBox()
        {
            try
            {
                // Логика обновления CurrentBox после агрегации
                var aggregatedBoxesCount = 0; // Placeholder - должен быть вызов к БД
                CurrentBox = aggregatedBoxesCount + 1;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка обновления CurrentBox: {ex.Message}", NotificationType.Error);
            }
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

        public bool IsLastLayerCompleted(AggregationMetrics metrics)
        {
            return CurrentLayer == _sessionService.SelectedTaskInfo?.LAYERS_QTY &&
                   metrics.ValidCount == metrics.TotalCells &&
                   metrics.TotalCells > 0;
        }

        public bool IsLayerCompleted(AggregationMetrics metrics)
        {
            return CurrentLayer < _sessionService.SelectedTaskInfo?.LAYERS_QTY &&
                   metrics.ValidCount == NumberOfLayers &&
                   metrics.TotalCells > 0;
        }

        public bool HasValidCodes(AggregationMetrics metrics)
        {
            return CurrentLayer < _sessionService.SelectedTaskInfo?.LAYERS_QTY &&
                   metrics.ValidCount == metrics.TotalCells &&
                   metrics.TotalCells > 0;
        }

        public void EnterInfoMode()
        {
            if (!_isNormalStateDataSaved)
            {
                SaveNormalModeState();
            }

            PreviousStepIndex = CurrentStepIndex;
            CurrentStepIndex = AggregationStep.InfoMode;
            IsInfoMode = true;

            DisableAllButtonsForInfoMode();
        }

        public void ExitInfoMode()
        {
            CurrentStepIndex = PreviousStepIndex;
            IsInfoMode = false;

            if (!IsDisaggregationMode)
            {
                RestoreNormalModeState();
            }

            UpdateScanAvailability();
        }

        public void EnterDisaggregationMode()
        {
            if (!_isNormalStateDataSaved)
            {
                SaveNormalModeState();
            }

            PreviousStepIndex = CurrentStepIndex;
            CurrentStepIndex = AggregationStep.DisaggregationMode;
            IsDisaggregationMode = true;

            DisableAllButtonsForDisaggregationMode();
        }

        public void ExitDisaggregationMode()
        {
            CurrentStepIndex = PreviousStepIndex;
            IsDisaggregationMode = false;

            UpdateScanAvailability();
        }

        public void UpdateScanAvailability()
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

        #endregion

        #region Private Methods

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
            CanScan = true;
            CanScanHardware = true;
            CanOpenTemplateSettings = true;
            // Остальные кнопки устанавливаются в зависимости от состояния
        }

        private void SaveNormalModeState()
        {
            _normalModeInfoLayerText = ""; // Должно передаваться извне
            _normalModeAggregationSummaryText = ""; // Должно передаваться извне
            _isNormalStateDataSaved = true;
        }

        private void RestoreNormalModeState()
        {
            if (_isNormalStateDataSaved)
            {
                // Восстановление состояния должно происходить через события
                _isNormalStateDataSaved = false;
            }
        }

        private void SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion
    }
}