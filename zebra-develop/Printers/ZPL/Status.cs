

using Microsoft.Extensions.Logging;
using System.Text;
using RM = MD.Aggregation.Devices.Printer.Resources.ZPL.Status;

namespace MD.Aggregation.Devices.Printer.ZPL;


public class Status(ILogger logger)
{
    #region Строка 1

    /// <summary>
    /// Настройки связи.
    /// Строка 1. Поле: aaa
    /// </summary>
    public CommunicationSettings? communicationSettings;

    /// <summary>
    /// Станица вышла.
    /// true - paper out
    /// Строка 1. Поле: b
    /// </summary>
    public bool PaperOutFlag = false;


    /// <summary>
    /// Пауза.
    /// true - активна
    /// Строка 1. Поле: с
    /// </summary>
    public bool PauseFlag { get; set; }

    /// <summary>
    /// Длина метки(количество точек).
    /// Строка 1. Поле: dddd
    /// </summary>
    public int LabelLength { get; set; }

    /// <summary>
    /// Количество форматов в буфере приема
    /// Строка 1. Поле: eee
    /// </summary>
    public int FormatsInBuffer { get; set; }

    /// <summary>
    /// Флаг заполнения буфера (1 = буфер приема заполнен)
    /// Строка 1. Поле: f
    /// </summary>
    public bool BufferFullFlag { get; set; }

    /// <summary>
    /// Флаг диагностического режима связи (1 = диагностический режим активен)
    /// Строка 1. Поле: g
    /// </summary>
    public bool DiagnosticModeFlag { get; set; }

    /// <summary>
    /// Флаг частичного форматирования (1 = выполняется частичное форматирование)
    /// Строка 1. Поле: h
    /// </summary>
    public bool PartialFormatFlag { get; set; }

    /// <summary>
    /// неиспользуемый (всегда 000)
    /// Строка 1. Поле: iii
    /// </summary>
    public string Unused1 { get; set; } = "000";

    /// <summary>
    /// Флаг повреждения оперативной памяти (1 = данные конфигурации потеряны)
    /// Строка 1. Поле: j
    /// </summary>
    public bool CorruptRamFlag { get; set; }

    /// <summary>
    /// Температурный диапазон (1 = пониженная температура)
    /// Строка 1. Поле: k
    /// </summary>
    public bool UnderTemperatureFlag { get; set; }

    /// <summary>
    /// Температурный диапазон (1 = перегрев)
    /// Строка 1. Поле: l
    /// </summary>
    public bool OverTemperatureFlag { get; set; }

    #endregion Строка 1

    #region Строка 2
    /// <summary>
    /// Настройки функций.
    /// Строка 2. Поле: mmm
    /// </summary>
    public int FunctionSettings { get; set; }   // TODO: выделить в отдельный класс

    /// <summary>
    /// Не используется.
    /// Строка 2. Поле: n
    /// </summary>
    public string Unused2 { get; set; } = string.Empty;

    /// <summary>
    /// Флаг поднятия головы (1 = голова в верхнем положении)
    /// Строка 2. Поле: o
    /// </summary>
    public bool HeadUpFlag { get; set; }

    /// <summary>
    /// Флаг выхода ленты (1 = выход ленты)
    /// Строка 2. Поле: p
    /// </summary>
    public bool RibbonOutFlag { get; set; }

    /// <summary>
    /// Флаг режима термопереноса (1 = выбран режим термопереноса)
    /// Строка 2. Поле: q
    /// </summary>
    public bool ThermalTransferModeFlag { get; set; }

    /// <summary>
    /// Режим печати: 0 = Перемотка, 1 = Отделение, 2 = Отрыв, 3 = Нарезка, 4 = Аппликатор.
    /// Строка 2. Поле: r
    /// </summary>
    public PrintModeEnum PrintMode { get; set; }  // TODO: вытащить в enum

    /// <summary>
    /// Режим ширины печати
    /// Строка 2. Поле: s
    /// </summary>
    public int PrintWidthMode { get; set; }

    /// <summary>
    /// Флаг ожидания этикетки (1 = ожидание этикетки в режиме отклеивания)
    /// Строка 2. Поле: t
    /// </summary>
    public bool LabelWaitingFlag { get; set; }

    /// <summary>
    /// Этикетки, оставшиеся в партии
    /// Строка 2. Поле: uuuuuuuu
    /// </summary>
    public int LabelsRemainingInBatch { get; set; }

