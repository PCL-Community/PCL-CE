using Microsoft.VisualBasic.CompilerServices;
using PCL.Core.App;
using PCL.Core.Utils;
using System.Collections;
using System.IO;
using System.Windows.Shell;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public static class ModLoader
{
    public enum LoaderFolderRunType
    {
        RunOnUpdated,
        ForceRun,
        UpdateOnly
    }

    // 任务栏进度条
    public static ModBase.SafeList<LoaderBase> LoaderTaskbar = new();
    public static double LoaderTaskbarProgress; // 平滑后的进度
    private static TaskbarItemProgressState LoaderTaskbarProgressLast = TaskbarItemProgressState.None;

    // 文件夹刷新类委托
    private static readonly Dictionary<LoaderBase, LoaderFolderDictionaryEntry> LoaderFolderDictionary = new();

    public static void LoaderTaskbarAdd<T>(LoaderCombo<T> Loader)
    {
        if (ModMain.FrmSpeedLeft is not null)
            ModMain.FrmSpeedLeft.TaskRemove(Loader);
        LoaderTaskbar.Add(Loader);
        ModBase.Log($"[Taskbar] {Loader.Name} 已加入任务列表");
    }

    public static void LoaderTaskbarProgressRefresh()
    {
        try
        {
            TaskbarItemProgressState NewState;
            var NewProgress = LoaderTaskbarProgressGet();
            // 若单个任务已中止，或全部任务已完成，则刷新并移除
            foreach (var Task in LoaderTaskbar)
                if (LoaderTaskbar.All(l => l.State != ModBase.LoadState.Loading) ||
                    Task.State == ModBase.LoadState.Waiting || Task.State == ModBase.LoadState.Aborted)
                {
                    ModMain.FrmSpeedLeft?.TaskRefresh(Task);
                    LoaderTaskbar.Remove(Task);
                    ModBase.Log($"[Taskbar] {Task.Name} 已移出任务列表");
                }

            // 更新平滑后的进度
            if (NewProgress <= 0d || NewProgress >= 1d || LoaderTaskbarProgress > NewProgress)
                LoaderTaskbarProgress = NewProgress;
            else
                LoaderTaskbarProgress = LoaderTaskbarProgress * 0.9d + NewProgress * 0.1d;
            ModBase.RunInUi(() => ModMain.FrmMain.BtnExtraDownload.Progress = LoaderTaskbarProgress);
            // 更新任务栏信息
            if (!LoaderTaskbar.Any() || LoaderTaskbarProgress == 1d)
            {
                NewState = TaskbarItemProgressState.None;
            }
            else if (LoaderTaskbarProgress < 0.015d)
            {
                NewState = TaskbarItemProgressState.Indeterminate;
            }
            else
            {
                NewState = TaskbarItemProgressState.Normal;
                ModMain.FrmMain.TaskbarItemInfo.ProgressValue = LoaderTaskbarProgress;
            }

            if (LoaderTaskbarProgressLast != NewState)
            {
                LoaderTaskbarProgressLast = NewState;
                ModMain.FrmMain.TaskbarItemInfo.ProgressState = NewState;
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新任务栏进度显示失败", ModBase.LogLevel.Feedback);
        }
    }

    public static double LoaderTaskbarProgressGet()
    {
        try
        {
            if (!LoaderTaskbar.Any())
                return 1d;

            return ModBase.MathClamp(
                LoaderTaskbar.Select(l => l.Progress).Average(),
                0,
                1
            );
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取任务栏进度出错", ModBase.LogLevel.Feedback);
            return 0.5d;
        }
    }

    /// <summary>
    ///     执行以文件夹检测作为输入的加载器。加载器需以文件夹路径为输入值。
    ///     返回是否执行了加载器。
    /// </summary>
    /// <param name="ExtraPath">用于检查文件夹修改的额外路径。该路径不会传入加载器。</param>
    /// <param name="LoaderInput">如果不想要文件夹路径为输入值，则传入期望数据</param>
    public static bool LoaderFolderRun(LoaderBase Loader, string FolderPath, LoaderFolderRunType Type, int MaxDepth = 0,
        string ExtraPath = "", bool WaitForExit = false, object LoaderInput = null)
    {
        DirectoryInfo FolderInfo;
        var Value = new LoaderFolderDictionaryEntry { FolderPath = FolderPath + ExtraPath, LastCheckTime = default };
        try
        {
            // 获取数据
            FolderInfo = new DirectoryInfo(FolderPath + ExtraPath);
            Value.LastCheckTime = FolderInfo.Exists ? GetActualLastWriteTimeUtc(FolderInfo, MaxDepth) : null;
            // 如果已经检查过，则跳过
            if (Type == LoaderFolderRunType.RunOnUpdated && LoaderFolderDictionary.ContainsKey(Loader))
            {
                if (FolderInfo.Exists)
                {
                    if (LoaderFolderDictionary[Loader].LastCheckTime is not null &&
                        Value.Equals(LoaderFolderDictionary[Loader]))
                        return false;
                }
                else if (LoaderFolderDictionary[Loader].LastCheckTime is null)
                {
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "文件夹加载器启动检测出错");
        }

        // 写入检查数据
        LoaderFolderDictionary[Loader] = Value;
        // 开始检查
        if (Type == LoaderFolderRunType.UpdateOnly)
            return false;
        if (WaitForExit)
            Loader.WaitForExit(LoaderInput ?? FolderPath, IsForceRestart: true);
        else
            Loader.Start(LoaderInput ?? FolderPath, true);
        return true;
    }

    private static DateTime GetActualLastWriteTimeUtc(DirectoryInfo FolderInfo, int MaxDepth)
    {
        var Time = FolderInfo.LastWriteTimeUtc;
        if (MaxDepth > 0)
            foreach (var Folder in FolderInfo.EnumerateDirectories())
            {
                var FolderTime = GetActualLastWriteTimeUtc(Folder, MaxDepth - 1);
                if (FolderTime > Time)
                    Time = FolderTime;
            }

        return Time;
    }

    // 各类加载器
    /// <summary>
    ///     加载器的统一基类。
    /// </summary>
    public abstract class LoaderBase : ILoadingTrigger
    {
        public delegate void OnStateChangedThreadEventHandler(LoaderBase Loader, ModBase.LoadState NewState,
            ModBase.LoadState OldState);

        public delegate void OnStateChangedUiEventHandler(LoaderBase Loader, ModBase.LoadState NewState,
            ModBase.LoadState OldState);

        public delegate void PreviewFinishEventHandler(LoaderBase Loader);

        // 等待结束
        public const string WaitForExitTimeoutMessage = "等待加载器执行超时。";

        /// <summary>
        ///     用于状态改变检测的同步锁。
        /// </summary>
        public readonly object LockState = new();

        private MyLoading.MyLoadingState _LoadingState = MyLoading.MyLoadingState.Stop;
        private double _Progress = -1;
        private ModBase.LoadState _State = ModBase.LoadState.Waiting;

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
        ///     父加载器。
        /// </summary>
        public LoaderBase Parent;

        /// <summary>
        ///     该加载器是否显示在列表中。
        /// </summary>
        public bool Show = true;

        // 基础属性
        /// <summary>
        ///     加载器的标识编号。
        /// </summary>
        public int Uuid = ModBase.GetUuid();

        public LoaderBase()
        {
            Name = "未命名任务 " + Uuid + "#";
        }

        /// <summary>
        ///     最上级的加载器。
        /// </summary>
        public LoaderBase RealParent
        {
            get
            {
                LoaderBase RealParentRet = default;
                try
                {
                    RealParentRet = Parent;
                    while (RealParentRet is not null && RealParentRet.Parent is not null)
                        RealParentRet = RealParentRet.Parent;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "获取父加载器失败（" + Name + "）", ModBase.LogLevel.Feedback);
                    return null;
                }

                return RealParentRet;
            }
        }

        /// <summary>
        ///     简易的在 UI 线程添加触发事件的方式。主要用于在新建 Loader 时直接使用 With 绑定事件，以及进行老代码兼容。
        /// </summary>
        public Action<LoaderBase> OnStateChanged
        {
            set { OnStateChangedUi += (Loader, NewState, OldState) => value(Loader); }
        }

        // 状态监控
        /// <summary>
        ///     加载器的状态。
        /// </summary>
        public ModBase.LoadState State
        {
            get => _State;
            set
            {
                if (_State == value)
                    return;
                var OldState = _State;
                if (value == ModBase.LoadState.Finished && Config.Debug.AddRandomDelay)
                    Thread.Sleep(RandomUtils.NextInt(100, 2000));
                _State = value;
                ModBase.Log("[Loader] 加载器 " + Name + " 状态改变：" + ModBase.GetStringFromEnum(value));
                // 实现 ILoadingTrigger 接口与 OnStateChanged 回调
                ModBase.RunInUi(() =>
                {
                    switch (value)
                    {
                        case ModBase.LoadState.Loading:
                        {
                            LoadingState = MyLoading.MyLoadingState.Run;
                            break;
                        }
                        case ModBase.LoadState.Failed:
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
                    ModBase.RunInThread(() => OnStateChangedThread?.Invoke(this, value, OldState));
            }
        }

        /// <summary>
        ///     若加载器出错，可提供给外部参考的异常。
        /// </summary>
        public Exception Error { get; set; }

        // 进度监控
        /// <summary>
        ///     加载器的执行进度，为 0 至 1 的小数。
        /// </summary>
        public virtual double Progress
        {
            get
            {
                switch (State)
                {
                    case ModBase.LoadState.Waiting:
                    {
                        return 0d;
                    }
                    case ModBase.LoadState.Loading:
                    {
                        return _Progress == -1 ? 0.02d : _Progress;
                    }

                    default:
                    {
                        return 1d;
                    }
                }
            }
            set
            {
                if (_Progress == value)
                    return;
                var OldValue = _Progress;
                _Progress = value;
                ProgressChanged?.Invoke(value, OldValue);
            }
        }

        /// <summary>
        ///     计算总进度时的权重。它应该为预计时间（秒）。
        /// </summary>
        public double ProgressWeight { get; set; } = 1d;

        public bool IsLoader { get; } = true;

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
        public event ILoadingTrigger.ProgressChangedEventHandler? ProgressChanged;

        public virtual void InitParent(LoaderBase Parent)
        {
            this.Parent = Parent;
        }

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

        /// <summary>
        ///     无限期地等待加载器完成，直到结束或抛出异常。若加载器尚未开始，则会开始执行。
        /// </summary>
        public void WaitForExit(object Input = null, LoaderBase LoaderToSyncProgress = null,
            bool IsForceRestart = false)
        {
            Start(Input, IsForceRestart);
            while (State == ModBase.LoadState.Loading)
            {
                if (LoaderToSyncProgress is not null)
                    LoaderToSyncProgress.Progress = Progress;
                Thread.Sleep(10);
            }

            if (State == ModBase.LoadState.Finished)
            {
            }
            else if (State == ModBase.LoadState.Aborted)
            {
                throw new ThreadInterruptedException("加载器执行已中断。");
            }
            else if (Error is null)
            {
                throw new Exception("未知错误！");
            }
            else
            {
                throw new Exception(Error.Message, Error);
            } // 保留调用堆栈，同时不影响信息输出与单元测试
        }

        /// <summary>
        ///     等待加载器完成，直到结束、抛出异常或超时。若加载器尚未开始，则会开始执行。
        /// </summary>
        /// <param name="Timeout">等待的超时时间，以毫秒为单位。</param>
        /// <param name="TimeoutMessage">若执行超时，将会抛出的异常信息。</param>
        public void WaitForExitTime(int Timeout, object Input = null, string TimeoutMessage = WaitForExitTimeoutMessage,
            object LoaderToSyncProgress = null, bool IsForceRestart = false)
        {
            Start(Input, IsForceRestart);
            while (State == ModBase.LoadState.Loading)
            {
                if (LoaderToSyncProgress is not null)
                    ((dynamic)LoaderToSyncProgress).Progress = Progress;
                Thread.Sleep(10);
                Timeout -= 10;
                if (Timeout < 0)
                    throw new TimeoutException(TimeoutMessage);
            }

            if (State == ModBase.LoadState.Finished)
            {
            }
            else if (State == ModBase.LoadState.Aborted)
            {
                throw new ThreadInterruptedException("加载器执行已中断。");
            }
            else if (Error is null)
            {
                throw new Exception("未知错误！");
            }
            else
            {
                throw Error;
            }
        }

        // 相同重载
        public override bool Equals(object obj)
        {
            var @base = obj as LoaderBase;
            return @base is not null && Uuid == @base.Uuid;
        }
    }

    // 说实话，我真的觉得 C# 应该学学 VB 的那种近乎 Java 泛型擦除的兼容性，省掉一堆麻烦
    public abstract class LoaderTask : LoaderBase
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
                   State == ModBase.LoadState.Aborted;
        }

        public abstract bool ShouldStart(ref object? input, bool isForceRestart = false, bool ignoreReloadTimeout = false);

        // 装箱！装箱！装箱圣地！
        public abstract object? StartGetInputNoType(object? input = null, Func<object>? inputDelegate = null);

    }

    /// <summary>
    ///     用于异步执行并监控单一函数的加载器。
    /// </summary>
    public class LoaderTask<InputType, OutputType> : LoaderTask
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

        public LoaderTask(string Name, Action<LoaderTask<InputType, OutputType>> LoadDelegate,
            Func<InputType?>? InputDelegate = null, ThreadPriority Priority = ThreadPriority.Normal)
        {
            this.Name = Name;
            this.LoadDelegate = LoadDelegate;
            this.InputDelegate = InputDelegate;
        }

        // 获取输入
        public InputType? StartGetInput(InputType? Input = default, Func<InputType?>? InputDelegate = null) // InputDelegate 参数存在匿名调用
        {
            InputDelegate ??= this.InputDelegate;
            // 按照龙猫的逻辑，此处将 input 与默认值直接进行等价比较，若相等则认为 input 未传入具体值，而调用 inputDelegate 获取
            // 这种逻辑未考虑值类型恰好传入 default 值 (如 double 传了 0.0) 的情况，这是一个陷阱，可能会产生 undefined behavior
            if (EqualityComparer<InputType>.Default.Equals(Input, default) && InputDelegate is not null)
                ModBase.RunInUiWait(() => Input = InputDelegate());
            return Input;
        }

        public override object? StartGetInputNoType(object? Input = null, Func<object?>? InputDelegate = null)
        {
            return StartGetInput(Input is null ? default : (InputType?)Input, InputDelegate is null ? null : () => (InputType?)InputDelegate());
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
                ModBase.Log(ex, "加载输入获取失败（" + Name + "）", ModBase.LogLevel.Hint);
                Error = ex;
                lock (LockState)
                {
                    State = ModBase.LoadState.Failed;
                }
            }

            // 检验输入以确定情况
            if (IsForceRestart)
                return true; // 强制要求重启
            if (Input is null != this.Input is null || (Input is not null && !Input.Equals(this.Input)))
                return true; // 输入不同
            if ((State == ModBase.LoadState.Loading || State == ModBase.LoadState.Finished) && (IgnoreReloadTimeout ||
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
                if (State == ModBase.LoadState.Loading)
                    TriggerThreadAbort();
                this.Input = Conversions.ToGenericParameter<InputType>(Input);
                lock (LockState)
                {
                    State = ModBase.LoadState.Loading;
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
                    if (ModBase.ModeDebug)
                        ModBase.Log(
                            $"[Loader] 加载线程 {Name} ({Task.CurrentId}) 已{(IsForceRestarting ? "强制" : "")}启动");
                    LoadDelegate(this);
                    if (IsAborted)
                    {
                        ModBase.Log(
                            $"[Loader] 加载线程 {Name} ({Task.CurrentId}) 已中断但线程正常运行至结束，输出被弃用（最新线程：{(LastRunningTask is null ? -1 : LastRunningTask.Id)}）",
                            ModBase.LogLevel.Developer);
                        return;
                    }

                    if (ModBase.ModeDebug)
                        ModBase.Log($"[Loader] 加载线程 {Name} ({Task.CurrentId}) 已完成");
                    RaisePreviewFinish();
                    State = ModBase.LoadState.Finished;
                    LastFinishedTime = TimeUtils.GetTimeTick();
                }
                catch (ModBase.CancelledException ex)
                {
                    if (ModBase.ModeDebug)
                        ModBase.Log(ex,
                            $"加载线程 {Name} ({Task.CurrentId}) 已触发取消中断，已完成 {Math.Round(Progress * 100d)}%");
                    if (!IsAborted) State = ModBase.LoadState.Aborted;
                }
                catch (ThreadInterruptedException ex)
                {
                    if (ModBase.ModeDebug)
                        ModBase.Log(ex,
                            $"加载线程 {Name} ({Task.CurrentId}) 已触发线程中断，已完成 {Math.Round(Progress * 100d)}%");
                    if (!IsAborted) State = ModBase.LoadState.Aborted;
                }
                catch (Exception ex)
                {
                    if (IsAborted) return;
                    ModBase.Log(ex,
                        $"加载线程 {Name} ({Task.CurrentId}) 出错，已完成 {Math.Round(Progress * 100d)}%",
                        ModBase.LogLevel.Developer);
                    Error = ex;
                    State = ModBase.LoadState.Failed;
                }
            }, (CancelToken ??= new CancellationTokenSource()).Token); // 未中断，本次输出有效
            // LastRunningTask.Start(); // 不能使用 RunInNewThread，否则在函数返回前线程就会运行完，导致误判 IsAborted
        }

        public override void Abort()
        {
            if (State != ModBase.LoadState.Loading)
                return;
            lock (LockState)
            {
                State = ModBase.LoadState.Aborted;
            }

            TriggerThreadAbort();
        }

        private void TriggerThreadAbort()
        {
            if (LastRunningTask is null) return;
            if (ModBase.ModeDebug) ModBase.Log($"[Loader] 加载线程 {Name} ({LastRunningTask.Id}) 已中断");
            if (!LastRunningTask.IsCompleted) CancelToken?.Cancel();
            LastRunningTask = null;
            CancelToken = null;
        }
    }

    /// <summary>
    ///     支持多个加载器连续运作的复合加载器。
    /// </summary>
    public class LoaderCombo : LoaderBase
    {
        public object? Input;

        public List<LoaderBase> Loaders = new();

        public LoaderCombo(string Name, IEnumerable<LoaderBase> Loaders)
        {
            this.Loaders.Clear();
            foreach (var Loader in Loaders)
                if (Loader is not null)
                {
                    this.Loaders.Add(Loader);
                    Loader.OnStateChangedThread += SubTaskStateChanged;
                    Loader.HasOnStateChangedThread = true;
                }

            InitParent(null);
            this.Name = Name;
        }

        public override double Progress
        {
            get
            {
                switch (State)
                {
                    case ModBase.LoadState.Waiting:
                    {
                        return 0d;
                    }
                    case ModBase.LoadState.Loading:
                    {
                        var Total = 0d;
                        var Finished = 0d;
                        foreach (var Loader in Loaders)
                        {
                            Total += Loader.ProgressWeight;
                            Finished += Loader.ProgressWeight * Loader.Progress;
                        }

                        if (Total == 0d)
                            return 0d;
                        return Finished / Total;
                    }

                    default:
                    {
                        return 1d;
                    }
                }
            }
            set => throw new Exception("多重加载器不支持设置进度");
        }

        public override void InitParent(LoaderBase Parent)
        {
            this.Parent = Parent;
            foreach (var Loader in Loaders)
                Loader.InitParent(this);
        }

        public override void Start(object Input = null, bool IsForceRestart = false)
        {
            IsForceRestarting = IsForceRestart;
            lock (LockState)
            {
                if (State == ModBase.LoadState.Loading) return;

                State = ModBase.LoadState.Loading;
            }

            // 启动加载
            this.Input = Input;
            if (IsForceRestart)
                foreach (var Loader in Loaders)
                    Loader.State = ModBase.LoadState.Waiting;
            ModBase.RunInThread(Update);
        }

        public override void Abort()
        {
            lock (LockState)
            {
                if (State == ModBase.LoadState.Loading || State == ModBase.LoadState.Waiting)
                    State = ModBase.LoadState.Aborted;
                else
                    return;
            }

            ModBase.RunInThread(() =>
            {
                foreach (var Loader in Loaders) Loader.Abort();
            });
        }

        /// <summary>
        ///     子任务状态变更。
        /// </summary>
        private void SubTaskStateChanged(LoaderBase Loader, ModBase.LoadState NewState, ModBase.LoadState OldState)
        {
            switch (NewState)
            {
                case ModBase.LoadState.Loading:
                {
                    break;
                }
                // 开始，啥都不干
                case ModBase.LoadState.Waiting:
                {
                    break;
                }
                // 子加载器可能由于外部输入改变而暂时变为 Waiting，之后会立即重新启动
                // 所以啥都不干就行
                case ModBase.LoadState.Finished:
                {
                    // 正常结束，触发刷新
                    Update();
                    break;
                }
                case ModBase.LoadState.Aborted:
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
                        if (State >= ModBase.LoadState.Finished)
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
            if (State == ModBase.LoadState.Finished
                || State == ModBase.LoadState.Failed
                || State == ModBase.LoadState.Aborted)
                return;

            var isFinished = true;
            var blocked = false;
            object input = Input;

            foreach (var loader in Loaders)
                switch (loader.State)
                {
                    case ModBase.LoadState.Finished:
                    {
                        if (loader is LoaderTask task)
                        {
                            var genericArg = task.GetType().GenericTypeArguments.FirstOrDefault();
                            var shouldInput = input is not null && genericArg == input.GetType()
                                ? input
                                : null;

                            if (task.ShouldStart(ref shouldInput, false, true))
                            {
                                ModBase.Log("[Loader] 由于输入条件变更，重启已完成的加载器 " + loader.Name);
                                goto Restart;
                            }

                            input = ((dynamic)loader).Output; // 何意味啊，没法匹配 LoaderTask<,>
                        }

                        if (loader.Block && !isFinished)
                            blocked = true;

                        break;
                    }

                    case ModBase.LoadState.Loading:
                    {
                        if (loader is LoaderTask task)
                        {
                            var genericArg = loader.GetType().GenericTypeArguments.FirstOrDefault();
                            var shouldInput = input is not null && genericArg == input.GetType()
                                ? input
                                : null;
                            if (task.ShouldStart(ref shouldInput, false, true))
                            {
                                ModBase.Log($"[Loader] 由于输入条件变更，重启进行中的加载器 {loader.Name}",
                                    ModBase.LogLevel.Developer);
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

                        if (input is not null)
                        {
                            switch (loader)
                            {
                                case LoaderTask:
                                case LoaderCombo:
                                    var genericArg = loader.GetType().GenericTypeArguments.FirstOrDefault();

                                    loader.Start(
                                        genericArg == input.GetType() ? input : null,
                                        IsForceRestarting);
                                    break;
                                case not null:
                                    loader.Start(
                                        input is List<DownloadFile> ? input : null,
                                        IsForceRestarting);
                                    break;
                                default:
                                    throw new Exception($"未知的加载器类型（{loader?.GetType()}）");
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
                State = ModBase.LoadState.Finished;
                ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            }
        }

        /// <summary>
        ///     获得最底层的，应被显示给用户的加载器列表，并追加于 List。
        /// </summary>
        public static void GetLoaderList(LoaderCombo Loader, ref List<LoaderBase> List, bool RequireShow = true)
        {
            foreach (var SubLoader in Loader.Loaders)
            {
                if (SubLoader.Show || !RequireShow)
                    List.Add(SubLoader);
                if (SubLoader is LoaderCombo combo)
                    GetLoaderList(combo, ref List);
            }
        }

        /// <summary>
        ///     获得最底层的，应被显示给用户的加载器列表，并追加于 List。
        /// </summary>
        public void GetLoaderList(ref List<LoaderBase> List, bool RequireShow = true)
        {
            GetLoaderList(this, ref List, RequireShow);
        }

        /// <summary>
        ///     获得最底层的，应被显示给用户的加载器列表。
        /// </summary>
        public List<LoaderBase> GetLoaderList(bool RequireShow = true)
        {
            var List = new List<LoaderBase>();
            GetLoaderList(ref List, RequireShow);
            return List;
        }
    }

    /// <summary>
    ///     支持多个加载器连续运作的复合加载器（泛型版本）。
    /// </summary>
    public class LoaderCombo<InputType> : LoaderCombo
    {
        public new InputType Input;

        public LoaderCombo(string Name, IEnumerable<LoaderBase> Loaders) : base(Name, Loaders) { }

        public override void Start(object Input = null, bool IsForceRestart = false)
        {
            this.Input = Conversions.ToGenericParameter<InputType>(Input);
            base.Start(this.Input, IsForceRestart);
        }
    }

    private struct LoaderFolderDictionaryEntry
    {
        public DateTime? LastCheckTime;
        public string FolderPath;

        public override bool Equals(object obj)
        {
            if (obj is not LoaderFolderDictionaryEntry)
                return false;
            var entry = (LoaderFolderDictionaryEntry)obj;
            return EqualityComparer<DateTime?>.Default.Equals(LastCheckTime, entry.LastCheckTime) &&
                   (FolderPath ?? "") == (entry.FolderPath ?? "");
        }
    }
}
