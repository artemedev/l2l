using DM_process_lib;
using DM_wraper_NS;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace l2l_aggregator.Services.DmProcessing
{
    public class DmScanService
    {
        private readonly DM_recogn_wraper _recognWrapper;
        private readonly DM_process _dmProcess;
        private static TaskCompletionSource<bool> _dmrDataReady;
        private string[] _camerasList;
        private TaskCompletionSource<bool>? _cameraReady;
        private TaskCompletionSource<bool>? _camerasListReady;

        private TaskCompletionSource<bool>? _startOkSignal;
        private result_data _dmrData;


        public DmScanService()
        {
            _recognWrapper = new DM_recogn_wraper();
            _recognWrapper.Init();
            _recognWrapper.swNewDMResult += OnNewResult;
            _recognWrapper.alarmEvent += OnAlarmEvent;
            _recognWrapper.swStartOk += OnStartOk;

            //--------новое---------
            _recognWrapper.swFindedCamerasList += OnCamerasFound;
            _recognWrapper.swStartError += OnStartError;
            _recognWrapper.swPrintPaternOk += OnPrintPatternOk;
            _recognWrapper.swParamsOk += OnParamsOk;
            _recognWrapper.swShotOk += OnShotOk;
            _recognWrapper.swStopOk += OnStopOk;
            _recognWrapper.logEvent += OnLogEvent;
            //-----------------
            _dmProcess = new DM_process();
            _dmProcess.Init(_recognWrapper);
        }


        ///// <summary>
        ///// Запустить процесс сканирования, начать задание, скидываем шаблон и 
        ///// </summary>
        public void StartScan(string? base64Template = null)
        {
            _dmrDataReady = new TaskCompletionSource<bool>();
            if (base64Template != null)
            {
                _recognWrapper.SendPrintPatternXML(base64Template);
            }
            _recognWrapper.SendStartShotComand();

        }

        /// <summary>
        /// Настроить параметры для сканирования
        /// </summary>
        public bool ConfigureParams(recogn_params parameters)
        {
            return _recognWrapper.SetParams(parameters);
        }

        /// <summary>
        /// Остановить сканирование
        /// </summary>
        public void StopScan()
        {
            _recognWrapper.SendStopShotComand();
        }
        /// <summary>
        /// Дождаться результата сканирования
        /// </summary>
        public async Task<result_data> WaitForResultAsync()
        {
            await _dmrDataReady.Task;
            return _dmrData;
        }
        /// <summary>
        /// Получаем данные о сканировании
        /// </summary>
        private void OnNewResult(int countResult)
        {
            _dmrData = _recognWrapper.GetDMResult();
            _dmrDataReady.TrySetResult(true);
        }

        /// <summary>
        /// Сделать снимок (программный триггер)
        /// </summary>
        public void startShot()
        {
            _dmrDataReady = new TaskCompletionSource<bool>();
            _recognWrapper.SendShotFrameComand();
        }
        public Task WaitForStartOkAsync()
        {
            _startOkSignal = new TaskCompletionSource<bool>();
            return _startOkSignal.Task;
        }
        private void OnStartOk()
        {
            _startOkSignal?.TrySetResult(true);
        }

        public static void OnAlarmEvent(string textEvent, string typeEvent)
        {
            Console.WriteLine($"ALARM EVENT {typeEvent} {textEvent}");
        }

        //----------новое---------------

        /// <summary>
        /// Проверить готовность камеры
        /// </summary>
        public async Task<bool> CheckCameraAsync()
        {
            _cameraReady = new TaskCompletionSource<bool>();
            _recognWrapper.CheckConfigCamera();

            try
            {
                return await _cameraReady.Task;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Получить список доступных камер
        /// </summary>
        public async Task<string[]> GetCamerasAsync(string camInterface = "GigEVision2")
        {

            var rpam = new recogn_params
            {
                CamInterfaces = camInterface
            };

            _recognWrapper.SetParams(rpam);
            _recognWrapper.GetCameras();
            await _camerasListReady.Task;
            return _camerasList;
        }
        private void OnCamerasFound(string[] cameras)
        {
            if (cameras == null || cameras.Length < 1)
            {
                Console.WriteLine("No cameras available!");
                _camerasList = new string[0];
                return;
            }

            foreach (string camera in cameras)
            {
                Console.WriteLine($"Found camera: {camera}");
            }
            _camerasList = cameras;
            _dmrDataReady.TrySetResult(true);
        }
        private void OnStartError()
        {
            Console.WriteLine("Camera start error");
            _startOkSignal?.TrySetException(new Exception("Camera start error"));
            _cameraReady?.TrySetException(new Exception("Camera not ready"));
        }
        private void OnPrintPatternOk()
        {
            Console.WriteLine("Print pattern set successfully");
        }
        private void OnParamsOk()
        {
            Console.WriteLine("Parameters set successfully");
        }
        private void OnShotOk()
        {
            Console.WriteLine("Shot completed successfully");
        }

        private void OnStopOk()
        {
            Console.WriteLine("Stopped successfully");
        }
        private void OnLogEvent(string textEvent, string typeEvent)
        {
            Console.WriteLine($"LOG EVENT {typeEvent}: {textEvent}");
        }
        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            StopScan();

            // Отписка от событий
            _recognWrapper.swNewDMResult -= OnNewResult;
            _recognWrapper.alarmEvent -= OnAlarmEvent;
            _recognWrapper.swStartOk -= OnStartOk;
            _recognWrapper.swStartError -= OnStartError;
            _recognWrapper.swFindedCamerasList -= OnCamerasFound;
            _recognWrapper.swPrintPaternOk -= OnPrintPatternOk;
            _recognWrapper.swParamsOk -= OnParamsOk;
            _recognWrapper.swShotOk -= OnShotOk;
            _recognWrapper.swStopOk -= OnStopOk;
            _recognWrapper.logEvent -= OnLogEvent;
        }
    }
}
