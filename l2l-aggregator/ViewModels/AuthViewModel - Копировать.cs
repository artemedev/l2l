//using Avalonia.SimpleRouter;
//using CommunityToolkit.Mvvm.ComponentModel;
//using CommunityToolkit.Mvvm.Input;
//using l2l_aggregator.Models;
//using l2l_aggregator.Services;
//using l2l_aggregator.Services.AggregationService;
//using l2l_aggregator.Services.Database;
//using l2l_aggregator.Services.Notification.Interface;
//using Refit;
//using System;
//using System.Collections.Generic;
//using System.IO.Ports;
//using System.Linq;
//using System.Security.Cryptography;
//using System.Text;
//using System.Threading.Tasks;

//namespace l2l_aggregator.ViewModels
//{
//    public partial class AuthViewModel : ViewModelBase
//    {
//        private ArmJobSsccResponse? _responseSscc;
//        private ArmJobSgtinResponse? _responseSgtin;
//        private readonly DeviceCheckService _deviceCheckService;
//        [ObservableProperty]
//        private string _login;

//        [ObservableProperty]
//        private string _password;

//        [ObservableProperty] private string _infoMessage;

//        [ObservableProperty] private bool _isDeviceCheckInProgress;

//        private readonly HistoryRouter<ViewModelBase> _router;
//        private readonly DatabaseService _databaseService;
//        private readonly SessionService _sessionService;
//        private readonly INotificationService _notificationService;
//        //private ScannerWorker _scannerWorker;
//        private readonly DatabaseDataService _databaseDataService;
//        private readonly DeviceInfoService _deviceInfoService;
//        public AuthViewModel(DatabaseService databaseService,
//                            HistoryRouter<ViewModelBase> router,
//                            INotificationService notificationService,
//                            DatabaseDataService databaseDataService,
//                            DeviceInfoService deviceInfoService,
//                            DeviceCheckService deviceCheckService,
//                            SessionService sessionService)
//        {
//            _databaseService = databaseService;
//            _router = router;
//            _notificationService = notificationService;
//            _sessionService = sessionService;
//            _databaseDataService = databaseDataService;
//            _deviceInfoService = deviceInfoService;
//            _deviceCheckService = deviceCheckService;
//            // Тестовые/заготовленные значения
//            _login = "TESTINNO1";//TESTADMIN
//            _password = "123456";
//            //InitializeScanner();
//        }

//        private string HashPassword(string password)
//        {
//            using (MD5 md5 = MD5.Create())
//            {
//                // Преобразуем пароль в байты
//                byte[] inputBytes = Encoding.UTF8.GetBytes(password);

//                // Вычисляем MD5 хэш
//                byte[] hashBytes = md5.ComputeHash(inputBytes);

//                // Преобразуем в Base64
//                string base64Hash = Convert.ToBase64String(hashBytes);

//                return base64Hash;
//            }
//        }

//        [RelayCommand]
//        public async Task LoginAsync()
//        {
//            try
//            {
//                // Если проверка устройства еще выполняется, ждем ее завершения
//                if (IsDeviceCheckInProgress)
//                {
//                    _notificationService.ShowMessage("Дождитесь завершения проверки устройства...", NotificationType.Info);
//                    return;
//                }

//                // Перед авторизацией проверяем регистрацию устройства
//                bool deviceCheckPassed = await CheckDeviceRegistrationAsync();
//                if (!deviceCheckPassed)
//                {
//                    _notificationService.ShowMessage("Невозможно выполнить авторизацию без корректной регистрации устройства", NotificationType.Error);
//                    return;
//                }
//                // Попытка входа через удаленную БД
//                try
//                {
//                    UserAuthResponse response = await _databaseDataService.LoginAsync(Login, HashPassword(Password));
//                    if (response != null)
//                    {
//                        if (response.AUTH_OK == "1")
//                        {
//                            _sessionService.User = response;

