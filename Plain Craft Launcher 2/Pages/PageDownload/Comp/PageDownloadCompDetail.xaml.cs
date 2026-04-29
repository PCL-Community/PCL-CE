using System.Collections;
using System.Collections.ObjectModel;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using FluentValidation;
using PCL.Core.App;
using PCL.Core.UI;
using PCL.Core.Utils.Validate;
using PCL.Network;
using PCL.Network.Loaders;
using Control = System.Windows.Forms.Control;

namespace PCL;

public partial class PageDownloadCompDetail
{
    // 资源下载；整合包另存为
    public static Dictionary<ModComp.CompType, string> CachedFolder = new(); // 仅在本次缓存的下载文件夹
    private MyCompItem _compItem;
    private bool _isFirstInit = true;

    private void Init()
    {
        ModAnimation.AniControlEnabled += 1;
        _project = ModMain.FrmMain.PageCurrent.Additional.Value.CompProject;
        PanBack.ScrollToHome();
        // 重启加载器
        if (_isFirstInit)
            // 在 Me.Initialized 已经初始化了加载器，不再重复初始化
            _isFirstInit = false;
        else
            PageLoaderRestart(IsForceRestart: true);
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
        BtnIntroWiki.Visibility = _project.WikiId == 0 ? Visibility.Collapsed : Visibility.Visible;

        ModAnimation.AniControlEnabled -= 1;
    }

