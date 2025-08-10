using l2l_aggregator.Services.Database;
using l2l_aggregator.ViewModels.VisualElements;
using System.Threading.Tasks;

namespace l2l_aggregator.Services.Configuration
{
    public class ConfigurationLoaderService
    {
        private readonly IConfigurationFileService _configurationService;

        public ConfigurationLoaderService(IConfigurationFileService configurationService)
        {
            _configurationService = configurationService;
        }

        public async Task<(CameraViewModel Camera, bool DisableVK)> LoadSettingsToSessionAsync()
        {
            var session = SessionService.Instance;

            session.PrinterIP = await _configurationService.GetConfigValueAsync("PrinterIP");
            session.PrinterModel = await _configurationService.GetConfigValueAsync("PrinterModel");
            session.ControllerIP = await _configurationService.GetConfigValueAsync("ControllerIP");
            session.CameraIP = await _configurationService.GetConfigValueAsync("CameraIP");
            session.CameraModel = await _configurationService.GetConfigValueAsync("CameraModel");

            session.CheckCamera = await _configurationService.GetConfigValueAsync("CheckCamera") == "True";
            session.CheckPrinter = await _configurationService.GetConfigValueAsync("CheckPrinter") == "True";
            session.CheckController = await _configurationService.GetConfigValueAsync("CheckController") == "True";
            session.CheckScanner = await _configurationService.GetConfigValueAsync("CheckScanner") == "True";

            session.ScannerPort = await _configurationService.GetConfigValueAsync("ScannerCOMPort");
            session.ScannerModel = await _configurationService.GetConfigValueAsync("ScannerModel");

            session.EnableVirtualKeyboard = bool.TryParse(
                await _configurationService.GetConfigValueAsync("EnableVirtualKeyboard"),
                out var parsedKeyboard) && parsedKeyboard;

            // Возвращаем также камеру (если нужно использовать в UI)
            var camera = new CameraViewModel
            {
                CameraIP = session.CameraIP,
                SelectedCameraModel = session.CameraModel
            };

            return (camera, session.EnableVirtualKeyboard);
        }
    }
}
