using Microsoft.VisualBasic.CompilerServices;
using PCL.Core.App;
using PCL.Core.Utils;
using System.Collections;
using System.IO;
using System.Windows.Shell;
using PCL.Network;

namespace PCL;

public static partial class ModLoader
{
    public abstract partial class LoaderBase
    {
        // 等待结束
        public const string WaitForExitTimeoutMessage = "等待加载器执行超时。";

        /// <summary>
        ///     用于状态改变检测的同步锁。
        /// </summary>
        public readonly object LockState = new();

        private MyLoading.MyLoadingState _LoadingState = MyLoading.MyLoadingState.Stop;
        private LoadState _State = LoadState.Waiting;

        /// <summary>
        ///     使用 LoaderCombo 加载时，该任务是否会阻碍后续任务的进行。
        /// </summary>
        public bool Block = true;

        public bool HasOnStateChangedThread;

        /// <summary>
        ///     当前加载器是否由 IsForceRestart 强制调起。
        ///     这个属性自身不会干任何事，而是提供给加载器执行的函数，使得加载器调用另一个加载器时，可以继承强制重启属性。
        /// </summary>
        public bool IsForceRestarting;

        /// <summary>
        ///     加载器的名称。
        /// </summary>
        public string Name;

        /// <summary>
        ///     该加载器是否显示在列表中。
        /// </summary>
        public bool Show = true;

        // 基础属性
        /// <summary>
        ///     加载器的标识编号。
        /// </summary>
        public int Uuid = LauncherDispatcher.GetUuid();

        // 状态监控
        /// <summary>
        ///     加载器的状态。
        /// </summary>
        public LoadState State
        {
            get => _State;
            set
            {
                if (_State == value)
                    return;
                var OldState = _State;
                if (value == LoadState.Finished && Config.Debug.AddRandomDelay)
                    Thread.Sleep(RandomUtils.NextInt(100, 2000));
                _State = value;
                LauncherLogger.Log("[Loader] 加载器 " + Name + " 状态改变：" + LauncherText.GetStringFromEnum(value));
                // 实现 ILoadingTrigger 接口与 OnStateChanged 回调
                LauncherDispatcher.RunInUi(() =>
                {
                    switch (value)
                    {
                        case LoadState.Loading:
                        {
                            LoadingState = MyLoading.MyLoadingState.Run;
                            break;
                        }
                        case LoadState.Failed:
                        {
                            LoadingState = MyLoading.MyLoadingState.Error;
                            break;
                        }

                        default:
                        {
                            LoadingState = MyLoading.MyLoadingState.Stop;
                            break;
                        }
                    }

                    OnStateChangedUi?.Invoke(this, value, OldState);
                });
                if (HasOnStateChangedThread)
                    LauncherDispatcher.RunInThread(() => OnStateChangedThread?.Invoke(this, value, OldState));
            }
        }

        /// <summary>
        ///     若加载器出错，可提供给外部参考的异常。
        /// </summary>
        public Exception Error { get; set; }

        public MyLoading.MyLoadingState LoadingState
        {
            get => _LoadingState;
            set
            {
                if (_LoadingState == value)
                    return;
                var OldState = _LoadingState;
                _LoadingState = value;
                LoadingStateChanged?.Invoke(value, OldState);
            }
        }

        public event ILoadingTrigger.LoadingStateChangedEventHandler? LoadingStateChanged;

        // 事件

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

        // 状态变化
        public abstract void Start(object? Input = null, bool IsForceRestart = false);
        public abstract void Abort();

        // 相同重载
        public override bool Equals(object obj)
        {
            var @base = obj as LoaderBase;
            return @base is not null && Uuid == @base.Uuid;
        }
    }

    public abstract partial class LoaderTask
    {
        /// <summary>
        ///     上次完成加载时的时间。
        /// </summary>
        public long LastFinishedTime;

        /// <summary>
        ///     最后一次运行加载器的线程。可能为 Nothing，或线程已结束。
        /// </summary>
        public Task? LastRunningTask;

        /// <summary>
        ///     在输入相同时使用原有结果的超时，单位为毫秒。
        /// </summary>
        public int ReloadTimeout = -1;

