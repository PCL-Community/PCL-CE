namespace PCL;

/// <summary>业务层共享的启动器运行状态。</summary>
public static class LauncherState
{
    /// <summary>当前选中的 Minecraft 实例。</summary>
    public static McInstance? SelectedInstance { get; set; }

    /// <summary>选中实例发生变化时触发，供 UI 清理缓存。</summary>
    public static event Action<McInstance?>? InstanceSelectionChanged;

    public static void NotifyInstanceSelectionChanged(McInstance? instance)
    {
        InstanceSelectionChanged?.Invoke(instance);
    }
}