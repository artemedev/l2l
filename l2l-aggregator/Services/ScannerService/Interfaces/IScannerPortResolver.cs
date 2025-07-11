using System.Collections.Generic;

namespace l2l_aggregator.Services.ScannerService.Interfaces
{
    public interface IScannerPortResolver
    {
        IEnumerable<string> GetHoneywellScannerPorts();
    }
}
