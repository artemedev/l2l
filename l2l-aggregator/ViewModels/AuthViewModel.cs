using Avalonia.SimpleRouter;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using l2l_aggregator.Models;
using l2l_aggregator.Services;
using l2l_aggregator.Services.AggregationService;
using l2l_aggregator.Services.Notification.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace l2l_aggregator.ViewModels
{
    public partial class AuthViewModel : ViewModelBase
    {
        private ArmJobSsccResponse? _responseSscc;
        private ArmJobSgtinResponse? _responseSgtin;
        private readonly DeviceCheckService _deviceCheckService;
        [ObservableProperty]
        private string _login;

        [ObservableProperty]
        private string _password;

        [ObservableProperty]
        private bool _isPasswordVisible;

        [ObservableProperty] private string _infoMessage;

        [ObservableProperty] private bool _isDeviceCheckInProgress;

        [ObservableProperty] private bool _enableVirtualKeyboard;

        private readonly HistoryRouter<ViewModelBase> _router;
        private readonly SessionService _sessionService;
        private readonly INotificationService _notificationService;
        private readonly DatabaseDataService _databaseDataService;
        private readonly DeviceInfoService _deviceInfoService;
        private readonly AggregationValidationService _validationService;
        private readonly AggregationLoadService _aggregationLoadService;

        public AuthViewModel(HistoryRouter<ViewModelBase> router,
                            INotificationService notificationService,
                            DatabaseDataService databaseDataService,
                            DeviceInfoService deviceInfoService,
                            DeviceCheckService deviceCheckService,
                            AggregationValidationService validationService,
                            AggregationLoadService aggregationLoadService,
                            SessionService sessionService)
        {
            _router = router;
            _notificationService = notificationService;
            _sessionService = sessionService;
            _databaseDataService = databaseDataService;
            _deviceInfoService = deviceInfoService;
            _deviceCheckService = deviceCheckService;
            _validationService = validationService;
            _aggregationLoadService = aggregationLoadService;

            // Тестовые/заготовленные значения
            EnableVirtualKeyboard = _sessionService.EnableVirtualKeyboard;
#if DEBUG
            // Тестовые/заготовленные значения только в DEBUG режиме
            _login = "TESTINNO1";//TESTADMIN
            _password = "123456";
#else
            _login = "";
            _password = "";
#endif
            //InitializeScanner();
        }
        // Обработчик изменения настройки виртуальной клавиатуры
        partial void OnEnableVirtualKeyboardChanged(bool value)
        {
            _sessionService.EnableVirtualKeyboard = EnableVirtualKeyboard;
            _notificationService.ShowMessage(
                value ? "Виртуальная клавиатура включена" : "Виртуальная клавиатура отключена",
                NotificationType.Info
            );
        }

        private string HashPassword(string password)
        {
            using (MD5 md5 = MD5.Create())
            {
                // Преобразуем пароль в байты
                byte[] inputBytes = Encoding.UTF8.GetBytes(password);

                // Вычисляем MD5 хэш
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // Преобразуем в Base64
                string base64Hash = Convert.ToBase64String(hashBytes);

                return base64Hash;
            }
        }

        [RelayCommand]
        public async Task LoginAsync()
        {
            try
            {
                // Если проверка устройства еще выполняется, ждем ее завершения
                if (IsDeviceCheckInProgress)
                {
                    _notificationService.ShowMessage("Дождитесь завершения проверки устройства...", NotificationType.Info);
                    return;
                }

                // Перед авторизацией проверяем регистрацию устройства
                bool deviceCheckPassed = await CheckDeviceRegistrationAsync();
                if (!deviceCheckPassed)
                {
                    _notificationService.ShowMessage("Невозможно выполнить авторизацию без корректной регистрации устройства", NotificationType.Error);
                    return;
                }
                // Попытка входа через удаленную БД
                try
                {
                    UserAuthResponse response = await _databaseDataService.Login(Login, HashPassword(Password));
                    if (response != null)
                    {
                        if (response.AUTH_OK == "1")
                        {
                            _sessionService.User = response;

                            // Проверяем права администратора
                            bool isAdmin = await _databaseDataService.CheckAdminRole(response.USERID);
                            _sessionService.IsAdmin = isAdmin;
                            // Успешная авторизация
                            _notificationService.ShowMessage("Авторизация прошла успешно!");
                            if (isAdmin)
                            {
                                _router.GoTo<SettingsViewModel>();
                                return;
                            }

                            long? currentTask = await _databaseDataService.GetCurrentJobId();
                            if (currentTask != 0 && currentTask != null)
                            {
                                bool loadSuccess = await _aggregationLoadService.LoadAggregation(currentTask);
                                if (loadSuccess)
                                {
                                    _router.GoTo<AggregationViewModel>();
                                }
                                return;
                            }
                            else
                            {
                                _router.GoTo<TaskListViewModel>();
                                return;
                            }
                        }
                        else
                        {
                            _notificationService.ShowMessage($"Ошибка авторизации: {response.ERROR_TEXT}", NotificationType.Warning);
                        }
                    }
                    else
                    {
                        _notificationService.ShowMessage("Ошибка: пустой ответ от базы данных.", NotificationType.Error);
                    }
                }
                catch (Exception ex)
                {
                    _notificationService.ShowMessage($"Ошибка входа: {ex.Message}", NotificationType.Error);
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка: {ex.Message}", NotificationType.Error);
            }
        }
        /// <summary>
        /// Проверяет необходимость перерегистрации устройства
        /// </summary>
        private async Task<bool> CheckDeviceRegistrationAsync()
        {
            try
            {
                IsDeviceCheckInProgress = true;
                InfoMessage = "Проверка регистрации устройства...";

                // Получаем текущую информацию об устройстве
                var currentRequest = _deviceInfoService.CreateRegistrationRequest();

                InfoMessage = "Обнаружены изменения в конфигурации устройства. Выполняется перерегистрация...";

                // Проверяем подключение к базе данных
                bool isConnected = await _databaseDataService.TestConnection();

                if (!isConnected)
                {
                    InfoMessage = "Не удалось подключиться к базе данных для перерегистрации устройства";
                    _notificationService.ShowMessage(InfoMessage, NotificationType.Error);
                    return false;
                }

                // Выполняем перерегистрацию устройства
                var deviceRegistered = await _databaseDataService.RegisterDevice(currentRequest);

                if (deviceRegistered != null)
                {
                    // Сохраняем новые данные устройства
                    await _sessionService.SaveDeviceInfoAsync(currentRequest, deviceRegistered);

                    InfoMessage = "Устройство успешно перерегистрировано";
                    _notificationService.ShowMessage("Перерегистрация устройства завершена успешно", NotificationType.Success);
                    return true;
                }
                else
                {
                    InfoMessage = "Ошибка перерегистрации устройства в базе данных";
                    _notificationService.ShowMessage(InfoMessage, NotificationType.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                InfoMessage = $"Ошибка при проверке регистрации устройства: {ex.Message}";
                _notificationService.ShowMessage(InfoMessage, NotificationType.Error);
                return false;
            }
            finally
            {
                IsDeviceCheckInProgress = false;
            }
        }

        //// Если нужна команда для переключения (альтернативный подход)
        [RelayCommand]
        private void TogglePasswordVisibility()
        {
            IsPasswordVisible = !IsPasswordVisible;
        }
    }
}
