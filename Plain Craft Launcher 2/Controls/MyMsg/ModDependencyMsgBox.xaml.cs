using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Controls;

namespace PCL;

/// <summary>
///     模组版本的「前置详情」弹窗：展示版本信息与必要 / 可选前置，并提供安装到实例、选择下载位置两个动作。
/// </summary>
public partial class ModDependencyMsgBox
{
    private readonly ModMain.MyMsgBoxConverter myConverter;
    private readonly int uuid = ModBase.GetUuid();

    /// <summary>弹窗中的前置项，用于在已安装检查完成后更新状态标签。</summary>
    private readonly List<DependencyEntry> dependencyEntries = [];

    private sealed record DependencyEntry(MyListItem Item, ModComp.CompProject Project, bool Required);

    public ModDependencyMsgBox(ModMain.MyMsgBoxConverter converter)
    {
        try
        {
            InitializeComponent();
            AppendUniqueNameSuffix(Btn1);
            AppendUniqueNameSuffix(Btn2);
            AppendUniqueNameSuffix(Btn3);
            myConverter = converter;
            LabTitle.Text = Lang.Text("Download.Comp.Detail.VersionPopup.Title");
            Btn1.Text = Lang.Text("Download.Comp.Detail.VersionPopup.ButtonInstall");
            Btn2.Text = Lang.Text("Download.Comp.Detail.VersionPopup.ButtonSaveAs");
            Btn3.Text = Lang.Text("Common.Action.Cancel");
            LabNoDeps.Text = Lang.Text("Download.Comp.Detail.VersionPopup.NoDeps");
            ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);
            if (converter.Content is ModComp.CompFile file)
                Populate(file);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "前置弹窗初始化失败", ModBase.LogLevel.Hint);
        }

        Loaded += Load;
        PreviewKeyDown += ModDependencyMsgBox_PreviewKeyDown;
        Btn1.Click += Btn1_Click;
        Btn2.Click += Btn2_Click;
        Btn3.Click += Btn3_Click;
        LabTitle.MouseLeftButtonDown += Drag;
        PanBorder.MouseLeftButtonDown += Drag;
    }

    private void AppendUniqueNameSuffix(FrameworkElement element)
    {
        element.Name += ModBase.GetUuid();
    }

    /// <summary>填充版本信息与必要 / 可选前置项。</summary>
    private void Populate(ModComp.CompFile file)
    {
        // 1. 版本信息
        LabVersionInfo.Text = file.DisplayName;
        _AddTag(file.StatusDescription);
        foreach (var loader in file.ModLoaders)
            _AddTag(loader.ToString());
        LabDateInfo.Text = Lang.Text("Download.Comp.Detail.FileList.Updated",
            Lang.TimeSpan(file.ReleaseDate - DateTime.Now));

        // 2. 必要前置 / 可选前置
        var hasRequired = FillDependencySection(LabReqTitle, PanReqDeps, file.Dependencies, required: true,
            Lang.Text("Download.Comp.Detail.FileList.RequiredDependencies"));
        var hasOptional = FillDependencySection(LabOptTitle, PanOptDeps, file.OptionalDependencies, required: false,
            Lang.Text("Download.Comp.Detail.FileList.OptionalDependencies"));
        if (!hasRequired && !hasOptional)
            LabNoDeps.Visibility = Visibility.Visible;
    }

    private void _AddTag(string text)
    {
        var tag = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(17, 0, 0, 0)),
            Padding = new Thickness(3d, 1d, 3d, 1d),
            CornerRadius = new CornerRadius(3d),
            Margin = new Thickness(0d, 0d, 3d, 0d),
            SnapsToDevicePixels = true,
            UseLayoutRounding = false
        };
        tag.Child = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.FromRgb(134, 134, 134)),
            FontSize = 11d
        };
        PanVersionTags.Children.Add(tag);
    }

    /// <summary>填充一个前置分区，返回该分区是否有内容。</summary>
    private bool FillDependencySection(TextBlock header, StackPanel panel, List<string> depIds, bool required,
        string title)
    {
        var projects = new List<ModComp.CompProject>();
        foreach (var id in depIds)
            if (ModComp.compProjectCache.TryGetValue(id, out var project))
                projects.Add(project);

        header.Text = $"{title}（{projects.Count}）";

        if (!projects.Any())
        {
            header.Visibility = Visibility.Collapsed;
            panel.Visibility = Visibility.Collapsed;
            return false;
        }

        foreach (var project in projects)
        {
            var item = CreateDependencyItem(project);
            dependencyEntries.Add(new DependencyEntry(item, project, required));
            panel.Children.Add(item);
        }

        return true;
    }

    /// <summary>创建前置列表项：图标 + 名称 + 加载器 / 游戏版本 + 安装状态标签。</summary>
    private MyListItem CreateDependencyItem(ModComp.CompProject project)
    {
        var item = project.ToListItem();
        item.Type = MyListItem.CheckType.Clickable;
        item.Height = 42d;
        item.Margin = new Thickness(0d, 0d, 0d, 4d);
        item.Info = GetDependencyInfo(project);
        // 有选中实例时先显示检查中，待后台检查完成后替换为实际状态
        item.Tags = ModInstanceList.McMcInstanceSelected is null
            ? new List<string>()
            : new List<string> { Lang.Text("Download.Comp.Detail.VersionPopup.Status.Checking") };
        item.Click += (_, _) => ReturnResult(project);
        return item;
    }

    /// <summary>前置项的副文本：加载器与游戏版本支持范围。</summary>
    private static string GetDependencyInfo(ModComp.CompProject project)
    {
        var loaders = project.ModLoaders.ToList();
        if (Config.Download.Comp.IgnoreQuilt)
            loaders.Remove(ModComp.CompLoaderType.Quilt);
        var loaderDesc = loaders.Count == 0 || loaders.Contains(ModComp.CompLoaderType.Any)
            ? Lang.Text("Download.Comp.Type.Any")
            : string.Join(" / ", loaders);
        var versionDesc = project.GetGameVersionDescription();
        return string.IsNullOrWhiteSpace(versionDesc) ? loaderDesc : $"{loaderDesc}  |  {versionDesc}";
    }

    /// <summary>在后台检查当前实例中已安装的前置并更新状态标签。</summary>
    private void StartInstalledCheck()
    {
        if (dependencyEntries.Count == 0) return;
        var instance = ModInstanceList.McMcInstanceSelected;
        if (instance is null) return;
        ModBase.RunInNewThread(() =>
        {
            HashSet<string>? installed = null;
            try
            {
                if (!instance.IsLoaded) instance.Load();
                installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var mod in ModCompDependency.ScanInstalledMods(instance.PathIndie + "mods\\"))
                {
                    if (string.IsNullOrEmpty(mod.SourceProjectId) || string.IsNullOrEmpty(mod.Source))
                        continue;
                    installed.Add($"{mod.Source}|{mod.SourceProjectId}");
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "检查已安装前置失败", ModBase.LogLevel.Hint);
            }

            ModBase.RunInUi(() => ApplyInstalledStatus(installed));
        }, "ModDependencyCheck");
    }

    /// <summary>填充前置的安装状态标签；<paramref name="installed" /> 为 null 表示检查失败。</summary>
    private void ApplyInstalledStatus(HashSet<string>? installed)
    {
        if (myConverter.IsExited) return;
        foreach (var entry in dependencyEntries)
        {
            string statusKey;
            if (installed is null)
            {
                statusKey = "Download.Comp.Detail.VersionPopup.Status.Unknown";
            }
            else
            {
                var source = entry.Project.FromCurseForge ? "CurseForge" : "Modrinth";
                if (installed.Contains($"{source}|{entry.Project.Id}"))
                    statusKey = "Download.Comp.Detail.VersionPopup.Status.Installed";
                else if (entry.Required)
                    // 「默认不安装」时不会自动补装前置，此时如实标注为未安装
                    statusKey = Config.Download.Comp.QuickDownloadDependencies == 1
                        ? "Download.Comp.Detail.VersionPopup.Status.NotInstalled"
                        : "Download.Comp.Detail.VersionPopup.Status.WillInstall";
                else
                    statusKey = "Download.Comp.Detail.VersionPopup.Status.Optional";
            }

            entry.Item.Tags = new List<string> { Lang.Text(statusKey) };
        }
    }

    /// <summary>以指定返回值关闭弹窗（按钮返回 1 / 2，前置项返回对应 CompProject）。</summary>
    private void ReturnResult(object? result)
    {
        if (myConverter.IsExited)
            return;
        myConverter.IsExited = true;
        myConverter.Result = result;
        Close();
    }

    private void Load(object sender, EventArgs e)
    {
        try
        {
            // UI 初始化
            if (Btn2.IsVisible && !(Btn1.ColorType == MyButton.ColorState.Red))
                Btn1.ColorType = MyButton.ColorState.Highlight;
            Btn1.Focus();
            // 动画
            Opacity = 0d;
            ModAnimation.AniStart(
                ModAnimation.AaColor(ModMain.frmMain.PanMsgBackground, BlurBorder.BackgroundProperty,
                    new ModBase.MyColor(90d, 0d, 0d, 0d) - ModMain.frmMain.PanMsgBackground.Background, 200),
                "PanMsgBackground Background");
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaOpacity(this, 1d, 120, 60),
                    ModAnimation.AaDouble(i => TransformPos.Y += (double)i,
                        -TransformPos.Y, 300, 60, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaDouble(i => TransformRotate.Angle += (double)i,
                        -TransformRotate.Angle, 300, 60,
                        new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
                }, "MyMsgBox " + uuid);
            ModBase.Log("[Control] 前置详情弹窗：" + LabTitle.Text);
            StartInstalledCheck();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "前置详情弹窗加载失败", ModBase.LogLevel.Hint);
        }
    }

    private void Close()
    {
        // 结束线程阻塞
        myConverter.WaitFrame.Continue = false;
        ComponentDispatcher.PopModal();
        // 动画
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() =>
            {
                if (!ModMain.WaitingMyMsgBox.Any())
                    ModAnimation.AniStart(ModAnimation.AaColor(ModMain.frmMain.PanMsgBackground,
                        BlurBorder.BackgroundProperty,
                        new ModBase.MyColor(0d, 0d, 0d, 0d) - ModMain.frmMain.PanMsgBackground.Background, 200,
                        ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
            }, 30),
            ModAnimation.AaOpacity(this, -Opacity, 80, 20),
            ModAnimation.AaDouble(i => TransformPos.Y += (double)i, 20d - TransformPos.Y,
                150, 0, new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaDouble(i => TransformRotate.Angle += (double)i,
                6d - TransformRotate.Angle, 150, 0, new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() => ((Grid)Parent).Children.Remove(this), after: true)
        }, "MyMsgBox " + uuid);
    }

    public void Btn1_Click(object sender, MouseButtonEventArgs e) => ReturnResult(1);

    public void Btn2_Click(object sender, MouseButtonEventArgs e) => ReturnResult(2);

    public void Btn3_Click(object sender, MouseButtonEventArgs e) => ReturnResult(null);

    /// <summary>按 Esc 关闭弹窗（等同点击「取消」，返回 null）。</summary>
    private void ModDependencyMsgBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            ReturnResult(null);
        }
    }

    private void Drag(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                if (e.GetPosition(ShapeLine).Y <= 2d)
                    ModMain.frmMain.DragMove();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "拖拽移动失败", ModBase.LogLevel.Hint);
        }
    }
}
