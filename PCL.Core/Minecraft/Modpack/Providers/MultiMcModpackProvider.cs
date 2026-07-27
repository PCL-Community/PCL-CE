using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Minecraft.Modpack.Manifests;
using PCL.Core.Minecraft.Modpack.Model;
using PCL.Core.Minecraft.Modpack.MultiMc;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Modpack.Providers;

/// <summary>
/// MultiMC / Prism Launcher 格式解析器。特征文件为 <c>mmc-pack.json</c> 与 <c>instance.cfg</c>。
/// </summary>
public sealed class MultiMcModpackProvider : IModpackProvider
{
    /// <summary>组件清单文件名。</summary>
    public const string PackFileName = "mmc-pack.json";

    /// <summary>实例配置文件名。</summary>
    public const string InstanceConfigFileName = "instance.cfg";

    /// <summary>本地补丁目录。</summary>
    private const string PatchesDirectory = "patches";

    /// <summary>覆写目录 —— MultiMC 把游戏目录整体放在 <c>.minecraft</c> 下。</summary>
    private const string MinecraftDirectory = ".minecraft";

    /// <summary>内嵌库文件目录。</summary>
    private const string LibrariesDirectory = "libraries";

    /// <summary>JAR Mod 目录。</summary>
    private const string JarModsDirectory = "jarmods";

    public ModpackFormat Format => ModpackFormat.MultiMc;

    public bool CanRead(ModpackArchive archive)
        => archive.HasEntry(PackFileName) || archive.HasEntry(InstanceConfigFileName);

    public async Task<ModpackDescriptor> ReadAsync(
        ModpackArchive archive, ModpackReadContext context, CancellationToken cancellationToken = default)
    {
        var warnings = new List<string>();
        var config = _ReadInstanceConfig(archive);
        var components = _ReadComponents(archive);

        var gameVersion = _ResolveGameVersion(components, config)
                          ?? throw new ModpackManifestInvalidException(
                              Format, PackFileName, $"未能确定 Minecraft 版本（缺少 {MultiMcComponentCatalog.GameUid} 组件）");

        var loaders = _ResolveLoaders(components, warnings);
        var modpackComponents = new ModpackComponents(gameVersion, loaders);
        modpackComponents.EnsureInstallable();

        var versionPatch = await _BuildVersionPatchAsync(archive, components, context, warnings, cancellationToken)
            .ConfigureAwait(false);

        return new ModpackDescriptor
        {
            Format = Format,
            Metadata = new ModpackMetadata(
                Name: config?.GetString("name"),
                Description: config?.GetString("notes")),
            Components = modpackComponents,
            Overrides = archive.HasDirectory(MinecraftDirectory)
                ? [ModpackOverride.ToInstanceRoot(MinecraftDirectory)]
                : [],
            EmbeddedPayloads = _CollectPayloads(archive),
            LaunchOptions = _ParseLaunchOptions(archive, config),
            VersionPatch = versionPatch,
            RawManifest = archive.HasEntry(PackFileName)
                ? JsonCompat.ParseNode(archive.ReadAllText(PackFileName))
                : null,
            Warnings = warnings
        };
    }

    private static MultiMcInstanceConfig? _ReadInstanceConfig(ModpackArchive archive)
        => archive.HasEntry(InstanceConfigFileName)
            ? MultiMcInstanceConfig.Parse(archive.ReadAllText(InstanceConfigFileName))
            : null;

    private List<MultiMcComponent> _ReadComponents(ModpackArchive archive)
    {
        if (!archive.HasEntry(PackFileName)) return [];

        try
        {
            return archive.ReadJson<MultiMcPack>(PackFileName)?.Components ?? [];
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new ModpackManifestInvalidException(Format, PackFileName, "不是合法的 JSON", ex);
        }
    }

    /// <summary>
    /// 确定 Minecraft 版本。优先取 <c>mmc-pack.json</c> 的组件，
    /// 回退到 <c>instance.cfg</c> 的 <c>IntendedVersion</c>（旧版 MultiMC 无 mmc-pack.json）。
    /// </summary>
    private static string? _ResolveGameVersion(List<MultiMcComponent> components, MultiMcInstanceConfig? config)
    {
        var declared = components
            .FirstOrDefault(component =>
                MultiMcComponentCatalog.GetRole(component.Uid) == MultiMcComponentRole.Game)
            ?.ResolveVersion();

        return !string.IsNullOrWhiteSpace(declared) ? declared : config?.GetString("IntendedVersion");
    }

