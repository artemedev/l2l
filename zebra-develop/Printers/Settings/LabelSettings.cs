namespace MD.Aggregation.Devices.Printer.Settings;

public partial class LabelSettings
{
    public Start Start { get; set; } = new Start();

    public DataMatrix DataMatrix { get; set; } = new DataMatrix();

    public FieldData FieldData { get; set; } = new FieldData();

    public EntityType EntityType { get; set; } = new EntityType();
}
