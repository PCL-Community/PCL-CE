using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using FluentValidation;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Minecraft.ResourceProject;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.Utils.Validate;
using PCL.Network;
using PCL.Network.Loaders;
using Control = System.Windows.Forms.Control;

namespace PCL;

public partial class PageDownloadCompDetail : IPageView
{
    private readonly PageDownloadCompDetailViewModel _vm = new();

    // 资源下载；整合包另存为
    public static Dictionary<Comp.CompType, string> cachedFolder = new(); // 仅在本次缓存的下载文件夹
    private MyCompItem _compItem;
    private bool _isFirstInit = true;
    private bool _refreshed;

    public void OnNavigatedTo(object? parameter) => RefreshOnNavigate();

    public void OnNavigatedFrom() { }

    private void RefreshOnNavigate()
    {
        if (_refreshed) return;
        _refreshed = true;
        Init();
    }

    private void Init()
    {
        Animations.AniBeginControl();
        _project = Launcher.frmMain.pageCurrent.additional.Value.CompProject;
        PanBack.ScrollToHome();
        // 重启加载器
        if (_isFirstInit)
            // 在 Me.Initialized 已经初始化了加载器，不再重复初始化
            _isFirstInit = false;
        else
            PageLoaderRestart(isForceRestart: true);
        // 放置当前工程
        if (_compItem is not null)
            PanIntro.Children.Remove(_compItem);
        _compItem = _project.ToCompItem(true, true);
        _compItem.CanInteraction = false;
        _compItem.ShowFavoriteBtn = false;
        _compItem.Margin = new Thickness(-7, -7, 0d, 8d);
        PanIntro.Children.Insert(0, _compItem);

        // 决定按钮显示
        _vm.ApplyProjectState(_project);
        RefreshFavoriteButton();

        Animations.AniEndControl();
    }

