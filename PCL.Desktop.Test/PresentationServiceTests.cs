// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Desktop.Services;
using PCL.Desktop.ViewModels.Feedback;
using PCL.UI.Abstractions;

namespace PCL.Desktop.Test;

[TestClass]
public sealed class PresentationServiceTests
{
    [TestMethod]
    public void IconService_ResolvesKnownIconAndRejectsUnknownIcon()
    {
        IconResource? icon = AvaloniaIconService.Shared.GetIcon("home");

        Assert.IsNotNull(icon);
        Assert.AreEqual("lucide/home", icon.Key);
        Assert.IsNull(AvaloniaIconService.Shared.GetIcon("missing-icon"));
    }

    [TestMethod]
    public void NotificationService_PublishesAndDismissesMessage()
    {
        InAppNotificationService service = new(new InlineUiScheduler());

        service.ShowWarning("测试消息");

        Assert.HasCount(1, service.Messages);
        InAppMessageViewModel message = service.Messages[0];
        Assert.AreEqual("测试消息", message.Message);
        Assert.AreEqual(HintSeverity.Warning, message.Severity);

        message.DismissCommand.Execute(null);

        Assert.IsEmpty(service.Messages);
    }

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
}
