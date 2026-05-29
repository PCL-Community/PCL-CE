using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using DotNet.Globbing;
using PCL.Core.App;
using PCL.Core.UI;
using PCL.Core.App.Localization;

namespace PCL;

public class ExportOption
{
    public string Title { get; set; }
    public string Description { get; set; }
    public string Rules { get; set; }

    /// <summary>
    ///     如果 Rules 为空，则根据 ShowRules 的内容判断是否应该显示这个复选框。
    ///     如果 ShowRules 也为空，则始终显示。
    /// </summary>
    public string ShowRules { get; set; }

    public bool DefaultChecked { get; set; }
    public bool RequireModLoader { get; set; } = false;
    public bool RequireOptiFine { get; set; } = false;
    public bool RequireModLoaderOrOptiFine { get; set; } = false;
}

public partial class PageInstanceExport : IRefreshable
{
    private string CurrentVersion = "";

    public PageInstanceExport()
    {
        InitializeComponent();
        Loaded += (_, _) => PageInstanceExport_Loaded();
        CardOptions.MouseLeftButtonDown += CardOptions_MouseLeftButtonDown;
        BtnAdvancedExport.Click += ExportConfig;
        BtnAdvancedImport.Click += ImportConfig;
        BtnExport.Click += StartExport;
        TextExportName.GotFocus += TextExportName_GotFocus;
        CheckAdvancedModrinth.Change += CheckAdvancedModrinth_Change;
        CheckAdvancedInclude.Change += CheckAdvancedInclude_Change;
    }

    void IRefreshable.Refresh()
    {
        RefreshAll();
    }

    private void PageInstanceExport_Loaded()
    {
        ModAnimation.AniControlEnabled += 1;
        if ((CurrentVersion ?? "") != (PageInstanceLeft.Instance.PathInstance ?? ""))
            RefreshAll(); // 切换到了另一个实例，重置页面
        CustomEventService.SetEventData(BtnAdvancedHelp, "指南/整合包制作.json");
        ModAnimation.AniControlEnabled -= 1;
    }

    public void RefreshAll()
    {
        ModBase.Log("[Export] 刷新导出页面");
        HintOptiFine.Visibility =
            PageInstanceLeft.Instance.Info.HasOptiFine ? Visibility.Visible : Visibility.Collapsed;
        CurrentVersion = PageInstanceLeft.Instance.PathInstance;
        TextExportName.Text = "";
        TextExportName.HintText = PageInstanceLeft.Instance.Name;
        TextExportVersion.Text = "";
        TextExportVersion.HintText = "1.0.0";
        CheckAdvancedInclude.Checked = false;
        CheckAdvancedModrinth.Checked = false;
        GetExportOption(CheckOptionsBasic).Description = PageInstanceLeft.Instance.GetDefaultDescription();
        ResetConfigOverrides();
        ReloadAllSubOptions();
        RefreshAllOptionsUI();
        PanBack.ScrollToHome();
    }

    // 自动填写整合包名称
    private void TextExportName_GotFocus(object sender, RoutedEventArgs routedEventArgs)
    {
        if (string.IsNullOrEmpty(TextExportName.Text))
        {
            TextExportName.Text = TextExportName.HintText;
            TextExportName.SelectionStart = TextExportName.Text.Length;
        }
    }

    // 勾选 Modrinth 上传模式时，禁止打包 PCL
    private void CheckAdvancedModrinth_Change(object sender, bool user)
    {
        if (CheckAdvancedModrinth.Checked == true)
            CheckOptionsPcl.Checked = false;
        CheckOptionsPcl.IsEnabled = (bool)!CheckAdvancedModrinth.Checked;
    }

    // 勾选打包资源文件时，禁止开启 Modrinth 上传模式
    private void CheckAdvancedInclude_Change(object sender, bool user)
    {
        if (CheckAdvancedInclude.Checked == true)
            CheckAdvancedModrinth.Checked = false;
        CheckAdvancedModrinth.IsEnabled = (bool)!CheckAdvancedInclude.Checked;
    }

    #region 子选项

    private readonly string[] SubOptionBlackList = new[] { "Quark Programmer Art.zip", "+ EuphoriaPatches_" };

    /// <summary>
    ///     动态生成子文件夹下的选项，例如资源包、存档等。
    /// </summary>
    private void ReloadAllSubOptions()
    {
        ReloadSubOptions(PanOptionsResourcePacks, true, true, "resourcepacks", "texturepacks");
        ReloadSubOptions(PanOptionsSaves, false, true, "saves");
        ReloadSubOptions(PanOptionsShaderPacks, true, true, "shaderpacks");
    }

