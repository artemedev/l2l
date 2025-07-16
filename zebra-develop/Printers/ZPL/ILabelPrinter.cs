using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MD.Aggregation.Devices.Printer.ZPL;

/// <summary>
/// Интерфейс принтера
/// для печати
/// </summary>
public interface ILabelPrinter : Devices.Printer.IService
{
    /// <summary>
    /// Отправить этикетку на печать
    /// </summary>
    /// <param name="label">Этикетка для печати</param>
    /// <returns>Истина если отправка прошла успешно</returns>
    public bool SendLabel(string label);
}
