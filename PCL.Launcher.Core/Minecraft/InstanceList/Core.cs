namespace PCL;

public static partial class InstanceList
{
    public const int McInstanceCacheVersion = 30;

    private static object? _mcInstanceSelectedMcInstanceSelectedLast = 0;

    /// <summary>
    ///     当前按卡片分类的所有版本列表。
    /// </summary>
    public static Dictionary<McInstanceCardType, List<McInstance>> mcInstanceList = new();

    /// <summary>
    ///     当前的 Minecraft 版本。
    /// </summary>
    public static McInstance? McMcInstanceSelected
    {
        get;
        set
        {
            if (ReferenceEquals(_mcInstanceSelectedMcInstanceSelectedLast, value))
                return;

            field = value;
            LauncherState.SelectedInstance = value;
            LauncherState.NotifyInstanceSelectionChanged(value);
            _mcInstanceSelectedMcInstanceSelectedLast = value;
        }
    }
}