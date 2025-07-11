using Avalonia.Controls;
using Avalonia.Notification;
using Avalonia.SimpleRouter;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using l2l_aggregator.Models;
using l2l_aggregator.Services;
using l2l_aggregator.Services.AggregationService;
using l2l_aggregator.Services.Database;
using l2l_aggregator.Services.Notification.Interface;
using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
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
        private bool _disableVirtualKeyboard;

        [ObservableProperty]
        private bool _isAdmin;

        private readonly DatabaseService _databaseService;
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



        public MainWindowViewModel(HistoryRouter<ViewModelBase> router, DatabaseService databaseService, INotificationService notificationService, SessionService sessionService, ConfigurationLoaderService configLoader)
        {
            _router = router;
            _databaseService = databaseService;
            _configLoader = configLoader;
            _notificationService = notificationService;
            _sessionService = sessionService;

            router.CurrentViewModelChanged += async viewModel =>
            {
                Content = viewModel;
                //если страница 
                IsNotLoginPage = !(viewModel is AuthViewModel || viewModel is InitializationViewModel);
                User = sessionService.User;
                IsAdmin = sessionService.IsAdmin;
                _disableVirtualKeyboard = _sessionService.DisableVirtualKeyboard;
                //if (IsNotLoginPage)
                //{
                //    await LoadUserData(Content); // Загружаем данные пользователя при входе
                //}
                // Инициализируем или переинициализируем сканер при смене страницы
                await InitializeOrReinitializeScannerAsync();
            };
            
            InitializeAsync();
            
            //_ = InitializeSessionFromDatabaseAsync();
            //-------Notification--------
            Notifications = _notificationService.Notifications;
            ClearNotificationsCommand = new RelayCommand(ClearNotifications);
        }

        private async void InitializeAsync()
        {
            await _sessionService.InitializeAsync(_databaseService);

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

                // Если настройки сканера отсутствуют, ничего не делаем
                if (string.IsNullOrWhiteSpace(savedScannerPort) || string.IsNullOrWhiteSpace(savedScannerModel))
                {
                    DisposeScannerWorker();
                    return;
                }

                // Если сканер уже инициализирован с теми же настройками, не переинициализируем
                if (_scannerInitialized && _globalScannerWorker != null)
                {
                    return;
                }

                // Освобождаем старый сканер если он есть
                DisposeScannerWorker();

                // Проверяем доступность порта
                var availablePorts = SerialPort.GetPortNames();
                if (!availablePorts.Contains(savedScannerPort))
                {
                    _notificationService.ShowMessage($"Сканер на порту '{savedScannerPort}' не найден.", NotificationType.Warning);
                    return;
                }

                // Инициализируем новый сканер
                if (savedScannerModel == "Honeywell")
                {
                    // Дополнительная проверка доступности порта
                    if (!ScannerWorker.IsPortFree(savedScannerPort))
                    {
                        _notificationService.ShowMessage($"Сканер на порту {savedScannerPort} не подключен", NotificationType.Error);
                        return;
                    }

                    _globalScannerWorker = new ScannerWorker(savedScannerPort);
                    _globalScannerWorker.BarcodeScanned += HandleGlobalScannedBarcode;
                    _globalScannerWorker.ErrorOccurred += HandleScannerError;
                    _globalScannerWorker.RunWorkerAsync();

                    _scannerInitialized = true;
                    _notificationService.ShowMessage($"Сканер подключен на порту {savedScannerPort}", NotificationType.Success);
                }
                else
                {
                    _notificationService.ShowMessage($"Модель сканера '{savedScannerModel}' не поддерживается.", NotificationType.Warning);
                }
            }
            catch (Exception ex)
            {
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
                    authVM.Login = parts[0];
                    authVM.Password = parts[1];
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

                var availablePorts = SerialPort.GetPortNames();
                bool portAvailable = availablePorts.Contains(savedScannerPort);

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
        public void ButtonExit()
        {
            Notifications.Clear();
            _sessionService.User = null;
            User = null;
            _router.GoTo<AuthViewModel>();
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
    }
}
