using MD.Devices.Printer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace l2l_aggregator.Helpers.AggregationHelpers
{
    public class PrinterTCP(string name, ILogger logger) : MD.Devices.TCP.Device(name, logger), IPrinter
    {
        // Конфигурация принтера
        private PrinterTcpConfig printerConfig = new();

        /// <inheritdoc/>
        public override void Configure(IConfiguration configuration)
        {
            base.Configure(configuration);
            configuration.Bind(printerConfig);
        }
        // Счетчик для расчетного определения размера буфера
        private int bufferSize = 0;

        public int BufferSize => bufferSize;

        public void Confirm(int count = 1)
        {
            bufferSize -= count;
        }
    }
}