    // 整合包安装
    public void Install_Click(MyListItem sender, EventArgs e)
    {
        try
        {
            // 获取基本信息
            var File = (ModComp.CompFile)sender.Tag;
            var LoaderName =
                $"{(_project.FromCurseForge ? "CurseForge" : "Modrinth")} 整合包下载：{_project.TranslatedName} ";

            // 获取实例名
            var PackName = _project.TranslatedName.Replace(".zip", "").Replace(".rar", "").Replace(".mrpack", "")
                .Replace(@"\", "＼").Replace("/", "／").Replace("|", "｜").Replace(":", "：").Replace("<", "＜")
                .Replace(">", "＞").Replace("*", "＊").Replace("?", "？").Replace("\"", "").Replace("： ", "：");
            var Validate = new FolderNameValidator(ModMinecraft.McFolderSelected + "versions");
            if (!Validate.Validate(PackName).IsValid)
                PackName = "";
            var InstanceName = ModMain.MyMsgBoxInput("输入实例名称", "", PackName, [Validate]);
            if (string.IsNullOrEmpty(InstanceName))
                return;

            // 构造步骤加载器
            var Loaders = new List<ModLoader.LoaderBase>();
            var Target =
                $@"{ModMinecraft.McFolderSelected}versions\{InstanceName}\原始整合包.{(_project.FromCurseForge ? "zip" : "mrpack")}";
            var LogoFileAddress = MyImage.GetTempPath(_compItem.Logo);
            Loaders.Add(new LoaderDownload("下载整合包文件", new List<DownloadFile> { File.ToNetFile(Target) })
                { ProgressWeight = 10d, Block = true });
            Loaders.Add(new ModLoader.LoaderTask<int, int>("准备安装整合包",
                _ => ModModpack.ModpackInstall(Target, InstanceName,
                    System.IO.File.Exists(LogoFileAddress) ? LogoFileAddress : null, File.ProjectId,
                    true)) { ProgressWeight = 0.1d });

            // 启动
            var Loader = new ModLoader.LoaderCombo<string>(LoaderName, Loaders)
            {
                OnStateChanged = MyLoader =>
                {
                    switch (MyLoader.State)
                    {
                        case ModBase.LoadState.Failed:
                        {
                            ModMain.Hint(MyLoader.Name + "失败：" + MyLoader.Error.Message, ModMain.HintType.Critical);
                            break;
                        }
                        case ModBase.LoadState.Aborted:
                        {
                            ModMain.Hint(MyLoader.Name + "已取消！");
                            break;
                        }
                        case ModBase.LoadState.Loading:
                        {
                            return; // 不重新加载版本列表
                        }
                    }

                    ModDownloadLib.McInstallFailedClearFolder(MyLoader);
                }
            };
            Loader.Start(ModMinecraft.McFolderSelected + @"versions\" + InstanceName + @"\");
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
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
            var File = (ModComp.CompFile)sender.Tag;
            var LoaderName = $"{(_project.FromCurseForge ? "CurseForge" : "Modrinth")} 世界下载：{_project.TranslatedName} ";

            // 确认默认保存位置
            string DefaultFolder = null;
            var SubFolder = @"saves\";
            Func<ModMinecraft.McInstance, bool> IsVersionSuitable = null;
            // 获取资源所需的加载器
            var AllowedLoaders = new List<ModComp.CompLoaderType>();
            if (File.ModLoaders.Any())
                AllowedLoaders = File.ModLoaders;
            else if (_project.ModLoaders.Any()) AllowedLoaders = _project.ModLoaders;
            ModBase.Log("[Comp] 世界要求的加载器种类：" + (AllowedLoaders.Any() ? AllowedLoaders.Join(" / ") : "无要求"));
            // 判断某个版本是否符合资源要求
            IsVersionSuitable = Version =>
            {
                if (Version is null)
                    return false;
                if (!Version.IsLoaded)
                    Version.Load();
                if (File.GameVersions.Any(v => v.Contains(".")) && !File.GameVersions.Any(v =>
                        v.Contains(".") && (v ?? "") == (Version.Info.VanillaName ?? "")))
                    return false;
                // 加载器
                if (!AllowedLoaders.Any())
                    return true; // 无要求
                return false;
            };
            // 获取常规资源默认下载位置
            if (CachedFolder.ContainsKey(File.Type) && !string.IsNullOrEmpty(CachedFolder[File.Type]))
            {
                DefaultFolder = CachedFolder.GetOrDefault(File.Type,
                    ModMinecraft.McInstanceSelected?.PathIndie ?? ModBase.ExePath);
                ModBase.Log($"[Comp] 使用上次下载时的文件夹作为默认下载位置：{DefaultFolder}");
            }
            else if (ModMinecraft.McInstanceSelected is not null && IsVersionSuitable(ModMinecraft.McInstanceSelected))
            {
                DefaultFolder = $"{ModMinecraft.McInstanceSelected.PathIndie}{SubFolder}";
                Directory.CreateDirectory(DefaultFolder);
                ModBase.Log($"[Comp] 使用当前实例作为默认下载位置：{DefaultFolder}");
            }
            else
            {
                // 查找所有可能的实例
                var NeedLoad = ModMinecraft.McInstanceListLoader.State != ModBase.LoadState.Finished;
                if (NeedLoad)
                {
                    ModMain.Hint("正在查找适合的游戏实例……");
                    ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
                        ModLoader.LoaderFolderRunType.ForceRun, 1, @"versions\", true);
                }

                var SuitableVersions = ModMinecraft.McInstanceList.Values.SelectMany(l => l)
                    .Where(v => IsVersionSuitable(v)).Select(v => new DirectoryInfo($"{v.PathIndie}{SubFolder}"));
                if (SuitableVersions.Any())
                {
                    var SelectedVersion = SuitableVersions
                        .OrderByDescending(Dir => Dir.Exists ? Dir.LastWriteTimeUtc : DateTime.MinValue)
                        .ThenByDescending(Dir => Dir.Exists ? Dir.GetFiles().Length : -1).First(); // 先按文件夹更改时间降序
                    // 再按文件夹中的文件数量降序
                    DefaultFolder = SelectedVersion.FullName;
                    Directory.CreateDirectory(DefaultFolder);
                    ModBase.Log($"[Comp] 使用适合的游戏实例作为默认下载位置：{DefaultFolder}");
                }
                else
                {
                    DefaultFolder = ModMinecraft.McFolderSelected;
                    if (NeedLoad)
                        ModMain.Hint("当前 MC 文件夹中没有找到适合此资源文件的实例！");
                    else
                        ModBase.Log("[Comp] 由于当前实例不兼容，使用当前的 MC 文件夹作为默认下载位置");
                }
            }

            var Target = SystemDialogs.SelectSaveFile("选择世界安装位置 (saves 文件夹)", File.FileName, "世界文件|" + "*.zip",
                DefaultFolder);
            if (string.IsNullOrEmpty(Target))
                return;

            // 构造步骤加载器
            var Loaders = new List<ModLoader.LoaderBase>();
            var TargetPath = Target.BeforeLast(@"\");
            var LogoFileAddress = MyImage.GetTempPath(_compItem.Logo);
            Loaders.Add(new LoaderDownload("下载世界文件", new List<DownloadFile> { File.ToNetFile(Target) })
                { ProgressWeight = 10d, Block = true });
            Loaders.Add(
                new ModLoader.LoaderTask<int, int>("安装世界", _ => ModBase.ExtractFile(Target, TargetPath, Encoding.UTF8))
                    { ProgressWeight = 0.1d, Block = true });
            Loaders.Add(new ModLoader.LoaderTask<int, int>("清理缓存", _ => System.IO.File.Delete(Target)));

            // 启动
            var Loader = new ModLoader.LoaderCombo<int>(LoaderName, Loaders)
                { OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly };
            Loader.Start();
            ModLoader.LoaderTaskbarAdd(Loader);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "下载世界资源失败", ModBase.LogLevel.Feedback);
        }
    }

    public void Save_Click(object sender, EventArgs e)
    {
        // 获取点击项关联的文件对象
        // 使用模式匹配 (Pattern Matching) 获取目标 Control/Item
        object target = sender switch
        {
            MyListItem item => item,
            Control ctrl => ctrl.Parent,
            _ => null
        };

        // 安全地访问 Tag 并转换
        var File = sender switch
        {
            MyListItem item => item.Tag as ModComp.CompFile,
            Control ctrl => (ctrl.Parent as Control)?.Tag as ModComp.CompFile,
            _ => null
        };

        ModBase.RunInNewThread(() =>
        {
            try
            {
                var Desc = "";
                switch (File.Type)
                {
                    case ModComp.CompType.ModPack: Desc = "整合包"; break;
                    case ModComp.CompType.Mod: Desc = "Mod "; break;
                    case ModComp.CompType.ResourcePack: Desc = "资源包"; break;
                    case ModComp.CompType.Shader: Desc = "光影包"; break;
                    case ModComp.CompType.DataPack: Desc = "数据包"; break;
                    case ModComp.CompType.World: Desc = "世界"; break;
                }

                // 确认默认保存位置
                string DefaultFolder = null;
                if (File.Type != ModComp.CompType.ModPack)
                {
                    var SubFolder = "";
                    switch (File.Type)
                    {
                        case ModComp.CompType.Mod: SubFolder = "mods\\"; break;
                        case ModComp.CompType.ResourcePack: SubFolder = "resourcepacks\\"; break;
                        case ModComp.CompType.Shader: SubFolder = "shaderpacks\\"; break;
                        case ModComp.CompType.World: SubFolder = "saves\\"; break;
                        case ModComp.CompType.DataPack: SubFolder = ""; break; // 导航到版本根目录
                    }

                    // 获取资源所需的加载器
                    var AllowedLoaders = new List<ModComp.CompLoaderType>();
                    if (File.ModLoaders.Any())
                        AllowedLoaders = File.ModLoaders;
                    else if (_project.ModLoaders.Any()) AllowedLoaders = _project.ModLoaders;
                    ModBase.Log(
                        $"[Comp] {Desc}要求的加载器种类：{(AllowedLoaders.Any() ? string.Join(" / ", AllowedLoaders) : "无要求")}");

                    // 判断某个版本是否符合资源要求 (局部函数)
                    Func<ModMinecraft.McInstance, bool> IsVersionSuitable = Version =>
                    {
                        if (Version == null) return false;
                        if (!Version.IsLoaded) Version.Load();

                        // 只对 Mod 和数据包进行版本检测
                        if (File.Type == ModComp.CompType.Mod || File.Type == ModComp.CompType.DataPack)
                            if (File.GameVersions.Any(v => v.Contains(".")) &&
                                !File.GameVersions.Any(v => v.Contains(".") && v == Version.Info.VanillaName))
                                return false;

                        // 加载器判定
                        if (!AllowedLoaders.Any()) return true; // 无要求
                        if (AllowedLoaders.Contains(ModComp.CompLoaderType.Forge) && Version.Info.HasForge) return true;
                        if (AllowedLoaders.Contains(ModComp.CompLoaderType.Fabric) &&
                            (Version.Info.HasFabric || Version.Info.HasLegacyFabric)) return true;
                        if (AllowedLoaders.Contains(ModComp.CompLoaderType.NeoForge) && Version.Info.HasNeoForge)
                            return true;
                        if (AllowedLoaders.Contains(ModComp.CompLoaderType.LiteLoader) && Version.Info.HasLiteLoader)
                            return true;
                        return false;
                    };

                    // 获取常规资源默认下载位置逻辑
                    if (CachedFolder.ContainsKey(File.Type) && !string.IsNullOrEmpty(CachedFolder[File.Type]))
                    {
                        DefaultFolder = CachedFolder.GetOrDefault(File.Type,
                            ModMinecraft.McInstanceSelected?.PathIndie ?? ModBase.ExePath);
                        ModBase.Log($"[Comp] 使用上次下载时的文件夹作为默认下载位置：{DefaultFolder}");
                    }
                    else if (ModMinecraft.McInstanceSelected != null &&
                             IsVersionSuitable(ModMinecraft.McInstanceSelected))
                    {
                        DefaultFolder = $"{ModMinecraft.McInstanceSelected.PathIndie}{SubFolder}";
                        Directory.CreateDirectory(DefaultFolder);
                        ModBase.Log($"[Comp] 使用当前实例作为默认下载位置：{DefaultFolder}");
                    }
                    else
                    {
                        // 查找所有可能的实例
                        var NeedLoad = ModMinecraft.McInstanceListLoader.State != ModBase.LoadState.Finished;
                        if (NeedLoad)
                        {
                            ModMain.Hint("正在查找适合的游戏实例……");
                            ModLoader.LoaderFolderRun(ModMinecraft.McInstanceListLoader, ModMinecraft.McFolderSelected,
                                ModLoader.LoaderFolderRunType.ForceRun, 1, "versions\\", true);
                        }

                        var SuitableVersions = ModMinecraft.McInstanceList.Values.SelectMany(l => l)
                            .Where(v => IsVersionSuitable(v))
                            .Select(v => new DirectoryInfo($"{v.PathIndie}{SubFolder}"));

                        if (SuitableVersions.Any())
                        {
                            var SelectedVersion = SuitableVersions
                                .OrderByDescending(Dir => Dir.Exists ? Dir.LastWriteTimeUtc : DateTime.MinValue)
                                .ThenByDescending(Dir => Dir.Exists ? Dir.GetFiles().Length : -1)
                                .First();
                            DefaultFolder = SelectedVersion.FullName;
                            Directory.CreateDirectory(DefaultFolder);
                            ModBase.Log($"[Comp] 使用适合的游戏实例作为默认下载位置：{DefaultFolder}");
                        }
                        else
                        {
                            DefaultFolder = ModMinecraft.McFolderSelected;
                            if (NeedLoad)
                                ModMain.Hint("当前 MC 文件夹中没有找到适合此资源文件的实例！");
                            else
                                ModBase.Log("[Comp] 由于当前实例不兼容，使用当前的 MC 文件夹作为默认下载位置");
                        }
                    }
                }

                // 获取文件名并弹窗
                var FileName = ModComp.CompFileNameGet(_project, File);
                ModBase.RunInUi(() =>
                {
                    var Target = SystemDialogs.SelectSaveFile("选择保存位置", FileName,
                        $"{Desc}文件|" + (File.Type == ModComp.CompType.Mod
                            ?
                            File.FileName.EndsWith(".litemod") ? "*.litemod" : "*.jar"
                            :
                            File.FileName.EndsWith(".mrpack")
                                ? "*.mrpack"
                                : "*.zip"),
                        DefaultFolder);

                    if (!Target.Contains("\\")) return;

                    // 记录缓存路径
                    var targetDir = ModBase.GetPathFromFullPath(Target);
                    if (Target != DefaultFolder)
                    {
                        if (CachedFolder.ContainsKey(File.Type))
                            CachedFolder[File.Type] = targetDir;
                        else
                            CachedFolder.Add(File.Type, targetDir);
                    }

                    // 构造下载任务
                    var LoaderName = $"{Desc}下载：{ModBase.GetFileNameWithoutExtentionFromPath(Target)} ";
                    var Loaders = new List<ModLoader.LoaderBase>
                    {
                        new LoaderDownload("下载文件", new List<DownloadFile> { File.ToNetFile(Target) })
                        {
                            ProgressWeight = 6,
                            Block = true
                        }
                    };

                    // 启动加载器
                    var Loader = new ModLoader.LoaderCombo<int>(LoaderName, Loaders);
                    Loader.OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly;
                    Loader.Start(1);
                    ModLoader.LoaderTaskbarAdd(Loader);

                    ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                    ModMain.FrmMain.BtnExtraDownload.Ribble();
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
        ModComp.CompFavorites.ShowMenu(_project, (UIElement)sender);
    }

    private void BtnIntroLinkCopy_Click(object sender, EventArgs e)
    {
        ModComp.CompClipboard.CurrentText = _project.Website;
        ModBase.ClipboardSet(_project.Website);
    }

    // 翻译简介
    private async void BtnTranslate_Click(object sender, EventArgs e)
    {
        ModMain.Hint($"正在获取 {_project.TranslatedName} 的简介译文……");
        var ChineseDescription = await _project.ChineseDescription;
        if (ChineseDescription is null)
            return;
        ModMain.MyMsgBox($"原文：{_project.Description}{"\r\n"}译文：{ChineseDescription}");
    }

    /// <summary>
    ///     刷新收藏按钮的显示状态
    /// </summary>
    public void RefreshFavoriteButton()
    {
        try
        {
            if (_project is not null)
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
            task.Output = result;
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
        var additional = ModMain.FrmMain.PageCurrent.Additional.Value;
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
                if (errorMessage.Contains("不是有效的 Json 文件"))
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
        public readonly string Topmost = "";

        public CardSorter(string topmost = "")
        {
            Topmost = topmost ?? "";
        }

        public int Compare(string x, string y)
        {
            // 相同
            if ((x ?? "") == (y ?? ""))
                return 0;
            // 置顶
            if ((x ?? "") == (Topmost ?? ""))
                return -1;
            if ((y ?? "") == (Topmost ?? ""))
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
            var versionCodeSort = -ModMinecraft.CompareVersion(x.Replace(x.BeforeFirst(" ") + " ", ""),
                y.Replace(y.BeforeFirst(" ") + " ", ""));
            if (versionCodeSort != 0)
                return versionCodeSort;
            // 比较全部
            return -ModMinecraft.CompareVersion(x, y);
        }
    }

    private string? _instanceFilter;
    private string? _modLoaderFilter;
    private bool GroupedDrop; // 是否按 Drop 筛选（1.21 / 1.20 / 1.19 / ...）而非小版本号（1.21.1 / 1.21 / 1.20.4 / ...）

    private bool GroupedOld; // 是否折叠远古版本为一个选项

    // 筛选类型相同的结果（Modrinth 会返回 Mod、服务端插件、数据包混合的列表）
    private List<ModComp.CompFile> GetResults()
    {
        var results = _compFileLoader.Output;
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
                .Select(v => GetGroupedVersionName(v, GroupedDrop, GroupedOld)).Distinct()
                .OrderByDescending(s => s, new ModMinecraft.VersionComparer()).ToList();
            modLoaderFilters = results.SelectMany(v => v.ModLoaders).Select(l => l.ToString()).Distinct()
                .OrderByDescending(s => s).ToList();
        }

        ;

        // 确定分组方式
        GroupedDrop = false;
        GroupedOld = false;
        updateFilters();
        if (instanceFilters.Count < 9)
            goto GroupDone;
        GroupedDrop = true;
        GroupedOld = false;
        updateFilters();
        if (instanceFilters.Count < 9)
            goto GroupDone;
        GroupedDrop = false;
        GroupedOld = true;
        updateFilters();
        if (instanceFilters.Count < 9)
            goto GroupDone;
        GroupedDrop = true;
        GroupedOld = true;
        updateFilters();
        GroupDone: ;


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
                    Text = "实例筛选：",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2d, 0d, 0d, 0d)
                };
                PanInstanceFilter.Children.Add(instanceTextBlock);
                var modLoaderTextBlock = new TextBlock
                {
                    Text = "模组加载器筛选：",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2d, 0d, 0d, 0d)
                };
                PanModLoaderFilter.Children.Add(modLoaderTextBlock);
            }

            instanceFilters.Insert(0, "全部");
            modLoaderFilters.Insert(0, "全部");
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
                    _instanceFilter = sender.Text == "全部" ? null : sender.Text;
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
                        _modLoaderFilter = sender.Text == "全部" ? null : sender.Text;
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
                    var targetGroup = GetGroupedVersionName(_targetInstance, GroupedDrop, GroupedOld);
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
            if (modLoaderToCheck is null & (_pageType == ModComp.CompType.Mod))
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
            ? $"所选版本：{targetVersionText}{_targetInstance}"
            : "";

        // 使用 HashSet 提高查询性能 O(1)
        var supportedLoaders =
            new HashSet<ModComp.CompLoaderType>(Enum.GetValues(typeof(ModComp.CompLoaderType))
                .Cast<ModComp.CompLoaderType>());
        var ignoreQuilt = Config.Download.Comp.IgnoreQuilt;
        var hasMultipleLoaders = _project.ModLoaders.Count > 1;

        // 2. 核心数据归类 (使用 Dictionary 配合 HashSet 去重)
        var dict = new SortedDictionary<string, List<ModComp.CompFile>>(new CardSorter(targetCardName));
        dict.Add("其他", new List<ModComp.CompFile>());

        // 用于记录每个卡片内已存在的 version，防止 Contains(version) 的 O(n) 消耗
        var versionDuplicateChecker = new Dictionary<string, HashSet<ModComp.CompFile>>();

        foreach (var version in results)
        {
            // 处理普通卡片归类
            foreach (var gameVersion in version.GameVersions)
            {
                // 筛选器预检查
                var currentGroupedName = GetGroupedVersionName(gameVersion, GroupedDrop, GroupedOld);
                if (_instanceFilter is not null && (currentGroupedName ?? "") != (_instanceFilter ?? ""))
                    continue;
                var verName = GetGroupedVersionName(gameVersion, false, false);
                var loaders = new List<string>();

                // 判定 Loader 逻辑
                if (hasMultipleLoaders && version.Type == ModComp.CompType.Mod &&
                    ModMinecraft.McInstanceInfo.IsFormatFit(verName))
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
                                    GetGroupedVersionName(_targetInstance, GroupedDrop, GroupedOld)
                                        .StartsWithF(_instanceFilter);

                if (isMatchFilter && version.GameVersions.Contains(_targetInstance))
                    if (_targetLoader == ModComp.CompLoaderType.Any || version.ModLoaders.Contains(_targetLoader))
                        // 再次检查 version 是否符合筛选器（针对该文件的所有游戏版本）
                        if (_instanceFilter is null || version.GameVersions.Any(v =>
                                (GetGroupedVersionName(v, GroupedDrop, GroupedOld) ?? "") == (_instanceFilter ?? "")))
                            AddVersionToDict(dict, versionDuplicateChecker, targetCardName, version);
            }
        }

        // 3. 渲染 UI
        try
        {
            PanResults.Children.Clear();
            var additional = ModMain.FrmMain.PageCurrent.Additional;
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
                                    (sender, e) => ModMain.FrmDownloadCompDetail.Install_Click((MyListItem)sender, e),
                                    ModMain.FrmDownloadCompDetail.Save_Click, badDisplayName));
                            break;
                        }
                        case ModComp.CompType.World:
                        {
                            foreach (var item in list)
                                stack.Children.Add(item.ToListItem(
                                    (sender, e) =>
                                        ModMain.FrmDownloadCompDetail.InstallWorld_Click((MyListItem)sender, e),
                                    ModMain.FrmDownloadCompDetail.Save_Click, badDisplayName));
                            break;
                        }

                        default:
                        {
                            ModComp.CompFilesCardPreload(stack, list);
                            foreach (var item in list)
                                stack.Children.Add(item.ToListItem(ModMain.FrmDownloadCompDetail.Save_Click,
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
                if (currentKey == "其他")
                    newStack.Children.Add(new MyHint
                    {
                        Text = "由于版本信息更新缓慢，可能无法识别刚更新的 MC 版本。几天后即可正常识别。",
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
        if (name is null) return "其他";

        if (name.Contains("w")) return "快照版";

        if (!ModMinecraft.McInstanceInfo.IsFormatFit(name) ||
            (foldOld && ModMinecraft.McInstanceInfo.VersionToDrop(name, true) < 120)) return "远古版";

        if (groupedByDrop)
            return ModMinecraft.McInstanceInfo.DropToVersion(ModMinecraft.McInstanceInfo.VersionToDrop(name, true));

        return name;
    }

    #endregion
}
