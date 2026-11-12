using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils;

namespace PCL;

public static partial class Loading
{
    /// <summary>
    ///     加载器的统一基类。
    /// </summary>
    public abstract class LoaderBase : ILoadingTrigger
    {
        public delegate void OnStateChangedThreadEventHandler(
            LoaderBase loader,
            LoadState newState,
            LoadState oldState);

        public delegate void OnStateChangedUiEventHandler(
            LoaderBase loader,
            LoadState newState,
            LoadState oldState);

        public delegate void PreviewFinishEventHandler(LoaderBase loader);

        public const string WaitForExitTimeoutMessage = "等待加载器执行超时。";

        /// <summary>
        ///     用于状态改变检测的同步锁。
        /// </summary>
        public readonly object lockState = new();

        /// <summary>
        ///     按当前语言生成显示名称的委托。为空时使用 <see cref="name" />。
        ///     需要跟随界面语言刷新时请设置该委托。
        /// </summary>
        public Func<string>? DisplayNameFactory;

        /// <summary>
        ///     加载器的内部标识，不参与本地化，可用于跨语言稳定的逻辑匹配。
        ///     为空表示该加载器没有特殊的内部标识。
        /// </summary>
        public string? Identifier;

        /// <summary>
        ///     加载器的标识编号。
        /// </summary>
        public int Uuid = LauncherCommon.GetUuid();


        /// <summary>
        ///     使用 LoaderCombo 加载时，该任务是否会阻碍后续任务的进行。
        /// </summary>
        public bool block = true;

        public bool hasOnStateChangedThread;

        /// <summary>
        ///     等待加载器结束的异步等待者，用于 <see cref="WaitForExitAsync" /> 与 <see cref="WaitForExitTimeAsync" />。
        /// </summary>
        private readonly List<TaskCompletionSource> _exitWaiters = [];

        /// <summary>
        ///     当前加载器是否由 IsForceRestart 强制调起。
        ///     这个属性自身不会干任何事，而是提供给加载器执行的函数，使得加载器调用另一个加载器时，可以继承强制重启属性。
        /// </summary>
        public bool isForceRestarting;

        /// <summary>
        ///     加载器的名称。该值面向用户显示，可能被本地化。
        /// </summary>
        public string name;

        /// <summary>
        ///     父加载器。
        /// </summary>
        public LoaderBase parent;

        /// <summary>
        ///     该加载器是否显示在列表中。
        /// </summary>
        public bool show = true;

        protected LoaderBase(LoaderBase? parent = null, Exception? error = null)
        {
            this.parent = parent!;
            Error = error!;
            name = $"未命名任务 {Uuid}#";
        }

        /// <summary>
        ///     当前语言下的显示名称。
        /// </summary>
        public string DisplayName => DisplayNameFactory?.Invoke() ?? name;

        /// <summary>
        ///     最上级的加载器。
        /// </summary>
        public LoaderBase? RealParent
        {
            get
            {
                LoaderBase realParentRet;
                try
                {
                    realParentRet = parent;
                    while (realParentRet?.parent != null)
                        realParentRet = realParentRet.parent;
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(
                        ex,
                        "获取父加载器失败（" + name + "）",
                        LauncherLogLevel.Feedback,
                        userSummary: Lang.Text("Application.Loader.Error.OperationFailed"));
                    return null;
                }

                return realParentRet;
            }
        }

        /// <summary>
        ///     简易的在 UI 线程添加触发事件的方式。主要用于在新建 Loader 时直接使用 With 绑定事件，以及进行老代码兼容。
        /// </summary>
        public Action<LoaderBase> OnStateChanged
        {
            set { OnStateChangedUi += (loader, _, _) => value(loader); }
        }

        /// <summary>
        ///     加载器的状态。
        /// </summary>
        public LoadState State
        {
            get;
            set
            {
                if (field == value)
                    return;
                var oldState = field;
                if (value == LoadState.Finished && Config.Debug.AddRandomDelay)
                    Thread.Sleep(RandomUtils.NextInt(100, 2000));
                field = value;
                if (IsExitState(value))
                    CompleteExitWaiters();
                LauncherCommon.Log(
                    $"[Loader] 加载器 {name} 状态改变：{LauncherCommon.GetStringFromEnum(value)}");
                LauncherCommon.RunInUi(() =>
                {
                    LoadingState = value switch
                    {
                        LoadState.Loading => LoadingState.Run,
                        LoadState.Failed => LoadingState.Error,
                        _ => LoadingState.Stop
                    };
                    OnStateChangedUi?.Invoke(this, value, oldState);
                });
                if (hasOnStateChangedThread)
                    LauncherCommon.RunInThread(() =>
                        OnStateChangedThread?.Invoke(this, value, oldState));
            }
        } = LoadState.Waiting;