//                            // Сохраняем в локальную базу
//                            await _databaseService.UserAuth.SaveUserAuthAsync(response);

//                            // Проверяем права администратора
//                            bool isAdmin = await _databaseDataService.CheckAdminRoleAsync(response.USERID);
//                            _sessionService.IsAdmin = isAdmin;
//                            // Успешная авторизация
//                            _notificationService.ShowMessage("Авторизация прошла успешно!");
//                            if (isAdmin)
//                            {
//                                _router.GoTo<SettingsViewModel>();
//                                return;
//                            }
//                            // Загружаем сохранённое состояние (если есть)
//                            await _sessionService.LoadAggregationStateAsync(_databaseService);
//                            long? currentTask = await _databaseDataService.GetCurrentJobIdAsync();
//                            if (currentTask != 0)
//                            {
//                                await GoAggregationAsync(currentTask);
//                                _notificationService.ShowMessage("Обнаружена незавершённая агрегация. Продолжаем...");
//                                _router.GoTo<AggregationViewModel>();
//                                return;
//                            }
//                            else
//                            {
//                                _router.GoTo<TaskListViewModel>();
//                                return;
//                            }
//                        }
//                        else
//                        {
//                            _notificationService.ShowMessage($"Ошибка авторизации: {response.ERROR_TEXT}", NotificationType.Warning);
//                        }
//                    }
//                    else
//                    {
//                        _notificationService.ShowMessage("Ошибка: пустой ответ от базы данных.", NotificationType.Error);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    _notificationService.ShowMessage($"Ошибка входа: {ex.Message}", NotificationType.Error);
//                }
//            }
//            catch (Exception ex)
//            {
//                _notificationService.ShowMessage($"Ошибка: {ex.Message}", NotificationType.Error);
//            }
//        }
//        /// <summary>
//        /// Проверяет необходимость перерегистрации устройства
//        /// </summary>
//        private async Task<bool> CheckDeviceRegistrationAsync()
//        {
//            try
//            {
//                IsDeviceCheckInProgress = true;
//                InfoMessage = "Проверка регистрации устройства...";

//                // Получаем текущую информацию об устройстве
//                var currentRequest = _deviceInfoService.CreateRegistrationRequest();

//                // Проверяем, изменились ли данные устройства с помощью SessionService
//                //bool deviceChanged = await _sessionService.HasDeviceDataChangedAsync(currentRequest);

//                //// Если данные не изменились, проверка не нужна
//                //if (!deviceChanged)
//                //{
//                //    InfoMessage = "";
//                //    return true;
//                //}

//                InfoMessage = "Обнаружены изменения в конфигурации устройства. Выполняется перерегистрация...";

//                // Проверяем подключение к базе данных
//                bool isConnected = await _databaseDataService.TestConnectionAsync();

//                if (!isConnected)
//                {
//                    InfoMessage = "Не удалось подключиться к базе данных для перерегистрации устройства";
//                    _notificationService.ShowMessage(InfoMessage, NotificationType.Error);
//                    return false;
//                }

//                // Выполняем перерегистрацию устройства
//                var deviceRegistered = await _databaseDataService.RegisterDeviceAsync(currentRequest);

//                if (deviceRegistered != null)
//                {
//                    // Сохраняем новые данные устройства
//                    await _sessionService.SaveDeviceInfoAsync(currentRequest, deviceRegistered);

//                    InfoMessage = "Устройство успешно перерегистрировано";
//                    _notificationService.ShowMessage("Перерегистрация устройства завершена успешно", NotificationType.Success);
//                    return true;
//                }
//                else
//                {
//                    InfoMessage = "Ошибка перерегистрации устройства в базе данных";
//                    _notificationService.ShowMessage(InfoMessage, NotificationType.Error);
//                    return false;
//                }
//            }
//            catch (Exception ex)
//            {
//                InfoMessage = $"Ошибка при проверке регистрации устройства: {ex.Message}";
//                _notificationService.ShowMessage(InfoMessage, NotificationType.Error);
//                return false;
//            }
//            finally
//            {
//                IsDeviceCheckInProgress = false;
//            }
//        }