    private void ReloadSubOptions(StackPanel Panel, bool AcceptCompressedFile, bool AcceptFolder,
        params string[] Folders)
    {
        Panel.Children.Clear();
        foreach (var Folder in Folders)
        {
            var TargetFolder = new DirectoryInfo(PageInstanceLeft.Instance.PathIndie + Folder);
            if (!TargetFolder.Exists)
                continue;
            // 查找文件夹下的对应项
            if (AcceptCompressedFile)
                foreach (var File in TargetFolder.EnumerateFiles("*.zip").Concat(TargetFolder.EnumerateFiles("*.rar")))
                {
                    if (SubOptionBlackList.Any(b => File.Name.ContainsF(b)))
                        continue;
                    Panel.Children.Add(new MyCheckBox
                    {
                        Tag = new ExportOption
                        {
                            Title = File.Name, DefaultChecked = true,
                            Rules = ModBase.EscapeLikePattern($"{Folder}/{File.Name}")
                        }
                    });
                    if (Folder == "shaderpacks") // 处理光影包的配置文件
                    {
                        var shaderConfig = new FileInfo(Path.Combine(File.Directory.FullName,
                            $"{File.Name}.txt"));
                        if (shaderConfig.Exists)
                            Panel.Children.Add(new MyCheckBox
                            {
                                Margin = new Thickness(30, 0, 0, 0),
                                Tag = new ExportOption
                                {
                                    Title = $"{shaderConfig.Name}", DefaultChecked = true,
                                    Description = "光影配置文件",
                                    Rules = ModBase.EscapeLikePattern($"{Folder}/{shaderConfig.Name}")
                                }
                            });
                    }
                }

            if (AcceptFolder)
                foreach (var SubFolder in TargetFolder.EnumerateDirectories().OrderByDescending(f => f.LastWriteTime))
                {
                    if (SubOptionBlackList.Any(b => SubFolder.Name.ContainsF(b)))
                        continue;
                    if (!SubFolder.EnumerateFileSystemInfos().Any())
                        continue;
                    var NewCheckBox = new MyCheckBox
                    {
                        Tag = new ExportOption
                        {
                            Title = SubFolder.Name, DefaultChecked = true,
                            Rules = ModBase.EscapeLikePattern($"{Folder}/{SubFolder.Name}/")
                        }
                    };
                    if (ReferenceEquals(Panel, PanOptionsSaves))
                        GetExportOption(NewCheckBox).Description =
                            Lang.Date(SubFolder.LastWriteTime, "g");
                    Panel.Children.Add(NewCheckBox);
                    if (Folder == "shaderpacks") // 处理文件夹形式光影包的配置文件
                    {
                        var shaderConfig = new FileInfo(Path.Combine(TargetFolder.FullName,
                            $"{SubFolder.Name}.txt"));
                        if (shaderConfig.Exists)
                            Panel.Children.Add(new MyCheckBox
                            {
                                Margin = new Thickness(30, 0, 0, 0),
                                Tag = new ExportOption
                                {
                                    Title = $"{shaderConfig.Name}", DefaultChecked = true,
                                    Description = "光影配置文件",
                                    Rules = ModBase.EscapeLikePattern($"{Folder}/{shaderConfig.Name}")
                                }
                            });
                    }
                }
        }
    }

    #endregion

    #region 选项

