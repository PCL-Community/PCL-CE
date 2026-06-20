// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Desktop.Views.Dialogs;
using PCL.UI.Abstractions;

namespace PCL.Desktop.Services;

internal sealed class AvaloniaDialogService : IDialogService
{
    public async Task ShowMessageAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default)
    {
        await ShowDialogAsync(
                new DialogWindow(title, message, DialogMode.Message),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ConfirmAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default)
    {
        DialogResult result = await ShowDialogAsync(
                new DialogWindow(title, message, DialogMode.Confirm),
                cancellationToken)
            .ConfigureAwait(false);
        return result.Accepted;
    }

    public async Task<string?> PromptAsync(
        string title,
        string message,
        string? defaultValue = null,
        CancellationToken cancellationToken = default)
    {
        DialogResult result = await ShowDialogAsync(
                new DialogWindow(
                    title,
                    message,
                    DialogMode.Prompt,
                    defaultValue),
                cancellationToken)
            .ConfigureAwait(false);
        return result.Accepted ? result.Value : null;
    }

    private static Task<DialogResult> ShowDialogAsync(
        DialogWindow dialog,
        CancellationToken cancellationToken) =>
        AvaloniaUiTaskRunner.RunAsync(
            () => dialog.ShowDialog<DialogResult>(
                DesktopWindowProvider.GetRequiredMainWindow()),
            cancellationToken);
}
