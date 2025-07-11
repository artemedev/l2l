////using System;
////using System.ComponentModel;
////using System.Diagnostics;
////using System.IO.Ports;

////namespace l2l_aggregator.Services.AggregationService
////{
////    internal class ScannerWorker(string portName) : BackgroundWorker
////    {
////        public event Action<string> BarcodeScanned;

////        private System.IO.Ports.SerialPort scannerPort = new System.IO.Ports.SerialPort(portName, 9600, System.IO.Ports.Parity.None, 8, System.IO.Ports.StopBits.One);
////        protected override void OnDoWork(DoWorkEventArgs e)
////        {
////            scannerPort.DataReceived += ScannerPort_DataReceived;
////            scannerPort.Open();
////        }

////        private void ScannerPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
////        {
////            try
////            {
////                string data = scannerPort.ReadExisting();
////                if (!string.IsNullOrWhiteSpace(data))
////                {
////                    Debug.WriteLine($"[ScannerWorker] Считан ШК: {data}");
////                    BarcodeScanned?.Invoke(data.Trim());
////                }
////            }
////            catch (Exception ex)
////            {
////                Debug.WriteLine($"[ScannerWorker] Ошибка чтения: {ex.Message}");
////            }

////        }

////        protected override void Dispose(bool disposing)
////        {
////            Debug.WriteLine("Прихолпываем воркер");
////            if (scannerPort != null)
////            {
////                if (!scannerPort.IsOpen)
////                    scannerPort.Close();
////                scannerPort.Dispose();
////            }
////            base.Dispose(disposing);
////        }

////    }
////}
//using System;
//using System.ComponentModel;
//using System.Diagnostics;
//using System.IO;
//using System.IO.Ports;
//using System.Threading;
//using System.Threading.Tasks;

//namespace l2l_aggregator.Services.AggregationService
//{
//    internal class ScannerWorker : BackgroundWorker
//    {
//        public event Action<string> BarcodeScanned;
//        public event Action<string> ErrorOccurred;

//        private readonly string _portName;
//        private SerialPort _scannerPort;
//        private readonly object _portLock = new object();
//        private bool _disposed = false;

//        public ScannerWorker(string portName) : base()
//        {
//            _portName = portName;
//            WorkerSupportsCancellation = true;
//        }

//        protected override void OnDoWork(DoWorkEventArgs e)
//        {
//            try
//            {
//                lock (_portLock)
//                {
//                    if (_disposed || CancellationPending)
//                        return;

//                    _scannerPort = new SerialPort(_portName, 9600, Parity.None, 8, StopBits.One)
//                    {
//                        ReadTimeout = 1000,
//                        WriteTimeout = 1000
//                    };

//                    _scannerPort.DataReceived += ScannerPort_DataReceived;
//                    _scannerPort.ErrorReceived += ScannerPort_ErrorReceived;

//                    _scannerPort.Open();
//                    Debug.WriteLine($"[ScannerWorker] Сканер подключен на порту {_portName}");
//                }

//                // Держим воркер активным
//                while (!CancellationPending && !_disposed)
//                {
//                    Thread.Sleep(100);

//                    // Проверяем состояние порта
//                    lock (_portLock)
//                    {
//                        if (_scannerPort != null && !_scannerPort.IsOpen)
//                        {
//                            Debug.WriteLine("[ScannerWorker] Порт закрыт, завершение работы");
//                            break;
//                        }
//                    }
//                }
//            }
//            catch (UnauthorizedAccessException ex)
//            {
//                Debug.WriteLine($"[ScannerWorker] Порт занят: {ex.Message}");
//                ErrorOccurred?.Invoke($"Порт {_portName} занят другим приложением");
//            }
//            catch (ArgumentException ex)
//            {
//                Debug.WriteLine($"[ScannerWorker] Неверное имя порта: {ex.Message}");
//                ErrorOccurred?.Invoke($"Неверное имя порта: {_portName}");
//            }
//            catch (IOException ex)
//            {
//                Debug.WriteLine($"[ScannerWorker] Ошибка ввода-вывода: {ex.Message}");
//                ErrorOccurred?.Invoke($"Ошибка подключения к порту {_portName}");
//            }
//            catch (Exception ex)
//            {
//                Debug.WriteLine($"[ScannerWorker] Неожиданная ошибка: {ex.Message}");
//                ErrorOccurred?.Invoke($"Ошибка сканера: {ex.Message}");
//            }
//            finally
//            {
//                ClosePort();
//            }
//        }

