using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI.Dialogs;
using PCL.Network;

namespace PCL.Views.Shell;

public sealed class ExitFlow
{
    private const string ExitAnimationKey = "MainWindow Exit";

    private readonly Window _window;

    private bool _running;

    public ExitFlow(Window window)
    {
        _window = window;
        window.Closing += OnClosing;
    }

    public bool IsExiting { get; private set; }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (IsExiting) return;

        e.Cancel = true;
        if (_running) return;
        _running = true;

        try
        {
            if (!ConfirmDownloadingTasks())
            {
                _running = false;
                return;
            }

            IsExiting = true;

            if (UpdateManager.isUpdateWaitingRestart)
                try
                {
                    UpdateManager.UpdateRestart(false, false);
                }
                catch (Exception ex)
                {
                    LogWrapper.Error(ex, "UI", "退出时启动更新程序失败");
                }

            LogWrapper.Info("UI", "收到关闭指令");
            PlayExitAnimation();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", "退出流程出错，直接关闭程序");
            ShutdownApplication();
        }
    }

    private static bool ConfirmDownloadingTasks()
    {
        if (!NetFacade.HasDownloadingTask()) return true;

        var button = DialogService.Instance.Show(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Common.Dialog.Title"),
                        Content = Lang.Text("Main.Exit.HasDownloadingTask"),
                        Buttons = DialogButtons.OkCancel,
                        ForceWait = true
                    })
                .Result?.Button
            ?? 0;
        if (button != 1) return false;

        Basics.RunInNewThread(
            () =>
            {
                LogWrapper.Info("UI", "正在强行停止任务");
                foreach (var task in Loading.loaderTaskbar.ToList()) task.Abort();
            },
            "ExitAbortTasks");
        return true;
    }

    private void PlayExitAnimation()
    {
        _window.IsHitTestVisible = false;
        if (_window.Content is not Control root || !AnimationService.IsEnabled || !root.IsLoaded)
        {
            ShutdownApplication();
            return;
        }

        root.RenderTransformOrigin = RelativePoint.Center;
        var scale = new ScaleTransform(1d, 1d);
        var translate = new TranslateTransform();
        var rotate = new RotateTransform();
        var group = new TransformGroup();
        group.Children.Add(rotate);
        group.Children.Add(translate);
        group.Children.Add(scale);
        root.RenderTransform = group;

        AnimationService.Run(
            new List<IAnimation>
            {
                new OpacityAnimation(
                    root,
                    -root.Opacity,
                    140,
                    40,
                    new FluentEaseOut(EasePower.Weak)),
                new DoubleAnimation(
                    value =>
                    {
                        scale.ScaleX += value;
                        scale.ScaleY += value;
                    },
                    0.88d - scale.ScaleX,
                    180),
                new DoubleAnimation(
                    value => translate.Y += value,
                    20d - translate.Y,
                    180,
                    0,
                    new FluentEaseOut(EasePower.Weak)),
                new DoubleAnimation(
                    value => rotate.Angle += value,
                    0.6d - rotate.Angle,
                    180,
                    0,
                    new FluentEaseInOut(EasePower.Weak)),
                new ActionAnimation(
                    () =>
                    {
                        _window.ShowInTaskbar = false;
                        _window.Hide();
                    },
                    210),
                new ActionAnimation(ShutdownApplication, 230)
            },
            ExitAnimationKey);
    }

    private void ShutdownApplication()
    {
        try
        {
            if (Lifecycle.CurrentState >= LifecycleState.Running)
            {
                Lifecycle.Shutdown();
                return;
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", "调用生命周期关闭失败，改用强制退出");
        }

        if (Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
        else
            Environment.Exit(0);
    }
}