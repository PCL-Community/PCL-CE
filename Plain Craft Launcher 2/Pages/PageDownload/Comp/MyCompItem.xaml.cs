using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PCL;

public partial class MyCompItem
{
    private string stateLast;

    /// <summary>
    ///     是否允许交互。目前仅用于 PageDownloadCompDetail 的顶部栏展示：若关闭碰撞检测，则无法展开 Tooltip。
    /// </summary>
    public bool CanInteraction { get; set; } = true;

    public void RefreshColor(object sender, EventArgs e)
    {
        if (!CanInteraction)
            return;
        // 判断当前颜色
        string stateNew;
        int time;
        if (IsMouseOver)
        {
            if (isMouseDown)
            {
                stateNew = "MouseDown";
                time = 120;
            }
            else
            {
                stateNew = "MouseOver";
                time = 120;
            }
        }
        else
        {
            stateNew = "Idle";
            time = 180;
        }

        if ((stateLast ?? "") == (stateNew ?? ""))
            return;
        stateLast = stateNew;
        // 触发颜色动画
        if (IsLoaded && ModAnimation.AniControlEnabled == 0) // 防止默认属性变更触发动画
        {
            // 有动画
            var ani = new List<ModAnimation.AniData>();
            if (IsMouseOver)
            {
                if (PanButtons is not null && ShowFavoriteBtn)
                    ani.Add(ModAnimation.AaOpacity(PanButtons, 1d - PanButtons.Opacity, (int)Math.Round(time * 0.35d),
                        (int)Math.Round(time * 0.15d)));
                ani.AddRange(new[]
                {
                    ModAnimation.AaColor(RectBack, Border.BackgroundProperty,
                        isMouseDown ? "ColorBrush6" : "ColorBrushBg1", time),
                    ModAnimation.AaOpacity(RectBack, 1d - RectBack.Opacity, time,
                        ease: new ModAnimation.AniEaseOutFluent())
                });
                if (isMouseDown)
                    ani.Add(ModAnimation.AaScaleTransform(RectBack,
                        0.996d - ((ScaleTransform)RectBack.RenderTransform).ScaleX, (int)Math.Round(time * 1.2d),
                        ease: new ModAnimation.AniEaseOutFluent()));
                else
                    ani.Add(ModAnimation.AaScaleTransform(RectBack,
                        1d - ((ScaleTransform)RectBack.RenderTransform).ScaleX, (int)Math.Round(time * 1.2d),
                        ease: new ModAnimation.AniEaseOutFluent()));
            }
            else
            {
                if (PanButtons is not null && ShowFavoriteBtn)
                    ani.Add(ModAnimation.AaOpacity(PanButtons, -PanButtons.Opacity, (int)Math.Round(time * 0.4d)));
                ani.AddRange(new[]
                {
                    ModAnimation.AaOpacity(RectBack, -RectBack.Opacity, time),
                    ModAnimation.AaColor(RectBack, Border.BackgroundProperty,
                        isMouseDown ? "ColorBrush6" : "ColorBrush7", time),
                    ModAnimation.AaScaleTransform(RectBack, 0.996d - ((ScaleTransform)RectBack.RenderTransform).ScaleX,
                        time, ease: new ModAnimation.AniEaseOutFluent()),
                    ModAnimation.AaScaleTransform(RectBack, -0.196d, 1, after: true)
                });
            }

            ModAnimation.AniStart(ani, "CompItem Color " + Uuid);
        }
        else
        {
            // 无动画
            ModAnimation.AniStop("CompItem Color " + Uuid);
            if (_RectBack is not null)
                RectBack.Opacity = 0d;
            if (PanButtons is not null)
                PanButtons.Opacity = 0d;
        }
    }

    #region 基础属性

    public int Uuid = ModBase.GetUuid();

    // Logo
    public string Logo
    {
        get => PathLogo.Source;
        set => PathLogo.Source = value;
    }

    // 标题
    public string Title
    {
        get => LabTitle.Text;
        set
        {
            if ((LabTitle.Text ?? "") == (value ?? ""))
                return;
            LabTitle.Text = value;
        }
    }