    /// <summary>
    /// Флаг формата при печати (всегда 1)
    /// Строка 2. Поле: v
    /// </summary>
    public bool FormatWhilePrintingFlag { get; set; }

    /// <summary>
    /// Количество графических изображений, хранящихся в памяти
    /// Строка 2. Поле: www
    /// </summary>
    public int NumberOfGraphicImages { get; set; }

    #endregion Строка 2

    #region Строка 3
    /// <summary>
    /// Пароль.
    /// Строка 3. Поле: xxxx
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Флаг установки статической RAM (0 = не установлена, 1 = установлена).
    /// Строка 3. Поле: y
    /// </summary>
    public bool IsStaticRamInstalled { get; set; }

    #endregion Строка 3

    /// <summary>
    /// Возвращает true если принтер готов к работе.
    /// </summary>
    /// <param name="byteBuf">полученный от принтера набор байт с состоянием</param>
    /// <returns>true - если принтер готов к работе</returns>
    public bool IsOk(byte[] byteBuf)
    {
        if (!Parsing(byteBuf))
        {
            return false;
        }
        if (PaperOutFlag)
            return false;
        if (CorruptRamFlag)
            return false;
        if (UnderTemperatureFlag)
        {
            return false;
        }
        if (OverTemperatureFlag)
        {
            return false;
        }
        if (RibbonOutFlag)
            return false;
        return true;
    }


