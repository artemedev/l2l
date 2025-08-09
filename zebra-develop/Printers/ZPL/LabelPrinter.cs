using Microsoft.Extensions.Logging;

namespace MD.Aggregation.Devices.Printer.ZPL;

/// <summary>
/// Принтер для печати в режиме готовых этикеток
/// </summary>
/// <param name="name"></param>
/// <param name="logger"></param>
public class LabelPrinter(string name, ILogger logger) : Printer(name, logger), ILabelPrinter
{
    /// <inheritdoc/>
    public bool SendLabel(string label)
    {
        return SendToBuffer(label);
    }
}
