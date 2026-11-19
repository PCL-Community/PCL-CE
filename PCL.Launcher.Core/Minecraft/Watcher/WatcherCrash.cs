using System.Diagnostics;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL;

public static partial class GameWatcher
{
    public partial class Watcher
    {
        private void Crashed()
        {
            if (State is MinecraftState.Crashed or MinecraftState.Ended)
                return;

            State = MinecraftState.Crashed;
            if (!Config.Launch.DisableCrashAnalysis)
            {
                WatcherLog(Lang.Text("Watcher.Crash.Detected"));
                WatcherUi.Current?.ShowHint(Lang.Text("Watcher.Crash.Hint"));

                WatcherUi.Current?.ShowFeedback();
                LauncherCommon.RunInNewThread(
                    () =>
                    {
                        WatcherLog(Lang.Text("Watcher.Crash.AnalysisStart"));
                        CrashAnalysisService.Current?.Analyze(
                            _pid,
                            version,
                            version.PathIndie,
                            [.. latestLog],
                            [
                                Path.Combine(version.PathInstance, $"{version.Name}.json"),
                                LogWrapper.CurrentLogger.CurrentLogFiles.Last(),
                                Path.Combine(
                                    Paths.Data,
                                    "LatestLaunch.bat")
                            ]);
                    },
                    "Crash Analyzer");
            }
            else
            {
                WatcherLog(Lang.Text("Watcher.Crash.DetectedDisabled"));
            }
        }

        public static bool CheckAlive(Process p)
        {
            return !p.HasExited
                || Array.Exists(
                    Process.GetProcesses(),
                    item => item.Id == p.Id);
        }

        public void Kill()
        {
            State = MinecraftState.Canceled;
            LauncherCommon.RunInNewThread(() =>
            {
                WatcherLog(Lang.Text("Watcher.Kill.Attempt"));
                try
                {
                    if (CheckAlive(gameProcess))
                        gameProcess.Kill();

                    gameProcess.WaitForExit(5000);
                    if (CheckAlive(gameProcess))
                    {
                        WatcherLog(Lang.Text("Watcher.Kill.TaskkillAttempt"));
                        gameProcess.Kill(true);
                        gameProcess.WaitForExit(5000);
                        if (CheckAlive(gameProcess))
                        {
                            WatcherLog(Lang.Text("Watcher.Kill.Timeout"));
                            return;
                        }
                    }

                    WatcherLog(Lang.Text("Watcher.Kill.Success"));
                    if (RealTimeLog)
                    {
                        var arglevel = GameLogLevel.Info;
                        LogRealTime(
                            Lang.Text("Watcher.ProcessExited", gameProcess.ExitCode),
                            ref arglevel);
                    }

                    RaiseGameExit();
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(
                        ex,
                        Lang.Text("Watcher.Kill.Failed"),
                        LauncherLogLevel.Hint,
                        userSummary: Lang.Text("Watcher.Kill.Failed"));
                }
            });
        }

        public List<string> ExportStackDump()
        {
            var dump = new List<string>();
            for (var i = 1; i <= 3; i++)
            {
                var output = "";
                try
                {
                    var runner = ApplicationRuntime.GetService<IExternalCommandRunner>();
                    if (runner is not null)
                        output = runner
                            .RunAsync(
                                jStackPath,
                                ["-l", "-e", gameProcess.Id.ToString()],
                                TimeSpan.FromSeconds(30))
                            .GetAwaiter()
                            .GetResult()
                            .StandardOutput;
                }
                catch
                {
                    // 堆栈导出失败不应影响崩溃报告。
                }

                dump.Add(output);
                Thread.Sleep(3000);
            }

            return dump;
        }
    }
}