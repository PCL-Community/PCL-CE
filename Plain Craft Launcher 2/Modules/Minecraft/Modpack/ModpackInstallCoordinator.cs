using System.IO;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Modpack;
using PCL.Core.Minecraft.Modpack.Installation;
using PCL.Core.Minecraft.Modpack.Model;
using PCL.Core.UI;
using PCL.Network;
using PCL.Network.Loaders;
using static PCL.ModLoader;

namespace PCL.Modpack;

/// <summary>
///     把 <see cref="ModpackInstallPlan" /> 落实到 PCL 的加载器与实例设置上。
///     只负责「翻译」，不含任何整合包格式判断 —— 那些都在 PCL.Core 的识别层完成。
/// </summary>
internal static class ModpackInstallCoordinator
{
    /// <summary>
    ///     构造整合包自身的安装阶段：解析下载信息、释放覆写文件、写入实例设置。
    ///     该任务的输出是待下载文件列表，交由后续的 <see cref="LoaderDownload" /> 消费。
    /// </summary>
    /// <param name="session">已完成识别的安装会话，本任务结束时会将其释放。</param>
    /// <param name="context">安装上下文。</param>
    public static LoaderTask<string, List<DownloadFile>> BuildInstallTask(
        ModpackInstallSession session, ModpackInstallContext context)
    {
        return new LoaderTask<string, List<DownloadFile>>(
            Lang.Text("Minecraft.Download.Modpack.Stage.ModpackInstall"),
            task =>
            {
                try
                {
                    task.output = _Execute(session, context, task);
                }
                finally
                {
                    session.Dispose();
                }
            })
        {
            ProgressWeight = context.ExtractionProgressWeight,
            block = false
        };
    }

    private static List<DownloadFile> _Execute(
        ModpackInstallSession session, ModpackInstallContext context, LoaderBase task)
    {
        // 阶段一：解析下载信息（CurseForge 整合包在此调用 API）
        var plan = session.CreatePlanAsync(new ModpackInstallOptions
        {
            InstanceDirectory = context.InstanceDirectory,
            CurseForgeResolver = PclCurseForgeFileResolver.Instance
        }).GetAwaiter().GetResult();

        task.Progress = 0.15d;
        _ReportUnresolvedFiles(plan);

        // 阶段二：释放覆写文件与内嵌载荷
        var overrideSnapshots = session
            .ExtractOverridesAsync(new Progress<double>(value => task.Progress = 0.15d + value * 0.6d))
            .GetAwaiter().GetResult();

        session.ExtractPayloadsAsync().GetAwaiter().GetResult();
        task.Progress = 0.8d;

        // 阶段三：写入实例设置
        _ApplyInstanceSettings(session, plan, context);
        session.WriteConfigurationAsync(overrideSnapshots).GetAwaiter().GetResult();
        task.Progress = 0.9d;

        // 阶段四：确认可选文件并构造下载任务
        var downloads = _BuildDownloadFiles(plan);
        task.Progress = 1d;

        return downloads;
    }

    private static void _ReportUnresolvedFiles(ModpackInstallPlan plan)
    {
        if (plan.UnresolvedFiles.Count == 0) return;

        ModBase.Log($"[Modpack] 有 {plan.UnresolvedFiles.Count} 个文件无法获取下载信息：" +
                    string.Join("、", plan.UnresolvedFiles.Take(10)));
        HintService.Hint(Lang.Text("Minecraft.Download.Modpack.SomeModsDeleted"), HintType.Error);
    }

    /// <summary>
    ///     把方案中的下载项转换为 <see cref="DownloadFile" />，其间询问用户是否需要可选文件。
    /// </summary>
    private static List<DownloadFile> _BuildDownloadFiles(ModpackInstallPlan plan)
    {
        var files = new List<DownloadFile>(plan.Downloads.Count);

        foreach (var download in plan.Downloads)
        {
            if (download.Requirement == ModpackFileRequirement.Optional && !_ConfirmOptionalFile(download))
                continue;

            // 为每个地址追加镜像源，交由下载器按顺序尝试
            var urls = download.Urls
                .SelectMany(ModDownload.DlSourceModDownloadGet)
                .Distinct()
                .ToList();

            if (urls.Count == 0) continue;

            files.Add(new DownloadFile(
                urls,
                download.TargetPath,
                // FileChecker 的 hash 形参默认值即为 null，只是未标注可空
                new ModBase.FileChecker(
                    actualSize: download.FileSize ?? -1,
                    hash: download.Sha1!),
                true));
        }

        return files;
    }

