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
using PCL.Core.Utils;

namespace PCL;

public class ExportOption : DependencyObject
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ExportOption)
    );

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(ExportOption)
    );

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string Rules { get; set; }

    /// <summary>
    ///     如果 Rules 为空，则根据 ShowRules 的内容判断是否应该显示这个复选框。
    ///     如果 ShowRules 也为空，则始终显示。
    /// </summary>
    public string ShowRules { get; set; }

    public bool DefaultChecked { get; set; }
    public bool RequireModLoader { get; set; }
    public bool RequireOptiFine { get; set; }
    public bool RequireModLoaderOrOptiFine { get; set; }
}

public partial class PageInstanceExport : IRefreshable
{
    private string currentVersion = "";

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
        if ((currentVersion ?? "") != (PageInstanceLeft.instance.PathInstance ?? ""))
            RefreshAll(); // 切换到了另一个实例，重置页面
        CustomEventService.SetEventData(BtnAdvancedHelp, "指南/整合包制作.json");
        ModAnimation.AniControlEnabled -= 1;
    }

    public void RefreshAll()
    {
        ModBase.Log("[Export] 刷新导出页面");
        HintOptiFine.Visibility =
            PageInstanceLeft.instance.Info.HasOptiFine ? Visibility.Visible : Visibility.Collapsed;
        currentVersion = PageInstanceLeft.instance.PathInstance;
        TextExportName.Text = "";
        TextExportName.HintText = PageInstanceLeft.instance.Name;
        TextExportVersion.Text = "";
        TextExportVersion.HintText = "1.0.0";
        CheckAdvancedInclude.Checked = false;
        CheckAdvancedModrinth.Checked = false;
        GetExportOption(CheckOptionsBasic).Description = PageInstanceLeft.instance.GetDefaultDescription();
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

    private readonly string[] subOptionBlackList = new[] { "Quark Programmer Art.zip", "+ EuphoriaPatches_" };

    /// <summary>
    ///     动态生成子文件夹下的选项，例如资源包、存档等。
    /// </summary>
    private void ReloadAllSubOptions()
    {
        ReloadSubOptions(PanOptionsResourcePacks, true, true, "resourcepacks", "texturepacks");
        ReloadSubOptions(PanOptionsSaves, false, true, "saves");
        ReloadSubOptions(PanOptionsShaderPacks, true, true, "shaderpacks");
    }

    private void ReloadSubOptions(StackPanel panel, bool acceptCompressedFile, bool acceptFolder,
        params string[] folders)
    {
        panel.Children.Clear();
        foreach (var Folder in folders)
        {
            var targetFolder = new DirectoryInfo(PageInstanceLeft.instance.PathIndie + Folder);
            if (!targetFolder.Exists)
                continue;
            // 查找文件夹下的对应项
            if (acceptCompressedFile)
                foreach (var File in targetFolder.EnumerateFiles("*.zip").Concat(targetFolder.EnumerateFiles("*.rar")))
                {
                    if (subOptionBlackList.Any(b => File.Name.ContainsF(b)))
                        continue;
                    panel.Children.Add(new MyCheckBox
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
                            panel.Children.Add(new MyCheckBox
                            {
                                Margin = new Thickness(30, 0, 0, 0),
                                Tag = new ExportOption
                                {
                                    Title = $"{shaderConfig.Name}", DefaultChecked = true,
                                    Description = Lang.Text("Instance.Export.ShaderConfigSuffix"),
                                    Rules = ModBase.EscapeLikePattern($"{Folder}/{shaderConfig.Name}")
                                }
                            });
                    }
                }

            if (acceptFolder)
                foreach (var SubFolder in targetFolder.EnumerateDirectories().OrderByDescending(f => f.LastWriteTime))
                {
                    if (subOptionBlackList.Any(b => SubFolder.Name.ContainsF(b)))
                        continue;
                    if (!SubFolder.EnumerateFileSystemInfos().Any())
                        continue;
                    var newCheckBox = new MyCheckBox
                    {
                        Tag = new ExportOption
                        {
                            Title = SubFolder.Name, DefaultChecked = true,
                            Rules = ModBase.EscapeLikePattern($"{Folder}/{SubFolder.Name}/")
                        }
                    };
                    if (ReferenceEquals(panel, PanOptionsSaves))
                        GetExportOption(newCheckBox).Description =
                            Lang.Date(SubFolder.LastWriteTime, "g");
                    panel.Children.Add(newCheckBox);
                    if (Folder == "shaderpacks") // 处理文件夹形式光影包的配置文件
                    {
                        var shaderConfig = new FileInfo(Path.Combine(targetFolder.FullName,
                            $"{SubFolder.Name}.txt"));
                        if (shaderConfig.Exists)
                            panel.Children.Add(new MyCheckBox
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
        var allEntries = new List<string>();

        bool IsValidDirectory(DirectoryInfo folder)
        {
            try
            {
                return folder.Exists && folder.EnumerateFileSystemInfos()
                    .Any(i => !subOptionBlackList.Any(b => i.Name.ContainsF(b)));
            }
            catch
            {
                return false;
            }
        }

        ; // 检查文件夹不为空
        // 一般是由于无法访问，或是一个指向已不存在的文件夹的链接（例如使用 mklink 创造的 resource 文件夹链接）
        var pathInfo = new DirectoryInfo(PageInstanceLeft.instance.PathIndie);
        allEntries.AddRange(pathInfo.EnumerateFiles().Select(f => f.Name));
        foreach (var SubFolder in pathInfo.EnumerateDirectories().Where(IsValidDirectory))
        {
            allEntries.Add($@"{SubFolder.Name}\");
            allEntries.AddRange(SubFolder.EnumerateFiles().Select(f => $@"{SubFolder.Name}\{f.Name}"));
            allEntries.AddRange(SubFolder.EnumerateDirectories().Where(IsValidDirectory)
                .Select(d => $@"{SubFolder.Name}\{d.Name}\"));
        }

        ModBase.Log($"[Export] 共发现 {allEntries.Count} 个可行的二级文件/文件夹");

        // 确认选项是否应该被显示
        bool IsVisible(ExportOption targetOption)
        {
            // 检查需要 OptiFine 或 Mod 加载器
            if (targetOption.RequireOptiFine && !PageInstanceLeft.instance.Info.HasOptiFine)
                return false;
            if (targetOption.RequireModLoader && !PageInstanceLeft.instance.Modable)
                return false;
            if (targetOption.RequireModLoaderOrOptiFine && !PageInstanceLeft.instance.Info.HasOptiFine &&
                !PageInstanceLeft.instance.Modable)
                return false;
            // 粗略检查是否可能有符合规则的文件/文件夹
            return StandardizeLines((targetOption.Rules ?? targetOption.ShowRules).Split('|'), true).Any(rule =>
            {
                if (rule.StartsWithF("!"))
                    return false; // 只看正向规则
                // 检查前两级
                try
                {
                    if (allEntries.Any(entry => LikeString(entry, rule)))
                        return true;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"错误的规则：{rule}", ModBase.LogLevel.Hint);
                    return false;
                }

                // 粗略检查所有级
                rule = rule.Trim("*?".ToCharArray());
                if (rule.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).Count() >= 3)
                {
                    if (rule.EndsWithF(@"\"))
                        return IsValidDirectory(new DirectoryInfo(PageInstanceLeft.instance.PathIndie + rule)); // 文件夹有效

                    return File.Exists(PageInstanceLeft.instance.PathIndie + rule);
                    // 文件有效
                }

                return false;
            });
        }

        ;
        // 逐个检查选项
        foreach (var CheckBox in GetAllOptions(true))
        {
            var targetOption = GetExportOption(CheckBox);
            // 名称与简介
            CheckBox.Inlines.Clear();
            CheckBox.Inlines.Add(new Run(targetOption.Title));
            if (!string.IsNullOrEmpty(targetOption.Description))
                CheckBox.Inlines.Add(new Run("   " + targetOption.Description) { Foreground = ThemeManager.colorGray5 });
            // 可见性、默认勾选
            if (string.IsNullOrEmpty(targetOption.Rules) && string.IsNullOrEmpty(targetOption.ShowRules))
            {
                CheckBox.Visibility = Visibility.Visible;
                CheckBox.Checked = targetOption.DefaultChecked;
            }
            else
            {
                var pass = IsVisible(targetOption);
                CheckBox.Visibility = pass ? Visibility.Visible : Visibility.Collapsed;
                CheckBox.Checked = targetOption.DefaultChecked && pass;
            }
        }
    }

    /// <summary>
    ///     对文本行进行标准化处理，以便使用 Like 进行匹配。
    /// </summary>
    private IEnumerable<string> StandardizeLines(IEnumerable<string> raw, bool addSuffixStarToFolderPath)
    {
        foreach (var IgnoreLineRaw in raw)
        {
            var ignoreLine = IgnoreLineRaw;
            ignoreLine = ignoreLine.Trim();
            if (string.IsNullOrEmpty(ignoreLine) || ignoreLine.StartsWithF("#") || ignoreLine.StartsWithF("="))
                continue;
            ignoreLine = ignoreLine.Replace("/", @"\");
            yield return ignoreLine + (ignoreLine.EndsWithF(@"\") && addSuffixStarToFolderPath ? "**" : "");
        }
    }

    /// <summary>
    ///     获取所有可作为选项的 CheckBox。
    /// </summary>
    private IEnumerable<MyCheckBox> GetAllOptions(bool includeHidden)
    {
        foreach (var Element in PanOptions.Children)
        {
            if (!includeHidden &&
                ((UIElement)Element).Visibility != Visibility.Visible)
                continue;
            if (Element is MyCheckBox)
                yield return (MyCheckBox)Element;
            else if (Element is StackPanel)
                foreach (var SubElement in ((StackPanel)Element).Children)
                {
                    if (!includeHidden && ((UIElement)SubElement).Visibility != Visibility.Visible)
                        continue;
                    if (SubElement is MyCheckBox)
                        yield return (MyCheckBox)SubElement;
                }
        }
    }

    /// <summary>
    ///     获取该 CheckBox 对应的 ExportOption。
    /// </summary>
    private ExportOption GetExportOption(MyCheckBox checkBox)
    {
        return (ExportOption)checkBox.Tag;
    }

    #endregion

    #region 配置文件

    private const string sperator = "==============================================================";

    // ================ 导出内容段 ================

    /// <summary>
    ///     从配置文件中读取的规则。
    ///     如果不为 Nothing，则会覆写当前勾选的规则并禁用对应 UI。
    /// </summary>
    private List<string> RulesOverrides
    {
        get => field;
        set
        {
            field = value;
            if (value is null)
            {
                BtnOverrideCancel.Visibility = Visibility.Collapsed;
                PanOptions.Visibility = Visibility.Visible;
                CardOptions.Inlines.Clear();
                CardOptions.Inlines.Add(new Run(Lang.Text("Instance.Export.OptionListTitle")) { FontWeight = FontWeights.Bold });
            }
            else
            {
                BtnOverrideCancel.Visibility = Visibility.Visible;
                PanOptions.Visibility = Visibility.Collapsed;
                CardOptions.Inlines.Clear();
                CardOptions.Inlines.Add(new Run(Lang.Text("Instance.Export.OptionListTitle") + ":    ") { FontWeight = FontWeights.Bold });
                CardOptions.Inlines.Add(new Run(Lang.Text("Instance.Export.OptionList.FromConfig")) { FontWeight = FontWeights.Normal });
            }
        }
    }

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
            yield return "# " + Lang.Text("Instance.Export.ConfigComment.ModifyRules");
            yield return "# " + Lang.Text("Instance.Export.ConfigComment.ReverseMatch");
            yield return "";
            foreach (var CheckBox in GetAllOptions(false))
            {
                if (CheckBox.Checked == false)
                    continue;
                var targetOption = GetExportOption(CheckBox);
                if (targetOption.Rules is null)
                    continue;
                yield return $"# {targetOption.Title}";
                foreach (var Rule in targetOption.Rules.Split('|'))
                    yield return Rule;
                yield return "";
            }

            yield return "# " + Lang.Text("Instance.Export.ConfigComment.ExcludedFiles");
            yield return "!*.log";
            yield return "!*.dat_old";
            yield return "!*.BakaCoreInfo";
            yield return "!hmclversion.cfg";
            yield return "!log4j2.xml";
            yield return "";
        }
    }

    // ================ 追加内容段 ================

    private List<string> extraFiles;

    /// <summary>
    ///     获取当前实际生效的追加内容。
    /// </summary>
    private IEnumerable<string> GetExtraFileLines()
    {
        if (extraFiles is not null)
        {
            // 返回覆盖的列表
            foreach (var File in extraFiles)
                yield return File;
        }
        else
        {
            // 从当前勾选的所有选项中获取所有规则行
            yield return "";
            yield return "# " + Lang.Text("Instance.Export.ConfigComment.ExtraFiles");
            yield return "# " + Lang.Text("Instance.Export.ConfigComment.ExtraFiles2");
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
        configPackPath = null;
        extraFiles = null;
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
            var configPath = SystemDialogs.SelectSaveFile(Lang.Text("Instance.Export.SelectFileLocation"), "export_config.txt", Lang.Text("Instance.Export.ConfigFileFilter"),
                (string?)States.System.ExportConfigPath);
            if (string.IsNullOrEmpty(configPath))
                return;
            States.System.ExportConfigPath = configPath;
            var configLines = new List<string>();
            // ini 段
            configLines.Add("Name:" + TextExportName.Text);
            configLines.Add("Version:" + TextExportVersion.Text);
            configLines.Add("");
            configLines.Add("# " + Lang.Text("Instance.Export.ConfigComment.IncludeLauncher"));
            configLines.Add("IncludeLauncher:" + CheckOptionsPcl.Checked);
            configLines.Add("");
            configLines.Add("# " + Lang.Text("Instance.Export.ConfigComment.IncludeLauncherCustom"));
            configLines.Add("IncludeLauncherCustom:" + CheckOptionsPclCustom.Checked);
            configLines.Add("");
            configLines.Add("# " + Lang.Text("Instance.Export.ConfigComment.BundleFiles"));
            configLines.Add("# " + Lang.Text("Instance.Export.ConfigComment.BundleFiles2"));
            configLines.Add("# " + Lang.Text("Instance.Export.ConfigComment.BundleFiles3"));
            configLines.Add("DontCheckHostedAssets:" + CheckAdvancedInclude.Checked);
            configLines.Add("");
            configLines.Add("# " + Lang.Text("Instance.Export.ConfigComment.Modrinth"));
            configLines.Add("# " + Lang.Text("Instance.Export.ConfigComment.Modrinth2"));
            configLines.Add("# " + Lang.Text("Instance.Export.ConfigComment.Modrinth3"));
            configLines.Add("ModrinthUploadMode:" + CheckAdvancedModrinth.Checked);
            configLines.Add("");
            configLines.Add("# " + Lang.Text("Instance.Export.ConfigComment.PackPath"));
            configLines.Add("# " + Lang.Text("Instance.Export.ConfigComment.PackPath2"));
            configLines.Add("# " + Lang.Text("Instance.Export.ConfigComment.PackPath3"));
            configLines.Add("PackPath:" + (configPackPath ?? ""));
            configLines.Add("");
            // 导出内容段
            configLines.Add(sperator);
            configLines.AddRange(GetAllRules());
            // 追加内容段
            configLines.Add(sperator);
            configLines.AddRange(GetExtraFileLines());
            // 结束
            ModBase.WriteFile(configPath, configLines.Join("\r\n"));
            ModMain.Hint(Lang.Text("Instance.Export.SaveSuccess", configPath), ModMain.HintType.Finish);
            ModBase.OpenExplorer(configPath);
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
            var segments = fileContent.Split(sperator);

            if (segments.Length == 0)
            {
                ModMain.Hint(Lang.Text("Instance.Export.ConfigInvalid"), ModMain.HintType.Critical);
                return;
            }

            // === 解析INI段 ===
            var ini = new Dictionary<string, string>();
            foreach (var LineRaw in segments[0].Split("\r\n".ToCharArray()))
            {
                var line = LineRaw;
                line = line.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWithF("#") || line.StartsWithF("="))
                    continue;
                var index = line.IndexOfF(":");
                if (index > 0) ini[line.Substring(0, index)] = line.Substring(index + 1);
            }

            // 赋值到界面控件
            TextExportName.Text = ini.GetOrDefault("Name", "");
            TextExportVersion.Text = ini.GetOrDefault("Version", "");
            CheckOptionsPcl.Checked =
                Convert.ToBoolean(ini.GetOrDefault("IncludeLauncher", true.ToString()));
            CheckOptionsPclCustom.Checked =
                Convert.ToBoolean(ini.GetOrDefault("IncludeLauncherCustom", true.ToString()));
            CheckAdvancedModrinth.Checked =
                Convert.ToBoolean(ini.GetOrDefault("ModrinthUploadMode", false.ToString()));
            CheckAdvancedInclude.Checked =
                Convert.ToBoolean(ini.GetOrDefault("DontCheckHostedAssets", false.ToString()));
            configPackPath = ini.GetOrDefault("PackPath");

            // === 解析导出内容段 ===
            RulesOverrides = segments[1].Replace("\r", "\n")
                .Replace("\n" + "\n", "\n").Split("\n").ToList();

            // === 解析追加内容段 ===
            if (segments.Length > 2)
                extraFiles = segments[2].Replace("\r", "\n")
                    .Replace("\n" + "\n", "\n").Split("\n").ToList();
            else
                extraFiles = null;

            // 提示成功
            ModMain.Hint(Lang.Text("Instance.Export.ReadSuccess", configPath), ModMain.HintType.Finish);
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
            var configPath = SystemDialogs.SelectFile(Lang.Text("Instance.Export.ConfigFileFilter"), Lang.Text("Instance.Export.SelectConfigFile"),
                (string?)States.System.ExportConfigPath);
            if (string.IsNullOrEmpty(configPath))
                return;

            // 调用核心读取逻辑
            ReadConfigFile(configPath);
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
    private string configPackPath;

    /// <summary>
    ///     开始导出。
    /// </summary>
    private void StartExport(object sender, MouseButtonEventArgs e)
    {
        var packName = string.IsNullOrEmpty(TextExportName.Text) ? TextExportName.HintText : TextExportName.Text;
        var packVersion = string.IsNullOrEmpty(TextExportVersion.Text) ? "1.0.0" : TextExportVersion.Text;

        // 重复任务检查
        var loaderName = Lang.Text("Instance.Export.ExportTask.Prefix") + packName;
        foreach (var OngoingLoader in ModLoader.loaderTaskbar)
        {
            if ((OngoingLoader.name ?? "") != (loaderName ?? ""))
                continue;
            ModMain.frmMain.PageChange(FormMain.PageType.TaskManager);
            return;
        }

        // 确认导出位置
        string packPath = null;
        if (!string.IsNullOrWhiteSpace(configPackPath) && !configPackPath.EndsWithF(@"\") &&
            !configPackPath.EndsWithF("/"))
            try
            {
                Directory.CreateDirectory(ModBase.GetPathFromFullPath(configPackPath));
                packPath = configPackPath;
                ModBase.Log($"[Export] 使用配置文件中指定的导出路径：{configPackPath}");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"无法使用配置文件中指定的导出路径（{configPackPath}）");
                if (ModMain.MyMsgBox(Lang.Text("Instance.Export.PathError", configPackPath) + "\r\n\r\n" + ex,
                        Lang.Text("Instance.Export.PackPathInvalid.Title"), Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel")) == 2)
                    return;
            }

        if (packPath is null)
        {
            var extensions = new List<string>();
            if (CheckAdvancedModrinth.Checked == false)
                extensions.Add(Lang.Text("Instance.Export.ZipFilter"));
            if (CheckOptionsPcl.Checked == false)
                extensions.Add(Lang.Text("Instance.Export.MrpackFilter"));
            packPath = SystemDialogs.SelectSaveFile(Lang.Text("Instance.Export.SelectSaveLocation"),
                packName + (string.IsNullOrEmpty(TextExportVersion.Text) ? "" : " " + TextExportVersion.Text),
                extensions.Join("|"));
            ModBase.Log($"[Export] 手动指定的导出路径：{packPath}");
        }

        if (string.IsNullOrEmpty(packPath))
            return;

        // 缓存所需参数
        var cacheFolder = ModMain.RequestTaskTempFolder();
        var overridesFolder = Path.Combine(cacheFolder, "modpack", "overrides");
        var mcInstance = PageInstanceLeft.instance;
        var pathIndie = mcInstance.PathIndie;
        var checkHostedAssets = (bool)!CheckAdvancedInclude.Checked;
        var modrinthUploadMode = (bool)CheckAdvancedModrinth.Checked;
        var includePCL = (bool)CheckOptionsPcl.Checked;
        var includePCLCustom = (bool)(includePCL ? CheckOptionsPclCustom.Checked : (bool?)false);
        var allRules = StandardizeLines(GetAllRules(), true).ToList();
        var allExtraFiles = StandardizeLines(GetExtraFileLines(), false).ToList();
        ModBase.Log($"[Export] 准备导出整合包，共有 {allRules.Count} 条规则，{allExtraFiles.Count} 条追加内容行");

        // 构造步骤加载器
        var loaders = new List<ModLoader.LoaderBase>();

        #region 准备 PCL 文件
        
        #if !RELEASE
        if (includePCL)
            loaders.Add(new ModLoader.LoaderTask<int, int>("下载 PCL 正式版", loader =>
            {
                UpdateManager.DownloadLatestPCL(loader);
                ModBase.CopyFile(Path.Combine(ModBase.pathTemp, "CE-Latest.exe"), Path.Combine(cacheFolder, "Plain Craft Launcher.exe"));
            })
            {
                ProgressWeight = 0.5d,
                block = false
            });
        #endif

        #endregion

        #region 复制文件

        loaders.Add(new ModLoader.LoaderTask<int, List<ModLocalComp.LocalCompFile>>("复制导出内容", loader =>
        {
            loader.output = new List<ModLocalComp.LocalCompFile>();
            // 复制实例文件
            var progress = 0;
            Action<DirectoryInfo> searchFolder = null;
            searchFolder = folder =>
            {
                // 文件夹：进一步搜索
                foreach (var SubFolder in folder.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
                {
                    // 跳过部分又没用文件又多的文件夹，加快搜索
                    if ((folder.FullName ?? "") == (pathIndie ?? "") &&
                        new[] { "assets", "versions", "libraries" }.Contains(SubFolder.Name))
                        continue;
                    if (new[] { "structureCacheV1", ".fabric", ".git", "avatar-cache", "cosmetic-cache" }.Contains(
                            SubFolder.Name))
                        continue;
                    searchFolder(SubFolder);
                }

                // 文件：检查规则并复制
                foreach (var Entry in folder.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
                {
                    var relativePath = Entry.FullName.AfterFirst(pathIndie);
                    // 检查规则
                    var shouldKeep = false;
                    foreach (var Rule in allRules)
                    {
                        var revert = Rule.StartsWith("!");
                        if (LikeString(relativePath, Rule.TrimStart('!')))
                            shouldKeep = !revert;
                    }

                    if (!shouldKeep)
                        continue;
                    var targetPath = Path.Combine(overridesFolder, relativePath);
                    ModBase.CopyFile(Entry.FullName, targetPath);
                    // 若为压缩包，考虑联网获取路径
                    if (checkHostedAssets &&
                        new[] { ".zip", ".rar", ".jar", ".disabled", ".old" }.Contains(Entry.Extension.ToLower()) &&
                        new[] { "mods", "packs", "openloader", "resource" }.Any(s => relativePath.Contains(s)))
                    {
                        var modFile = new ModLocalComp.LocalCompFile(targetPath);
                        var unused = modFile.ModrinthHash; // 提前计算 Hash
                        unused = modFile.CurseForgeHash.ToString();
                        loader.output.Add(modFile);
                    }

                    // 更新进度（进度并不准确，主要突出一个我还没似）
                    progress += 1;
                    if (progress == 25)
                    {
                        loader.Progress += (0.94d - loader.Progress) * 0.012d;
                        progress = 0;
                    }
                }
            };
            searchFolder(new DirectoryInfo(pathIndie));
            ModBase.Log($"[Export] 复制 overrides 文件完成，有 {loader.output.Count} 个文件需要联网检查");
            loader.Progress = 0.95d;
            // 复制追加内容到根目录
            var baseFolder = includePCL ? cacheFolder : Path.Combine(cacheFolder, "modpack");
            foreach (var Line in allExtraFiles)
                if (Line.EndsWithF(@"\") || Line.EndsWithF("/"))
                {
                    if (Directory.Exists(Line))
                        ModBase.CopyDirectory(Line, Path.Combine(baseFolder, ModBase.GetFolderNameFromPath(Line)) + @"\");
                    else
                        ModMain.Hint(Lang.Text("Instance.Export.ConfigFolderNotFound", Line), ModMain.HintType.Critical);
                }
                else if (File.Exists(Line))
                {
                    ModBase.CopyFile(Line, Path.Combine(baseFolder, ModBase.GetFileNameFromPath(Line)));
                }
                else
                {
                    ModMain.Hint(Lang.Text("Instance.Export.ConfigFileNotFound", Line), ModMain.HintType.Critical);
                }

            loader.Progress = 0.97d;
            // 复制 PCL 实例设置
            ModBase.CopyDirectory(Path.Combine(mcInstance.PathInstance, "PCL"), Path.Combine(overridesFolder, "PCL"));
            #if RELEASE
                        // 复制 PCL 本体
                        if (includePCL) ModBase.CopyFile(Basics.ExecutablePath, Path.Combine(cacheFolder, Basics.ExecutableName));
            #endif
            // 复制 PCL 个性化内容
            if (includePCLCustom)
            {
                if (Directory.Exists(Path.Combine(ModBase.exePath, "PCL", "Pictures")))
                    ModBase.CopyDirectory(Path.Combine(ModBase.exePath, "PCL", "Pictures"), Path.Combine(cacheFolder, "PCL", "Pictures"));
                if (Directory.Exists(Path.Combine(ModBase.exePath, "PCL", "Musics")))
                    ModBase.CopyDirectory(Path.Combine(ModBase.exePath, "PCL", "Musics"), Path.Combine(cacheFolder, "PCL", "Musics"));
                if (Directory.Exists(Path.Combine(ModBase.exePath, "PCL", "Help")))
                    ModBase.CopyDirectory(Path.Combine(ModBase.exePath, "PCL", "Help"), Path.Combine(cacheFolder, "PCL", "Help"));
                if (File.Exists(Path.Combine(ModBase.exePath, "PCL", "Custom.xaml")))
                    ModBase.CopyFile(Path.Combine(ModBase.exePath, "PCL", "Custom.xaml"), Path.Combine(cacheFolder, "PCL", "Custom.xaml"));
                if (File.Exists(Path.Combine(ModBase.exePath, "PCL", "Setup.ini")))
                    ModBase.CopyFile(Path.Combine(ModBase.exePath, "PCL", "Setup.ini"), Path.Combine(cacheFolder, "PCL", "Setup.ini"));
                if (File.Exists(Path.Combine(ModBase.exePath, "PCL", "hints.txt")))
                    ModBase.CopyFile(Path.Combine(ModBase.exePath, "PCL", "hints.txt"), Path.Combine(cacheFolder, "PCL", "hints.txt"));
                if (File.Exists(Path.Combine(ModBase.exePath, "PCL", "Logo.png")))
                    ModBase.CopyFile(Path.Combine(ModBase.exePath, "PCL", "Logo.png"), Path.Combine(cacheFolder, "PCL", "Logo.png"));
            }
        })
        {
            ProgressWeight = 5d
        });

        #endregion

        #region 联网检查

        loaders.Add(
            new ModLoader.LoaderTask<List<ModLocalComp.LocalCompFile>,
                Dictionary<ModLocalComp.LocalCompFile, List<string>>>("联网获取文件信息", loader =>
            {
                loader.output = new Dictionary<ModLocalComp.LocalCompFile, List<string>>();
                if (!checkHostedAssets)
                {
                    ModBase.Log("[Export] 要求跳过联网获取步骤");
                    return;
                }

                if (!loader.input.Any())
                {
                    ModBase.Log("[Export] 没有需要联网检查的文件，跳过联网获取步骤");
                    return;
                }

                // 分平台获取下载地址
                var endedThreadCount = 0;
                var failedExceptions = new List<Exception>();

                // 从 Modrinth 获取信息
                // 查找对应的文件
                // 写入下载地址
                ModBase.RunInNewThread(() =>
                {
                    try
                    {
                        var modrinthHashes = loader.input.Select(m => m.ModrinthHash);
                        var modrinthRaw = (JsonObject)ModBase.GetJson(ModDownload.DlModRequest(
                            "https://api.modrinth.com/v2/version_files", "POST",
                            $"{{\"hashes\": [\"{modrinthHashes.Join("\",\"")}\"], \"algorithm\": \"sha1\"}}",
                            "application/json"));
                        foreach (var ModFile in loader.input)
                        {
                            if (!modrinthRaw.ContainsKey(ModFile.ModrinthHash)) continue;
                            if ((string)modrinthRaw[ModFile.ModrinthHash]?["files"]?[0]["hashes"]?["sha1"] !=
                                ModFile.ModrinthHash) continue;
                            loader.output.AddToList(ModFile,
                                (string)modrinthRaw[ModFile.ModrinthHash]["files"][0]["url"]);
                        }

                        ModBase.Log($"[Export] 从 Modrinth 获取到 {modrinthRaw.Count} 个本地资源项的对应信息");
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "从 Modrinth 获取本地 Mod 信息失败");
                        failedExceptions.Add(ex);
                    }
                    finally
                    {
                        endedThreadCount += 1;
                        loader.Progress += 0.45d;
                    }
                }, "Modrinth - " + loaderName);

                // 从 CurseForge 获取信息
                // 查找对应的文件
                // 写入下载地址
                ModBase.RunInNewThread(() =>
                {
                    try
                    {
                        if (modrinthUploadMode) return;
                        var curseForgeHashes = loader.input.Select(m => m.CurseForgeHash);
                        var curseForgeRaw = (JsonNode)((JsonObject)ModBase.GetJson(
                            ModDownload.DlModRequest("https://api.curseforge.com/v1/fingerprints/432/", "POST",
                                $"{{\"fingerprints\": [{curseForgeHashes.Join(",")}]}}", "application/json")))["data"][
                            "exactMatches"];
                        foreach (JsonObject ResultJson in curseForgeRaw.AsArray())
                        {
                            if (!ResultJson.ContainsKey("file")) continue;
                            var file = (JsonObject)ResultJson["file"];
                            if (string.IsNullOrEmpty((string)file["downloadUrl"])) continue;
                            var modFile = loader.input.FirstOrDefault(m =>
                                m.CurseForgeHash == file["fileFingerprint"].ToObject<uint>());
                            if (modFile is null) continue;
                            loader.output.AddToList(modFile,
                                ModComp.CompFile.HandleCurseForgeDownloadUrls(file["downloadUrl"].ToString()));
                        }

                        ModBase.Log($"[Export] 从 CurseForge 获取到 {curseForgeRaw.AsArray().Count} 个本地资源项的对应信息");
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "从 CurseForge 获取本地 Mod 信息失败");
                        failedExceptions.Add(ex);
                    }
                    finally
                    {
                        endedThreadCount += 1;
                        loader.Progress += 0.45d;
                    }
                }, "CurseForge - " + loaderName); // Modrinth 上传模式下，不能从 CurseForge 获取信息

                // 等待线程结束
                while (endedThreadCount != 2)
                {
                    if (loader.IsAborted)
                        return;
                    Thread.Sleep(10);
                }

                // 若失败，确认是否继续
                if (failedExceptions.Count == 1)
                {
                    if (ModMain.MyMsgBox(
                            Lang.Text("Instance.Export.NetCheckPartialFailed.Message"),
                            Lang.Text("Instance.Export.NetCheckPartialFailed.Title"), Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel")) == 2)
                        throw failedExceptions.First();
                }
                else if (failedExceptions.Count > 1)
                {
                    if (ModMain.MyMsgBox(
                            Lang.Text("Instance.Export.NetCheckAllFailed.Message"),
                            Lang.Text("Instance.Export.NetCheckAllFailed.Title"), Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel")) == 2)
                        throw failedExceptions.First();
                }
            })
            {
                show = checkHostedAssets,
                ProgressWeight = checkHostedAssets ? 2d : 0.01d
            });

        #endregion

        #region 生成压缩包

        loaders.Add(new ModLoader.LoaderTask<Dictionary<ModLocalComp.LocalCompFile, List<string>>, int>("生成压缩包",
            loader =>
            {
                // 整理文件列表
                var files = new JsonArray();
                foreach (var Pair in loader.input)
                {
                    var modFile = Pair.Key;
                    files.Add(new JsonObject
                    {
                        { "path", Path.GetRelativePath(overridesFolder, modFile.path).Replace(@"\", "/") },
                        {
                            "hashes",
                            new JsonObject
                            {
                                { "sha1", modFile.ModrinthHash }, { "sha512", ModBase.GetFileSHA512(modFile.path) }
                            }
                        },
                        { "downloads", new JsonArray(Pair.Value.OrderByDescending(u => u.Contains("modrinth.com")).Select(s => (JsonNode)s).ToArray()) },
                        { "fileSize", new FileInfo(modFile.path).Length }
                    });
                    File.Delete(modFile.path);
                }

                loader.Progress = 0.2d;
                // 导出最终 JSON 文件
                var dependencies = new JsonObject { { "minecraft", mcInstance.Info.VanillaName } };
                if (mcInstance.Info.HasForge)
                    dependencies.Add("forge", mcInstance.Info.Forge);
                if (mcInstance.Info.HasFabric)
                    dependencies.Add("fabric-loader", mcInstance.Info.Fabric);
                if (mcInstance.Info.HasNeoForge)
                    dependencies.Add("neoforge", mcInstance.Info.NeoForge);
                var resultJson = new JsonObject
                {
                    { "game", "minecraft" }, { "formatVersion", 1 }, { "versionId", packVersion }, { "name", packName },
                    { "summary", mcInstance.Desc }, { "files", files }, { "dependencies", dependencies }
                };
                File.WriteAllText(Path.Combine(cacheFolder, "modpack", "modrinth.index.json"),
                    resultJson.ToJsonString(new JsonSerializerOptions(JsonCompat.SerializerOptions) { WriteIndented = true }));
                // 打包
                Directory.CreateDirectory(ModBase.GetPathFromFullPath(packPath));
                if (File.Exists(packPath))
                    File.Delete(packPath);
                if (includePCL)
                {
                    // 首次压缩整合包
                    ZipFile.CreateFromDirectory(Path.Combine(cacheFolder, "modpack"), Path.Combine(cacheFolder, "modpack.mrpack"));
                    loader.Progress = 0.5d;
                    Directory.Delete(Path.Combine(cacheFolder, "modpack"), true);
                    loader.Progress = 0.6d;
                    // 二次压缩整合包
                    ZipFile.CreateFromDirectory(cacheFolder, packPath);
                    loader.Progress = 0.9d;
                }
                else
                {
                    // 直接压缩整合包
                    ZipFile.CreateFromDirectory(Path.Combine(cacheFolder, "modpack"), packPath);
                    loader.Progress = 0.8d;
                }

                Directory.Delete(cacheFolder, true);
                ModBase.OpenExplorer(packPath);
            })
        {
            ProgressWeight = 6d
        });

        #endregion

        // 启动
        var mainLoader = new ModLoader.LoaderCombo<string>(loaderName, loaders)
            { OnStateChanged = ModDownloadLib.LoaderStateChangedHintOnly };
        mainLoader.Start();
        ModLoader.LoaderTaskbarAdd(mainLoader);
        ModMain.frmMain.BtnExtraDownload.ShowRefresh();
        ModMain.frmMain.BtnExtraDownload.Ribble();
        ModMain.frmMain.PageChange(FormMain.PageType.TaskManager);
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