        // 状态指示
        /// <summary>
        ///     当前执行线程是否应当中断。只应用在加载器的工作线程中判断，不可跨线程调用。
        /// </summary>
        public bool IsAborted => IsAbortedWithThread(Task.CurrentId ?? -1);

        /// <summary>
        ///     当前执行线程是否应当中断。需要手动提供加载器线程，用于需要跨线程检查的情况。
        /// </summary>
        public bool IsAbortedWithThread(int compareTaskId)
        {
            return LastRunningTask is null || compareTaskId != LastRunningTask.Id ||
                   State == LoadState.Aborted;
        }

        public abstract bool ShouldStart(ref object? input, bool isForceRestart = false, bool ignoreReloadTimeout = false);

        // 装箱！装箱！装箱圣地！
        public abstract object? StartGetInputNoType(object? input = null, Func<object>? inputDelegate = null);
    }

    public partial class LoaderTask<InputType, OutputType>
    {
        // 输入输出
        public InputType Input;
        protected internal Func<InputType?>? InputDelegate;

        // 执行事件
        protected internal Action<LoaderTask<InputType, OutputType>> LoadDelegate;
        public OutputType Output = default;

        private CancellationTokenSource? CancelToken;

        // 线程设定
        protected internal ThreadPriority ThreadPriority;

        // 获取输入
        public InputType? StartGetInput(InputType? Input = default, Func<InputType?>? InputDelegate = null) // InputDelegate 参数存在匿名调用
        {
            InputDelegate ??= this.InputDelegate;
            // 按照龙猫的逻辑，此处将 input 与默认值直接进行等价比较，若相等则认为 input 未传入具体值，而调用 inputDelegate 获取
            // 这种逻辑未考虑值类型恰好传入 default 值 (如 double 传了 0.0) 的情况，这是一个陷阱，可能会产生 undefined behavior
            if (EqualityComparer<InputType>.Default.Equals(Input, default) && InputDelegate is not null)
                LauncherDispatcher.RunInUiWait(() => Input = InputDelegate());
            return Input;
        }

        public override object? StartGetInputNoType(object? Input = null, Func<object?>? InputDelegate = null)
        {
            return StartGetInput(Input == null ? default : (InputType?)Input, InputDelegate == null ? null : () => (InputType?)InputDelegate());
        }

        // 代码执行
        public override bool ShouldStart(ref object? Input, bool IsForceRestart = false, bool IgnoreReloadTimeout = false)
        {
            // 获取输入
            try
            {
                Input = StartGetInput(Conversions.ToGenericParameter<InputType>(Input));
            }
            catch (Exception ex)
            {
                LauncherLogger.Log(ex, "加载输入获取失败（" + Name + "）", LauncherLogger.LogLevel.Hint);
                Error = ex;
                lock (LockState)
                {
                    State = LoadState.Failed;
                }
            }

            // 检验输入以确定情况
            if (IsForceRestart)
                return true; // 强制要求重启
            if (Input is null != this.Input is null || (Input is not null && !Input.Equals(this.Input)))
                return true; // 输入不同
            if ((State == LoadState.Loading || State == LoadState.Finished) && (IgnoreReloadTimeout ||
                    ReloadTimeout == -1 || LastFinishedTime == 0L ||
                    TimeUtils.GetTimeTick() - LastFinishedTime < ReloadTimeout)) // 正在加载或已结束
                // 没有超时
                return false; // 则不重试

            return true;
            // 需要开始
        }

