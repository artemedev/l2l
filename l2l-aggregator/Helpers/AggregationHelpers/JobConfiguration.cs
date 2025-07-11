namespace l2l_aggregator.Helpers.AggregationHelpers
{
    public class JobConfiguration : MD.ServiceSettings
    {
        // Какая головка принтера используется
        // для печати этикеток
        public FastReport.Export.Zpl.ZplExport.ZplDensity ZplDensity { get; set; } = FastReport.Export.Zpl.ZplExport.ZplDensity.d6_dpmm_152_dpi;


        // Количество кодов
        // генерируемых за один проход
        public int QuantityPerPass { get; set; } = 100;
    }

}
