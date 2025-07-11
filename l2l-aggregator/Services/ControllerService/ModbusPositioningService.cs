using NModbus;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace l2l_aggregator.Services.ControllerService
{
    public class ModbusPositioningService
    {
        private readonly string _controllerIp;
        private readonly float _packHeight;
        private readonly byte _slaveId = 1;

        public ModbusPositioningService(string controllerIp, float packHeight)
        {
            _controllerIp = controllerIp;
            _packHeight = packHeight;
        }
        public async Task<bool> CheckMutualConnectionAsync()
        {
            //try
            //{
            //    using var client = new TcpClient(_controllerIp, 502);
            //    var factory = new ModbusFactory();
            //    var master = factory.CreateMaster(client);

            //    // Установить бит 4x0.12 (включение режима пинг-понг)
            //    await master.WriteSingleCoilAsync(_slaveId, 12, true);

            //    // Сбросить бит активности ПК 4x0.10 с задержкой не более 1500 мс
            //    await Task.Delay(1000);
            //    await master.WriteSingleCoilAsync(_slaveId, 10, false);

            //    // Проверка ответа от ПЛК: 3x0.11 (ответ от ПЛК)
            //    var input = await master.ReadInputsAsync(_slaveId, 11, 1);
            //    return input[0]; // true = ответ есть
            //}
            //catch
            //{
            //    return false;
            //}
            try
            {
                using var client = new TcpClient(_controllerIp, 502);
                var factory = new ModbusFactory();
                var master = factory.CreateMaster(client);

                // 1. Включить пинг-понг: 4x0.12 = true
                await master.WriteSingleCoilAsync(_slaveId, 12, true);

                // 2. Включить контроль активности ПК: бит 12 в 4x0
                var reg0 = await master.ReadHoldingRegistersAsync(_slaveId, 0, 1);
                ushort regValue = (ushort)(reg0[0] | (1 << 12));
                await master.WriteSingleRegisterAsync(_slaveId, 0, regValue);

                // 3. Прочитать тайм-аут из 4x17
                var timeoutReg = await master.ReadHoldingRegistersAsync(_slaveId, 16, 1);
                ushort timeoutMs = timeoutReg[0];
                if (timeoutMs == 0 || timeoutMs > 10000)
                    timeoutMs = 3000;

                // 4. Подождать немного
                await Task.Delay(Math.Min(timeoutMs / 3, 1000));

                // 5. Сбросить бит активности ПК (бит 10)
                reg0 = await master.ReadHoldingRegistersAsync(_slaveId, 0, 1);
                regValue = (ushort)(reg0[0] & ~(1 << 10));
                await master.WriteSingleRegisterAsync(_slaveId, 0, regValue);

                // 6. Проверка: ПЛК ответил (бит 3x0.11)
                var pong = await master.ReadInputsAsync(_slaveId, 11, 1);
                if (!pong[0])
                    return false;

                // 7. Проверка: ПЛК считает ПК активным (бит 13)
                var pcStatus = await master.ReadInputsAsync(_slaveId, 0, 14); // читаем до бита 13
                bool pcIsActive = pcStatus[13];
                if (!pcIsActive)
                    return false;

                // 8. Установить флаг активности ПЛК (бит 11 в 4x0)
                reg0 = await master.ReadHoldingRegistersAsync(_slaveId, 0, 1);
                regValue = (ushort)(reg0[0] | (1 << 11));
                await master.WriteSingleRegisterAsync(_slaveId, 0, regValue);

                return true;
            }
            catch
            {
                return false;
            }
        }
        public async Task SetLayerAsync(int layerIndex)
        {
            float height = _packHeight * layerIndex;
            ushort heightValue = ConvertHeightToPlcValue(height);

            using var client = new TcpClient(_controllerIp, 502);
            var factory = new ModbusFactory();
            var master = factory.CreateMaster(client);

            // 1. Установить уставку: допустим, в регистр 4x1
            await master.WriteSingleRegisterAsync(_slaveId, 1, heightValue);

            // 2. Установить бит "Принудительное позиционирование от ПК" — 4x0.0
            await master.WriteSingleCoilAsync(_slaveId, 0, true);

            // 3. Дождаться запроса на позиционирование от ПЛК — 3x0.0
            while (true)
            {
                var inputs = await master.ReadInputsAsync(_slaveId, 0, 1);
                if (inputs[0]) break;
                await Task.Delay(50);
            }

            // 4. Сбросить бит — 4x0.0
            await master.WriteSingleCoilAsync(_slaveId, 0, false);

            // 5. Установить бит разрешения позиционирования — 4x0.1
            await master.WriteSingleCoilAsync(_slaveId, 1, true);

            // 6. Дождаться подтверждения от ПЛК — 3x0.2
            while (true)
            {
                var inputs = await master.ReadInputsAsync(_slaveId, 2, 1);
                if (inputs[0]) break;
                await Task.Delay(50);
            }

            // 7. Сбросить бит разрешения — 4x0.1
            await master.WriteSingleCoilAsync(_slaveId, 1, false);
        }

        private ushort ConvertHeightToPlcValue(float heightMm)
        {
            return (ushort)(heightMm); // если ПЛК принимает *10
        }
    }

}
