using System.Diagnostics;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.Logging;

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
                HintService.Show(Hints.Info(Lang.Text("Watcher.Crash.Hint")));

                Common.FeedbackInfo();
                Common.RunInNewThread(
                    () =>
                    {
                        try
                        {
                            Thread.Sleep(2000);
                            WatcherLog(Lang.Text("Watcher.Crash.AnalysisStart"));
                            var analyzer = new CrashAnalyzer(_pid);
                            analyzer.Collect(version.PathIndie, [.. latestLog]);
                            analyzer.Prepare();
                            analyzer.Analyze(version);
                            analyzer.Output(
                                false,
                                [
                                    version.PathInstance + version.Name + ".json",
                                    LogWrapper.CurrentLogger.CurrentLogFiles.Last(),
                                    Common.ExePath + @"PCL\LatestLaunch.bat"
                                ]);
                        }
                        catch (Exception ex)
                        {
                            Common.Log(
                                ex,
                                "崩溃分析失败",
                                Common.LogLevel.Feedback,
                                userSummary: Lang.Text("Crash.Analysis.Error.Failed"));
                        }
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
            Common.RunInNewThread(() =>
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
                        var taskkillInfo = new ProcessStartInfo
                        {
                            FileName = "taskkill.exe",
                            Arguments = $"/PID {gameProcess.Id} /F /T",
                            RedirectStandardOutput = true,
                            UseShellExecute = false
                        };
                        var taskkillProcess = Process.Start(taskkillInfo);
                        var output = taskkillProcess?.StandardOutput.ReadToEnd() ?? string.Empty;
                        WatcherLog(Lang.Text("Watcher.Kill.TaskkillResult", output));
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

                    GameExit?.Invoke();
                }
                catch (Exception ex)
                {
                    Common.Log(
                        ex,
                        Lang.Text("Watcher.Kill.Failed"),
                        Common.LogLevel.Hint,
                        userSummary: Lang.Text("Watcher.Kill.Failed"));
                }
            });
        }

        public List<string> ExportStackDump()
        {
            var dump = new List<string>();
            for (var i = 1; i <= 3; i++)
            {
                dump.Add(Common.ShellAndGetOutput(jStackPath, "-l -e " + gameProcess.Id));
                Thread.Sleep(3000);
            }

            return dump;
        }

        private delegate bool EnumWindowsSub(nint hwnd, nint lParam);
    }
}