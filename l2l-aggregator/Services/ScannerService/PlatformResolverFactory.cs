using l2l_aggregator.Services.ScannerService.Interfaces;
using System;
using System.Runtime.InteropServices;

namespace l2l_aggregator.Services.ScannerService
{
    public static class PlatformResolverFactory
    {
        public static IScannerPortResolver CreateScannerResolver()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return new WindowsScannerPortResolver();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return new LinuxScannerPortResolver();
            else
                throw new PlatformNotSupportedException("Unsupported OS for scanner detection.");
        }
    }
}