//        public async Task GoAggregationAsync(long? currentTask)
//        {
//            if (currentTask == null && currentTask == 0)
//            {
//                return;
//            }

//            var results = new List<(bool Success, string Message)>
//            {
//                await _deviceCheckService.CheckCameraAsync(_sessionService),
//                await _deviceCheckService.CheckPrinterAsync(_sessionService),
//                await _deviceCheckService.CheckControllerAsync(_sessionService),
//                await _deviceCheckService.CheckScannerAsync(_sessionService)
//            };

//            var errors = results.Where(r => !r.Success).Select(r => r.Message).ToList();
//            if (errors.Any())
//            {
//                foreach (var msg in errors)
//                    _notificationService.ShowMessage(msg);
//            }


//            InfoMessage = "Загружаем детальную информацию о задаче...";

//            // Загружаем детальную информацию о задаче
//            var jobInfo = await _databaseDataService.GetJobDetailsAsync(currentTask ?? 0);
//            if (jobInfo == null)
//            {
//                _notificationService.ShowMessage("Не удалось загрузить детальную информацию о задаче.", NotificationType.Error);
//                return;
//            }




//            // Загружаем данные SSCC
//            await LoadSsccDataAsync(currentTask ?? 0);

//            // Проверяем, что все необходимые данные загружены
//            if (_responseSscc == null)
//            {
//                _notificationService.ShowMessage("SSCC данные не загружены. Невозможно начать агрегацию.", NotificationType.Error);
//                return;
//            }
//            // Загружаем данные SGTIN
//            await LoadSgtinDataAsync(currentTask ?? 0);

//            if (_responseSgtin == null)
//            {
//                _notificationService.ShowMessage("SGTIN данные не загружены. Невозможно начать агрегацию.", NotificationType.Error);
//                return;
//            }
//            // Сохраняем детальную информацию в сессию
//            _sessionService.SelectedTaskInfo = jobInfo;

//            // Сохраняем данные в сессию для использования в AggregationViewModel
//            _sessionService.CachedSsccResponse = _responseSscc;
//            _sessionService.CachedSgtinResponse = _responseSgtin;

//            _router.GoTo<AggregationViewModel>();
//        }
//        private async Task LoadSsccDataAsync(long docId)
//        {
//            try
//            {
//                _responseSscc = await _databaseDataService.GetSsccAsync(docId);
//                if (_responseSscc != null)
//                {
//                    // Сохраняем первую запись SSCC в сессию
//                    _sessionService.SelectedTaskSscc = _responseSscc.RECORDSET.FirstOrDefault();
//                    InfoMessage = "SSCC данные загружены успешно.";
//                }
//                else
//                {
//                    InfoMessage = "Не удалось загрузить SSCC данные.";
//                    _notificationService.ShowMessage(InfoMessage, NotificationType.Warning);
//                }
//            }
//            catch (Exception ex)
//            {
//                InfoMessage = $"Ошибка загрузки SSCC данных: {ex.Message}";
//                _notificationService.ShowMessage(InfoMessage, NotificationType.Error);
//            }
//        }

//        private async Task LoadSgtinDataAsync(long docId)
//        {
//            try
//            {
//                _responseSgtin = await _databaseDataService.GetSgtinAsync(docId);
//                if (_responseSgtin != null)
//                {
//                    InfoMessage = "SGTIN данные загружены успешно.";
//                }
//                else
//                {
//                    InfoMessage = "Не удалось загрузить SGTIN данные.";
//                    _notificationService.ShowMessage(InfoMessage, NotificationType.Warning);
//                }
//            }
//            catch (Exception ex)
//            {
//                InfoMessage = $"Ошибка загрузки SGTIN данных: {ex.Message}";
//                _notificationService.ShowMessage(InfoMessage, NotificationType.Error);
//            }
//        }
//    }
//}
