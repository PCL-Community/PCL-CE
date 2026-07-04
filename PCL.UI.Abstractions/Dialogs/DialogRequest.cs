// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.UI.Abstractions.Dialogs;

public enum DialogSeverity
{
    Info,
    Warning,
    Error,
    Success
}

public sealed record DialogRequest
{
    public required string Title { get; init; }

    public required string Message { get; init; }

    public string PrimaryButton { get; init; } = "确定";

    public string? SecondaryButton { get; init; }

    public DialogSeverity Severity { get; init; }
}

public sealed record DialogResult(bool IsPrimaryAction);

public interface IDialogService
{
    ValueTask<DialogResult> ShowAsync(
        DialogRequest request,
        CancellationToken cancellationToken);
}
