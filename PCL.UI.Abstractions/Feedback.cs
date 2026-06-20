// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.UI.Abstractions;

public enum HintSeverity
{
    Information,
    Success,
    Warning,
    Error
}

public interface IHintService
{
    void ShowInfo(string message);

    void ShowSuccess(string message);

    void ShowWarning(string message);

    void ShowError(string message);
}

public interface INotificationService
{
    Task ShowToastAsync(
        string title,
        string message,
        HintSeverity severity = HintSeverity.Information,
        CancellationToken cancellationToken = default);
}
