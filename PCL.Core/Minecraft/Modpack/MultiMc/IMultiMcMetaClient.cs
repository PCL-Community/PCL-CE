using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Minecraft.Modpack.MultiMc;

/// <summary>
/// MultiMC / Prism 组件元数据的获取入口。
/// </summary>
public interface IMultiMcMetaClient
{
    /// <summary>
    /// 拉取指定组件版本的 JSON Patch。
    /// </summary>
    /// <param name="uid">组件 UID。</param>
    /// <param name="version">组件版本号。</param>
    /// <returns>获取失败（不存在、网络错误、内容非法）时返回 <c>null</c>，不抛出异常。</returns>
    Task<MultiMcPatch?> TryGetPatchAsync(string uid, string version, CancellationToken cancellationToken = default);
}
