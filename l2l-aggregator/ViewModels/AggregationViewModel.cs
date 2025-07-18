using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.SimpleRouter;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DM_wraper_NS;
using FastReport.Export.Hpgl.Commands;
using l2l_aggregator.Helpers.AggregationHelpers;
using l2l_aggregator.Models;
using l2l_aggregator.Models.AggregationModels;
using l2l_aggregator.Services;
using l2l_aggregator.Services.AggregationService;
using l2l_aggregator.Services.ControllerService;
using l2l_aggregator.Services.Database;
using l2l_aggregator.Services.DmProcessing;
using l2l_aggregator.Services.GS1ParserService;
using l2l_aggregator.Services.Notification.Interface;
using l2l_aggregator.Services.Printing;
using l2l_aggregator.ViewModels.VisualElements;
using l2l_aggregator.Views.Popup;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;

namespace l2l_aggregator.ViewModels
{
    public partial class AggregationViewModel : ViewModelBase
    {
        //сервис работы с сессией
        private readonly SessionService _sessionService;

        //сервис работы с api

        //сервис кропа изображения выбранной ячейки пользователем
        private readonly ImageHelper _imageProcessingService;

        //сервис обработки шаблона, после выбора пользователя элементов в ui. Для дальнейшей отправки в библиотеку распознавания
        private readonly TemplateService _templateService;



        //сервис работы с бд
        private readonly DatabaseService _databaseService;

        ////сервис сканера через comport
        //private ScannerWorker _scannerWorker;

        //сервис нотификаций
        private readonly INotificationService _notificationService;

        //сервис роутинга
        private readonly HistoryRouter<ViewModelBase> _router;

        //сервис принтера
        private readonly PrintingService _printingService;

        //для обновления размеров ячейки, UI
        public IRelayCommand<SizeChangedEventArgs> ImageSizeChangedCommand { get; }
        public IRelayCommand<SizeChangedEventArgs> ImageSizeCellChangedCommand { get; }
        [ObservableProperty] private Avalonia.Size imageSize;
        [ObservableProperty] private Avalonia.Size imageSizeCell;
        [ObservableProperty] private double imageWidth;
        [ObservableProperty] private double imageHeight;
        [ObservableProperty] private double imageCellWidth;
        [ObservableProperty] private double imageCellHeight;

        //валидация ячейки будет она красная или зеленая, UI
        [ObservableProperty] private bool isValid;

        //изображение слоя, UI
        [ObservableProperty] private Bitmap scannedImage;

        //Данные ячеек, UI
        [ObservableProperty] private ObservableCollection<DmCellViewModel> dMCells = new();

        //переменная для открытия всплывающего окна с изображением выбранной ячейки
        [ObservableProperty] private bool isPopupOpen;

        //перемення для отображения элементов в выбранной ячейке
        [ObservableProperty] private DmCellViewModel selectedDmCell;
        //изображение выбранной ячейки
        [ObservableProperty] private Bitmap selectedSquareImage;

        //состояние кнопок
        //Кнопока сканировать
        //для отслеживания состояния загрузки камеры и шаблона
        [ObservableProperty] private bool canScan = true;
        //Кнопока настройки шаблона
        [ObservableProperty] private bool canOpenTemplateSettings = true;
        //Доступ к кнопоке печать этикетки коробки
        [ObservableProperty] private bool canPrintBoxLabel = false;
        //Доступ к кнопоке печать этикетки паллеты
        [ObservableProperty] private bool сanPrintPalletLabel = false;
        //Очистить короб
        [ObservableProperty] private bool canClearBox = false;
        //Очистить паллету
        [ObservableProperty] private bool canClearPallet = false;
        //Завершить агрегацию
        [ObservableProperty] private bool canCompleteAggregation = false;
        //Остановить сессию
        [ObservableProperty] private bool canStopSession = false;

        //переменная на каком мы шаге находимся, 1-агрегация пачек, 2 агрегация короба, 3 агрегация паллеты, 4 сканирование короба, 5 сканирование паллеты
        private int CurrentStepIndex = 1;

        //переменная для сообщений нотификации
        private string InfoMessage;

        //элементы шаболона в список всплывающего окна 
        public ObservableCollection<TemplateField> TemplateFields { get; } = new();

        //переменные для высчитывания разницы между кропнутым изображением и изображением из интерфейса
        private double scaleX, scaleY, scaleXObrat, scaleYObrat;

        //переменная для сохранение шаблона при показе информацию из ячейки
        private string? _lastUsedTemplateJson;

        //переменная для колличества слоёв всего
        private int numberOfLayers;

        //переменная для шаблона коробки, для печати
        private byte[] frxBoxBytes;

        //свойство для получения данных SSCC из кэша сессии
        private ArmJobSsccResponse? ResponseSscc => _sessionService.CachedSsccResponse;
        //данные распознавания
        static result_data dmrData;


        //текущий слой
        [ObservableProperty] private int currentLayer = 1;
        //текущая коробка
        [ObservableProperty] private int currentBox = 1;
        //текущая паллета
        [ObservableProperty] private int currentPallet = 1;

        // Добавление опции "распознавание коробки" в настройки распознавания
        [ObservableProperty] private bool recognizePack = true;

        //информационное текстовое поле выше изображения 
        [ObservableProperty] private string infoLayerText = "Выберите элементы шаблона для агрегации и нажмите кнопку сканировать!";

        //информационное текстовое поле справа изображения
        [ObservableProperty] private string aggregationSummaryText = "Результат агрегации пока не рассчитан.";

        //поле для запоминания предыдущего значения информации о агрегации для выхода из информации для клика по ячейке
        private string _previousAggregationSummaryText;