    // 整合包安装
    public async void Install_Click(MyListItem sender, EventArgs e)
    {
        if (!RestrictedFeaturePolicy.Check("online-modpack-install")) return;
        var control = sender as FrameworkElement;
        if (control is not null)
            control.IsEnabled = false;
        try
        {
            // 获取基本信息
            var file = (Comp.CompFile)sender.Tag;
            var loaderName =
                $"{(_project.FromCurseForge ? "CurseForge" : "Modrinth")} {Lang.Text("Download.Comp.Detail.ModpackDownload")}：{_project.TranslatedName} ";

            // 获取实例名
            var packName = _project.TranslatedName.Replace(".zip", "").Replace(".rar", "").Replace(".mrpack", "")
                .Replace(@"\", "＼").Replace("/", "／").Replace("|", "｜").Replace(":", "：").Replace("<", "＜")
                    .Replace(">", "＞").Replace("*", "＊").Replace("?", "？").Replace("\"", "").Replace("： ", "：");
            var validate = new FolderNameValidator(GameFolders.mcFolderSelected + "versions");
            if (!validate.Validate(packName).IsValid)
                packName = "";
            var instanceName = await Launcher.MyMsgBoxInputAsync(Lang.Text("Download.Comp.Detail.InputInstanceName"), "",
                packName, [validate]);
            if (string.IsNullOrEmpty(instanceName))
                return;

            // 构造步骤加载器
            var loaders = new List<Loading.LoaderBase>();
            var target =
                $@"{GameFolders.mcFolderSelected}versions\{instanceName}\原始整合包.{(_project.FromCurseForge ? "zip" : "mrpack")}";
            var logoFileAddress = MyImage.GetTempPath(_compItem.Logo);
            loaders.Add(new LoaderDownload(Lang.Text("Download.Comp.Detail.DownloadModpackFile"),
                    new List<DownloadFile> { file.ToNetFile(target, Comp.DownloadReason.ModPack) })
                { ProgressWeight = 10d, block = true });
            loaders.Add(new Loading.LoaderTask<int, int>(Lang.Text("Download.Comp.Detail.PrepareModpackInstall"),
                _ => Modpack.ModpackInstall(target, instanceName,
                    System.IO.File.Exists(logoFileAddress) ? logoFileAddress : null, file.ProjectId,
                    true)) { ProgressWeight = 0.1d });

            // 启动
            var loader = new Loading.LoaderCombo<string>(loaderName, loaders)
            {
                OnStateChanged = myLoader =>
                {
                    switch (myLoader.State)
                    {
                        case Common.LoadState.Failed:
                        {
                            HintService.Hint(
                                Lang.Text("Download.Comp.Detail.Task.Failed", myLoader.name,
                                    myLoader.Error.ToString()), HintType.Error);
                            break;
                        }
                        case Common.LoadState.Aborted:
                        {
                            HintService.Hint(Lang.Text("Download.Comp.Detail.Task.Cancelled", myLoader.name));
                            break;
                        }
                        case Common.LoadState.Loading:
                        {
                            return; // 不重新加载版本列表
                        }
                    }

                    DownloadUi.McInstallFailedClearFolder(myLoader);
                }
            };
            loader.Start(Path.Combine(GameFolders.mcFolderSelected, "versions", instanceName));
            Loading.LoaderTaskbarAdd(loader);
            Launcher.frmMain.BtnExtraDownload.ShowRefresh();
            Launcher.frmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            Common.Log(
                ex,
                "下载资源整合包失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }
        finally
        {
            if (control is not null)
                control.IsEnabled = true;
        }
    }

    // 世界下载
    public void InstallWorld_Click(MyListItem sender, EventArgs e)
    {
        try
        {
            // 获取基本信息
            var file = (Comp.CompFile)sender.Tag;
            var loaderName = $"{(_project.FromCurseForge ? "CurseForge" : "Modrinth")} {Lang.Text("Download.Comp.Detail.WorldDownload")}：{_project.TranslatedName} ";

            // 确认默认保存位置
            string defaultFolder = null;
            var subFolder = @"saves\";
            Func<McInstance, bool> isVersionSuitable = null;
            // 获取资源所需的加载器
            var allowedLoaders = new List<Comp.CompLoaderType>();
            if (file.ModLoaders.Any())
                allowedLoaders = file.ModLoaders;
            else if (_project.ModLoaders.Any()) allowedLoaders = _project.ModLoaders;
            Common.Log("[Comp] 世界要求的加载器种类：" + (allowedLoaders.Any() ? allowedLoaders.Join(" / ") : "无要求"));
            // 判断某个版本是否符合资源要求
            isVersionSuitable = version =>
            {
                if (version is null)
                    return false;
                if (!version.IsLoaded)
                    version.Load();
                if (file.GameVersions.Any(v => v.Contains(".")) && !file.GameVersions.Any(v =>
                        v.Contains(".") && (v ?? "") == (version.Info.VanillaName ?? "")))
                    return false;
                // 加载器
                if (!allowedLoaders.Any())
                    return true; // 无要求
                return false;
            };
            // 获取常规资源默认下载位置
            if (cachedFolder.ContainsKey(file.Type) && !string.IsNullOrEmpty(cachedFolder[file.Type]))
            {
                defaultFolder = cachedFolder.GetOrDefault(file.Type,
                    InstanceList.McMcInstanceSelected?.PathIndie ?? Common.ExePath);
                Common.Log($"[Comp] 使用上次下载时的文件夹作为默认下载位置：{defaultFolder}");
            }
            else if (InstanceList.McMcInstanceSelected is not null && isVersionSuitable(InstanceList.McMcInstanceSelected))
            {
                defaultFolder = $"{InstanceList.McMcInstanceSelected.PathIndie}{subFolder}";
                Directory.CreateDirectory(defaultFolder);
                Common.Log($"[Comp] 使用当前实例作为默认下载位置：{defaultFolder}");
            }
            else
            {
                // 查找所有可能的实例
                var needLoad = InstanceList.mcInstanceListLoader.State != Common.LoadState.Finished;
                if (needLoad)
                {
                    HintService.Hint(Lang.Text("Download.Comp.Detail.FindingApplicableInstance"));
                    Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
                        Loading.LoaderFolderRunType.ForceRun, 1, @"versions\", true);
                }

                var suitableVersions = InstanceList.mcInstanceList.Values.SelectMany(l => l)
                    .Where(v => isVersionSuitable(v)).Select(v => new DirectoryInfo($"{v.PathIndie}{subFolder}"));
                if (suitableVersions.Any())
                {
                    var selectedVersion = suitableVersions
                        .OrderByDescending(dir => dir.Exists ? dir.LastWriteTimeUtc : DateTime.MinValue)
                        .ThenByDescending(dir => dir.Exists ? dir.GetFiles().Length : -1).First(); // 先按文件夹更改时间降序
                    // 再按文件夹中的文件数量降序
                    defaultFolder = selectedVersion.FullName;
                    Directory.CreateDirectory(defaultFolder);
                    Common.Log($"[Comp] 使用适合的游戏实例作为默认下载位置：{defaultFolder}");
                }
                else
                {
                    defaultFolder = GameFolders.mcFolderSelected;
                    if (needLoad)
                        HintService.Hint(Lang.Text("Download.Comp.Detail.NoApplicableInstance"));
                    else
                        Common.Log("[Comp] 由于当前实例不兼容，使用当前的 MC 文件夹作为默认下载位置");
                }
            }

            var target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Comp.Detail.SelectWorldInstallLocation"), file.FileName ?? "", Lang.Text("Download.Comp.Detail.WorldFile.Filter"),
                defaultFolder);
            if (string.IsNullOrEmpty(target))
                return;

            // 构造步骤加载器
            var loaders = new List<Loading.LoaderBase>();
            var targetPath = target.BeforeLast(@"\");
            var logoFileAddress = MyImage.GetTempPath(_compItem.Logo);
            loaders.Add(new LoaderDownload(Lang.Text("Download.Comp.Detail.DownloadWorldFile"),
                    new List<DownloadFile>
                    {
                        file.ToNetFile(target, Comp.DownloadReason.Standalone, file.RawGameVersions.FirstOrDefault())
                    })
                { ProgressWeight = 10d, block = true });
            loaders.Add(new Loading.LoaderTask<int, int>(Lang.Text("Download.Comp.Detail.InstallWorld"),
                _ => Common.ExtractFile(target, targetPath, Encoding.UTF8)) { ProgressWeight = 0.1d, block = true });
            loaders.Add(new Loading.LoaderTask<int, int>(Lang.Text("Download.Comp.Detail.CleanCache"),
                _ => System.IO.File.Delete(target)));

            // 启动
            var loader = new Loading.LoaderCombo<int>(loaderName, loaders)
                { OnStateChanged = DownloadUi.LoaderStateChangedHintOnly };
            loader.Start();
            Loading.LoaderTaskbarAdd(loader);
            Launcher.frmMain.BtnExtraDownload.ShowRefresh();
            Launcher.frmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            Common.Log(
                ex,
                "下载世界资源失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }
    }

    public void Save_Click(object sender, EventArgs e)
    {
        // 获取点击项关联的文件对象
        var file = sender switch
        {
            FrameworkElement Element when Element.Tag is Comp.CompFile CompFile => CompFile,
            FrameworkElement Element when Element.Parent is FrameworkElement Parent && Parent.Tag is Comp.CompFile CompFile => CompFile,
            FrameworkElement Element when Element.Parent is FrameworkElement Parent && Parent.Parent is FrameworkElement GrandParent && GrandParent.Tag is Comp.CompFile CompFile => CompFile,
            _ => null
        };

        Common.RunInNewThread(() =>
        {
            try
            {
                var desc = file.Type switch
                {
                    Comp.CompType.ModPack => Lang.Text("Download.Comp.Type.Modpack"),
                    Comp.CompType.Mod => Lang.Text("Download.Comp.Type.Mod"),
                    Comp.CompType.ResourcePack => Lang.Text("Download.Comp.Type.ResourcePack"),
                    Comp.CompType.Shader => Lang.Text("Download.Comp.Type.Shader"),
                    Comp.CompType.DataPack => Lang.Text("Download.Comp.Type.DataPack"),
                    Comp.CompType.World => Lang.Text("Download.Comp.Type.World"),
                    _ => ""
                };

                // 确认默认保存位置
                string defaultFolder = null;
                var allowedLoaders = new List<Comp.CompLoaderType>();
                if (file.Type != Comp.CompType.ModPack)
                {
                    var subFolder = "";
                    switch (file.Type)
                    {
                        case Comp.CompType.Mod: subFolder = "mods\\"; break;
                        case Comp.CompType.ResourcePack: subFolder = "resourcepacks\\"; break;
                        case Comp.CompType.Shader: subFolder = "shaderpacks\\"; break;
                        case Comp.CompType.World: subFolder = "saves\\"; break;
                        case Comp.CompType.DataPack: subFolder = ""; break; // 导航到版本根目录
                    }

                    // 获取资源所需的加载器
                    if (file.ModLoaders.Any())
                        allowedLoaders = file.ModLoaders;
                    else if (_project.ModLoaders.Any()) allowedLoaders = _project.ModLoaders;
                    Common.Log(
                        $"[Comp] {desc}要求的加载器种类：{(allowedLoaders.Any() ? string.Join(" / ", allowedLoaders) : "无要求")}");

                    // 判断某个版本是否符合资源要求 (局部函数)
                    Func<McInstance, bool> isVersionSuitable = version =>
                    {
                        if (version is null) return false;
                        if (!version.IsLoaded) version.Load();

                        // 只对 Mod 和数据包进行版本检测
                        if (file.Type == Comp.CompType.Mod || file.Type == Comp.CompType.DataPack)
                            if (file.GameVersions.Any(v => v.Contains(".")) &&
                                !file.GameVersions.Any(v => v.Contains(".") && v == version.Info.VanillaName))
                                return false;

                        // 加载器判定
                        if (!allowedLoaders.Any()) return true; // 无要求
                        if (allowedLoaders.Contains(Comp.CompLoaderType.Forge) && version.Info.HasForge) return true;
                        if (allowedLoaders.Contains(Comp.CompLoaderType.Forge) && version.Info.HasCleanroom)
                            return true;
                        if (allowedLoaders.Contains(Comp.CompLoaderType.Fabric) &&
                            (version.Info.HasFabric || version.Info.HasLegacyFabric)) return true;
                        if (allowedLoaders.Contains(Comp.CompLoaderType.NeoForge) && version.Info.HasNeoForge)
                            return true;
                        if (allowedLoaders.Contains(Comp.CompLoaderType.LiteLoader) && version.Info.HasLiteLoader)
                            return true;
                        return false;
                    };

                    // 获取常规资源默认下载位置逻辑
                    if (cachedFolder.ContainsKey(file.Type) && !string.IsNullOrEmpty(cachedFolder[file.Type]))
                    {
                        defaultFolder = cachedFolder.GetOrDefault(file.Type,
                            InstanceList.McMcInstanceSelected?.PathIndie ?? Common.ExePath);
                        Common.Log($"[Comp] 使用上次下载时的文件夹作为默认下载位置：{defaultFolder}");
                    }
                    else if (InstanceList.McMcInstanceSelected is not null &&
                             isVersionSuitable(InstanceList.McMcInstanceSelected))
                    {
                        defaultFolder = $"{InstanceList.McMcInstanceSelected.PathIndie}{subFolder}";
                        Directory.CreateDirectory(defaultFolder);
                        Common.Log($"[Comp] 使用当前实例作为默认下载位置：{defaultFolder}");
                    }
                    else
                    {
                        // 查找所有可能的实例
                        var needLoad = InstanceList.mcInstanceListLoader.State != Common.LoadState.Finished;
                        if (needLoad)
                        {
                            HintService.Hint(Lang.Text("Download.Comp.Detail.FindingApplicableInstance"));
                            Loading.LoaderFolderRun(InstanceList.mcInstanceListLoader, GameFolders.mcFolderSelected,
                                Loading.LoaderFolderRunType.ForceRun, 1, "versions\\", true);
                        }

                        var suitableVersions = InstanceList.mcInstanceList.Values.SelectMany(l => l)
                            .Where(v => isVersionSuitable(v))
                            .Select(v => new DirectoryInfo($"{v.PathIndie}{subFolder}"));

                        if (suitableVersions.Any())
                        {
                            var selectedVersion = suitableVersions
                                .OrderByDescending(dir => dir.Exists ? dir.LastWriteTimeUtc : DateTime.MinValue)
                                .ThenByDescending(dir => dir.Exists ? dir.GetFiles().Length : -1)
                                .First();
                            defaultFolder = selectedVersion.FullName;
                            Directory.CreateDirectory(defaultFolder);
                            Common.Log($"[Comp] 使用适合的游戏实例作为默认下载位置：{defaultFolder}");
                        }
                        else
                        {
                            defaultFolder = GameFolders.mcFolderSelected;
                            if (needLoad)
                                HintService.Hint(Lang.Text("Download.Comp.Detail.NoApplicableInstance"));
                            else
                                Common.Log("[Comp] 由于当前实例不兼容，使用当前的 MC 文件夹作为默认下载位置");
                        }
                    }
                }

                // 获取文件名并弹窗
                var fileName = Comp.CompFileNameGet(_project, file);
                Common.RunInUi(() =>
                {
                    var target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Comp.Detail.SelectSaveLocation"),
                        fileName, Lang.Text("Download.Comp.Detail.ResourceFile.Filter", desc) + "|" +
                                  (file.Type == Comp.CompType.Mod
                                      ? file.FileName?.EndsWith(".litemod") == true ? "*.litemod" : "*.jar"
                                      : file.FileName?.EndsWith(".mrpack") == true
                                        || (string.IsNullOrWhiteSpace(file.FileName) && file.Type == Comp.CompType.ModPack && !file.FromCurseForge)
                                          ? "*.mrpack"
                                          : "*.zip"),
                        defaultFolder);

                    if (!target.Contains("\\")) return;

                    // 记录缓存路径
                    var targetDir = Common.GetPathFromFullPath(target);
                    if (target != defaultFolder)
                    {
                        if (cachedFolder.ContainsKey(file.Type))
                            cachedFolder[file.Type] = targetDir;
                        else
                            cachedFolder.Add(file.Type, targetDir);
                    }

                    if (file.Type == Comp.CompType.Mod && Config.Download.Comp.AutoInstallDependencies &&
                        file.Dependencies.Any())
                    {
                        try
                        {
                            McInstance? targetInstance = null;
                            var knownInstances = new List<McInstance>();
                            if (InstanceList.McMcInstanceSelected is not null)
                            {
                                knownInstances.Add(InstanceList.McMcInstanceSelected);
                            }

                            knownInstances.AddRange(InstanceList.mcInstanceList.Values.SelectMany(list => list)
                                .Where(instance => instance is not null));
                            targetInstance = knownInstances
                                .Distinct()
                                .FirstOrDefault(instance =>
                                    targetDir.StartsWith(instance.PathIndie, StringComparison.OrdinalIgnoreCase));
                            if (targetInstance is not null && !targetInstance.IsLoaded)
                            {
                                targetInstance.Load();
                            }

                            var mcVersion = targetInstance?.Info?.VanillaName
                                            ?? file.GameVersions.FirstOrDefault(version => version.Contains("."))
                                            ?? string.Empty;
                            var targetLoaders = new List<Comp.CompLoaderType>();
                            if (targetInstance is not null)
                            {
                                if (targetInstance.Info.HasForge || targetInstance.Info.HasCleanroom)
                                    targetLoaders.Add(Comp.CompLoaderType.Forge);
                                if (targetInstance.Info.HasFabric || targetInstance.Info.HasLegacyFabric)
                                    targetLoaders.Add(Comp.CompLoaderType.Fabric);
                                if (targetInstance.Info.HasQuilt)
                                    targetLoaders.Add(Comp.CompLoaderType.Quilt);
                                if (targetInstance.Info.HasNeoForge)
                                    targetLoaders.Add(Comp.CompLoaderType.NeoForge);
                                if (targetInstance.Info.HasLiteLoader)
                                    targetLoaders.Add(Comp.CompLoaderType.LiteLoader);
                            }

                            if (!targetLoaders.Any())
                            {
                                targetLoaders = allowedLoaders.ToList();
                            }

                            Common.Log($"[CompDeps] 开始解析必需前置: {file.Dependencies.Count} 个依赖");
                            var request = CompDependency.BuildRequest(file, _project, mcVersion, targetLoaders,
                                targetDir);
                            var resolver = new ModDependencyResolver();
                            var result = resolver.Resolve(request);

                            void DownloadDependencies()
                            {
                                if (!result.ToInstall.Any())
                                {
                                    Common.Log("[CompDeps] 所有前置均无法解析，仅下载 Mod 本体");
                                    return;
                                }

                                Common.Log($"[CompDeps] 准备下载: {result.ToInstall.Count} 个前置");
                                var depDownloads = CompDependency.BuildDependencyDownloads(result, targetDir);
                                foreach (var (depFilename, downloadFile) in depDownloads)
                                {
                                    var depLoaderName = Lang.Text("Download.Comp.Detail.DownloadResource", desc,
                                        Path.GetFileNameWithoutExtension(depFilename));
                                    var depLoaders = new List<Loading.LoaderBase>
                                    {
                                        new LoaderDownload(Lang.Text("Download.Comp.Detail.DownloadFile"),
                                            new List<DownloadFile> { downloadFile })
                                        {
                                            ProgressWeight = 6,
                                            block = true
                                        }
                                    };

                                    // 启动加载器
                                    var depLoader = new Loading.LoaderCombo<int>(depLoaderName, depLoaders);
                                    depLoader.OnStateChanged = DownloadUi.LoaderStateChangedHintOnly;
                                    depLoader.Start(1);
                                    Loading.LoaderTaskbarAdd(depLoader);
                                }
                            }

                            if (result.Unresolved.Any() || result.ToInstall.Any())
                            {
                                var installChoice = CompDependency.ConfirmDependencyInstall(result);

                                switch (installChoice)
                                {
                                    case Comp.CompDepsInstallTypes.Unresolved:
                                        Common.Log("[CompDeps] 发现无法解析的前置");
                                        DownloadDependencies();
                                        break;

                                    case Comp.CompDepsInstallTypes.WithDeps:
                                        DownloadDependencies();
                                        break;

                                    case Comp.CompDepsInstallTypes.WithoutDeps:
                                        Common.Log("[CompDeps] 用户选择仅下载 Mod 本体，跳过前置下载");
                                        break;

                                    case Comp.CompDepsInstallTypes.Cancel:
                                        Common.Log("[CompDeps] 用户取消安装");
                                        return;

                                    default:
                                        Common.Log($"[CompDeps] 未知返回值: {installChoice} ，终止下载");
                                        return;
                                }
                            }
                            else
                            {
                                Common.Log("[CompDeps] 已满足: 所有必需前置已安装");
                            }
                        }
                        catch (Exception depEx)
                        {
                            Common.Log(depEx, "[CompDeps] 依赖解析失败，跳过前置安装");
                            var message = ExceptionDetails.Compose(
                                Lang.Text("Download.Comp.Dependency.ResolveFailed.Message"),
                                depEx);
                            Launcher.MyMsgBox(
                                message,
                                Lang.Text("Download.Comp.Dependency.ResolveFailed.Title"),
                                Lang.Text("Download.Comp.Dependency.ResolveFailed.Continue"),
                                isWarn: true,
                                forceWait: true);
                        }
                    }

                    // 构造下载任务
                    var loaderName = Lang.Text("Download.Comp.Detail.DownloadResource", desc,
                        Path.GetFileNameWithoutExtension(target));
                    var loaders = new List<Loading.LoaderBase>
                    {
                        new LoaderDownload(Lang.Text("Download.Comp.Detail.DownloadFile"),
                            new List<DownloadFile>
                            {
                                file.Type == Comp.CompType.Mod
                                    ? file.ToNetFile(target)
                                    : file.ToNetFile(target, Comp.DownloadReason.Standalone, null)
                            })
                        {
                            ProgressWeight = 6,
                            block = true
                        }
                    };

                    // 启动加载器
                    var loader = new Loading.LoaderCombo<int>(loaderName, loaders);
                    loader.OnStateChanged = DownloadUi.LoaderStateChangedHintOnly;
                    loader.Start(1);
                    Loading.LoaderTaskbarAdd(loader);

                    Launcher.frmMain.BtnExtraDownload.ShowRefresh();
                    Launcher.frmMain.BtnExtraDownload.Ribble();
                });
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    "保存资源文件失败",
                    Common.LogLevel.Feedback,
                    userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
            }
        }, "Download CompDetail Save");
    }

    private void BtnIntroWeb_Click(object sender, Common.RouteEventArgs e)
    {
        Common.OpenWebsite(_project.Website);
    }

    private void BtnIntroWiki_Click(object sender, Common.RouteEventArgs e)
    {
        Common.OpenWebsite("https://www.mcmod.cn/class/" + _project.WikiId + ".html");
    }

    private void BtnIntroCopy_Click(object sender, Common.RouteEventArgs e)
    {
        Common.ClipboardSet(_compItem.LabTitle.Text + _compItem.LabTitleRaw.Text);
    }

    private void BtnFavorites_Click(object sender, Common.RouteEventArgs e)
    {
        Comp.CompFavorites.ShowMenu(_project, (UIElement)sender, RefreshFavoriteButton);
    }

    private void BtnIntroLinkCopy_Click(object sender, Common.RouteEventArgs e)
    {
        Comp.CompClipboard.currentText = _project.Website;
        Common.ClipboardSet(_project.Website);
    }

    // 翻译简介
    private async void BtnTranslate_Click(object sender, Common.RouteEventArgs e)
    {
        var control = sender as FrameworkElement;
        if (control is not null)
            control.IsEnabled = false;
        try
        {
            HintService.Hint(Lang.Text("Download.Comp.Detail.DescriptionTranslating", _project.TranslatedName));
            var chineseDescription = await _project.ChineseDescription;
            if (chineseDescription is null)
                return;
            await Launcher.MyMsgBoxAsync(Lang.Text("Download.Comp.Detail.DescriptionTranslationResult",
                _project.Description, chineseDescription));
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "翻译资源简介失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }
        finally
        {
            if (control is not null)
                control.IsEnabled = true;
        }
    }

    /// <summary>
    ///     刷新收藏按钮的显示状态
    /// </summary>
    public void RefreshFavoriteButton()
    {
        try
        {
            if (_project is null) return;

            _vm.RefreshFavorite(_project.Id);
            BtnFavorites.SvgIcon = _vm.IsFavorite ? "lucide/heart-filled" : "lucide/heart";

            // 刷新顶部的项目卡片收藏状态
            if (_compItem is not null)
                _compItem.RefreshFavoriteStatus();
        }
        catch (Exception ex)
        {
            Common.Log(ex, "刷新收藏按钮状态时出错");
        }
    }

    #region 加载器

    private readonly Loading.LoaderTask<int, List<Comp.CompFile>> _compFileLoader;

    public PageDownloadCompDetail()
    {
        _compFileLoader = new Loading.LoaderTask<int, List<Comp.CompFile>>("Comp File", task =>
        {
            LoadTargetFromAdditional();
            var result = Comp.CompFilesGet(_project.Id, _project.FromCurseForge);
            if (task.IsAborted)
                return;
            task.output = result;
        });
        Initialized += PageDownloadCompDetail_Inited;
        Loaded += (_, _) => LoadTargetFromAdditional();
        Unloaded += (_, _) => _refreshed = false;
        PageEnter += RefreshOnNavigate;
        InitializeComponent();
        DataContext = _vm;
        _vm.FilterSelectionChanged = UpdateFilterResult;
    }

    public PageDownloadCompDetailViewModel ViewModel => _vm;

    // 初始化加载器信息
    private void PageDownloadCompDetail_Inited(object sender, EventArgs e)
    {
        LoadTargetFromAdditional();
        PageLoaderInit(Load, PanLoad, PanMain, CardIntro, _compFileLoader, _ => Load_OnFinish());
    }

    public void LoadTargetFromAdditional()
    {
        var additional = Launcher.frmMain.pageCurrent.additional.Value;
        _project = additional.CompProject;
        _targetInstance = additional.TargetVersion;
        _targetLoader = additional.TargetLoader;
        _pageType = additional.ResourceType;
    }

    private Comp.CompProject _project;
    private string _targetInstance;
    private Comp.CompLoaderType _targetLoader;

    /// <summary>
    ///     当前页面应展示的内容类别。可能为 Any。
    /// </summary>
    private Comp.CompType _pageType;

    // 自动重试
    private void Load_State(object sender, MyLoading.MyLoadingState state, MyLoading.MyLoadingState oldState)
    {
        switch (_compFileLoader.State)
        {
            case Common.LoadState.Failed:
            {
                var errorMessage = "";
                if (_compFileLoader.Error is not null)
                    errorMessage = _compFileLoader.Error.Message;
                if (errorMessage.Contains(Lang.Text("Common.Error.InvalidJson")))
                {
                    Common.Log("[Comp] 下载的文件 Json 列表损坏，已自动重试", Common.LogLevel.Debug);
                    PageLoaderRestart();
                }

                break;
            }
        }
    }

    private bool groupedDrop; // 是否按 Drop 筛选（1.21 / 1.20 / 1.19 / ...）而非小版本号（1.21.1 / 1.21 / 1.20.4 / ...）

    private bool groupedOld; // 是否折叠远古版本为一个选项

    private void Load_OnFinish()
    {
        var results = PageDownloadCompDetailViewModel.FilterResults(_compFileLoader.output, _pageType);

        // 初始化筛选器
        List<string> instanceFilters = null;
        List<string> modLoaderFilters = null;

        void updateFilters()
        {
            instanceFilters = results.SelectMany(v => v.GameVersions)
                .Select(v => GetGroupedVersionName(v, groupedDrop, groupedOld)).Distinct()
                .OrderByDescending(s => s, new McVersionComparer.VersionComparer()).ToList();
            modLoaderFilters = results.SelectMany(v => v.ModLoaders).Select(l => l.ToString()).Distinct()
                .OrderByDescending(s => s).ToList();
        }

        ;

        // 确定分组方式
        groupedDrop = false;
        groupedOld = false;
        updateFilters();
        if (instanceFilters.Count >= 9)
        {
            groupedDrop = true;
            groupedOld = false;
            updateFilters();
            if (instanceFilters.Count >= 9)
            {
                groupedDrop = false;
                groupedOld = true;
                updateFilters();
                if (instanceFilters.Count >= 9)
                {
                    groupedDrop = true;
                    groupedOld = true;
                    updateFilters();
                }
            }
        }


        // UI 化筛选器
        _vm.InstanceFilterOptions.Clear();
        _vm.ModLoaderFilterOptions.Clear();
        _vm.InstanceFilterLabelChips.Clear();
        _vm.ModLoaderFilterLabelChips.Clear();
        if (_pageType == Comp.CompType.Mod)
        {
            PanInstanceFilterHost.Margin = new Thickness(10d, 10d, 0d, 5d);
            PanModLoaderFilter.Margin = new Thickness(10d, 5d, 0d, 10d);
        }
        else
        {
            PanInstanceFilterHost.Margin = new Thickness(10d, 10d, 0d, 10d);
            PanModLoaderFilter.Margin = new Thickness(0d);
        }

        _vm.SetFilterVisibility(instanceFilters.Count >= 2);
        if (instanceFilters.Count < 2)
        {
            _vm.SetInstanceFilter(null);
        }
        else
        {
            // 插入标签
            if (_pageType == Comp.CompType.Mod)
            {
                _vm.InstanceFilterLabelChips.Add(
                    new CompFilterChip(Lang.Text("Download.Comp.Detail.InstanceFilter")));
                _vm.ModLoaderFilterLabelChips.Add(
                    new CompFilterChip(Lang.Text("Download.Comp.Detail.LoaderFilter")));
            }

            instanceFilters.Insert(0, Lang.Text("Common.Option.All"));
            modLoaderFilters.Insert(0, Lang.Text("Common.Option.All"));
            // 转化为按钮
            foreach (var version in instanceFilters)
                _vm.InstanceFilterOptions.Add(new CompFilterOption(version));

            if (_pageType == Comp.CompType.Mod)
                foreach (var loader in modLoaderFilters)
                    _vm.ModLoaderFilterOptions.Add(new CompFilterOption(loader));

            // 自动选择
            CompFilterOption? instanceToCheck = null;
            CompFilterOption? modLoaderToCheck = null;
            if (!string.IsNullOrEmpty(_targetInstance))
            {
                var targetFile = results.FirstOrDefault(v => v.GameVersions.Contains(_targetInstance));
                if (targetFile is not null)
                {
                    var targetGroup = GetGroupedVersionName(_targetInstance, groupedDrop, groupedOld);
                    instanceToCheck = _vm.InstanceFilterOptions.FirstOrDefault(option =>
                        (option.Text ?? "") == (targetGroup ?? ""));
                }
            }

            if (_pageType == Comp.CompType.Mod)
                if (_targetLoader != Comp.CompLoaderType.Any)
                {
                    var targetFile = results.FirstOrDefault(v => v.ModLoaders.Contains(_targetLoader));
                    if (targetFile is not null)
                        modLoaderToCheck = _vm.InstanceFilterOptions.Skip(1).FirstOrDefault(option =>
                            (option.Text ?? "") == (_targetLoader.ToString() ?? ""));
                }

            // 注意：在 Mod 下加载器筛选项与实例筛选项分别存放
            instanceToCheck ??= _vm.InstanceFilterOptions.FirstOrDefault();
            if (_pageType == Comp.CompType.Mod)
                modLoaderToCheck ??= _vm.ModLoaderFilterOptions.FirstOrDefault();
            _vm.SetInstanceFilter(instanceToCheck);
            if (_pageType == Comp.CompType.Mod)
                _vm.SetModLoaderFilter(modLoaderToCheck);
        }

        // 更新筛选结果（文件列表 UI 化）
        UpdateFilterResult();
    }

    private void UpdateFilterResult()
    {
        var results = PageDownloadCompDetailViewModel.FilterResults(_compFileLoader.output, _pageType);
        if (results is null)
            return;

        var instanceFilter = _vm.InstanceFilterValue;
        var modLoaderFilter = _vm.ModLoaderFilterValue;

        // 1. 预处理基础变量
        var targetVersionText = _targetLoader != Comp.CompLoaderType.Any ? _targetLoader + " " : "";
        var targetCardName = !string.IsNullOrEmpty(_targetInstance) || _targetLoader != Comp.CompLoaderType.Any
            ? Lang.Text("Download.Comp.Detail.SelectedVersion", targetVersionText, _targetInstance)
            : "";

        // 使用 HashSet 提高查询性能 O(1)
        var supportedLoaders =
            new HashSet<Comp.CompLoaderType>(Enum.GetValues(typeof(Comp.CompLoaderType))
                .Cast<Comp.CompLoaderType>());
        var ignoreQuilt = Config.Download.Comp.IgnoreQuilt;
        var hasMultipleLoaders = _project.ModLoaders.Count > 1;

        // 2. 核心数据归类 (使用 Dictionary 配合 HashSet 去重)
        var dict = new SortedDictionary<string, List<Comp.CompFile>>(
            new PageDownloadCompDetailViewModel.CardSorter(targetCardName));
        dict.Add(Lang.Text("Download.Comp.Detail.VersionGroup.Other"), new List<Comp.CompFile>());

        // 用于记录每个卡片内已存在的 version，防止 Contains(version) 的 O(n) 消耗
        var versionDuplicateChecker = new Dictionary<string, HashSet<Comp.CompFile>>();

        foreach (var version in results)
        {
            // 处理普通卡片归类
            foreach (var gameVersion in version.GameVersions)
            {
                // 筛选器预检查
                var currentGroupedName = GetGroupedVersionName(gameVersion, groupedDrop, groupedOld);
                if (instanceFilter is not null && (currentGroupedName ?? "") != (instanceFilter ?? ""))
                    continue;
                var verName = GetGroupedVersionName(gameVersion, false, false);
                var loaders = new List<string>();

                // 判定 Loader 逻辑
                if (hasMultipleLoaders && version.Type == Comp.CompType.Mod &&
                    McInstanceInfo.IsFormatFit(verName))
                {
                    foreach (var loader in version.ModLoaders)
                    {
                        if (loader == Comp.CompLoaderType.Quilt && ignoreQuilt)
                            continue;
                        if (!supportedLoaders.Contains(loader))
                            continue;

                        // 模组加载器筛选器
                        if (modLoaderFilter is not null && (loader.ToString() ?? "") != (modLoaderFilter ?? ""))
                            continue;

                        loaders.Add(loader + " ");
                    }

                    if (loaders.Count == 0 && modLoaderFilter is not null) continue;
                }

                if (loaders.Count == 0)
                    loaders.Add("");

                // 填充数据
                foreach (var loaderPrefix in loaders)
                {
                    var targetKey = loaderPrefix + verName;
                    AddVersionToDict(dict, versionDuplicateChecker, targetKey, version);
                }
            }

            // 处理“所选版本”卡片 (逻辑合并，减少二次循环)
            if (!string.IsNullOrEmpty(targetCardName))
            {
                var isMatchFilter = instanceFilter is null ||
                                    GetGroupedVersionName(_targetInstance, groupedDrop, groupedOld)
                                        .StartsWithF(instanceFilter);

                if (isMatchFilter && version.GameVersions.Contains(_targetInstance))
                    if (_targetLoader == Comp.CompLoaderType.Any || version.ModLoaders.Contains(_targetLoader))
                        // 再次检查 version 是否符合筛选器（针对该文件的所有游戏版本）
                        if (instanceFilter is null || version.GameVersions.Any(v =>
                                (GetGroupedVersionName(v, groupedDrop, groupedOld) ?? "") == (instanceFilter ?? "")))
                            AddVersionToDict(dict, versionDuplicateChecker, targetCardName, version);
            }
        }

        // 3. 渲染 UI
        try
        {
            PanResults.Children.Clear();
            var additional = Launcher.frmMain.pageCurrent.additional;
            var additionalTitles = additional is not null
                ? additional.Value.ExpandedTitles
                : new List<string>();

            foreach (var pair in dict)
            {
                if (pair.Value.Count == 0)
                    continue;

                // 创建卡片组件
                var newCard = new MyCard
                {
                    Title = pair.Key,
                    Margin = new Thickness(0d, 0d, 0d, 15d)
                };

                // 闭包引用：避免在 Sub 内做高耗时操作
                var files = pair.Value;
                var currentKey = pair.Key;

                var newStack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top,
                    Tag = files
                };

                newCard.Children.Add(newStack);
                newCard.SwapControl = newStack;

                // 延迟加载安装项的逻辑
                newCard.InstallMethod = stack =>
                {
                    var list = (List<Comp.CompFile>)stack.Tag;
                    // 排序和去重检查
                    list.Sort((a, b) => b.ReleaseDate.CompareTo(a.ReleaseDate));
                    var distinctCount = list.Select(f => f.DisplayName).Distinct().Count();
                    var badDisplayName = distinctCount != list.Count;

                    // 批量添加子项
                    switch (_project.Type)
                    {
                        case Comp.CompType.ModPack:
                        {
                            foreach (var item in list)
                                stack.Children.Add(item.ToListItem(
                                    (sender, e) => Launcher.frmDownloadCompDetail.Install_Click((MyListItem)sender, e),
                                    Launcher.frmDownloadCompDetail.Save_Click, badDisplayName));
                            break;
                        }
                        case Comp.CompType.World:
                        {
                            foreach (var item in list)
                                stack.Children.Add(item.ToListItem(
                                    (sender, e) =>
                                        Launcher.frmDownloadCompDetail.InstallWorld_Click((MyListItem)sender, e),
                                    Launcher.frmDownloadCompDetail.Save_Click, badDisplayName));
                            break;
                        }

                        default:
                        {
                            Comp.CompFilesCardPreload(stack, list);
                            foreach (var item in list)
                                stack.Children.Add(item.ToListItem(Launcher.frmDownloadCompDetail.Save_Click,
                                    badDisplayName: badDisplayName));
                            break;
                        }
                    }
                };

                PanResults.Children.Add(newCard);

                // 展开逻辑
                if ((currentKey ?? "") == (targetCardName ?? "") || additionalTitles.Contains(newCard.Title))
                    newCard.StackInstall();
                else
                    newCard.IsSwapped = true;

                // 特殊提示
                if (currentKey == Lang.Text("Download.Comp.Detail.VersionGroup.Other"))
                    newStack.Children.Add(new MyHint
                    {
                        Text = Lang.Text("Download.Comp.Detail.VersionRecognitionDelayHint"),
                        Theme = MyHint.Themes.Yellow,
                        Margin = new Thickness(5d, 0d, 0d, 8d)
                    });
            }

            // 单卡片自动展开
            if (PanResults.Children.Count == 1) ((MyCard)PanResults.Children[0]).IsSwapped = false;
        }

        catch (Exception ex)
        {
            Common.Log(
                ex,
                "可视化工程下载列表出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     辅助方法：向字典添加数据并处理去重
    /// </summary>
    private void AddVersionToDict(SortedDictionary<string, List<Comp.CompFile>> dict,
        Dictionary<string, HashSet<Comp.CompFile>> checker, string key, Comp.CompFile version)
    {
        if (!dict.ContainsKey(key))
        {
            dict.Add(key, new List<Comp.CompFile>());
            checker.Add(key, new HashSet<Comp.CompFile>());
        }

        // 使用 HashSet.Add 判断是否重复，比 List.Contains 快得多
        if (checker[key].Add(version)) dict[key].Add(version);
    }

    private string GetGroupedVersionName(string name, bool groupedByDrop, bool foldOld)
    {
        if (name is null)
            return Lang.Text("Download.Comp.Detail.VersionGroup.Other");
        if (name.Contains('w'))
            return Lang.Text("Download.Comp.Detail.VersionGroup.Snapshot");
        if (foldOld && McInstanceInfo.VersionToDrop(name, true) < 120)
            return Lang.Text("Download.Comp.Detail.VersionGroup.Old");
        if (groupedByDrop)
        {
            var drop = McInstanceInfo.VersionToDrop(name, true);
            if (drop >= 0)
                return McInstanceInfo.DropToVersion(drop);
            return name;
        }

        return name;
    }

    #endregion
}

/// <summary>为筛选器模板中的单选框提供内部文本边距的附加属性。</summary>
public static class CompFilterRadioButton
{
    public static readonly DependencyProperty TextMarginProperty = DependencyProperty.RegisterAttached(
        "TextMargin", typeof(Thickness), typeof(CompFilterRadioButton),
        new FrameworkPropertyMetadata(new Thickness(), OnTextMarginChanged));

    private static void OnTextMarginChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MyRadioButton radioButton && radioButton.LabText is not null)
            radioButton.LabText.Margin = (Thickness)e.NewValue;
    }

    public static Thickness GetTextMargin(DependencyObject element)
    {
        return (Thickness)element.GetValue(TextMarginProperty);
    }

    public static void SetTextMargin(DependencyObject element, Thickness value)
    {
        element.SetValue(TextMarginProperty, value);
    }
}
