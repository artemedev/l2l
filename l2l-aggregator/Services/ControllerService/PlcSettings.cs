namespace l2l_aggregator.Services.ControllerService
{
    public class PlcSettings
    {
        // Основные уставки позиционирования
        public ushort MoveFromZero { get; set; }
        public ushort ZeroTimeout { get; set; }
        public ushort EstimatedZeroHome { get; set; }
        public bool DirectionChange { get; set; }
        public ushort MoveSpeed { get; set; }
        public ushort MinDistanceCameraToTable { get; set; }

        // Подсветка
        public ushort LightIntensity { get; set; }
        public ushort LightDelay { get; set; }
        public ushort LightDuration { get; set; }

        // Камера
        public ushort TriggerDelay { get; set; }
        public ushort LightMode { get; set; }

        // Обмен
        public ushort PcTimeout { get; set; }
    }
}