//        private void ScannerPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
//        {
//            try
//            {
//                lock (_portLock)
//                {
//                    if (_scannerPort != null && _scannerPort.IsOpen)
//                    {
//                        string data = _scannerPort.ReadExisting();
//                        if (!string.IsNullOrWhiteSpace(data))
//                        {
//                            Debug.WriteLine($"[ScannerWorker] Считан ШК: {data}");
//                            BarcodeScanned?.Invoke(data.Trim());
//                        }
//                    }
//                }
//            }
//            catch (TimeoutException)
//            {
//                Debug.WriteLine("[ScannerWorker] Таймаут чтения данных");
//            }
//            catch (IOException ex)
//            {
//                Debug.WriteLine($"[ScannerWorker] Ошибка чтения: {ex.Message}");
//                ErrorOccurred?.Invoke("Ошибка чтения данных со сканера");
//            }
//            catch (Exception ex)
//            {
//                Debug.WriteLine($"[ScannerWorker] Ошибка обработки данных: {ex.Message}");
//            }
//        }

//        private void ScannerPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
//        {
//            Debug.WriteLine($"[ScannerWorker] Ошибка порта: {e.EventType}");
//            ErrorOccurred?.Invoke($"Ошибка последовательного порта: {e.EventType}");
//        }

//        private void ClosePort()
//        {
//            lock (_portLock)
//            {
//                if (_scannerPort != null)
//                {
//                    try
//                    {
//                        if (_scannerPort.IsOpen)
//                        {
//                            _scannerPort.DataReceived -= ScannerPort_DataReceived;
//                            _scannerPort.ErrorReceived -= ScannerPort_ErrorReceived;
//                            _scannerPort.Close();
//                            Debug.WriteLine("[ScannerWorker] Порт закрыт");
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        Debug.WriteLine($"[ScannerWorker] Ошибка закрытия порта: {ex.Message}");
//                    }
//                    finally
//                    {
//                        _scannerPort.Dispose();
//                        _scannerPort = null;
//                    }
//                }
//            }
//        }

//        protected override void Dispose(bool disposing)
//        {
//            if (!_disposed)
//            {
//                _disposed = true;

//                if (disposing)
//                {
//                    Debug.WriteLine("[ScannerWorker] Освобождение ресурсов");

//                    // Отменяем работу воркера
//                    if (IsBusy)
//                    {
//                        CancelAsync();

//                        // Ждем завершения с таймаутом
//                        var timeout = DateTime.Now.AddSeconds(2);
//                        while (IsBusy && DateTime.Now < timeout)
//                        {
//                            Thread.Sleep(50);
//                        }
//                    }

//                    ClosePort();
//                }
//            }

//            base.Dispose(disposing);
//        }

//        /// <summary>
//        /// Проверка доступности порта
//        /// </summary>
//        public static bool IsPortAvailable(string portName)
//        {
//            try
//            {
//                var availablePorts = SerialPort.GetPortNames();
//                return Array.Exists(availablePorts, port => port.Equals(portName, StringComparison.OrdinalIgnoreCase));
//            }
//            catch
//            {
//                return false;
//            }
//        }

//        /// <summary>
//        /// Проверка, что порт не занят
//        /// </summary>
//        public static bool IsPortFree(string portName)
//        {
//            try
//            {
//                using var testPort = new SerialPort(portName);
//                testPort.Open();
//                testPort.Close();
//                return true;
//            }
//            catch
//            {
//                return false;
//            }
//        }
//    }
//}


//using System;
//using System.ComponentModel;
//using System.Diagnostics;
//using System.IO;
//using System.IO.Ports;
//using System.Threading;
//using System.Text;
//using l2l_aggregator.Services.ScannerService;
//using System.Linq;

//namespace l2l_aggregator.Services.AggregationService
//{
//    internal class ScannerWorker : BackgroundWorker
//    {
//        public event Action<string> BarcodeScanned;
//        public event Action<string> ErrorOccurred;

//        private readonly string _portName;
//        private SerialPort _scannerPort;
//        private readonly object _portLock = new object();
//        private bool _disposed = false;
//        private StringBuilder _dataBuffer = new StringBuilder();