    // 副标题
    public string SubTitle
    {
        get => LabTitleRaw?.Text ?? "";
        set
        {
            if ((LabTitleRaw.Text ?? "") == (value ?? ""))
                return;
            LabTitleRaw.Text = value;
            LabTitleRaw.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    // 描述
    public string Description
    {
        get => LabInfo.Text;
        set
        {
            if ((LabInfo.Text ?? "") == (value ?? ""))
                return;
            LabInfo.Text = value;
        }
    }

    public MyCompItem()
    {
        InitializeComponent();
        Click += (sender, e) => MyCompItem_Click((MyCompItem)sender, e);
        PreviewMouseLeftButtonUp += Button_MouseUp;
        PreviewMouseLeftButtonDown += Button_MouseDown;
        MouseLeave += Button_MouseLeave;
        PreviewMouseLeftButtonUp += Button_MouseLeave;
        MouseEnter += RefreshColor;
        MouseLeave += RefreshColor;
        MouseLeftButtonDown += RefreshColor;
        MouseLeftButtonUp += RefreshColor;
        // Handles
        LabInfo.MouseEnter += LabInfo_MouseEnter;
        BtnDelete.Click += BtnDelete_Click;
    }

    // 指向时扩展描述
    private void LabInfo_MouseEnter(object sender, MouseEventArgs e)
    {
        if (IsTextTrimmed(LabInfo))
        {
            ToolTipInfo.Content = LabInfo.Text;
            ToolTipInfo.Width = LabInfo.ActualWidth + 25d;
            LabInfo.ToolTip = ToolTipInfo;
        }
        else
        {
            LabInfo.ToolTip = null;
        }
    }

    private bool IsTextTrimmed(TextBlock textBlock)
    {
        var typeface = new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight,
            textBlock.FontStretch);
        var formattedText = new FormattedText(textBlock.Text, Thread.CurrentThread.CurrentCulture,
            textBlock.FlowDirection, typeface, textBlock.FontSize, textBlock.Foreground, ModBase.dpi);
        return formattedText.Width > textBlock.ActualWidth;
    }

    // Tag
    public List<string> Tags
    {
        set
        {
            PanTags.Children.Clear();
            PanTags.Visibility = value.Any() ? Visibility.Visible : Visibility.Collapsed;
            foreach (var tagText in value)
            {
                var newTag = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(17, 0, 0, 0)),
                    Padding = new Thickness(3d, 1d, 3d, 1d),
                    CornerRadius = new CornerRadius(3d),
                    Margin = new Thickness(0d, 0d, 3d, 0d),
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = false
                };
                var tagTextBlock = new TextBlock
                {
                    Text = tagText,
                    Foreground = new SolidColorBrush(Color.FromRgb(134, 134, 134)),
                    FontSize = 11d
                };
                newTag.Child = tagTextBlock;
                PanTags.Children.Add(newTag);
            }
        }
    }

    // ‘收藏按钮
    public bool ShowFavoriteBtn
    {
        set => PanButtons.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        get => PanButtons.Visibility == Visibility.Visible;
    }

    /// <summary>
    ///     刷新收藏状态
    /// </summary>
    public void RefreshFavoriteStatus()
    {
        if (Tag is not ModComp.CompProject project) return;

        var isFavourite = ModComp.CompFavorites.IsFavourite(project.Id);
        BtnDelete.SvgIcon = isFavourite ? "lucide/heart-filled" : "lucide/heart";
        ShowFavoriteBtn = isFavourite;
    }

    #endregion

    #region 点击

    // 触发点击事件
    public event ClickEventHandler? Click;

    public delegate void ClickEventHandler(object sender, MouseButtonEventArgs e);

    private void BtnDelete_Click(object sender, EventArgs e)
    {
        if (PanButtons.Opacity > 0d && Tag is ModComp.CompProject)
        {
            var project = (ModComp.CompProject)Tag;
            ModComp.CompFavorites.ShowMenu(project, (UIElement)sender, () => RefreshFavoriteStatus());
        }
    }

    private void MyCompItem_Click(MyCompItem sender, EventArgs e)
    {
        // 记录当前展开的卡片标题（#2712）
        var titles = new List<string>();
        if (ModMain.frmMain.pageCurrent.page == FormMain.PageType.CompDetail)
        {
            foreach (MyCard Card in ModMain.frmDownloadCompDetail.PanResults.Children)
                if (!string.IsNullOrEmpty(Card.Title) && !Card.IsSwapped)
                    titles.Add(Card.Title);
            ModBase.Log("[Comp] 记录当前已展开的卡片：" + string.Join("、", titles));
            var additional = ModMain.frmMain.pageCurrent.additional.Value;
            ModMain.frmMain.pageCurrent.additional = additional with { ExpandedTitles = titles };
        }

        // 打开详情页
        var targetType = default(ModComp.CompType);
        string targetVersion = null;
        var targetLoader = ModComp.CompLoaderType.Any;
        if (ModMain.frmMain.pageCurrent.page == FormMain.PageType.Download)
        {
            if (ModMain.frmMain.PageCurrentSub == FormMain.PageSubType.DownloadCompFavorites)
            {
                targetVersion = "";
                targetLoader = ModComp.CompLoaderType.Any;
            }
            else
            {
                // 从下载页进入
                switch (ModMain.frmMain.PageCurrentSub)
                {
                    case FormMain.PageSubType.DownloadMod:
                    {
                        targetType = ModComp.CompType.Mod;
                        targetVersion = ModMain.frmDownloadMod.Content.loader.input.gameVersion;
                        targetLoader = ModMain.frmDownloadMod.Content.loader.input.modLoader;
                        break;
                    }
                    case FormMain.PageSubType.DownloadPack:
                    {
                        targetType = ModComp.CompType.ModPack;
                        targetVersion = ModMain.frmDownloadPack.Content.loader.input.gameVersion;
                        break;
                    }
                    case FormMain.PageSubType.DownloadDataPack:
                    {
                        targetType = ModComp.CompType.DataPack;
                        targetVersion = ModMain.frmDownloadDataPack.Content.loader.input.gameVersion;
                        break;
                    }
                    case FormMain.PageSubType.DownloadResourcePack:
                    {
                        targetType = ModComp.CompType.ResourcePack;
                        targetVersion = ModMain.frmDownloadResourcePack.Content.loader.input.gameVersion;
                        break;
                    }
                    case FormMain.PageSubType.DownloadShader:
                    {
                        targetType = ModComp.CompType.Shader;
                        targetVersion = ModMain.frmDownloadShader.Content.loader.input.gameVersion;
                        break;
                    }
                    case FormMain.PageSubType.DownloadWorld:
                    {
                        targetType = ModComp.CompType.World;
                        targetVersion = ModMain.frmDownloadWorld.Content.loader.input.gameVersion;
                        break;
                    }
                }
            }
        }
        else if (ModMain.frmMain.pageCurrent.page == FormMain.PageType.InstanceSetup)
        {
            // 从实例设置页进入（查看整合包信息）
            targetType = ModComp.CompType.ModPack;
        }
        else
        {
            // 从详情页进入（查看前置）
            targetType = ModComp.CompType.Any; // 允许任意类别
            var additional = ModMain.frmMain.pageCurrent.additional.Value;
            targetVersion = additional.TargetVersion;
            targetLoader = additional.TargetLoader;
        }

        ModMain.frmMain.PageChange(new FormMain.PageStackData
        {
            page = FormMain.PageType.CompDetail,
            additional = ((ModComp.CompProject)sender.Tag, new List<string>(), targetVersion, targetLoader, targetType, null, null, null)
        });
    }

    // 鼠标点击判定
    private bool isMouseDown;

    // 触发点击事件
    private void Button_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!isMouseDown)
            return;
        Click?.Invoke(sender, e);
    }

    private void Button_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!CanInteraction)
            return;
        // 检查点击位置是否在按钮区域内
        var clickPosition = e.GetPosition(this);
        var isClickOnButton = false;

        if (PanButtons.Visibility == Visibility.Visible)
        {
            var buttonBounds = new Rect(BtnDelete.TranslatePoint(new Point(0d, 0d), this), BtnDelete.RenderSize);
            isClickOnButton = buttonBounds.Contains(clickPosition);
        }

        // 如果点击在按钮上，不处理主项目点击事件
        if (isClickOnButton) return;

        // 如果点击在其他区域，按原逻辑处理
        // 也要检查是否点击在LabInfo区域（支持ToolTip点击）
        var isClickOnLabInfo = false;
        if (LabInfo.Visibility == Visibility.Visible)
        {
            var labInfoBounds = new Rect(LabInfo.TranslatePoint(new Point(0d, 0d), this), LabInfo.RenderSize);
            isClickOnLabInfo = labInfoBounds.Contains(clickPosition);
        }

        if (IsMouseDirectlyOver || isClickOnLabInfo) isMouseDown = true;
    }

    private void Button_MouseLeave(object sender, object e)
    {
        isMouseDown = false;
    }

    #endregion

    #region 后加载指向背景

    private Border _RectBack;

    public Border RectBack
    {
        get
        {
            if (_RectBack is null)
            {
                var rect = new Border
                {
                    Name = "RectBack",
                    CornerRadius = new CornerRadius(3d),
                    RenderTransform = new ScaleTransform(0.8d, 0.8d),
                    RenderTransformOrigin = new Point(0.5d, 0.5d),
                    BorderThickness = new Thickness(ModBase.GetWPFSize(1d)),
                    SnapsToDevicePixels = true,
                    IsHitTestVisible = false,
                    Opacity = 0d
                };
                rect.SetResourceReference(Border.BackgroundProperty, "ColorBrush7");
                rect.SetResourceReference(Border.BorderBrushProperty, "ColorBrush6");
                SetColumnSpan(rect, 999);
                SetRowSpan(rect, 999);
                Children.Insert(0, rect);
                _RectBack = rect;
                // <!--<corelocal:BlurBorder x:Name = "RectBack" CornerRadius="3" RenderTransformOrigin="0.5,0.5" SnapsToDevicePixels="True" 
                // IsHitTestVisible = "False" Opacity="0" BorderThickness="1" 
                // Grid.ColumnSpan = "4" Background="{DynamicResource ColorBrush7}" BorderBrush="{DynamicResource ColorBrush6}"/>-->
            }

            return _RectBack;
        }
    }

    #endregion
}