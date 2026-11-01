using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PCL;

public partial class MyCompItem
{
    private readonly MyCompItemViewModel _vm = new();

    public MyCompItemViewModel ViewModel => _vm;

    private string _stateLast;

    /// <summary>
    /// 是否允许交互。目前仅用于 PageDownloadCompDetail 的顶部栏展示：若关闭碰撞检测，则无法展开 Tooltip。
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
            if (_isMouseDown)
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

        if ((_stateLast ?? "") == (stateNew ?? ""))
            return;
        _stateLast = stateNew;
        // 触发颜色动画
        if (IsLoaded && !Animations.AniExitsAnyControl()) // 防止默认属性变更触发动画
        {
            // 有动画
            var ani = new List<Animations.AniData>();
            if (IsMouseOver)
            {
                if (PanButtons is not null && _HasActionButtons)
                    ani.Add(Animations.AaOpacity(PanButtons, 1d - PanButtons.Opacity,
                        (int)Math.Round(time * 0.35d),
                        (int)Math.Round(time * 0.15d)));
                ani.AddRange(new[]
                {
                    Animations.AaColor(RectBack, Border.BackgroundProperty,
                        _isMouseDown ? "ColorBrush6" : "ColorBrushBg1", time),
                    Animations.AaOpacity(RectBack, 1d - RectBack.Opacity, time,
                        ease: new Animations.AniEaseOutFluent())
                });
                if (_isMouseDown)
                    ani.Add(Animations.AaScaleTransform(RectBack,
                        0.996d - ((ScaleTransform)RectBack.RenderTransform).ScaleX, (int)Math.Round(time * 1.2d),
                        ease: new Animations.AniEaseOutFluent()));
                else
                    ani.Add(Animations.AaScaleTransform(RectBack,
                        1d - ((ScaleTransform)RectBack.RenderTransform).ScaleX, (int)Math.Round(time * 1.2d),
                        ease: new Animations.AniEaseOutFluent()));
            }
            else
            {
                if (PanButtons is not null && _HasActionButtons)
                    ani.Add(Animations.AaOpacity(PanButtons, -PanButtons.Opacity,
                        (int)Math.Round(time * 0.4d)));
                ani.AddRange(new[]
                {
                    Animations.AaOpacity(RectBack, -RectBack.Opacity, time),
                    Animations.AaColor(RectBack, Border.BackgroundProperty,
                        _isMouseDown ? "ColorBrush6" : "ColorBrush7", time),
                    Animations.AaScaleTransform(RectBack,
                        0.996d - ((ScaleTransform)RectBack.RenderTransform).ScaleX,
                        time, ease: new Animations.AniEaseOutFluent()),
                    Animations.AaScaleTransform(RectBack, -0.196d, 1, after: true)
                });
            }

            Animations.AniStart(ani, "CompItem Color " + Uuid);
        }
        else
        {
            // 无动画
            Animations.AniStop("CompItem Color " + Uuid);
            if (_rectBack is not null)
                RectBack.Opacity = 0d;
            if (PanButtons is not null)
                PanButtons.Opacity = 0d;
        }
    }

    #region 基础属性

    public int Uuid = Common.GetUuid();

    // Logo
    public string Logo
    {
        get => PathLogo.Source;
        set => PathLogo.Source = value;
    }

    // 标题
    public string Title
    {
        get => _vm.TitleText;
        set
        {
            if ((_vm.TitleText ?? "") == (value ?? ""))
                return;
            _vm.TitleText = value;
        }
    }

    // 副标题
    public string SubTitle
    {
        get => _vm.SubTitleText;
        set
        {
            if ((_vm.SubTitleText ?? "") == (value ?? ""))
                return;
            _vm.SubTitleText = value;
            _vm.SubTitleVisibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    // 描述
    public string Description
    {
        get => _vm.DescriptionText;
        set
        {
            if ((_vm.DescriptionText ?? "") == (value ?? ""))
                return;
            _vm.DescriptionText = value;
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
            textBlock.FlowDirection, typeface, textBlock.FontSize, textBlock.Foreground,
            Common.Dpi);
        return formattedText.Width > textBlock.ActualWidth;
    }

    // Tag
    public List<string> Tags
    {
        set => TagPanelHelper.FillLocalized(
            PanTags,
            value,
            new SolidColorBrush(Color.FromRgb(134, 134, 134)));
    }

    // ‘收藏按钮
    public bool ShowFavoriteBtn
    {
        get => BtnDelete.Visibility == Visibility.Visible;
        set
        {
            BtnDelete.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            _UpdatePanButtons();
        }
    }

    // 快速下载按钮
    public bool ShowDownloadBtn
    {
        get => BtnDownload.Visibility == Visibility.Visible;
        set
        {
            BtnDownload.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            _UpdatePanButtons();
        }
    }

    /// <summary>右侧是否存在任意可见的操作按钮（收藏 / 下载），用于决定悬停时是否淡入按钮区。</summary>
    private bool _HasActionButtons => ShowFavoriteBtn || ShowDownloadBtn;

    private void _UpdatePanButtons()
    {
        if (PanButtons is null) return;
        PanButtons.Visibility = _HasActionButtons ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 刷新收藏状态（仅更新图标，不影响 hover 展示逻辑）
    /// </summary>
    public void RefreshFavoriteStatus()
    {
        if (Tag is not Comp.CompProject project) return;

        var isFavourite = Comp.CompFavorites.IsFavourite(project.Id);
        BtnDelete.SvgIcon = isFavourite ? "lucide/heart-filled" : "lucide/heart";
    }
    #endregion
    #region 点击



    // 触发点击事件
    public event ClickEventHandler? Click;

    public delegate void ClickEventHandler(object sender, MouseButtonEventArgs e);

    private void BtnDelete_Click(object sender, EventArgs e)
    {
        if (PanButtons.Opacity > 0d && Tag is Comp.CompProject)
        {
            var project = (Comp.CompProject)Tag;
            Comp.CompFavorites.ShowMenu(project, (UIElement)sender, () => RefreshFavoriteStatus());
        }
    }

    private void _BtnDownload_Click(object sender, EventArgs e)
    {
        if (PanButtons.Opacity > 0d && Tag is Comp.CompProject project)
            Comp.QuickDownload(project);
    }

    private void MyCompItem_Click(MyCompItem sender, EventArgs e)
    {
        // 记录当前展开的卡片标题
        // 记录当前展开的卡片标题（#2712）
        var titles = new List<string>();
        if (Launcher.frmMain.pageCurrent.page == FormMain.PageType.CompDetail)
        {
            foreach (MyCard Card in Launcher.frmDownloadCompDetail.PanResults.Children)
                if (Card.Tag is string cardKey && !string.IsNullOrEmpty(cardKey) && !Card.IsSwapped)
                    titles.Add(cardKey);
            Common.Log(
                $"[Comp] 记录当前已展开的卡片：{string.Join("、", titles.Select(PageDownloadCompDetail.GetVersionGroupTitleForLog))}");
            var additional = Launcher.frmMain.pageCurrent.additional.Value;
            Launcher.frmMain.pageCurrent.additional = additional with { ExpandedTitles = titles };
        }

        // 打开详情页
        var targetType = default(Comp.CompType);
        string targetVersion = null;
        var targetLoader = Comp.CompLoaderType.Any;
        if (Launcher.frmMain.pageCurrent.page == FormMain.PageType.Download)
        {
            if (Launcher.frmMain.PageCurrentSub == FormMain.PageSubType.DownloadCompFavorites)
            {
                targetVersion = "";
                targetLoader = Comp.CompLoaderType.Any;
            }
            else
            {
                // 从下载页进入
                switch (Launcher.frmMain.PageCurrentSub)
                {
                    case FormMain.PageSubType.DownloadMod:
                    {
                        targetType = Comp.CompType.Mod;
                        targetVersion = Launcher.frmDownloadMod.Content.loader.input.gameVersion;
                        targetLoader = Launcher.frmDownloadMod.Content.loader.input.modLoader;
                        break;
                    }
                    case FormMain.PageSubType.DownloadPack:
                    {
                        targetType = Comp.CompType.ModPack;
                        targetVersion = Launcher.frmDownloadPack.Content.loader.input.gameVersion;
                        break;
                    }
                    case FormMain.PageSubType.DownloadDataPack:
                    {
                        targetType = Comp.CompType.DataPack;
                        targetVersion = Launcher.frmDownloadDataPack.Content.loader.input
                            .gameVersion;
                        break;
                    }
                    case FormMain.PageSubType.DownloadResourcePack:
                    {
                        targetType = Comp.CompType.ResourcePack;
                        targetVersion = Launcher.frmDownloadResourcePack.Content.loader.input
                            .gameVersion;
                        break;
                    }
                    case FormMain.PageSubType.DownloadShader:
                    {
                        targetType = Comp.CompType.Shader;
                        targetVersion = Launcher.frmDownloadShader.Content.loader.input.gameVersion;
                        break;
                    }
                    case FormMain.PageSubType.DownloadWorld:
                    {
                        targetType = Comp.CompType.World;
                        targetVersion = Launcher.frmDownloadWorld.Content.loader.input.gameVersion;
                        break;
                    }
                }
            }
        }
        else if (Launcher.frmMain.pageCurrent.page == FormMain.PageType.InstanceSetup)
        {
            // 从实例设置页进入（查看整合包信息）
            targetType = Comp.CompType.ModPack;
        }
        else
        {
            // 从详情页进入（查看前置）
            targetType = Comp.CompType.Any; // 允许任意类别
            var additional = Launcher.frmMain.pageCurrent.additional.Value;
            targetVersion = additional.TargetVersion;
            targetLoader = additional.TargetLoader;
        }

        Launcher.frmMain.PageChange(new FormMain.PageStackData
        {
            page = FormMain.PageType.CompDetail,
            additional = ((Comp.CompProject)sender.Tag, new List<string>(), targetVersion,
                targetLoader, targetType, null)
        });
    }

    // 鼠标点击判定
    private bool _isMouseDown;

    // 触发点击事件
    private void Button_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isMouseDown)
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
            isClickOnButton = _IsClickOnActionButton(BtnDelete, clickPosition) ||
                              _IsClickOnActionButton(BtnDownload, clickPosition);

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

        if (IsMouseDirectlyOver || isClickOnLabInfo) _isMouseDown = true;
    }

    private void Button_MouseLeave(object sender, object e)
    {
        _isMouseDown = false;
    }

    // 判断点击是否落在某个操作按钮（收藏 / 下载）上
    private bool _IsClickOnActionButton(FrameworkElement button, Point clickPosition)
    {
        if (button is null || button.Visibility != Visibility.Visible) return false;
        var bounds = new Rect(button.TranslatePoint(new Point(0d, 0d), this), button.RenderSize);
    #endregion
    #region 后加载指向背景
        return bounds.Contains(clickPosition);
    }


    private Border _rectBack;

    public Border RectBack
    {
        get
        {
            if (_rectBack is null)
            {
                var rect = new Border
                {
                    Name = "RectBack",
                    CornerRadius = new CornerRadius(3d),
                    RenderTransform = new ScaleTransform(0.8d, 0.8d),
                    RenderTransformOrigin = new Point(0.5d, 0.5d),
                    BorderThickness = new Thickness(Common.GetWpfSize(1d)),
                    SnapsToDevicePixels = true,
                    IsHitTestVisible = false,
                    Opacity = 0d
                };
                rect.SetResourceReference(Border.BackgroundProperty, "ColorBrush7");
                rect.SetResourceReference(Border.BorderBrushProperty, "ColorBrush6");
                SetColumnSpan(rect, 999);
                SetRowSpan(rect, 999);
                Children.Insert(0, rect);
                _rectBack = rect;
                // <!--<corelocal:BlurBorder x:Name = "RectBack" CornerRadius="3" RenderTransformOrigin="0.5,0.5" SnapsToDevicePixels="True"
                // IsHitTestVisible = "False" Opacity="0" BorderThickness="1"
                // Grid.ColumnSpan = "4" Background="{DynamicResource ColorBrush7}" BorderBrush="{DynamicResource ColorBrush6}"/>-->
            }
    #endregion

            return _rectBack;
        }
    }

}
