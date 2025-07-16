using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace MD;

public class TestBase(ITestOutputHelper output)
{
    private TestLoggerFactory loggerFactory = new(output);
    /// <summary>
    /// Фабрика тестовых логов
    /// </summary>
    public ILoggerFactory LoggerFactory => loggerFactory;

    /// <summary>
    /// Поток для вывода в консоль
    /// </summary>
    public ITestOutputHelper OutputHelper => output;

    /// <summary>
    /// Конкретная конфигурация, создается при необходимости
    /// </summary>
    private IConfiguration? config;

    /// <summary>
    /// Интерфейс доступа к конфигурации тестов
    /// </summary>
    public IConfiguration Config
    {
        get
        {
            if (config == null)
            {
                var configurationBuilder = new ConfigurationBuilder();
                var source = new Microsoft.Extensions.Configuration.Json.JsonConfigurationSource()
                {
                    Path = "config.json"
                };
                configurationBuilder.Add(source);
                config = configurationBuilder.Build();
            }
            return config;
        }
    }

    /// <summary>
    /// Получить строку подключения
    /// </summary>
    /// <param name="dataBaseName">Имя базы данных</param>
    /// <returns></returns>
    public string? GetConnectionString(string dataBaseName)
    {
        return Config.GetConnectionString(dataBaseName);
    }


    
}
