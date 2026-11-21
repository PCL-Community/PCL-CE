using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.ViewModel;

namespace PCL.Core.Test;

[TestClass]
public sealed class ViewModelBaseTest
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task RunAsyncSuccessReachesReady()
    {
        var vm = new TestViewModel();
        var observed = new List<AsyncState>();
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ViewModelBase.State))
                observed.Add(vm.State);
        };

        await vm.Run(_ => Task.CompletedTask, TestContext.CancellationToken);

        Assert.Contains(AsyncState.Loading, observed);
        Assert.AreEqual(AsyncState.Ready, vm.State);
        Assert.IsNull(vm.Error);
        Assert.IsFalse(vm.IsBusy);
    }

    [TestMethod]
    public async Task RunAsyncFailureSetsError()
    {
        var vm = new TestViewModel();

        await vm.Run(
            _ => throw new InvalidOperationException("boom"),
            TestContext.CancellationToken);

        Assert.AreEqual(AsyncState.Failed, vm.State);
        Assert.AreEqual("boom", vm.Error);
        Assert.IsFalse(vm.IsBusy);
    }

    [TestMethod]
    public async Task RunAsyncCancellationReturnsToIdle()
    {
        var vm = new TestViewModel();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await vm.Run(
            token =>
            {
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            cts.Token);

        Assert.AreEqual(AsyncState.Idle, vm.State);
        Assert.IsNull(vm.Error);
    }

    [TestMethod]
    public async Task RunAsyncGenericReturnsValue()
    {
        var vm = new TestViewModel();

        var result = await vm.RunValue(_ => Task.FromResult<int?>(42));

        Assert.AreEqual(42, result);
        Assert.AreEqual(AsyncState.Ready, vm.State);
    }

    private sealed class TestViewModel : ViewModelBase
    {
        public Task Run(Func<CancellationToken, Task> body, CancellationToken token = default)
        {
            return RunAsync(body, token);
        }

        public Task<int?> RunValue(Func<CancellationToken, Task<int?>> body)
        {
            return RunAsync(body);
        }
    }
}