using Avalonia.Logging;
using PCL.Core.Logging;

namespace PCL.UI.Diagnostics;

internal sealed class BindingLogSink : ILogSink
{
    public bool IsEnabled(LogEventLevel level, string area)
    {
        return area == LogArea.Binding && level >= LogEventLevel.Warning;
    }

    public void Log(
        LogEventLevel level,
        string area,
        object source,
        string messageTemplate)
    {
        Log(level, area, source, messageTemplate, []);
    }

    public void Log(
        LogEventLevel level,
        string area,
        object source,
        string messageTemplate,
        object[] propertyValues)
    {
        if (!IsEnabled(level, area)) return;
        var sourceName = source?.GetType().Name ?? "unknown";
        var values = propertyValues is { Length: > 0 }
            ? $" [{string.Join(", ", propertyValues)}]"
            : string.Empty;
        LogWrapper.Warn("Avalonia.Binding", $"{sourceName}: {messageTemplate}{values}");
    }
}