using HidSharp;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace l2l_aggregator.Services.AggregationService
{
    public class ScannerListenerService
    {
        private HidDevice _device;
        private HidStream _stream;
        private CancellationTokenSource _cts;

        public event Action<string> BarcodeScanned;

        public bool StartListening(string devicePath)
        {
            var device = DeviceList.Local.GetHidDevices().FirstOrDefault(d => d.DevicePath == devicePath);
            if (device == null) return false;

            _device = device;
            _cts = new CancellationTokenSource();

            Task.Run(() => Listen(_cts.Token));

            return true;
        }

        public void StopListening()
        {
            _cts?.Cancel();
            _stream?.Close();
        }

        private async Task Listen(CancellationToken token)
        {
            try
            {
                if (_device.TryOpen(out _stream))
                {
                    Console.WriteLine("[Scanner] Открыт HID поток");
                    _stream.ReadTimeout = 3000; // 3 секунды

                    var reportLength = _device.GetMaxInputReportLength();
                    var buffer = new byte[reportLength];
                    var sb = new StringBuilder();

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            int bytesRead = _stream.Read(buffer, 0, buffer.Length); // sync
                            Console.WriteLine($"[Scanner] Прочитано байт: {bytesRead}");

                            foreach (var b in buffer.Skip(2).Where(b => b > 0))
                            {
                                char ch = TranslateHidKeyToChar(b);
                                if (ch == '\n' || ch == '\r')
                                {
                                    if (sb.Length > 0)
                                    {
                                        BarcodeScanned?.Invoke(sb.ToString());
                                        Console.WriteLine($"[Scanner] Скан завершён: {sb}");
                                        sb.Clear();
                                    }
                                }
                                else
                                {
                                    sb.Append(ch);
                                }
                            }
                        }
                        catch (TimeoutException)
                        {
                            // ⏳ Таймаут — просто ждём следующую итерацию
                            Console.WriteLine("[Scanner] Таймаут ожидания сканера — ждём дальше...");
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Scanner] ФАТАЛЬНАЯ ОШИБКА: " + ex.Message);
            }
        }
        private char TranslateHidKeyToChar(byte code)
        {
            // Простая таблица HID key → char (можно дополнить под язык)
            string hidMap = "abcdefghijklmnopqrstuvwxyz1234567890";
            if (code >= 4 && code <= 29) return hidMap[code - 4];      // a-z
            if (code >= 30 && code <= 39) return hidMap[code - 30 + 26]; // 1-9,0
            if (code == 40) return '\n'; // Enter
            return '?';
        }
    }
}
