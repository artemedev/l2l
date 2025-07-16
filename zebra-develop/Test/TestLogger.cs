using Microsoft.Extensions.Logging;
using System.Text;

namespace MD;

public class TestLogger (string Category): ILogger
{
    public string? SourceName = Category;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        throw new NotImplementedException();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public event EventHandler<string>? LogMessage;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var sb = new StringBuilder();
        if (SourceName != null)
        {
            sb.Append($"[{SourceName}]");
        }
        else
        {
            sb.Append("\t");
        }
        sb.Append($"\t[{logLevel}] ");
        if (eventId.Id>0)
        {
            sb.Append($"[{eventId.Id}]");
        }
        sb.Append(state?.ToString());
        LogMessage?.Invoke(this, sb.ToString());
    }

}