        private int minX;
        private int minY;
        private int maxX;
        private int maxY;


        [ObservableProperty]
        private bool isControllerAvailable = true; // по умолчанию доступен

        private Image<Rgba32> _croppedImageRaw;

        private PcPlcConnectionService _plcConnection;

        private readonly ILogger<PcPlcConnectionService> _logger;

        //состояние кнопок
        //Кнопка "Начать задание"
        [ObservableProperty] private bool canStartTask = true;

        //переменная для отслеживания состояния шаблона
        private bool templateOk = false;

        //Кнопка сканировать (hardware trigger)
        [ObservableProperty] private bool canScanHardware = false;

        private readonly DatabaseDataService _databaseDataService;
        //сервис обработки и работы с библиотекой распознавания, нужно сделать только чтобы была работа с распознавание, перенести обработку!!!!!!!!!!!!!!!!!!!!!!!!!
        private readonly DmScanService _dmScanService;
        public AggregationViewModel(
            ImageHelper imageProcessingService,
            SessionService sessionService,
            TemplateService templateService,
            DmScanService dmScanService,
            ScannerListenerService scannerListener,
            DatabaseService databaseService,
            DatabaseDataService databaseDataService,
            ScannerInputService scannerInputService,
            INotificationService notificationService,
            HistoryRouter<ViewModelBase> router,
            PrintingService printingService,
            ILogger<PcPlcConnectionService> logger
            )
        {
            _sessionService = sessionService;
            _imageProcessingService = imageProcessingService;
            _templateService = templateService;
            _dmScanService = dmScanService;
            _databaseService = databaseService;
            _notificationService = notificationService;
            _router = router;
            _printingService = printingService;
            _logger = logger;
            _databaseDataService = databaseDataService;


            ImageSizeChangedCommand = new RelayCommand<SizeChangedEventArgs>(OnImageSizeChanged);
            ImageSizeCellChangedCommand = new RelayCommand<SizeChangedEventArgs>(OnImageSizeCellChanged);


            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            ////инициализация предыдущего состояния если оно есть
            //InitializeFromSavedState();

            //инициализация шаблона коробки 
            InitializeBoxTemplate();

            //инициализация колличества пачек на слое
            InitializeNumberOfLayers();

            //инициализация CurrentBox из счетчиков
            InitializeCurrentBoxFromCounters();

            //инициализация SSCC
            InitializeSscc();

            //заполнение из шаблона в модальное окно для выбора элементов для сканирования
            InitializeTemplate();

            //Периодическая проверка контроллера раз в 10 секунд в
            InitializeControllerPing();

            InitializeSession();
        }

        private void InitializeBoxTemplate()
        {
            // Проверяем, что BOX_TEMPLATE не null и не пустой
            if (_sessionService.SelectedTaskInfo.BOX_TEMPLATE != null)
            {
                // Если BOX_TEMPLATE это byte[], то используем его напрямую
                frxBoxBytes = _sessionService.SelectedTaskInfo.BOX_TEMPLATE;
            }
            else
            {
                InfoMessage = "Ошибка: шаблон коробки отсутствует.";
                _notificationService.ShowMessage(InfoMessage, NotificationType.Warning);
            }
        }
        private void InitializeNumberOfLayers()
        {
            if (_sessionService.SelectedTaskInfo != null)
            {
                var inBoxQty = _sessionService.SelectedTaskInfo.IN_BOX_QTY ?? 0;
                var layersQty = _sessionService.SelectedTaskInfo.LAYERS_QTY ?? 0;

                if (layersQty > 0)
                {
                    numberOfLayers = inBoxQty / layersQty; //колличество пачек в слое
                }
                else
                {
                    numberOfLayers = 0;
                    InfoMessage = "Ошибка: некорректное количество слоев (LAYERS_QTY).";
                    _notificationService.ShowMessage(InfoMessage);
                }
            }
            else
            {
                InfoMessage = "Ошибка: отсутствует информация о задании.";
                _notificationService.ShowMessage(InfoMessage);
                return;
            }
        }
        private void InitializeSscc()
        {
            // Проверяем, что данные уже загружены в TaskDetailsViewModel
            if (ResponseSscc == null)
            {
                InfoMessage = "Ошибка: SSCC данные не загружены.";
                _notificationService.ShowMessage(InfoMessage);
                return;
            }
            //!!!!!!!!!!!!!!!!!!!!!!!!!
            var boxRecord = ResponseSscc.RECORDSET
                                       .Where(r => r.TYPEID == 0)
                                       .ElementAtOrDefault(CurrentBox - 1);


            if (boxRecord != null)
            {
                _sessionService.SelectedTaskSscc = boxRecord;
                //_printingService.PrintReport(frxBoxBytes, true);
            }
            else
            {
                InfoMessage = $"Не удалось найти запись коробки с индексом {CurrentBox - 1}.";
                _notificationService.ShowMessage(InfoMessage);
            }
            _sessionService.SelectedTaskSscc = ResponseSscc.RECORDSET.FirstOrDefault();
        }

