// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Logging;
using PCL.Core.Logging;
using System.Globalization;

namespace PCL.Desktop.ViewModels.Log;

public sealed class LogLineViewModel(LauncherLogEntry entry)
{
    public PortableLogLevel Level => entry.Level;

    public string LevelText => entry.Level switch
    {
        PortableLogLevel.Trace => "跟踪",
        PortableLogLevel.Debug => "调试",
        PortableLogLevel.Info => "信息",
        PortableLogLevel.Warn => "警告",
        PortableLogLevel.Error => "错误",
        _ => entry.Level.ToString()
    };

    public string TimeText =>
        entry.Timestamp.ToLocalTime().ToString(
            "HH:mm:ss.fff",
            CultureInfo.InvariantCulture);

    public string Module => entry.Module;

    public string Message => entry.Message;

    public string? ExceptionText => entry.ExceptionText;

    public string DisplayText => entry.ToDisplayText();

    public bool HasException => !string.IsNullOrWhiteSpace(ExceptionText);

    public bool IsDebug =>
        Level is PortableLogLevel.Trace or PortableLogLevel.Debug;

    public bool IsWarning => Level == PortableLogLevel.Warn;

    public bool IsError => Level == PortableLogLevel.Error;
}
