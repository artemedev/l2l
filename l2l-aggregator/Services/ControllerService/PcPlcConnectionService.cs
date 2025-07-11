using NModbus;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace l2l_aggregator.Services.ControllerService
{
    public class PcPlcConnectionService : IDisposable
    {
        private readonly ILogger<PcPlcConnectionService> _logger;
        private TcpClient _tcpClient;
        private IModbusMaster _master;
        private Timer _pingPongTimer;
        private bool _isConnected = false;
        private bool _disposedValue = false;

        // Modbus константы
        private const byte SlaveId = 1;
        private const int ModbusPort = 502;

        // Регестрируем адреса из документации
        private const ushort PC_PLC_CONNECT_REGISTER = 0;          // 4x0
        private const ushort PC_TIMEOUT_REGISTER = 17;             // 4x17
        private const ushort CYCLE_STEP_NUMBER_REGISTER = 16;      // 4x16
        private const ushort NON_FATAL_ERRORS_REGISTER = 4;        // 3x4
        private const ushort FATAL_ERRORS_REGISTER = 5;            // 3x5
        private const ushort FATAL_ERRORS_FINAL_REGISTER = 6;      // 3x6

        // Номер бита в регистре
        private const int PC_PLC_CONNECT_BIT = 10;                 // 4x0.10
        private const int PC_PLC_CONNECT_CONTROL_BIT = 12;         // 4x0.12
        private const int PLC_IS_ACTIVE_BIT = 11;                  // 4x0.11
        private const int FORCE_POSITIONING_BIT = 0;              // 4x0.0
        private const int POSITIONING_PERMIT_BIT = 1;             // 4x0.1
        private const int CYCLE_STEP_START_BIT = 5;               // 4x0.5
        private const int PHOTO_TAKEN_BIT = 6;                    // 4x0.6
        private const int START_PEDAL_BIT = 7;                    // 4x0.7
        private const int APPLY_CAM_BOX_DIST_BIT = 8;             // 4x0.8
        private const int CONTINUOUS_LIGHT_MODE_BIT = 9;          // 4x0.9

        // Номер регистра настроек
        private const ushort RETREAT_ZERO_HOME_POSITION_REGISTER = 1;   // 4x1
        private const ushort ZERO_POSITIONING_REGISTER = 2;             // 4x2
        private const ushort ESTIMATED_ZERO_HOME_DISTANCE_REGISTER = 3; // 4x3
        private const ushort TIME_BETWEEN_DIRECTIONS_CHANGE_REGISTER = 4; // 4x4
        private const ushort CAM_MOVEMENT_VELOCITY_REGISTER = 5;        // 4x5
        private const ushort CAM_BOX_MIN_DISTANCE_REGISTER = 7;         // 4x7
        private const ushort CAM_BOX_DISTANCE_REGISTER = 8;             // 4x8
        private const ushort BOX_HEIGHT_REGISTER = 9;                   // 4x9
        private const ushort LAYERS_QTTY_REGISTER = 10;                 // 4x10
        private const ushort LIGHT_LEVEL_REGISTER = 11;                 // 4x11
        private const ushort LIGHT_DELAY_REGISTER = 12;                 // 4x12
        private const ushort LIGHT_EXPOSURE_REGISTER = 13;              // 4x13
        private const ushort CAM_DELAY_REGISTER = 14;                   // 4x14
        private const ushort CAM_EXPOSURE_REGISTER = 15;                // 4x15

        public event Action<bool> ConnectionStatusChanged;
        public event Action<PlcErrors> ErrorsReceived;

        public bool IsConnected => _isConnected;

        public PcPlcConnectionService(ILogger<PcPlcConnectionService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> ConnectAsync(string ipAddress)
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(ipAddress, ModbusPort);

                var factory = new ModbusFactory();
                _master = factory.CreateMaster(_tcpClient);

                _isConnected = true;
                ConnectionStatusChanged?.Invoke(true);

                _logger.LogInformation($"Connected to PLC at {ipAddress}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to connect to PLC at {ipAddress}");
                _isConnected = false;
                ConnectionStatusChanged?.Invoke(false);
                return false;
            }
        }

        public void Disconnect()
        {
            StopPingPong();

            _master?.Dispose();
            _tcpClient?.Close();

            _isConnected = false;
            ConnectionStatusChanged?.Invoke(false);

            _logger.LogInformation("Disconnected from PLC");
        }

        public async Task<bool> TestConnectionAsync()
        {
            if (!_isConnected) return false;

            try
            {
                // Включить управление подключением
                await EnableConnectionControlAsync(true);

                // Выполнить одиночный тест пинг-понга
                bool pingResult = await PerformSinglePingPongAsync();

                if (pingResult)
                {
                    _logger.LogInformation("PLC ping-pong test successful");
                    return true;
                }
                else
                {
                    _logger.LogWarning("PLC ping-pong test failed");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during PLC connection test");
                return false;
            }
        }

        public void StartPingPong(int intervalMs = 1000)
        {
            if (!_isConnected)
            {
                _logger.LogWarning("Cannot start ping-pong: not connected to PLC");
                return;
            }

            StopPingPong();

            _pingPongTimer = new Timer(async _ => await PerformPingPongCycle(),
                                     null, 0, intervalMs);

            _logger.LogInformation($"Started ping-pong with interval {intervalMs}ms");
        }

        public void StopPingPong()
        {
            _pingPongTimer?.Dispose();
            _pingPongTimer = null;

            _ = Task.Run(async () =>
            {
                try
                {
                    await EnableConnectionControlAsync(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disabling connection control");
                }
            });

            _logger.LogInformation("Stopped ping-pong");
        }

        private async Task<bool> PerformSinglePingPongAsync()
        {
            try
            {
                // Узнать текущее состояние
                var holdingRegs = await _master.ReadHoldingRegistersAsync(SlaveId, PC_PLC_CONNECT_REGISTER, 1);
                bool pcPlcConnectBit = GetBit(holdingRegs[0], PC_PLC_CONNECT_BIT);

                if (pcPlcConnectBit)
                {
                    // Сбросить бит (с PC в PLC)
                    await SetBitAsync(PC_PLC_CONNECT_REGISTER, PC_PLC_CONNECT_BIT, false);

                    // Установить PLC флаг активности
                    await SetBitAsync(PC_PLC_CONNECT_REGISTER, PLC_IS_ACTIVE_BIT, true);

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during ping-pong operation");
                return false;
            }
        }

        private async Task PerformPingPongCycle()
        {
            try
            {
                var success = await PerformSinglePingPongAsync();

                if (!success)
                {
                    // Проверка на ошибки
                    await CheckErrorsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ping-pong cycle");
            }
        }

        public async Task EnableConnectionControlAsync(bool enable)
        {
            await SetBitAsync(PC_PLC_CONNECT_REGISTER, PC_PLC_CONNECT_CONTROL_BIT, enable);
            _logger.LogInformation($"Connection control {(enable ? "enabled" : "disabled")}");
        }

        // Настройки позиционирования камеры
        public async Task SetPositioningSettingsAsync(PositioningSettings settings)
        {
            await _master.WriteSingleRegisterAsync(SlaveId, RETREAT_ZERO_HOME_POSITION_REGISTER, settings.RetreatZeroHomePosition);
            await _master.WriteSingleRegisterAsync(SlaveId, ZERO_POSITIONING_REGISTER, settings.ZeroPositioning);
            await _master.WriteSingleRegisterAsync(SlaveId, ESTIMATED_ZERO_HOME_DISTANCE_REGISTER, settings.EstimatedZeroHomeDistance);
            await _master.WriteSingleRegisterAsync(SlaveId, TIME_BETWEEN_DIRECTIONS_CHANGE_REGISTER, settings.TimeBetweenDirectionsChange);
            await _master.WriteSingleRegisterAsync(SlaveId, CAM_MOVEMENT_VELOCITY_REGISTER, settings.CamMovementVelocity);

            _logger.LogInformation("Positioning settings updated");
        }

        public async Task<PositioningSettings> GetPositioningSettingsAsync()
        {
            var registers = await _master.ReadHoldingRegistersAsync(SlaveId, RETREAT_ZERO_HOME_POSITION_REGISTER, 5);

            return new PositioningSettings
            {
                RetreatZeroHomePosition = registers[0],
                ZeroPositioning = registers[1],
                EstimatedZeroHomeDistance = registers[2],
                TimeBetweenDirectionsChange = registers[3],
                CamMovementVelocity = registers[4]
            };
        }

        //Настройки работы с коробкой
        public async Task SetBoxWorkSettingsAsync(BoxWorkSettings settings)
        {
            await _master.WriteSingleRegisterAsync(SlaveId, CAM_BOX_MIN_DISTANCE_REGISTER, settings.CamBoxMinDistance);
            await _master.WriteSingleRegisterAsync(SlaveId, CAM_BOX_DISTANCE_REGISTER, settings.CamBoxDistance);
            await _master.WriteSingleRegisterAsync(SlaveId, BOX_HEIGHT_REGISTER, settings.BoxHeight);
            await _master.WriteSingleRegisterAsync(SlaveId, LAYERS_QTTY_REGISTER, settings.LayersQtty);

            _logger.LogInformation("Box work settings updated");
        }

        // Настройки освещения
        public async Task SetLightingSettingsAsync(LightingSettings settings)
        {
            await _master.WriteSingleRegisterAsync(SlaveId, LIGHT_LEVEL_REGISTER, settings.LightLevel);
            await _master.WriteSingleRegisterAsync(SlaveId, LIGHT_DELAY_REGISTER, settings.LightDelay);
            await _master.WriteSingleRegisterAsync(SlaveId, LIGHT_EXPOSURE_REGISTER, settings.LightExposure);
            await _master.WriteSingleRegisterAsync(SlaveId, CAM_DELAY_REGISTER, settings.CamDelay);
            await _master.WriteSingleRegisterAsync(SlaveId, CAM_EXPOSURE_REGISTER, settings.CamExposure);
            await SetBitAsync(PC_PLC_CONNECT_REGISTER, CONTINUOUS_LIGHT_MODE_BIT, settings.ContinuousLightMode);

            _logger.LogInformation("Lighting settings updated");
        }

        public async Task<LightingSettings> GetLightingSettingsAsync()
        {
            var registers = await _master.ReadHoldingRegistersAsync(SlaveId, LIGHT_LEVEL_REGISTER, 5);
            var holdingReg0 = await _master.ReadHoldingRegistersAsync(SlaveId, PC_PLC_CONNECT_REGISTER, 1);

            return new LightingSettings
            {
                LightLevel = registers[0],
                LightDelay = registers[1],
                LightExposure = registers[2],
                CamDelay = registers[3],
                CamExposure = registers[4],
                ContinuousLightMode = GetBit(holdingReg0[0], CONTINUOUS_LIGHT_MODE_BIT)
            };
        }

        // Работа в цикле
        public async Task StartCycleStepAsync(ushort stepNumber)
        {
            await _master.WriteSingleRegisterAsync(SlaveId, CYCLE_STEP_NUMBER_REGISTER, stepNumber);
            await SetBitAsync(PC_PLC_CONNECT_REGISTER, CYCLE_STEP_START_BIT, true);

            _logger.LogInformation($"Started cycle step {stepNumber}");
        }
        //!!!!КАК ЭТО РАБОТАЕТ. СЕЙЧАС Я ПОЛУЧАЮ ФОТО ОТ БИБЛИОТЕКИ РАСПОЗНАВАНИЯ, ТО ЕСТЬ ТАМ ТОЖЕ ЕСТЬ ТРИГЕР ДЛЯ ФОТО, ТРИГЕР КАМЕРЫ ДЕЛАЕТ КАДР? КОГДА БУДЕТ ПЕДАЛЬ!!!! 
        //START_PEDAL_BIT = 7 // 4x0.7
        public async Task TriggerPhotoAsync()
        {
            await SetBitAsync(PC_PLC_CONNECT_REGISTER, START_PEDAL_BIT, true);
            _logger.LogInformation("Photo trigger activated");
        }

        public async Task ConfirmPhotoProcessedAsync()
        {
            await SetBitAsync(PC_PLC_CONNECT_REGISTER, PHOTO_TAKEN_BIT, true);
            _logger.LogInformation("Photo processing confirmed");
        }

        public async Task ApplyCameraBoxDistanceAsync()
        {
            await SetBitAsync(PC_PLC_CONNECT_REGISTER, APPLY_CAM_BOX_DIST_BIT, true);
            _logger.LogInformation("Camera-box distance applied");
        }

        // Операции по позиционированию
        public async Task ForcePositioningAsync()
        {
            await SetBitAsync(PC_PLC_CONNECT_REGISTER, FORCE_POSITIONING_BIT, true);
            _logger.LogInformation("Force positioning initiated");
        }

        public async Task GrantPositioningPermissionAsync()
        {
            await SetBitAsync(PC_PLC_CONNECT_REGISTER, POSITIONING_PERMIT_BIT, true);
            _logger.LogInformation("Positioning permission granted");
        }

        // Проверка ошибок
        public async Task CheckErrorsAsync()
        {
            try
            {
                var nonFatalErrors = await _master.ReadInputRegistersAsync(SlaveId, NON_FATAL_ERRORS_REGISTER, 1);
                var fatalErrors = await _master.ReadInputRegistersAsync(SlaveId, FATAL_ERRORS_REGISTER, 1);
                var fatalErrorsFinal = await _master.ReadInputRegistersAsync(SlaveId, FATAL_ERRORS_FINAL_REGISTER, 1);

                var errors = new PlcErrors
                {
                    NonFatalErrors = nonFatalErrors[0],
                    FatalErrors = fatalErrors[0],
                    FatalErrorsFinal = fatalErrorsFinal[0]
                };

                if (errors.HasErrors())
                {
                    ErrorsReceived?.Invoke(errors);
                    _logger.LogWarning($"PLC errors detected: NonFatal={errors.NonFatalErrors:X4}, Fatal={errors.FatalErrors:X4}, FinalFatal={errors.FatalErrorsFinal:X4}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading PLC errors");
            }
        }

        // Вспомогательные методы
        private async Task SetBitAsync(ushort register, int bitPosition, bool value)
        {
            var currentValue = await _master.ReadHoldingRegistersAsync(SlaveId, register, 1);
            var newValue = SetBit(currentValue[0], bitPosition, value);
            await _master.WriteSingleRegisterAsync(SlaveId, register, newValue);
        }

        private static bool GetBit(ushort value, int bitPosition)
        {
            return (value & (1 << bitPosition)) != 0;
        }

        private static ushort SetBit(ushort value, int bitPosition, bool bitValue)
        {
            if (bitValue)
                return (ushort)(value | (1 << bitPosition));
            else
                return (ushort)(value & ~(1 << bitPosition));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    StopPingPong();
                    Disconnect();
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    // Интерфейсы
    public class PositioningSettings
    {
        public ushort RetreatZeroHomePosition { get; set; } = 70;
        public ushort ZeroPositioning { get; set; } = 10000;
        public ushort EstimatedZeroHomeDistance { get; set; } = 252;
        public ushort TimeBetweenDirectionsChange { get; set; } = 500;
        public ushort CamMovementVelocity { get; set; } = 20;
    }

    public class BoxWorkSettings
    {
        public ushort CamBoxMinDistance { get; set; } = 500;
        public ushort CamBoxDistance { get; set; } = 450;
        public ushort BoxHeight { get; set; } = 50;
        public ushort LayersQtty { get; set; } = 4;
    }

    public class LightingSettings
    {
        public ushort LightLevel { get; set; } = 100;
        public ushort LightDelay { get; set; } = 1000;
        public ushort LightExposure { get; set; } = 4000;
        public ushort CamDelay { get; set; } = 1000;
        public ushort CamExposure { get; set; } = 30;
        public bool ContinuousLightMode { get; set; } = false;
    }

    public class PlcErrors
    {
        public ushort NonFatalErrors { get; set; }
        public ushort FatalErrors { get; set; }
        public ushort FatalErrorsFinal { get; set; }

        public bool HasErrors() => NonFatalErrors != 0 || FatalErrors != 0 || FatalErrorsFinal != 0;

        public string GetErrorDescription()
        {
            var errors = new List<string>();

            // Non-fatal errors
            if ((NonFatalErrors & (1 << 0)) != 0) errors.Add("PC response timeout exceeded (1500ms)");
            if ((NonFatalErrors & (1 << 1)) != 0) errors.Add("PLC response timeout exceeded");
            if ((NonFatalErrors & (1 << 2)) != 0) errors.Add("No printer ready signal");

            // Fatal errors
            if ((FatalErrors & (1 << 0)) != 0) errors.Add("Stepper motor driver error");
            if ((FatalErrors & (1 << 1)) != 0) errors.Add("Lower limit switch activated (SW_1)");
            if ((FatalErrors & (1 << 2)) != 0) errors.Add("Upper limit switch activated (SW_2)");
            if ((FatalErrors & (1 << 3)) != 0) errors.Add("Insufficient camera-box distance (IrSens_1)");
            if ((FatalErrors & (1 << 4)) != 0) errors.Add("Low light level on photographed surface");
            if ((FatalErrors & (1 << 5)) != 0) errors.Add("Safety circuit violation");
            if ((FatalErrors & (1 << 6)) != 0) errors.Add("Home position sensor triggered too early");
            if ((FatalErrors & (1 << 7)) != 0) errors.Add("Home position sensor not triggered in expected range");
            if ((FatalErrors & (1 << 8)) != 0) errors.Add("Cycle step positioning timeout");
            if ((FatalErrors & (1 << 9)) != 0) errors.Add("Zero positioning timeout - sensor still active");
            if ((FatalErrors & (1 << 10)) != 0) errors.Add("Zero position exceeded retreat distance");
            if ((FatalErrors & (1 << 11)) != 0) errors.Add("Zero position sensor not triggered during downward movement");
            if ((FatalErrors & (1 << 12)) != 0) errors.Add("Zero position sensor not triggered after reaching -10mm");
            if ((FatalErrors & (1 << 13)) != 0) errors.Add("Zero sensor triggered too early during downward movement");
            if ((FatalErrors & (1 << 14)) != 0) errors.Add("Arrow overshot position during Zero to Home movement");

            // Final fatal errors
            if ((FatalErrorsFinal & (1 << 0)) != 0) errors.Add("220V power supply failure");

            return string.Join("; ", errors);
        }
    }
}