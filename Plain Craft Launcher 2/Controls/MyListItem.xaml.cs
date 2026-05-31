using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PCL;

[ContentProperty("Inlines")]
public partial class MyListItem : IMyRadio
{
    public delegate void ClickEventHandler(object sender, MouseButtonEventArgs e);

    public delegate void LogoClickEventHandler(object sender, MouseButtonEventArgs e);

    public bool isMouseOverAnimationEnabled = true;

    private string stateLast;

    public object tag { get; set; }
    public event IMyRadio.CheckEventHandler? Check;
    public event IMyRadio.ChangedEventHandler? Changed;

    public event ClickEventHandler? Click;
    public event LogoClickEventHandler? LogoClick;

    public void RefreshColor(object sender, EventArgs e)
    {
        // 菜单虚拟化检测
        if (ContentHandler is not null)
        {
            ContentHandler.Invoke(this, e);
            ContentHandler = null;
        }

        // 判断当前颜色
        string stateNew;
        int time;
        if (isMouseDown && !(Type == CheckType.RadioBox && Checked))
        {
            stateNew = "MouseDown";
            time = 120;
        }
        else if (IsMouseOver && isMouseOverAnimationEnabled)
        {
            stateNew = "MouseOver";
            time = 120;
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
            if (IsMouseOver && isMouseOverAnimationEnabled)
            {
                if (buttonStack is not null)
                {
                    ani.Add(ModAnimation.AaOpacity(buttonStack, 1d - buttonStack.Opacity, (int)Math.Round(time * 0.7d),
                        (int)Math.Round(time * 0.3d)));
                    ani.Add(ModAnimation.AaDouble(
                        i => ColumnPaddingRight.Width =
                            new GridLength(Math.Max(0, ColumnPaddingRight.Width.Value + (double)i)),
                        Math.Max(MinPaddingRight, 5 + Buttons.Count() * 25) - ColumnPaddingRight.Width.Value,
                        (int)Math.Round(time * 0.3d), (int)Math.Round(time * 0.7d)));
                }

                ani.AddRange(new[]
                {
                    ModAnimation.AaColor(RectBack, Border.BackgroundProperty,
                        isMouseDown ? "ColorBrush6" : "ColorBrushBg1", time),
                    ModAnimation.AaOpacity(RectBack, 1d - RectBack.Opacity, time,
                        ease: new ModAnimation.AniEaseOutFluent())
                });
                if (IsScaleAnimationEnabled)
                {
                    ani.Add(ModAnimation.AaScaleTransform(RectBack,
                        1d - ((ScaleTransform)RectBack.RenderTransform).ScaleX, (int)Math.Round(time * 1.6d),
                        ease: new ModAnimation.AniEaseOutFluent()));
                    if (isMouseDown)
                        ani.Add(ModAnimation.AaScaleTransform(this, 0.98d - ((ScaleTransform)RenderTransform).ScaleX,
                            (int)Math.Round(time * 0.9d), ease: new ModAnimation.AniEaseOutFluent()));
                    else
                        ani.Add(ModAnimation.AaScaleTransform(this, 1d - ((ScaleTransform)RenderTransform).ScaleX,
                            (int)Math.Round(time * 1.2d), ease: new ModAnimation.AniEaseOutFluent()));
                }
            }
            else
            {
                if (buttonStack is not null)
                {
                    ani.Add(ModAnimation.AaOpacity(buttonStack, -buttonStack.Opacity, (int)Math.Round(time * 0.4d)));
                    ani.Add(ModAnimation.AaDouble(
                        i => ColumnPaddingRight.Width =
                            new GridLength(Math.Max(0, ColumnPaddingRight.Width.Value + (double)i)),
                        MinPaddingRight - ColumnPaddingRight.Width.Value, (int)Math.Round(time * 0.4d)));
                }

                ani.Add(ModAnimation.AaOpacity(RectBack, -RectBack.Opacity, time));
                if (IsScaleAnimationEnabled)
                    ani.AddRange(new[]
                    {
                        ModAnimation.AaColor(RectBack, Border.BackgroundProperty,
                            isMouseDown ? "ColorBrush6" : "ColorBrush7", time),
                        ModAnimation.AaScaleTransform(this, 1d - ((ScaleTransform)RenderTransform).ScaleX, time * 3,
                            ease: new ModAnimation.AniEaseOutFluent()),
                        ModAnimation.AaScaleTransform(RectBack,
                            0.996d - ((ScaleTransform)RectBack.RenderTransform).ScaleX, time,
                            ease: new ModAnimation.AniEaseOutFluent()),
                        ModAnimation.AaScaleTransform(RectBack, -0.246d, 1, after: true)
                    });
            }

            ModAnimation.AniStart(ani, "ListItem Color " + uuid);
        }
        else
        {
            // 无动画
            if (IsMouseOver && isMouseOverAnimationEnabled)
            {
                if (buttonStack is not null)
                {
                    buttonStack.Opacity = 1d;
                    ColumnPaddingRight.Width = new GridLength(Math.Max(MinPaddingRight, 5 + Buttons.Count() * 25));
                }

                // 由于鼠标已经移入，所以直接实例化 RectBack
                RectBack.Background = (Brush)ThemeManager.AppResources["ColorBrushBg1"];
                RectBack.Opacity = 1d;
                RectBack.RenderTransform = new ScaleTransform(1d, 1d);
                RenderTransform = new ScaleTransform(1d, 1d);
            }
            else
            {
                if (buttonStack is not null)
                {
                    buttonStack.Opacity = 0d;
                    ColumnPaddingRight.Width = new GridLength(MinPaddingRight);
                }

                RenderTransform = new ScaleTransform(1d, 1d);
                if (_RectBack is not null)
                {
                    if (IsScaleAnimationEnabled)
                        RectBack.RenderTransform = new ScaleTransform(0.75d, 0.75d);
                    RectBack.Background = (Brush)ThemeManager.AppResources["ColorBrush7"];
                    RectBack.Opacity = 0d;
                }
            }

            ModAnimation.AniStop("ListItem Color " + uuid);
        }
    }

