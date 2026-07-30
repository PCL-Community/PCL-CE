using System.IO;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Modpack;
using PCL.Core.Minecraft.Modpack.Installation;
using PCL.Core.Minecraft.Modpack.Model;
using PCL.Core.Minecraft.Modpack.MultiMc;
using PCL.Core.UI;
using PCL.Core.Utils.Validate;
using PCL.Modpack;
using PCL.Network.Loaders;
using static PCL.ModLoader;

namespace PCL;

/// <summary>
///     整合包安装的对外入口。
///     <para>
///         识别、解析与安装规划全部由 <c>PCL.Core.Minecraft.Modpack</c> 负责，
///         本类只负责把结果接到 PCL 的加载器体系上。
///     </para>
/// </summary>
public static class ModModpack
{
    /// <summary>
    ///     MultiMC 整合包带来的版本 JSON 覆盖信息。
    ///     <para>
    ///         <see cref="ModDownloadLib.McInstallRequest" /> 依赖该类型，故保留其形状；
    ///         内容由 PCL.Core 的补丁合并结果转换而来。
    ///     </para>
    /// </summary>
    public class MMCPackInfo
    {
        /// <summary>为 <c>true</c> 时，<see cref="overridedJson" /> 直接取代原版实例 JSON。</summary>
        public bool isMinecraftOverrided;

        /// <summary>补丁自带 Cleanroom 定义，无需再合并启动器安装的 Cleanroom JSON。</summary>
        public bool isCleanroomOverrided;

        /// <summary>补丁自带 Fabric 定义，无需再合并启动器安装的 Fabric JSON。</summary>
        public bool isFabricOverrided;

        /// <summary>补丁自带 Forge 定义，无需再合并启动器安装的 Forge JSON。</summary>
        public bool isForgeOverrided;

        /// <summary>补丁自带 NeoForge 定义，无需再合并启动器安装的 NeoForge JSON。</summary>
        public bool isNeoForgeOverrided;

        /// <summary>补丁自带游戏启动参数，需移除实例 JSON 中旧式的 minecraftArguments 字段。</summary>
        public bool isMcArgsEdited;

        /// <summary>需要叠加到实例 JSON 上的内容。</summary>
        public JsonObject overridedJson = new();

        /// <summary>MultiMC 组件的原始顺序，用于把自定义补丁穿插到加载器 JSON 之间。</summary>
        public IReadOnlyList<ModpackVersionComponent> orderedComponents = [];

        /// <summary>只下载、不加入运行时 classpath 的 MultiMC Maven 文件。</summary>
        public IReadOnlyList<JsonObject> mavenFiles = [];

        /// <summary>由实例 <c>libraries/</c> 提供的主 JAR 文件名。</summary>
        public string? localMainJarFileName;

        /// <summary>按组件顺序排列的本地与远程 JAR Mod。</summary>
        public IReadOnlyList<ModpackJarMod> jarMods = [];

        /// <summary>MultiMC 组件声明的实例特征。</summary>
        public IReadOnlyList<string> traits = [];

        /// <summary>
        ///     由 PCL.Core 的版本补丁构造，补丁为空时返回 <c>null</c>。
        ///     <para>
        ///         Minecraft 与 PCL 可安装的加载器仍以官方安装结果作为基础；MultiMC 本地补丁会在
        ///         对应组件的位置继续叠加，以保留整合包作者的定制内容。
        ///     </para>
        /// </summary>
        internal static MMCPackInfo? FromVersionPatch(ModpackVersionPatch? patch)
        {
            if (patch is null || patch.IsEmpty) return null;

            ModBase.Log($"[Modpack] 应用整合包自带的版本补丁：{string.Join("、", patch.AppliedComponentUids)}");

            return new MMCPackInfo
            {
                isMinecraftOverrided = patch.ReplacesGameJson,
                isMcArgsEdited = patch.OverridesGameArguments,
                overridedJson = patch.VersionJson.DeepClone().AsObject(),
                mavenFiles = patch.MavenFiles.Select(file => file.DeepClone().AsObject()).ToArray(),
                localMainJarFileName = patch.LocalMainJarFileName,
                jarMods = patch.JarMods.Select(jarMod => jarMod with
                {
                    DownloadUrls = jarMod.DownloadUrls.ToArray()
                }).ToArray(),
                traits = patch.Traits.ToArray(),
                orderedComponents = patch.OrderedComponents
                    .Select(component => component with
                    {
                        Patch = component.Patch?.DeepClone().AsObject()
                    })
                    .ToArray()
            };
        }
    }

