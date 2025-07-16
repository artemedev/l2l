using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MD.Aggregation.Devices.Printer.ZPL;

/// <summary>
/// Класс настройки печати
/// </summary>
public class ImagePrint
{
    /// <summary>
    /// Режим печати из файлов
    /// включен
    /// </summary>
    public bool Enable { get; set; } = false;

    /// <summary>
    /// Папка где искать файлы изображений
    /// </summary>
    public string Path {  get; set; } = "Images";

    /// <summary>
    /// Расширение файлов
    /// </summary>
    public string Extension { get; set; } = "zpl";

    /// <summary>
    /// По какому ключу шукаем данные для поиска файла
    /// </summary>
    public string Key { get; set; } = "UNID";
}
