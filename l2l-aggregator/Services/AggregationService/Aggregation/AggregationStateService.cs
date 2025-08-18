using l2l_aggregator.Models;
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

    // Интерфейс для получения состояния от ViewModel
    public interface IAggregationStateProvider
    {
        int CurrentLayer { get; }
        int CurrentBox { get; }
        int NumberOfLayers { get; }
        bool IsInfoMode { get; }
        bool IsDisaggregationMode { get; }
        bool CanDisaggregation { get; }
        bool CanScan { get; }
        bool CanScanHardware { get; }
        bool CanOpenTemplateSettings { get; }
        bool CanPrintBoxLabel { get; }
        bool CanPrintPalletLabel { get; }
        bool CanClearBox { get; }
        bool CanCompleteAggregation { get; }
        bool CanStopSession { get; }
        bool IsAutoPrintEnabled { get; }
    }

    // Интерфейс для обновления состояния ViewModel
    public interface IAggregationStateUpdater
    {
        void UpdateCanScan(bool value);
        void UpdateCanScanHardware(bool value);
        void UpdateCanOpenTemplateSettings(bool value);
        void UpdateCanPrintBoxLabel(bool value);
        void UpdateCanPrintPalletLabel(bool value);
        void UpdateCanClearBox(bool value);
        void UpdateCanCompleteAggregation(bool value);
        void UpdateCanStopSession(bool value);
        void UpdateCanDisaggregation(bool value);
        void UpdateCurrentLayer(int value);
        void UpdateCurrentBox(int value);
        void UpdateNumberOfLayers(int value);
    }

    public class AggregationStateService
    {
        private readonly SessionService _sessionService;
        private readonly INotificationService _notificationService;
        private readonly IAggregationStateProvider _stateProvider;
        private readonly IAggregationStateUpdater _stateUpdater;

        private AggregationStep _currentStepIndex = AggregationStep.PackAggregation;
        private AggregationStep _previousStepIndex = AggregationStep.PackAggregation;

        // Сохраненное состояние для режимов
        private bool _isNormalStateDataSaved = false;
        private string _normalModeInfoLayerText = "";
        private string _normalModeAggregationSummaryText = "";

        public AggregationStateService(
            SessionService sessionService,
            INotificationService notificationService,
            IAggregationStateProvider stateProvider,
            IAggregationStateUpdater stateUpdater)
        {
            _sessionService = sessionService;
            _notificationService = notificationService;
            _stateProvider = stateProvider;
            _stateUpdater = stateUpdater;
        }

        #region Properties

        public AggregationStep CurrentStepIndex
        {
            get => _currentStepIndex;
            set => _currentStepIndex = value;
        }

        public AggregationStep PreviousStepIndex
        {
            get => _previousStepIndex;
            set => _previousStepIndex = value;
        }

        // Все остальные свойства теперь делегируются к _stateProvider
        public int CurrentLayer => _stateProvider.CurrentLayer;
        public int CurrentBox => _stateProvider.CurrentBox;
        public int NumberOfLayers => _stateProvider.NumberOfLayers;
        public bool IsInfoMode => _stateProvider.IsInfoMode;
        public bool IsDisaggregationMode => _stateProvider.IsDisaggregationMode;
        public bool CanDisaggregation => _stateProvider.CanDisaggregation;
        public bool CanScan => _stateProvider.CanScan;
        public bool CanScanHardware => _stateProvider.CanScanHardware;
        public bool CanOpenTemplateSettings => _stateProvider.CanOpenTemplateSettings;
        public bool CanPrintBoxLabel => _stateProvider.CanPrintBoxLabel;
        public bool CanPrintPalletLabel => _stateProvider.CanPrintPalletLabel;
        public bool CanClearBox => _stateProvider.CanClearBox;
        public bool CanCompleteAggregation => _stateProvider.CanCompleteAggregation;
        public bool CanStopSession => _stateProvider.CanStopSession;
        public bool IsAutoPrintEnabled => _stateProvider.IsAutoPrintEnabled;

        #endregion

        #region Public Methods

        public void Initialize(int numberOfLayers, int currentBox)
        {
            _stateUpdater.UpdateNumberOfLayers(numberOfLayers);
            _stateUpdater.UpdateCurrentBox(currentBox);
        }

        public void UpdateCurrentBox()
        {
            // Логика обновления текущего короба
            // Теперь мы обновляем через _stateUpdater
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

        public void EnterInfoMode()
        {
            if (!_isNormalStateDataSaved)
            {
                SaveNormalModeState();
            }

            _previousStepIndex = _currentStepIndex;
            _currentStepIndex = AggregationStep.InfoMode;

            DisableAllButtonsForInfoMode();
        }

        public void ExitInfoMode()
        {
            _currentStepIndex = _previousStepIndex;

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

            _previousStepIndex = _currentStepIndex;
            _currentStepIndex = AggregationStep.DisaggregationMode;

            DisableAllButtonsForDisaggregationMode();
        }

        public void ExitDisaggregationMode()
        {
            _currentStepIndex = _previousStepIndex;

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
            _stateUpdater.UpdateCanScan(false);
            _stateUpdater.UpdateCanScanHardware(false);
            _stateUpdater.UpdateCanOpenTemplateSettings(false);
            _stateUpdater.UpdateCanPrintBoxLabel(false);
            _stateUpdater.UpdateCanClearBox(false);
            _stateUpdater.UpdateCanCompleteAggregation(false);
        }

        private void DisableAllButtonsForDisaggregationMode()
        {
            _stateUpdater.UpdateCanScan(false);
            _stateUpdater.UpdateCanScanHardware(false);
            _stateUpdater.UpdateCanOpenTemplateSettings(false);
            _stateUpdater.UpdateCanPrintBoxLabel(false);
            _stateUpdater.UpdateCanClearBox(false);
        }

        private void EnableNormalModeButtons()
        {
            _stateUpdater.UpdateCanScan(true);
            _stateUpdater.UpdateCanScanHardware(true);
            _stateUpdater.UpdateCanOpenTemplateSettings(true);
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

        #endregion
    }
}