        public override void Start(object Input = null, bool IsForceRestart = false)
        {
            // 确认是否开始加载
            if (ShouldStart(ref Input, IsForceRestart))
            {
                // 输入不同或失败，开始加载
                if (State == LoadState.Loading)
                    TriggerThreadAbort();
                this.Input = Conversions.ToGenericParameter<InputType>(Input);
                lock (LockState)
                {
                    State = LoadState.Loading;
                    Progress = -1;
                }
            }
            else return;

            // 如果线程是因为判断到 IsAborted 而提前中止，则代表已有新线程被重启，此时不应当改为 Aborted
            // 如果线程是在没有 IsAborted 时手动引发了 ThreadInterruptedException，则代表没有重启线程，这通常代表用户手动取消，应当改为 Aborted
            LastRunningTask = Task.Run(() =>
            {
                try
                {
                    IsForceRestarting = IsForceRestart;
                    if (LauncherLogger.ModeDebug)
                        LauncherLogger.Log(
                            $"[Loader] 加载线程 {Name} ({Task.CurrentId}) 已{(IsForceRestarting ? "强制" : "")}启动");
                    LoadDelegate(this);
                    if (IsAborted)
                    {
                        LauncherLogger.Log(
                            $"[Loader] 加载线程 {Name} ({Task.CurrentId}) 已中断但线程正常运行至结束，输出被弃用（最新线程：{(LastRunningTask is null ? -1 : LastRunningTask.Id)}）",
                            LauncherLogger.LogLevel.Developer);
                        return;
                    }

                    if (LauncherLogger.ModeDebug)
                        LauncherLogger.Log($"[Loader] 加载线程 {Name} ({Task.CurrentId}) 已完成");
                    RaisePreviewFinish();
                    State = LoadState.Finished;
                    LastFinishedTime = TimeUtils.GetTimeTick();
                }
            catch (CancelledException ex)
                {
                    if (LauncherLogger.ModeDebug)
                        LauncherLogger.Log(ex,
                            $"加载线程 {Name} ({Task.CurrentId}) 已触发取消中断，已完成 {Math.Round(Progress * 100d)}%");
                    if (!IsAborted) State = LoadState.Aborted;
                }
                catch (ThreadInterruptedException ex)
                {
                    if (LauncherLogger.ModeDebug)
                        LauncherLogger.Log(ex,
                            $"加载线程 {Name} ({Task.CurrentId}) 已触发线程中断，已完成 {Math.Round(Progress * 100d)}%");
                    if (!IsAborted) State = LoadState.Aborted;
                }
                catch (Exception ex)
                {
                    if (IsAborted) return;
                    LauncherLogger.Log(ex,
                        $"加载线程 {Name} ({Task.CurrentId}) 出错，已完成 {Math.Round(Progress * 100d)}%",
                        LauncherLogger.LogLevel.Developer);
                    Error = ex;
                    State = LoadState.Failed;
                }
            }, (CancelToken ??= new CancellationTokenSource()).Token); // 未中断，本次输出有效
            // LastRunningTask.Start(); // 不能使用 RunInNewThread，否则在函数返回前线程就会运行完，导致误判 IsAborted
        }

        public override void Abort()
        {
            if (State != LoadState.Loading)
                return;
            lock (LockState)
            {
                State = LoadState.Aborted;
            }

            TriggerThreadAbort();
        }

        private void TriggerThreadAbort()
        {
            if (LastRunningTask is null) return;
            if (LauncherLogger.ModeDebug) LauncherLogger.Log($"[Loader] 加载线程 {Name} ({LastRunningTask.Id}) 已中断");
            if (!LastRunningTask.IsCompleted) CancelToken?.Cancel();
            LastRunningTask = null;
            CancelToken = null;
        }
    }

    public partial class LoaderCombo
    {
        public object? Input;

        public override void Start(object Input = null, bool IsForceRestart = false)
        {
            IsForceRestarting = IsForceRestart;
            lock (LockState)
            {
                if (State == LoadState.Loading) return;

                State = LoadState.Loading;
            }

            // 启动加载
            this.Input = Input;
            if (IsForceRestart)
                foreach (var Loader in Loaders)
                    Loader.State = LoadState.Waiting;
            LauncherDispatcher.RunInThread(Update);
        }

        public override void Abort()
        {
            lock (LockState)
            {
                if (State == LoadState.Loading || State == LoadState.Waiting)
                    State = LoadState.Aborted;
                else
                    return;
            }

            LauncherDispatcher.RunInThread(() =>
            {
                foreach (var Loader in Loaders) Loader.Abort();
            });
        }