    /// <summary>
    ///     重新确认是否应该显示每个选项，并将 ExportOption 同步到 UI。
    /// </summary>
    private void RefreshAllOptionsUI()
    {
        // 预先归纳所有至多二级的文件/文件夹
        var AllEntries = new List<string>();

        bool IsValidDirectory(DirectoryInfo Folder)
        {
            try
            {
                return Folder.Exists && Folder.EnumerateFileSystemInfos()
                    .Any(i => !SubOptionBlackList.Any(b => i.Name.ContainsF(b)));
            }
            catch
            {
                return false;
            }
        }

        ; // 检查文件夹不为空
        // 一般是由于无法访问，或是一个指向已不存在的文件夹的链接（例如使用 mklink 创造的 resource 文件夹链接）
        var PathInfo = new DirectoryInfo(PageInstanceLeft.Instance.PathIndie);
        AllEntries.AddRange(PathInfo.EnumerateFiles().Select(f => f.Name));
        foreach (var SubFolder in PathInfo.EnumerateDirectories().Where(IsValidDirectory))
        {
            AllEntries.Add($@"{SubFolder.Name}\");
            AllEntries.AddRange(SubFolder.EnumerateFiles().Select(f => $@"{SubFolder.Name}\{f.Name}"));
            AllEntries.AddRange(SubFolder.EnumerateDirectories().Where(IsValidDirectory)
                .Select(d => $@"{SubFolder.Name}\{d.Name}\"));
        }

        ModBase.Log($"[Export] 共发现 {AllEntries.Count} 个可行的二级文件/文件夹");

        // 确认选项是否应该被显示
        bool IsVisible(ExportOption TargetOption)
        {
            // 检查需要 OptiFine 或 Mod 加载器
            if (TargetOption.RequireOptiFine && !PageInstanceLeft.Instance.Info.HasOptiFine)
                return false;
            if (TargetOption.RequireModLoader && !PageInstanceLeft.Instance.Modable)
                return false;
            if (TargetOption.RequireModLoaderOrOptiFine && !PageInstanceLeft.Instance.Info.HasOptiFine &&
                !PageInstanceLeft.Instance.Modable)
                return false;
            // 粗略检查是否可能有符合规则的文件/文件夹
            return StandardizeLines((TargetOption.Rules ?? TargetOption.ShowRules).Split('|'), true).Any(Rule =>
            {
                if (Rule.StartsWithF("!"))
                    return false; // 只看正向规则
                // 检查前两级
                try
                {
                    if (AllEntries.Any(Entry => LikeString(Entry, Rule)))
                        return true;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"错误的规则：{Rule}", ModBase.LogLevel.Hint);
                    return false;
                }

                // 粗略检查所有级
                Rule = Rule.Trim("*?".ToCharArray());
                if (Rule.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).Count() >= 3)
                {
                    if (Rule.EndsWithF(@"\"))
                        return IsValidDirectory(new DirectoryInfo(PageInstanceLeft.Instance.PathIndie + Rule)); // 文件夹有效

                    return File.Exists(PageInstanceLeft.Instance.PathIndie + Rule);
                    // 文件有效
                }

                return false;
            });
        }

        ;
        // 逐个检查选项
        foreach (var CheckBox in GetAllOptions(true))
        {
            var TargetOption = GetExportOption(CheckBox);
            // 名称与简介
            CheckBox.Inlines.Clear();
            CheckBox.Inlines.Add(new Run(TargetOption.Title));
            if (!string.IsNullOrEmpty(TargetOption.Description))
                CheckBox.Inlines.Add(new Run("   " + TargetOption.Description) { Foreground = ThemeManager.ColorGray5 });
            // 可见性、默认勾选
            if (string.IsNullOrEmpty(TargetOption.Rules) && string.IsNullOrEmpty(TargetOption.ShowRules))
            {
                CheckBox.Visibility = Visibility.Visible;
                CheckBox.Checked = TargetOption.DefaultChecked;
            }
            else
            {
                var Pass = IsVisible(TargetOption);
                CheckBox.Visibility = Pass ? Visibility.Visible : Visibility.Collapsed;
                CheckBox.Checked = TargetOption.DefaultChecked && Pass;
            }
        }
    }

    /// <summary>
    ///     对文本行进行标准化处理，以便使用 Like 进行匹配。
    /// </summary>
    private IEnumerable<string> StandardizeLines(IEnumerable<string> Raw, bool AddSuffixStarToFolderPath)
    {
        foreach (var IgnoreLineRaw in Raw)
        {
            var IgnoreLine = IgnoreLineRaw;
            IgnoreLine = IgnoreLine.Trim();
            if (string.IsNullOrEmpty(IgnoreLine) || IgnoreLine.StartsWithF("#") || IgnoreLine.StartsWithF("="))
                continue;
            IgnoreLine = IgnoreLine.Replace("/", @"\");
            yield return IgnoreLine + (IgnoreLine.EndsWithF(@"\") && AddSuffixStarToFolderPath ? "*" : "");
        }
    }

    /// <summary>
    ///     获取所有可作为选项的 CheckBox。
    /// </summary>
    private IEnumerable<MyCheckBox> GetAllOptions(bool IncludeHidden)
    {
        foreach (var Element in PanOptions.Children)
        {
            if (!IncludeHidden &&
                ((UIElement)Element).Visibility != Visibility.Visible)
                continue;
            if (Element is MyCheckBox)
                yield return (MyCheckBox)Element;
            else if (Element is StackPanel)
                foreach (var SubElement in ((StackPanel)Element).Children)
                {
                    if (!IncludeHidden && ((UIElement)SubElement).Visibility != Visibility.Visible)
                        continue;
                    if (SubElement is MyCheckBox)
                        yield return (MyCheckBox)SubElement;
                }
        }
    }

    /// <summary>
    ///     获取该 CheckBox 对应的 ExportOption。
    /// </summary>
    private ExportOption GetExportOption(MyCheckBox CheckBox)
    {
        return (ExportOption)CheckBox.Tag;
    }

    #endregion

    #region 配置文件

    private const string Sperator = "==============================================================";

    // ================ 导出内容段 ================

    /// <summary>
    ///     从配置文件中读取的规则。
    ///     如果不为 Nothing，则会覆写当前勾选的规则并禁用对应 UI。
    /// </summary>
    private List<string> RulesOverrides
    {
        get => _RulesOverrides;
        set
        {
            _RulesOverrides = value;
            if (value is null)
            {
                BtnOverrideCancel.Visibility = Visibility.Collapsed;
                PanOptions.Visibility = Visibility.Visible;
                CardOptions.Inlines.Clear();
                CardOptions.Inlines.Add(new Run("导出内容列表") { FontWeight = FontWeights.Bold });
            }
            else
            {
                BtnOverrideCancel.Visibility = Visibility.Visible;
                PanOptions.Visibility = Visibility.Collapsed;
                CardOptions.Inlines.Clear();
                CardOptions.Inlines.Add(new Run("导出内容列表:    ") { FontWeight = FontWeights.Bold });
                CardOptions.Inlines.Add(new Run("从配置文件中读取") { FontWeight = FontWeights.Normal });
            }
        }
    }

    private List<string> _RulesOverrides;

    /// <summary>
    ///     获取当前实际生效的所有规则。
    /// </summary>
    private IEnumerable<string> GetAllRules()
    {
        if (RulesOverrides is not null)
        {
            // 返回覆盖的列表
            foreach (var Rule in RulesOverrides)
                yield return Rule;
        }
        else
        {
            // 从当前勾选的所有选项中获取所有规则行
            yield return "";
            yield return "# 修改下方的规则以控制需要导出的内容。";
            yield return "# 以 ! 开头以反选。可以使用 *、?、[] 通配符。靠后的行覆盖靠前的。";
            yield return "";
            foreach (var CheckBox in GetAllOptions(false))
            {
                if (CheckBox.Checked == false)
                    continue;
                var TargetOption = GetExportOption(CheckBox);
                if (TargetOption.Rules is null)
                    continue;
                yield return $"# {TargetOption.Title}";
                foreach (var Rule in TargetOption.Rules.Split('|'))
                    yield return Rule;
                yield return "";
            }

            yield return "# 排除的文件";
            yield return "!*.log";
            yield return "!*.dat_old";
            yield return "!*.BakaCoreInfo";
            yield return "!hmclversion.cfg";
            yield return "!log4j2.xml";
            yield return "";
        }
    }

    // ================ 追加内容段 ================

    private List<string> ExtraFiles;

    /// <summary>
    ///     获取当前实际生效的追加内容。
    /// </summary>
    private IEnumerable<string> GetExtraFileLines()
    {
        if (ExtraFiles is not null)
        {
            // 返回覆盖的列表
            foreach (var File in ExtraFiles)
                yield return File;
        }
        else
        {
            // 从当前勾选的所有选项中获取所有规则行
            yield return "";
            yield return "# 如果想将额外的文件自动放到压缩包根目录中，可以将它们的路径写在下方。";
            yield return @"# 必须是完整路径。每行中，若以 \ 结尾则代表是文件夹，不以 \ 结尾则代表是文件。";
            yield return "";
        }
    }

    // ================ 重置 ================

    /// <summary>
    ///     重置配置文件所带来的影响。
    /// </summary>
    private void ResetConfigOverrides()
    {
        RulesOverrides = null;
        ConfigPackPath = null;
        ExtraFiles = null;
        PanBack.ScrollToHome();
    }

    private void CardOptions_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (RulesOverrides is null)
            return;
        ResetConfigOverrides();
    }

    // ================ 保存 / 读取 ================

    // 保存配置文件
    private void ExportConfig(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var ConfigPath = SystemDialogs.SelectSaveFile("选择文件位置", "export_config.txt", "整合包导出配置(*.txt)|*.txt",
                (string?)States.System.ExportConfigPath);
            if (string.IsNullOrEmpty(ConfigPath))
                return;
            States.System.ExportConfigPath = ConfigPath;
            var ConfigLines = new List<string>();
            // ini 段
            ConfigLines.Add("Name:" + TextExportName.Text);
            ConfigLines.Add("Version:" + TextExportVersion.Text);
            ConfigLines.Add("");
            ConfigLines.Add("# 是否打包正式版 PCL，以便没有启动器的玩家安装整合包。");
            ConfigLines.Add("IncludeLauncher:" + CheckOptionsPcl.Checked);
            ConfigLines.Add("");
            ConfigLines.Add("# 是否打包 PCL 个性化内容，例如功能隐藏设置、主页、背景音乐和图片等。");
            ConfigLines.Add("IncludeLauncherCustom:" + CheckOptionsPclCustom.Checked);
            ConfigLines.Add("");
            ConfigLines.Add("# 是否将 Mod、资源包、光影包的文件直接放入整合包中，这样在导入时就无需联网下载它们。");
            ConfigLines.Add("# 建议仅在无法稳定连接 CurseForge 或 Modrinth 时才考虑启用。");
            ConfigLines.Add("# 二次分发可能违反使用协议，请尽量不要公开发布包含资源文件的整合包！");
            ConfigLines.Add("DontCheckHostedAssets:" + CheckAdvancedInclude.Checked);
            ConfigLines.Add("");
            ConfigLines.Add("# 如果你想要打包上传到 Modrinth，启用此项会生成完全符合 Modrinth 要求的整合包文件。");
            ConfigLines.Add("# 由于 Modrinth 要求，只能从 CurseForge 下载的资源将无法联网下载，会被直接放入整合包中。");
            ConfigLines.Add("# 此选项与 IncludeLauncher、IncludeLauncherCustom、DontCheckHostedAssets 冲突。");
            ConfigLines.Add("ModrinthUploadMode:" + CheckAdvancedModrinth.Checked);
            ConfigLines.Add("");
            ConfigLines.Add("# 导出的文件的存放位置。");
            ConfigLines.Add("# 若设置了此项，在导出时会直接将文件放到此路径，不会弹窗要求选择。");
            ConfigLines.Add("# 若 IncludeLauncher 为 True，应以 .zip 结尾；若为 False，应以 .mrpack 结尾。");
            ConfigLines.Add("PackPath:" + (ConfigPackPath ?? ""));
            ConfigLines.Add("");
            // 导出内容段
            ConfigLines.Add(Sperator);
            ConfigLines.AddRange(GetAllRules());
            // 追加内容段
            ConfigLines.Add(Sperator);
            ConfigLines.AddRange(GetExtraFileLines());
            // 结束
            ModBase.WriteFile(ConfigPath, ConfigLines.Join("\r\n"));
            ModMain.Hint("已保存配置文件：" + ConfigPath, ModMain.HintType.Finish);
            ModBase.OpenExplorer(ConfigPath);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "保存配置失败", ModBase.LogLevel.Msgbox);
        }
    }

    #region 配置文件核心读取逻辑

    /// <summary>
    ///     从指定路径读取配置文件（供按钮和拖放调用）
    /// </summary>
    /// <param name="configPath">配置文件路径</param>
    private void ReadConfigFile(string configPath)
    {
        try
        {
            // 保存配置文件路径到缓存
            States.System.ExportConfigPath = configPath;

            var fileContent = ModBase.ReadFile(configPath);
            var Segments = fileContent.Split(Sperator);

            if (Segments.Length == 0)
            {
                ModMain.Hint("配置文件内容无效或为空！", ModMain.HintType.Critical);
                return;
            }

            // === 解析INI段 ===
            var Ini = new Dictionary<string, string>();
            foreach (var LineRaw in Segments[0].Split("\r\n".ToCharArray()))
            {
                var Line = LineRaw;
                Line = Line.Trim();
                if (string.IsNullOrEmpty(Line) || Line.StartsWithF("#") || Line.StartsWithF("="))
                    continue;
                var Index = Line.IndexOfF(":");
                if (Index > 0) Ini[Line.Substring(0, Index)] = Line.Substring(Index + 1);
            }

            // 赋值到界面控件
            TextExportName.Text = Ini.GetOrDefault("Name", "");
            TextExportVersion.Text = Ini.GetOrDefault("Version", "");
            CheckOptionsPcl.Checked =
                Convert.ToBoolean(Ini.GetOrDefault("IncludeLauncher", true.ToString()));
            CheckOptionsPclCustom.Checked =
                Convert.ToBoolean(Ini.GetOrDefault("IncludeLauncherCustom", true.ToString()));
            CheckAdvancedModrinth.Checked =
                Convert.ToBoolean(Ini.GetOrDefault("ModrinthUploadMode", false.ToString()));
            CheckAdvancedInclude.Checked =
                Convert.ToBoolean(Ini.GetOrDefault("DontCheckHostedAssets", false.ToString()));
            ConfigPackPath = Ini.GetOrDefault("PackPath");

            // === 解析导出内容段 ===
            RulesOverrides = Segments[1].Replace("\r", "\n")
                .Replace("\n" + "\n", "\n").Split("\n").ToList();

            // === 解析追加内容段 ===
            if (Segments.Length > 2)
                ExtraFiles = Segments[2].Replace("\r", "\n")
                    .Replace("\n" + "\n", "\n").Split("\n").ToList();
            else
                ExtraFiles = null;

            // 提示成功
            ModMain.Hint("已读取配置文件：" + configPath, ModMain.HintType.Finish);
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, $"读取配置文件失败：{configPath}", ModBase.LogLevel.Msgbox);
        }
    }

    #endregion

    // 读取配置文件
    private void ImportConfig(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var ConfigPath = SystemDialogs.SelectFile("整合包导出配置(*.txt)|*.txt", "选择配置文件",
                (string?)States.System.ExportConfigPath);
            if (string.IsNullOrEmpty(ConfigPath))
                return;

            // 调用核心读取逻辑
            ReadConfigFile(ConfigPath);
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "选择配置文件失败", ModBase.LogLevel.Msgbox);
        }
    }

