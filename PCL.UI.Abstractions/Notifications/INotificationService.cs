// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.UI.Abstractions.Notifications;

public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Error
}

public sealed record NotificationRequest
{
    public required string Title { get; init; }

    public string? Message { get; init; }

    public NotificationSeverity Severity { get; init; }

    public TimeSpan? Duration { get; init; }
}

public interface INotificationService
{
    ValueTask ShowAsync(
        NotificationRequest request,
        CancellationToken cancellationToken);
}
