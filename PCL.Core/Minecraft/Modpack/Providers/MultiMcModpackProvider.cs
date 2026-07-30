using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Minecraft.Modpack.Manifests;
using PCL.Core.Minecraft.Modpack.Model;
using PCL.Core.Minecraft.Modpack.MultiMc;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Modpack.Providers;

/// <summary>
/// MultiMC / Prism Launcher 格式解析器。以 <c>instance.cfg</c> 所在目录作为实例根，
/// 并按 <c>mmc-pack.json</c> 的组件顺序与依赖关系构建最终版本补丁。
/// </summary>
public sealed class MultiMcModpackProvider : IModpackProvider
{
    public const string PackFileName = "mmc-pack.json";
    public const string InstanceConfigFileName = "instance.cfg";

    private const string PatchesDirectory = "patches";
    private const string MinecraftDirectory = ".minecraft";
    private const string LibrariesDirectory = "libraries";
    private const string JarModsDirectory = "jarmods";

    public ModpackFormat Format => ModpackFormat.MultiMc;

    public bool CanRead(ModpackArchive archive) => _FindInstanceRoots(archive).Count > 0;

    public async Task<ModpackDescriptor> ReadAsync(
        ModpackArchive archive, ModpackReadContext context, CancellationToken cancellationToken = default)
    {
        var roots = _FindInstanceRoots(archive);
        if (roots.Count == 0)
            throw new ModpackManifestInvalidException(Format, InstanceConfigFileName, "文件缺失");
        if (roots.Count > 1)
            throw new ModpackManifestInvalidException(
                Format, InstanceConfigFileName, $"压缩包内包含多个实例目录：{string.Join("、", roots)}");

        var root = roots[0];
        var configPath = _At(root, InstanceConfigFileName);
        var packPath = _At(root, PackFileName);
        var warnings = new List<string>();
        var config = MultiMcInstanceConfig.Parse(archive.ReadAllText(configPath));
        var pack = _ReadPack(archive, packPath);
        var localPatches = _ReadLocalPatches(archive, root);

        var sourceComponents = pack?.Components ?? localPatches.Values
            .OrderBy(patch => patch.Order)
            .Select(patch => new MultiMcComponent
            {
                Uid = patch.Uid,
                Version = patch.Version
            })
            .ToList();
        var gameVersionHint = sourceComponents.FirstOrDefault(component =>
                !component.Disabled &&
                MultiMcComponentCatalog.GetRole(component.Uid) == MultiMcComponentRole.Game)
            ?.ResolveVersion();
        if (string.IsNullOrWhiteSpace(gameVersionHint) && pack is null)
            gameVersionHint = config.GetString("IntendedVersion");

        var resolved = await _ResolveComponentsAsync(
                sourceComponents, localPatches, context, packPath, gameVersionHint, warnings, cancellationToken)
            .ConfigureAwait(false);

        if (pack is not null)
        {
            var declaredUids = sourceComponents
                .Select(component => component.Uid)
                .Where(uid => !string.IsNullOrWhiteSpace(uid))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var patch in localPatches.Values.Where(patch => !declaredUids.Contains(patch.Uid)))
                warnings.Add($"本地补丁「{patch.Uid}」未在 mmc-pack.json 中声明，已按 MultiMC 规范忽略");
        }

        var gameVersion = _ResolveGameVersion(resolved, config, pack is null)
                          ?? throw new ModpackManifestInvalidException(
                              Format, pack is null ? configPath : packPath,
                              $"未能确定 Minecraft 版本（缺少 {MultiMcComponentCatalog.GameUid} 组件）");

        var loaders = _ResolveLoaders(resolved, gameVersion, warnings);
        var modpackComponents = new ModpackComponents(gameVersion, loaders);
        modpackComponents.EnsureInstallable();

        var versionPatch = _BuildVersionPatch(archive, root, resolved, gameVersion, packPath, warnings);
        var launchOptions = _ParseLaunchOptions(archive, root, config, configPath, warnings);

