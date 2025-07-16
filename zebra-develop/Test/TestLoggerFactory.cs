using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace MD;
public class TestLoggerFactory (ITestOutputHelper output) : ILoggerFactory
{
    private DateTime StartTime = DateTime.Now;

    public void AddProvider(ILoggerProvider provider)
    {
        throw new NotImplementedException();
    }

    public ILogger CreateLogger(string categoryName)
    {
        var result = new TestLogger(categoryName);
        result.LogMessage += Result_LogMessage;
        return result;
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    private void Result_LogMessage(object? sender, string e)
    {
        output.WriteLine($"{DateTime.Now.Subtract(StartTime)}{e}");
    }
}
