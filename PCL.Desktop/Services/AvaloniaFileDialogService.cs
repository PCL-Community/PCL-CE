// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Platform.Storage;
using PCL.UI.Abstractions;

namespace PCL.Desktop.Services;

internal sealed class AvaloniaFileDialogService : IFileDialogService
{
    public Task<string?> PickSaveFileAsync(
        string title,
        string suggestedFileName,
        IReadOnlyList<FileDialogFilter> filters,
        CancellationToken cancellationToken = default) =>
        AvaloniaUiTaskRunner.RunAsync(
            async () =>
            {
                IStorageFile? file = await DesktopWindowProvider
                    .GetRequiredMainWindow()
                    .StorageProvider
                    .SaveFilePickerAsync(
                        new FilePickerSaveOptions
                        {
                            Title = title,
                            SuggestedFileName = suggestedFileName,
                            FileTypeChoices = CreateFileTypes(filters),
                            ShowOverwritePrompt = true
                        });
                return file?.TryGetLocalPath();
            },
            cancellationToken);

    public Task<string?> PickOpenFileAsync(
        string title,
        IReadOnlyList<FileDialogFilter> filters,
        CancellationToken cancellationToken = default) =>
        AvaloniaUiTaskRunner.RunAsync(
            async () =>
            {
                IReadOnlyList<IStorageFile> files = await DesktopWindowProvider
                    .GetRequiredMainWindow()
                    .StorageProvider
                    .OpenFilePickerAsync(
                        new FilePickerOpenOptions
                        {
                            Title = title,
                            AllowMultiple = false,
                            FileTypeFilter = CreateFileTypes(filters)
                        });
                return files.Count == 0
                    ? null
                    : files[0].TryGetLocalPath();
            },
            cancellationToken);

    private static FilePickerFileType[] CreateFileTypes(
        IReadOnlyList<FileDialogFilter> filters) =>
        filters
            .Select(
                static filter =>
                    new FilePickerFileType(filter.Name)
                    {
                        Patterns = filter.Patterns
                    })
            .ToArray();
}
