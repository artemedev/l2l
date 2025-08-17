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
        private readonly AggregationLoadService _aggregationLoadService;


        public TaskDetailsViewModel(HistoryRouter<ViewModelBase> router,
            SessionService sessionService,
            INotificationService notificationService,
            AggregationLoadService aggregationLoadService)
        {
            _router = router;
            _sessionService = sessionService;
            _aggregationLoadService = aggregationLoadService;
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
            if (Task.DOCID != 0 && Task.DOCID != null)
            {
                bool loadSuccess = await _aggregationLoadService.LoadAggregation(Task.DOCID);
                if (loadSuccess)
                {
                    _router.GoTo<AggregationViewModel>();
                }
                return;
            }
        }
    }
}