        private void InitializeCurrentBoxFromCounters()
        {
            try
            {
                var countersResponse = _databaseDataService.GetArmCounters();

                if (countersResponse?.RECORDSET != null)
                {
                    // Ищем запись с CODE = "UN_STATE_AGREGATE"
                    var aggregateCounter = countersResponse.RECORDSET
                        .FirstOrDefault(c => c.CODE == "UN_STATE_AGREGATE");

                    if (aggregateCounter?.QTY != null)
                    {
                        // Устанавливаем CurrentBox на основе QTY + 1 (так как это следующий короб)
                        CurrentBox = aggregateCounter.QTY.Value + 1;

                        InfoMessage = $"Продолжение агрегации с короба №{CurrentBox}";
                        _notificationService.ShowMessage(InfoMessage, NotificationType.Info);
                    }
                    else
                    {
                        // Если запись не найдена, начинаем с 1
                        CurrentBox = 1;
                        InfoMessage = "Начало новой агрегации";
                        _notificationService.ShowMessage(InfoMessage, NotificationType.Info);
                    }
                }
                else
                {
                    // Если данные не получены, начинаем с 1
                    CurrentBox = 1;
                    InfoMessage = "Не удалось получить данные счетчиков, начинаем с короба №1";
                    _notificationService.ShowMessage(InfoMessage, NotificationType.Warning);
                }
            }
            catch (Exception ex)
            {
                CurrentBox = 1;
                InfoMessage = $"Ошибка инициализации CurrentBox: {ex.Message}";
                _notificationService.ShowMessage(InfoMessage, NotificationType.Error);
            }
        }

        private void InitializeTemplate()
        {
            TemplateFields.Clear();

            // Проверяем, что UN_TEMPLATE_FR не null
            if (_sessionService.SelectedTaskInfo?.UN_TEMPLATE_FR != null)
            {
                var loadedFields = _templateService.LoadTemplate(_sessionService.SelectedTaskInfo.UN_TEMPLATE_FR);
                foreach (var f in loadedFields)
                    TemplateFields.Add(f);
            }
            else
            {
                InfoMessage = "Ошибка: шаблон распознавания отсутствует.";
                _notificationService.ShowMessage(InfoMessage);
            }

            UpdateScanAvailability();
        }

        private async void InitializeControllerPing()
        {
            if (!_sessionService.CheckController || string.IsNullOrWhiteSpace(_sessionService.ControllerIP))
                return;

            try
            {
                _plcConnection = new PcPlcConnectionService(_logger);
                bool connected = await _plcConnection.ConnectAsync(_sessionService.ControllerIP);

                if (connected)
                {
                    // Запуск непрерывного мониторинга ping-pong
                    _plcConnection.StartPingPong(10000); // Каждые 10 секунд

                    // Подписка на изменения статуса соединения
                    _plcConnection.ConnectionStatusChanged += OnPlcConnectionStatusChanged;
                    _plcConnection.ErrorsReceived += OnPlcErrorsReceived;

                    IsControllerAvailable = true;
                    _notificationService.ShowMessage("Контроллер подключен и мониторинг активен");
                }
                else
                {
                    IsControllerAvailable = false;
                    _notificationService.ShowMessage("Не удалось подключиться к контроллеру", NotificationType.Error);
                }
            }
            catch (Exception ex)
            {
                IsControllerAvailable = false;
                _notificationService.ShowMessage($"Ошибка инициализации контроллера: {ex.Message}", NotificationType.Error);
            }
        }
        private void InitializeSession()
        {
            if (_sessionService.SelectedTaskInfo == null)
            {
                InfoMessage = "Ошибка: отсутствует информация о задании.";
                _notificationService.ShowMessage(InfoMessage);
                return;
            }
        }
        partial void OnIsControllerAvailableChanged(bool value)
        {
            UpdateScanAvailability();
        }
        private void UpdateScanAvailability()
        {
            CanScan = IsControllerAvailable && TemplateFields.Count > 0 && templateOk;
            CanScanHardware = IsControllerAvailable && TemplateFields.Count > 0 && templateOk;
        }

        [RelayCommand]
        public void StartTask()
        {
           
            if (!_databaseDataService.StartAggregationSession())
            {
                _notificationService.ShowMessage("Ошибка начала сессии агрегации, зайдите в задание заново", NotificationType.Error);
                return; // Остановить, если позиционирование не удалось
            }

            // Очищаем коды при начале нового задания
            CanStartTask = false; // Отключаем кнопку во время выполнения

            try
            {
                //отправляет шаблон распознавания в библиотеку
                templateOk = SendTemplateToRecognizer();

                if (templateOk)
                {
                    InfoLayerText = "Шаблон успешно отправлен. Теперь можно начинать сканирование!";
                    _notificationService.ShowMessage("Шаблон распознавания успешно настроен");
                }
                else
                {
                    InfoLayerText = "Ошибка отправки шаблона. Проверьте настройки и попробуйте снова.";
                    _notificationService.ShowMessage("Ошибка настройки шаблона распознавания", NotificationType.Error);
                    CanStartTask = true; // Включаем кнопку обратно при ошибке
                }
            }
            catch (Exception ex)
            {
                InfoMessage = $"Ошибка инициализации задания: {ex.Message}";
                _notificationService.ShowMessage(InfoMessage, NotificationType.Error);
                CanStartTask = true; // Включаем кнопку обратно при ошибке
                templateOk = false;
            }
            finally
            {
                UpdateScanAvailability();
            }
        }

