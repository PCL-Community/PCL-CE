// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Logging;
using PCL.Core.Logging;
using PCL.Desktop.ViewModels.Log;
using PCL.UI.Abstractions;

namespace PCL.Desktop.Test;

[TestClass]
public sealed class LogPageViewModelTests
{
    [TestMethod]
    public void ViewModel_FiltersByLevelAndSearchText()
    {
        using PortableLauncherLogSource source = new();
        using LogPageViewModel viewModel = CreateViewModel(source);

        PortableLog.Info("Download", "manifest loaded");
        PortableLog.Warn("Online", "request delayed");
        PortableLog.Error(
            new InvalidOperationException("socket failure"),
            "Online",
            "request failed");

        viewModel.SelectedLevel = viewModel.LevelOptions.Single(
            static option => option.MinimumLevel == PortableLogLevel.Warn);
        viewModel.SearchText = "Online";

        Assert.HasCount(2, viewModel.Lines);
        Assert.IsTrue(
            viewModel.Lines.All(
                static line => line.Module == "Online"));
    }

    [TestMethod]
    public void ClearCommand_ClearsViewAndSource()
    {
        using PortableLauncherLogSource source = new();
        using LogPageViewModel viewModel = CreateViewModel(source);
        PortableLog.Info("Test", "entry");

        viewModel.ClearCommand.Execute(null);

        Assert.IsEmpty(viewModel.Lines);
        Assert.IsEmpty(source.GetSnapshot());
        Assert.IsTrue(viewModel.IsEmpty);
    }

    private static LogPageViewModel CreateViewModel(
        ILauncherLogSource source) =>
        new(
            source,
            new InlineUiScheduler(),
            new NullClipboardService(),
            new NullFileDialogService(),
            new NullHintService());

    private sealed class InlineUiScheduler : IUiScheduler
    {
        public bool CheckAccess() => true;

        public void Post(Action action) => action();

        public Task InvokeAsync(
            Action action,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            action();
            return Task.CompletedTask;
        }
    }

    private sealed class NullClipboardService : IClipboardService
    {
        public Task SetTextAsync(
            string text,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NullFileDialogService : IFileDialogService
    {
        public Task<string?> PickSaveFileAsync(
            string title,
            string suggestedFileName,
            IReadOnlyList<FileDialogFilter> filters,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task<string?> PickOpenFileAsync(
            string title,
            IReadOnlyList<FileDialogFilter> filters,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class NullHintService : IHintService
    {
        public void ShowInfo(string message)
        {
        }

        public void ShowSuccess(string message)
        {
        }

        public void ShowWarning(string message)
        {
        }

        public void ShowError(string message)
        {
        }
    }
}