//        public ScannerWorker(string portName) : base()
//        {
//            _portName = portName;
//            WorkerSupportsCancellation = true;
//        }

//        protected override void OnDoWork(DoWorkEventArgs e)
//        {
//            try
//            {
//                lock (_portLock)
//                {
//                    if (_disposed || CancellationPending)
//                        return;

//                    // Конфигурация для Honeywell сканеров
//                    _scannerPort = new SerialPort(_portName)
//                    {
//                        BaudRate = 9600,  // Попробуйте также 115200 если 9600 не работает
//                        DataBits = 8,
//                        Parity = Parity.None,
//                        StopBits = StopBits.One,
//                        Handshake = Handshake.None,  // Важно для CDC-ACM
//                        ReadTimeout = 500,
//                        WriteTimeout = 500,
//                        RtsEnable = false,  // Отключаем RTS
//                        DtrEnable = false,  // Отключаем DTR
//                        Encoding = Encoding.UTF8
//                    };

//                    _scannerPort.DataReceived += ScannerPort_DataReceived;
//                    _scannerPort.ErrorReceived += ScannerPort_ErrorReceived;

//                    _scannerPort.Open();

//                    // Очищаем буферы после открытия
//                    _scannerPort.DiscardInBuffer();
//                    _scannerPort.DiscardOutBuffer();

//                    Debug.WriteLine($"[ScannerWorker] Сканер подключен на порту {_portName}");
//                    Debug.WriteLine($"[ScannerWorker] Настройки: {_scannerPort.BaudRate} baud, {_scannerPort.DataBits} data bits");
//                }

//                // Держим воркер активным
//                while (!CancellationPending && !_disposed)
//                {
//                    Thread.Sleep(100);

//                    // Проверяем состояние порта
//                    lock (_portLock)
//                    {
//                        if (_scannerPort != null && !_scannerPort.IsOpen)
//                        {
//                            Debug.WriteLine("[ScannerWorker] Порт закрыт, завершение работы");
//                            break;
//                        }
//                    }
//                }
//            }
//            catch (UnauthorizedAccessException ex)
//            {
//                Debug.WriteLine($"[ScannerWorker] Порт занят: {ex.Message}");
//                ErrorOccurred?.Invoke($"Порт {_portName} занят другим приложением. Проверьте права доступа.");
//            }
//            catch (ArgumentException ex)
//            {
//                Debug.WriteLine($"[ScannerWorker] Неверное имя порта: {ex.Message}");
//                ErrorOccurred?.Invoke($"Неверное имя порта: {_portName}");
//            }
//            catch (IOException ex)
//            {
//                Debug.WriteLine($"[ScannerWorker] Ошибка ввода-вывода: {ex.Message}");
//                ErrorOccurred?.Invoke($"Ошибка подключения к порту {_portName}. Возможно устройство отключено.");
//            }
//            catch (Exception ex)
//            {
//                Debug.WriteLine($"[ScannerWorker] Неожиданная ошибка: {ex.Message}");
//                ErrorOccurred?.Invoke($"Ошибка сканера: {ex.Message}");
//            }
//            finally
//            {
//                ClosePort();
//            }
//        }

//        private void ScannerPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
//        {
//            try
//            {
//                lock (_portLock)
//                {
//                    if (_scannerPort != null && _scannerPort.IsOpen)
//                    {
//                        // Читаем все доступные данные
//                        string data = _scannerPort.ReadExisting();

//                        if (!string.IsNullOrEmpty(data))
//                        {
//                            Debug.WriteLine($"[ScannerWorker] Получены данные: '{data}' (длина: {data.Length})");
//                            Debug.WriteLine($"[ScannerWorker] Байты: {string.Join(" ", Encoding.UTF8.GetBytes(data).Select(b => b.ToString("X2")))}");

//                            // Добавляем данные в буфер
//                            _dataBuffer.Append(data);

//                            // Обрабатываем буфер
//                            ProcessBuffer();
//                        }
//                    }
//                }
//            }
//            catch (TimeoutException)
//            {
//                Debug.WriteLine("[ScannerWorker] Таймаут чтения данных");
//            }
//            catch (IOException ex)
//            {
//                Debug.WriteLine($"[ScannerWorker] Ошибка чтения: {ex.Message}");
//                ErrorOccurred?.Invoke("Ошибка чтения данных со сканера");
//            }
//            catch (Exception ex)
//            {
//                Debug.WriteLine($"[ScannerWorker] Ошибка обработки данных: {ex.Message}");
//            }
//        }

