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
using PCL.Core.Minecraft.ResourceProject;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.Utils.Validate;
using PCL.Network;
using PCL.Network.Loaders;
using Control = System.Windows.Forms.Control;

namespace PCL;

public partial class PageDownloadCompDetail
{
    // 资源下载；整合包另存为
    public static Dictionary<ModComp.CompType, string> cachedFolder = new(); // 仅在本次缓存的下载文件夹
    private MyCompItem _compItem;
    private bool _isFirstInit = true;

    private void Init()
    {
        ModAnimation.AniControlEnabled += 1;
        _project = ModMain.frmMain.pageCurrent.additional.Value.CompProject;
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
        BtnIntroWeb.Text = _project.FromCurseForge ? "CurseForge" : "Modrinth";
        BtnIntroWiki.Visibility = Lang.IsChineseMainland && _project.WikiId != 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        BtnTranslate.Visibility = Lang.IsChineseMainland
            ? Visibility.Visible
            : Visibility.Collapsed;
        RefreshFavoriteButton();

        ModAnimation.AniControlEnabled -= 1;
    }

    // 整合包安装
    public void Install_Click(MyListItem sender, EventArgs e)
    {
        try
        {
            // 获取基本信息
            var file = (ModComp.CompFile)sender.Tag;
            var loaderName =
                $"{(_project.FromCurseForge ? "CurseForge" : "Modrinth")} {Lang.Text("Download.Comp.Detail.ModpackDownload")}：{_project.TranslatedName} ";

            // 获取实例名
            var packName = _project.TranslatedName.Replace(".zip", "").Replace(".rar", "").Replace(".mrpack", "")
                .Replace(@"\", "＼").Replace("/", "／").Replace("|", "｜").Replace(":", "：").Replace("<", "＜")
                    .Replace(">", "＞").Replace("*", "＊").Replace("?", "？").Replace("\"", "").Replace("： ", "：");
            var validate = new FolderNameValidator(ModFolder.mcFolderSelected + "versions");
            if (!validate.Validate(packName).IsValid)
                packName = "";
            var instanceName = ModMain.MyMsgBoxInput(Lang.Text("Download.Comp.Detail.InputInstanceName"), "", packName, [validate]);
            if (string.IsNullOrEmpty(instanceName))
                return;

            // 构造步骤加载器
            var loaders = new List<ModLoader.LoaderBase>();
            var target =
                $@"{ModFolder.mcFolderSelected}versions\{instanceName}\原始整合包.{(_project.FromCurseForge ? "zip" : "mrpack")}";
            var logoFileAddress = MyImage.GetTempPath(_compItem.Logo);
            loaders.Add(new LoaderDownload(Lang.Text("Download.Comp.Detail.DownloadModpackFile"), new List<DownloadFile> { file.ToNetFile(target) })
                { ProgressWeight = 10d, block = true });
            loaders.Add(new ModLoader.LoaderTask<int, int>(Lang.Text("Download.Comp.Detail.PrepareModpackInstall"),
                _ => ModModpack.ModpackInstall(target, instanceName,
                    System.IO.File.Exists(logoFileAddress) ? logoFileAddress : null, file.ProjectId,
                    true)) { ProgressWeight = 0.1d });

            // 启动
            var loader = new ModLoader.LoaderCombo<string>(loaderName, loaders)
            {
                OnStateChanged = myLoader =>
                {
                    switch (myLoader.State)
                    {
                        case ModBase.LoadState.Failed:
                        {
                            HintService.Hint(myLoader.name + Lang.Text("Common.Status.Failure") + myLoader.Error.Message, HintType.Error);
                            break;
                        }
                        case ModBase.LoadState.Aborted:
                        {
                            HintService.Hint(myLoader.name + Lang.Text("Common.Status.Cancelled"));
                            break;
                        }
                        case ModBase.LoadState.Loading:
                        {
                            return; // 不重新加载版本列表
                        }
                    }

                    ModDownloadLib.McInstallFailedClearFolder(myLoader);
                }
            };
            loader.Start(Path.Combine(ModFolder.mcFolderSelected, "versions", instanceName));
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "下载资源整合包失败", ModBase.LogLevel.Feedback);
        }
    }

