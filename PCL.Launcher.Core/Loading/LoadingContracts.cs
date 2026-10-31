namespace PCL;

/// <summary>加载器业务状态。</summary>
public enum LoadState
{
    Waiting,
    Loading,
    Finished,
    Failed,
    Aborted
}

/// <summary>加载动画的显示状态。</summary>
public enum LoadingState
{
    Unloaded,
    Stop,
    Run,
    Error
}

/// <summary>指示用户手动取消了操作，或用户已知晓操作被取消的原因。</summary>
public class CancelledException : Exception;

/// <summary>加载动画触发源。</summary>
public interface ILoadingTrigger
{
    delegate void LoadingStateChangedEventHandler(LoadingState newState, LoadingState oldState);

    delegate void ProgressChangedEventHandler(double newProgress, double oldProgress);

    bool IsLoader { get; }
    double Progress { get; }
    Exception? Error { get; }
    LoadingState LoadingState { get; set; }
    event LoadingStateChangedEventHandler? LoadingStateChanged;
    event ProgressChangedEventHandler? ProgressChanged;
}

/// <summary>不依赖 UI 的加载状态模拟器，用于没有真实加载器时驱动动画。</summary>
public class MyLoadingStateSimulator : ILoadingTrigger
{
    private LoadingState _LoadingState { get; set; } = LoadingState.Unloaded;

    public LoadingState LoadingState
    {
        get => _LoadingState;
        set
        {
            if (_LoadingState == value)
                return;
            var oldState = _LoadingState;
            _LoadingState = value;
            LoadingStateChanged?.Invoke(value, oldState);
        }
    }

    public bool IsLoader { get; } = false;

    public double Progress => 0;
    public Exception? Error => null;

    public event ILoadingTrigger.LoadingStateChangedEventHandler? LoadingStateChanged;
    public event ILoadingTrigger.ProgressChangedEventHandler? ProgressChanged;
}