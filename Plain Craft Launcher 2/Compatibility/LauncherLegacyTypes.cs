namespace PCL;

/// <summary>
///     顶层兼容集合类型，用于逐步移除调用点中的 ModBase 前缀。
/// </summary>
public class SafeList<T> : ModBase.SafeList<T>
{
    public SafeList()
    {
    }

    public SafeList(IEnumerable<T> data) : base(data)
    {
    }
}

/// <summary>
///     顶层兼容集合类型，用于逐步移除调用点中的 ModBase 前缀。
/// </summary>
public class EqualableList<T> : ModBase.EqualableList<T>
{
}