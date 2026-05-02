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
    // 任务栏进度条
    public static ModBase.SafeList<LoaderBase> LoaderTaskbar = new();
    public static double LoaderTaskbarProgress; // 平滑后的进度
    private static TaskbarItemProgressState LoaderTaskbarProgressLast = TaskbarItemProgressState.None;

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

    public abstract partial class LoaderBase
    {
        private double _Progress = -1;

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

        public event ILoadingTrigger.ProgressChangedEventHandler? ProgressChanged;

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
            else if (Error == null)
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
            else if (Error == null)
            {
                throw new Exception("未知错误！");
            }
            else
            {
                throw Error;
            }
        }
    }

    public partial class LoaderCombo
    {
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
    }
}
