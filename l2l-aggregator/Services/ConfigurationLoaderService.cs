using l2l_aggregator.Services.Database;
using l2l_aggregator.ViewModels.VisualElements;
using System.Threading.Tasks;

namespace l2l_aggregator.Services
{
    public class ConfigurationLoaderService
    {
        private readonly DatabaseService _databaseService;

        public ConfigurationLoaderService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public async Task<(CameraViewModel Camera, bool DisableVK)> LoadSettingsToSessionAsync()
        {
            var session = SessionService.Instance;

            session.PrinterIP = await _databaseService.Config.GetConfigValueAsync("PrinterIP");
            session.PrinterModel = await _databaseService.Config.GetConfigValueAsync("PrinterModel");
            session.ControllerIP = await _databaseService.Config.GetConfigValueAsync("ControllerIP");
            session.CameraIP = await _databaseService.Config.GetConfigValueAsync("CameraIP");
            session.CameraModel = await _databaseService.Config.GetConfigValueAsync("CameraModel");

            session.CheckCamera = await _databaseService.Config.GetConfigValueAsync("CheckCamera") == "True";
            session.CheckPrinter = await _databaseService.Config.GetConfigValueAsync("CheckPrinter") == "True";
            session.CheckController = await _databaseService.Config.GetConfigValueAsync("CheckController") == "True";
            session.CheckScanner = await _databaseService.Config.GetConfigValueAsync("CheckScanner") == "True";

            session.ScannerPort = await _databaseService.Config.GetConfigValueAsync("ScannerCOMPort");
            session.ScannerModel = await _databaseService.Config.GetConfigValueAsync("ScannerModel");

            session.DisableVirtualKeyboard = bool.TryParse(
                await _databaseService.Config.GetConfigValueAsync("DisableVirtualKeyboard"),
                out var parsedKeyboard) && parsedKeyboard;

            // Возвращаем также камеру (если нужно использовать в UI)
            var camera = new CameraViewModel
            {
                CameraIP = session.CameraIP,
                SelectedCameraModel = session.CameraModel
            };

            return (camera, session.DisableVirtualKeyboard);
        }
    }
}
