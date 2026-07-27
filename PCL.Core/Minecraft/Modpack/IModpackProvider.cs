using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Minecraft.Modpack.MultiMc;

namespace PCL.Core.Minecraft.Modpack;

/// <summary>
/// 单一整合包格式的识别与解析器。
/// <para>
/// 识别与解析被刻意拆成两步：<see cref="CanRead"/> 只做廉价的特征判断且从不抛异常，
/// <see cref="ReadAsync"/> 才做完整解析并在清单不合法时抛出。
/// 这样 <see cref="ModpackIdentifier"/> 的格式选择不依赖异常控制流，
/// 且「格式已识别但内容有问题」能给出精确报错，而不会被误判成「格式不支持」。
/// </para>
/// </summary>
public interface IModpackProvider
{
    /// <summary>本 Provider 负责的格式。</summary>
    ModpackFormat Format { get; }

    /// <summary>
    /// 判断压缩包是否属于本格式。实现必须廉价、只读，且不抛出异常。
    /// </summary>
    bool CanRead(ModpackArchive archive);

    /// <summary>
    /// 完整解析压缩包，产出归一化描述。
    /// </summary>
    /// <exception cref="ModpackManifestInvalidException">清单缺少必填字段或无法解析。</exception>
    /// <exception cref="ModpackUnsupportedContentException">整合包要求当前启动器不支持的内容。</exception>
    Task<ModpackDescriptor> ReadAsync(
        ModpackArchive archive, ModpackReadContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// 解析期可用的外部依赖。全部可选 —— 缺失时 Provider 退化为纯离线解析。
/// </summary>
public sealed record ModpackReadContext
{
    /// <summary>
    /// MultiMC / Prism 组件元数据客户端。用于补齐整合包未自带、
    /// 且启动器无法自行安装的组件补丁；为 <c>null</c> 时只使用压缩包内的本地补丁。
    /// </summary>
    public IMultiMcMetaClient? MetaClient { get; init; }

    /// <summary>纯离线解析上下文。</summary>
    public static ModpackReadContext Offline { get; } = new();
}
