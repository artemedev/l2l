using Avalonia.SimpleRouter;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using l2l_aggregator.Models;
using l2l_aggregator.Services;
using l2l_aggregator.Services.Database;
using l2l_aggregator.Services.Notification.Interface;
using System;
using System.Collections.ObjectModel;

namespace l2l_aggregator.ViewModels
{
    public partial class TaskListViewModel : ViewModelBase
    {
        private bool _isLast;
        public bool IsLast
        {
            get => _isLast;
            set => SetProperty(ref _isLast, value);
        }


        [ObservableProperty]
        private ObservableCollection<ArmJobRecord> _tasks = new();

        [ObservableProperty]
        private string _infoMessage;

        private ArmJobRecord _selectedTask;
        public ArmJobRecord SelectedTask
        {
            get => _selectedTask;
            set
            {
                SetProperty(ref _selectedTask, value);
                SelectTaskCommand.Execute(value);
            }
        }

        private readonly SessionService _sessionService;
        private readonly INotificationService _notificationService;

        private readonly HistoryRouter<ViewModelBase> _router;
        private readonly DatabaseDataService _databaseDataService;

        public TaskListViewModel(DatabaseDataService databaseDataService, 
                                HistoryRouter<ViewModelBase> router, 
                                SessionService sessionService, 
                                INotificationService notificationService)
        {
            _router = router;
            _sessionService = sessionService;
            _notificationService = notificationService;
            _databaseDataService = databaseDataService;


            Initialize();
        }
        private void Initialize()
        {
            try
            {
                var userId = _sessionService.User.USERID;
                LoadTasks(userId);
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка инициализации: {ex.Message}");
            }
        }
        public void LoadTasks(string userId)
        {
            try
            {
                var request = new ArmJobRequest { userid = userId };
                var response = _databaseDataService.GetJobs(userId);
                if (response != null)
                {
                    Tasks.Clear();
                    foreach (var rec in response.Result.RECORDSET)
                    {
                        Tasks.Add(rec);
                    }
                    // Устанавливаем IsLast для последнего элемента
                    for (int i = 0; i < Tasks.Count; i++)
                    {
                        Tasks[i].IsLast = (i == Tasks.Count - 1);
                    }

                    InfoMessage = $"Загружено {Tasks.Count} заданий";
                    _notificationService.ShowMessage($"Загружено {Tasks.Count} заданий", NotificationType.Success);
                }
                else
                {
                    InfoMessage = "Задания не найдены";
                    _notificationService.ShowMessage("Задания не найдены", NotificationType.Warning);
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка инициализации: {ex.Message}");

                InfoMessage = $"Ошибка: {ex.Message}";
            }
        }

        [RelayCommand]
        public void SelectTask(ArmJobRecord selectedTask)
        {
            if (selectedTask == null) return;

            // Запоминаем выбранную задачу в репозитории
            _sessionService.SelectedTask = selectedTask;

            _router.GoTo<TaskDetailsViewModel>();
        }
    }
}