        /// <summary>
        ///     计算总进度时的权重。它应该为预计时间（秒）。
        /// </summary>
        public double ProgressWeight { get; set; } = 1d;

        /// <summary>
        ///     若加载器出错，可提供给外部参考的异常。
        /// </summary>
        public Exception Error { get; set; }

        /// <summary>
        ///     加载器的执行进度，为 0 至 1 的小数。
        /// </summary>
        public virtual double Progress
        {
            get
            {
                return State switch
                {
                    LoadState.Waiting => 0d,
                    LoadState.Loading => field == -1 ? 0.02d : field,
                    _ => 1d
                };
            }
            set
            {
                if (field == value)
                    return;
                var oldValue = field;
                field = value;
                ProgressChanged?.Invoke(value, oldValue);
            }
        } = -1;

        public bool IsLoader => true;

        public LoadingState LoadingState
        {
            get;
            set
            {
                if (field == value)
                    return;
                var oldState = field;
                field = value;
                LoadingStateChanged?.Invoke(value, oldState);
            }
        } = LoadingState.Stop;

        public event ILoadingTrigger.LoadingStateChangedEventHandler? LoadingStateChanged;
        public event ILoadingTrigger.ProgressChangedEventHandler? ProgressChanged;

        public virtual void InitParent(LoaderBase parent)
        {
            this.parent = parent;
        }

        /// <summary>
        ///     当状态改变时，在工作线程触发代码。在添加事件后，必须将 HasOnStateChangedThread 设为 True。
        /// </summary>
        public event OnStateChangedThreadEventHandler? OnStateChangedThread;

        /// <summary>
        ///     当状态改变时，在 UI 线程触发代码。
        /// </summary>
        public event OnStateChangedUiEventHandler? OnStateChangedUi;

        /// <summary>
        ///     在加载器目标事件执行完成，加载器状态即将变为 Finish 时调用。可以视为扩展加载器目标事件。
        /// </summary>
        public event PreviewFinishEventHandler? PreviewFinish;

        protected void RaisePreviewFinish()
        {
            PreviewFinish?.Invoke(this);
        }

        public abstract void Start(object? input = null, bool isForceRestart = false);
        public abstract void Abort();

        /// <summary>
        ///     无限期地等待加载器完成，直到结束或抛出异常。若加载器尚未开始，则会开始执行。
        /// </summary>
        public void WaitForExit(
            object? input = null,
            LoaderBase? loaderToSyncProgress = null,
            bool isForceRestart = false)
        {
            Start(input, isForceRestart);
            while (State == LoadState.Loading)
            {
                loaderToSyncProgress?.Progress = Progress;
                Thread.Sleep(10);
            }

            switch (State)
            {
                case LoadState.Finished:
                    break;

                case LoadState.Aborted:
                    throw new ThreadInterruptedException("加载器执行已中断。");

                case LoadState.Waiting:
                case LoadState.Loading:
                case LoadState.Failed:
                default:
                    {
                        if (Error is null)
                            throw new InvalidOperationException("未知错误！");
                        throw Error;
                    }
            }
        }

        /// <summary>
        ///     等待加载器完成，直到结束、抛出异常或超时。若加载器尚未开始，则会开始执行。
        /// </summary>
        /// <param name="timeout">等待的超时时间，以毫秒为单位。</param>
        /// <param name="timeoutMessage">若执行超时，将会抛出的异常信息。</param>
        public void WaitForExitTime(
            int timeout,
            object? input = null,
            string timeoutMessage = WaitForExitTimeoutMessage,
            object? loaderToSyncProgress = null,
            bool isForceRestart = false)
        {
            Start(input, isForceRestart);
            while (State == LoadState.Loading)
            {
                if (loaderToSyncProgress is not null)
                    ((dynamic)loaderToSyncProgress).Progress = Progress;
                Thread.Sleep(10);
                timeout -= 10;
                if (timeout < 0)
                    throw new TimeoutException(timeoutMessage);
            }

            switch (State)
            {
                case LoadState.Finished:
                    break;

                case LoadState.Aborted:
                    throw new ThreadInterruptedException("加载器执行已中断。");

                case LoadState.Waiting:
                case LoadState.Loading:
                case LoadState.Failed:
                default:
                    {
                        if (Error is null) throw new InvalidOperationException("未知错误！");
                        throw Error;
                    }
            }
        }

        public override bool Equals(object? obj)
        {
            var @base = obj as LoaderBase;
            return @base is not null && Uuid == @base.Uuid;
        }

        /// <summary>
        ///     该状态是否代表加载器已经结束（无论成功与否）。
        /// </summary>
        private static bool IsExitState(LoadState state)
        {
            return state is LoadState.Finished or LoadState.Failed or LoadState.Aborted;
        }

