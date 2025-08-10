using Avalonia.Controls;
using Avalonia.Notification;
using Avalonia.SimpleRouter;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using l2l_aggregator.Models;
using l2l_aggregator.Services;
using l2l_aggregator.Services.AggregationService;
using l2l_aggregator.Services.Configuration;
using l2l_aggregator.Services.Database;
using l2l_aggregator.Services.Notification.Interface;
using l2l_aggregator.Services.ScannerService;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static l2l_aggregator.Services.Notification.NotificationService;

namespace l2l_aggregator.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ViewModelBase _content = default!;

        // Новое булевое свойство для управления видимостью
        [ObservableProperty]
        private bool _isNotLoginPage;

        // Новое свойство для хранения данных пользователя
        [ObservableProperty]
        private UserAuthResponse? _user;

        [ObservableProperty]
        private bool _enableVirtualKeyboard;

        [ObservableProperty]
        private bool _isAdmin;

        private readonly HistoryRouter<ViewModelBase> _router;
        private readonly INotificationService _notificationService;
        private readonly SessionService _sessionService;
        private readonly ConfigurationLoaderService _configLoader;

        // Глобальный сканер
        private ScannerWorker _globalScannerWorker;
        private bool _scannerInitialized = false;
        // Для автоматического переподключения
        private System.Timers.Timer _scannerMonitorTimer;
        private bool _scannerMonitoringEnabled = false;
        public INotificationMessageManager Manager => _notificationService.Manager;

        //-------Notification--------
        [ObservableProperty]
        private ObservableCollection<NotificationItem> _notifications = new();

        public IRelayCommand ToggleNotificationsFlyoutCommand { get; }

        private Flyout? _notificationsFlyout;

        public IRelayCommand ClearNotificationsCommand { get; }

        private readonly DatabaseDataService _databaseDataService;

        private readonly IDialogService _dialogService;

        private readonly IConfigurationFileService _configurationService;
        public MainWindowViewModel(IConfigurationFileService configurationService, 
                                    HistoryRouter<ViewModelBase> router,
                                    INotificationService notificationService,
                                    SessionService sessionService,
                                    ConfigurationLoaderService configLoader,
                                    DatabaseDataService databaseDataService,
                                    IDialogService dialogService)
        {
            _router = router;
            _configurationService = configurationService;
            _configLoader = configLoader;
            _notificationService = notificationService;
            _sessionService = sessionService;
            _databaseDataService = databaseDataService;
            _dialogService = dialogService;
            _sessionService.PropertyChanged += OnSessionPropertyChanged;

            router.CurrentViewModelChanged += async viewModel =>
            {
                Content = viewModel;
                //если страница 
                IsNotLoginPage = !(viewModel is AuthViewModel);
                User = sessionService.User;
                IsAdmin = sessionService.IsAdmin;
                _enableVirtualKeyboard = _sessionService.EnableVirtualKeyboard;
                // Инициализируем или переинициализируем сканер при смене страницы
                await InitializeOrReinitializeScannerAsync();
            };

            InitializeAsync();
            //-------Notification--------
            Notifications = _notificationService.Notifications;
            ClearNotificationsCommand = new RelayCommand(ClearNotifications);
        }

        // Метод для инициализации DialogService с ConfirmationDialog
        public void InitializeDialogService(Views.Popup.ConfirmationDialog confirmationDialog)
        {
            _dialogService.SetDialogContainer(confirmationDialog);
        }

        private void OnSessionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SessionService.EnableVirtualKeyboard))
            {
                EnableVirtualKeyboard = _sessionService.EnableVirtualKeyboard;
            }
        }
        private async void InitializeAsync()
        {
            await _sessionService.InitializeAsync(_configurationService);

            // Запускаем мониторинг сканера
            StartScannerMonitoring();

            _router.GoTo<AuthViewModel>();

        }
        /// <summary>
        /// Инициализация или переинициализация сканера при необходимости
        /// </summary>
        private async Task InitializeOrReinitializeScannerAsync()
        {
            try
            {
                var savedScannerPort = _sessionService.ScannerPort;
                var savedScannerModel = _sessionService.ScannerModel;

                System.Diagnostics.Debug.WriteLine($"[Debug] Инициализация сканера: порт={savedScannerPort}, модель={savedScannerModel}");

                // Если настройки сканера отсутствуют, ничего не делаем
                if (string.IsNullOrWhiteSpace(savedScannerPort) || string.IsNullOrWhiteSpace(savedScannerModel))
                {
                    System.Diagnostics.Debug.WriteLine("[Debug] Настройки сканера отсутствуют");
                    DisposeScannerWorker();
                    return;
                }

                // Если сканер уже инициализирован с теми же настройками, не переинициализируем
                if (_scannerInitialized && _globalScannerWorker != null)
                {
                    System.Diagnostics.Debug.WriteLine("[Debug] Сканер уже инициализирован");
                    return;
                }

                // Освобождаем старый сканер если он есть
                DisposeScannerWorker();

                // Проверяем доступность порта
                bool portAvailable = false;
                string[] availablePorts = null;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    availablePorts = SerialPort.GetPortNames();
                    portAvailable = availablePorts.Contains(savedScannerPort);
                    System.Diagnostics.Debug.WriteLine($"[Debug] Windows - доступные порты: {string.Join(", ", availablePorts)}");
                }
                else
                {
                    var scannerResolver = new LinuxScannerPortResolver();
                    var honeywellPorts = scannerResolver.GetHoneywellScannerPorts().ToList();
                    portAvailable = honeywellPorts.Contains(savedScannerPort);
                    System.Diagnostics.Debug.WriteLine($"[Debug] Linux - Honeywell порты: {string.Join(", ", honeywellPorts)}");
                }

                System.Diagnostics.Debug.WriteLine($"[Debug] Порт {savedScannerPort} доступен: {portAvailable}");

                if (!portAvailable)
                {
                    _notificationService.ShowMessage($"Сканер на порту '{savedScannerPort}' не найден.", NotificationType.Warning);
                    return;
                }

                // Инициализируем новый сканер
                if (savedScannerModel == "Honeywell")
                {
                    System.Diagnostics.Debug.WriteLine($"[Debug] Проверка свободности порта {savedScannerPort}");

                    // Дополнительная проверка доступности порта
                    if (!ScannerWorker.IsPortFree(savedScannerPort))
                    {
                        _notificationService.ShowMessage($"Сканер на порту {savedScannerPort} не подключен", NotificationType.Error);
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine($"[Debug] Создание ScannerWorker для порта {savedScannerPort}");

                    _globalScannerWorker = new ScannerWorker(savedScannerPort);
                    _globalScannerWorker.BarcodeScanned += HandleGlobalScannedBarcode;
                    _globalScannerWorker.ErrorOccurred += HandleScannerError;
                    _globalScannerWorker.RunWorkerAsync();

                    _scannerInitialized = true;
                    _notificationService.ShowMessage($"Сканер подключен на порту {savedScannerPort}", NotificationType.Success);
                    System.Diagnostics.Debug.WriteLine($"[Debug] Сканер успешно инициализирован");
                }
                else
                {
                    _notificationService.ShowMessage($"Модель сканера '{savedScannerModel}' не поддерживается.", NotificationType.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Debug] Ошибка инициализации сканера: {ex}");
                _notificationService.ShowMessage($"Ошибка инициализации сканера: {ex.Message}", NotificationType.Error);
                _scannerInitialized = false;
            }
        }
        /// <summary>
        /// Глобальный обработчик отсканированного штрихкода
        /// </summary>
        private void HandleGlobalScannedBarcode(string barcode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(barcode)) return;

                // Определяем текущую ViewModel и передаем ей штрихкод
                switch (Content)
                {
                    case AuthViewModel authVM:
                        HandleAuthBarcode(authVM, barcode);
                        break;

                    case AggregationViewModel aggVM:
                        aggVM.HandleScannedBarcode(barcode);
                        break;

                    case SettingsViewModel settingsVM:
                        // Обработка тестового сканирования в настройках
                        settingsVM.HandleTestScanResult(barcode);
                        break;
                    // Можно добавить обработку для других ViewModels если нужно
                    default:
                        _notificationService.ShowMessage($"Отсканирован код: {barcode}", NotificationType.Info);
                        break;
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка обработки штрих-кода: {ex.Message}", NotificationType.Error);
            }
        }

        /// <summary>
        /// Обработка штрихкода для страницы авторизации
        /// </summary>
        private void HandleAuthBarcode(AuthViewModel authVM, string barcode)
        {
            try
            {
                var parts = barcode.Trim().Split(';');
                if (parts.Length == 2)
                {
                    authVM.Login = barcode;

                    //authVM.Login = parts[0];
                    //authVM.Password = parts[1];
                    // Можно автоматически запустить авторизацию
                    // authVM.LoginCommand.Execute(null);
                }
                else
                {
                    _notificationService.ShowMessage("Некорректный формат штрих-кода для авторизации. Ожидается 'Логин;Пароль'.", NotificationType.Warning);
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка обработки штрих-кода авторизации: {ex.Message}", NotificationType.Error);
            }
        }

        /// <summary>
        /// Обработчик ошибок сканера
        /// </summary>
        private void HandleScannerError(string error)
        {
            try
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _notificationService.ShowMessage($"Ошибка сканера: {error}", NotificationType.Error);
                });

                // При ошибке помечаем сканер как неинициализированный для переподключения
                _scannerInitialized = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обработки ошибки сканера: {ex.Message}");
            }
        }
        /// <summary>
        /// Освобождение ресурсов сканера
        /// </summary>
        private void DisposeScannerWorker()
        {
            if (_globalScannerWorker != null)
            {
                try
                {
                    _globalScannerWorker.BarcodeScanned -= HandleGlobalScannedBarcode;
                    _globalScannerWorker.ErrorOccurred -= HandleScannerError;
                    _globalScannerWorker.Dispose();
                }
                catch (Exception ex)
                {
                    _notificationService.ShowMessage($"Ошибка при освобождении сканера: {ex.Message}", NotificationType.Warning);
                }
                finally
                {
                    _globalScannerWorker = null;
                    _scannerInitialized = false;
                }
            }
        }

        /// <summary>
        /// Запуск мониторинга сканера для автоматического переподключения
        /// </summary>
        private void StartScannerMonitoring()
        {
            if (_scannerMonitoringEnabled) return;

            _scannerMonitorTimer = new System.Timers.Timer(10000); // Проверка каждые 10 секунд
            _scannerMonitorTimer.Elapsed += async (sender, e) => await MonitorScannerConnectionAsync();
            _scannerMonitorTimer.AutoReset = true;
            _scannerMonitorTimer.Start();
            _scannerMonitoringEnabled = true;
        }

        /// <summary>
        /// Остановка мониторинга сканера
        /// </summary>
        private void StopScannerMonitoring()
        {
            if (_scannerMonitorTimer != null)
            {
                _scannerMonitorTimer.Stop();
                _scannerMonitorTimer.Dispose();
                _scannerMonitorTimer = null;
            }
            _scannerMonitoringEnabled = false;
        }

        /// <summary>
        /// Мониторинг подключения сканера
        /// </summary>
        private async Task MonitorScannerConnectionAsync()
        {
            try
            {
                var savedScannerPort = _sessionService.ScannerPort;
                var savedScannerModel = _sessionService.ScannerModel;


                // Если настройки сканера отсутствуют, ничего не проверяем
                if (string.IsNullOrWhiteSpace(savedScannerPort) || string.IsNullOrWhiteSpace(savedScannerModel))
                {
                    return;
                }
                bool portAvailable = false;
                string[] availablePorts;

                //var availablePorts = null;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    availablePorts = SerialPort.GetPortNames();
                    portAvailable = availablePorts.Contains(savedScannerPort);
                }
                else
                {
                    var scannerResolver = new LinuxScannerPortResolver();
                    var honeywellPorts = scannerResolver.GetHoneywellScannerPorts().ToList();
                    portAvailable = honeywellPorts.Contains(savedScannerPort);

                }


                //bool portAvailable = availablePorts.Contains(savedScannerPort);

                // Если сканер был инициализирован, но порт пропал
                if (_scannerInitialized && !portAvailable)
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _notificationService.ShowMessage($"Сканер отключен (порт {savedScannerPort} недоступен). Ожидание переподключения...", NotificationType.Warning);
                    });

                    DisposeScannerWorker();
                    return;
                }

                // Если сканер не инициализирован, но порт появился
                if (!_scannerInitialized && portAvailable)
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await InitializeOrReinitializeScannerAsync();
                    });
                    return;
                }

                // Если сканер инициализирован, но воркер завершился (ошибка)
                if (_scannerInitialized && _globalScannerWorker != null &&
                    (_globalScannerWorker.CancellationPending || !_globalScannerWorker.IsBusy))
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        _notificationService.ShowMessage("Обнаружена ошибка сканера. Попытка переподключения...", NotificationType.Warning);
                    });

                    DisposeScannerWorker();

                    // Попытка переподключения через 1 секунду
                    await Task.Delay(1000);
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await InitializeOrReinitializeScannerAsync();
                    });
                }
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не показываем пользователю (чтобы не спамить)
                System.Diagnostics.Debug.WriteLine($"Ошибка мониторинга сканера: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task ButtonExitAsync()
        {
            // Показываем модальное окно подтверждения выхода
            bool confirmed = await _dialogService.ShowExitConfirmationAsync();
            if (confirmed)
            {
                try
                {
                    // Если мы на странице AggregationViewModel, закрываем сессию агрегации
                    if (Content is AggregationViewModel)
                    {
                        _sessionService.ClearCurrentBoxCodes();
                        _databaseDataService.CloseAggregationSession();
                    }
                }
                catch (Exception ex)
                {
                    _notificationService.ShowMessage($"Ошибка при закрытии сессии агрегации: {ex.Message}", NotificationType.Error);
                }
                finally
                {
                    // Очищаем уведомления и данные пользователя
                    Notifications.Clear();
                    _sessionService.User = null;
                    User = null;
                    _router.GoTo<AuthViewModel>();
                }
            }
        }

        [RelayCommand]
        public void ButtonSettings()
        {
            _router.GoTo<SettingsViewModel>();
        }

        public void SetFlyout(Flyout flyout)
        {
            _notificationsFlyout = flyout;
        }
        private void ClearNotifications()
        {
            Notifications.Clear();
        }

        [RelayCommand]
        public async Task ShutdownAsync()
        {
            try
            {


                // Показываем модальное окно подтверждения выключения
                bool confirmed = await _dialogService.ShowCustomConfirmationAsync(
                    "Подтверждение выключения",
                    "Вы действительно хотите выключить компьютер?",
                    Material.Icons.MaterialIconKind.Power,
                    Avalonia.Media.Brushes.Red,
                    Avalonia.Media.Brushes.IndianRed,
                    "Выключить",
                    "Отмена"
                );

                if (confirmed)
                {
                    StopScannerMonitoring();
                    DisposeScannerWorker();

                    // Дополнительная задержка для освобождения ресурсов
                    await Task.Delay(500);
                    // Показываем уведомление о выключении
                    _notificationService.ShowMessage("Выключение системы...", NotificationType.Info);

                    // Небольшая задержка для отображения уведомления
                    await Task.Delay(1000);

                    // Проверяем, находимся ли мы в режиме отладки
                    bool isDebugMode = IsDebugMode();

                    if (isDebugMode)
                    {
                        // В режиме отладки просто закрываем приложение
                        Environment.Exit(0);
                    }
                    else
                    {
                        // В продакшене выключаем компьютер
                        await ShutdownSystemAsync();
                    }
                }

            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка при выключении: {ex.Message}", NotificationType.Error);
            }
        }

        private bool IsDebugMode()
        {
#if DEBUG
            return true;
#else
                return false;
#endif
        }

        private async Task ShutdownSystemAsync()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Выключение Windows
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "shutdown",
                        Arguments = "/s /t 0",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };

                    using (var process = Process.Start(processInfo))
                    {
                        if (process != null)
                        {
                            await process.WaitForExitAsync();
                        }
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Выключение Linux
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "sudo",
                        Arguments = "shutdown -h now",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };

                    using (var process = Process.Start(processInfo))
                    {
                        if (process != null)
                        {
                            await process.WaitForExitAsync();
                        }
                    }
                }
                else
                {
                    // Для других ОС просто закрываем приложение
                    _notificationService.ShowMessage("Выключение не поддерживается на данной ОС. Закрываем приложение...", NotificationType.Warning);
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Не удалось выключить систему: {ex.Message}", NotificationType.Error);
                // В случае ошибки выключения просто закрываем приложение
                Environment.Exit(0);
            }
        }

    }
}
