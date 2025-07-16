using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace MD.Aggregation.Devices.Printer;

/// <summary>
/// Тестируем обмен с принтером
/// </summary>
/// <param name="output"></param>
public class PrinterTest(ITestOutputHelper output) : TestBase(output)
{

    private IConfiguration config
    {
        get
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddJsonFile("config.json");
            return configBuilder.Build();
        }
    }

    /// <summary>
    /// Тестируем как упадет соединение
    /// </summary>
    [Fact]
    public async Task OpenTcp()
    {
        var logger = LoggerFactory.CreateLogger("TEST");
        logger.LogInformation("Начинаем тест");
        var printer = new ZPL.Printer("dima", LoggerFactory.CreateLogger("dima"));
        printer.StatusReceived += Printer_StatusReceived;
        printer.Configure(config.GetSection("Printer"));
        await Task.Run(() => { while (!endTest) { } });
        printer.Release();

    }

    private void Printer_StatusReceived(object? sender, DeviceStatusEventArgs e)
    {
        endTest = true;
    }

    private bool endTest = false;

    [Fact]
    public async Task PrintCode()
    {
        var logger = LoggerFactory.CreateLogger("TEST");
        logger.LogInformation("Начинаем тест");
        var printer = new ZPL.Printer("testPrinter", LoggerFactory.CreateLogger("Зёбра"));
        printer.StatusReceived += Printer_StatusReceived;
        endTest = false;
        printer.Configure(config.GetSection("Printer"));
        await Task.Run(() => { while (!endTest) { } });
        printer.StartWork();
        printer.CodeTemplate = GetTestTemplate();
        long gtin = 04607045192170;
        printer.JobInfo.Add("GTIN", gtin.ToString("D14"));
        long currentSn = 091737914773;
        long startCode = 9172000000000001;
        for (int i=0;i<1;i++)
        {
            
            var code = new Aggregation.Marking.UN.CodeInfo
            {
                { nameSn, currentSn.ToString("D13") },
                { nameGS91, "xxxx" },
                { nameGS92, "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx" },
                {nameUnId,startCode.ToString()}
            };
            if (printer.SendToBuffer(code))
                logger.LogInformation("Код отправлен на печать");
            else
                logger.LogError("Не смогли отправить код на печать.");
            startCode++;
        }
        printer.Release();
        logger.LogInformation("Тест завершен");
    }
    private const string nameUnId = "UNID";
    private const string nameSn = "SN";
    private const string nameGS91 = "GS91";
    private const string nameGS92 = "GS92";

    // <summary>
    /// Создать тестовое правило сборки кода
    /// </summary>
    /// <returns></returns>
    public MD.Aggregation.Marking.UN.Template GetTestTemplate()
    {
        var temp = new MD.Aggregation.Marking.UN.Template
        {
            new ()
            {
                Index = 1,
                EntityType = Aggregation.Marking.UN.TemplateEntityType.ConstChar,
                Entity = "01",
                Length = 2
            },
            new()
            {
                Index = 2,
                EntityType = Aggregation.Marking.UN.TemplateEntityType.ResourceInfo,
                Entity = "GTIN",
                Length = 14
            },
            new ()
            {
                Index = 3,
                EntityType = Aggregation.Marking.UN.TemplateEntityType.ConstChar,
                Entity = "21",
                Length = 2,
            },
            new ()
            {
                Index = 4,
                EntityType = Aggregation.Marking.UN.TemplateEntityType.SerialNumber,
                Entity = "SN",
                Length = 13
            },
            new ()
            {
                Index = 5,
                EntityType = Aggregation.Marking.UN.TemplateEntityType.GS91,
                Entity = "GS91",
                Length = 4,
            },
            new ()
            {
                Index = 6,
                EntityType = Aggregation.Marking.UN.TemplateEntityType.GS92,
                Entity = "GS92",
                Length = 44
            }
        };
        return temp;
    }

    /// <summary>
    /// Тестируем генерацию управляющей строки
    /// </summary>
    [Fact]
    public async Task GetPreGenerationString()
    {
        var logger = LoggerFactory.CreateLogger("TEST");
        logger.LogInformation("Начинаем тест");
        var printer = new ZPL.Printer("dima", LoggerFactory.CreateLogger("dima"));
        printer.StatusReceived += Printer_StatusReceived;
        printer.Configure(config.GetSection("Printer"));
        await Task.Run(() => { while (!endTest) { } });
        logger.LogInformation(printer.PreGenerationString);
        printer.Release();

    }

}
