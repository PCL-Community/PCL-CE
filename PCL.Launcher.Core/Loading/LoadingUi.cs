namespace PCL;

/// <summary>任务栏进度状态，与各 UI 框架的枚举解耦。</summary>
public enum TaskbarProgressState
{
    None,
    Normal,
    Error,
    Paused,
    Indeterminate
}

/// <summary>加载框架向 UI 层发出的通知。</summary>
public interface ILoadingUi
{
    void TaskAdded(Loading.LoaderBase loader);
    void TaskRemoved(Loading.LoaderBase loader);
    void TaskRefreshed(Loading.LoaderBase loader);
    void RefreshDownloadButton();
    void UpdateButtonProgress(double progress);
    void UpdateTaskbar(TaskbarProgressState state, double progress);
}

/// <summary>UI 层注册的加载通知实现。</summary>
public static class LoadingUi
{
    public static ILoadingUi? Current { get; set; }
}