    private static List<ModpackLoader> _ResolveLoaders(List<MultiMcComponent> components, List<string> warnings)
    {
        var loaders = new List<ModpackLoader>();

        foreach (var component in components)
        {
            var role = MultiMcComponentCatalog.GetRole(component.Uid);
            if (role is MultiMcComponentRole.Game or MultiMcComponentRole.LauncherManaged) continue;

            var version = component.ResolveVersion();
            if (string.IsNullOrWhiteSpace(version)) continue;

            if (role == MultiMcComponentRole.Unknown)
            {
                warnings.Add($"整合包声明了未知组件「{component.Uid}」（版本 {version}），将仅应用其本地补丁");
                continue;
            }

            var kind = MultiMcComponentCatalog.ResolveLoader(component.Uid, version);
            if (kind is not null) loaders.Add(new ModpackLoader(kind.Value, version));
        }

        return loaders;
    }

    private static List<ModpackEmbeddedPayload> _CollectPayloads(ModpackArchive archive)
    {
        var payloads = new List<ModpackEmbeddedPayload>(2);

        if (archive.HasDirectory(LibrariesDirectory))
            payloads.Add(new ModpackEmbeddedPayload(ModpackPayloadKind.Libraries, LibrariesDirectory));

        if (archive.HasDirectory(JarModsDirectory))
            payloads.Add(new ModpackEmbeddedPayload(ModpackPayloadKind.JarMods, JarModsDirectory));

        return payloads;
    }

    /// <summary>
    /// 构建版本 JSON 补丁。
    /// <para>
    /// <b>应用策略</b>：只合并「启动器自身不会安装」的组件补丁，即 UID 未知的自定义组件。
    /// Minecraft 与各加载器由 PCL 按官方渠道安装并生成完整的实例 JSON，
    /// 而 MultiMC / Prism 的同名组件补丁描述的是另一套启动方案
    /// （例如 Forge 经 ForgeWrapper 启动、库文件来自 Prism 自建 Maven），
    /// 两者叠加会产生互相冲突的 mainClass 与类路径。因此这些补丁被跳过并记录提示，
    /// 而不是强行合并出一份无法启动的 JSON。
    /// </para>
    /// </summary>
    private static async Task<ModpackVersionPatch?> _BuildVersionPatchAsync(
        ModpackArchive archive,
        List<MultiMcComponent> components,
        ModpackReadContext context,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var localPatches = _ReadLocalPatches(archive);
        var applicable = new List<MultiMcPatch>();

        // 按 mmc-pack.json 中的组件顺序应用 —— 这是 MultiMC 的权威顺序
        foreach (var component in components)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var uid = component.Uid;
            if (string.IsNullOrWhiteSpace(uid)) continue;

            var role = MultiMcComponentCatalog.GetRole(uid);
            if (role is not MultiMcComponentRole.Unknown)
            {
                if (localPatches.ContainsKey(uid))
                    warnings.Add($"整合包自带了组件「{uid}」的版本补丁，PCL 将改用官方渠道安装该组件");
                continue;
            }

            if (localPatches.TryGetValue(uid, out var local))
            {
                applicable.Add(local);
                continue;
            }

            var version = component.ResolveVersion();
            if (context.MetaClient is null || string.IsNullOrWhiteSpace(version))
            {
                warnings.Add($"整合包缺少自定义组件「{uid}」的定义，该组件将被忽略");
                continue;
            }

            var remote = await context.MetaClient
                .TryGetPatchAsync(uid, version, cancellationToken)
                .ConfigureAwait(false);

            if (remote is not null) applicable.Add(remote);
            else warnings.Add($"未能获取自定义组件「{uid}」（版本 {version}）的定义，该组件将被忽略");
        }

        // 未在组件列表中登记、但存在于 patches/ 的补丁按 order 追加
        var declaredUids = components
            .Select(component => component.Uid)
            .Where(uid => !string.IsNullOrWhiteSpace(uid))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        applicable.AddRange(localPatches.Values
            .Where(patch => !declaredUids.Contains(patch.Uid))
            .Where(patch => MultiMcComponentCatalog.GetRole(patch.Uid) == MultiMcComponentRole.Unknown)
            .OrderBy(patch => patch.Order));

