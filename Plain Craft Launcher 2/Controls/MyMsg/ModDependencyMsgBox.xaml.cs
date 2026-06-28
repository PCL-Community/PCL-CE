using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using PCL.Core.App.Localization;
using PCL.Core.UI.Controls;

namespace PCL;

/// <summary>
///     模组版本的「前置详情」弹窗：展示该版本的版本信息、必要前置 / 可选前置，
///     并提供「安装到当前实例」/「选择下载位置」两个动作。点击某个前置项会关闭弹窗并返回该前置工程。
///     布局由 XAML 定义，调用方只需传入 CompFile 数据（由 <see cref="ModMain.ModDependencyMsgBox" /> 触发）。
/// </summary>
public partial class ModDependencyMsgBox
{
    private readonly ModMain.MyMsgBoxConverter myConverter;
    private readonly int uuid = ModBase.GetUuid();

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

    /// <summary>
    ///     填充版本信息与必要 / 可选前置项。
    /// </summary>
    private void Populate(ModComp.CompFile file)
    {
        // 1. 版本信息
        LabVersionInfo.Text = file.DisplayName;
        _AddTag(file.StatusDescription);
        foreach (var loader in file.ModLoaders)
            _AddTag(loader.ToString());
        LabDateInfo.Text = Lang.Text("Download.Comp.Detail.FileList.Updated", file.ReleaseDate.ToString("G"));

        // 2. 必要前置 / 可选前置
        FillDependencySection(LabReqTitle, PanReqDeps, file.Dependencies,
            Lang.Text("Download.Comp.Detail.FileList.RequiredDependencies"));
        FillDependencySection(LabOptTitle, PanOptDeps, file.OptionalDependencies,
            Lang.Text("Download.Comp.Detail.FileList.OptionalDependencies"));
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

    /// <summary>
    ///     填充一个前置分区：标题 + 可点击的前置项（点击以该前置工程关闭弹窗）。
    /// </summary>
    private void FillDependencySection(TextBlock header, StackPanel panel, List<string> depIds, string title)
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
            return;
        }

        foreach (var project in projects)
        {
            var captured = project;
            // 复用「游戏资源 → 模组」搜索结果同款 MyCompItem 卡片样式；
            // 关闭其内置跳转（AutoNavigate=false），改由弹窗统一「先关闭再跳转」
            var item = project.ToCompItem(true, true).Init();
            item.AutoNavigate = false;
            item.Click += (_, _) => ReturnResult(captured);
            panel.Children.Add(item);
        }
    }

    /// <summary>
    ///     以指定返回值关闭弹窗：按钮返回 1 / 2，前置项返回对应 CompProject。
    /// </summary>
    private void ReturnResult(object result)
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