        [RelayCommand]
        public async Task ScanSoftware()
        {
            if (_sessionService.SelectedTaskInfo == null)
            {
                InfoMessage = "Ошибка: отсутствует информация о задании.";
                _notificationService.ShowMessage(InfoMessage);
                return;
            }
            if (!templateOk)
            {
                InfoMessage = "Шаблон не инициализирован. Сначала нажмите 'Начать задание'.";
                _notificationService.ShowMessage(InfoMessage);
                return;
            }


            ;
            if (!await MoveCameraToCurrentLayerAsync())
                return; // Остановить, если позиционирование не удалось

            //выполняет процесс получения данных от распознавания и отображение результата в UI
            await StartScanningSoftwareAsync();

        }
        // Команда для hardware trigger сканирования
        [RelayCommand]
        public async Task ScanHardware()
        {
            if (_sessionService.SelectedTaskInfo == null)
            {
                InfoMessage = "Ошибка: отсутствует информация о задании.";
                _notificationService.ShowMessage(InfoMessage);
                return;
            }

            if (!templateOk)
            {
                InfoMessage = "Задание не инициализировано. Сначала нажмите 'Начать задание'.";
                _notificationService.ShowMessage(InfoMessage);
                return;
            }

            if (!await MoveCameraToCurrentLayerAsync())
                return; // Остановить, если позиционирование не удалось



            //// Отправляем шаблон для hardware trigger режима
            //if (!await SendTemplateToRecognizerAsync())
            //    return;

            // Запуск захвата фото через контроллер
            try
            {
                await _plcConnection.TriggerPhotoAsync();
                //выполняет процесс получения данных от распознавания и отображение результата в UI
                await StartScanningHardwareAsync();
            }
            catch (Exception ex)
            {
                InfoMessage = $"Ошибка hardware trigger: {ex.Message}";
                _notificationService.ShowMessage(InfoMessage);
            }
        }

        private void OnPlcConnectionStatusChanged(bool isConnected)
        {
            IsControllerAvailable = isConnected;

            if (!isConnected)
            {
                _notificationService.ShowMessage("Потеряно соединение с контроллером!", NotificationType.Error);
            }
            else
            {
                _notificationService.ShowMessage("Соединение с контроллером восстановлено");
            }
        }

        private void OnPlcErrorsReceived(PlcErrors errors)
        {
            string errorMessage = errors.GetErrorDescription();
            _notificationService.ShowMessage($"Ошибки контроллера: {errorMessage}", NotificationType.Error);
        }

        //работа с контроллером, выставление высоты
        private async Task<bool> MoveCameraToCurrentLayerAsync()
        {
            // Если отключена проверка контроллера — позиционирование не требуется
            if (!_sessionService.CheckController)
                return true;

            if (!IsControllerAvailable)
            {
                _notificationService.ShowMessage("Контроллер недоступен!");
                return false;
            }

            if (_sessionService.SelectedTaskInfo == null)
            {
                _notificationService.ShowMessage("Информация о задании отсутствует.");
                return false;
            }

            var packHeight = _sessionService.SelectedTaskInfo.PACK_HEIGHT ?? 0;
            var layersQty = _sessionService.SelectedTaskInfo.LAYERS_QTY ?? 0;

            if (packHeight == null || packHeight == 0)
            {
                _notificationService.ShowMessage("Ошибка: не задана высота слоя (PACK_HEIGHT).");
                return false;
            }
            if (layersQty == null || layersQty == 0)
            {
                _notificationService.ShowMessage("Ошибка: не задана колличество слёв (LAYERS_QTY).");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_sessionService.ControllerIP))
            {
                _notificationService.ShowMessage("IP контроллера не задан.");
                return false;
            }

            try
            {
                // Установка рабочих настроек бокса для текущей операции
                var boxSettings = new BoxWorkSettings
                {
                    CamBoxDistance = (ushort)(450 - ((CurrentLayer - 1) * packHeight)), // Настройка для текущего слоя
                    BoxHeight = (ushort)packHeight,
                    LayersQtty = (ushort)layersQty,
                    CamBoxMinDistance = 500
                };

                await _plcConnection.SetBoxWorkSettingsAsync(boxSettings);

                // Запуск шага цикла для текущего слоя
                await _plcConnection.StartCycleStepAsync((ushort)CurrentLayer);

                //// Запуск захвата фото
                //await _plcConnection.TriggerPhotoAsync();
                return true;
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка позиционирования: {ex.Message}");
                return false;
            }
        }

        // Метод для подтверждения обработки фото в ПЛК
        //!!!!УЗНАТЬ ЧТО ОН ДЕЛАЕТ, ЕГО НУЖНО ОТПРАВЛЯТЬ ПОСЛЕ ПОЛКУЧЕНИЯ ИЗОБРАЖЕНИЯ, ИЛИ КОГДА ДАННЫЕ С КАДРА ХОРОШО СЧИТАЛИСЬ!!!!
        private async Task ConfirmPhotoToPlcAsync()
        {
            if (_plcConnection?.IsConnected == true)
            {
                try
                {
                    await _plcConnection.ConfirmPhotoProcessedAsync();
                }
                catch (Exception ex)
                {
                    _notificationService.ShowMessage($"Ошибка подтверждения фото: {ex.Message}");
                }
            }
        }


        //отправляет шаблон распознавания в библиотеку
        public bool SendTemplateToRecognizer()
        {
            // Генерация шаблона из ui
            var currentTemplate = _templateService.GenerateTemplate(TemplateFields.ToList());
            // Сравнение текущего шаблона с последним использованным
            if (_lastUsedTemplateJson != currentTemplate)
            {
                // Определение, есть ли выбранные OCR или DM элементы
                bool hasOcr = TemplateFields.Any(f =>
                    f.IsSelected && (
                        f.Element.Name.LocalName == "TfrxMemoView" ||
                        f.Element.Name.LocalName == "TfrxTemplateMemoView"
                    ));

                bool hasDm = TemplateFields.Any(f =>
                    f.IsSelected && (
                        f.Element.Name.LocalName == "TfrxBarcode2DView" ||
                        f.Element.Name.LocalName == "TfrxTemplateBarcode2DView"
                    ));
                // Настройки параметров камеры для библиотеки распознавания
                var recognParams = new recogn_params
                {
                    countOfDM = numberOfLayers,
                    CamInterfaces = "GigEVision2",
                    cameraName = _sessionService.CameraIP,
                    _Preset = new camera_preset(_sessionService.CameraModel),
                    softwareTrigger = true, //поменять на false
                    hardwareTrigger = true, //поменять на true
                    OCRRecogn = hasOcr,
                    packRecogn = RecognizePack,
                    DMRecogn = hasDm
                };
                _dmScanService.StopScan();
                //отправка настроек камеры в библиотеку распознавания
                _dmScanService.ConfigureParams(recognParams);

                try
                {
                    //отправка шаблона в библиотеку распознавания и даёт старт для распознавания
                    _dmScanService.StartScan(currentTemplate);
                    _lastUsedTemplateJson = currentTemplate;
                    return true;
                }
                catch (Exception ex)
                {
                    InfoMessage = $"Ошбика отправки шаблона {ex.Message}.";
                    _notificationService.ShowMessage(InfoMessage);
                    return false;
                }
            }

            return true; // шаблон не изменился, можно использовать старый
        }

