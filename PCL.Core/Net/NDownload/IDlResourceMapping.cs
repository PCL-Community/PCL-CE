namespace PCL.Core.Net.NDownload;

/// <summary>
/// 资源 ID 映射。
/// </summary>
/// <typeparam name="TMappingValue">映射目标类型</typeparam>
public interface IDlResourceMapping<out TMappingValue>
{
    public TMappingValue? Parse(string resId);
}
