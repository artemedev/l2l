using MD.Aggregation.Devices.Tcp;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
