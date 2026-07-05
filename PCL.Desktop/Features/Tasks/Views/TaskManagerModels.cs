// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;

namespace PCL.Desktop.Features.Tasks.Views;

public enum TaskManagerTaskState
{
    Waiting,
    Running,
    Finished,
    Failed,
    Canceled
}

public sealed record TaskManagerEntrySnapshot(
    string TaskId,
    string Title,
    string Stage,
    string Detail,
    double Progress,
    int CompletedFiles,
    int TotalFiles,
    long SpeedBytesPerSecond,
    TaskManagerTaskState State,
    string? ErrorMessage = null);

public sealed record TaskManagerSummary(
    double Progress,
    long SpeedBytesPerSecond,
    int RemainingFiles,
    int ActiveThreads,
    int ThreadLimit);

public sealed class TaskManagerTaskEventArgs(string taskId) : EventArgs
{
    public string TaskId { get; } = taskId;
}

internal static class TaskManagerFormatting
{
    public static string Percent(double value, bool twoDecimals = false)
    {
        value = Math.Clamp(value, 0d, 1d);
        if (value >= 0.999999d)
            return "100%";

        return value.ToString(twoDecimals ? "P2" : "P0", CultureInfo.CurrentCulture);
    }

    public static string Speed(long bytesPerSecond)
    {
        if (bytesPerSecond <= 0)
            return "0 B/s";

        double value = bytesPerSecond;
        string[] units = ["B/s", "KB/s", "MB/s", "GB/s"];
        int unit = 0;
        while (value >= 1024d && unit < units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }

        return value.ToString(unit == 0 ? "F0" : "F1", CultureInfo.CurrentCulture) + " " + units[unit];
    }
}