        //выполняет процесс получения данных от распознавания и отображение результата в UI.Software
        public async Task StartScanningSoftwareAsync()
        {
            if (_lastUsedTemplateJson == null)
            {
                _notificationService.ShowMessage("Шаблон не отправлен. Сначала выполните отправку шаблона.");
                return;
            }
            var boxRecord = ResponseSscc.RECORDSET
                           .Where(r => r.TYPEID == 0)
                           .ElementAtOrDefault(CurrentBox - 1);


            if (boxRecord != null)
            {
                _sessionService.SelectedTaskSscc = boxRecord;
                //_printingService.PrintReport(frxBoxBytes, true);
            }
            else
            {
                InfoMessage = $"Не удалось найти запись коробки с индексом {CurrentBox - 1}.";
                _notificationService.ShowMessage(InfoMessage);
            }
            if (!await TryReceiveScanDataSoftwareAsync())
                return;

            if (!await TryCropImageAsync())
                return;

            if (!await TryBuildCellsAsync())
                return;

            UpdateInfoAndUI();
        }

        //выполняет процесс получения данных от распознавания и отображение результата в UI.Hardware
        public async Task StartScanningHardwareAsync()
        {
            if (_lastUsedTemplateJson == null)
            {
                _notificationService.ShowMessage("Шаблон не отправлен. Сначала выполните отправку шаблона.");
                return;
            }

            if (!await TryReceiveScanDataHardwareAsync())
                return;

            if (!await TryCropImageAsync())
                return;

            if (!await TryBuildCellsAsync())
                return;

            UpdateInfoAndUI();
        }

        // Получение данных распознавания. Software
        private async Task<bool> TryReceiveScanDataSoftwareAsync()
        {
            try
            {
                CanOpenTemplateSettings = false;
                //старт распознавания
                //await _dmScanService.WaitForStartOkAsync();
                _dmScanService.startShot();
                dmrData = await _dmScanService.WaitForResultAsync();
                return true;
            }
            catch (Exception ex)
            {
                InfoMessage = $"Ошибка распознавания: {ex.Message}";
                _notificationService.ShowMessage(InfoMessage);
                return false;
            }
        }
        // Получение данных распознавания. Hardware
        private async Task<bool> TryReceiveScanDataHardwareAsync()
        {
            try
            {
                CanOpenTemplateSettings = false;
                //старт распознавания
                dmrData = await _dmScanService.WaitForResultAsync();
                return true;
            }
            catch (Exception ex)
            {
                InfoMessage = $"Ошибка распознавания: {ex.Message}";
                _notificationService.ShowMessage(InfoMessage);
                return false;
            }
        }

        // Кроп изображения
        private async Task<bool> TryCropImageAsync()
        {
            if (dmrData.rawImage == null)
            {
                InfoMessage = "Изображение из распознавания не получено.";
                _notificationService.ShowMessage(InfoMessage);
                return false;
            }

            double boxRadius = Math.Sqrt(dmrData.BOXs[0].height * dmrData.BOXs[0].height +
                                         dmrData.BOXs[0].width * dmrData.BOXs[0].width) / 2;

            minX = Math.Max(0, (int)dmrData.BOXs.Min(d => d.poseX - boxRadius));
            minY = Math.Max(0, (int)dmrData.BOXs.Min(d => d.poseY - boxRadius));
            maxX = Math.Min(dmrData.rawImage.Width, (int)dmrData.BOXs.Max(d => d.poseX + boxRadius));
            maxY = Math.Min(dmrData.rawImage.Height, (int)dmrData.BOXs.Max(d => d.poseY + boxRadius));

            //кроп изображения
            //ScannedImage = await _dmScanService.GetCroppedImage(dmrData, minX, minY, maxX, maxY);
            _croppedImageRaw = _imageProcessingService.GetCroppedImage(dmrData, minX, minY, maxX, maxY);

            // Освобождаем старое изображение перед новым
            ScannedImage?.Dispose();
            ScannedImage = _imageProcessingService.ConvertToAvaloniaBitmap(_croppedImageRaw);

            await Task.Delay(100); //исправить
            scaleX = imageSize.Width / ScannedImage.PixelSize.Width;
            scaleY = imageSize.Height / ScannedImage.PixelSize.Height;

            scaleXObrat = ScannedImage.PixelSize.Width / imageSize.Width;
            scaleYObrat = ScannedImage.PixelSize.Height / imageSize.Height;

            return true;
        }

