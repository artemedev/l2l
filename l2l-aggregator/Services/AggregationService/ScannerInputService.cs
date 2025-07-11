using System;
using System.Text;
using System.Timers;

namespace l2l_aggregator.Services.AggregationService
{
    public class ScannerInputService
    {
        private StringBuilder _buffer = new();
        private Timer _inputTimer;

        private const int MAX_INTERVAL_MS = 100; // Если между символами прошло > 100мс, считаем это вводом человека

        public event Action<string> BarcodeScanned;

        public ScannerInputService()
        {
            _inputTimer = new Timer(MAX_INTERVAL_MS);
            _inputTimer.Elapsed += (_, _) => ResetBuffer();
            _inputTimer.AutoReset = false;
        }

        public void ProcessKey(char input)
        {
            if (input == '\r' || input == '\n')
            {
                if (_buffer.Length > 0)
                {
                    var code = _buffer.ToString();
                    _buffer.Clear();
                    BarcodeScanned?.Invoke(code);
                    Console.WriteLine($"[ScannerInput] Barcode received: {code}");
                }

                _inputTimer.Stop();
            }
            else
            {
                _buffer.Append(input);
                _inputTimer.Stop();
                _inputTimer.Start(); // перезапуск таймера
            }
        }

        private void ResetBuffer()
        {
            Console.WriteLine("[ScannerInput] Input timeout, clearing buffer");
            _buffer.Clear();
        }
    }
}
