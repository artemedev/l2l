using Avalonia.SimpleRouter;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using l2l_aggregator.Models;
using l2l_aggregator.Services;
using l2l_aggregator.Services.Database;
using l2l_aggregator.Services.Notification.Interface;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

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

        private readonly DatabaseService _databaseService;

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

        public TaskListViewModel(DatabaseService databaseService, 
                                DatabaseDataService databaseDataService, 
                                HistoryRouter<ViewModelBase> router, 
                                SessionService sessionService, 
                                INotificationService notificationService)
        {
            _databaseService = databaseService;
            _router = router;
            _sessionService = sessionService;
            _notificationService = notificationService;
            _databaseDataService = databaseDataService;


            Initialize();
            //string userId = await _databaseService.UserAuth.GetLastUserIdAsync();
            //LoadTasksAsync(userId).ConfigureAwait(false);
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
                //InfoMessage = $"Ошибка инициализации: {ex.Message}";
                _notificationService.ShowMessage($"Ошибка инициализации: {ex.Message}");
            }
        }
        public void LoadTasks(string userId)
        {
            try
            {
                //var serverUri =_sessionService.DatabaseUri;
                //if (string.IsNullOrWhiteSpace(serverUri))
                //{
                //    InfoMessage = "Сервер не настроен!";
                //    _notificationService.ShowMessage("Сервер не настроен!");
                //    return;
                //}
                //HttpClientHandler httpClientHandler = new HttpClientHandler();
                //httpClientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
                //var client = RestService.For<ITaskApi>(new HttpClient(httpClientHandler)
                //{
                //    BaseAddress = new Uri(serverUri)
                //});

                var request = new ArmJobRequest { userid = userId };
                //var response = await _dataApiService.GetJobsAsync(userId);
                var response = _databaseDataService.GetJobs(userId);
                if (response != null)
                {
                    //client.GetJobs(request);

                    Tasks.Clear();
                    foreach (var rec in response.RECORDSET)
                    {
                        Tasks.Add(rec);
                    }
                    // Устанавливаем IsLast для последнего элемента
                    for (int i = 0; i < Tasks.Count; i++)
                    {
                        Tasks[i].IsLast = (i == Tasks.Count - 1);
                    }
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
