using Avalonia.SimpleRouter;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using l2l_aggregator.Models;
using l2l_aggregator.Services;
using l2l_aggregator.Services.Configuration;
using l2l_aggregator.Services.Notification.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace l2l_aggregator.ViewModels
{
    public partial class TaskDetailsViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ArmJobRecord _task;

        [ObservableProperty]
        private string _infoMessage;

        // Данные SSCC и SGTIN для передачи в сессию
        private ArmJobSsccResponse? _responseSscc;
        private ArmJobSgtinResponse? _responseSgtin;

        private readonly HistoryRouter<ViewModelBase> _router;
        private readonly SessionService _sessionService;
        private readonly INotificationService _notificationService;
        private readonly DeviceCheckService _deviceCheckService;
        private readonly DatabaseDataService _databaseDataService;


        public TaskDetailsViewModel(HistoryRouter<ViewModelBase> router, 
            DatabaseDataService databaseDataService,
            SessionService sessionService, 
            INotificationService notificationService, 
            DeviceCheckService deviceCheckService)
        {
            _router = router;
            _sessionService = sessionService;
            _notificationService = notificationService;
            _deviceCheckService = deviceCheckService;
            _databaseDataService = databaseDataService;
            Task = _sessionService.SelectedTask;
        }


       

        [RelayCommand]
        public void GoBack()
        {
            // Переход на страницу назад
            _router.GoTo<TaskListViewModel>();
        }


        [RelayCommand]
        public async Task GoAggregationAsync()
        {


            var results = new List<(bool Success, string Message)>
            {
                await _deviceCheckService.CheckCameraAsync(_sessionService),
                await _deviceCheckService.CheckPrinterAsync(_sessionService),
                await _deviceCheckService.CheckControllerAsync(_sessionService),
                await _deviceCheckService.CheckScannerAsync(_sessionService)
            };

            var errors = results.Where(r => !r.Success).Select(r => r.Message).ToList();
            if (errors.Any())
            {
                foreach (var msg in errors)
                    _notificationService.ShowMessage(msg);
                return;
            }


            InfoMessage = "Загружаем детальную информацию о задаче...";

            // Загружаем детальную информацию о задаче
            var jobInfo = _databaseDataService.GetJobDetails(Task.DOCID);
            if (jobInfo == null)
            {
                _notificationService.ShowMessage("Не удалось загрузить детальную информацию о задаче.", NotificationType.Error);
                return;
            }




            // Загружаем данные SSCC
            LoadSsccData(Task.DOCID);

            // Проверяем, что все необходимые данные загружены
            if (_responseSscc == null)
            {
                _notificationService.ShowMessage("SSCC данные не загружены. Невозможно начать агрегацию.", NotificationType.Error);
                return;
            }
            // Загружаем данные SGTIN
            LoadSgtinData(Task.DOCID);

            if (_responseSgtin == null)
            {
                _notificationService.ShowMessage("SGTIN данные не загружены. Невозможно начать агрегацию.", NotificationType.Error);
                return;
            }
            // Сохраняем детальную информацию в сессию
            _sessionService.SelectedTaskInfo = jobInfo;

            // Сохраняем данные в сессию для использования в AggregationViewModel
            _sessionService.CachedSsccResponse = _responseSscc;
            _sessionService.CachedSgtinResponse = _responseSgtin;

            _router.GoTo<AggregationViewModel>();
        }
        private void LoadSsccData(long docId)
        {
            try
            {
                _responseSscc = _databaseDataService.GetSscc(docId);
                if (_responseSscc != null)
                {
                    // Сохраняем первую запись SSCC в сессию
                    _sessionService.SelectedTaskSscc = _responseSscc.RECORDSET.FirstOrDefault();
                    InfoMessage = "SSCC данные загружены успешно.";
                }
                else
                {
                    InfoMessage = "Не удалось загрузить SSCC данные.";
                    _notificationService.ShowMessage(InfoMessage, NotificationType.Warning);
                }
            }
            catch (Exception ex)
            {
                InfoMessage = $"Ошибка загрузки SSCC данных: {ex.Message}";
                _notificationService.ShowMessage(InfoMessage, NotificationType.Error);
            }
        }

        private void LoadSgtinData(long docId)
        {
            try
            {
                _responseSgtin = _databaseDataService.GetSgtin(docId);
                if (_responseSgtin != null)
                {
                    InfoMessage = "SGTIN данные загружены успешно.";
                }
                else
                {
                    InfoMessage = "Не удалось загрузить SGTIN данные.";
                    _notificationService.ShowMessage(InfoMessage, NotificationType.Warning);
                }
            }
            catch (Exception ex)
            {
                InfoMessage = $"Ошибка загрузки SGTIN данных: {ex.Message}";
                _notificationService.ShowMessage(InfoMessage, NotificationType.Error);
            }
        }
    }
}
