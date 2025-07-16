using MD.Aggregation.Marking.UN;
using Microsoft.Extensions.Logging;
using System.Text;
using RM = MD.Aggregation.Devices.Printer.Resources.Printer;

namespace MD.Aggregation.Devices.Printer;

public abstract class Printer(string name, ILogger logger) : 
    Device(name, logger), MD.Aggregation.Devices.Printer.IService
{
    /// <inheritdoc/>
    public abstract bool SendToBuffer(CodeInfo codeInfo);

    /// <inheritdoc/>
    public override FunctionalPurpose Purpose => FunctionalPurpose.Printer;

    /// <inheritdoc/>
    public Dictionary<string, string> JobInfo { get; set; } = [];

    /// <inheritdoc/>
    public Template? CodeTemplate { get; set; }
    
    /// <inheritdoc/>
    public virtual int BufferSize => bufferSize;

    /// <summary>
    /// Счетчик для расчетного определения размера буфера
    /// </summary>
    protected int bufferSize = 0;

    /// <summary>
    /// Преобразовать информацию о печатаемом коде
    /// в строку доступную для отправки на принтер.
    /// </summary>
    /// <param name="codeInfo">Информация о коде</param>
    /// <returns></returns>
    protected virtual string? GetCodeString(CodeInfo codeInfo)
    {
        if (CodeTemplate != null)
        {
            var sb = new StringBuilder();
            PreGenerateString(sb);
            foreach (var item in CodeTemplate)
            {
                switch (item.EntityType)
                {
                    case TemplateEntityType.ConstChar:
                        PrintConstChar(item, sb);
                        break;
                    case TemplateEntityType.GS91:
                        PrintGS(item, codeInfo, sb);
                        break;
                    case TemplateEntityType.GS92:
                        PrintGS(item, codeInfo, sb);
                        break;
                    case TemplateEntityType.GS93:
                        PrintGS(item, codeInfo, sb);
                        break;
                    case TemplateEntityType.SerialNumber:
                        PrintSerial(item, codeInfo, sb);
                        break;
                    case TemplateEntityType.ResourceInfo:
                        PrintResourceInfo(item, sb);
                        break;
                }
            }
            PostGenerateString(sb);
            return sb.ToString();
        }
        else
        {
            Logger.LogError(RM.ErrNoTemplate);
        }
        return null;
    }

    /// <summary>
    /// Добавить в строку кода специальные
    /// символы в конец строки.
    /// Т.е. какие символы должны быть в конце строки,
    /// определяется моделью принтера
    /// </summary>
    /// <param name="sb">Строитель строк в который нужно добавлять данные</param>
    protected virtual void PostGenerateString(StringBuilder sb) { }

    /// <summary>
    /// Добавить в строку кода специальные
    /// символы перед отправкой кода.
    /// Т.е. какие символы должны быть в начале строки,
    /// определяется моделью принтера
    /// </summary>
    /// <param name="sb">Строитель строк в который нужно добавлять данные</param>
    protected virtual void PreGenerateString(StringBuilder sb) { }

    /// <summary>
    /// Добавить в код серийный номер
    /// </summary>
    /// <param name="item">Элемент для печати</param>
    /// <param name="codeInfo">Информация о коде</param>
    /// <param name="sb">Строитель строк в который нужно добавлять данные</param>
    protected virtual void PrintResourceInfo(TemplateEntity item, StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(item.Entity))
        {
            var rk = JobInfo[item.Entity];
            if (!string.IsNullOrEmpty(rk))
            {
                sb.Append(rk);
            }
            else
            {
                Logger.LogError(RM.ErrNoResourceCode, item.Index, item.Entity);
            }
        }
        else
        {
            Logger.LogError(RM.ErrNoResourceName, item.Index);
        }
    }

    /// <summary>
    /// Добавить в код серийный номер
    /// </summary>
    /// <param name="item">Элемент для печати</param>
    /// <param name="codeInfo">Информация о коде</param>
    /// <param name="sb">Строитель строк в который нужно добавлять данные</param>
    protected virtual void PrintSerial(TemplateEntity item, CodeInfo codeInfo, StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(item.Entity))
        {
            var sn = codeInfo[item.Entity];
            if (!string.IsNullOrEmpty(sn))
            {
                sb.Append(sn);
            }
            else
            {
                Logger.LogError(RM.ErrNoSerialCode, item.Index, item.Entity);
            }
        }
        else
        {
            Logger.LogError(RM.ErrNoSerialName, item.Index);
        }
    }

    /// <summary>
    /// Добавить поле GS
    /// </summary>
    /// <param name="item">Элемент для печати</param>
    /// <param name="codeInfo">Информация о коде</param>
    /// <param name="sb">Строитель строк в который нужно добавлять данные</param>
    protected virtual void PrintGS(TemplateEntity item, CodeInfo codeInfo, StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(item.Entity))
        {
            var gs = codeInfo[item.Entity];
            if (!string.IsNullOrEmpty(gs))
            {
                sb.Append(GetSymbolGS(item.EntityType));
                sb.Append(gs);
            }
            else
            {
                Logger.LogError(RM.ErrNoGSName, item.EntityType, item.Index, item.Entity);
            }
        }
        else
        {
            Logger.LogError(RM.ErrNoGSName, item.EntityType, item.Index);
        }
    }

    /// <summary>
    /// Добавить в принтер константу
    /// </summary>
    /// <param name="item">Элемент который добавляется в код</param>
    /// <param name="sb">Строитель строк в который нужно добавлять данные</param>
    protected virtual void PrintConstChar(TemplateEntity item, StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(item.Entity))
        {
            sb.Append(item.Entity);
        }
        else
        {
            Logger.LogWarning(RM.WarnNoConstantChar, item.Index);
        }
    }


    /// <summary>
    /// Символ GS по умолчанию
    /// </summary>
    public const char DefaultSymbolGS = (char)0x1d;

    /// <summary>
    /// Получить префикс для кода GS
    /// </summary>
    protected virtual string GetSymbolGS(TemplateEntityType type)
    {
        return type switch
        {
            TemplateEntityType.GS92 => $"{DefaultSymbolGS}92",
            TemplateEntityType.GS93 => $"{DefaultSymbolGS}93",
            TemplateEntityType.GS91 => $"{DefaultSymbolGS}91",
            _ => $"{DefaultSymbolGS}",
        };
    }

    /// <inheritdoc/>
    public void Confirm(int count = 1)
    {
        if (count<0)
        {
            bufferSize = 0;
        }
        else
        {
            bufferSize -= count;
            if (bufferSize < 0)
                bufferSize = 0;
        }
        
    }
}