    /// <summary>
    ///     弹窗要求选择一个整合包文件并进行安装。
    /// </summary>
    public static void ModpackInstall()
    {
        var file = SystemDialogs.SelectFile(
            Lang.Text("Minecraft.Download.Modpack.FileDialog.Filter"),
            Lang.Text("Minecraft.Download.Modpack.FileDialog.Title"));
        if (string.IsNullOrEmpty(file)) return;

        ModBase.RunInThread(() =>
        {
            try
            {
                ModpackInstall(file);
            }
            catch (ModBase.CancelledException)
            {
                // 用户主动取消，无需提示
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "手动安装整合包失败", ModBase.LogLevel.Msgbox,
                    userSummary: Lang.Text("Minecraft.Download.Modpack.Error.OperationFailed"));
            }
        });
    }

    /// <summary>
    ///     构建并启动安装给定整合包文件的加载器，并返回该加载器。若失败则抛出异常。
    ///     必须在工作线程执行。
    /// </summary>
    /// <param name="file">整合包文件路径。</param>
    /// <param name="instanceName">指定实例名；为 <c>null</c> 时取整合包名称，必要时询问用户。</param>
    /// <param name="logo">实例图标路径。</param>
    /// <param name="resourceId">在线整合包的项目 ID。</param>
    /// <param name="isOnlineInstall">是否由在线下载页发起；在线流程拥有下载的临时源文件。</param>
    /// <exception cref="ModBase.CancelledException" />
    public static LoaderCombo<string> ModpackInstall(
        string file, string? instanceName = null, string? logo = null,
        string? resourceId = null, bool isOnlineInstall = false)
    {
        ModBase.Log("[Modpack] 整合包安装请求：" + file);
        if (string.IsNullOrEmpty(file)) throw new ModBase.CancelledException();

        var session = _OpenSession(file);
        try
        {
            return _StartInstall(session, file, instanceName, logo, resourceId, isOnlineInstall);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     打开整合包并完成识别，把底层异常翻译为面向用户的提示。
    ///     <para>
    ///         外层压缩包里套着 <c>modpack.zip</c> / <c>modpack.mrpack</c> 的情形由
    ///         <see cref="ModpackInstallSession" /> 自动拆解，此处无需特殊处理。
    ///     </para>
    /// </summary>
    private static ModpackInstallSession _OpenSession(string file)
    {
        var context = new ModpackReadContext { MetaClient = MultiMcMetaClient.Shared };

        try
        {
            return ModpackInstallSession.OpenAsync(file, context).GetAwaiter().GetResult();
        }
        catch (ModpackFormatNotRecognizedException ex)
        {
            // 连内层整合包都找不到，说明这个文件确实不是整合包，明确告知用户而不是继续猜
            ModBase.Log(ex, "[Modpack] 未能识别整合包格式：" + file);
            ModMain.MyMsgBox(
                Lang.Text("Minecraft.Download.Modpack.NotRecognized.Message", ModBase.GetFileNameFromPath(file)),
                Lang.Text("Minecraft.Download.Modpack.NotRecognized.Title"),
                isWarn: true);
            throw new ModBase.CancelledException();
        }
        catch (ModpackArchiveException ex)
        {
            if (ex.IsEncrypted)
                throw new Exception(Lang.Text("Minecraft.Download.Modpack.EncryptedArchiveUnsupported"), ex);
            if (file.EndsWithF(".rar", true))
                throw new Exception(Lang.Text("Minecraft.Download.Modpack.RarUnsupported"), ex);
            throw new Exception(Lang.Text("Minecraft.Download.Modpack.UnsupportedArchive"), ex);
        }
        catch (ModpackUnsupportedContentException ex)
        {
            throw new Exception(ex.Message, ex);
        }
    }

    private static LoaderCombo<string> _StartInstall(
        ModpackInstallSession session, string file, string? instanceName,
        string? logo, string? resourceId, bool isOnlineInstall)
    {
        var descriptor = session.Descriptor;
        ModBase.Log($"[Modpack] 整合包种类：{descriptor.Format.ToDisplayName()}，" +
                    $"游戏版本：{descriptor.Components.GameVersion}");

        instanceName ??= _ResolveInstanceName(descriptor.Metadata.Name);
        var instanceFolder = Path.Combine(ModFolder.mcFolderSelected, "versions", instanceName) +
                             Path.DirectorySeparatorChar;

        // PCL 的启动命令无法正确转义这两个字符
        if (instanceFolder.Contains('!') || instanceFolder.Contains(';'))
        {
            HintService.Hint(
                Lang.Text("Minecraft.Download.Modpack.InvalidGamePathChars", instanceFolder), HintType.Error);
            throw new ModBase.CancelledException();
        }

        var loaderName = _BuildTaskName(descriptor.Format, instanceName);
        if (loaderTaskbar.Any(l => l.name == loaderName))
        {
            HintService.Hint(Lang.Text("Minecraft.Download.Modpack.Installing"), HintType.Error);
            throw new ModBase.CancelledException();
        }

        var context = new ModpackInstallContext(
            instanceName,
            instanceFolder,
            file,
            logo,
            resourceId,
            isOnlineInstall,
            [],
            // 释放阶段按每 6 MB 约 1 秒估算耗时
            ExtractionProgressWeight: Math.Max(1d, new FileInfo(file).Length / 1024d / 1024d / 6d));

        var loader = new LoaderCombo<string>(loaderName, _BuildLoaders(session, descriptor, context))
        {
            OnStateChanged = ModDownloadLib.McInstallState
        };

        loader.Start(instanceFolder);
        LoaderTaskbarAdd(loader);

        ModMain.frmMain?.BtnExtraDownload.ShowRefresh();
        if (!isOnlineInstall)
            ModBase.RunInUi(() => ModMain.frmMain?.PageChange(FormMain.PageType.TaskManager));

        return loader;
    }

    /// <summary>
    ///     构造完整的安装加载器链。
    /// </summary>
    private static List<LoaderBase> _BuildLoaders(
        ModpackInstallSession session, ModpackDescriptor descriptor, ModpackInstallContext context)
    {
        // 整合包阶段：解析下载信息 → 释放覆写文件 → 写入设置 → 下载附加文件
        var modpackStages = new List<LoaderBase>
        {
            ModpackInstallCoordinator.BuildInstallTask(session, context),
            new LoaderDownload(Lang.Text("Minecraft.Download.Modpack.Stage.DownloadAdditions"), [])
            {
                ProgressWeight = Math.Max(1d, descriptor.Files.Count * 1.5d)
            }
        };

        // 游戏阶段：按整合包声明的组件安装 Minecraft 与加载器
        var request = ModpackInstallCoordinator.BuildInstallRequest(descriptor, context);
        var gameStages = ModDownloadLib.McInstallLoader(request);

        return
        [
            new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.ModpackInstall"), modpackStages)
            {
                show = false, block = false, ProgressWeight = modpackStages.Sum(l => l.ProgressWeight)
            },
            new LoaderCombo<string>(Lang.Text("Minecraft.Download.Modpack.Stage.GameInstall"), gameStages)
            {
                show = false, ProgressWeight = gameStages.Sum(l => l.ProgressWeight)
            },
            new LoaderTask<string, string>(
                Lang.Text("Minecraft.Download.Modpack.Stage.FinalizeFiles"),
                _ => _Finalize(descriptor, context))
            {
                ProgressWeight = 0.1d, show = false
            }
        ];
    }

    /// <summary>
    ///     安装完成后记录整合包来源信息并清理临时文件。
    /// </summary>
    private static void _Finalize(ModpackDescriptor descriptor, ModpackInstallContext context)
    {
        var folder = context.InstanceDirectory;

        if (descriptor.Metadata.Version is { } version)
            States.Instance.ModpackVersion[folder] = version;

        States.Instance.ModpackSource[folder] = descriptor.Format.ToDisplayName();

        if (context.ResourceId is { } resourceId)
        {
            States.Instance.ModpackId[folder] = resourceId;
            _TryFetchDescription(folder, resourceId);
        }
        else if (descriptor.Metadata.Description is { } description)
        {
            States.Instance.CustomInfo[folder] = description;
        }

        _CleanupSourceFiles(folder, context.SourceFilePath, context.IsOnlineInstall);
    }

    private static void _TryFetchDescription(string folder, string resourceId)
    {
        try
        {
            var projects = ModComp.CompRequest.GetCompProjectsByIds([resourceId]);
            if (projects.Count > 0) States.Instance.CustomInfo[folder] = projects.First().Description;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Modpack] 获取整合包描述文本失败");
        }
    }

    /// <summary>
    ///     删除安装过程中残留的整合包原始文件。
    /// </summary>
    private static void _CleanupSourceFiles(string instanceFolder, string sourceFile, bool isOnlineInstall)
    {
        if (!isOnlineInstall) return;

        foreach (var name in new[] { "原始整合包.zip", "原始整合包.mrpack" })
        {
            var path = Path.Combine(instanceFolder, name);
            if (!File.Exists(path)) continue;

            try
            {
                File.Delete(path);
                ModBase.Log("[Modpack] 已删除原始整合包文件：" + path);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[Modpack] 删除原始整合包文件失败：" + path);
            }
        }

        // 在线安装时下载的临时文件统一命名为 modpack.*
        if (!File.Exists(sourceFile) ||
            !string.Equals(Path.GetFileNameWithoutExtension(sourceFile), "modpack", StringComparison.Ordinal))
            return;

        try
        {
            File.Delete(sourceFile);
            ModBase.Log("[Modpack] 已删除安装用的整合包文件：" + sourceFile);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Modpack] 删除安装用的整合包文件失败：" + sourceFile);
        }
    }

    /// <summary>
    ///     确定实例名：整合包名称可用则采纳，否则询问用户。
    /// </summary>
    /// <exception cref="ModBase.CancelledException">用户取消输入。</exception>
    private static string _ResolveInstanceName(string? preferred)
    {
        var validator = new FolderNameValidator(Path.Combine(ModFolder.mcFolderSelected, "versions"));

        if (!string.IsNullOrWhiteSpace(preferred) && validator.Validate(preferred).IsValid) return preferred;

        var input = ModMain.MyMsgBoxInput(
            Lang.Text("Minecraft.Download.Modpack.InputInstanceName"), "", "", [validator]);

        if (string.IsNullOrEmpty(input)) throw new ModBase.CancelledException();
        return input;
    }

    private static string _BuildTaskName(ModpackFormat format, string instanceName) => format switch
    {
        ModpackFormat.CurseForge => Lang.Text("Minecraft.Download.Modpack.Task.CurseForgeInstall", instanceName),
        ModpackFormat.Modrinth => Lang.Text("Minecraft.Download.Modpack.Task.ModrinthInstall", instanceName),
        ModpackFormat.MultiMc => Lang.Text("Minecraft.Download.Modpack.Task.MmcInstall", instanceName),
        ModpackFormat.Mcbbs => Lang.Text("Minecraft.Download.Modpack.Task.McbbsInstall", instanceName),
        ModpackFormat.Hmcl => Lang.Text("Minecraft.Download.Modpack.Task.HmclInstall", instanceName),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "不支持的整合包格式")
    };
}