        // 补丁作为增量叠加到 PCL 生成的实例 JSON 上，因此不产出自包含的完整 JSON
        return MultiMcPatchMerger.Merge(applicable, selfContained: false);
    }

    private static Dictionary<string, MultiMcPatch> _ReadLocalPatches(ModpackArchive archive)
    {
        var patches = new Dictionary<string, MultiMcPatch>(StringComparer.OrdinalIgnoreCase);
        if (!archive.HasDirectory(PatchesDirectory)) return patches;

        foreach (var item in archive.EnumerateFiles(PatchesDirectory))
        {
            if (!item.RelativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

            try
            {
                var content = archive.ReadAllText($"{PatchesDirectory}/{item.RelativePath}");
                var fallbackUid = item.RelativePath[..^".json".Length];
                var patch = MultiMcPatch.TryCreate(
                    JsonCompat.ParseNode(content), MultiMcPatchSource.Local, fallbackUid);

                if (patch is not null) patches[patch.Uid] = patch;
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or ModpackArchiveException)
            {
                // 单个补丁损坏不应导致整个整合包无法安装
            }
        }

        return patches;
    }

    /// <summary>
    /// 从 <c>instance.cfg</c> 提取实例设置。
    /// <para>
    /// 命令类字段中的 MultiMC 占位符会被翻译为 PCL 的等价写法。
    /// </para>
    /// </summary>
    private static ModpackLaunchOptions _ParseLaunchOptions(ModpackArchive archive, MultiMcInstanceConfig? config)
    {
        if (config is null) return ModpackLaunchOptions.None;

        var jvmArgs = config.GetOverridden("OverrideJavaArgs", "JvmArgs");
        var overrideMemory = config.GetBoolean("OverrideMemory");
        var joinServer = config.GetBoolean("JoinServerOnLaunch");

        return new ModpackLaunchOptions
        {
            JvmArguments = jvmArgs is null ? [] : [jvmArgs],
            MinMemoryMegabytes = overrideMemory ? config.GetInt32("MinMemAlloc") : null,
            MaxMemoryMegabytes = overrideMemory ? config.GetInt32("MaxMemAlloc") : null,
            JavaPath = config.GetOverridden("OverrideJavaLocation", "JavaPath"),
            PreLaunchCommand = _TranslateCommand(config.GetOverridden("OverrideCommands", "PreLaunchCommand")),
            PostExitCommand = _TranslateCommand(config.GetOverridden("OverrideCommands", "PostExitCommand")),
            WrapperCommand = _TranslateCommand(config.GetOverridden("OverrideCommands", "WrapperCommand")),
            ServerToJoin = joinServer ? config.GetString("JoinServerOnLaunchAddress") : null,
            IgnoreJavaCompatibility = config.GetBoolean("IgnoreJavaCompatibility") ? true : null,
            IconArchivePath = _ResolveIconPath(archive, config),
            Notes = config.GetString("notes")
        };
    }

    /// <summary>
    /// 把 MultiMC 的命令占位符翻译为 PCL 的写法。
    /// </summary>
    private static string? _TranslateCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;

        return command
            .Replace("$INST_JAVA", "{java}java.exe")
            .Replace("$INST_MC_DIR", "{minecraft}")
            .Replace("$INST_DIR", "{verpath}")
            .Replace("$INST_ID", "{name}")
            .Replace("$INST_NAME", "{name}");
    }

    /// <summary>
    /// 定位实例图标。<c>iconKey</c> 不含扩展名，需在逻辑根下探测常见图片格式。
    /// </summary>
    private static string? _ResolveIconPath(ModpackArchive archive, MultiMcInstanceConfig config)
    {
        var iconKey = config.GetString("iconKey");
        if (string.IsNullOrWhiteSpace(iconKey) || iconKey == "default") return null;

        // iconKey 可能含路径分隔符，只取文件名部分
        var name = iconKey.Replace('\\', '/').Split('/')[^1];
        if (name.Length == 0) return null;

        foreach (var extension in _IconExtensions)
        {
            var candidate = $"{name}{extension}";
            if (archive.HasEntry(candidate)) return candidate;
        }

        return null;
    }

    private static readonly string[] _IconExtensions = [".png", ".jpg", ".jpeg", ".webp", ".ico"];
}
