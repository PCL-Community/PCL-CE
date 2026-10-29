using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.ResourceProject;
using PCL.Core.UI.Controls;

namespace PCL;

/// <summary>
///     前置安装确认弹窗：以窄高度的模组卡片列出将要安装的前置，并提供安装前置 / 仅下载本体 / 取消三个选项。
/// </summary>
public partial class ModDependencyInstallMsgBox
{
    private readonly ModMain.MyMsgBoxConverter myConverter;
    private readonly int uuid = ModBase.GetUuid();

    public ModDependencyInstallMsgBox(ModMain.MyMsgBoxConverter converter)
    {
        try
        {
            InitializeComponent();
            AppendUniqueNameSuffix(Btn1);
            AppendUniqueNameSuffix(Btn2);
            AppendUniqueNameSuffix(Btn3);
            myConverter = converter;
            LabTitle.Text = Lang.Text("Download.Comp.Dependency.Install.Title");
            Btn1.Text = Lang.Text("Download.Comp.Dependency.Install.WithDependencies");
            Btn2.Text = Lang.Text("Download.Comp.Dependency.Install.WithoutDependencies");
            Btn3.Text = Lang.Text("Download.Comp.Dependency.Install.Cancel");
            ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);
            if (converter.Content is ModDependencyResolutionResult result)
                Populate(result);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "前置安装弹窗初始化失败", ModBase.LogLevel.Hint);
        }

        Loaded += Load;
        PreviewKeyDown += ModDependencyInstallMsgBox_PreviewKeyDown;
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

    /// <summary>填充说明文字与前置列表。</summary>
    private void Populate(ModDependencyResolutionResult result)
    {
        LabMessage.Text = Lang.Text("Download.Comp.Dependency.Install.Caption", result.ToInstall.Count);
        foreach (var install in result.ToInstall)
            PanDeps.Children.Add(CreateDependencyItem(install));
    }

    /// <summary>创建前置卡片：图标 + 名称 + 版本信息 + 来源标签。</summary>
    private static MyListItem CreateDependencyItem(ResolvedDependencyInstall install)
    {
        var title = string.IsNullOrWhiteSpace(install.ProjectName) ? install.ProjectId : install.ProjectName;
        MyListItem item;
        if (ModComp.compProjectCache.TryGetValue(install.ProjectId, out var project))
        {
            item = project.ToListItem();
            item.Title = title;
        }
        else
        {
            item = new MyListItem
            {
                Title = title,
                Logo = ModBase.pathImage + "Icons/NoIcon.png"
            };
        }

        item.Height = 42d;
        item.Margin = new Thickness(0d, 0d, 0d, 4d);
        item.Info = GetFileInfo(install.File);
        item.Tags = new List<string> { install.Source };
        return item;
    }

    /// <summary>前置文件的副文本：文件展示名，必要时补充版本号。</summary>
    private static string GetFileInfo(ModDependencyFile file)
    {
        var info = file.DisplayName;
        if (!string.IsNullOrWhiteSpace(file.Version) &&
            !info.Contains(file.Version, StringComparison.OrdinalIgnoreCase))
            info += $"  |  {file.Version}";
        return info;
    }

    /// <summary>以指定返回值关闭弹窗：1 = 安装前置，2 = 仅下载本体，null = 取消。</summary>
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
            ModBase.Log("[Control] 前置安装弹窗：" + LabTitle.Text);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "前置安装弹窗加载失败", ModBase.LogLevel.Hint);
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

    /// <summary>按 Esc 关闭弹窗（等同点击「取消安装」）。</summary>
    private void ModDependencyInstallMsgBox_PreviewKeyDown(object sender, KeyEventArgs e)
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
