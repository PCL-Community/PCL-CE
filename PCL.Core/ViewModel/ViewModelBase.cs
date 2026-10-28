using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.Logging;

namespace PCL.Core.ViewModel;

/// <summary>统一的异步状态与异常处理基类。</summary>
public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty] private AsyncState _state = AsyncState.Idle;

    [ObservableProperty] private string? _error;

    public bool IsBusy => State == AsyncState.Loading;

    partial void OnStateChanged(AsyncState value)
    {
        OnPropertyChanged(nameof(IsBusy));
    }

    protected async Task RunAsync(Func<CancellationToken, Task> body, CancellationToken external = default)
    {
        State = AsyncState.Loading;
        Error = null;

        try
        {
            await body(external);
            State = AsyncState.Ready;
        }
        catch (OperationCanceledException)
        {
            State = AsyncState.Idle;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            State = AsyncState.Failed;
            LogWrapper.Error(ex, "异步操作执行失败");
        }
    }

    protected async Task<T?> RunAsync<T>(Func<CancellationToken, Task<T>> body, CancellationToken external = default)
    {
        State = AsyncState.Loading;
        Error = null;

        try
        {
            var result = await body(external);
            State = AsyncState.Ready;
            return result;
        }
        catch (OperationCanceledException)
        {
            State = AsyncState.Idle;
            return default;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            State = AsyncState.Failed;
            LogWrapper.Error(ex, "异步操作执行失败");
            return default;
        }
    }
}