    // 世界下载
    public void InstallWorld_Click(MyListItem sender, EventArgs e)
    {
        try
        {
            // 获取基本信息
            var file = (ModComp.CompFile)sender.Tag;
            var loaderName = $"{(_project.FromCurseForge ? "CurseForge" : "Modrinth")} {Lang.Text("Download.Comp.Detail.WorldDownload")}：{_project.TranslatedName} ";

            // 确认默认保存位置
            string defaultFolder = null;
            var subFolder = @"saves\";
            Func<McInstance, bool> isVersionSuitable = null;
            // 获取资源所需的加载器
            var allowedLoaders = new List<ModComp.CompLoaderType>();
            if (file.ModLoaders.Any())
                allowedLoaders = file.ModLoaders;
            else if (_project.ModLoaders.Any()) allowedLoaders = _project.ModLoaders;
            ModBase.Log("[Comp] 世界要求的加载器种类：" + (allowedLoaders.Any() ? allowedLoaders.Join(" / ") : "无要求"));
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
                    ModInstanceList.McMcInstanceSelected?.PathIndie ?? ModBase.exePath);
                ModBase.Log($"[Comp] 使用上次下载时的文件夹作为默认下载位置：{defaultFolder}");
            }
            else if (ModInstanceList.McMcInstanceSelected is not null && isVersionSuitable(ModInstanceList.McMcInstanceSelected))
            {
                defaultFolder = $"{ModInstanceList.McMcInstanceSelected.PathIndie}{subFolder}";
                Directory.CreateDirectory(defaultFolder);
                ModBase.Log($"[Comp] 使用当前实例作为默认下载位置：{defaultFolder}");
            }
            else
            {
                // 查找所有可能的实例
                var needLoad = ModInstanceList.mcInstanceListLoader.State != ModBase.LoadState.Finished;
                if (needLoad)
                {
                    HintService.Hint(Lang.Text("Download.Comp.Detail.FindingApplicableInstance"));
                    ModLoader.LoaderFolderRun(ModInstanceList.mcInstanceListLoader, ModFolder.mcFolderSelected,
                        ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\", true);
                }

                var suitableVersions = ModInstanceList.mcInstanceList.Values.SelectMany(l => l)
                    .Where(v => isVersionSuitable(v)).Select(v => new DirectoryInfo($"{v.PathIndie}{subFolder}"));
                if (suitableVersions.Any())
                {
                    var selectedVersion = suitableVersions
                        .OrderByDescending(dir => dir.Exists ? dir.LastWriteTimeUtc : DateTime.MinValue)
                        .ThenByDescending(dir => dir.Exists ? dir.GetFiles().Length : -1).First(); // 先按文件夹更改时间降序
                    // 再按文件夹中的文件数量降序
                    defaultFolder = selectedVersion.FullName;
                    Directory.CreateDirectory(defaultFolder);
                    ModBase.Log($"[Comp] 使用适合的游戏实例作为默认下载位置：{defaultFolder}");
                }
                else
                {
                    defaultFolder = ModFolder.mcFolderSelected;
                    if (needLoad)
                        HintService.Hint(Lang.Text("Download.Comp.Detail.NoApplicableInstance"));
                    else
                        ModBase.Log("[Comp] 由于当前实例不兼容，使用当前的 MC 文件夹作为默认下载位置");
                }
            }

            var target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Comp.Detail.SelectWorldInstallLocation"), file.FileName, Lang.Text("Download.Comp.Detail.WorldFile.Filter"),
                defaultFolder);
            if (string.IsNullOrEmpty(target))
                return;