    #region 拖放事件处理

    /// <summary>
    ///     文件拖入界面时触发：验证文件类型
    /// </summary>
    private void PanAllBack_DragEnter(object sender, DragEventArgs e)
    {
        // 检查是否包含文件拖放数据
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            // 获取拖入的文件路径数组
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            // 验证：仅允许单个.txt文件
            if (files.Length == 1 &&
                files[0].EndsWithF(".txt", true))
                e.Effects = DragDropEffects.Copy; // 设置拖放效果为“复制”
            else
                e.Effects = DragDropEffects.None; // 不允许拖放
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    /// <summary>
    ///     文件放下时触发：读取配置文件
    /// </summary>
    private void PanAllBack_Drop(object sender, DragEventArgs e)
    {
        // 获取拖入的文件路径
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var configPath = files[0];

            // 调用核心读取逻辑
            ReadConfigFile(configPath);
        }

        e.Handled = true;
    }

    #endregion

    #endregion

    #region 导出

    /// <summary>
    ///     配置文件中指定的导出位置。
    /// </summary>
    private string ConfigPackPath;

    /// <summary>
    ///     开始导出。
    /// </summary>
    private void StartExport(object sender, MouseButtonEventArgs e)
    {
        var PackName = string.IsNullOrEmpty(TextExportName.Text) ? TextExportName.HintText : TextExportName.Text;
        var PackVersion = string.IsNullOrEmpty(TextExportVersion.Text) ? "1.0.0" : TextExportVersion.Text;

        // 重复任务检查
        var LoaderName = "导出整合包：" + PackName;
        foreach (var OngoingLoader in ModLoader.LoaderTaskbar)
        {
            if ((OngoingLoader.Name ?? "") != (LoaderName ?? ""))
                continue;
            ModMain.FrmMain.PageChange(FormMain.PageType.TaskManager);
            return;
        }

        // 确认导出位置
        string PackPath = null;
        if (!string.IsNullOrWhiteSpace(ConfigPackPath) && !ConfigPackPath.EndsWithF(@"\") &&
            !ConfigPackPath.EndsWithF("/"))
            try
            {
                Directory.CreateDirectory(ModBase.GetPathFromFullPath(ConfigPackPath));
                PackPath = ConfigPackPath;
                ModBase.Log($"[Export] 使用配置文件中指定的导出路径：{ConfigPackPath}");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"无法使用配置文件中指定的导出路径（{ConfigPackPath}）");
                if (ModMain.MyMsgBox($"指定的路径：{ConfigPackPath}{"\r\n"}{"\r\n"}{ex}",
                        "无法使用配置文件中指定的导出路径", Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel")) == 2)
                    return;
            }

        if (PackPath is null)
        {
            var Extensions = new List<string>();
            if (CheckAdvancedModrinth.Checked == false)
                Extensions.Add("压缩文件(*.zip)|*.zip");
            if (CheckOptionsPcl.Checked == false)
                Extensions.Add("Modrinth 整合包文件(*.mrpack)|*.mrpack");
            PackPath = SystemDialogs.SelectSaveFile("选择导出位置",
                PackName + (string.IsNullOrEmpty(TextExportVersion.Text) ? "" : " " + TextExportVersion.Text),
                Extensions.Join("|"));
            ModBase.Log($"[Export] 手动指定的导出路径：{PackPath}");
        }

        if (string.IsNullOrEmpty(PackPath))
            return;

        // 缓存所需参数
        var CacheFolder = ModMain.RequestTaskTempFolder();
        var OverridesFolder = Path.Combine(CacheFolder, "modpack", "overrides");
        var McInstance = PageInstanceLeft.Instance;
        var PathIndie = McInstance.PathIndie;
        var CheckHostedAssets = (bool)!CheckAdvancedInclude.Checked;
        var ModrinthUploadMode = (bool)CheckAdvancedModrinth.Checked;
        var IncludePCL = (bool)CheckOptionsPcl.Checked;
        var IncludePCLCustom = (bool)(IncludePCL ? CheckOptionsPclCustom.Checked : (bool?)false);
        var AllRules = StandardizeLines(GetAllRules(), true).ToList();
        var AllExtraFiles = StandardizeLines(GetExtraFileLines(), false).ToList();
        ModBase.Log($"[Export] 准备导出整合包，共有 {AllRules.Count} 条规则，{AllExtraFiles.Count} 条追加内容行");

        // 构造步骤加载器
        var Loaders = new List<ModLoader.LoaderBase>();

        #region 准备 PCL 文件
        
        #if !RELEASE
        if (IncludePCL)
            Loaders.Add(new ModLoader.LoaderTask<int, int>("下载 PCL 正式版", Loader =>
            {
                UpdateManager.DownloadLatestPCL(Loader);
                ModBase.CopyFile(Path.Combine(ModBase.PathTemp, "CE-Latest.exe"), Path.Combine(CacheFolder, "Plain Craft Launcher.exe"));
            })
            {
                ProgressWeight = 0.5d,
                Block = false
            });
        #endif

        #endregion

        #region 复制文件

        Loaders.Add(new ModLoader.LoaderTask<int, List<ModLocalComp.LocalCompFile>>("复制导出内容", Loader =>
        {
            Loader.Output = new List<ModLocalComp.LocalCompFile>();
            // 复制实例文件
            var Progress = 0;
            Action<DirectoryInfo> SearchFolder = null;
            SearchFolder = Folder =>
            {
                // 文件夹：进一步搜索
                foreach (var SubFolder in Folder.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
                {
                    // 跳过部分又没用文件又多的文件夹，加快搜索
                    if ((Folder.FullName ?? "") == (PathIndie ?? "") &&
                        new[] { "assets", "versions", "libraries" }.Contains(SubFolder.Name))
                        continue;
                    if (new[] { "structureCacheV1", ".fabric", ".git", "avatar-cache", "cosmetic-cache" }.Contains(
                            SubFolder.Name))
                        continue;
                    SearchFolder(SubFolder);
                }

                // 文件：检查规则并复制
                foreach (var Entry in Folder.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
                {
                    var RelativePath = Entry.FullName.AfterFirst(PathIndie);
                    // 检查规则
                    var ShouldKeep = false;
                    foreach (var Rule in AllRules)
                    {
                        var Revert = Rule.StartsWith("!");
                        if (LikeString(RelativePath, Rule.TrimStart('!')))
                            ShouldKeep = !Revert;
                    }

                    if (!ShouldKeep)
                        continue;
                    var TargetPath = OverridesFolder + RelativePath;
                    ModBase.CopyFile(Entry.FullName, TargetPath);
                    // 若为压缩包，考虑联网获取路径
                    if (CheckHostedAssets &&
                        new[] { ".zip", ".rar", ".jar", ".disabled", ".old" }.Contains(Entry.Extension.ToLower()) &&
                        new[] { "mods", "packs", "openloader", "resource" }.Any(s => RelativePath.Contains(s)))
                    {
                        var ModFile = new ModLocalComp.LocalCompFile(TargetPath);
                        var Unused = ModFile.ModrinthHash; // 提前计算 Hash
                        Unused = ModFile.CurseForgeHash.ToString();
                        Loader.Output.Add(ModFile);
                    }

                    // 更新进度（进度并不准确，主要突出一个我还没似）
                    Progress += 1;
                    if (Progress == 25)
                    {
                        Loader.Progress += (0.94d - Loader.Progress) * 0.012d;
                        Progress = 0;
                    }
                }
            };
            SearchFolder(new DirectoryInfo(PathIndie));
            ModBase.Log($"[Export] 复制 overrides 文件完成，有 {Loader.Output.Count} 个文件需要联网检查");
            Loader.Progress = 0.95d;
            // 复制追加内容到根目录
            var BaseFolder = IncludePCL ? CacheFolder : Path.Combine(CacheFolder, "modpack");
            foreach (var Line in AllExtraFiles)
                if (Line.EndsWithF(@"\") || Line.EndsWithF("/"))
                {
                    if (Directory.Exists(Line))
                        ModBase.CopyDirectory(Line, Path.Combine(BaseFolder, ModBase.GetFolderNameFromPath(Line)) + @"\");
                    else
                        ModMain.Hint($"未找到配置文件中指定的文件夹：{Line}", ModMain.HintType.Critical);
                }
                else if (File.Exists(Line))
                {
                    ModBase.CopyFile(Line, BaseFolder + ModBase.GetFileNameFromPath(Line));
                }
                else
                {
                    ModMain.Hint($"未找到配置文件中指定的单个文件：{Line}", ModMain.HintType.Critical);
                }

            Loader.Progress = 0.97d;
            // 复制 PCL 实例设置
            ModBase.CopyDirectory(Path.Combine(McInstance.PathInstance, "PCL"), Path.Combine(OverridesFolder, "PCL"));
            #if RELEASE
                        // 复制 PCL 本体
                        if (IncludePCL) ModBase.CopyFile(ModBase.ExePathWithName, Path.Combine(CacheFolder, "Plain Craft Launcher.exe"));
            #endif
            // 复制 PCL 个性化内容
            if (IncludePCLCustom)
            {
                if (Directory.Exists(Path.Combine(ModBase.ExePath, "PCL", "Pictures")))
                    ModBase.CopyDirectory(Path.Combine(ModBase.ExePath, "PCL", "Pictures"), Path.Combine(CacheFolder, "PCL", "Pictures"));
                if (Directory.Exists(Path.Combine(ModBase.ExePath, "PCL", "Musics")))
                    ModBase.CopyDirectory(Path.Combine(ModBase.ExePath, "PCL", "Musics"), Path.Combine(CacheFolder, "PCL", "Musics"));
                if (Directory.Exists(Path.Combine(ModBase.ExePath, "PCL", "Help")))
                    ModBase.CopyDirectory(Path.Combine(ModBase.ExePath, "PCL", "Help"), Path.Combine(CacheFolder, "PCL", "Help"));
                if (File.Exists(Path.Combine(ModBase.ExePath, "PCL", "Custom.xaml")))
                    ModBase.CopyFile(Path.Combine(ModBase.ExePath, "PCL", "Custom.xaml"), Path.Combine(CacheFolder, "PCL", "Custom.xaml"));
                if (File.Exists(Path.Combine(ModBase.ExePath, "PCL", "Setup.ini")))
                    ModBase.CopyFile(Path.Combine(ModBase.ExePath, "PCL", "Setup.ini"), Path.Combine(CacheFolder, "PCL", "Setup.ini"));
                if (File.Exists(Path.Combine(ModBase.ExePath, "PCL", "hints.txt")))
                    ModBase.CopyFile(Path.Combine(ModBase.ExePath, "PCL", "hints.txt"), Path.Combine(CacheFolder, "PCL", "hints.txt"));
                if (File.Exists(Path.Combine(ModBase.ExePath, "PCL", "Logo.png")))
                    ModBase.CopyFile(Path.Combine(ModBase.ExePath, "PCL", "Logo.png"), Path.Combine(CacheFolder, "PCL", "Logo.png"));
            }
        })
        {
            ProgressWeight = 5d
        });

        #endregion

        #region 联网检查

        Loaders.Add(
            new ModLoader.LoaderTask<List<ModLocalComp.LocalCompFile>,
                Dictionary<ModLocalComp.LocalCompFile, List<string>>>("联网获取文件信息", Loader =>
            {
                Loader.Output = new Dictionary<ModLocalComp.LocalCompFile, List<string>>();
                if (!CheckHostedAssets)
                {
                    ModBase.Log("[Export] 要求跳过联网获取步骤");
                    return;
                }

                if (!Loader.Input.Any())
                {
                    ModBase.Log("[Export] 没有需要联网检查的文件，跳过联网获取步骤");
                    return;
                }

                // 分平台获取下载地址
                var EndedThreadCount = 0;
                var FailedExceptions = new List<Exception>();

                // 从 Modrinth 获取信息
                // 查找对应的文件
                // 写入下载地址
                ModBase.RunInNewThread(() =>
                {
                    try
                    {
                        var ModrinthHashes = Loader.Input.Select(m => m.ModrinthHash);
                        var ModrinthRaw = (JsonObject)ModBase.GetJson(ModDownload.DlModRequest(
                            "https://api.modrinth.com/v2/version_files", "POST",
                            $"{{\"hashes\": [\"{ModrinthHashes.Join("\",\"")}\"], \"algorithm\": \"sha1\"}}",
                            "application/json"));
                        foreach (var ModFile in Loader.Input)
                        {
                            if (!ModrinthRaw.ContainsKey(ModFile.ModrinthHash)) continue;
                            if ((string)ModrinthRaw[ModFile.ModrinthHash]?["files"]?[0]["hashes"]?["sha1"] !=
                                ModFile.ModrinthHash) continue;
                            Loader.Output.AddToList(ModFile,
                                (string)ModrinthRaw[ModFile.ModrinthHash]["files"][0]["url"]);
                        }

                        ModBase.Log($"[Export] 从 Modrinth 获取到 {ModrinthRaw.Count} 个本地资源项的对应信息");
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "从 Modrinth 获取本地 Mod 信息失败");
                        FailedExceptions.Add(ex);
                    }
                    finally
                    {
                        EndedThreadCount += 1;
                        Loader.Progress += 0.45d;
                    }
                }, "Modrinth - " + LoaderName);

                // 从 CurseForge 获取信息
                // 查找对应的文件
                // 写入下载地址
                ModBase.RunInNewThread(() =>
                {
                    try
                    {
                        if (ModrinthUploadMode) return;
                        var CurseForgeHashes = Loader.Input.Select(m => m.CurseForgeHash);
                        var CurseForgeRaw = (JsonNode)((JsonObject)ModBase.GetJson(
                            ModDownload.DlModRequest("https://api.curseforge.com/v1/fingerprints/432/", "POST",
                                $"{{\"fingerprints\": [{CurseForgeHashes.Join(",")}]}}", "application/json")))["data"][
                            "exactMatches"];
                        foreach (JsonObject ResultJson in CurseForgeRaw.AsArray())
                        {
                            if (!ResultJson.ContainsKey("file")) continue;
                            var File = (JsonObject)ResultJson["file"];
                            if (string.IsNullOrEmpty((string)File["downloadUrl"])) continue;
                            var ModFile = Loader.Input.FirstOrDefault(m =>
                                m.CurseForgeHash == File["fileFingerprint"].ToObject<uint>());
                            if (ModFile is null) continue;
                            Loader.Output.AddToList(ModFile,
                                ModComp.CompFile.HandleCurseForgeDownloadUrls(File["downloadUrl"].ToString()));
                        }

                        ModBase.Log($"[Export] 从 CurseForge 获取到 {CurseForgeRaw.AsArray().Count} 个本地资源项的对应信息");
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "从 CurseForge 获取本地 Mod 信息失败");
                        FailedExceptions.Add(ex);
                    }
                    finally
                    {
                        EndedThreadCount += 1;
                        Loader.Progress += 0.45d;
                    }
                }, "CurseForge - " + LoaderName); // Modrinth 上传模式下，不能从 CurseForge 获取信息

                // 等待线程结束
                while (EndedThreadCount != 2)
                {
                    if (Loader.IsAborted)
                        return;
                    Thread.Sleep(10);
                }

                // 若失败，确认是否继续
                if (FailedExceptions.Count == 1)
                {
                    if (ModMain.MyMsgBox(
                            "联网获取部分文件信息失败，是否继续导出？" + "\r\n" + "\r\n" + "若继续，无法获取信息的文件将被直接打包。" +
                            "\r\n" + "由于二次分发可能违反使用协议，请尽量不要公开发布导出的整合包！", "部分文件信息获取失败", "继续", Lang.Text("Common.Action.Cancel")) == 2)
                        throw FailedExceptions.First();
                }
                else if (FailedExceptions.Count > 1)
                {
                    if (ModMain.MyMsgBox(
                            "联网获取文件信息失败，是否继续导出？" + "\r\n" + "\r\n" + "若继续，所有文件都将被直接打包。" +
                            "\r\n" + "由于二次分发可能违反使用协议，请尽量不要公开发布导出的整合包！", "文件信息获取失败", "继续", Lang.Text("Common.Action.Cancel")) == 2)
                        throw FailedExceptions.First();
                }
            })
            {
                Show = CheckHostedAssets,
                ProgressWeight = CheckHostedAssets ? 2d : 0.01d
            });

        #endregion

        #region 生成压缩包

        Loaders.Add(new ModLoader.LoaderTask<Dictionary<ModLocalComp.LocalCompFile, List<string>>, int>("生成压缩包",
            Loader =>
            {
                // 整理文件列表
                var Files = new JsonArray();
                foreach (var Pair in Loader.Input)
                {
                    var ModFile = Pair.Key;
                    Files.Add(new JsonObject
                    {
                        { "path", ModFile.Path.AfterFirst(OverridesFolder).Replace(@"\", "/") },
                        {
                            "hashes",
                            new JsonObject
                            {
                                { "sha1", ModFile.ModrinthHash }, { "sha512", ModBase.GetFileSHA512(ModFile.Path) }
                            }
                        },
                        { "downloads", new JsonArray(Pair.Value.OrderByDescending(u => u.Contains("modrinth.com")).Select(s => (JsonNode)s).ToArray()) },
                        { "fileSize", new FileInfo(ModFile.Path).Length }
                    });
                    File.Delete(ModFile.Path);
                }

                Loader.Progress = 0.2d;
                // 导出最终 JSON 文件
                var Dependencies = new JsonObject { { "minecraft", McInstance.Info.VanillaName } };
                if (McInstance.Info.HasForge)
                    Dependencies.Add("forge", McInstance.Info.Forge);
                if (McInstance.Info.HasFabric)
                    Dependencies.Add("fabric-loader", McInstance.Info.Fabric);
                if (McInstance.Info.HasNeoForge)
                    Dependencies.Add("neoforge", McInstance.Info.NeoForge);
                var ResultJson = new JsonObject
                {
                    { "game", "minecraft" }, { "formatVersion", 1 }, { "versionId", PackVersion }, { "name", PackName },
                    { "summary", McInstance.Desc }, { "files", Files }, { "dependencies", Dependencies }
                };
                File.WriteAllText(Path.Combine(CacheFolder, "modpack", "modrinth.index.json"),
                    ResultJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                // 打包
                Directory.CreateDirectory(ModBase.GetPathFromFullPath(PackPath));
                if (File.Exists(PackPath))
                    File.Delete(PackPath);
                if (IncludePCL)
                {
                    // 首次压缩整合包
                    ZipFile.CreateFromDirectory(Path.Combine(CacheFolder, "modpack"), Path.Combine(CacheFolder, "modpack.mrpack"));
                    Loader.Progress = 0.5d;
                    Directory.Delete(Path.Combine(CacheFolder, "modpack"), true);
                    Loader.Progress = 0.6d;
                    // 二次压缩整合包
                    ZipFile.CreateFromDirectory(CacheFolder, PackPath);
                    Loader.Progress = 0.9d;
                }
                else
                {
                    // 直接压缩整合包
                    ZipFile.CreateFromDirectory(Path.Combine(CacheFolder, "modpack"), PackPath);
                    Loader.Progress = 0.8d;
                }

                Directory.Delete(CacheFolder, true);
                ModBase.OpenExplorer(PackPath);
            })
        {
            ProgressWeight = 6d
        });

        #endregion

        // 启动
        var MainLoader = new ModLoader.LoaderCombo<string>(LoaderName, Loaders)
            { OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly };
        MainLoader.Start();
        ModLoader.LoaderTaskbarAdd(MainLoader);
        ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
        ModMain.FrmMain.BtnExtraDownload.Ribble();
        ModMain.FrmMain.PageChange(FormMain.PageType.TaskManager);
    }

    #endregion

    private static bool LikeString(string input, string pattern)
    {
        pattern = pattern.Replace("#", "[0-9]");
        var options = new GlobOptions { Evaluation = { CaseInsensitive = true } };
        var glob = Glob.Parse(pattern, options);
        return glob.IsMatch(input);
    }
}
