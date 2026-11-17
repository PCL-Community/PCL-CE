using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Platform;
using PCL.Core.UI.Dialogs;

namespace PCL.Core.Test.UI;

[TestClass]
[DoNotParallelize]
public sealed class DialogServiceAsyncTest
{
    private readonly TestDialogPresenter _presenter = new();

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Initialize()
    {
        ApplicationRuntime.ResetForTests();
        var provider = new TestServiceProvider()
            .Add<IUiDispatcher>(new TestUiDispatcher())
            .Add<ILocalizationBackend>(new TestLocalizationBackend());
        ApplicationRuntime.Initialize(
            new ApplicationServices
            {
                Platform = new PlatformModule
                {
                    Kind = PlatformKind.Linux,
                    Capabilities = PlatformCapabilities.Linux
                },
                Ui = new UiModule(),
                Services = provider
            });

        DialogService.Instance.ClearPending();
        DialogService.Instance.Presenter = _presenter;
    }

    [TestCleanup]
    public void Cleanup()
    {
        DialogService.Instance.ClearPending();
        DialogService.Instance.Presenter = null;
        ApplicationRuntime.Shutdown();
        ApplicationRuntime.ResetForTests();
    }

    [TestMethod]
    public async Task ShowAsyncWaitsForButtonResult()
    {
        var task = DialogService.Instance.ShowAsync(
            CreateTextRequest(),
            TestContext.CancellationToken);

        var dialog = _presenter.LastDialog;
        Assert.IsNotNull(dialog);

        dialog.CloseByButton(1);
        var result = await task;

        Assert.AreEqual(1, result.Button);
    }

    [TestMethod]
    public async Task ShowAsyncReturnsNoneWhenPresenterIsMissing()
    {
        DialogService.Instance.Presenter = null;

        var result = await DialogService.Instance.ShowAsync(
            CreateTextRequest(),
            TestContext.CancellationToken);

        Assert.AreEqual(DialogResult.None.Button, result.Button);
    }

    [TestMethod]
    public async Task ShowAsyncCancelClosesDialog()
    {
        using var cts = new CancellationTokenSource();
        var task = DialogService.Instance.ShowAsync(CreateTextRequest(), cts.Token);

        var dialog = _presenter.LastDialog;
        Assert.IsNotNull(dialog);

        await cts.CancelAsync();
        var result = await task;

        Assert.AreEqual(DialogResult.Cancel.Button, result.Button);
        Assert.IsTrue(dialog.IsClosed);
    }

    [TestMethod]
    public void BlockingEntryRejectsUiThread()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            DialogService.Instance.ShowBlockingOnWorkerThread(CreateTextRequest()));
    }

    [TestMethod]
    public void ShowNotificationDoesNotWaitForResult()
    {
        DialogService.Instance.ShowNotification(CreateTextRequest());

        var dialog = _presenter.LastDialog;
        Assert.IsNotNull(dialog);
        Assert.IsFalse(dialog.IsClosed);

        dialog.CloseByButton(1);
        Assert.IsTrue(dialog.IsClosed);
    }

    private static TextDialogRequest CreateTextRequest()
    {
        return new TextDialogRequest
        {
            Title = "Test",
            Content = "Test",
            Buttons = DialogButtons.OkCancel
        };
    }

    private sealed class TestLocalizationBackend : ILocalizationBackend
    {
        public string? TryFindResource(string key)
        {
            return null;
        }

        public void ApplyLanguageResources(string baseLanguageCode, string? currentLanguageCode)
        {
        }
    }

    private sealed class TestUiDispatcher : IUiDispatcher
    {
        public bool CheckAccess()
        {
            return true;
        }

        public void Post(Action action)
        {
            action();
        }

        public void Invoke(Action action)
        {
            action();
        }

        public T? Invoke<T>(Func<T> func)
        {
            return func();
        }

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public Task<T> InvokeAsync<T>(Func<T> func)
        {
            return Task.FromResult(func());
        }
    }

    private sealed class TestDialogPresenter : IDialogPresenter
    {
        public DialogViewModel? LastDialog { get; private set; }
        public bool IsAvailable => true;

        public void Present(DialogViewModel dialog)
        {
            LastDialog = dialog;
        }

        public void Dismiss(DialogViewModel dialog)
        {
        }

        public void ShowFallback(DialogViewModel dialog)
        {
            LastDialog = dialog;
            dialog.Close(DialogResult.None);
        }
    }
}