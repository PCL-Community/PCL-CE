using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PCL;

public partial class MyCompItem
{
    /// <summary>未显式指定 ViewModel 时使用的默认实例，保证旧调用点（直接设置 CLR 属性）仍然可用。</summary>
    private readonly MyCompItemViewModel _defaultVm = new();

    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(MyCompItemViewModel), typeof(MyCompItem),
        new PropertyMetadata(null, (d, e) => ((MyCompItem)d).OnViewModelChanged((MyCompItemViewModel?)e.NewValue)));

    /// <summary>条目数据。列表虚拟化时会随容器复用而重新赋值。</summary>
    public MyCompItemViewModel ViewModel
    {
        get => (MyCompItemViewModel?)GetValue(ViewModelProperty) ?? _defaultVm;
        set => SetValue(ViewModelProperty, value);
    }

    private MyCompItemViewModel? _boundVm;

    private string? _stateLast;

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

        if (_stateLast == stateNew)
            return;
        _stateLast = stateNew;
        // 触发颜色动画
        if (IsLoaded && !Animations.AniExitsAnyControl())
        {
            var ani = new List<Animations.AniData>();
            if (IsMouseOver)
            {
                if (PanButtons is not null && HasActionButtons)
                    ani.Add(Animations.AaOpacity(PanButtons, 1d - PanButtons.Opacity,
                        (int)Math.Round(time * 0.35d),
                        (int)Math.Round(time * 0.15d)));
                ani.AddRange([
                    Animations.AaColor(RectBack, Border.BackgroundProperty,
                        _isMouseDown ? "ColorBrush6" : "ColorBrushBg1", time),
                    Animations.AaOpacity(RectBack, 1d - RectBack.Opacity, time,
                        ease: new Animations.AniEaseOutFluent())
                ]);
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
                if (PanButtons is not null && HasActionButtons)
                    ani.Add(Animations.AaOpacity(PanButtons, -PanButtons.Opacity,
                        (int)Math.Round(time * 0.4d)));
                ani.AddRange([
                    Animations.AaOpacity(RectBack, -RectBack.Opacity, time),
                    Animations.AaColor(RectBack, Border.BackgroundProperty,
                        _isMouseDown ? "ColorBrush6" : "ColorBrush7", time),
                    Animations.AaScaleTransform(RectBack,
                        0.996d - ((ScaleTransform)RectBack.RenderTransform).ScaleX,
                        time, ease: new Animations.AniEaseOutFluent()),
                    Animations.AaScaleTransform(RectBack, -0.196d, 1, after: true)
                ]);
            }

            Animations.AniStart(ani, "CompItem Color " + Uuid);
        }
        else
        {
            Animations.AniStop("CompItem Color " + Uuid);
            if (_rectBack is not null)
                RectBack.Opacity = 0d;
            PanButtons?.Opacity = 0d;
        }
    }

    #region 基础属性

    public int Uuid = Common.GetUuid();

    public string Logo
    {
        get => PathLogo.Source;
        set => PathLogo.Source = value;
    }

    public string Title
    {
        get => ViewModel.TitleText;
        set
        {
            if (ViewModel.TitleText == value)
                return;
            ViewModel.TitleText = value;
        }
    }

    public string SubTitle
    {
        get => ViewModel.SubTitleText;
        set
        {
            if (ViewModel.SubTitleText == value)
                return;
            ViewModel.SubTitleText = value;
            ViewModel.SubTitleVisibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public string Description
    {
        get => ViewModel.DescriptionText;
        set
        {
            if (ViewModel.DescriptionText == value)
                return;
            ViewModel.DescriptionText = value;
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

    /// <summary>
    ///     ViewModel 变更时重新把数据应用到控件上。<br/>
    ///     虚拟化容器回收后，同一个控件会被复用来显示另一条数据，因此这里必须重置所有视觉状态。
    /// </summary>
    private void OnViewModelChanged(MyCompItemViewModel? vm)
    {
        if (_boundVm is not null)
            _boundVm.PropertyChanged -= OnViewModelPropertyChanged;
        _boundVm = vm;
        if (vm is null)
            return;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        ApplyViewModel(vm);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_boundVm is null)
            return;
        if (e.PropertyName == nameof(MyCompItemViewModel.FavoriteIcon))
            BtnDelete.SvgIcon = _boundVm.FavoriteIcon;
    }

    private void ApplyViewModel(MyCompItemViewModel vm)
    {
        // 重置 hover 状态，避免容器复用后残留上一个条目的悬停视觉
        _stateLast = null;
        _isMouseDown = false;
        Animations.AniStop("CompItem Color " + Uuid);
        _rectBack?.Opacity = 0d;
        PanButtons?.Opacity = 0d;

        Tag = vm.Project;
        vm.Project?.ApplyLogoToMyImage(PathLogo);
        Tags = vm.Tags ?? [];

        LabSource.Text = vm.SourceText;
        LocalizedText.Bind(LabVersion, TextBlock.TextProperty, () => vm.VersionText.Value);
        LocalizedText.Bind(LabTime, TextBlock.TextProperty, () => vm.TimeText.Value);
        LocalizedText.Bind(LabDownload, TextBlock.TextProperty, () => vm.DownloadText.Value);

        BtnDelete.SvgIcon = vm.FavoriteIcon;
        BtnDelete.Visibility = vm.ShowFavoriteButton ? Visibility.Visible : Visibility.Collapsed;
        BtnDownload.Visibility = vm.ShowDownloadButton ? Visibility.Visible : Visibility.Collapsed;
        _UpdatePanButtons();

        ApplyVersionVisibility(vm.ShowVersion);
        ApplyTimeVisibility(vm.ShowTime);
    }

    private void ApplyVersionVisibility(bool show)
    {
        SvgIconVersion.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        LabVersion.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ColumnVersion1.Width = show ? GridLength.Auto : new GridLength(0d);
        ColumnVersion2.MaxWidth = show ? double.PositiveInfinity : 0d;
        ColumnVersion3.Width = show ? new GridLength(0.25d, GridUnitType.Star) : new GridLength(0d);
    }

    private void ApplyTimeVisibility(bool show)
    {
        SvgIconTime.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        LabTime.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        ColumnTime1.Width = show ? GridLength.Auto : new GridLength(0d);
        ColumnTime2.Width = show ? GridLength.Auto : new GridLength(0d);
        ColumnTime3.Width = show ? new GridLength(0.25d, GridUnitType.Star) : new GridLength(0d);
    }

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

    public List<string> Tags
    {
        set => TagPanelHelper.FillLocalized(
            PanTags,
            value,
            new SolidColorBrush(Color.FromRgb(134, 134, 134)));
    }

    public bool ShowFavoriteBtn
    {
        get => BtnDelete.Visibility == Visibility.Visible;
        set
        {
            BtnDelete.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            _UpdatePanButtons();
        }
    }

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
    private bool HasActionButtons => ShowFavoriteBtn || ShowDownloadBtn;

    private void _UpdatePanButtons()
    {
        PanButtons?.Visibility = HasActionButtons ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 刷新收藏状态（仅更新图标，不影响 hover 展示逻辑）
    /// </summary>
    public void RefreshFavoriteStatus()
    {
        if (ViewModel.Project is null && Tag is Comp.CompProject project)
            ViewModel.Project = project;
        ViewModel.RefreshFavoriteStatus();
    }
    #endregion
    #region 点击

    public event ClickEventHandler? Click;

    public delegate void ClickEventHandler(object sender, MouseButtonEventArgs e);

    private void BtnDelete_Click(object sender, EventArgs e)
    {
        if (PanButtons.Opacity > 0d && Tag is Comp.CompProject)
        {
            var project = (Comp.CompProject)Tag;
            CompFavoritesUi.ShowMenu(project, (UIElement)sender, RefreshFavoriteStatus);
        }
    }

    private void _BtnDownload_Click(object sender, EventArgs e)
    {
        if (PanButtons.Opacity > 0d && Tag is Comp.CompProject project)
            Comp.QuickDownload(project);
    }

    private void MyCompItem_Click(MyCompItem sender, EventArgs e)
    {
        // 记录当前展开的卡片标题（#2712）
        var titles = new List<string>();
        if (Launcher.frmMain!.pageCurrent.page == FormMain.PageType.CompDetail)
        {
            foreach (var group in Launcher.frmDownloadCompDetail!.ViewModel.FileGroups)
                if (group is { Title: { Length: > 0 } cardKey, IsSwapped: false })
                    titles.Add(cardKey);
            Common.Log(
                $"[Comp] 记录当前已展开的卡片：{string.Join("、", titles.Select(PageDownloadCompDetail.GetVersionGroupTitleForLog))}");
            var additional = Launcher.frmMain.pageCurrent.additional!.Value;
            Launcher.frmMain.pageCurrent.additional = additional with { ExpandedTitles = titles };
        }

        // 打开详情页
        var targetType = default(Comp.CompType);
        string? targetVersion = null;
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
                            targetVersion = Launcher.frmDownloadMod!.Content.loader.input!.gameVersion;
                            targetLoader = Launcher.frmDownloadMod.Content.loader.input.modLoader;
                            break;
                        }
                    case FormMain.PageSubType.DownloadPack:
                        {
                            targetType = Comp.CompType.ModPack;
                            targetVersion = Launcher.frmDownloadPack!.Content.loader.input!.gameVersion;
                            break;
                        }
                    case FormMain.PageSubType.DownloadDataPack:
                        {
                            targetType = Comp.CompType.DataPack;
                            targetVersion = Launcher.frmDownloadDataPack!.Content.loader.input!
                                .gameVersion;
                            break;
                        }
                    case FormMain.PageSubType.DownloadResourcePack:
                        {
                            targetType = Comp.CompType.ResourcePack;
                            targetVersion = Launcher.frmDownloadResourcePack!.Content.loader.input!
                                .gameVersion;
                            break;
                        }
                    case FormMain.PageSubType.DownloadShader:
                        {
                            targetType = Comp.CompType.Shader;
                            targetVersion = Launcher.frmDownloadShader!.Content.loader.input!.gameVersion;
                            break;
                        }
                    case FormMain.PageSubType.DownloadWorld:
                        {
                            targetType = Comp.CompType.World;
                            targetVersion = Launcher.frmDownloadWorld!.Content.loader.input!.gameVersion;
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
            var additional = Launcher.frmMain.pageCurrent.additional!.Value;
            targetVersion = additional.TargetVersion;
            targetLoader = additional.TargetLoader;
        }

        Launcher.frmMain.PageChange(new FormMain.PageStackData
        {
            page = FormMain.PageType.CompDetail,
            additional = ((Comp.CompProject)sender.Tag, [], targetVersion, targetLoader, targetType, null)!
        });
    }

    private bool _isMouseDown;

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

    /// <summary>
    /// 判断点击是否落在某个操作按钮（收藏 / 下载）上
    /// </summary>
    private bool _IsClickOnActionButton(FrameworkElement? button, Point clickPosition)
    {
        if (button is null || button.Visibility != Visibility.Visible) return false;
        var bounds = new Rect(button.TranslatePoint(new Point(0d, 0d), this), button.RenderSize);

        return bounds.Contains(clickPosition);
    }

    #endregion
    #region 后加载指向背景

    private Border? _rectBack;

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

            return _rectBack;
        }
    }

    #endregion
}