//        private void ProcessBuffer()
//        {
//            try
//            {
//                string bufferContent = _dataBuffer.ToString();

//                // Ищем разделители (возможные варианты для Honeywell)
//                char[] separators = { '\r', '\n', '\0' };

//                foreach (char separator in separators)
//                {
//                    if (bufferContent.Contains(separator))
//                    {
//                        var parts = bufferContent.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);

//                        if (parts.Length > 0)
//                        {
//                            // Обрабатываем все полные сообщения кроме последнего
//                            for (int i = 0; i < parts.Length - 1; i++)
//                            {
//                                string barcode = parts[i].Trim();
//                                if (!string.IsNullOrWhiteSpace(barcode))
//                                {
//                                    Debug.WriteLine($"[ScannerWorker] Считан штрихкод: '{barcode}'");
//                                    BarcodeScanned?.Invoke(barcode);
//                                }
//                            }

//                            // Оставляем в буфере последнюю часть (может быть неполной)
//                            string lastPart = parts[parts.Length - 1];

//                            // Если буфер заканчивается разделителем, последняя часть тоже готова
//                            if (bufferContent.EndsWith(separator))
//                            {
//                                if (!string.IsNullOrWhiteSpace(lastPart.Trim()))
//                                {
//                                    Debug.WriteLine($"[ScannerWorker] Считан штрихкод: '{lastPart.Trim()}'");
//                                    BarcodeScanned?.Invoke(lastPart.Trim());
//                                }
//                                _dataBuffer.Clear();
//                            }
//                            else
//                            {
//                                _dataBuffer.Clear();
//                                _dataBuffer.Append(lastPart);
//                            }

//                            return; // Выходим после первого найденного разделителя
//                        }
//                    }
//                }

//                // Если буфер стал слишком большим без разделителей, очищаем его
//                if (_dataBuffer.Length > 1000)
//                {
//                    Debug.WriteLine($"[ScannerWorker] Буфер переполнен, очищаем: '{bufferContent}'");
//                    _dataBuffer.Clear();
//                }
//            }
//            catch (Exception ex)
//            {
//                Debug.WriteLine($"[ScannerWorker] Ошибка обработки буфера: {ex.Message}");
//                _dataBuffer.Clear();
//            }
//        }

//        private void ScannerPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
//        {
//            Debug.WriteLine($"[ScannerWorker] Ошибка порта: {e.EventType}");
//            ErrorOccurred?.Invoke($"Ошибка последовательного порта: {e.EventType}");
//        }

//        private void ClosePort()
//        {
//            lock (_portLock)
//            {
//                if (_scannerPort != null)
//                {
//                    try
//                    {
//                        if (_scannerPort.IsOpen)
//                        {
//                            _scannerPort.DataReceived -= ScannerPort_DataReceived;
//                            _scannerPort.ErrorReceived -= ScannerPort_ErrorReceived;
//                            _scannerPort.Close();
//                            Debug.WriteLine("[ScannerWorker] Порт закрыт");
//                        }
//                    }
//                    catch (Exception ex)
//                    {
//                        Debug.WriteLine($"[ScannerWorker] Ошибка закрытия порта: {ex.Message}");
//                    }
//                    finally
//                    {
//                        _scannerPort.Dispose();
//                        _scannerPort = null;
//                    }
//                }
//            }
//        }

//        protected override void Dispose(bool disposing)
//        {
//            if (!_disposed)
//            {
//                _disposed = true;

//                if (disposing)
//                {
//                    Debug.WriteLine("[ScannerWorker] Освобождение ресурсов");

//                    // Отменяем работу воркера
//                    if (IsBusy)
//                    {
//                        CancelAsync();

//                        // Ждем завершения с таймаутом
//                        var timeout = DateTime.Now.AddSeconds(2);
//                        while (IsBusy && DateTime.Now < timeout)
//                        {
//                            Thread.Sleep(50);
//                        }
//                    }

//                    ClosePort();
//                }
//            }

//            base.Dispose(disposing);
//        }

