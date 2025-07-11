using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace l2l_aggregator.Helpers.AggregationHelpers
{
    public static class PrinterConfigBuilder
    {
        public static IConfiguration Build(string ip, int port = 6101)
        {
            var settings = new Dictionary<string, string>
            {
                ["Connection:Ip"] = ip,
                ["Connection:Port"] = port.ToString(),
                ["Connection:ConnectTimeout"] = "5000",
                ["Connection:ReceiveTimeout"] = "1000",
                ["Connection:SendTimeout"] = "1000",
                ["Connection:MaxBufferSize"] = "1048576",
                ["RequestStatusTimeOut"] = "2000",
                ["TcpKeepAliveTime"] = "2",
                ["TcpKeepAliveInterval"] = "1",
                ["TcpKeepAliveRetryCount"] = "2",
                ["KeepAliveEnable"] = "true",

                // GS настройки по умолчанию
                ["GS:GS91"] = "!",
                ["GS:GS92"] = "!",
                ["GS:GS93"] = "!",

                ["SleepTime"] = "10",
                ["EndOfLine"] = "AB",
                ["EncodingName"] = "ASCII"
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }
    }
}
