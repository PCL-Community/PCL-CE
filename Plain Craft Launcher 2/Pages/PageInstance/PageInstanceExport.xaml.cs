using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

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

    public string Rules { get; set; } = string.Empty;

    /// <summary>
    ///     如果 Rules 为空，则根据 ShowRules 的内容判断是否应该显示这个复选框。
    ///     如果 ShowRules 也为空，则始终显示。
    /// </summary>
    public string ShowRules { get; set; } = string.Empty;

    public bool DefaultChecked { get; set; }
    public bool RequireModLoader { get; set; }
    public bool RequireOptiFine { get; set; }
    public bool RequireModLoaderOrOptiFine { get; set; }
}

public partial class PageInstanceExport : IRefreshable, IPageView
{
    private readonly PageInstanceExportViewModel _vm = new();
    private bool _refreshed;

    public PageInstanceExport()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += PageInstanceExport_Loaded;
        Unloaded += (_, _) => _refreshed = false;
    }

    public PageInstanceExportViewModel ViewModel => _vm;

    void IRefreshable.Refresh()
    {
        RefreshAll();
    }

    public void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    public void OnNavigatedFrom()
    {
    }

    private void PageInstanceExport_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshOnNavigate();
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        Animations.AniBeginControl();
        if (_vm.CurrentVersion != PageInstanceLeft.McInstance.PathInstance)
            RefreshAll(); // 切换到了另一个实例，重置页面
        Animations.AniEndControl();
    }

    public void RefreshAll()
    {
        Common.Log("[Export] 刷新导出页面");
        _vm.RefreshHeader(PageInstanceLeft.McInstance);
        CheckAdvancedInclude.Checked = false;
        CheckAdvancedModrinth.Checked = false;
        GetExportOption(CheckOptionsBasic).Description = PageInstanceLeft.McInstance.GetDefaultDescription();
        ResetConfigOverrides();
        ReloadAllSubOptions();
        RefreshAllOptionsUi();
        PanBack.ScrollToHome();
    }

    // 自动填写整合包名称
    private void TextExportName_GotFocus(object sender, RoutedEventArgs routedEventArgs)
    {
        if (string.IsNullOrEmpty(_vm.ExportName))
        {
            _vm.ExportName = _vm.ExportNameHint;
            TextExportName.SelectionStart = TextExportName.Text.Length;
        }
    }

    // 勾选 Modrinth 上传模式时，禁止打包 PCL
    private void CheckAdvancedModrinth_Change(object sender, bool user)
    {
        if (CheckAdvancedModrinth.Checked == true)
            CheckOptionsPcl.Checked = false;
        _vm.IncludePclEnabled = CheckAdvancedModrinth.Checked != true;
    }

    // 勾选"其他文件夹"时，同步勾选/取消所有子选项
    private void CheckOptionsOtherFolders_Change(object sender, bool user)
    {
        if (!user) return;
        foreach (var child in PanOptionsOtherFolders.Children)
            if (child is MyCheckBox childBox)
                childBox.Checked = CheckOptionsOtherFolders.Checked;
    }

    // 勾选打包资源文件时，禁止开启 Modrinth 上传模式
    private void CheckAdvancedInclude_Change(object sender, bool user)
    {
        if (CheckAdvancedInclude.Checked == true)
            CheckAdvancedModrinth.Checked = false;
        _vm.ModrinthEnabled = CheckAdvancedInclude.Checked != true;
    }

    #region 子选项

    private readonly string[] _subOptionBlackList = ["Quark Programmer Art.zip", "+ EuphoriaPatches_"];

    /// <summary>
    ///     动态生成子文件夹下的选项，例如资源包、存档等。
    /// </summary>
    private void ReloadAllSubOptions()
    {
        ReloadSubOptions(PanOptionsResourcePacks, true, true, "resourcepacks", "texturepacks");
        ReloadSubOptions(PanOptionsSaves, false, true, "saves");
        ReloadSubOptions(PanOptionsShaderPacks, true, true, "shaderpacks");
        ReloadOtherFolders();
    }

    /// <summary>
    ///     扫描实例根目录下未被已有选项覆盖的文件夹，生成独立的复选框。
    /// </summary>
    private void ReloadOtherFolders()
    {
        PanOptionsOtherFolders.Children.Clear();

        var pathIndie = PageInstanceLeft.McInstance.PathIndie;
        var rootDir = new DirectoryInfo(pathIndie);
        if (!rootDir.Exists)
        {
            _vm.RefreshOtherFolders(0);
            return;
        }

        var coveredFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // 模组
            "mods", "coremods", "lib",
            // 整合包重要数据
            "addons", "multiblocked", "modpack-update-checker", "global_packs",
            "global_resource_packs", "global_data_packs", "optional_data_packs", "maps",
            "mods-resourcepacks", "matmos", "resource_assorts",
            "patchouli_books", "datapacks",
            "openloader", "worldshape", "resources", "scripts", "structures",
            "fontfiles", "oresources", "packmenu", "craftpresence", "pointblanks",
            // 模组设置
            "config", "defaultconfigs", "journeymap", "local", "essential", "gg.essential.mod",
            "CustomSkinLoader",
            // 地图
            "xaero", "XaeroWaypoints", "XaeroWorldMap",
            // 资源包
            "resourcepacks", "texturepacks",
            // 光影
            "shaderpacks",
            // 截图 / 结构 / 录像
            "screenshots", "schematics",
            "replay_recordings", "replay_videos",
            // 存档 / 设置文件夹
            "saves", "configureddefaults",
            // 始终跳过（大量文件或无用缓存）
            "assets", "versions", "libraries", "structureCacheV1",
            ".fabric", ".git", "avatar-cache", "cosmetic-cache",
            // PCL 单独处理
            "PCL",
        };

        var coveredPrefixes = new[] { "kubejs", "template" };
        var coveredSuffixes = new[] { "-natives" };

        foreach (var subDir in rootDir.EnumerateDirectories())
        {
            if (coveredFolders.Contains(subDir.Name))
                continue;
            if (coveredPrefixes.Any(p => subDir.Name.StartsWithF(p)))
                continue;
            if (coveredSuffixes.Any(s => subDir.Name.EndsWithF(s, true)))
                continue;

            PanOptionsOtherFolders.Children.Add(new MyCheckBox
            {
                Tag = new ExportOption
                {
                    Title = subDir.Name,
                    DefaultChecked = false,
                    Rules = Common.EscapeLikePattern($"{subDir.Name}/")
                }
            });
        }

        _vm.RefreshOtherFolders(PanOptionsOtherFolders.Children.Count);
    }

    private void ReloadSubOptions(StackPanel panel, bool acceptCompressedFile, bool acceptFolder,
        params string[] folders)
    {
        panel.Children.Clear();
        foreach (var folder in folders)
        {
            var targetFolder = new DirectoryInfo(PageInstanceLeft.McInstance.PathIndie + folder);
            if (!targetFolder.Exists)
                continue;
            // 查找文件夹下的对应项
            if (acceptCompressedFile)
                foreach (var file in targetFolder.EnumerateFiles("*.zip").Concat(targetFolder.EnumerateFiles("*.rar")))
                {
                    if (_subOptionBlackList.Any(b => file.Name.ContainsF(b)))
                        continue;
                    panel.Children.Add(new MyCheckBox
                    {
                        Tag = new ExportOption
                        {
                            Title = file.Name,
                            DefaultChecked = true,
                            Rules = Common.EscapeLikePattern($"{folder}/{file.Name}")
                        }
                    });
                    if (folder == "shaderpacks") // 处理光影包的配置文件
                    {
                        var shaderConfig = new FileInfo(Path.Combine(file.Directory.FullName,
                            $"{file.Name}.txt"));
                        if (shaderConfig.Exists)
                            panel.Children.Add(new MyCheckBox
                            {
                                Margin = new Thickness(30, 0, 0, 0),
                                Tag = new ExportOption
                                {
                                    Title = $"{shaderConfig.Name}",
                                    DefaultChecked = true,
                                    Description = Lang.Text("Instance.Export.Config.ShaderConfigSuffix"),
                                    Rules = Common.EscapeLikePattern($"{folder}/{shaderConfig.Name}")
                                }
                            });
                    }
                }

            if (acceptFolder)
                foreach (var subFolder in targetFolder.EnumerateDirectories().OrderByDescending(f => f.LastWriteTime))
                {
                    if (_subOptionBlackList.Any(b => subFolder.Name.ContainsF(b)))
                        continue;
                    if (!subFolder.EnumerateFileSystemInfos().Any())
                        continue;
                    var newCheckBox = new MyCheckBox
                    {
                        Tag = new ExportOption
                        {
                            Title = subFolder.Name,
                            DefaultChecked = true,
                            Rules = Common.EscapeLikePattern($"{folder}/{subFolder.Name}/")
                        }
                    };
                    if (ReferenceEquals(panel, PanOptionsSaves))
                        GetExportOption(newCheckBox).Description =
                            Lang.Date(subFolder.LastWriteTime, "g");
                    panel.Children.Add(newCheckBox);
                    if (folder == "shaderpacks") // 处理文件夹形式光影包的配置文件
                    {
                        var shaderConfig = new FileInfo(Path.Combine(targetFolder.FullName,
                            $"{subFolder.Name}.txt"));
                        if (shaderConfig.Exists)
                            panel.Children.Add(new MyCheckBox
                            {
                                Margin = new Thickness(30, 0, 0, 0),
                                Tag = new ExportOption
                                {
                                    Title = $"{shaderConfig.Name}",
                                    DefaultChecked = true,
                                    Description = Lang.Text("Instance.Export.Config.ShaderConfigSuffix"),
                                    Rules = Common.EscapeLikePattern($"{folder}/{shaderConfig.Name}")
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
    private void RefreshAllOptionsUi()
    {
        // 预先归纳所有至多二级的文件/文件夹
        var allEntries = new List<string>();

        bool IsValidDirectory(DirectoryInfo folder)
        {
            try
            {
                return folder.Exists && folder.EnumerateFileSystemInfos()
                    .Any(i => !_subOptionBlackList.Any(b => i.Name.ContainsF(b)));
            }
            catch
            {
                return false;
            }
        }

        ; // 检查文件夹不为空
        // 一般是由于无法访问，或是一个指向已不存在的文件夹的链接（例如使用 mklink 创造的 resource 文件夹链接）
        var pathInfo = new DirectoryInfo(PageInstanceLeft.McInstance.PathIndie);
        allEntries.AddRange(pathInfo.EnumerateFiles().Select(f => f.Name));
        foreach (var subFolder in pathInfo.EnumerateDirectories().Where(IsValidDirectory))
        {
            allEntries.Add($@"{subFolder.Name}\");
            allEntries.AddRange(subFolder.EnumerateFiles().Select(f => $@"{subFolder.Name}\{f.Name}"));
            allEntries.AddRange(subFolder.EnumerateDirectories().Where(IsValidDirectory)
                .Select(d => $@"{subFolder.Name}\{d.Name}\"));
        }

        Common.Log($"[Export] 共发现 {allEntries.Count} 个可行的二级文件/文件夹");

        // 确认选项是否应该被显示
        bool IsVisible(ExportOption targetOption)
        {
            // 检查需要 OptiFine 或 Mod 加载器
            if (targetOption.RequireOptiFine && !PageInstanceLeft.McInstance.Info.HasOptiFine)
                return false;
            if (targetOption.RequireModLoader && !PageInstanceLeft.McInstance.Modable)
                return false;
            if (targetOption.RequireModLoaderOrOptiFine && !PageInstanceLeft.McInstance.Info.HasOptiFine &&
                !PageInstanceLeft.McInstance.Modable)
                return false;
            // 粗略检查是否可能有符合规则的文件/文件夹
            return PageInstanceExportViewModel.StandardizeLines(targetOption.Rules.Split('|'), true).Any(rule =>
            {
                if (rule.StartsWithF("!"))
                    return false; // 只看正向规则
                // 检查前两级
                try
                {
                    if (allEntries.Any(entry => PageInstanceExportViewModel.LikeString(entry, rule)))
                        return true;
                }
                catch (Exception ex)
                {
                    Common.Log(
                        ex,
                        $"错误的规则：{rule}",
                        Common.LogLevel.Hint,
                        userSummary: Lang.Text("Instance.Export.Error.OperationFailed"));
                    return false;
                }

                // 粗略检查所有级
                rule = rule.Trim("*?".ToCharArray());
                if (rule.Split(['\\'], StringSplitOptions.RemoveEmptyEntries).Count() >= 3)
                {
                    if (rule.EndsWithF(@"\"))
                        return IsValidDirectory(new DirectoryInfo(PageInstanceLeft.McInstance.PathIndie + rule)); // 文件夹有效

                    return File.Exists(PageInstanceLeft.McInstance.PathIndie + rule);
                    // 文件有效
                }

                return false;
            });
        }

        // 逐个检查选项
        foreach (var checkBox in GetAllOptions(true))
        {
            var targetOption = GetExportOption(checkBox);
            // 名称与简介
            checkBox.Inlines.Clear();
            checkBox.Inlines.Add(new Run(targetOption.Title));
            if (!string.IsNullOrEmpty(targetOption.Description))
                checkBox.Inlines.Add(new Run("   " + targetOption.Description) { Foreground = ThemeManager.colorGray5 });
            // 可见性、默认勾选
            if (string.IsNullOrEmpty(targetOption.Rules) && string.IsNullOrEmpty(targetOption.ShowRules))
            {
                checkBox.Visibility = Visibility.Visible;
                checkBox.Checked = targetOption.DefaultChecked;
            }
            else
            {
                var pass = IsVisible(targetOption);
                checkBox.Visibility = pass ? Visibility.Visible : Visibility.Collapsed;
                checkBox.Checked = targetOption.DefaultChecked && pass;
            }
        }
    }

    /// <summary>
    ///     获取所有可作为选项的 CheckBox。
    /// </summary>
    private IEnumerable<MyCheckBox> GetAllOptions(bool includeHidden)
    {
        foreach (var element in PanOptions.Children)
        {
            if (!includeHidden && ((UIElement)element).Visibility != Visibility.Visible)
            {
                continue;
            }

            switch (element)
            {
                case MyCheckBox box:
                    yield return box;
                    break;
                case StackPanel panel:
                    {
                        foreach (var subElement in panel.Children)
                        {
                            if (!includeHidden && ((UIElement)subElement).Visibility != Visibility.Visible)
                                continue;
                            if (subElement is MyCheckBox checkBox)
                                yield return checkBox;
                        }

                        break;
                    }
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

    private const string Sperator = "==============================================================";

    // ================ 导出内容段 ================

    /// <summary>
    ///     从配置文件中读取的规则。
    ///     如果不为 Nothing，则会覆写当前勾选的规则并禁用对应 UI。
    /// </summary>
    private List<string> RulesOverrides
    {
        get;
        set
        {
            field = value;
            _vm.HasOverrides = value.Count != 0;
            if (!_vm.HasOverrides)
            {
                CardOptions.Inlines.Clear();
                CardOptions.Inlines.Add(new Run(Lang.Text("Instance.Export.OptionListTitle"))
                { FontWeight = FontWeights.Bold });
            }
            else
            {
                CardOptions.Inlines.Clear();
                CardOptions.Inlines.Add(new Run(Lang.Text("Instance.Export.OptionListTitle") + ":    ")
                { FontWeight = FontWeights.Bold });
                CardOptions.Inlines.Add(new Run(Lang.Text("Instance.Export.OptionList.FromConfig"))
                { FontWeight = FontWeights.Normal });
            }
        }
    } = [];

    /// <summary>
    ///     获取当前实际生效的所有规则。
    /// </summary>
    private IEnumerable<string> GetAllRules()
    {
        // i think is will never be null
        // but keep this code is fine
        if (RulesOverrides.Count != 0)
        {
            // 返回覆盖的列表
            foreach (var rule in RulesOverrides)
                yield return rule;
        }
        else
        {
            // 从当前勾选的所有选项中获取所有规则行
            yield return "";
            yield return "# " + Lang.Text("Instance.Export.Config.Comment.ModifyRules");
            yield return "# " + Lang.Text("Instance.Export.Config.Comment.ReverseMatch");
            yield return "";
            foreach (var checkBox in GetAllOptions(false))
            {
                if (checkBox.Checked == false)
                    continue;
                var targetOption = GetExportOption(checkBox);
                if (string.IsNullOrEmpty(targetOption.Rules))
                    continue;
                yield return $"# {targetOption.Title}";
                foreach (var rule in targetOption.Rules.Split('|'))
                    yield return rule;
                yield return "";
            }

            yield return "# " + Lang.Text("Instance.Export.Config.Comment.ExcludedFiles");
            yield return "!*.log";
            yield return "!*.dat_old";
            yield return "!*.BakaCoreInfo";
            yield return "!hmclversion.cfg";
            yield return "!log4j2.xml";
            yield return "";
        }
    }

    // ================ 追加内容段 ================

    private List<string> _extraFiles = [];

    /// <summary>
    ///     获取当前实际生效的追加内容。
    /// </summary>
    private IEnumerable<string> GetExtraFileLines()
    {
        if (_extraFiles.Count != 0)
        {
            // 返回覆盖的列表
            foreach (var file in _extraFiles)
                yield return file;
        }
        else
        {
            // 从当前勾选的所有选项中获取所有规则行
            yield return "";
            yield return "# " + Lang.Text("Instance.Export.Config.Comment.ExtraFiles");
            yield return "# " + Lang.Text("Instance.Export.Config.Comment.ExtraFiles2");
            yield return "";
        }
    }

    // ================ 重置 ================

    /// <summary>
    ///     重置配置文件所带来的影响。
    /// </summary>
    private void ResetConfigOverrides()
    {
        RulesOverrides = [];
        _configPackPath = string.Empty;
        _extraFiles = [];
        PanBack.ScrollToHome();
    }

    private void CardOptions_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (RulesOverrides.Count == 0)
            return;
        ResetConfigOverrides();
    }

    // ================ 保存 / 读取 ================

    // 保存配置文件
    private void ExportConfig(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var configPath = SystemDialogs.SelectSaveFile(Lang.Text("Instance.Export.SelectFileLocation"), "export_config.txt", Lang.Text("Instance.Export.Config.FileFilter"),
                (string?)States.System.ExportConfigPath);
            if (string.IsNullOrEmpty(configPath))
                return;
            States.System.ExportConfigPath = configPath;
            var configLines = new List<string>
            {
                // ini 段
                "Name:" + _vm.ExportName,
                "Version:" + _vm.ExportVersion,
                "",
                "# " + Lang.Text("Instance.Export.Config.Comment.IncludeLauncher"),
                "IncludeLauncher:" + CheckOptionsPcl.Checked,
                "",
                "# " + Lang.Text("Instance.Export.Config.Comment.IncludeLauncherCustom"),
                "IncludeLauncherCustom:" + CheckOptionsPclCustom.Checked,
                "",
                "# " + Lang.Text("Instance.Export.Config.Comment.BundleFiles"),
                "# " + Lang.Text("Instance.Export.Config.Comment.BundleFiles2"),
                "# " + Lang.Text("Instance.Export.Config.Comment.BundleFiles3"),
                "DontCheckHostedAssets:" + CheckAdvancedInclude.Checked,
                "",
                "# " + Lang.Text("Instance.Export.Config.Comment.Modrinth"),
                "# " + Lang.Text("Instance.Export.Config.Comment.Modrinth2"),
                "# " + Lang.Text("Instance.Export.Config.Comment.Modrinth3"),
                "ModrinthUploadMode:" + CheckAdvancedModrinth.Checked,
                "",
                "# " + Lang.Text("Instance.Export.Config.Comment.PackPath"),
                "# " + Lang.Text("Instance.Export.Config.Comment.PackPath2"),
                "# " + Lang.Text("Instance.Export.Config.Comment.PackPath3"),
                "PackPath:" + (_configPackPath ?? ""),
                "",
                // 导出内容段
                Sperator
            };
            configLines.AddRange(GetAllRules());
            // 追加内容段
            configLines.Add(Sperator);
            configLines.AddRange(GetExtraFileLines());
            // 结束
            Common.WriteFile(configPath, configLines.Join("\r\n"));
            HintService.Show(Hints.Success(Lang.Text("Instance.Export.SaveSuccess", configPath)));
            Common.OpenExplorer(configPath);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "保存配置失败",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Export.Error.OperationFailed"));
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

            var fileContent = Common.ReadFile(configPath);
            var segments = fileContent.Split(Sperator);

            if (segments.Length == 0)
            {
                HintService.Show(Hints.Error(Lang.Text("Instance.Export.Config.Invalid")));
                return;
            }

            // === 解析INI段 ===
            var ini = new Dictionary<string, string>();
            foreach (var lineRaw in segments[0].Split("\r\n".ToCharArray()))
            {
                var line = lineRaw;
                line = line.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWithF("#") || line.StartsWithF("="))
                    continue;
                var index = line.IndexOfF(":");
                if (index > 0) ini[line.Substring(0, index)] = line.Substring(index + 1);
            }

            // 赋值到界面控件
            _vm.ExportName = ini.GetOrDefault("Name", "");
            _vm.ExportVersion = ini.GetOrDefault("Version", "");
            CheckOptionsPcl.Checked =
                Convert.ToBoolean(ini.GetOrDefault("IncludeLauncher", false.ToString()));
            CheckOptionsPclCustom.Checked =
                Convert.ToBoolean(ini.GetOrDefault("IncludeLauncherCustom", true.ToString()));
            CheckAdvancedModrinth.Checked =
                Convert.ToBoolean(ini.GetOrDefault("ModrinthUploadMode", false.ToString()));
            CheckAdvancedInclude.Checked =
                Convert.ToBoolean(ini.GetOrDefault("DontCheckHostedAssets", false.ToString()));
            _configPackPath = ini.GetOrDefault("PackPath");

            // === 解析导出内容段 ===
            RulesOverrides = segments[1].Replace('\r', '\n')
                .Replace("\n" + "\n", "\n").Split('\n').ToList();

            // === 解析追加内容段 ===
            if (segments.Length > 2)
                _extraFiles = segments[2].Replace('\r', '\n')
                    .Replace("\n\n", "\n").Split('\n').ToList();
            else
                _extraFiles = [];

            // 提示成功
            HintService.Show(Hints.Success(Lang.Text("Instance.Export.ReadSuccess", configPath)));
        }

        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"读取配置文件失败：{configPath}",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Export.Error.OperationFailed"));
        }
    }

    #endregion

    // 读取配置文件
    private void ImportConfig(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var configPath = SystemDialogs.SelectFile(Lang.Text("Instance.Export.Config.FileFilter"), Lang.Text("Instance.Export.SelectConfigFile"),
                (string?)States.System.ExportConfigPath);
            if (string.IsNullOrEmpty(configPath))
                return;

            // 调用核心读取逻辑
            ReadConfigFile(configPath);
        }

        catch (Exception ex)
        {
            Common.Log(
                ex,
                "选择配置文件失败",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Export.Error.OperationFailed"));
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
            var files = (string[]?)e.Data.GetData(DataFormats.FileDrop) ?? [];

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
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
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
    private string _configPackPath = string.Empty;

    /// <summary>
    ///     开始导出。
    /// </summary>
    private async void StartExport(object sender, MouseButtonEventArgs e)
    {
        var ctl = sender as Control;
        ctl?.IsEnabled = false;
        try
        {
            await StartExportAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "导出整合包失败",
                Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Export.Error.OperationFailed"));
        }
        finally
        {
            ctl?.IsEnabled = true;
        }
    }

    private async Task StartExportAsync()
    {
        var packName = string.IsNullOrEmpty(_vm.ExportName) ? _vm.ExportNameHint : _vm.ExportName;
        var packVersion = string.IsNullOrEmpty(_vm.ExportVersion) ? "1.0.0" : _vm.ExportVersion;

        // 重复任务检查
        var loaderName = Lang.Text("Instance.Export.ExportTask.Prefix") + packName;
        foreach (var ongoingLoader in Loading.loaderTaskbar)
        {
            if ((ongoingLoader.name ?? "") != (loaderName ?? ""))
                continue;
            Launcher.frmMain.PageChange(FormMain.PageType.TaskManager);
            return;
        }

        // 确认导出位置
        string? packPath = null;
        if (!string.IsNullOrWhiteSpace(_configPackPath) && !_configPackPath.EndsWithF(@"\") &&
            !_configPackPath.EndsWithF("/"))
            try
            {
                Directory.CreateDirectory(Common.GetPathFromFullPath(_configPackPath));
                packPath = _configPackPath;
                Common.Log($"[Export] 使用配置文件中指定的导出路径：{_configPackPath}");
            }
            catch (Exception ex)
            {
                Common.Log(ex, $"无法使用配置文件中指定的导出路径（{_configPackPath}）");
                if ((await DialogService.Instance.ShowAsync(new TextDialogRequest
                {
                    Title = Lang.Text("Instance.Export.PackPathInvalid.Title"),
                    Content = Lang.Text("Instance.Export.PackPathInvalid.WithDetail", _configPackPath, ex.ToString()),
                    Buttons = DialogButtons.OkCancel,
                    ForceWait = false
                }).ConfigureAwait(false)).Button == 2)
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
                packName + (string.IsNullOrEmpty(_vm.ExportVersion) ? "" : " " + _vm.ExportVersion),
                extensions.Join("|"));
            Common.Log($"[Export] 手动指定的导出路径：{packPath}");
        }

        if (string.IsNullOrEmpty(packPath))
            return;

        // 缓存所需参数
        var cacheFolder = Launcher.RequestTaskTempFolder();
        var overridesFolder = Path.Combine(cacheFolder, "modpack", "overrides");
        var mcInstance = PageInstanceLeft.McInstance;
        var pathIndie = mcInstance.PathIndie;
        var checkHostedAssets = (bool)(!CheckAdvancedInclude.Checked)!;
        var modrinthUploadMode = (bool)CheckAdvancedModrinth.Checked;
        var includePcl = (bool)CheckOptionsPcl.Checked!;
        var includePclCustom = (bool)(includePcl ? CheckOptionsPclCustom.Checked : (bool?)false)!;
        var allRules = PageInstanceExportViewModel.StandardizeLines(GetAllRules(), true).ToList();
        var allExtraFiles = PageInstanceExportViewModel.StandardizeLines(GetExtraFileLines(), false).ToList();
        Common.Log($"[Export] 准备导出整合包，共有 {allRules.Count} 条规则，{allExtraFiles.Count} 条追加内容行");

        // 构造步骤加载器
        var loaders = new List<Loading.LoaderBase>();

        #region 准备 PCL 文件

#if !RELEASE
        if (includePcl)
            loaders.Add(new Loading.LoaderTask<int, int>(Lang.Text("Instance.Export.Task.DownloadPclRelease"),
                loader =>
                {
                    UpdateManager.DownloadLatestPcl(loader);
                    Common.CopyFile(Path.Combine(Common.pathTemp, "CE-Latest.exe"),
                        Path.Combine(cacheFolder, "Plain Craft Launcher.exe"));
                })
            {
                ProgressWeight = 0.5d,
                block = false
            });
#endif

        #endregion

        #region 复制文件

        loaders.Add(new Loading.LoaderTask<int, List<LocalComp.LocalCompFile>>(
            Lang.Text("Instance.Export.Task.CopyContent"), loader =>
            {
                loader.output = [];
                // 复制实例文件
                var progress = 0;

                void SearchFolder(DirectoryInfo folder)
                {
                    // 文件夹：进一步搜索
                    foreach (var subFolder in folder.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
                    {
                        // 跳过部分又没用文件又多的文件夹，加快搜索
                        if ((folder.FullName ?? "") == (pathIndie ?? "") && new[] { "assets", "versions", "libraries" }.Contains(subFolder.Name)) continue;
                        if (new[] { "structureCacheV1", ".fabric", ".git", "avatar-cache", "cosmetic-cache" }.Contains(subFolder.Name)) continue;
                        SearchFolder(subFolder);
                    }

                    // 文件：检查规则并复制
                    foreach (var entry in folder.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
                    {
                        var relativePath = entry.FullName.AfterFirst(pathIndie ?? string.Empty);
                        // 检查规则
                        var shouldKeep = false;
                        foreach (var rule in allRules)
                        {
                            var revert = rule.StartsWith("!");
                            if (PageInstanceExportViewModel.LikeString(relativePath, rule.TrimStart('!'))) shouldKeep = !revert;
                        }

                        if (!shouldKeep) continue;
                        var targetPath = Path.Combine(overridesFolder, relativePath);
                        Common.CopyFile(entry.FullName, targetPath);
                        // 若为压缩包，考虑联网获取路径
                        if (checkHostedAssets &&
                            new[] { ".zip", ".rar", ".jar", ".disabled", ".old" }.Contains(entry.Extension.ToLower()) &&
                            new[] { "mods", "packs", "openloader", "resource" }.Any(relativePath.Contains))
                        {
                            var modFile = new LocalComp.LocalCompFile(targetPath);
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
                }

                SearchFolder(new DirectoryInfo(pathIndie));
                Common.Log($"[Export] 复制 overrides 文件完成，有 {loader.output.Count} 个文件需要联网检查");
                loader.Progress = 0.95d;
                // 复制追加内容到根目录
                var baseFolder = includePcl ? cacheFolder : Path.Combine(cacheFolder, "modpack");
                foreach (var line in allExtraFiles)
                    if (line.EndsWithF(@"\") || line.EndsWithF("/"))
                    {
                        if (Directory.Exists(line))
                            Common.CopyDirectory(line, Path.Combine(baseFolder, Common.GetFolderNameFromPath(line)) + @"\");
                        else
                            HintService.Show(Hints.Error(Lang.Text("Instance.Export.Config.FolderNotFound", line)));
                    }
                    else if (File.Exists(line))
                    {
                        Common.CopyFile(line, Path.Combine(baseFolder, Common.GetFileNameFromPath(line)));
                    }
                    else
                    {
                        HintService.Show(Hints.Error(Lang.Text("Instance.Export.Config.FileNotFound", line)));
                    }

                loader.Progress = 0.97d;
                // 复制 PCL 实例设置
                Common.CopyDirectory(Path.Combine(mcInstance.PathInstance, "PCL"), Path.Combine(overridesFolder, "PCL"));
#if RELEASE
                        // 复制 PCL 本体
                        if (includePCL) Common.CopyFile(Basics.ExecutablePath, Path.Combine(cacheFolder, Basics.ExecutableName));
#endif
                // 复制 PCL 个性化内容
                if (includePclCustom)
                {
                    if (Directory.Exists(Path.Combine(Common.ExePath, "PCL", "Pictures")))
                        Common.CopyDirectory(Path.Combine(Common.ExePath, "PCL", "Pictures"), Path.Combine(cacheFolder, "PCL", "Pictures"));
                    if (Directory.Exists(Path.Combine(Common.ExePath, "PCL", "Musics")))
                        Common.CopyDirectory(Path.Combine(Common.ExePath, "PCL", "Musics"), Path.Combine(cacheFolder, "PCL", "Musics"));
                    if (File.Exists(Path.Combine(Common.ExePath, "PCL", "Custom.xaml")))
                        Common.CopyFile(Path.Combine(Common.ExePath, "PCL", "Custom.xaml"), Path.Combine(cacheFolder, "PCL", "Custom.xaml"));
                    if (File.Exists(Path.Combine(Common.ExePath, "PCL", "Setup.ini")))
                        Common.CopyFile(Path.Combine(Common.ExePath, "PCL", "Setup.ini"), Path.Combine(cacheFolder, "PCL", "Setup.ini"));
                    if (File.Exists(Path.Combine(Common.ExePath, "PCL", "hints.txt")))
                        Common.CopyFile(Path.Combine(Common.ExePath, "PCL", "hints.txt"), Path.Combine(cacheFolder, "PCL", "hints.txt"));
                    if (File.Exists(Path.Combine(Common.ExePath, "PCL", "Logo.png")))
                        Common.CopyFile(Path.Combine(Common.ExePath, "PCL", "Logo.png"), Path.Combine(cacheFolder, "PCL", "Logo.png"));
                }
            })
        {
            ProgressWeight = 5d
        });

        #endregion

        #region 联网检查

        loaders.Add(
            new Loading.LoaderTask<List<LocalComp.LocalCompFile>,
                Dictionary<LocalComp.LocalCompFile, List<string>>>(Lang.Text("Instance.Export.Task.FetchFileInfo"),
                loader =>
                {
                    loader.output = [];
                    if (!checkHostedAssets)
                    {
                        Common.Log("[Export] 要求跳过联网获取步骤");
                        return;
                    }

                    if (!loader.input!.Any())
                    {
                        Common.Log("[Export] 没有需要联网检查的文件，跳过联网获取步骤");
                        return;
                    }

                    // 分平台获取下载地址
                    var endedThreadCount = 0;
                    var failedExceptions = new List<Exception>();

                    // 从 Modrinth 获取信息
                    // 查找对应的文件
                    // 写入下载地址
                    Common.RunInNewThread(() =>
                    {
                        try
                        {
                            var modrinthHashes = loader.input!.Select(m => m.ModrinthHash);
                            var modrinthRaw = (JsonObject)Common.GetJson(GameDownload.DlModRequest(
                                "https://api.modrinth.com/v2/version_files", "POST",
                                $"{{\"hashes\": [\"{modrinthHashes.Join("\",\"")}\"], \"algorithm\": \"sha1\"}}",
                                "application/json"));
                            foreach (var modFile in loader.input!)
                            {
                                if (!modrinthRaw.ContainsKey(modFile.ModrinthHash)) continue;
                                if ((string)modrinthRaw[modFile.ModrinthHash]?["files"]?[0]["hashes"]?["sha1"] !=
                                    modFile.ModrinthHash) continue;
                                loader.output!.AddToList(modFile,
                                    (string)modrinthRaw[modFile.ModrinthHash]!["files"]![0]!["url"]!);
                            }

                            Common.Log($"[Export] 从 Modrinth 获取到 {modrinthRaw.Count} 个本地资源项的对应信息");
                        }
                        catch (Exception ex)
                        {
                            Common.Log(ex, "从 Modrinth 获取本地 Mod 信息失败");
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
                    Common.RunInNewThread(() =>
                    {
                        try
                        {
                            if (modrinthUploadMode) return;
                            var curseForgeHashes = loader.input.Select(m => m.CurseForgeHash);
                            var curseForgeRaw = (JsonNode)((JsonObject)Common.GetJson(
                                GameDownload.DlModRequest("https://api.curseforge.com/v1/fingerprints/432/",
                                    "POST",
                                    $"{{\"fingerprints\": [{curseForgeHashes.Join(",")}]}}",
                                    "application/json")))
                                ["data"]["exactMatches"];
                            foreach (JsonObject resultJson in curseForgeRaw.AsArray())
                            {
                                if (!resultJson.ContainsKey("file")) continue;
                                var file = (JsonObject)resultJson["file"];
                                if (string.IsNullOrEmpty((string)file["downloadUrl"])) continue;
                                var modFile = loader.input.FirstOrDefault(m =>
                                    m.CurseForgeHash == file["fileFingerprint"].ToObject<uint>());
                                if (modFile is null) continue;
                                loader.output.AddToList(modFile,
                                    Comp.CompFile.HandleCurseForgeDownloadUrls(file["downloadUrl"].ToString()));
                            }

                            Common.Log($"[Export] 从 CurseForge 获取到 {curseForgeRaw.AsArray().Count} 个本地资源项的对应信息");
                        }
                        catch (Exception ex)
                        {
                            Common.Log(ex, "从 CurseForge 获取本地 Mod 信息失败");
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
                        if ((DialogService.Instance.Show(new TextDialogRequest
                        {
                            Title = Lang.Text("Instance.Export.NetCheckPartialFailed.Title"),
                            Content = Lang.Text("Instance.Export.NetCheckPartialFailed.Message"),
                            Buttons = DialogButtons.ContinueCancel,
                            ForceWait = true
                        }).Result?.Button ?? 0) == 2)
                            throw failedExceptions.First();
                    }
                    else if (failedExceptions.Count > 1)
                    {
                        if ((DialogService.Instance.Show(new TextDialogRequest
                        {
                            Title = Lang.Text("Instance.Export.NetCheckAllFailed.Title"),
                            Content = Lang.Text("Instance.Export.NetCheckAllFailed.Message"),
                            Buttons = DialogButtons.ContinueCancel,
                            ForceWait = true
                        }).Result?.Button ?? 0) == 2)
                            throw failedExceptions.First();
                    }
                })
            {
                show = checkHostedAssets,
                ProgressWeight = checkHostedAssets ? 2d : 0.01d
            });

        #endregion

        #region 生成压缩包

        loaders.Add(new Loading.LoaderTask<Dictionary<LocalComp.LocalCompFile, List<string>>, int>(
            Lang.Text("Instance.Export.Task.CreateArchive"),
            loader =>
            {
                // 整理文件列表
                var files = new JsonArray();
                foreach (var pair in loader.input)
                {
                    var modFile = pair.Key;
                    files.Add(new JsonObject
                    {
                        { "path", Path.GetRelativePath(overridesFolder, modFile.path).Replace('\\', '/') },
                        {
                            "hashes",
                            new JsonObject
                            {
                                { "sha1", modFile.ModrinthHash }, { "sha512", Common.GetFileSha512(modFile.path) }
                            }
                        },
                        { "downloads", new JsonArray(pair.Value.OrderByDescending(u => u.Contains("modrinth.com")).Select(s => (JsonNode)s).ToArray()) },
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
                Directory.CreateDirectory(Common.GetPathFromFullPath(packPath));
                if (File.Exists(packPath))
                    File.Delete(packPath);
                if (includePcl)
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
                Common.OpenExplorer(packPath);
            })
        {
            ProgressWeight = 6d
        });

        #endregion

        // 启动
        var mainLoader = new Loading.LoaderCombo<string>(loaderName, loaders)
        { OnStateChanged = DownloadUi.LoaderStateChangedHintOnly };
        mainLoader.Start();
        Loading.LoaderTaskbarAdd(mainLoader);
        Launcher.frmMain.BtnExtraDownload.ShowRefresh();
        Launcher.frmMain.BtnExtraDownload.Ribble();
        Launcher.frmMain.PageChange(FormMain.PageType.TaskManager);
    }

    #endregion
}