//        /// <summary>
//        /// Проверка доступности Honeywell сканеров
//        /// </summary>
//        public static bool IsHoneywellScannerAvailable(string portName)
//        {
//            try
//            {
//                var resolver = new LinuxScannerPortResolver();
//                var honeywellPorts = resolver.GetHoneywellScannerPorts();
//                return honeywellPorts.Contains(portName);
//            }
//            catch
//            {
//                return false;
//            }
//        }

//        /// <summary>
//        /// Проверка, что порт не занят
//        /// </summary>
//        public static bool IsPortFree(string portName)
//        {
//            try
//            {
//                using var testPort = new SerialPort(portName)
//                {
//                    BaudRate = 9600,
//                    DataBits = 8,
//                    Parity = Parity.None,
//                    StopBits = StopBits.One,
//                    ReadTimeout = 100,
//                    WriteTimeout = 100
//                };

//                testPort.Open();
//                testPort.Close();
//                return true;
//            }
//            catch
//            {
//                return false;
//            }
//        }

//        /// <summary>
//        /// Получение списка доступных Honeywell сканеров
//        /// </summary>
//        public static string[] GetAvailableHoneywellScanners()
//        {
//            try
//            {
//                var resolver = new LinuxScannerPortResolver();
//                return resolver.GetHoneywellScannerPorts().ToArray();
//            }
//            catch
//            {
//                return new string[0];
//            }
//        }
//    }
//}

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Text;
using System.Runtime.InteropServices;
using System.Linq;
using l2l_aggregator.Services.ScannerService;

namespace l2l_aggregator.Services.AggregationService
{
    internal class ScannerWorker : BackgroundWorker
    {
        public event Action<string> BarcodeScanned;
        public event Action<string> ErrorOccurred;

        private readonly string _portName;
        private SerialPort _scannerPort;
        private readonly object _portLock = new object();
        private bool _disposed = false;
        private StringBuilder _dataBuffer = new StringBuilder();
        private readonly bool _isLinux;

        public ScannerWorker(string portName) : base()
        {
            _portName = portName;
            _isLinux = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            WorkerSupportsCancellation = true;
        }

