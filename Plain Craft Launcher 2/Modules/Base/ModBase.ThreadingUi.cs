namespace PCL;

public static partial class ModBase
{
    public static int GetUuid() => LauncherDispatcher.GetUuid();
    public static Thread RunInNewThread(Action Action, string? Name = null,
        ThreadPriority Priority = ThreadPriority.Normal) => LauncherDispatcher.RunInNewThread(Action, Name, Priority);
    public static Output RunInUiWait<Output>(Func<Output> Action) => LauncherDispatcher.RunInUiWait(Action);
    public static void RunInUiWait(Action Action) => LauncherDispatcher.RunInUiWait(Action);
    public static void RunInUi(Action Action, bool ForceWaitUntilLoaded = false) => LauncherDispatcher.RunInUi(Action, ForceWaitUntilLoaded);
    public static bool RunInUi() => LauncherDispatcher.RunInUi();
}
