Классы для работы с TCP/IP

[[_TOC_]]

# Объекты модуля

- `Configuration` хранит настройки TCP-соединения.
- `Device` представляет TCP-соединение и управляет его жизненным циклом.
- `Builder` отвечает за создание и инициализацию объектов `Device`.

# Использование

Для создания `Device` необходимо создать экземпляр `Builder` и запустить его метод `RunWorkerAsync`, передав в него секцию конфигурации `IConfiguration config`, которая будет связана с классом `Configuration`. Результат создания будет доступен в событии `RunWorkerCompleted`.

Пример создания и открытия соединения:

```csharp
public class SampleClass
{
    /// <summary>
    /// Это соединение TCP которое будет использоваться
    /// классом
    /// </summary>
    private Device? tcpDevice;
    
    /// <summary>
    /// Журнал действий класса
    /// </summary>
    private readonly ILogger logger;

    /// <summary>
    /// Инициализация класса
    /// </summary>
    /// <param name="logger">Журнал действий</param>
    /// <param name="configuration">Конфигурация</param>
    public SampleClass(ILogger logger, IConfiguration configuration)
    {
        this.logger = logger;
        //Создаём обработчик создания и открытия соединения
        var builder = new MD.Aggregation.Devices.Tcp.Builder(logger);
        // Подписываемся на событие окончания работы
        builder.RunWorkerCompleted += Builder_RunWorkerCompleted;
        //Запускаем обработчик в работу
        builder.RunWorkerAsync(configuration);
    }

    /// <summary>
    /// Сюда приходит когда TCP соединение создано и открыто
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Builder_RunWorkerCompleted(object? sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
    {
        //Проверяем создалось ли устройство
        if (e.Result is Device tcp)
        {
            //Если создалось сохраняем его в классе
            tcpDevice = tcp;
            //И передаем в соединение журнал
            //базового класса
            tcpDevice.Logger = logger;
            //Вот примерно тут tcp соединение уже можно использовать.
        }
    }
}
```