        /// <summary>
        ///     子任务状态变更。
        /// </summary>
        private void SubTaskStateChanged(LoaderBase Loader, LoadState NewState, LoadState OldState)
        {
            switch (NewState)
            {
                case LoadState.Loading:
                {
                    break;
                }
                // 开始，啥都不干
                case LoadState.Waiting:
                {
                    break;
                }
                // 子加载器可能由于外部输入改变而暂时变为 Waiting，之后会立即重新启动
                // 所以啥都不干就行
                case LoadState.Finished:
                {
                    // 正常结束，触发刷新
                    Update();
                    break;
                }
                case LoadState.Aborted:
                {
                    // 被中断，这个任务也中断
                    Abort();
                    break;
                }

                default:
                {
                    // 完蛋，出错了
                    lock (LockState)
                    {
                        if (State >= LoadState.Finished)
                            return;
                        Error = new Exception(Loader.Name + "失败", Loader.Error);
                        State = Loader.State;
                    }

                    foreach (var currentLoader in Loaders)
                    {
                        Loader = currentLoader;
                        Loader.Abort();
                    }

                    ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                    return;
                }
            }
        }

        /// <summary>
        ///     触发一次更新，以启动新加载器或完成。
        /// </summary>
        private void Update()
        {
            if (State == LoadState.Finished
                || State == LoadState.Failed
                || State == LoadState.Aborted)
                return;

            var isFinished = true;
            var blocked = false;
            object input = Input;

            foreach (var loader in Loaders)
                switch (loader.State)
                {
                    case LoadState.Finished:
                    {
                        if (loader.GetType().Name.StartsWithF("LoaderTask"))
                        {
                            var genericArg = loader.GetType().GenericTypeArguments.FirstOrDefault();
                            var shouldInput = input != null && genericArg == input.GetType()
                                ? input
                                : null;

                            if (((dynamic)loader).ShouldStart(ref shouldInput, false, true))
                            {
                                LauncherLogger.Log("[Loader] 由于输入条件变更，重启已完成的加载器 " + loader.Name);
                                goto Restart;
                            }

                            input = ((dynamic)loader).Output;
                        }

                        if (loader.Block && !isFinished)
                            blocked = true;

                        break;
                    }

                    case LoadState.Loading:
                    {
                        if (loader.GetType().Name.StartsWithF("LoaderTask"))
                        {
                            var genericArg = loader.GetType().GenericTypeArguments.FirstOrDefault();
                            var shouldInput = input != null && genericArg == input.GetType()
                                ? input
                                : null;

                            if (((dynamic)loader).ShouldStart(ref shouldInput, false, true))
                            {
                                LauncherLogger.Log("[Loader] 由于输入条件变更，重启进行中的加载器 "
                                            + loader.Name,
                                    LauncherLogger.LogLevel.Developer);
                                goto Restart;
                            }
                        }

                        isFinished = false;
                        blocked = true;
                        break;
                    }

                    default:

                        Restart:

                        isFinished = false;

                        if (blocked)
                            continue;

                        if (input != null)
                        {
                            var loaderType = loader.GetType().Name;

                            if (loaderType.StartsWithF("LoaderTask")
                                || loaderType.StartsWithF("LoaderCombo"))
                            {
                                var genericArg = loader.GetType().GenericTypeArguments.FirstOrDefault();

                                loader.Start(
                                    genericArg == input.GetType() ? input : null,
                                    IsForceRestarting);
                            }
                            else if (loaderType.StartsWithF("LoaderDownload"))
                            {
                                loader.Start(
                                    input is List<DownloadFile> ? input : null,
                                    IsForceRestarting);
                            }
                            else
                            {
                                throw new Exception("未知的加载器类型（" + loaderType + "）");
                            }
                        }
                        else
                        {
                            loader.Start(IsForceRestart: IsForceRestarting);
                        }

                        if (loader.Block)
                            blocked = true;

                        break;
                }

            if (isFinished)
            {
                RaisePreviewFinish();
                State = LoadState.Finished;
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            }
        }
    }

    public partial class LoaderCombo<InputType>
    {
        public new InputType Input;

        public override void Start(object Input = null, bool IsForceRestart = false)
        {
            this.Input = Conversions.ToGenericParameter<InputType>(Input);
            base.Start(this.Input, IsForceRestart);
        }
    }

    private partial struct LoaderFolderDictionaryEntry
    {
        public DateTime? LastCheckTime;

        public override bool Equals(object obj)
        {
            if (!(obj is LoaderFolderDictionaryEntry))
                return false;
            var entry = (LoaderFolderDictionaryEntry)obj;
            return EqualityComparer<DateTime?>.Default.Equals(LastCheckTime, entry.LastCheckTime) &&
                   (FolderPath ?? "") == (entry.FolderPath ?? "");
        }
    }
}