        protected override void OnDoWork(DoWorkEventArgs e)
        {
            try
            {
                lock (_portLock)
                {
                    if (_disposed || CancellationPending)
                        return;

                    // Создаем порт с настройками, зависящими от платформы
                    _scannerPort = CreateSerialPort();

                    _scannerPort.DataReceived += ScannerPort_DataReceived;
                    _scannerPort.ErrorReceived += ScannerPort_ErrorReceived;

                    _scannerPort.Open();

                    // Очищаем буферы после открытия
                    _scannerPort.DiscardInBuffer();
                    _scannerPort.DiscardOutBuffer();

                    Debug.WriteLine($"[ScannerWorker] Сканер подключен на порту {_portName} ({GetPlatformName()})");
                    Debug.WriteLine($"[ScannerWorker] Настройки: {_scannerPort.BaudRate} baud, {_scannerPort.DataBits} data bits, {_scannerPort.Parity} parity");
                }

                // Держим воркер активным
                while (!CancellationPending && !_disposed)
                {
                    Thread.Sleep(100);

                    // Проверяем состояние порта
                    lock (_portLock)
                    {
                        if (_scannerPort != null && !_scannerPort.IsOpen)
                        {
                            Debug.WriteLine("[ScannerWorker] Порт закрыт, завершение работы");
                            break;
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"[ScannerWorker] Порт занят: {ex.Message}");
                var message = _isLinux
                    ? $"Порт {_portName} занят или нет прав доступа. Попробуйте: sudo chmod 666 {_portName}"
                    : $"Порт {_portName} занят другим приложением";
                ErrorOccurred?.Invoke(message);
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine($"[ScannerWorker] Неверное имя порта: {ex.Message}");
                ErrorOccurred?.Invoke($"Неверное имя порта: {_portName}");
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"[ScannerWorker] Ошибка ввода-вывода: {ex.Message}");
                var message = _isLinux
                    ? $"Устройство {_portName} отключено или недоступно"
                    : $"Ошибка подключения к порту {_portName}";
                ErrorOccurred?.Invoke(message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScannerWorker] Неожиданная ошибка: {ex.Message}");
                ErrorOccurred?.Invoke($"Ошибка сканера: {ex.Message}");
            }
            finally
            {
                ClosePort();
            }
        }

        private SerialPort CreateSerialPort()
        {
            var port = new SerialPort(_portName);

            if (_isLinux)
            {
                // Настройки для Linux (CDC-ACM устройства)
                port.BaudRate = 9600;  // Можно также попробовать 115200
                port.DataBits = 8;
                port.Parity = Parity.None;
                port.StopBits = StopBits.One;
                port.Handshake = Handshake.None;  // Важно для CDC-ACM
                port.ReadTimeout = 500;
                port.WriteTimeout = 500;
                port.RtsEnable = false;  // Отключаем RTS для CDC-ACM
                port.DtrEnable = false;  // Отключаем DTR для CDC-ACM
                port.Encoding = Encoding.UTF8;
            }
            else
            {
                // Настройки для Windows
                port.BaudRate = 9600;
                port.DataBits = 8;
                port.Parity = Parity.None;
                port.StopBits = StopBits.One;
                port.Handshake = Handshake.None;
                port.ReadTimeout = 1000;
                port.WriteTimeout = 1000;
                port.RtsEnable = true;   // На Windows часто нужен RTS
                port.DtrEnable = true;   // На Windows часто нужен DTR
                port.Encoding = Encoding.UTF8;
            }

            return port;
        }

        private void ScannerPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                lock (_portLock)
                {
                    if (_scannerPort != null && _scannerPort.IsOpen)
                    {
                        // Читаем все доступные данные
                        string data = _scannerPort.ReadExisting();

                        if (!string.IsNullOrEmpty(data))
                        {
                            Debug.WriteLine($"[ScannerWorker] Получены данные: '{data}' (длина: {data.Length})");
                            Debug.WriteLine($"[ScannerWorker] Байты: {string.Join(" ", Encoding.UTF8.GetBytes(data).Select(b => b.ToString("X2")))}");

                            // Добавляем данные в буфер
                            _dataBuffer.Append(data);

                            // Обрабатываем буфер
                            ProcessBuffer();
                        }
                    }
                }
            }
            catch (TimeoutException)
            {
                Debug.WriteLine("[ScannerWorker] Таймаут чтения данных");
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"[ScannerWorker] Ошибка чтения: {ex.Message}");
                ErrorOccurred?.Invoke("Ошибка чтения данных со сканера");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScannerWorker] Ошибка обработки данных: {ex.Message}");
            }
        }

        private void ProcessBuffer()
        {
            try
            {
                string bufferContent = _dataBuffer.ToString();

                // Разные сканеры используют разные разделители
                char[] separators = { '\r', '\n', '\0' };

                foreach (char separator in separators)
                {
                    if (bufferContent.Contains(separator))
                    {
                        var parts = bufferContent.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);

                        if (parts.Length > 0)
                        {
                            // Обрабатываем все полные сообщения кроме последнего
                            for (int i = 0; i < parts.Length - 1; i++)
                            {
                                string barcode = parts[i].Trim();
                                if (!string.IsNullOrWhiteSpace(barcode))
                                {
                                    Debug.WriteLine($"[ScannerWorker] Считан штрихкод: '{barcode}'");
                                    BarcodeScanned?.Invoke(barcode);
                                }
                            }

                            // Оставляем в буфере последнюю часть (может быть неполной)
                            string lastPart = parts[parts.Length - 1];

                            // Если буфер заканчивается разделителем, последняя часть тоже готова
                            if (bufferContent.EndsWith(separator))
                            {
                                if (!string.IsNullOrWhiteSpace(lastPart.Trim()))
                                {
                                    Debug.WriteLine($"[ScannerWorker] Считан штрихкод: '{lastPart.Trim()}'");
                                    BarcodeScanned?.Invoke(lastPart.Trim());
                                }
                                _dataBuffer.Clear();
                            }
                            else
                            {
                                _dataBuffer.Clear();
                                _dataBuffer.Append(lastPart);
                            }

                            return; // Выходим после первого найденного разделителя
                        }
                    }
                }

                // Если буфер стал слишком большим без разделителей, очищаем его
                if (_dataBuffer.Length > 1000)
                {
                    Debug.WriteLine($"[ScannerWorker] Буфер переполнен, очищаем: '{bufferContent}'");
                    _dataBuffer.Clear();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScannerWorker] Ошибка обработки буфера: {ex.Message}");
                _dataBuffer.Clear();
            }
        }

        private void ScannerPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            Debug.WriteLine($"[ScannerWorker] Ошибка порта: {e.EventType}");
            ErrorOccurred?.Invoke($"Ошибка последовательного порта: {e.EventType}");
        }

        private void ClosePort()
        {
            lock (_portLock)
            {
                if (_scannerPort != null)
                {
                    try
                    {
                        if (_scannerPort.IsOpen)
                        {
                            _scannerPort.DataReceived -= ScannerPort_DataReceived;
                            _scannerPort.ErrorReceived -= ScannerPort_ErrorReceived;
                            _scannerPort.Close();
                            Debug.WriteLine("[ScannerWorker] Порт закрыт");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ScannerWorker] Ошибка закрытия порта: {ex.Message}");
                    }
                    finally
                    {
                        _scannerPort.Dispose();
                        _scannerPort = null;
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    Debug.WriteLine("[ScannerWorker] Освобождение ресурсов");

                    // Отменяем работу воркера
                    if (IsBusy)
                    {
                        CancelAsync();

                        // Ждем завершения с таймаутом
                        var timeout = DateTime.Now.AddSeconds(2);
                        while (IsBusy && DateTime.Now < timeout)
                        {
                            Thread.Sleep(50);
                        }
                    }

                    ClosePort();
                }
            }

            base.Dispose(disposing);
        }

        private string GetPlatformName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Windows";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "Linux";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "macOS";
            return "Unknown";
        }

        /// <summary>
        /// Проверка доступности порта (кроссплатформенная)
        /// </summary>
        public static bool IsPortAvailable(string portName)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var availablePorts = SerialPort.GetPortNames();
                    return availablePorts.Contains(portName, StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    // Для Linux используем LinuxScannerPortResolver
                    var resolver = new LinuxScannerPortResolver();
                    var honeywellPorts = resolver.GetHoneywellScannerPorts();
                    return honeywellPorts.Contains(portName);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScannerWorker] Ошибка проверки доступности порта: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Проверка, что порт не занят (кроссплатформенная)
        /// </summary>
        public static bool IsPortFree(string portName)
        {
            try
            {
                var testWorker = new ScannerWorker(portName);
                using var testPort = testWorker.CreateSerialPort();

                testPort.Open();
                testPort.Close();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScannerWorker] Порт {portName} занят или недоступен: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Получение списка доступных сканеров (кроссплатформенно)
        /// </summary>
        public static string[] GetAvailableScanners()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // На Windows возвращаем все COM порты
                    return SerialPort.GetPortNames();
                }
                else
                {
                    // На Linux используем LinuxScannerPortResolver для поиска Honeywell сканеров
                    var resolver = new LinuxScannerPortResolver();
                    return resolver.GetHoneywellScannerPorts().ToArray();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScannerWorker] Ошибка получения списка сканеров: {ex.Message}");
                return new string[0];
            }
        }

        /// <summary>
        /// Тестирование порта сканера
        /// </summary>
        public static async System.Threading.Tasks.Task<bool> TestPortAsync(string portName, int timeoutMs = 5000)
        {
            try
            {
                Debug.WriteLine($"[ScannerWorker] Тестирование порта {portName}...");

                var testWorker = new ScannerWorker(portName);
                bool dataReceived = false;

                testWorker.BarcodeScanned += (data) =>
                {
                    Debug.WriteLine($"[ScannerWorker] Тест успешен - получены данные: {data}");
                    dataReceived = true;
                };

                testWorker.ErrorOccurred += (error) =>
                {
                    Debug.WriteLine($"[ScannerWorker] Ошибка теста: {error}");
                };

                testWorker.RunWorkerAsync();

                // Ждем данные или таймаут
                var startTime = DateTime.Now;
                while (!dataReceived && (DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
                {
                    await System.Threading.Tasks.Task.Delay(100);

                    if (!testWorker.IsBusy)
                        break;
                }

                testWorker.Dispose();

                Debug.WriteLine($"[ScannerWorker] Тест порта {portName} завершен. Данные получены: {dataReceived}");
                return dataReceived;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScannerWorker] Ошибка тестирования порта {portName}: {ex.Message}");
                return false;
            }
        }
    }
}