        /// <summary>
        ///     注册一个等待加载器结束的异步等待者。
        ///     若加载器已经结束，则返回的等待者会立即完成。
        /// </summary>
        private TaskCompletionSource RegisterExitWaiter()
        {
            var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (lockState)
            {
                if (IsExitState(State))
                    waiter.TrySetResult();
                else
                    _exitWaiters.Add(waiter);
            }

            return waiter;
        }

        private void UnregisterExitWaiter(TaskCompletionSource waiter)
        {
            lock (lockState)
                _exitWaiters.Remove(waiter);
        }

        /// <summary>
        ///     加载器结束时唤醒全部等待者。由 <see cref="State" /> 的 setter 调用。
        /// </summary>
        private void CompleteExitWaiters()
        {
            TaskCompletionSource[] waiters;
            lock (lockState)
            {
                if (_exitWaiters.Count == 0)
                    return;
                waiters = _exitWaiters.ToArray();
                _exitWaiters.Clear();
            }

            foreach (var waiter in waiters)
                waiter.TrySetResult();
        }

        /// <summary>
        ///     等待加载器结束，并在结束后按状态抛出对应异常。<br />
        ///     不会阻塞调用线程，可以在 UI 线程中安全使用。
        /// </summary>
        /// <param name="input">传递给加载器的输入。</param>
        /// <param name="loaderToSyncProgress">需要同步进度的加载器，可以为 <see langword="null" />。</param>
        /// <param name="isForceRestart">是否强制重新执行。</param>
        /// <param name="cancellationToken">取消等待。取消不会中止加载器本身，只会让本方法抛出 <see cref="OperationCanceledException" />。</param>
        public async Task WaitForExitAsync(
            object? input = null,
            LoaderBase? loaderToSyncProgress = null,
            bool isForceRestart = false,
            CancellationToken cancellationToken = default)
        {
            var waiter = RegisterExitWaiter();
            try
            {
                Start(input, isForceRestart);
                await WaitForWaiterAsync(waiter, loaderToSyncProgress, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                UnregisterExitWaiter(waiter);
            }

            ThrowIfExitFailed();
        }

        /// <summary>
        ///     等待加载器结束，直到结束、抛出异常或超时。<br />
        ///     不会阻塞调用线程，可以在 UI 线程中安全使用。
        /// </summary>
        /// <param name="timeout">等待的超时时间，以毫秒为单位。</param>
        /// <param name="input">传递给加载器的输入。</param>
        /// <param name="timeoutMessage">若执行超时，将会抛出的异常信息。</param>
        /// <param name="loaderToSyncProgress">需要同步进度的加载器，可以为 <see langword="null" />。</param>
        /// <param name="isForceRestart">是否强制重新执行。</param>
        /// <param name="cancellationToken">取消等待。取消不会中止加载器本身，只会让本方法抛出 <see cref="OperationCanceledException" />。</param>
        public async Task WaitForExitTimeAsync(
            int timeout,
            object? input = null,
            string timeoutMessage = WaitForExitTimeoutMessage,
            LoaderBase? loaderToSyncProgress = null,
            bool isForceRestart = false,
            CancellationToken cancellationToken = default)
        {
            var waiter = RegisterExitWaiter();
            try
            {
                Start(input, isForceRestart);
                var waitTask = WaitForWaiterAsync(waiter, loaderToSyncProgress, cancellationToken);
                var completed = await Task.WhenAny(waitTask, Task.Delay(timeout, cancellationToken))
                    .ConfigureAwait(false);
                if (completed != waitTask)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new TimeoutException(timeoutMessage);
                }

                await waitTask.ConfigureAwait(false);
            }
            finally
            {
                UnregisterExitWaiter(waiter);
            }

            ThrowIfExitFailed();
        }

        private async Task WaitForWaiterAsync(
            TaskCompletionSource waiter,
            LoaderBase? loaderToSyncProgress,
            CancellationToken cancellationToken)
        {
            void SyncProgress(double value, double oldValue)
            {
                if (loaderToSyncProgress is null)
                    return;
                LauncherCommon.RunInUi(() => loaderToSyncProgress.Progress = value);
            }

            if (loaderToSyncProgress is not null)
            {
                ProgressChanged += SyncProgress;
                loaderToSyncProgress.Progress = Progress;
            }

            try
            {
                await waiter.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (loaderToSyncProgress is not null)
                    ProgressChanged -= SyncProgress;
            }
        }

        private void ThrowIfExitFailed()
        {
            switch (State)
            {
                case LoadState.Finished:
                    break;

                case LoadState.Aborted:
                    throw new OperationCanceledException("加载器执行已中断。");

                case LoadState.Waiting:
                case LoadState.Loading:
                case LoadState.Failed:
                default:
                    {
                        if (Error is null)
                            throw new InvalidOperationException("未知错误！");
                        throw Error;
                    }
            }
        }
    }
}