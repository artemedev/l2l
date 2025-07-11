using l2l_aggregator.Services.ScannerService.Interfaces;
using System.Collections.Generic;
using System.Management;
using System.Text.RegularExpressions;

namespace l2l_aggregator.Services.ScannerService
{
    public class WindowsScannerPortResolver : IScannerPortResolver
    {
        public IEnumerable<string> GetHoneywellScannerPorts()
        {
            var result = new List<string>();

            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");
            foreach (var device in searcher.Get())
            {
                var deviceId = device["DeviceID"]?.ToString(); // USB\\VID_0C2E...
                var name = device["Name"]?.ToString();         // Honeywell USB Serial (COM3)

                if (deviceId?.ToLower().Contains("vid_0c2e") == true)
                {
                    var match = Regex.Match(name ?? "", @"\(COM\d+\)");
                    if (match.Success)
                    {
                        result.Add(match.Value.Trim('(', ')')); // COM3
                    }
                }
            }

            return result;
        }
    }
}
