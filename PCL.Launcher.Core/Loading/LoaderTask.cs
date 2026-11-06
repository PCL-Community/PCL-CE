using Microsoft.VisualBasic.CompilerServices;
using PCL.Core.App.Localization;
using PCL.Core.Utils;

namespace PCL;

public static partial class Loading
{
    public abstract class LoaderTask : LoaderBase
    {
        /// <summary>
        ///     上次完成加载时的时间。
        /// </summary>
        public long lastFinishedTime;

        /// <summary>
        ///     最后一次运行加载器的线程。可能为 Nothing，或线程已结束。
        /// </summary>
        public Task? lastRunningTask;

        /// <summary>
        ///     在输入相同时使用原有结果的超时，单位为毫秒。
        /// </summary>
        public int reloadTimeout = -1;

        /// <summary>
        ///     当前执行线程是否应当中断。只应用在加载器的工作线程中判断，不可跨线程调用。
        /// </summary>
        public bool IsAborted => IsAbortedWithThread(Task.CurrentId ?? -1);

        /// <summary>
        ///     当前执行线程是否应当中断。需要手动提供加载器线程，用于需要跨线程检查的情况。
        /// </summary>
        public bool IsAbortedWithThread(int compareTaskId)
        {
            return lastRunningTask is null
                || compareTaskId != lastRunningTask.Id
                || State == LoadState.Aborted;
        }

        public abstract bool ShouldStart(
            ref object? input,
            bool isForceRestart = false,
            bool ignoreReloadTimeout = false);

        public abstract object? StartGetInputNoType(
            object? input = null,
            Func<object>? inputDelegate = null);
    }

    /// <summary>
    ///     用于异步执行并监控单一函数的加载器。
    /// </summary>
    public class LoaderTask<InputType, OutputType> : LoaderTask
    {
        private CancellationTokenSource? _cancelToken;

        public InputType? input;
        protected internal Func<InputType?>? inputDelegate;

        protected internal Action<LoaderTask<InputType, OutputType>> loadDelegate;
        public OutputType? output = default;

        protected internal ThreadPriority threadPriority;

        public LoaderTask(
            string name,
            Action<LoaderTask<InputType, OutputType>> loadDelegate,
            InputType? input = default,
            Func<InputType?>? inputDelegate = null,
            ThreadPriority priority = ThreadPriority.Normal)
        {
            this.name = name;
            this.loadDelegate = loadDelegate;
            this.input = input;
            this.inputDelegate = inputDelegate;
        }

        public CancellationToken AbortedToken =>
            _cancelToken?.Token
            ?? new CancellationToken(true);

        public InputType? StartGetInput(
            InputType? input = default,
            Func<InputType?>? inputDelegate = null)
        {
            inputDelegate ??= this.inputDelegate;
            // 该逻辑未考虑值类型恰好传入 default 值（如 double 传了 0.0）的情况，可能产生未定义行为
            if (EqualityComparer<InputType>.Default.Equals(input, default)
                && inputDelegate is not null)
                LauncherCommon.RunInUiWait(() => input = inputDelegate());
            return input;
        }

        public override object? StartGetInputNoType(
            object? input = null,
            Func<object?>? inputDelegate = null)
        {
            return StartGetInput(
                input is null
                    ? default
                    : (InputType?)input,
                inputDelegate is null
                    ? null
                    : () => (InputType?)inputDelegate());
        }

        public override bool ShouldStart(
            ref object? input,
            bool isForceRestart = false,
            bool ignoreReloadTimeout = false)
        {
            try
            {
                input = StartGetInput(Conversions.ToGenericParameter<InputType>(input));
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(
                    ex,
                    $"加载输入获取失败（{name}）",
                    LauncherLogLevel.Hint,
                    userSummary: Lang.Text("Application.Loader.Error.OperationFailed"));
                Error = ex;
                lock (lockState)
                {
                    State = LoadState.Failed;
                }
            }

            if (isForceRestart)
                return true;

            if (input is null != this.input is null)
                return true;

            if (input is not null && !input.Equals(this.input))
                return true;

            if (State is not (LoadState.Loading or LoadState.Finished))
                return true;

            return !ignoreReloadTimeout
                && reloadTimeout != -1
                && lastFinishedTime != 0L
                && TimeUtils.GetTimeTick() - lastFinishedTime >= reloadTimeout;
        }

        public override void Start(object? input = null, bool isForceRestart = false)
        {
            if (ShouldStart(ref input, isForceRestart))
            {
                if (State == LoadState.Loading)
                    TriggerThreadAbort();
                this.input = Conversions.ToGenericParameter<InputType>(input);
                lock (lockState)
                {
                    State = LoadState.Loading;
                    Progress = -1;
                }
            }
            else
            {
                return;
            }

            // 因 IsAborted 提前中止说明已有新线程重启，此时不应改为 Aborted；手动取消才改为 Aborted
            lastRunningTask = Task.Run(
                () =>
                {
                    try
                    {
                        isForceRestarting = isForceRestart;
                        if (LauncherCommon.ModeDebug)
                            LauncherCommon.Log(
                                $"[Loader] 加载线程 {name} ({Task.CurrentId}) 已{(isForceRestarting ? "强制" : "")}启动");
                        loadDelegate(this);
                        if (IsAborted)
                        {
                            LauncherCommon.Log(
                                $"[Loader] 加载线程 {name} ({Task.CurrentId}) 已中断但线程正常运行至结束，输出被弃用（最新线程：{lastRunningTask?.Id ?? -1}）",
                                LauncherLogLevel.Developer);
                            return;
                        }

                        if (LauncherCommon.ModeDebug)
                            LauncherCommon.Log($"[Loader] 加载线程 {name} ({Task.CurrentId}) 已完成");
                        RaisePreviewFinish();
                        State = LoadState.Finished;
                        lastFinishedTime = TimeUtils.GetTimeTick();
                    }
                    catch (CancelledException ex)
                    {
                        if (LauncherCommon.ModeDebug)
                            LauncherCommon.Log(
                                ex,
                                $"加载线程 {name} ({Task.CurrentId}) 已触发取消中断，已完成 {Math.Round(Progress * 100d)}%");
                        if (!IsAborted) State = LoadState.Aborted;
                    }
                    catch (ThreadInterruptedException ex)
                    {
                        if (LauncherCommon.ModeDebug)
                            LauncherCommon.Log(
                                ex,
                                $"加载线程 {name} ({Task.CurrentId}) 已触发线程中断，已完成 {Math.Round(Progress * 100d)}%");
                        if (!IsAborted) State = LoadState.Aborted;
                    }
                    catch (Exception ex)
                    {
                        if (IsAborted) return;
                        LauncherCommon.Log(
                            ex,
                            $"加载线程 {name} ({Task.CurrentId}) 出错，已完成 {Math.Round(Progress * 100d)}%",
                            LauncherLogLevel.Developer);
                        Error = ex;
                        State = LoadState.Failed;
                    }
                },
                (_cancelToken ??= new CancellationTokenSource()).Token);
        }

        public override void Abort()
        {
            if (State != LoadState.Loading)
                return;
            lock (lockState)
            {
                State = LoadState.Aborted;
            }

            TriggerThreadAbort();
        }

        private void TriggerThreadAbort()
        {
            if (lastRunningTask is null) return;
            if (LauncherCommon.ModeDebug)
                LauncherCommon.Log($"[Loader] 加载线程 {name} ({lastRunningTask.Id}) 已中断");
            if (!lastRunningTask.IsCompleted) _cancelToken?.Cancel();
            lastRunningTask = null;
            _cancelToken = null;
        }
    }
}