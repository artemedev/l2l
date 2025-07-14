using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

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

        public ScannerWorker(string portName) : base()
        {
            _portName = portName;
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

                    _scannerPort = new SerialPort(_portName, 9600, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = 1000,
                        WriteTimeout = 1000
                    };

                    _scannerPort.DataReceived += ScannerPort_DataReceived;
                    _scannerPort.ErrorReceived += ScannerPort_ErrorReceived;

                    _scannerPort.Open();
                    Debug.WriteLine($"[ScannerWorker] Сканер подключен на порту {_portName}");
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
                ErrorOccurred?.Invoke($"Порт {_portName} занят другим приложением");
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine($"[ScannerWorker] Неверное имя порта: {ex.Message}");
                ErrorOccurred?.Invoke($"Неверное имя порта: {_portName}");
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"[ScannerWorker] Ошибка ввода-вывода: {ex.Message}");
                ErrorOccurred?.Invoke($"Ошибка подключения к порту {_portName}");
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

        private void ScannerPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                lock (_portLock)
                {
                    if (_scannerPort != null && _scannerPort.IsOpen)
                    {
                        string data = _scannerPort.ReadExisting();
                        if (!string.IsNullOrWhiteSpace(data))
                        {
                            Debug.WriteLine($"[ScannerWorker] Считан ШК: {data}");
                            BarcodeScanned?.Invoke(data.Trim());
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

        /// <summary>
        /// Проверка доступности порта
        /// </summary>
        public static bool IsPortAvailable(string portName)
        {
            try
            {
                var availablePorts = SerialPort.GetPortNames();
                return Array.Exists(availablePorts, port => port.Equals(portName, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Проверка, что порт не занят
        /// </summary>
        public static bool IsPortFree(string portName)
        {
            try
            {
                using var testPort = new SerialPort(portName);
                testPort.Open();
                testPort.Close();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}