        // Построение ячеек
        private async Task<bool> TryBuildCellsAsync()
        {
            var docId = _sessionService.SelectedTaskInfo?.DOCID ?? 0;
            if (docId == 0)
            {
                InfoMessage = "Ошибка: некорректный ID документа.";
                _notificationService.ShowMessage(InfoMessage);
                return false;
            }
            // Используем кэшированные данные SGTIN из сессии
            var responseSgtin = _sessionService.CachedSgtinResponse;
            if (responseSgtin == null)
            {
                InfoMessage = "Ошибка загрузки данных SGTIN.";
                _notificationService.ShowMessage(InfoMessage);
                return false;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                DMCells.Clear();
                foreach (var cell in _imageProcessingService.BuildCellViewModels(
                             dmrData,
                             scaleX,
                             scaleY,
                             _sessionService,
                             TemplateFields,
                             responseSgtin,
                             this,
                             minX, minY))
                {
                    DMCells.Add(cell);
                }
            });

            return true;
        }

        private async void UpdateInfoAndUI()
        {
            int validCountDMCells = DMCells.Count(c => c.IsValid);
            int duplicatesInCurrentScan = DMCells.Count(c => c.IsDuplicateInCurrentScan);
            int duplicatesInAllScans = DMCells.Count(c => c.IsDuplicateInAllScans);



            // Обновление информационного текста выше изображения
            InfoLayerText = $"Слой {CurrentLayer} из {_sessionService.SelectedTaskInfo.LAYERS_QTY}. Распознано {validCountDMCells} из {numberOfLayers}";

            // Обновление информационного текста справа изображения с информацией о дубликатах
            string duplicateInfo = "";
            if (duplicatesInCurrentScan > 0 || duplicatesInAllScans > 0)
            {
                duplicateInfo = $"\nДубликаты в текущем скане: {duplicatesInCurrentScan}";
                if (duplicatesInAllScans > 0)
                {
                    duplicateInfo += $"\nДубликаты из предыдущих сканов: {duplicatesInAllScans}";
                }
            }

            AggregationSummaryText = $"""
Агрегируемая серия: {_sessionService.SelectedTaskInfo.RESOURCEID}
Количество собранных коробов: {CurrentBox - 1}
Номер собираемого короба: {CurrentBox}
Номер слоя: {CurrentLayer}
Количество слоев в коробе: {_sessionService.SelectedTaskInfo.LAYERS_QTY}
Количество СИ, распознанное в слое: {validCountDMCells}
Количество СИ, считанное в слое: {DMCells.Count}
Количество СИ, ожидаемое в слое: {numberOfLayers}{duplicateInfo}
""";

            CanScan = true;
            CanOpenTemplateSettings = true;

            if (CurrentLayer == _sessionService.SelectedTaskInfo.LAYERS_QTY &&
                validCountDMCells == numberOfLayers)
            {

                //CanScan = false;
                CanOpenTemplateSettings = false;
                CanPrintBoxLabel = true;
                CurrentStepIndex = 2;
                _printingService.PrintReportTEST(frxBoxBytes, true);

                // Метод подтверждения обработки фотографий в ПЛК
                await ConfirmPhotoToPlcAsync();
            }
        }

        //Печать этикетки коробки
        [RelayCommand]
        public void PrintBoxLabel()
        {
            if (frxBoxBytes == null || frxBoxBytes.Length == 0)
            {
                InfoMessage = "Шаблон коробки не загружен.";
                _notificationService.ShowMessage(InfoMessage);
                return;
            }
            if (ResponseSscc == null)
            {
                InfoMessage = "SSCC данные не загружены.";
                _notificationService.ShowMessage(InfoMessage);
                return;
            }

            var boxRecord = ResponseSscc.RECORDSET
                           .Where(r => r.TYPEID == 0)
                           .ElementAtOrDefault(CurrentBox - 1);


            if (boxRecord != null)
            {
                _sessionService.SelectedTaskSscc = boxRecord;
                _printingService.PrintReport(frxBoxBytes, true);
            }
            else
            {
                InfoMessage = $"Не удалось найти запись коробки с индексом {CurrentBox - 1}.";
                _notificationService.ShowMessage(InfoMessage);
            }
        }

        //Очистить короб
        [RelayCommand]
        public void ClearBox()
        {
            CurrentStepIndex = 4;
        }

        //Очистить паллету
        [RelayCommand]
        public void ClearPallet()
        {
            CurrentStepIndex = 5;
        }
        [RelayCommand]
        public void StopSession()
        {
            _dmScanService.StopScan();

            CanScanHardware = false;
            CanScan = false;
            CanClearBox = false;
            CanOpenTemplateSettings = false;
            CanPrintBoxLabel = false;

            _databaseDataService.CloseJob();

        }
        //Завершить агрегацию
        [RelayCommand]
        public async Task CompleteAggregation()
        {
            _dmScanService.StopScan();
             _databaseDataService.CloseAggregationSession();
            _databaseDataService.CloseJob();

            // Очищаем кэшированные данные
            _sessionService.ClearCachedAggregationData();
            // Очищаем коды при конце задания
            _sessionService.ClearScannedCodes();
            _notificationService.ShowMessage("Агрегация завершена.");
            _router.GoTo<TaskListViewModel>();
        }

        //отменить агрегацию
        [RelayCommand]
        public void CancelAggregation()
        {

            // await _databaseService.AggregationState.ClearStateAsync(_sessionService.User.USER_NAME);
            _dmScanService.StopScan();
            _databaseDataService.CloseAggregationSession();
            _databaseDataService.CloseJob();

            // Очищаем кэшированные данные
            // _sessionService.ClearCachedAggregationData();
            // Очищаем коды при конце задания
            _sessionService.ClearScannedCodes();
            _notificationService.ShowMessage("Агрегация завершена.");
            _router.GoTo<TaskListViewModel>();

        }

        //сканирование кода этикетки
        public void HandleScannedBarcode(string barcode)
        {

            // Проверка, что мы находимся на шаге 2
            if (CurrentStepIndex != 2 && CurrentStepIndex != 3)
                return;

            if (ResponseSscc?.RECORDSET == null || ResponseSscc.RECORDSET.Count == 0)
            {
                InfoMessage = "Данные SSCC отсутствуют.";
                _notificationService.ShowMessage(InfoMessage);
                return;
            }

            if (_sessionService.SelectedTaskSscc == null)
            {
                InfoMessage = "Данные SSCC отсутствуют.";
                _notificationService.ShowMessage(InfoMessage);
                return;
            }
            if (CurrentStepIndex == 2)
            {
                //foreach (ArmJobSsccRecord resp in responseSscc.RECORDSET)
                foreach (ArmJobSsccRecord resp in ResponseSscc.RECORDSET)
                {
                    //resp.TYPEID == 0 это тип коробки
                    if (resp.TYPEID == 0 && resp.DISPLAY_BAR_CODE == barcode)
                    {
                        //добавить сохранение
                        //!!!!!!!!!!!!!!!!!!!!!!!!!
                        var boxRecord = ResponseSscc.RECORDSET
                                                   .Where(r => r.TYPEID == 0)
                                                   .ElementAtOrDefault(CurrentBox - 1);


                        if (boxRecord != null)
                        {
                            _sessionService.SelectedTaskSscc = boxRecord;
                            //_printingService.PrintReport(frxBoxBytes, true);
                        }
                        else
                        {
                            InfoMessage = $"Не удалось найти запись коробки с индексом {CurrentBox - 1}.";
                            _notificationService.ShowMessage(InfoMessage);
                        }
                        //изменение состояния после сканирования
                        //if (CurrentBox == _sessionService.SelectedTaskInfo.IN_PALLET_BOX_QTY)
                        //{
                        //// Добавляем валидные коды в глобальную коллекцию
                        foreach (var cell in DMCells.Where(c => c.IsValid && !string.IsNullOrWhiteSpace(c.Dm_data?.Data)))
                        {
                            _sessionService.AllScannedDmCodes.Add(cell.Dm_data.Data);
                        }

                        SaveAllDmCells();
                        //DMCells
                        //_databaseDataService.LogAggregationCompletedAsync(, _sessionService.SelectedTaskSscc.SSCCID);
                        CanPrintBoxLabel = false;
                        //сanPrintPalletLabel = true;
                        //}
                        CanScan = true;
                        CurrentBox++;
                        CurrentLayer = 1;
                        CurrentStepIndex = 1;
                        // Совпадение найдено
                        InfoMessage = $"Короб с ШК {barcode} успешно найден!";
                        _notificationService.ShowMessage(InfoMessage);
                        return;
                    }
                    else
                    {
                        InfoMessage = $"ШК {barcode} не найден в списке!";
                        _notificationService.ShowMessage(InfoMessage);
                    }
                }
            }
            if (CurrentStepIndex == 3)
            {
                //foreach (ArmJobSsccRecord resp in responseSscc.RECORDSET)
                foreach (ArmJobSsccRecord resp in ResponseSscc.RECORDSET)
                {
                    //resp.TYPEID == 1 это тип паллеты
                    if (resp.TYPEID == 1 && resp.DISPLAY_BAR_CODE == barcode)
                    {

                        //изменение состояния после сканирования
                        CurrentPallet++;
                        CurrentBox = 1;
                        CurrentLayer = 1;

                        CurrentStepIndex = 1;
                        // Совпадение найдено
                        InfoMessage = $"Короб с ШК {barcode} успешно найден!";
                        _notificationService.ShowMessage(InfoMessage);
                    }
                    else
                    {
                        InfoMessage = $"ШК {barcode} не найден в списке!";
                        _notificationService.ShowMessage(InfoMessage);
                    }
                }
            }

        }
        //Сохранение в бд.
        private void SaveAllDmCells()
        {
            try
            {
                foreach (var cell in DMCells.Where(c => c.IsValid && !string.IsNullOrWhiteSpace(c.Dm_data?.Data)))
                {
                    // Парсим DM код для получения SerialNumber
                    var gS1Parser = new GS1Parser();
                    var parsedData = gS1Parser.ParseGTIN(cell.Dm_data.Data);

                    if (!string.IsNullOrWhiteSpace(parsedData.SerialNumber))
                    {
                        _databaseDataService.LogAggregationCompleted(parsedData.SerialNumber, _sessionService.SelectedTaskSscc.CHECK_BAR_CODE);

                    }
                    else
                    {
                        _notificationService.ShowMessage($"Не найден SGTIN для серийного номера: {parsedData.SerialNumber}", NotificationType.Warning);
                    }
                }
                //InfoMessage = $"Коды сохранены";
                // _notificationService.ShowMessage(InfoMessage);
                //_notificationService.ShowMessage(
                //    $"Сохранено {countCode} кодов агрегации",
                //    Notificatio);
            }
            catch (Exception ex)
            {
                _notificationService.ShowMessage($"Ошибка сохранения кодов агрегации: {ex.Message}", NotificationType.Error);
            }
        }


        public async void OnCellClicked(DmCellViewModel cell)
        {
            _previousAggregationSummaryText = AggregationSummaryText; // Сохраняем старый текст

            double boxRadius = Math.Sqrt(dmrData.BOXs[0].height * dmrData.BOXs[0].height +
                         dmrData.BOXs[0].width * dmrData.BOXs[0].width) / 2;
            int minX = (int)dmrData.BOXs.Min(d => d.poseX - boxRadius);
            int minY = (int)dmrData.BOXs.Min(d => d.poseY - boxRadius);
            int maxX = (int)dmrData.BOXs.Max(d => d.poseX + boxRadius);
            int maxY = (int)dmrData.BOXs.Max(d => d.poseY + boxRadius);

            var cropped = _imageProcessingService.CropImage(
                _croppedImageRaw,
                cell.X,
                cell.Y,
                cell.SizeWidth,
                cell.SizeHeight,
                scaleXObrat,
                scaleYObrat,
                (float)cell.Angle
            );
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                SelectedSquareImage = _imageProcessingService.ConvertToAvaloniaBitmap(cropped);
                await Task.Delay(100);
            });

            var scaleXCell = ImageSizeCell.Width / SelectedSquareImage.PixelSize.Width;
            var scaleYCell = ImageSizeCell.Height / SelectedSquareImage.PixelSize.Height;

            var newOcrList = new ObservableCollection<SquareCellViewModel>();

            foreach (var ocr in cell.OcrCells)
            {
                double newX = ocr.X;
                double newY = ocr.Y;

                newOcrList.Add(new SquareCellViewModel
                {
                    X = ocr.X * scaleXCell,
                    Y = ocr.Y * scaleYCell,
                    SizeWidth = ocr.SizeWidth * scaleXCell,
                    SizeHeight = ocr.SizeHeight * scaleYCell,
                    IsValid = ocr.IsValid,
                    Angle = ocr.Angle,
                    OcrName = ocr.OcrName,
                    OcrText = ocr.OcrText
                });
            }
            // Добавим DM элемент (если есть)
            if (cell.Dm_data.Data != null)
            {
                newOcrList.Add(new SquareCellViewModel
                {
                    X = cell.Dm_data.X * scaleXCell,
                    Y = cell.Dm_data.Y * scaleYCell,
                    SizeWidth = cell.Dm_data.SizeWidth * scaleYCell,
                    SizeHeight = cell.Dm_data.SizeHeight * scaleYCell,
                    IsValid = cell.Dm_data.IsValid,
                    Angle = cell.Dm_data.Angle,
                    OcrName = "DM",
                    OcrText = cell.Dm_data.Data ?? "пусто"
                });
            }

            cell.OcrCellsInPopUp.Clear();
            foreach (var newOcr in newOcrList)
                cell.OcrCellsInPopUp.Add(newOcr);

            IsPopupOpen = true;
            var GS1 = false;
            var GTIN = "";
            var DMData = "";
            var SerialNumber = "";
            if (cell.Dm_data?.Data != null)
            {
                var gS1Parser = new GS1Parser();
                GS1_data newGS = gS1Parser.ParseGTIN(cell.Dm_data?.Data);
                GS1 = newGS.GS1isCorrect;
                GTIN = newGS.GTIN;
                DMData = newGS.DMData;
                SerialNumber = newGS.SerialNumber;
            }

            // Проверка дубликатов
            string duplicateStatus = "Нет";
            if (cell.IsDuplicateInCurrentScan)
            {
                duplicateStatus = "Да (в текущем скане)";
            }
            else if (cell.IsDuplicateInAllScans)
            {
                duplicateStatus = "Да (в предыдущих сканах)";
            }

            // Обновление текста
            AggregationSummaryText = $"""
GTIN-код: {(string.IsNullOrWhiteSpace(GTIN) ? "нет данных" : GTIN)}
SerialNumber-код: {(string.IsNullOrWhiteSpace(SerialNumber) ? "нет данных" : SerialNumber)}
Валидность: {(cell.Dm_data?.IsValid == true ? "Да" : "Нет")}
Дубликат: {duplicateStatus}
Координаты: {(cell.Dm_data is { } dm1 ? $"({dm1.X:0.##}, {dm1.Y:0.##})" : "нет данных")}
Размер: {(cell.Dm_data is { } dm ? $"({dm.SizeWidth:0.##} x {dm.SizeHeight:0.##})" : "нет данных")}
Угол: {(cell.Dm_data?.Angle is double a ? $"{a:0.##}°" : "нет данных")}
OCR:
{(cell.OcrCells.Count > 0
        ? string.Join('\n', cell.OcrCells.Select(o =>
            $"- {(string.IsNullOrWhiteSpace(o.OcrName) ? "нет данных" : o.OcrName)}: {(string.IsNullOrWhiteSpace(o.OcrText) ? "нет данных" : o.OcrText)} ({(o.IsValid ? "валид" : "не валид")})"))
        : "- нет данных")}
""";
        }


        partial void OnIsPopupOpenChanged(bool value)
        {
            if (!value)
            {
                AggregationSummaryText = _previousAggregationSummaryText;
            }
        }
        [RelayCommand]
        public void OpenTemplateSettings()
        {
            var window = new TemplateSettingsWindow
            {
                DataContext = this
            };

            if (App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                window.ShowDialog(desktop.MainWindow);
            }
        }
        private void OnImageSizeChanged(SizeChangedEventArgs e)
        {
            imageWidth = e.NewSize.Width;
            imageHeight = e.NewSize.Height;
        }

        private void OnImageSizeCellChanged(SizeChangedEventArgs e)
        {
            imageCellWidth = e.NewSize.Width;
            imageCellHeight = e.NewSize.Height;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Очищаем коды при конце задания
                //_sessionService.ClearScannedCodes();
                _databaseDataService.CloseAggregationSession();
                _plcConnection?.StopPingPong();
                _plcConnection?.Disconnect();
                _plcConnection?.Dispose();
                _dmScanService?.Dispose();
                //_scannerWorker?.Dispose();
            }
            base.Dispose(disposing);
        }

    }

}