    private void MyListItem_Loaded(object sender, RoutedEventArgs e)
    {
        if (Checked)
            SetResourceReference(ForegroundProperty, Height < 40d ? "ColorBrush3" : "ColorBrush2");
        else
            SetResourceReference(ForegroundProperty, "ColorBrush1");
        ColumnPaddingRight.Width = new GridLength(MinPaddingRight);
        if (CustomEventService.GetEventType(this) == CustomEvent.EventType.打开帮助 && !(Title != "" && Info != "")) // #3266
        {
            try
            {
                ModMain.HelpEntry entry = new ModMain.HelpEntry(CustomEvent.GetAbsoluteUrls(CustomEventService.GetEventData(this), CustomEventService.GetEventType(this))[0]);
                entry.SetToListItem(this);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "设置帮助 MyListItem 失败", ModBase.LogLevel.Msgbox);
                CustomEventService.SetEventType(this, CustomEvent.EventType.None);
                CustomEventService.SetEventData(this, "");
            }
        }
    }

    public override string ToString()
    {
        return Title;
    }

    #region 后加载控件

    // 指向背景
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
                    CornerRadius = new CornerRadius(IsScaleAnimationEnabled || Height > 40d ? 6 : 0),
                    RenderTransform = IsScaleAnimationEnabled ? new ScaleTransform(0.8d, 0.8d) : null,
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
                // <!--<Border x:Name = "RectBack" CornerRadius="3" RenderTransformOrigin="0.5,0.5" SnapsToDevicePixels="True" 
                // IsHitTestVisible = "False" Opacity="0" BorderThickness="1" 
                // Grid.ColumnSpan = "4" Background="{DynamicResource ColorBrush7}" BorderBrush="{DynamicResource ColorBrush6}"/>-->
            }

            return _RectBack;
        }
    }

    // 按钮
    public FrameworkElement buttonStack;

    // 图标
    public FrameworkElement pathLogo;

    // 勾选条
    public Border rectCheck;


    /// <summary>
    ///     Tags 的存放 StackPanel
    /// </summary>
    public StackPanel _PanTags;

    public StackPanel PanTags
    {
        get
        {
            if (_PanTags is not null)
                return _PanTags;
            var newStack = new StackPanel
            {
                IsHitTestVisible = false,
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(3.5d, 0d, -3, 0d)
            };
            SetColumn(newStack, 3);
            SetRow(newStack, 2);
            PanBack.Children.Add(newStack);
            _PanTags = newStack;
            return _PanTags;
        }
    }

    /// <summary>
    ///     标签，可以传入 String 和 List(Of String)
    /// </summary>
    public object Tags
    {
        set
        {
            var list = new List<string>();
            if (value is string str) list = str.Split("|").ToList();
            if (value is List<string>) list = (List<string>)value;
            PanTags.Children.Clear();
            PanTags.Visibility = list.Any() ? Visibility.Visible : Visibility.Collapsed;
            foreach (var TagText in list)
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
                    Text = TagText,
                    Foreground = new SolidColorBrush(Color.FromRgb(134, 134, 134)),
                    FontSize = 11d
                };
                newTag.Child = tagTextBlock;
                PanTags.Children.Add(newTag);
            }
        }
    }

    // 副文本
    private TextBlock _LabInfo;

    public TextBlock LabInfo
    {
        get
        {
            if (_LabInfo is null)
            {
                var lab = new TextBlock
                {
                    Name = "LabInfo",
                    SnapsToDevicePixels = false,
                    UseLayoutRounding = false,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    IsHitTestVisible = false,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Visibility = Visibility.Collapsed,
                    FontSize = 12d,
                    Margin = new Thickness(4d, 0d, 0d, 0d),
                    Opacity = 0.6d
                };
                SetColumn(lab, 4);
                SetRow(lab, 2);
                PanBack.Children.Add(lab);
                _LabInfo = lab;
                // <TextBlock Grid.Row="2" SnapsToDevicePixels="False" UseLayoutRounding="False" HorizontalAlignment="Left" x:Name = "LabInfo" IsHitTestVisible="False" Grid.Column="2" 
                // TextTrimming = "CharacterEllipsis" Visibility="Collapsed" FontSize="12" Foreground="{StaticResource ColorBrushGray2}" Margin="4,0,0,0" />
            }

            return _LabInfo;
        }
    }

    #endregion

    #region 自定义属性

    // Uuid
    public int uuid = ModBase.GetUuid();

    /// <summary>
    ///     是否启用缩放动画。
    /// </summary>
    public bool IsScaleAnimationEnabled
    {
        get => _IsScaleAnimationEnabled;
        set
        {
            _IsScaleAnimationEnabled = value;
            if (_RectBack is not null)
                RectBack.CornerRadius = new CornerRadius(value ? 6 : 0);
        }
    }

    private bool _IsScaleAnimationEnabled = true;

    // 边距
    public int PaddingLeft
    {
        get => (int)Math.Round(ColumnPaddingLeft.Width.Value);
        set => ColumnPaddingLeft.Width = new GridLength(value);
    }

    /// <summary>
    ///     右边距的最小值。
    ///     在存在右侧按钮时，右边距会被自动设置为 5 + 按钮数 * 25。
    /// </summary>
    public int MinPaddingRight { get; set; } = 4;

    // 按钮
    private IEnumerable<MyIconButton> _Buttons;

    public IEnumerable<MyIconButton> Buttons
    {
        get => _Buttons;
        set
        {
            _Buttons = value;
            // 没有特殊按钮，移除原 Stack
            if (buttonStack is not null)
            {
                Children.Remove(buttonStack);
                buttonStack = null;
            }

            // 添加新 Stack
            switch (value.Count())
            {
                case 0:
                {
                    break;
                }
                // 没有按钮，不添加新的
                case 1:
                {
                    // 只有一个按钮
                    foreach (var Btn in value)
                    {
                        if (Btn.Height.Equals(double.NaN))
                            Btn.Height = 25d;
                        if (Btn.Width.Equals(double.NaN))
                            Btn.Width = 25d;
                        Btn.Opacity = 0d;
                        Btn.Margin = new Thickness(0d, 0d, 5d, 0d);
                        Btn.SnapsToDevicePixels = false;
                        Btn.HorizontalAlignment = HorizontalAlignment.Right;
                        Btn.VerticalAlignment = VerticalAlignment.Center;
                        Btn.UseLayoutRounding = false;
                        SetColumnSpan(Btn, 10);
                        SetRowSpan(Btn, 10);
                        Children.Add(Btn);
                        buttonStack = Btn;
                    }

                    break;
                }

                default:
                {
                    // 有复数按钮，使用 StackPanel
                    buttonStack = new StackPanel
                    {
                        Opacity = 0d, Margin = new Thickness(0d, 0d, 5d, 0d), SnapsToDevicePixels = false,
                        Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center, UseLayoutRounding = false
                    };
                    SetColumnSpan(buttonStack, 10);
                    SetRowSpan(buttonStack, 10);
                    // 构造按钮
                    foreach (var Btn in value)
                    {
                        if (Btn.Height.Equals(double.NaN))
                            Btn.Height = 25d;
                        if (Btn.Width.Equals(double.NaN))
                            Btn.Width = 25d;
                        ((StackPanel)buttonStack).Children.Add(Btn);
                    }

                    Children.Add(buttonStack);
                    break;
                }
            }
        }
    }

    // 标题
    public InlineCollection Inlines => LabTitle.Inlines;

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value.Replace("\r", "").Replace("\n", ""));
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register("Title", typeof(string), typeof(MyListItem));

    // 字号
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public static readonly DependencyProperty FontSizeProperty =
        DependencyProperty.Register("FontSize", typeof(double), typeof(MyListItem), new PropertyMetadata(14d));

    // 信息
    public string Info
    {
        get => (string)GetValue(InfoProperty);
        set
        {
            if (Info == value)
                return;
            value = value?.Replace("\r", "").Replace("\n", "");
            SetValue(InfoProperty, value);
        }
    }

    public static readonly DependencyProperty InfoProperty = DependencyProperty.Register("Info", typeof(string),
        typeof(MyListItem), new PropertyMetadata("", OnInfoChanged));

    public MyListItem()
    {
        InitializeComponent();

        SizeChanged += (_, _) => OnSizeChanged();
        PreviewMouseLeftButtonUp += Button_MouseUp;
        PreviewMouseLeftButtonDown += Button_MouseDown;
        MouseLeave += Button_MouseLeave;
        PreviewMouseLeftButtonUp += Button_MouseLeave;
        MouseEnter += RefreshColor;
        MouseLeave += RefreshColor;
        MouseLeftButtonDown += RefreshColor;
        MouseLeftButtonUp += RefreshColor;
        Loaded += MyListItem_Loaded;
    }

    private static void OnInfoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MyListItem)d;
        var value = e.NewValue as string;
        control.LabInfo.Text = value;
        control.LabInfo.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
    }

    // 图片
    public string Logo
    {
        get => (string)GetValue(LogoProperty);
        set
        {
            if (Logo == value)
                return;
            SetValue(LogoProperty, value);
        }
    }

    public static readonly DependencyProperty LogoProperty = DependencyProperty.Register("Logo", typeof(string),
        typeof(MyListItem), new PropertyMetadata("", OnLogoChanged));

    private static void OnLogoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (MyListItem)d;
        var value = e.NewValue as string;
        control.UpdateLogo(value);
    }

    private void UpdateLogo(string _Logo)
    {
        // 删除旧 Logo
            if (pathLogo is not null)
            Children.Remove(pathLogo);
        // 添加新 Logo
        if (!string.IsNullOrEmpty(_Logo))
        {
            if (_Logo.StartsWithF("http", true))
            {
                // 网络图片
                pathLogo = new MyImage
                {
                    Tag = this,
                    IsHitTestVisible = LogoClickable,
                    Source = _Logo,
                    RenderTransformOrigin = new Point(0.5d, 0.5d),
                    RenderTransform = new ScaleTransform { ScaleX = LogoScale, ScaleY = LogoScale },
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = false
                };
                RenderOptions.SetBitmapScalingMode(pathLogo, BitmapScalingMode.Linear);
            }
            else if (_Logo.EndsWithF(".png", true) || _Logo.EndsWithF(".jpg", true) || _Logo.EndsWithF(".webp", true))
            {
                // 位图
                pathLogo = new Canvas
                {
                    Tag = this,
                    IsHitTestVisible = LogoClickable,
                    Background = new MyBitmap(_Logo),
                    RenderTransformOrigin = new Point(0.5d, 0.5d),
                    RenderTransform = new ScaleTransform { ScaleX = LogoScale, ScaleY = LogoScale },
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = false,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                if (_Logo.Contains(ModBase.pathTemp + @"Cache\Skin\Head") ||
                    _Logo.Contains(ModBase.pathTemp + @"Cache\Cape"))
                    RenderOptions.SetBitmapScalingMode(pathLogo, BitmapScalingMode.NearestNeighbor);
                else
                    RenderOptions.SetBitmapScalingMode(pathLogo, BitmapScalingMode.Linear);
            }
            else
            {
                // 矢量图
                pathLogo = new Path
                {
                    Tag = this,
                    IsHitTestVisible = LogoClickable,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Stretch = Stretch.Uniform,
                    Data = (Geometry)new GeometryConverter().ConvertFromString(_Logo),
                    RenderTransformOrigin = new Point(0.5d, 0.5d),
                    RenderTransform = new ScaleTransform { ScaleX = LogoScale, ScaleY = LogoScale },
                    SnapsToDevicePixels = false,
                    UseLayoutRounding = false
                };
                pathLogo.SetBinding(Shape.FillProperty, new Binding("Foreground") { Source = this });
            }

            SetColumn(pathLogo, 2);
            SetRowSpan(pathLogo, 4);
            OnSizeChanged(); // 设置边距
            Children.Add(pathLogo);
            // 图标的点击事件
            if (LogoClickable)
            {
                pathLogo.MouseLeave += (sender, e) => isLogoDown = false;
                pathLogo.MouseLeftButtonDown += (sender, e) => isLogoDown = true;
                pathLogo.MouseLeftButtonUp += (sender, e) =>
                {
                    if (isLogoDown)
                    {
                        isLogoDown = false;
                        LogoClick?.Invoke(((FrameworkElement)sender).Tag, e);
                    }
                };
            }
        }

        // 改变行距
        ColumnLogo.Width = new GridLength((string.IsNullOrEmpty(_Logo) ? 0 : 34) + (Height < 40d ? 0 : 4));
    }

    private double _LogoScale = 1d;

    public double LogoScale
    {
        get => _LogoScale;
        set
        {
            _LogoScale = value;
        if (pathLogo is not null)
                pathLogo.RenderTransform = new ScaleTransform { ScaleX = LogoScale, ScaleY = LogoScale };
        }
    }

    // 图标的点击
    /// <summary>
    ///     该 Logo 是否可用点击触发事件。需要在 Logo 属性之前设置。
    /// </summary>
    public bool LogoClickable { get; set; } = false;

    private bool isLogoDown;

    // 勾选选项
    public enum CheckType
    {
        None,
        Clickable,
        RadioBox,
        CheckBox
    }

    private CheckType _Type = CheckType.None;

    public CheckType Type
    {
        get => _Type;
        set
        {
            if (_Type == value)
                return;
            _Type = value;
            // 切换左栏大小
            ColumnCheck.Width =
                new GridLength(_Type == CheckType.None || _Type == CheckType.Clickable ? Height < 40d ? 4 : 2 : 6);
            // 切换竖条控件
            if (_Type == CheckType.None || _Type == CheckType.Clickable)
            {
                // 移除竖条控件
                if (rectCheck is not null)
                {
                    Children.Remove(rectCheck);
                    rectCheck = null;
                }

                SetChecked(false, false, false);
            }
            // 添加竖条控件
            else if (rectCheck is null)
            {
                rectCheck = new Border
                {
                    Width = 5d,
                    Height = Checked ? double.NaN : 0d,
                    CornerRadius = new CornerRadius(2d, 2d, 2d, 2d),
                    VerticalAlignment = Checked ? VerticalAlignment.Stretch : VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    UseLayoutRounding = false,
                    SnapsToDevicePixels = false,
                    Margin = Checked ? new Thickness(-1, 6d, 0d, 6d) : new Thickness(-1, 0d, 0d, 0d)
                };
                rectCheck.SetResourceReference(Border.BackgroundProperty, "ColorBrush3");
                SetRowSpan(rectCheck, 4);
                Children.Add(rectCheck);
            }
        }
    }

    // 适应尺寸
    private void OnSizeChanged()
    {
        var _Logo = Logo;
        ColumnCheck.Width =
            new GridLength(_Type == CheckType.None || _Type == CheckType.Clickable ? Height < 40d ? 4 : 2 : 6);
        ColumnLogo.Width = new GridLength((string.IsNullOrEmpty(_Logo) ? 0 : 34) + (Height < 40d ? 0 : 4));
        if (pathLogo is not null)
        {
            if (_Logo.EndsWithF(".png", true) || _Logo.EndsWithF(".jpg", true) || _Logo.EndsWithF(".webp", true))
                pathLogo.Margin = new Thickness(4d, 5d, 3d, 5d);
            else
                pathLogo.Margin = new Thickness(Height < 40d ? 6 : 8, 8d, Height < 40d ? 4 : 6, 8d);
        }

        LabTitle.Margin = new Thickness(4d, 0d, 0d, Height < 40d ? 0 : 2);
    }

    // 勾选状态
    private bool _Checked;

    public bool Checked
    {
        get => _Checked;
        set => SetChecked(value, false, value != _Checked); // 仅在值发生变化时触发动画 (#4596)
    }

    /// <summary>
    ///     手动设置 Checked 属性。
    /// </summary>
    /// <param name="value">新的 Checked 属性。</param>
    /// <param name="user">是否由用户引发。</param>
    /// <param name="anime">是否执行动画。</param>
    public void SetChecked(bool value, bool user, bool anime)
    {
        try
        {
            // 自定义属性基础

            var ChangedEventArgs = new ModBase.RouteEventArgs(user);
            var rawValue = _Checked;
            if (Type == CheckType.RadioBox)
            {
                if (IsInitialized && value != _Checked)
                {
                    _Checked = value;
                    Changed?.Invoke(this, ChangedEventArgs);
                    if (ChangedEventArgs.handled)
                    {
                        _Checked = rawValue;
                        return;
                    }
                }

                _Checked = value;
            }
            else
            {
                if (value == _Checked)
                    return;
                _Checked = value;
                if (IsInitialized)
                {
                    Changed?.Invoke(this, ChangedEventArgs);
                    if (ChangedEventArgs.handled)
                    {
                        _Checked = rawValue;
                        return;
                    }
                }
            }

            if (value)
            {
                var checkEventArgs = new ModBase.RouteEventArgs(user);
                Check?.Invoke(this, checkEventArgs);
                if (checkEventArgs.handled)
                    return;
            }

            // 保证只有一个单选 ListItem 选中

            if (Type == CheckType.RadioBox)
            {
                if (Parent is null)
                    return;
                var radioboxList = new List<MyListItem>();
                var checkedCount = 0;
                // 收集控件列表与选中个数
                foreach (var ControlRaw in ((Panel)Parent).Children)
                {
                    var control = MyVirtualizingElement.TryInit((FrameworkElement)ControlRaw);
                    if (control is MyListItem listItem && listItem.Type == CheckType.RadioBox)
                    {
                        radioboxList.Add(listItem);
                        if (listItem.Checked)
                            checkedCount += 1;
                    }
                }

                // 判断选中情况
                switch (checkedCount)
                {
                    case 0:
                    {
                        // 没有任何单选框被选中，选择第一个
                        radioboxList[0].Checked = true;
                        break;
                    }
                    case var @case when @case > 1:
                    {
                        // 选中项目多于 1 个
                        if (Checked)
                        {
                            // 如果本控件选中，则取消其他所有控件的选中
                            foreach (var Control in radioboxList)
                                if (Control.Checked && !Control.Equals(this))
                                    Control.Checked = false;
                        }
                        else
                        {
                            // 如果本控件未选中，则只保留第一个选中的控件
                            var firstChecked = false;
                            foreach (var Control in radioboxList)
                                if (Control.Checked)
                                {
                                    if (firstChecked)
                                        Control.Checked = false; // 修改 Checked 会自动触发 Change 事件，所以不用额外触发
                                    else
                                        firstChecked = true;
                                }
                        }

                        break;
                    }
                }
            }

            var customHeight = 20d; // 修改左侧神秘动画条的高度

            if (IsLoaded && ModAnimation.AniControlEnabled == 0 && anime) // 防止默认属性变更触发动画
            {
                var anim = new List<ModAnimation.AniData>();
                if (Checked)
                {
                    // 由无变有
                    if (rectCheck is not null)
                    {
                        // 统一静态属性：固定高度、居中对齐、无额外边距
                        rectCheck.Height = customHeight;
                        rectCheck.VerticalAlignment = VerticalAlignment.Center;
                        rectCheck.Margin = new Thickness(-1, 0d, 0d, 0d);
                        rectCheck.Opacity = 1d;

                        // 初始化缩放中心为正中心
                        var scale = new ScaleTransform(1d, 0d);
                        rectCheck.RenderTransformOrigin = new Point(0.5d, 0.5d);
                        rectCheck.RenderTransform = scale;

                        // 动画：让 ScaleY 从 0 弹性放大到 1
                        anim.Add(ModAnimation.AaDouble(
                            i => scale.ScaleY = Math.Max(0d, scale.ScaleY + (double)i),
                            1d - scale.ScaleY,
                            300,
                            ease: new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)
                        ));
                    }

                    anim.Add(ModAnimation.AaColor(this, ForegroundProperty,
                        Height < 40d ? "ColorBrush3" : "ColorBrush2", 200));
                }
                else
                {
                    // 由有变无
                    if (rectCheck is not null)
                    {
                        if (rectCheck.RenderTransform is not ScaleTransform)
                        {
                            rectCheck.RenderTransformOrigin = new Point(0.5d, 0.5d);
                            rectCheck.RenderTransform = new ScaleTransform(1d, 1d);
                        }

                        var scale = (ScaleTransform)rectCheck.RenderTransform;

                        // 动画：让 ScaleY 从当前值缩减到 0
                        anim.Add(ModAnimation.AaDouble(
                            i => scale.ScaleY = Math.Max(0d, scale.ScaleY + (double)i),
                            -scale.ScaleY,
                            120,
                            ease: new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)
                        ));
                        anim.Add(ModAnimation.AaOpacity(rectCheck, -rectCheck.Opacity, 70, 40));
                    }

                    anim.Add(ModAnimation.AaColor(this, ForegroundProperty, "ColorBrush1", 120));
                }

                ModAnimation.AniStart(anim, "MyListItem Checked " + uuid);
            }
            else
            {
                // 不使用动画
                ModAnimation.AniStop("MyListItem Checked " + uuid);
                if (Checked)
                {
                    if (rectCheck is not null)
                    {
                        rectCheck.Height = customHeight; // 应用自定义固定高度
                        rectCheck.Margin = new Thickness(-1, 0d, 0d, 0d);
                        rectCheck.Opacity = 1d;
                        rectCheck.VerticalAlignment = VerticalAlignment.Center; // 居中
                        rectCheck.RenderTransform = null; // 清除缩放
                    }

                    SetResourceReference(ForegroundProperty, Height < 40d ? "ColorBrush3" : "ColorBrush2");
                }
                else
                {
                    if (rectCheck is not null)
                    {
                        rectCheck.Height = 0d;
                        rectCheck.Margin = new Thickness(-1, 0d, 0d, 0d);
                        rectCheck.Opacity = 0d;
                        rectCheck.VerticalAlignment = VerticalAlignment.Center;
                        rectCheck.RenderTransform = null;
                    }

                    SetResourceReference(ForegroundProperty, "ColorBrush1");
                }
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "设置 Checked 失败");
        }
    }

    // 前景色绑定
    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register("Foreground",
        typeof(Brush), typeof(MyListItem), new PropertyMetadata(ThemeManager.AppResources["ColorBrush1"]));

    // 菜单与按钮绑定
    public Action<MyListItem, EventArgs> ContentHandler { get; set; }
    
    /// <summary>
    /// 图标可选的圆角。仅当 Logo 为位图（MyImage 或 Canvas）时生效。
    /// 当所有角的圆角半径均 ≥ 0 时，应用圆角效果；否则不应用。
    /// </summary>
    public CornerRadius LogoCornerRadius
    {
        get => field;
        set
        {
            field = value;
            if (pathLogo is Canvas) _canvasClipHandlerAdded = false;
            ApplyLogoCornerRadius();
        }
    } = new CornerRadius(-1);
    
    private bool IsLogoCornerRadiusEnabled() => 
        LogoCornerRadius is { TopLeft: >= 0, TopRight: >= 0, BottomLeft: >= 0, BottomRight: >= 0 };

    private void UpdateCanvasClip(Canvas c)
    {
        if (c is { ActualWidth: > 0, ActualHeight: > 0 })
        {
            var r = LogoCornerRadius;
            double radius = Math.Max(Math.Max(r.TopLeft, r.TopRight), Math.Max(r.BottomLeft, r.BottomRight));
            c.Clip = new RectangleGeometry(new Rect(0, 0, c.ActualWidth, c.ActualHeight), radius, radius);
        }
    }

    private bool _canvasClipHandlerAdded = false;

    private void ApplyLogoCornerRadius()
    {
        if (pathLogo is null || !IsLogoCornerRadiusEnabled()) return;

        switch (pathLogo)
        {
            case MyImage myImage:
                myImage.CornerRadius = LogoCornerRadius;
                break;
            case Canvas canvas when !_canvasClipHandlerAdded && canvas is { ActualWidth: 0, ActualHeight: 0 }:
                UpdateCanvasClip(canvas);
                canvas.SizeChanged += OnCanvasLogoSizeChanged;
                _canvasClipHandlerAdded = true;
                break;
            case Canvas canvas:
                UpdateCanvasClip(canvas);
                break;
        }
    }

    private void OnCanvasLogoSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (sender is not Canvas { ActualWidth: > 0, ActualHeight: > 0 } canvas) return;
    
        UpdateCanvasClip(canvas);
        canvas.SizeChanged -= OnCanvasLogoSizeChanged;
        _canvasClipHandlerAdded = false;
    }
    #endregion

    #region 点击

    // 触发点击事件
    private void Button_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!isMouseDown)
            return;
        Click?.Invoke(sender, e);
        if (e.Handled)
            return;
        // 触发自定义事件
        var dependencyObject = (DependencyObject)sender;
        if (CustomEventService.GetEventType(dependencyObject) != CustomEvent.EventType.None)
        {
            ModMain.RaiseCustomEvent(this);
            e.Handled = true;
        }

        if (e.Handled)
            return;
        // 实际的单击处理
        switch (Type)
        {
            case CheckType.Clickable:
            {
                ModBase.Log("[Control] 按下单击列表项：" + Title);
                break;
            }
            case CheckType.RadioBox:
            {
                ModBase.Log("[Control] 按下单选列表项：" + Title);
                if (!Checked)
                    SetChecked(true, true, true);
                break;
            }
            case CheckType.CheckBox:
            {
                ModBase.Log("[Control] 按下复选列表项（" + !Checked + "）：" + Title);
                SetChecked(!Checked, true, true);
                break;
            }
        }
    }

    // 鼠标点击判定
    private bool isMouseDown;

    private void Button_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsMouseDirectlyOver && !(Type == CheckType.None))
        {
            isMouseDown = true;
            if (buttonStack is not null)
                buttonStack.IsHitTestVisible = false;
        }
    }

    private void Button_MouseLeave(object sender, object e)
    {
        isMouseDown = false;
        if (buttonStack is not null)
            buttonStack.IsHitTestVisible = true;
    }

    #endregion
}