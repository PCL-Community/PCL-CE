namespace PCL.Core.Logging;

public record LoggerConfiguration(
    string StoreFolder,
    long MaxFileSize = 32 * 1024 * 1024,
    string? FileNameFormat = null,
    bool AutoDeleteOldFile = true,
    int MaxKeepOldFile = 16,
#if TRACE
    LogLevel MinLogLevel = LogLevel.Trace
#else
    LogLevel MinLogLevel = LogLevel.Info
#endif
);