    /// <summary>
    /// Парсинг ответа от принтера (запрос состояния)
    /// </summary>
    /// <param name="byteBuf">полученный от принтера набор байт с состоянием</param>
    /// <returns>true - если принтер готов к работе</returns>
    private bool Parsing(byte[] byteBuf)
    {
        try
        {
            if (byteBuf == null)
            {
                logger.LogError(RM.ErrByteBufNull);
                return false;
            }

            string result = Encoding.UTF8.GetString(byteBuf).Trim().TrimEnd('\0');
            string[] lines = result.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length != 4)
            {
                logger.LogError(RM.ErrLinesLen, lines.Length);
                return false;
            }

            if (!ParsingString1(lines[0]))
            {
                logger.LogError(RM.ErrParsingString1);
            }

            logger.LogDebug(RM.DbgParsingString1);

            if (!ParsingString2(lines[1]))
            {
                logger.LogError(RM.ErrParsingString2);
            }

            logger.LogDebug(RM.DbgParsingString2);

            if (!ParsingString3(lines[2]))
            {
                logger.LogError(RM.ErrParsingString3);
            }

            logger.LogDebug(RM.DbgParsingString3);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, RM.ExpParsing, ex.Message);
            return false;
        }
    }


    /// <summary>
    /// Парсинг строки 1
    /// </summary>
    /// <param name="str">строка с набором значений</param>
    /// <returns></returns>
    private bool ParsingString1(String str)
    {
        if (!(str.StartsWith('\u0002') && str.EndsWith('\u0003')))
        {
            logger.LogError(RM.ErrStartEnd1);
            return false;
        }

        string s = str[1..^1];

        string[] values = s.Split(',');
        if (values.Length != 12) 
        { 
            logger.LogError(RM.ErrParamLen1, values.Length);
            return false;
        }

        //CommunicationSettings communicationSettings = new CommunicationSettings();
        //communicationSettings.Parsing(values[0]);

        PaperOutFlag = values[1] == "1";
        logger.LogDebug(RM.DbgPaperOutFlag, PaperOutFlag);

        PauseFlag = values[2] == "1";
        logger.LogDebug(RM.DbgPauseFlag, PauseFlag);

        if (int.TryParse(values[3], out int labelLength))
        {
            LabelLength = labelLength;
            logger.LogDebug(RM.DbgLabelLength, LabelLength);
        }
        else
        {
            logger.LogError(RM.ErrLabelLength, values[3]);
            return false;
        }

        if (int.TryParse(values[4], out int formatsInBuffer))
        {
            FormatsInBuffer = formatsInBuffer;
            logger.LogDebug(RM.DbgFormatsInBuffer, FormatsInBuffer);
        }
        else
        {
            logger.LogError(RM.ErrFormatsInBuffer, values[4]);
            return false;
        }

        BufferFullFlag = values[5] == "1";
        logger.LogDebug(RM.DbgBufferFullFlag, BufferFullFlag);

        DiagnosticModeFlag = values[6] == "1";
        logger.LogDebug(RM.DbgDiagnosticModeFlag, DiagnosticModeFlag);

        PartialFormatFlag = values[7] == "1";
        logger.LogDebug(RM.DbgPartialFormatFlag, PartialFormatFlag);

        //public string Unused1 { get; set; }[8]

        CorruptRamFlag = values[9] == "1";
        logger.LogDebug(RM.DbgCorruptRamFlag, CorruptRamFlag);

        UnderTemperatureFlag = values[10] == "1";
        logger.LogDebug(RM.DbgUnderTemperatureFlag, UnderTemperatureFlag);

        OverTemperatureFlag = values[11] == "1";
        logger.LogDebug(RM.DbgOverTemperatureFlag, OverTemperatureFlag);

        return true;
    }

    /// <summary>
    /// Парсинг строки 2
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    private bool ParsingString2(String str)
    {
        if (!(str.StartsWith('\u0002') && str.EndsWith('\u0003')))
        {
            logger.LogError(RM.ErrStartEnd2);
            return false;
        }

        string s = str[1..^1];

        string[] values = s.Split(',');
        if (values.Length != 11)
        {
            logger.LogError(RM.ErrParamLen2, values.Length);
            return false;
        }

        // FunctionSettings [0] - обработку выделить в отдельный класс

        // Поле: n [1] - не используется

        HeadUpFlag = values[2] == "1";
        logger.LogDebug(RM.DbfHeadUpFlag, HeadUpFlag);

        RibbonOutFlag = values[3] == "1";
        logger.LogDebug(RM.DbfRibbonOutFlag, RibbonOutFlag);

        ThermalTransferModeFlag = values[4] == "1";
        logger.LogDebug(RM.DbfThermalTransferModeFlag, ThermalTransferModeFlag);

        if (int.TryParse(values[5], out int printMode))
        {
            if (!Enum.IsDefined(typeof(PrintModeEnum), printMode))
            { 
                logger.LogError(RM.ErrPrintModeEnum, printMode);
                return false;
            }

            PrintMode = (PrintModeEnum)printMode;
            logger.LogDebug(RM.DbgPrintMode, PrintMode);
        }
        else
        {
            logger.LogError(RM.ErrPrintMode, values[5]);
            return false;
        }

        if (int.TryParse(values[6], out int printWidthMode))
        {
            PrintWidthMode = printWidthMode;
            logger.LogDebug(RM.DbgPrintWidthMode, PrintWidthMode);
        }
        else
        {
            logger.LogError(RM.ErrPrintWidthMode, values[6]);
            return false;
        }

        LabelWaitingFlag = values[7] == "1";
        logger.LogDebug(RM.DbfLabelWaitingFlag, LabelWaitingFlag);

        if (int.TryParse(values[8], out int labelsRemainingInBatch))
        {
            LabelsRemainingInBatch = labelsRemainingInBatch;
            logger.LogDebug(RM.DbgLabelsRemainingInBatch, LabelsRemainingInBatch);
        }
        else
        {
            logger.LogError(RM.ErrLabelsRemainingInBatch, values[8]);
            return false;
        }

        FormatWhilePrintingFlag = values[9] == "1";
        logger.LogDebug(RM.DbfFormatWhilePrintingFlag, FormatWhilePrintingFlag);

        if (int.TryParse(values[10], out int numberOfGraphicImages))
        {
            NumberOfGraphicImages = numberOfGraphicImages;
            logger.LogDebug(RM.DbgNumberOfGraphicImages, NumberOfGraphicImages);
        }
        else
        {
            logger.LogError(RM.ErrNumberOfGraphicImages, values[10]);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Парсинг строки 3
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    private bool ParsingString3(String str)
    {
        if (!(str.StartsWith('\u0002') && str.EndsWith('\u0003')))
        {
            logger.LogError(RM.ErrStartEnd3);
            return false;
        }

        string s = str[1..^1];

        string[] values = s.Split(',');
        if (values.Length != 2)
        {
            logger.LogError(RM.ErrParamLen3, values.Length);
            return false;
        }

        Password = values[0];
        logger.LogDebug(RM.DbgPassword, Password);

        IsStaticRamInstalled = values[1] == "1";
        logger.LogDebug(RM.DbgIsStaticRamInstalled, IsStaticRamInstalled);

        return true;
    }
}
