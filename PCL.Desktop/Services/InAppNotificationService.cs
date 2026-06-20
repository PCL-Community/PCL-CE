// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.ObjectModel;
using PCL.Desktop.ViewModels.Feedback;
using PCL.UI.Abstractions;

namespace PCL.Desktop.Services;

public sealed class InAppNotificationService :
    IHintService,
    INotificationService
{
    private readonly ObservableCollection<InAppMessageViewModel> _messages = [];
    private readonly IUiScheduler _scheduler;

    public InAppNotificationService(IUiScheduler scheduler)
    {
        _scheduler = scheduler;
        Messages =
            new ReadOnlyObservableCollection<InAppMessageViewModel>(_messages);
    }

    public ReadOnlyObservableCollection<InAppMessageViewModel> Messages { get; }

    public void ShowInfo(string message) =>
        Publish("提示", message, HintSeverity.Information);

    public void ShowSuccess(string message) =>
        Publish("操作成功", message, HintSeverity.Success);

    public void ShowWarning(string message) =>
        Publish("请注意", message, HintSeverity.Warning);

    public void ShowError(string message) =>
        Publish("出现问题", message, HintSeverity.Error);

    public Task ShowToastAsync(
        string title,
        string message,
        HintSeverity severity = HintSeverity.Information,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Publish(title, message, severity);
        return Task.CompletedTask;
    }

    private void Publish(
        string title,
        string message,
        HintSeverity severity)
    {
        _scheduler.Post(
            () =>
            {
                InAppMessageViewModel? item = null;
                item = new InAppMessageViewModel(
                    title,
                    message,
                    severity,
                    () =>
                    {
                        if (item is not null)
                            _messages.Remove(item);
                    });
                _messages.Add(item);
                _ = RemoveLaterAsync(item);
            });
    }

    private async Task RemoveLaterAsync(InAppMessageViewModel item)
    {
        await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        _scheduler.Post(() => _messages.Remove(item));
    }
}
