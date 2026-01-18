namespace PCL.Core.Logging;

public record LoggerConfiguration(
    string StoreFolder,
    LoggerSegmentMode SegmentMode = LoggerSegmentMode.BySize,
    long MaxFileSize = 5 * 1024 * 1024,
    string? FileNameFormat = null,
    bool AutoDeleteOldFile = true,
    int MaxKeepOldFile = 10
);