    private static bool _ConfirmOptionalFile(ModpackPlannedDownload download)
        => ModMain.MyMsgBox(
            Lang.Text("Minecraft.Download.Modpack.OptionalFile.Message", download.DisplayName),
            Lang.Text("Minecraft.Download.Modpack.OptionalFile.Title"),
            Lang.Text("Minecraft.Download.Modpack.OptionalFile.Download"),
            Lang.Text("Minecraft.Download.Modpack.OptionalFile.Skip")) == 1;

    /// <summary>
    ///     把整合包声明的设置写入实例独立配置。
    /// </summary>
    private static void _ApplyInstanceSettings(
        ModpackInstallSession session, ModpackInstallPlan plan, ModpackInstallContext context)
    {
        var folder = context.InstanceDirectory;

        // 整合包安装的实例默认开启版本隔离，避免与其他实例互相污染
        Config.Instance.IndieV1[folder] = 1;
        Config.Instance.IndieV2[folder] = true;

        var options = plan.LaunchOptions;
        if (options.JvmArguments.Count > 0)
            Config.Instance.JvmArgs[folder] = string.Join(" ", options.JvmArguments);

        if (options.GameArguments.Count > 0)
            Config.Instance.GameArgs[folder] = string.Join(" ", options.GameArguments);

        if (options.PreLaunchCommand is not null)
            Config.Instance.PreLaunchCommand[folder] = options.PreLaunchCommand;

        if (options.ServerToJoin is not null)
            Config.Instance.ServerToEnter[folder] = options.ServerToJoin;

        if (options.IgnoreJavaCompatibility is true)
            Config.Instance.IgnoreJavaCompatibility[folder] = true;

        _ApplyLogo(session, plan, context);
    }

    /// <summary>
    ///     设置实例图标。外部传入的图标优先于整合包内嵌的图标。
    /// </summary>
    private static void _ApplyLogo(
        ModpackInstallSession session, ModpackInstallPlan plan, ModpackInstallContext context)
    {
        var target = Path.Combine(context.InstanceDirectory, "PCL", "Logo.png");

        try
        {
            if (context.LogoPath is not null && File.Exists(context.LogoPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(context.LogoPath, target, true);
            }
            else if (plan.LaunchOptions.IconArchivePath is { } iconPath)
            {
                if (!session.TryExtractFileAsync(iconPath, target).GetAwaiter().GetResult()) return;
            }
            else
            {
                return;
            }

            States.Instance.LogoPath[context.InstanceDirectory] = @"PCL\Logo.png";
            States.Instance.IsLogoCustom[context.InstanceDirectory] = true;
            ModBase.Log("[Modpack] 已设置实例图标");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Modpack] 设置实例图标失败");
        }
    }

    /// <summary>
    ///     由整合包组件构造游戏安装请求。
    /// </summary>
    public static ModDownloadLib.McInstallRequest BuildInstallRequest(
        ModpackDescriptor descriptor, ModpackInstallContext context)
    {
        var components = descriptor.Components;

        // McInstallRequest 的各版本号字段以 null 表示「不安装该加载器」，只是未标注可空
        return new ModDownloadLib.McInstallRequest
        {
            targetInstanceName = context.InstanceName,
            targetInstanceFolder = context.InstanceDirectory,
            minecraftName = components.GameVersion,
            forgeVersion = components.GetLoaderVersion(ModLoaderKind.Forge)!,
            neoForgeVersion = components.GetLoaderVersion(ModLoaderKind.NeoForge)!,
            fabricVersion = components.GetLoaderVersion(ModLoaderKind.Fabric)!,
            legacyFabricVersion = components.GetLoaderVersion(ModLoaderKind.LegacyFabric)!,
            cleanroomVersion = components.GetLoaderVersion(ModLoaderKind.Cleanroom)!,
            optiFineVersion = components.GetLoaderVersion(ModLoaderKind.OptiFine)!,
            mmcPackInfo = ModModpack.MMCPackInfo.FromVersionPatch(descriptor.VersionPatch)!
        };
    }
}

/// <summary>
///     一次整合包安装的上下文参数。
/// </summary>
/// <param name="InstanceName">实例名。</param>
/// <param name="InstanceDirectory">实例目录，以路径分隔符结尾。</param>
/// <param name="SourceFilePath">整合包文件路径。</param>
/// <param name="LogoPath">外部指定的实例图标路径。</param>
/// <param name="ResourceId">在线整合包的项目 ID。</param>
/// <param name="ExtractionProgressWeight">释放阶段的进度权重。</param>
internal readonly record struct ModpackInstallContext(
    string InstanceName,
    string InstanceDirectory,
    string SourceFilePath,
    string? LogoPath,
    string? ResourceId,
    double ExtractionProgressWeight);
