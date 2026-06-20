// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.UI.Abstractions;

public sealed record FileDialogFilter(
    string Name,
    IReadOnlyList<string> Patterns);

public interface IFileDialogService
{
    Task<string?> PickSaveFileAsync(
        string title,
        string suggestedFileName,
        IReadOnlyList<FileDialogFilter> filters,
        CancellationToken cancellationToken = default);

    Task<string?> PickOpenFileAsync(
        string title,
        IReadOnlyList<FileDialogFilter> filters,
        CancellationToken cancellationToken = default);
}

public interface IDialogService
{
    Task ShowMessageAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default);

    Task<bool> ConfirmAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default);

    Task<string?> PromptAsync(
        string title,
        string message,
        string? defaultValue = null,
        CancellationToken cancellationToken = default);
}