            // 构造步骤加载器
            var loaders = new List<ModLoader.LoaderBase>();
            var targetPath = target.BeforeLast(@"\");
            var logoFileAddress = MyImage.GetTempPath(_compItem.Logo);
            loaders.Add(new LoaderDownload(Lang.Text("Download.Comp.Detail.DownloadWorldFile"),
                new List<DownloadFile> { file.ToNetFile(target) }) { ProgressWeight = 10d, block = true });
            loaders.Add(new ModLoader.LoaderTask<int, int>(Lang.Text("Download.Comp.Detail.InstallWorld"),
                _ => ModBase.ExtractFile(target, targetPath, Encoding.UTF8)) { ProgressWeight = 0.1d, block = true });
            loaders.Add(new ModLoader.LoaderTask<int, int>(Lang.Text("Download.Comp.Detail.CleanCache"),
                _ => System.IO.File.Delete(target)));

            // 启动
            var loader = new ModLoader.LoaderCombo<int>(loaderName, loaders)
                { OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly };
            loader.Start();
            ModLoader.LoaderTaskbarAdd(loader);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "下载世界资源失败", ModBase.LogLevel.Feedback);
        }
    }

    public void Save_Click(object sender, EventArgs e)
    {
        // 获取点击项关联的文件对象
        var file = sender switch
        {
            FrameworkElement Element when Element.Tag is ModComp.CompFile CompFile => CompFile,
            FrameworkElement Element when Element.Parent is FrameworkElement Parent && Parent.Tag is ModComp.CompFile CompFile => CompFile,
            FrameworkElement Element when Element.Parent is FrameworkElement Parent && Parent.Parent is FrameworkElement GrandParent && GrandParent.Tag is ModComp.CompFile CompFile => CompFile,
            _ => null
        };

        ModBase.RunInNewThread(() =>
        {
            try
            {
                var desc = file.Type switch
                {
                    ModComp.CompType.ModPack => Lang.Text("Download.Comp.Type.Modpack"),
                    ModComp.CompType.Mod => Lang.Text("Download.Comp.Type.Mod"),
                    ModComp.CompType.ResourcePack => Lang.Text("Download.Comp.Type.ResourcePack"),
                    ModComp.CompType.Shader => Lang.Text("Download.Comp.Type.Shader"),
                    ModComp.CompType.DataPack => Lang.Text("Download.Comp.Type.DataPack"),
                    ModComp.CompType.World => Lang.Text("Download.Comp.Type.World"),
                    _ => ""
                };

                // 确认默认保存位置
                string defaultFolder = null;
                var allowedLoaders = new List<ModComp.CompLoaderType>();
                if (file.Type != ModComp.CompType.ModPack)
                {
                    var subFolder = "";
                    switch (file.Type)
                    {
                        case ModComp.CompType.Mod: subFolder = "mods\\"; break;
                        case ModComp.CompType.ResourcePack: subFolder = "resourcepacks\\"; break;
                        case ModComp.CompType.Shader: subFolder = "shaderpacks\\"; break;
                        case ModComp.CompType.World: subFolder = "saves\\"; break;
                        case ModComp.CompType.DataPack: subFolder = ""; break; // 导航到版本根目录
                    }

                    // 获取资源所需的加载器
                    if (file.ModLoaders.Any())
                        allowedLoaders = file.ModLoaders;
                    else if (_project.ModLoaders.Any()) allowedLoaders = _project.ModLoaders;
                    ModBase.Log(
                        $"[Comp] {desc}要求的加载器种类：{(allowedLoaders.Any() ? string.Join(" / ", allowedLoaders) : "无要求")}");

                    // 判断某个版本是否符合资源要求 (局部函数)
                    Func<McInstance, bool> isVersionSuitable = version =>
                    {
                        if (version is null) return false;
                        if (!version.IsLoaded) version.Load();

                        // 只对 Mod 和数据包进行版本检测
                        if (file.Type == ModComp.CompType.Mod || file.Type == ModComp.CompType.DataPack)
                            if (file.GameVersions.Any(v => v.Contains(".")) &&
                                !file.GameVersions.Any(v => v.Contains(".") && v == version.Info.VanillaName))
                                return false;

                        // 加载器判定
                        if (!allowedLoaders.Any()) return true; // 无要求
                        if (allowedLoaders.Contains(ModComp.CompLoaderType.Forge) && version.Info.HasForge) return true;
                        if (allowedLoaders.Contains(ModComp.CompLoaderType.Fabric) &&
                            (version.Info.HasFabric || version.Info.HasLegacyFabric)) return true;
                        if (allowedLoaders.Contains(ModComp.CompLoaderType.NeoForge) && version.Info.HasNeoForge)
                            return true;
                        if (allowedLoaders.Contains(ModComp.CompLoaderType.LiteLoader) && version.Info.HasLiteLoader)
                            return true;
                        return false;
                    };

                    // 获取常规资源默认下载位置逻辑
                    if (cachedFolder.ContainsKey(file.Type) && !string.IsNullOrEmpty(cachedFolder[file.Type]))
                    {
                        defaultFolder = cachedFolder.GetOrDefault(file.Type,
                            ModInstanceList.McMcInstanceSelected?.PathIndie ?? ModBase.exePath);
                        ModBase.Log($"[Comp] 使用上次下载时的文件夹作为默认下载位置：{defaultFolder}");
                    }
                    else if (ModInstanceList.McMcInstanceSelected is not null &&
                             isVersionSuitable(ModInstanceList.McMcInstanceSelected))
                    {
                        defaultFolder = $"{ModInstanceList.McMcInstanceSelected.PathIndie}{subFolder}";
                        Directory.CreateDirectory(defaultFolder);
                        ModBase.Log($"[Comp] 使用当前实例作为默认下载位置：{defaultFolder}");
                    }
                    else
                    {
                        // 查找所有可能的实例
                        var needLoad = ModInstanceList.mcInstanceListLoader.State != ModBase.LoadState.Finished;
                        if (needLoad)
                        {
                            HintService.Hint(Lang.Text("Download.Comp.Detail.FindingApplicableInstance"));
                            ModLoader.LoaderFolderRun(ModInstanceList.mcInstanceListLoader, ModFolder.mcFolderSelected,
                                ModLoader.LoaderFolderRunType.ForceRun, 1, "versions\\", true);
                        }

                        var suitableVersions = ModInstanceList.mcInstanceList.Values.SelectMany(l => l)
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
                            ModBase.Log($"[Comp] 使用适合的游戏实例作为默认下载位置：{defaultFolder}");
                        }
                        else
                        {
                            defaultFolder = ModFolder.mcFolderSelected;
                            if (needLoad)
                                HintService.Hint(Lang.Text("Download.Comp.Detail.NoApplicableInstance"));
                            else
                                ModBase.Log("[Comp] 由于当前实例不兼容，使用当前的 MC 文件夹作为默认下载位置");
                        }
                    }
                }

                // 获取文件名并弹窗
                var fileName = ModComp.CompFileNameGet(_project, file);
                ModBase.RunInUi(() =>
                {
                    var target = SystemDialogs.SelectSaveFile(Lang.Text("Download.Comp.Detail.SelectSaveLocation"),
                        fileName, Lang.Text("Download.Comp.Detail.ResourceFile.Filter", desc) + "|" +
                                  (file.Type == ModComp.CompType.Mod
                                      ? file.FileName.EndsWith(".litemod") ? "*.litemod" : "*.jar"
                                      : file.FileName.EndsWith(".mrpack")
                                          ? "*.mrpack"
                                          : "*.zip"),
                        defaultFolder);

                    if (!target.Contains("\\")) return;

                    // 记录缓存路径
                    var targetDir = ModBase.GetPathFromFullPath(target);
                    if (target != defaultFolder)
                    {
                        if (cachedFolder.ContainsKey(file.Type))
                            cachedFolder[file.Type] = targetDir;
                        else
                            cachedFolder.Add(file.Type, targetDir);
                    }

                    var downloadFiles = new List<DownloadFile> { file.ToNetFile(target) };
                    if (file.Type == ModComp.CompType.Mod && Config.Download.Comp.AutoInstallDependencies &&
                        file.Dependencies.Any())
                    {
                        try
                        {
                            McInstance? targetInstance = null;
                            var knownInstances = new List<McInstance>();
                            if (ModInstanceList.McMcInstanceSelected is not null)
                            {
                                knownInstances.Add(ModInstanceList.McMcInstanceSelected);
                            }

                            knownInstances.AddRange(ModInstanceList.mcInstanceList.Values.SelectMany(list => list)
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
                            var targetLoaders = new List<ModComp.CompLoaderType>();
                            if (targetInstance is not null)
                            {
                                if (targetInstance.Info.HasForge)
                                    targetLoaders.Add(ModComp.CompLoaderType.Forge);
                                if (targetInstance.Info.HasFabric || targetInstance.Info.HasLegacyFabric)
                                    targetLoaders.Add(ModComp.CompLoaderType.Fabric);
                                if (targetInstance.Info.HasQuilt)
                                    targetLoaders.Add(ModComp.CompLoaderType.Quilt);
                                if (targetInstance.Info.HasNeoForge)
                                    targetLoaders.Add(ModComp.CompLoaderType.NeoForge);
                                if (targetInstance.Info.HasLiteLoader)
                                    targetLoaders.Add(ModComp.CompLoaderType.LiteLoader);
                            }

                            if (!targetLoaders.Any())
                            {
                                targetLoaders = allowedLoaders.ToList();
                            }

                            ModBase.Log($"[CompDeps] 开始解析必需前置: {file.Dependencies.Count} 个依赖");
                            var request = ModCompDependency.BuildRequest(file, _project, mcVersion, targetLoaders,
                                targetDir);
                            var resolver = new ModDependencyResolver();
                            var result = resolver.Resolve(request);

                            if (result.Unresolved.Any() || result.ToInstall.Any())
                            {
                                if (!ModCompDependency.ConfirmDependencyInstall(result))
                                {
                                    return;
                                }

                                ModBase.Log($"[CompDeps] 准备下载: {result.ToInstall.Count} 个前置");
                                var depDownloads = ModCompDependency.BuildDependencyDownloads(result, targetDir);
                                downloadFiles = depDownloads.Concat(downloadFiles).ToList();
                            }
                            else
                            {
                                ModBase.Log("[CompDeps] 已满足: 所有必需前置已安装");
                            }
                        }
                        catch (Exception depEx)
                        {
                            ModBase.Log(depEx, "[CompDeps] 依赖解析失败，跳过前置安装");
                            ModMain.MyMsgBox("前置 Mod 解析失败，将仅下载本体。\n\n" + depEx.Message,
                                "前置解析失败", button1: "继续下载", isWarn: true, forceWait: true);
                        }
                    }

                    // 构造下载任务
                    var loaderName = Lang.Text("Download.Comp.Detail.DownloadResource", desc,
                        ModBase.GetFileNameWithoutExtentionFromPath(target));
                    var loaders = new List<ModLoader.LoaderBase>
                    {
                        new LoaderDownload(Lang.Text("Download.Comp.Detail.DownloadFile"),
                            downloadFiles)
                        {
                            ProgressWeight = 6,
                            block = true
                        }
                    };

                    // 启动加载器
                    var loader = new ModLoader.LoaderCombo<int>(loaderName, loaders);
                    loader.OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly;
                    loader.Start(1);
                    ModLoader.LoaderTaskbarAdd(loader);

                    ModMain.frmMain.BtnExtraDownload.ShowRefresh();
                    ModMain.frmMain.BtnExtraDownload.Ribble();
                });
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "保存资源文件失败", ModBase.LogLevel.Feedback);
            }
        }, "Download CompDetail Save");
    }

    private void BtnIntroWeb_Click(object sender, EventArgs e)
    {
        ModBase.OpenWebsite(_project.Website);
    }

    private void BtnIntroWiki_Click(object sender, EventArgs e)
    {
        ModBase.OpenWebsite("https://www.mcmod.cn/class/" + _project.WikiId + ".html");
    }

    private void BtnIntroCopy_Click(object sender, EventArgs e)
    {
        ModBase.ClipboardSet(_compItem.LabTitle.Text + _compItem.LabTitleRaw.Text);
    }

    private void BtnFavorites_Click(object sender, EventArgs e)
    {
        ModComp.CompFavorites.ShowMenu(_project, (UIElement)sender, RefreshFavoriteButton);
    }

    private void BtnIntroLinkCopy_Click(object sender, EventArgs e)
    {
        ModComp.CompClipboard.currentText = _project.Website;
        ModBase.ClipboardSet(_project.Website);
    }

    // 翻译简介
    private async void BtnTranslate_Click(object sender, EventArgs e)
    {
        HintService.Hint(Lang.Text("Download.Comp.Detail.DescriptionTranslating", _project.TranslatedName));
        var chineseDescription = await _project.ChineseDescription;
        if (chineseDescription is null)
            return;
        ModMain.MyMsgBox(Lang.Text("Download.Comp.Detail.DescriptionTranslationResult", _project.Description,
            chineseDescription));
    }

    /// <summary>
    ///     刷新收藏按钮的显示状态
    /// </summary>
    public void RefreshFavoriteButton()
    {
        try
        {
            if (_project is null) return;

            var isFavourite = ModComp.CompFavorites.IsFavourite(_project.Id);
            BtnFavorites.SvgIcon = isFavourite ? "lucide/heart-filled" : "lucide/heart";

            // 刷新顶部的项目卡片收藏状态
            if (_compItem is not null)
                _compItem.RefreshFavoriteStatus();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新收藏按钮状态时出错");
        }
    }

    #region 加载器

    private readonly ModLoader.LoaderTask<int, List<ModComp.CompFile>> _compFileLoader;

    public PageDownloadCompDetail()
    {
        _compFileLoader = new ModLoader.LoaderTask<int, List<ModComp.CompFile>>("Comp File", task =>
        {
            LoadTargetFromAdditional();
            var result = ModComp.CompFilesGet(_project.Id, _project.FromCurseForge);
            if (task.IsAborted)
                return;
            task.output = result;
        });
        Initialized += PageDownloadCompDetail_Inited;
        Loaded += (_, _) => LoadTargetFromAdditional();
        PageEnter += Init;
        InitializeComponent();
        Load.StateChanged += Load_State;
        BtnIntroWeb.Click += BtnIntroWeb_Click;
        BtnIntroWiki.Click += BtnIntroWiki_Click;
        BtnIntroCopy.Click += BtnIntroCopy_Click;
        BtnFavorites.Click += BtnFavorites_Click;
        BtnIntroLinkCopy.Click += BtnIntroLinkCopy_Click;
        BtnTranslate.Click += BtnTranslate_Click;
    }

    // 初始化加载器信息
    private void PageDownloadCompDetail_Inited(object sender, EventArgs e)
    {
        LoadTargetFromAdditional();
        PageLoaderInit(Load, PanLoad, PanMain, CardIntro, _compFileLoader, _ => Load_OnFinish());
    }

    public void LoadTargetFromAdditional()
    {
        var additional = ModMain.frmMain.pageCurrent.additional.Value;
        _project = additional.CompProject;
        _targetInstance = additional.TargetVersion;
        _targetLoader = additional.TargetLoader;
        _pageType = additional.ResourceType;
    }

    private ModComp.CompProject _project;
    private string _targetInstance;
    private ModComp.CompLoaderType _targetLoader;

    /// <summary>
    ///     当前页面应展示的内容类别。可能为 Any。
    /// </summary>
    private ModComp.CompType _pageType;

    // 自动重试
    private void Load_State(object sender, MyLoading.MyLoadingState state, MyLoading.MyLoadingState oldState)
    {
        switch (_compFileLoader.State)
        {
            case ModBase.LoadState.Failed:
            {
                var errorMessage = "";
                if (_compFileLoader.Error is not null)
                    errorMessage = _compFileLoader.Error.Message;
                if (errorMessage.Contains(Lang.Text("Common.Error.InvalidJson")))
                {
                    ModBase.Log("[Comp] 下载的文件 Json 列表损坏，已自动重试", ModBase.LogLevel.Debug);
                    PageLoaderRestart();
                }

                break;
            }
        }
    }

    // 结果 UI 化
    private class CardSorter : IComparer<string>
    {
        public readonly string topmost = "";

        public CardSorter(string topmost = "")
        {
            this.topmost = topmost ?? "";
        }

        public int Compare(string x, string y)
        {
            // 相同
            if ((x ?? "") == (y ?? ""))
                return 0;
            // 置顶
            if ((x ?? "") == (topmost ?? ""))
                return -1;
            if ((y ?? "") == (topmost ?? ""))
                return 1;
            // 特殊版本
            var isXSpecial = !x.Contains(".");
            var isYSpecial = !y.Contains(".");
            if (isXSpecial && isYSpecial)
                return string.Compare(x, y, StringComparison.Ordinal);
            if (isXSpecial)
                return 1;
            if (isYSpecial)
                return -1;
            // 比较版本号
            var versionCodeSort = -McVersionComparer.CompareVersion(x.Replace(x.BeforeFirst(" ") + " ", ""),
                y.Replace(y.BeforeFirst(" ") + " ", ""));
            if (versionCodeSort != 0)
                return versionCodeSort;
            // 比较全部
            return -McVersionComparer.CompareVersion(x, y);
        }
    }

    private string? _instanceFilter;
    private string? _modLoaderFilter;
    private bool groupedDrop; // 是否按 Drop 筛选（1.21 / 1.20 / 1.19 / ...）而非小版本号（1.21.1 / 1.21 / 1.20.4 / ...）

    private bool groupedOld; // 是否折叠远古版本为一个选项

    // 筛选类型相同的结果（Modrinth 会返回 Mod、服务端插件、数据包混合的列表）
    private List<ModComp.CompFile> GetResults()
    {
        var results = _compFileLoader.output;
        if (_pageType == ModComp.CompType.Any)
        {
            results = results.Where(r => r.Type != ModComp.CompType.Plugin).ToList();
        }
        else if (_pageType == ModComp.CompType.Shader || _pageType == ModComp.CompType.ResourcePack)
        {
        }
        // 不筛选光影和资源包，否则原版光影会因为是资源包格式而被过滤（Meloong-Git/#6473）
        else
        {
            results = results.Where(r => r.Type == _pageType).ToList();
        }

        return results;
    }

    private void Load_OnFinish()
    {
        var results = GetResults();

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
        PanInstanceFilter.Children.Clear();
        PanModLoaderFilter.Children.Clear();
        if (_pageType == ModComp.CompType.Mod)
        {
            PanInstanceFilter.Margin = new Thickness(10d, 10d, 0d, 5d);
            PanModLoaderFilter.Margin = new Thickness(10d, 5d, 0d, 10d);
        }
        else
        {
            PanInstanceFilter.Margin = new Thickness(10d, 10d, 0d, 10d);
            PanModLoaderFilter.Margin = new Thickness(0d);
        }

        if (instanceFilters.Count < 2)
        {
            CardFilter.Visibility = Visibility.Collapsed;
            _instanceFilter = null;
        }
        else
        {
            CardFilter.Visibility = Visibility.Visible;
            // 插入标签
            if (_pageType == ModComp.CompType.Mod)
            {
                var instanceTextBlock = new TextBlock
                {
                    Text = Lang.Text("Download.Comp.Detail.InstanceFilter"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2d, 0d, 0d, 0d)
                };
                PanInstanceFilter.Children.Add(instanceTextBlock);
                var modLoaderTextBlock = new TextBlock
                {
                    Text = Lang.Text("Download.Comp.Detail.LoaderFilter"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2d, 0d, 0d, 0d)
                };
                PanModLoaderFilter.Children.Add(modLoaderTextBlock);
            }

            instanceFilters.Insert(0, Lang.Text("Common.Option.All"));
            modLoaderFilters.Insert(0, Lang.Text("Common.Option.All"));
            // 转化为按钮
            foreach (var version in instanceFilters)
            {
                var newButton = new MyRadioButton
                {
                    Text = version, Margin = new Thickness(2d, 0d, 2d, 0d),
                    ColorType = MyRadioButton.ColorState.Highlight
                };
                newButton.LabText.Margin = new Thickness(-2, 0d, 10d, 0d);
                newButton.Check += (sender, raiseByMouse) =>
                {
                    _instanceFilter = sender.Text == Lang.Text("Common.Option.All") ? null : sender.Text;
                    UpdateFilterResult();
                };
                PanInstanceFilter.Children.Add(newButton);
            }

            if (_pageType == ModComp.CompType.Mod)
                foreach (var loader in modLoaderFilters)
                {
                    var newButton = new MyRadioButton
                    {
                        Text = loader,
                        Margin = new Thickness(2d, 0d, 2d, 0d),
                        ColorType = MyRadioButton.ColorState.Highlight
                    };
                    newButton.LabText.Margin = new Thickness(-2, 0d, 10d, 0d);
                    newButton.Check += (sender, raiseByMouse) =>
                    {
                        _modLoaderFilter = sender.Text == Lang.Text("Common.Option.All") ? null : sender.Text;
                        UpdateFilterResult();
                    };
                    PanModLoaderFilter.Children.Add(newButton);
                }

            // 自动选择
            MyRadioButton instanceToCheck = null;
            MyRadioButton modLoaderToCheck = null;
            if (!string.IsNullOrEmpty(_targetInstance))
            {
                var targetFile = results.FirstOrDefault(v => v.GameVersions.Contains(_targetInstance));
                if (targetFile is not null)
                {
                    var targetGroup = GetGroupedVersionName(_targetInstance, groupedDrop, groupedOld);
                    var children = _pageType == ModComp.CompType.Mod
                        ? PanInstanceFilter.Children.Cast<UIElement>().Skip(1)
                        : PanInstanceFilter.Children.Cast<UIElement>();
                    foreach (MyRadioButton button in (IEnumerable)children)
                    {
                        if ((button.Text ?? "") != (targetGroup ?? ""))
                            continue;
                        instanceToCheck = button;
                        break;
                    }
                }
            }

            if (_pageType == ModComp.CompType.Mod)
                if (_targetLoader != ModComp.CompLoaderType.Any)
                {
                    var targetFile = results.FirstOrDefault(v => v.ModLoaders.Contains(_targetLoader));
                    if (targetFile is not null)
                    {
                        var children = _pageType == ModComp.CompType.Mod
                            ? PanInstanceFilter.Children.Cast<UIElement>().Skip(1)
                            : PanInstanceFilter.Children.Cast<UIElement>();
                        foreach (MyRadioButton button in (IEnumerable)children)
                        {
                            if ((button.Text ?? "") != (_targetLoader.ToString() ?? ""))
                                continue;
                            modLoaderToCheck = button;
                            break;
                        }
                    }
                }

            // 注意：在 Mod 下 index 0 是 TextBlock
            var index = _pageType == ModComp.CompType.Mod ? 1 : 0;
            if (instanceToCheck is null)
                instanceToCheck = (MyRadioButton)PanInstanceFilter.Children[index];
            if (modLoaderToCheck is null && (_pageType == ModComp.CompType.Mod))
                modLoaderToCheck = (MyRadioButton)PanModLoaderFilter.Children[index];
            instanceToCheck.Checked = true;
            if (_pageType == ModComp.CompType.Mod)
                modLoaderToCheck.Checked = true;
        }

        // 更新筛选结果（文件列表 UI 化）
        UpdateFilterResult();
    }

    private void UpdateFilterResult()
    {
        var results = GetResults();
        if (results is null)
            return;

        // 1. 预处理基础变量
        var targetVersionText = _targetLoader != ModComp.CompLoaderType.Any ? _targetLoader + " " : "";
        var targetCardName = !string.IsNullOrEmpty(_targetInstance) || _targetLoader != ModComp.CompLoaderType.Any
            ? Lang.Text("Download.Comp.Detail.SelectedVersion", targetVersionText, _targetInstance)
            : "";

        // 使用 HashSet 提高查询性能 O(1)
        var supportedLoaders =
            new HashSet<ModComp.CompLoaderType>(Enum.GetValues(typeof(ModComp.CompLoaderType))
                .Cast<ModComp.CompLoaderType>());
        var ignoreQuilt = Config.Download.Comp.IgnoreQuilt;
        var hasMultipleLoaders = _project.ModLoaders.Count > 1;

        // 2. 核心数据归类 (使用 Dictionary 配合 HashSet 去重)
        var dict = new SortedDictionary<string, List<ModComp.CompFile>>(new CardSorter(targetCardName));
        dict.Add(Lang.Text("Download.Comp.Detail.VersionGroup.Other"), new List<ModComp.CompFile>());

        // 用于记录每个卡片内已存在的 version，防止 Contains(version) 的 O(n) 消耗
        var versionDuplicateChecker = new Dictionary<string, HashSet<ModComp.CompFile>>();

        foreach (var version in results)
        {
            // 处理普通卡片归类
            foreach (var gameVersion in version.GameVersions)
            {
                // 筛选器预检查
                var currentGroupedName = GetGroupedVersionName(gameVersion, groupedDrop, groupedOld);
                if (_instanceFilter is not null && (currentGroupedName ?? "") != (_instanceFilter ?? ""))
                    continue;
                var verName = GetGroupedVersionName(gameVersion, false, false);
                var loaders = new List<string>();

                // 判定 Loader 逻辑
                if (hasMultipleLoaders && version.Type == ModComp.CompType.Mod &&
                    McInstanceInfo.IsFormatFit(verName))
                {
                    foreach (var loader in version.ModLoaders)
                    {
                        if (loader == ModComp.CompLoaderType.Quilt && ignoreQuilt)
                            continue;
                        if (!supportedLoaders.Contains(loader))
                            continue;

                        // 模组加载器筛选器
                        if (_modLoaderFilter is not null && (loader.ToString() ?? "") != (_modLoaderFilter ?? ""))
                            continue;

                        loaders.Add(loader + " ");
                    }

                    if (loaders.Count == 0 && _modLoaderFilter is not null) continue;
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
                var isMatchFilter = _instanceFilter is null ||
                                    GetGroupedVersionName(_targetInstance, groupedDrop, groupedOld)
                                        .StartsWithF(_instanceFilter);

                if (isMatchFilter && version.GameVersions.Contains(_targetInstance))
                    if (_targetLoader == ModComp.CompLoaderType.Any || version.ModLoaders.Contains(_targetLoader))
                        // 再次检查 version 是否符合筛选器（针对该文件的所有游戏版本）
                        if (_instanceFilter is null || version.GameVersions.Any(v =>
                                (GetGroupedVersionName(v, groupedDrop, groupedOld) ?? "") == (_instanceFilter ?? "")))
                            AddVersionToDict(dict, versionDuplicateChecker, targetCardName, version);
            }
        }

        // 3. 渲染 UI
        try
        {
            PanResults.Children.Clear();
            var additional = ModMain.frmMain.pageCurrent.additional;
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
                    var list = (List<ModComp.CompFile>)stack.Tag;
                    // 排序和去重检查
                    list.Sort((a, b) => b.ReleaseDate.CompareTo(a.ReleaseDate));
                    var distinctCount = list.Select(f => f.DisplayName).Distinct().Count();
                    var badDisplayName = distinctCount != list.Count;

                    // 批量添加子项
                    switch (_project.Type)
                    {
                        case ModComp.CompType.ModPack:
                        {
                            foreach (var item in list)
                                stack.Children.Add(item.ToListItem(
                                    (sender, e) => ModMain.frmDownloadCompDetail.Install_Click((MyListItem)sender, e),
                                    ModMain.frmDownloadCompDetail.Save_Click, badDisplayName));
                            break;
                        }
                        case ModComp.CompType.World:
                        {
                            foreach (var item in list)
                                stack.Children.Add(item.ToListItem(
                                    (sender, e) =>
                                        ModMain.frmDownloadCompDetail.InstallWorld_Click((MyListItem)sender, e),
                                    ModMain.frmDownloadCompDetail.Save_Click, badDisplayName));
                            break;
                        }

                        default:
                        {
                            ModComp.CompFilesCardPreload(stack, list);
                            foreach (var item in list)
                                stack.Children.Add(item.ToListItem(ModMain.frmDownloadCompDetail.Save_Click,
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
            ModBase.Log(ex, "可视化工程下载列表出错", ModBase.LogLevel.Feedback);
        }
    }

    /// <summary>
    ///     辅助方法：向字典添加数据并处理去重
    /// </summary>
    private void AddVersionToDict(SortedDictionary<string, List<ModComp.CompFile>> dict,
        Dictionary<string, HashSet<ModComp.CompFile>> checker, string key, ModComp.CompFile version)
    {
        if (!dict.ContainsKey(key))
        {
            dict.Add(key, new List<ModComp.CompFile>());
            checker.Add(key, new HashSet<ModComp.CompFile>());
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
            return McInstanceInfo.DropToVersion(McInstanceInfo.VersionToDrop(name, true));

        return name;
    }

    #endregion
}