        return new ModpackDescriptor
        {
            Format = Format,
            Metadata = new ModpackMetadata(
                Name: config.GetString("name"),
                Description: config.GetString("notes")),
            Components = modpackComponents,
            Overrides = archive.HasDirectory(_At(root, MinecraftDirectory))
                ? [ModpackOverride.ToInstanceRoot(_At(root, MinecraftDirectory))]
                : [],
            EmbeddedPayloads = _CollectPayloads(
                archive, root, versionPatch?.JarMods ?? []),
            LaunchOptions = launchOptions,
            VersionPatch = versionPatch,
            RawManifest = pack is not null
                ? JsonCompat.ParseNode(archive.ReadAllText(packPath))
                : null,
            Warnings = warnings
        };
    }

    private MultiMcPack? _ReadPack(ModpackArchive archive, string packPath)
    {
        if (!archive.HasEntry(packPath)) return null;

        MultiMcPack? pack;
        try
        {
            pack = archive.ReadJson<MultiMcPack>(packPath);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new ModpackManifestInvalidException(Format, packPath, "不是合法的 JSON", ex);
        }

        if (pack is null)
            throw new ModpackManifestInvalidException(Format, packPath, "文件内容为空");
        if (pack.FormatVersion != 1)
            throw new ModpackManifestInvalidException(
                Format, packPath, $"不支持 formatVersion={pack.FormatVersion}，MultiMC 组件清单必须为版本 1");
        if (pack.Components is null)
            throw new ModpackManifestInvalidException(Format, packPath, "缺少 components 数组");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in pack.Components)
        {
            if (string.IsNullOrWhiteSpace(component.Uid))
                throw new ModpackManifestInvalidException(Format, packPath, "组件缺少 uid");

            component.Uid = component.Uid.Trim();
            if (!seen.Add(component.Uid))
                throw new ModpackManifestInvalidException(
                    Format, packPath, $"组件 uid 重复：{component.Uid}");
        }

        return pack;
    }

    private static async Task<IReadOnlyList<ResolvedComponent>> _ResolveComponentsAsync(
        IReadOnlyList<MultiMcComponent> components,
        IReadOnlyDictionary<string, MultiMcPatch> localPatches,
        ModpackReadContext context,
        string manifestPath,
        string? gameVersionHint,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var allDeclared = components
            .Where(component => !string.IsNullOrWhiteSpace(component.Uid))
            .ToDictionary(component => component.Uid!, StringComparer.OrdinalIgnoreCase);
        var enabled = components
            .Where(component => !component.Disabled)
            .ToDictionary(component => component.Uid!, StringComparer.OrdinalIgnoreCase);
        var disabled = components
            .Where(component => component.Disabled)
            .Select(component => component.Uid!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var resolvedByUid = new Dictionary<string, ResolvedComponent>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<ResolvedComponent>();
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        ResolvedComponent GetOrCreate(MultiMcComponent component, bool declared)
        {
            var uid = component.Uid!.Trim();
            if (resolvedByUid.TryGetValue(uid, out var existing)) return existing;

            localPatches.TryGetValue(uid, out var localPatch);
            var declaredVersion = component.ResolveVersion();
            var patchVersion = localPatch?.Version;
            if (!string.IsNullOrWhiteSpace(declaredVersion) && !string.IsNullOrWhiteSpace(patchVersion) &&
                !string.Equals(declaredVersion, patchVersion, StringComparison.OrdinalIgnoreCase))
                throw _Invalid(manifestPath,
                    $"组件「{uid}」的清单版本 {declaredVersion} 与本地补丁版本 {patchVersion} 不一致");

            var result = new ResolvedComponent(
                component,
                uid,
                !string.IsNullOrWhiteSpace(declaredVersion) ? declaredVersion!.Trim() : patchVersion?.Trim(),
                localPatch,
                declared);
            resolvedByUid.Add(uid, result);
            return result;
        }

        async Task EnsurePatchAsync(ResolvedComponent component, bool required)
        {
            if (component.Patch is not null)
            {
                _ValidatePatchIdentity(component, manifestPath);
                return;
            }

            var role = MultiMcComponentCatalog.GetRole(component.Uid);
            var loader = MultiMcComponentCatalog.ResolveLoader(component.Uid, component.Version);
            var unsupportedLoader = role == MultiMcComponentRole.ModLoader &&
                                    loader is { } kind && !_CanInstallWithPcl(kind, gameVersionHint);
            // 本地补丁优先；联网安装时仍应获取游戏与已知加载器的官方组件补丁，
            // 其中可能包含 mainJar、jarMods、mavenFiles 或启动参数等 PCL 安装器没有的信息。
            var shouldFetch = required || context.MetaClient is not null;
            if (!shouldFetch) return;

            if (string.IsNullOrWhiteSpace(component.Version))
                throw _Invalid(manifestPath, $"组件「{component.Uid}」缺少版本号，无法获取组件定义");
            if (context.MetaClient is null)
                throw _Invalid(manifestPath,
                    $"整合包未包含组件「{component.Uid}」{component.Version} 的本地补丁，且当前无法联网获取元数据");

            component.Patch = await context.MetaClient
                .TryGetPatchAsync(component.Uid, component.Version, cancellationToken)
                .ConfigureAwait(false);

            if (component.Patch is null)
            {
                if (required || role is MultiMcComponentRole.Unknown or MultiMcComponentRole.LauncherManaged ||
                    unsupportedLoader)
                    throw _Invalid(manifestPath,
                        $"未能获取组件「{component.Uid}」{component.Version} 的定义");
                return;
            }

            _ValidatePatchIdentity(component, manifestPath);
            component.Version ??= component.Patch.Version;
        }

        async Task ResolveAsync(ResolvedComponent component, bool requirePatch)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (completed.Contains(component.Uid))
            {
                var hadPatch = component.Patch is not null;
                await EnsurePatchAsync(component, requirePatch).ConfigureAwait(false);
                if (!hadPatch && component.Patch is not null)
                {
                    completed.Remove(component.Uid);
                    ordered.Remove(component);
                    await ResolveAsync(component, requirePatch).ConfigureAwait(false);
                }
                return;
            }

            if (!visiting.Add(component.Uid))
                throw _Invalid(manifestPath, $"组件依赖形成循环：{component.Uid}");

            await EnsurePatchAsync(component, requirePatch).ConfigureAwait(false);

            var requirements = component.Patch is not null
                ? component.Patch.GetRequirements()
                : _ConvertRequirements(component.Manifest.CachedRequires);

            foreach (var requirement in requirements)
            {
                var dependencyUid = requirement.Uid?.Trim();
                if (string.IsNullOrWhiteSpace(dependencyUid))
                    throw _Invalid(manifestPath, $"组件「{component.Uid}」包含缺少 uid 的依赖");
                if (disabled.Contains(dependencyUid))
                    throw _Invalid(manifestPath,
                        $"组件「{component.Uid}」依赖已禁用的组件「{dependencyUid}」");

                ResolvedComponent dependency;
                if (enabled.TryGetValue(dependencyUid, out var declaredDependency))
                {
                    dependency = GetOrCreate(declaredDependency, declared: true);
                }
                else
                {
                    var version = requirement.PreferredVersion;
                    if (string.IsNullOrWhiteSpace(version))
                        throw _Invalid(manifestPath,
                            $"组件「{component.Uid}」依赖「{dependencyUid}」，但未声明可解析的版本");

                    dependency = GetOrCreate(new MultiMcComponent
                    {
                        Uid = dependencyUid,
                        Version = version,
                        DependencyOnly = true
                    }, declared: false);
                }

                if (string.IsNullOrWhiteSpace(dependency.Version) &&
                    !string.IsNullOrWhiteSpace(requirement.PreferredVersion))
                    dependency.Version = requirement.PreferredVersion!.Trim();

                if (!string.IsNullOrWhiteSpace(requirement.RequiredVersion) &&
                    !string.IsNullOrWhiteSpace(dependency.Version) &&
                    !string.Equals(requirement.RequiredVersion, dependency.Version,
                        StringComparison.OrdinalIgnoreCase))
                    throw _Invalid(manifestPath,
                        $"组件「{component.Uid}」要求「{dependencyUid}」版本 {requirement.RequiredVersion}，" +
                        $"但整合包声明的是 {dependency.Version}");

                var dependencyRole = MultiMcComponentCatalog.GetRole(dependencyUid);
                var dependencyLoader = MultiMcComponentCatalog.ResolveLoader(dependencyUid, dependency.Version);
                var dependencyNeedsPatch = dependencyRole is MultiMcComponentRole.Unknown or
                                               MultiMcComponentRole.LauncherManaged ||
                                           dependencyRole == MultiMcComponentRole.ModLoader &&
                                           dependencyLoader is { } dependencyKind &&
                                           !_CanInstallWithPcl(dependencyKind, gameVersionHint);

                await ResolveAsync(dependency, dependencyNeedsPatch).ConfigureAwait(false);
            }

            visiting.Remove(component.Uid);
            completed.Add(component.Uid);
            ordered.Add(component);
        }

        foreach (var component in components.Where(component => !component.Disabled))
        {
            var resolved = GetOrCreate(component, declared: true);
            var role = MultiMcComponentCatalog.GetRole(resolved.Uid);
            var loader = MultiMcComponentCatalog.ResolveLoader(resolved.Uid, resolved.Version);
            var requirePatch = role == MultiMcComponentRole.Unknown ||
                               role == MultiMcComponentRole.ModLoader &&
                               loader is { } kind && !_CanInstallWithPcl(kind, gameVersionHint);
            await ResolveAsync(resolved, requirePatch).ConfigureAwait(false);
        }

        foreach (var component in ordered)
        {
            var conflicts = component.Patch is not null
                ? component.Patch.GetConflicts()
                : _ConvertRequirements(component.Manifest.CachedConflicts);

            foreach (var conflict in conflicts)
            {
                var conflictUid = conflict.Uid?.Trim();
                if (string.IsNullOrWhiteSpace(conflictUid) ||
                    !resolvedByUid.TryGetValue(conflictUid, out var other) ||
                    ReferenceEquals(component, other))
                    continue;

                if (string.IsNullOrWhiteSpace(conflict.RequiredVersion) ||
                    string.Equals(conflict.RequiredVersion, other.Version, StringComparison.OrdinalIgnoreCase))
                    throw _Invalid(manifestPath,
                        $"组件「{component.Uid}」与「{other.Uid}」{other.Version} 冲突");
            }
        }

        foreach (var declaredComponent in allDeclared.Values.Where(component => component.Disabled))
            warnings.Add($"已跳过禁用组件「{declaredComponent.Uid}」{declaredComponent.ResolveVersion()}");

        return ordered;
    }

    private static IReadOnlyList<MultiMcPatchRequirement> _ConvertRequirements(
        IReadOnlyList<MultiMcRequirement>? requirements)
        => requirements is null
            ? []
            : requirements.Select(requirement => new MultiMcPatchRequirement(
                requirement.Uid,
                requirement.RequiredVersion,
                requirement.Suggests)).ToArray();

    private static void _ValidatePatchIdentity(ResolvedComponent component, string manifestPath)
    {
        var patch = component.Patch!;
        _ValidatePatchSchema(patch, manifestPath);
        if (patch.Raw["formatVersion"]?.GetValue<int?>() is { } formatVersion && formatVersion != 1)
            throw _Invalid(manifestPath,
                $"组件「{component.Uid}」使用了不支持的补丁格式版本 {formatVersion}");
        if (!string.Equals(component.Uid, patch.Uid, StringComparison.OrdinalIgnoreCase))
            throw _Invalid(manifestPath,
                $"组件「{component.Uid}」加载到了 UID 为「{patch.Uid}」的补丁");
        if (!string.IsNullOrWhiteSpace(component.Version) && !string.IsNullOrWhiteSpace(patch.Version) &&
            !string.Equals(component.Version, patch.Version, StringComparison.OrdinalIgnoreCase))
            throw _Invalid(manifestPath,
                $"组件「{component.Uid}」要求版本 {component.Version}，补丁版本却是 {patch.Version}");
    }

    private static string? _ResolveGameVersion(
        IReadOnlyList<ResolvedComponent> components,
        MultiMcInstanceConfig config,
        bool allowLegacyFallback)
    {
        var declared = components.FirstOrDefault(component =>
            MultiMcComponentCatalog.GetRole(component.Uid) == MultiMcComponentRole.Game)?.Version;
        if (!string.IsNullOrWhiteSpace(declared)) return declared;
        return allowLegacyFallback ? config.GetString("IntendedVersion") : null;
    }

    private static List<ModpackLoader> _ResolveLoaders(
        IReadOnlyList<ResolvedComponent> components, string gameVersion, List<string> warnings)
    {
        var loaders = new List<ModpackLoader>();

        foreach (var component in components)
        {
            if (MultiMcComponentCatalog.GetRole(component.Uid) != MultiMcComponentRole.ModLoader ||
                string.IsNullOrWhiteSpace(component.Version))
                continue;

            var kind = MultiMcComponentCatalog.ResolveLoader(component.Uid, component.Version);
            if (kind is null) continue;

            if (_CanInstallWithPcl(kind.Value, gameVersion))
            {
                loaders.Add(new ModpackLoader(kind.Value, component.Version));
            }
            else if (component.Patch is not null)
            {
                warnings.Add($"加载器「{component.Uid}」将使用 MultiMC 组件补丁安装");
            }
        }

        return loaders;
    }

    private static ModpackVersionPatch? _BuildVersionPatch(
        ModpackArchive archive,
        string root,
        IReadOnlyList<ResolvedComponent> components,
        string gameVersion,
        string manifestPath,
        List<string> warnings)
    {
        var applicable = new List<MultiMcPatch>();
        var orderedComponents = new List<ModpackVersionComponent>();
        var jarMods = new List<ModpackJarMod>();

        foreach (var component in components)
        {
            if (component.Patch is { } patch)
            {
                _ValidatePatchLibraries(archive, root, patch, manifestPath);
                applicable.Add(patch);
                _AppendJarMods(archive, root, patch, jarMods, manifestPath);
                _AppendUnsupportedFeatureWarnings(patch, warnings);
            }

            var versionComponent = _CreateVersionComponent(component, gameVersion);
            if (versionComponent is not null) orderedComponents.Add(versionComponent);
        }

        var merged = MultiMcPatchMerger.Merge(applicable, selfContained: false);
        if (merged is null && applicable.Count == 0 && jarMods.Count == 0) return null;

        foreach (var trait in merged?.Traits ?? [])
        {
            if (!_KnownTraits.Contains(trait))
                warnings.Add($"MultiMC 组件声明了 PCL 无对应行为的 trait「{trait}」");
        }

        return new ModpackVersionPatch(
            merged?.VersionJson ?? new JsonObject(),
            ReplacesGameJson: false,
            merged?.AppliedComponentUids ?? applicable.Select(patch => patch.Uid).ToArray())
        {
            OrderedComponents = orderedComponents,
            JarMods = jarMods,
            MavenFiles = merged?.MavenFiles ?? [],
            LocalMainJarFileName = merged?.LocalMainJarFileName,
            Traits = merged?.Traits ?? []
        };
    }

    private static ModpackVersionComponent? _CreateVersionComponent(
        ResolvedComponent component, string gameVersion)
    {
        var patch = component.Patch?.Raw.DeepClone().AsObject();
        if (patch is not null && string.IsNullOrWhiteSpace(patch["uid"]?.GetValue<string?>()))
            patch["uid"] = component.Uid;
        var role = MultiMcComponentCatalog.GetRole(component.Uid);

        if (role == MultiMcComponentRole.Game)
            return new ModpackVersionComponent(
                component.Uid, ModpackVersionComponentKind.Game, Patch: patch);

        if (role == MultiMcComponentRole.ModLoader &&
            MultiMcComponentCatalog.ResolveLoader(component.Uid, component.Version) is { } loaderKind &&
            _CanInstallWithPcl(loaderKind, gameVersion))
            return new ModpackVersionComponent(
                component.Uid, ModpackVersionComponentKind.Loader, loaderKind, patch);

        return patch is null
            ? null
            : new ModpackVersionComponent(
                component.Uid, ModpackVersionComponentKind.CustomPatch, Patch: patch);
    }

    private static bool _CanInstallWithPcl(ModLoaderKind kind, string? gameVersion)
    {
        if (!ModpackLoaderSupport.IsInstallable(kind)) return false;
        if (kind != ModLoaderKind.Forge) return true;

        // Forge 在 Minecraft 1.6 之前由 FML 平铺依赖与 universal ZIP 直接修改游戏 JAR，
        // 不存在可交给 PCL Forge 安装器处理的版本 JSON，必须完整采用 MultiMC 组件补丁。
        if (string.IsNullOrWhiteSpace(gameVersion)) return false;
        var version = gameVersion.AsSpan().Trim();
        var dot = version.IndexOf('.');
        if (dot <= 0 || !int.TryParse(version[..dot], out var major)) return false;

        var minorStart = dot + 1;
        var minorLength = 0;
        while (minorStart + minorLength < version.Length &&
               char.IsDigit(version[minorStart + minorLength]))
            minorLength++;
        if (minorLength == 0 ||
            !int.TryParse(version.Slice(minorStart, minorLength), out var minor))
            return false;

        return major > 1 || major == 1 && minor >= 6;
    }

    private static void _ValidatePatchLibraries(
        ModpackArchive archive,
        string root,
        MultiMcPatch patch,
        string manifestPath)
    {
        foreach (var propertyName in new[] { "libraries", "+libraries", "mavenFiles" })
        {
            if (patch.Raw[propertyName] is not JsonArray entries) continue;

            foreach (var entry in entries)
            {
                if (entry is not JsonObject library)
                    throw _Invalid(manifestPath,
                        $"组件「{patch.Uid}」的 {propertyName} 中包含非对象条目");
                _ValidateLibrary(archive, root, patch.Uid, library, manifestPath, honorRules: true);
            }
        }

        if (patch.Raw["mainJar"] is JsonObject mainJar)
            _ValidateLibrary(archive, root, patch.Uid, mainJar, manifestPath, honorRules: false);
    }

    private static void _ValidateLibrary(
        ModpackArchive archive,
        string root,
        string componentUid,
        JsonObject library,
        string manifestPath,
        bool honorRules)
    {
        JsonObject normalized;
        bool active;
        try
        {
            normalized = MultiMcPatchMerger.NormalizeLibrary(library);
            active = MultiMcPatchMerger.IsLibraryActiveOnCurrentSystem(normalized);
        }
        catch (FormatException ex)
        {
            throw _Invalid(manifestPath, $"组件「{componentUid}」包含非法库定义：{ex.Message}", ex);
        }

        var isLocal = string.Equals(
            normalized["hint"]?.GetValue<string?>(), "local", StringComparison.OrdinalIgnoreCase);

        if (honorRules && !active) return;

        var artifactPath = normalized["downloads"]?["artifact"]?["path"]?.GetValue<string?>();
        if (!ModpackPathPolicy.TryNormalizeRelativePath(artifactPath, out _))
            throw _Invalid(manifestPath,
                $"组件「{componentUid}」声明了非法的库路径：{artifactPath}");

        if (isLocal)
        {
            var fileName = MultiMcPatchMerger.GetLibraryFileName(library);
            if (!_IsSafeLeafName(fileName))
                throw _Invalid(manifestPath,
                    $"组件「{componentUid}」声明了非法的本地库文件名：{fileName}");

            var archivePath = _At(root, $"{LibrariesDirectory}/{fileName}");
            if (!archive.HasEntry(archivePath))
                throw _Invalid(manifestPath,
                    $"组件「{componentUid}」声明的本地库不存在：{LibrariesDirectory}/{fileName}");
            return;
        }
    }

    private static bool _IsSafeLeafName(string? fileName)
        => !string.IsNullOrWhiteSpace(fileName) &&
           !fileName.Contains('/') &&
           !fileName.Contains('\\') &&
           fileName is not "." and not "..";

    private static void _AppendJarMods(
        ModpackArchive archive,
        string root,
        MultiMcPatch patch,
        List<ModpackJarMod> target,
        string manifestPath)
    {
        JsonArray? entries;
        var legacy = false;

        if (patch.Raw.ContainsKey("jarMods"))
        {
            entries = patch.Raw["jarMods"] as JsonArray ??
                      throw _Invalid(manifestPath, $"组件「{patch.Uid}」的 jarMods 必须是数组");
        }
        else if (patch.Raw.ContainsKey("+jarMods"))
        {
            entries = patch.Raw["+jarMods"] as JsonArray ??
                      throw _Invalid(manifestPath, $"组件「{patch.Uid}」的 +jarMods 必须是数组");
            legacy = true;
        }
        else
        {
            return;
        }

        foreach (var entry in entries)
        {
            if (entry is not JsonObject definition)
                throw _Invalid(manifestPath,
                    $"组件「{patch.Uid}」的 {(legacy ? "+jarMods" : "jarMods")} 中包含非对象条目");

            if (legacy)
            {
                var legacyFileName = definition["name"]?.GetValue<string?>()?.Trim();
                _ValidateLocalJarMod(archive, root, patch.Uid, legacyFileName, manifestPath);
                target.Add(new ModpackJarMod(legacyFileName!, IsLocal: true, DownloadUrls: []));
                continue;
            }

            JsonObject normalized;
            try
            {
                normalized = MultiMcPatchMerger.NormalizeLibrary(definition);
            }
            catch (FormatException ex)
            {
                throw _Invalid(manifestPath, $"组件「{patch.Uid}」包含非法 JAR Mod：{ex.Message}", ex);
            }

            var fileName = MultiMcPatchMerger.GetLibraryFileName(definition)?.Trim();
            if (!_IsSafeLeafName(fileName))
                throw _Invalid(manifestPath,
                    $"组件「{patch.Uid}」的 JAR Mod 缺少合法文件名：{fileName}");

            var isLocal = string.Equals(
                normalized["hint"]?.GetValue<string?>(), "local", StringComparison.OrdinalIgnoreCase);
            if (isLocal)
            {
                _ValidateLocalJarMod(archive, root, patch.Uid, fileName, manifestPath);
                target.Add(new ModpackJarMod(fileName!, IsLocal: true, DownloadUrls: []));
                continue;
            }

            var artifact = normalized["downloads"]!["artifact"]!.AsObject();
            var artifactPath = artifact["path"]!.GetValue<string>();
            var urls = new List<string>();
            if (artifact["url"]?.GetValue<string?>() is { Length: > 0 } absoluteUrl)
                urls.Add(_ValidateDownloadUrl(absoluteUrl, patch.Uid, manifestPath));
            else if (normalized["url"]?.GetValue<string?>() is { Length: > 0 } repositoryUrl)
                urls.Add(_ValidateDownloadUrl(
                    $"{repositoryUrl.TrimEnd('/')}/{artifactPath.Replace('\\', '/')}", patch.Uid, manifestPath));
            else
                urls.Add($"https://libraries.minecraft.net/{artifactPath.Replace('\\', '/')}");

            target.Add(new ModpackJarMod(
                fileName!,
                IsLocal: false,
                DownloadUrls: urls.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                Sha1: artifact["sha1"]?.GetValue<string?>(),
                FileSize: _GetInt64(artifact["size"])));
        }
    }

    private static void _ValidateLocalJarMod(
        ModpackArchive archive,
        string root,
        string componentUid,
        string? fileName,
        string manifestPath)
    {
        if (!_IsSafeLeafName(fileName))
            throw _Invalid(manifestPath,
                $"组件「{componentUid}」声明了非法的本地 JAR Mod 文件名：{fileName}");

        var archivePath = _At(root, $"{JarModsDirectory}/{fileName}");
        if (!archive.HasEntry(archivePath))
            throw _Invalid(manifestPath,
                $"组件「{componentUid}」声明的本地 JAR Mod 不存在：{JarModsDirectory}/{fileName}");
    }

    private static string _ValidateDownloadUrl(string url, string componentUid, string manifestPath)
    {
        url = url.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
            parsed.Scheme is not ("http" or "https"))
            throw _Invalid(manifestPath,
                $"组件「{componentUid}」声明了非法的 JAR Mod 下载地址：{url}");
        return url;
    }

    private static long? _GetInt64(JsonNode? value)
    {
        if (value is not JsonValue jsonValue) return null;
        if (jsonValue.TryGetValue<long>(out var longValue)) return longValue > 0 ? longValue : null;
        if (jsonValue.TryGetValue<int>(out var intValue)) return intValue > 0 ? intValue : null;
        return null;
    }

    private static void _AppendUnsupportedFeatureWarnings(MultiMcPatch patch, List<string> warnings)
    {
        if (patch.Raw["mods"] is JsonArray { Count: > 0 })
            warnings.Add($"组件「{patch.Uid}」声明了已被 Prism Launcher 弃用且不会消费的 mods 字段");
        if (patch.Raw["appletClass"]?.GetValue<string?>() is { Length: > 0 })
            warnings.Add($"组件「{patch.Uid}」声明了仅适用于旧式内嵌窗口的 appletClass，PCL 不会使用该字段");
    }

    private static Dictionary<string, MultiMcPatch> _ReadLocalPatches(
        ModpackArchive archive, string root)
    {
        var patches = new Dictionary<string, MultiMcPatch>(StringComparer.OrdinalIgnoreCase);
        var directory = _At(root, PatchesDirectory);
        if (!archive.HasDirectory(directory)) return patches;

        foreach (var item in archive.EnumerateFiles(directory))
        {
            if (!item.RelativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

            var path = $"{directory}/{item.RelativePath}";
            try
            {
                var fileName = item.RelativePath.Replace('\\', '/').Split('/')[^1];
                var fallbackUid = fileName[..^".json".Length];
                var patch = MultiMcPatch.TryCreate(
                    JsonCompat.ParseNode(archive.ReadAllText(path)), MultiMcPatchSource.Local, fallbackUid);

                if (patch is null)
                    throw _Invalid(path, "补丁根节点必须是对象且必须包含 uid");
                _ValidatePatchSchema(patch, path);
                if (!patches.TryAdd(patch.Uid, patch))
                    throw _Invalid(path, $"组件补丁重复：{patch.Uid}");
            }
            catch (ModpackManifestInvalidException)
            {
                throw;
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or ModpackArchiveException or
                                       InvalidOperationException or FormatException or ArgumentException)
            {
                throw _Invalid(path, "不是合法的组件补丁", ex);
            }
        }

        return patches;
    }

    private static List<ModpackEmbeddedPayload> _CollectPayloads(
        ModpackArchive archive, string root, IReadOnlyList<ModpackJarMod> jarModDefinitions)
    {
        var payloads = new List<ModpackEmbeddedPayload>(2);
        var libraries = _At(root, LibrariesDirectory);
        var jarMods = _At(root, JarModsDirectory);

        if (archive.HasDirectory(libraries))
            payloads.Add(new ModpackEmbeddedPayload(
                ModpackPayloadKind.Libraries,
                libraries,
                TargetDirectory: LibrariesDirectory));

        var localJarMods = jarModDefinitions
            .Where(jarMod => jarMod.IsLocal)
            .Select(jarMod => jarMod.FileName)
            .ToArray();
        if (localJarMods.Length > 0)
            payloads.Add(new ModpackEmbeddedPayload(
                ModpackPayloadKind.JarMods, jarMods, localJarMods));

        return payloads;
    }

    private static ModpackLaunchOptions _ParseLaunchOptions(
        ModpackArchive archive,
        string root,
        MultiMcInstanceConfig config,
        string manifestPath,
        List<string> warnings)
    {
        var jvmArgs = config.GetOverridden("OverrideJavaArgs", "JvmArgs");
        var overrideMemory = config.GetBoolean("OverrideMemory");
        var minMemory = overrideMemory ? _Positive(config.GetInt32("MinMemAlloc")) : null;
        var maxMemory = overrideMemory ? _Positive(config.GetInt32("MaxMemAlloc")) : null;
        var permGen = overrideMemory ? _Positive(config.GetInt32("PermGen")) : null;

        if (minMemory is not null && maxMemory is not null && minMemory > maxMemory)
        {
            (minMemory, maxMemory) = (maxMemory, minMemory);
            warnings.Add("instance.cfg 中 MinMemAlloc 大于 MaxMemAlloc，已按 Prism Launcher 的行为交换两者");
        }

        var preLaunch = _TranslateCommand(config.GetOverridden("OverrideCommands", "PreLaunchCommand"));
        var postExit = _TranslateCommand(config.GetOverridden("OverrideCommands", "PostExitCommand"));
        var wrapper = _TranslateCommand(config.GetOverridden("OverrideCommands", "WrapperCommand"));
        if (postExit is not null)
            warnings.Add("PCL 暂无实例级退出后命令，PostExitCommand 未写入实例设置");
        if (wrapper is not null)
            warnings.Add("PCL 暂无实例级 Java 包装命令，WrapperCommand 未写入实例设置");
        if (config.GetBoolean("OverrideWindow"))
            warnings.Add("PCL 暂无等价的实例级 MultiMC 窗口设置，OverrideWindow 未迁移");
        if (config.GetBoolean("OverrideConsole"))
            warnings.Add("PCL 暂无等价的实例级 MultiMC 控制台设置，OverrideConsole 未迁移");

        var joinServer = config.GetBoolean("JoinServerOnLaunch");
        return new ModpackLaunchOptions
        {
            JvmArguments = jvmArgs is null ? [] : [jvmArgs],
            MinMemoryMegabytes = minMemory,
            MaxMemoryMegabytes = maxMemory,
            PermGenMegabytes = permGen,
            JavaPath = config.GetOverridden("OverrideJavaLocation", "JavaPath"),
            PreLaunchCommand = preLaunch,
            PostExitCommand = postExit,
            WrapperCommand = wrapper,
            ServerToJoin = joinServer ? config.GetString("JoinServerOnLaunchAddress") : null,
            IgnoreJavaCompatibility = config.GetBoolean("IgnoreJavaCompatibility") ? true : null,
            IconArchivePath = _ResolveIconPath(archive, root, config),
            Notes = config.GetString("notes")
        };
    }

    private static int? _Positive(int? value) => value is > 0 ? value : null;

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

    private static string? _ResolveIconPath(
        ModpackArchive archive, string root, MultiMcInstanceConfig config)
    {
        var iconKey = config.GetString("iconKey");
        if (string.IsNullOrWhiteSpace(iconKey) || iconKey == "default") return null;

        var name = iconKey.Replace('\\', '/').Split('/')[^1];
        if (name.Length == 0) return null;

        foreach (var extension in _IconExtensions)
        {
            var candidate = _At(root, $"{name}{extension}");
            if (archive.HasEntry(candidate)) return candidate;
        }

        return null;
    }

    private static IReadOnlyList<string> _FindInstanceRoots(ModpackArchive archive)
        => archive.EnumerateFiles()
            .Where(item => string.Equals(
                item.RelativePath.Replace('\\', '/').Split('/')[^1],
                InstanceConfigFileName,
                StringComparison.OrdinalIgnoreCase))
            .Select(item => _DirectoryOf(item.RelativePath))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(root => archive.HasEntry(_At(root, PackFileName)))
            .ThenBy(root => root.Count(character => character == '/'))
            .ThenBy(root => root, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string _DirectoryOf(string path)
    {
        path = path.Replace('\\', '/').Trim('/');
        var separator = path.LastIndexOf('/');
        return separator < 0 ? string.Empty : path[..separator];
    }

    private static string _At(string root, string relativePath)
        => string.IsNullOrEmpty(root)
            ? relativePath.Replace('\\', '/').Trim('/')
            : $"{root.Trim('/')}/{relativePath.Replace('\\', '/').Trim('/')}";

    private static ModpackManifestInvalidException _Invalid(
        string manifestPath, string message, Exception? inner = null)
        => new(ModpackFormat.MultiMc, manifestPath, message, inner);

    private static void _ValidatePatchSchema(MultiMcPatch patch, string manifestPath)
    {
        var raw = patch.Raw;

        _RequireOptionalValue<int>(raw, "formatVersion", patch.Uid, manifestPath);
        _RequireOptionalValue<int>(raw, "order", patch.Uid, manifestPath);
        foreach (var propertyName in new[]
                 {
                     "uid", "version", "mainClass", "assets", "minecraftArguments", "appletClass"
                 })
            _RequireOptionalValue<string>(raw, propertyName, patch.Uid, manifestPath);

        foreach (var propertyName in new[] { "mainJar", "assetIndex" })
            if (raw.ContainsKey(propertyName) && raw[propertyName] is not JsonObject)
                throw _Invalid(manifestPath,
                    $"组件「{patch.Uid}」的 {propertyName} 必须是对象");

        foreach (var propertyName in new[]
                 {
                     "libraries", "+libraries", "mavenFiles", "jarMods", "+jarMods", "mods"
                 })
            _RequireArrayEntries<JsonObject>(raw, propertyName, patch.Uid, manifestPath, "对象");

        if (raw.ContainsKey("-libraries"))
        {
            if (raw["-libraries"] is not JsonArray removals)
                throw _Invalid(manifestPath,
                    $"组件「{patch.Uid}」的 -libraries 必须是数组");
            if (removals.Any(node => !_IsLibraryRemoval(node)))
                throw _Invalid(manifestPath,
                    $"组件「{patch.Uid}」的 -libraries 只能包含库对象或 Maven 坐标字符串");
        }

        foreach (var propertyName in new[]
                 {
                     "+gameArgs", "-gameArgs", "+jvmArgs", "-jvmArgs", "+tweakers", "+traits"
                 })
            _RequireArrayValues<string>(raw, propertyName, patch.Uid, manifestPath, "字符串");

        _RequireArrayValues<int>(raw, "compatibleJavaMajors", patch.Uid, manifestPath, "整数");

        foreach (var propertyName in new[] { "requires", "conflicts" })
        {
            var requirements = _RequireArrayEntries<JsonObject>(
                raw, propertyName, patch.Uid, manifestPath, "对象");
            if (requirements is null) continue;

            foreach (var requirement in requirements.OfType<JsonObject>())
                foreach (var valueName in new[] { "uid", "equals", "suggests" })
                    _RequireOptionalValue<string>(
                        requirement, valueName, patch.Uid, manifestPath, propertyName);
        }
    }

    private static bool _IsLibraryRemoval(JsonNode? node)
        => node is JsonObject ||
           node is JsonValue value && value.TryGetValue<string>(out _);

    private static JsonArray? _RequireArrayEntries<T>(
        JsonObject owner,
        string propertyName,
        string componentUid,
        string manifestPath,
        string expectedEntryType) where T : JsonNode
    {
        if (!owner.ContainsKey(propertyName)) return null;
        if (owner[propertyName] is not JsonArray array)
            throw _Invalid(manifestPath,
                $"组件「{componentUid}」的 {propertyName} 必须是数组");
        if (array.Any(node => node is not T))
            throw _Invalid(manifestPath,
                $"组件「{componentUid}」的 {propertyName} 只能包含{expectedEntryType}条目");
        return array;
    }

    private static void _RequireArrayValues<T>(
        JsonObject owner,
        string propertyName,
        string componentUid,
        string manifestPath,
        string expectedEntryType)
    {
        if (!owner.ContainsKey(propertyName)) return;
        if (owner[propertyName] is not JsonArray array)
            throw _Invalid(manifestPath,
                $"组件「{componentUid}」的 {propertyName} 必须是数组");
        if (array.Any(node => node is not JsonValue value || !value.TryGetValue<T>(out _)))
            throw _Invalid(manifestPath,
                $"组件「{componentUid}」的 {propertyName} 只能包含{expectedEntryType}条目");
    }

    private static void _RequireOptionalValue<T>(
        JsonObject owner,
        string propertyName,
        string componentUid,
        string manifestPath,
        string? parentName = null)
    {
        if (!owner.TryGetPropertyValue(propertyName, out var node) || node is null) return;
        if (node is JsonValue value && value.TryGetValue<T>(out _)) return;

        var qualifiedName = parentName is null ? propertyName : $"{parentName}.{propertyName}";
        throw _Invalid(manifestPath,
            $"组件「{componentUid}」的 {qualifiedName} 类型不正确");
    }

    private static readonly string[] _IconExtensions = [".png", ".jpg", ".jpeg", ".webp", ".ico"];

    private static readonly HashSet<string> _KnownTraits = new(StringComparer.Ordinal)
    {
        "FirstThreadOnMacOS",
        "legacyFML",
        "XR:Initial",
        "texturepacks",
        "no-texturepacks"
    };

    private sealed class ResolvedComponent(
        MultiMcComponent manifest,
        string uid,
        string? version,
        MultiMcPatch? patch,
        bool declared)
    {
        public MultiMcComponent Manifest { get; } = manifest;
        public string Uid { get; } = uid;
        public string? Version { get; set; } = version;
        public MultiMcPatch? Patch { get; set; } = patch;
        public bool Declared { get; } = declared;
    }
}
