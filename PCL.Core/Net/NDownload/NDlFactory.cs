namespace PCL.Core.Net.NDownload;

/// <summary>
/// 无泛型的下载器构建工厂。
/// </summary>
public abstract class NDlFactory
{
    /// <summary>
    /// 新建连接。
    /// </summary>
    /// <param name="resId">资源 ID</param>
    /// <returns>下载连接</returns>
    public abstract IDlConnection? CreateConnection(string resId);

    /// <summary>
    /// 新建写入器。
    /// </summary>
    /// <param name="resId">资源 ID</param>
    /// <returns>下载写入器</returns>
    public abstract IDlWriter? CreateWriter(string resId);
}

/// <summary>
/// 下载器构建工厂。
/// </summary>
/// <typeparam name="TSourceArgument">下载源参数类型</typeparam>
/// <typeparam name="TTargetArgument">写入目标参数类型</typeparam>
public abstract class NDlFactory<TSourceArgument, TTargetArgument> : NDlFactory
{
    /// <summary>
    /// 下载源映射。
    /// </summary>
    protected abstract IDlResourceMapping<TSourceArgument> SourceMapping { get; }

    /// <summary>
    /// 写入目标映射。
    /// </summary>
    protected abstract IDlResourceMapping<TTargetArgument> TargetMapping { get; }

    /// <summary>
    /// 新建连接。
    /// </summary>
    /// <param name="source">下载源参数</param>
    /// <returns>下载连接</returns>
    protected abstract IDlConnection CreateConnection(TSourceArgument source);

    public override IDlConnection? CreateConnection(string resId)
    {
        var source = SourceMapping.Parse(resId);
        return (source == null) ? null : CreateConnection(source);
    }

    /// <summary>
    /// 新建写入器。
    /// </summary>
    /// <param name="target">写入目标</param>
    /// <returns>下载写入器</returns>
    protected abstract IDlWriter CreateWriter(TTargetArgument target);

    public override IDlWriter? CreateWriter(string resId)
    {
        var target = TargetMapping.Parse(resId);
        return (target == null) ? null : CreateWriter(target);
    }
}
