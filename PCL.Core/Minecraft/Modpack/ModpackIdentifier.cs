using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Modpack.Providers;

namespace PCL.Core.Minecraft.Modpack;

/// <summary>
/// 整合包格式识别入口。
/// </summary>
public sealed class ModpackIdentifier
{
    /// <summary>
    /// Provider 的尝试顺序。
    /// <para>
    /// 顺序有实际含义：MCBBS 与 CurseForge 共用 <c>manifest.json</c>，
    /// 必须先让 MCBBS 判定（依据 <c>addons</c> 字段）；MultiMC 排在两者之前，
    /// 因为存在同时含 <c>mmc-pack.json</c> 与 <c>manifest.json</c> 的整合包，
    /// 此时 MultiMC 的组件声明更完整。HMCL 格式排在最后 ——
    /// 其特征文件 <c>modpack.json</c> 过于常见，只在其他格式都不匹配时才采用。
    /// </para>
    /// </summary>
    public static IReadOnlyList<IModpackProvider> DefaultProviders { get; } =
    [
        new MultiMcModpackProvider(),
        new McbbsModpackProvider(),
        new CurseForgeModpackProvider(),
        new ModrinthModpackProvider(),
        new ServerModpackProvider(),
        new HmclModpackProvider()
    ];

    /// <summary>使用默认 Provider 顺序的共享实例。</summary>
    public static ModpackIdentifier Shared { get; } = new();

    private readonly IReadOnlyList<IModpackProvider> _providers;

    public ModpackIdentifier(IReadOnlyList<IModpackProvider>? providers = null)
        => _providers = providers is { Count: > 0 } ? providers : DefaultProviders;

    /// <summary>
    /// 识别压缩包的格式，不做完整解析。
    /// </summary>
    /// <returns>无法识别时返回 <c>null</c>。</returns>
    public IModpackProvider? Identify(ModpackArchive archive)
        => _providers.FirstOrDefault(provider => provider.CanRead(archive));

    /// <summary>
    /// 识别并完整解析压缩包。
    /// </summary>
    /// <param name="archive">已打开的压缩包。</param>
    /// <param name="context">解析期可用的外部依赖。</param>
    /// <exception cref="ModpackFormatNotRecognizedException">所有 Provider 均未识别该压缩包。</exception>
    /// <exception cref="ModpackManifestInvalidException">格式已识别但清单不合法。</exception>
    /// <exception cref="ModpackUnsupportedContentException">整合包要求当前启动器不支持的内容。</exception>
    public async Task<ModpackDescriptor> ReadAsync(
        ModpackArchive archive, ModpackReadContext? context = null, CancellationToken cancellationToken = default)
    {
        var provider = Identify(archive) ?? throw new ModpackFormatNotRecognizedException(archive.FilePath);

        LogWrapper.Info("Modpack", $"识别到整合包格式：{provider.Format.ToDisplayName()}（{archive.FilePath}）");

        var descriptor = await provider
            .ReadAsync(archive, context ?? ModpackReadContext.Offline, cancellationToken)
            .ConfigureAwait(false);

        // 逐条记入日志即可，不要用 Warn：在调试构建中 LogWrapper.Warn 会弹出提示条，
        // 而解析告警是「每个条目一条」的量级，逐条弹窗会直接刷屏。
        // 是否以及如何提示用户，由宿主根据 ModpackDescriptor.Warnings 汇总决定。
        if (descriptor.Warnings.Count > 0)
        {
            LogWrapper.Info("Modpack", $"解析整合包时产生 {descriptor.Warnings.Count} 条提示：");
            foreach (var warning in descriptor.Warnings) LogWrapper.Info("Modpack", $"  {warning}");
        }

        return descriptor;
    }

    /// <summary>
    /// 打开并解析指定路径的整合包文件。
    /// </summary>
    /// <exception cref="ModpackArchiveException">压缩包无法读取。</exception>
    /// <exception cref="ModpackFormatNotRecognizedException">所有 Provider 均未识别该压缩包。</exception>
    public async Task<ModpackDescriptor> ReadAsync(
        string filePath, ModpackReadContext? context = null, CancellationToken cancellationToken = default)
    {
        using var archive = ModpackArchive.Open(filePath);
        return await ReadAsync(archive, context, cancellationToken).ConfigureAwait(false);
    }
}
