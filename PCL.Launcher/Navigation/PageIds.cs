namespace PCL.Navigation;

/// <summary>顶级页面的稳定标识。</summary>
public readonly record struct PageId(string Value)
{
    public bool IsEmpty => string.IsNullOrEmpty(Value);

    public override string ToString()
    {
        return Value;
    }
}

/// <summary>顶级页面 ID 的常量集合。值同时用于日志、调试入口和自定义事件的外部名称。</summary>
public static class PageIds
{
    public static readonly PageId Launch = new("Launch");
    public static readonly PageId Download = new("Download");
    public static readonly PageId Tools = new("Tools");
    public static readonly PageId Setup = new("Setup");
    public static readonly PageId InstanceSelect = new("InstanceSelect");
    public static readonly PageId TaskManager = new("TaskManager");
    public static readonly PageId InstanceSetup = new("InstanceSetup");
    public static readonly PageId CompDetail = new("CompDetail");
    public static readonly PageId HelpDetail = new("HelpDetail");
    public static readonly PageId GameLog = new("GameLog");
    public static readonly PageId VersionSaves = new("VersionSaves");
}