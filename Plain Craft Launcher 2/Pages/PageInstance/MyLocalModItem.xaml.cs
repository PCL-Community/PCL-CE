using Microsoft.VisualBasic;
using PCL.Core.Utils.Exts;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace PCL;

public partial class MyLocalCompItem
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel),
        typeof(MyLocalModItemViewModel),
        typeof(MyLocalCompItem),
        new PropertyMetadata(null, OnViewModelPropertyChanged));

    private static void OnViewModelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((MyLocalCompItem)d).OnViewModelChanged((MyLocalModItemViewModel?)e.OldValue,
            (MyLocalModItemViewModel?)e.NewValue);

    /// <summary>
    ///     条目视图模型。<br />
    ///     由列表的 DataTemplate 绑定；旧调用方也可以直接构造控件后赋值 <see cref="Entry" />。
    /// </summary>
    public MyLocalModItemViewModel ViewModel
    {
        get => (MyLocalModItemViewModel)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private MyLocalModItemViewModel? _subscribedViewModel;
    private bool _suppressCheckedHandler;
    private bool _defaultButtonsCreated;
    private bool _externalButtons;
    private List<MyIconButton>? _buttons;
    private MyLocalCompItem.SwipeSelect? _currentSwipe;
    private MyIconButton? _btnEd;
    private DateTime _lastClickTime = DateTime.MinValue;

    public MyLocalCompItem()
    {
        InitializeComponent();
        // 无参构造时创建默认 VM，避免设计器与其他直接构造控件的调用点崩溃
        ViewModel = new MyLocalModItemViewModel();
        // 虚拟化容器会被回收复用：DataContext 变化（含首次绑定）时同步到 ViewModel
        DataContextChanged += OnDataContextChangedForViewModel;
        PreviewMouseLeftButtonUp += Button_MouseUp;
        PreviewMouseLeftButtonDown += Button_MouseDown;
        MouseLeave += Button_MouseLeave;
        PreviewMouseLeftButtonUp += Button_MouseLeave;
        MouseLeftButtonDown += Button_MouseSwipeStart;
        MouseEnter += Button_MouseSwipe;
        MouseLeave += Button_MouseSwipe;
        MouseLeftButtonUp += Button_MouseSwipe;
        // 不再在 Loaded 时刷新：虚拟化容器会被回收复用，刷新由 ViewModel 驱动
        MouseEnter += RefreshColor;
        MouseLeave += RefreshColor;
        MouseLeftButtonDown += RefreshColor;
        MouseLeftButtonUp += RefreshColor;
        Changed += RefreshColor;
        MouseRightButtonUp += Button_MouseRightButtonUp;
    }

    /// <summary>
    ///     容器复用或首次绑定时，把 DataContext 中的条目 VM 同步到 <see cref="ViewModel" />。
    /// </summary>
    private void OnDataContextChangedForViewModel(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is MyLocalModItemViewModel vm && !ReferenceEquals(vm, ViewModel))
            ViewModel = vm;
    }

    private void OnViewModelChanged(MyLocalModItemViewModel? oldValue, MyLocalModItemViewModel? newValue)
    {
        if (_subscribedViewModel is not null)
            _subscribedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _subscribedViewModel = newValue;
        if (newValue is not null)
            newValue.PropertyChanged += ViewModel_PropertyChanged;

        _lastClickTime = DateTime.MinValue;
        ResetVisualState();
        ApplyViewModel();
        EnsureDefaultButtons();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MyLocalModItemViewModel.IsChecked))
            OnCheckedChanged();
        else if (e.PropertyName is nameof(MyLocalModItemViewModel.EnabledButtonIcon)
                 or nameof(MyLocalModItemViewModel.ShowEnabledButton))
            UpdateEnabledButton();
        else if (e.PropertyName == nameof(MyLocalModItemViewModel.TitleText))
            ApplyTitleText();
        else if (e.PropertyName == nameof(MyLocalModItemViewModel.SubTitleText))
            ApplySubTitleText();
        else if (e.PropertyName == nameof(MyLocalModItemViewModel.DescriptionText))
            ApplyDescriptionText();
    }

    /// <summary>
    ///     设置文本块的文本，并还原 Minecraft 颜色代码（§a 之类）。
    ///     统一走 Inlines：Text 与 Inlines 混用时 TextBlock 内部文本可能不同步，表现为 Text 有值但测量宽度为 0（文本不显示）。
    /// </summary>
    private static void SetBlockText(TextBlock block, string text)
    {
        // 统一走 Inlines：Text 与 Inlines 混用时 TextBlock 的内部文本可能不同步（表现为文本有值但测量宽度为 0）
        block.Inlines.Clear();
        if (string.IsNullOrEmpty(text))
            return;
        if (text.Contains('§'))
        {
            TextStyles.MinecraftFormatter.SetColorfulTextLab(text, block, PCL.Core.UI.Theme.ThemeService.IsDarkMode);
            return;
        }

        block.Inlines.Add(new Run(text));
    }

    /// <summary>把 VM 的标题应用到控件（虚拟化容器复用时不依赖 XAML 绑定）。</summary>
    private void ApplyTitleText()
    {
        SetBlockText(LabTitle, ViewModel?.TitleText ?? "");
        // TextBlock 的文本在文本树被布局后修改时，Auto 列可能仍保留旧的 0 宽度，需显式使标题网格重新测量
        LabTitle.InvalidateMeasure();
        PanTitle.InvalidateMeasure();
    }

    /// <summary>把 VM 的副标题应用到控件。</summary>
    private void ApplySubTitleText()
    {
        SetBlockText(LabSubtitle, ViewModel?.SubTitleText ?? "");
    }

    /// <summary>把 VM 的描述应用到控件。</summary>
    private void ApplyDescriptionText()
    {
        SetBlockText(LabInfo, ViewModel?.DescriptionText ?? "");
    }

    private void UpdateEnabledButton()
    {
        if (_btnEd is null || ViewModel is null)
            return;
        _btnEd.SvgIcon = ViewModel.EnabledButtonIcon;
        _btnEd.Visibility = ViewModel.ShowEnabledButton ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string GetTitleBrushKey(bool isChecked, LocalComp.LocalCompFile? entry)
    {
        var isFine = entry?.State == LocalComp.LocalCompFile.LocalFileStatus.Fine;
        if (isChecked)
            return isFine ? "ColorBrush2" : "ColorBrush5";
        return isFine ? "ColorBrush1" : "ColorBrushGray4";
    }

    /// <summary>把当前 ViewModel 的轻量状态（颜色、勾选、悬停）应用到控件上。</summary>
    private void ApplyViewModel()
    {
        var vm = ViewModel;
        if (vm is null)
            return;
        LabTitle.SetResourceReference(TextBlock.ForegroundProperty, GetTitleBrushKey(vm.IsChecked, vm.Entry));
        ApplyCheckedVisual(false);
        ResetVisualState();
        UpdateEnabledButton();
        // 首次绑定时同步填充文本（绑定丢失或容器复用时同样生效）
        ApplyTitleText();
        ApplySubTitleText();
        ApplyDescriptionText();
    }

    /// <summary>重置悬停相关视觉状态，避免回收后的容器残留上一项的效果。</summary>
    private void ResetVisualState()
    {
        _isMouseDown = false;
        Animations.AniStop("LocalModItem Color " + Uuid);
        if (buttonStack is not null)
        {
            buttonStack.Opacity = 0d;
            buttonStack.IsHitTestVisible = true;
        }

        ColumnPaddingRight.Width = new GridLength(4d);
        var back = RectBack;
        var scale = (ScaleTransform)back.RenderTransform;
        if (ViewModel?.IsChecked == true)
        {
            back.SetResourceReference(Border.BackgroundProperty, "ColorBrushBg1");
            back.Opacity = 1d;
            scale.ScaleX = 1d;
            scale.ScaleY = 1d;
        }
        else
        {
            back.Opacity = 0d;
            scale.ScaleX = 0.8d;
            scale.ScaleY = 0.8d;
        }
    }

    /// <summary>应用勾选状态的视觉表现；<paramref name="animate" /> 为 false 时直接设置。</summary>
    private void ApplyCheckedVisual(bool animate)
    {
        var vm = ViewModel;
        if (vm is null)
            return;
        var isChecked = vm.IsChecked;
        if (!animate || !this.IsVisibleInWindow(Launcher.frmMain))
        {
            RectCheck.VerticalAlignment = VerticalAlignment.Center;
            RectCheck.Margin = new Thickness(-3, 0d, 0d, 0d);
            RectCheck.Height = isChecked ? 32d : 0d;
            RectCheck.Opacity = isChecked ? 1d : 0d;
            LabTitle.SetResourceReference(TextBlock.ForegroundProperty, GetTitleBrushKey(isChecked, vm.Entry));
            Animations.AniStop("MyLocalCompItem Checked " + Uuid);
            return;
        }

        var anim = new List<Animations.AniData>();
        if (isChecked)
        {
            // 由无变有
            var delta = 32d - RectCheck.ActualHeight;
            anim.Add(Animations.AaHeight(RectCheck, delta * 0.4d, 200,
                ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Weak)));
            anim.Add(Animations.AaHeight(RectCheck, delta * 0.6d, 300,
                ease: new Animations.AniEaseOutBack(Animations.AniEasePower.Weak)));
            anim.Add(Animations.AaOpacity(RectCheck, 1d - RectCheck.Opacity, 30));
            RectCheck.VerticalAlignment = VerticalAlignment.Center;
            RectCheck.Margin = new Thickness(-3, 0d, 0d, 0d);
            anim.Add(Animations.AaColor(LabTitle, TextBlock.ForegroundProperty,
                GetTitleBrushKey(true, vm.Entry), 200));
        }
        else
        {
            // 由有变无
            anim.Add(Animations.AaHeight(RectCheck, -RectCheck.ActualHeight, 120,
                ease: new Animations.AniEaseInFluent(Animations.AniEasePower.Weak)));
            anim.Add(Animations.AaOpacity(RectCheck, -RectCheck.Opacity, 70, 40));
            RectCheck.VerticalAlignment = VerticalAlignment.Center;
            anim.Add(Animations.AaColor(LabTitle, TextBlock.ForegroundProperty,
                GetTitleBrushKey(false, vm.Entry), 120));
        }

        Animations.AniStart(anim, "MyLocalCompItem Checked " + Uuid);
    }

    /// <summary>勾选状态变化（来自 ViewModel）。</summary>
    private void OnCheckedChanged()
    {
        if (_suppressCheckedHandler)
            return;
        var vm = ViewModel;
        if (vm is null)
            return;
        try
        {
            var value = vm.IsChecked;
            // 触发属性值修改
            var changedEventArgs = new Common.RouteEventArgs();
            if (IsInitialized)
            {
                Changed?.Invoke(this, changedEventArgs);
                if (changedEventArgs.handled)
                {
                    _suppressCheckedHandler = true;
                    vm.IsChecked = !value;
                    _suppressCheckedHandler = false;
                    return;
                }
            }

            if (value)
            {
                var checkEventArgs = new Common.RouteEventArgs();
                Check?.Invoke(this, checkEventArgs);
                if (checkEventArgs.handled)
                    return;
            }

            ApplyCheckedVisual(true);
        }
        catch (Exception ex)
        {
            Common.Log(ex, "设置 Checked 失败");
        }
    }

    public void Refresh() => ViewModel?.Refresh();

    public void RefreshColor(object sender, EventArgs e)
    {
        InitLate(sender, e);
        // 触发颜色动画
        var time = IsMouseOver ? 120 : 180;
        var ani = new List<Animations.AniData>();
        // ButtonStack
        if (buttonStack is not null)
        {
            if (IsMouseOver)
            {
                ani.Add(Animations.AaOpacity(buttonStack, 1d - buttonStack.Opacity,
                    (int)Math.Round(time * 0.7d),
                    (int)Math.Round(time * 0.3d)));
                ani.Add(Animations.AaDouble(
                    i => ColumnPaddingRight.Width =
                        new GridLength(Math.Max(0, ColumnPaddingRight.Width.Value + (double)i)),
                    5 + VisibleButtonCount * 25 - ColumnPaddingRight.Width.Value, (int)Math.Round(time * 0.3d),
                    (int)Math.Round(time * 0.7d)));
            }
            else
            {
                ani.Add(Animations.AaOpacity(buttonStack, -buttonStack.Opacity,
                    (int)Math.Round(time * 0.4d)));
                ani.Add(Animations.AaDouble(
                    i => ColumnPaddingRight.Width =
                        new GridLength(Math.Max(0, ColumnPaddingRight.Width.Value + (double)i)),
                    4d - ColumnPaddingRight.Width.Value, (int)Math.Round(time * 0.4d)));
            }
        }

        // RectBack
        if (IsMouseOver || ViewModel?.IsChecked == true)
        {
            ani.AddRange([
                Animations.AaColor(RectBack, Border.BackgroundProperty,
                    _isMouseDown ? "ColorBrush6" : "ColorBrushBg1",
                    time),
                Animations.AaOpacity(RectBack, 1d - RectBack.Opacity, time,
                    ease: new Animations.AniEaseOutFluent())
            ]);
            if (_isMouseDown)
                ani.Add(Animations.AaScaleTransform(RectBack,
                    0.996d - ((ScaleTransform)RectBack.RenderTransform).ScaleX, (int)Math.Round(time * 1.2d),
                    ease: new Animations.AniEaseOutFluent()));
            else
                ani.Add(Animations.AaScaleTransform(RectBack,
                    1d - ((ScaleTransform)RectBack.RenderTransform).ScaleX,
                    (int)Math.Round(time * 1.2d), ease: new Animations.AniEaseOutFluent()));
        }
        else
        {
            ani.AddRange([
                Animations.AaOpacity(RectBack, -RectBack.Opacity, time),
                Animations.AaScaleTransform(RectBack,
                    0.996d - ((ScaleTransform)RectBack.RenderTransform).ScaleX,
                    time, ease: new Animations.AniEaseOutFluent()),
                Animations.AaScaleTransform(RectBack, -0.196d, 1, after: true)
            ]);
        }

        Animations.AniStart(ani, "LocalModItem Color " + Uuid);
    }

    // 触发虚拟化内容
    private void InitLate(object sender, EventArgs e)
    {
        if (buttonHandler is not null)
        {
            buttonHandler(this, e);
            buttonHandler = null;
        }
    }

    // 显示更新日志
    private void BtnUpdate_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ViewModel?.ShowUpdateLog();
    }

    // 触发更新
    private void BtnUpdate_Click(object sender, EventArgs e) => ViewModel?.UpdateCommand.Execute(null);

    // 自适应（#4465）
    private void PanTitle_SizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs)
    {
        var vm = ViewModel;
        if (vm is null)
            return;
        // 0：全部舒展：Auto - Auto - (Auto) - 1*
        // 1：压缩 Subtitle：Auto - 1* - (Auto) - 0
        // 2：继续压缩 Title：1* - 0 - (Auto) - 0
        var currentCompressLevel =
            vm.ColumnExtendWidth.IsStar ? 0 : vm.ColumnTitleWidth.IsStar ? 2 : 1; // Subtitle 可能是 Collapsed
        var newCompressLevel = 0;
        switch (currentCompressLevel)
        {
            case 0:
                {
                    if (ColumnExtend.ActualWidth < 0.5d)
                        newCompressLevel = LabSubtitle.Visibility == Visibility.Collapsed ? 2 : 1;
                    else
                        return;

                    break;
                }
            case 1:
                {
                    if (ColumnSubtitle.ActualWidth < 0.5d)
                        newCompressLevel = 2;
                    else if (!LabSubtitle.IsTextTrimmed())
                        newCompressLevel = 0;
                    else
                        return;

                    break;
                }
            case 2:
                {
                    if (!LabTitle.IsTextTrimmed())
                        newCompressLevel = LabSubtitle.Visibility == Visibility.Collapsed ? 0 : 1;
                    else
                        return;

                    break;
                }
        }

        switch (newCompressLevel)
        {
            case 0:
                {
                    // 全部舒展：Auto - Auto - (Auto) - 1*
                    vm.ColumnTitleWidth = GridLength.Auto;
                    vm.ColumnSubtitleWidth = GridLength.Auto;
                    vm.ColumnExtendWidth = new GridLength(1d, GridUnitType.Star);
                    break;
                }
            case 1:
                {
                    // 压缩 Subtitle：Auto - 1* - (Auto) - 0
                    vm.ColumnTitleWidth = GridLength.Auto;
                    vm.ColumnSubtitleWidth = new GridLength(1d, GridUnitType.Star);
                    vm.ColumnExtendWidth = new GridLength(0d, GridUnitType.Pixel);
                    break;
                }
            case 2:
                {
                    // 继续压缩 Title：1* - 0 - (Auto) - 0
                    vm.ColumnTitleWidth = new GridLength(1d, GridUnitType.Star);
                    vm.ColumnSubtitleWidth = new GridLength(0d, GridUnitType.Pixel);
                    vm.ColumnExtendWidth = new GridLength(0d, GridUnitType.Pixel);
                    break;
                }
        }
    }

    #region 基础属性

    public readonly int Uuid = Common.GetUuid();

    // Logo
    public string Logo
    {
        get => ViewModel?.Logo ?? "";
        set
        {
            if (ViewModel is not null)
                ViewModel.Logo = value;
        }
    }

    // 标题
    public string Title
    {
        get => ViewModel?.TitleText ?? "";
        set
        {
            if (ViewModel is not null)
                ViewModel.TitleText = value;
        }
    }

    // 副标题
    public string? SubTitle
    {
        get => ViewModel?.SubTitleText;
        set
        {
            if (ViewModel is null)
                return;
            ViewModel.SubTitleText = value ?? "";
            ViewModel.SubTitleVisibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    // 描述
    public string Description
    {
        get => ViewModel?.DescriptionText ?? "";
        set
        {
            if (ViewModel is not null)
                ViewModel.DescriptionText = value;
        }
    }

    // Tag
    public List<string> Tags
    {
        set => ViewModel?.SetTags(value);
    }

    // 相关联的 Mod
    public LocalComp.LocalCompFile Entry
    {
        get => ViewModel?.Entry!;
        set
        {
            if (ViewModel is null || ReferenceEquals(ViewModel.Entry, value))
                return;
            ViewModel.Initialize(value, null);
        }
    }

    #endregion

    #region 点击与勾选

    // 触发点击事件
    public event ClickEventHandler? Click;

    public delegate void ClickEventHandler(object sender, MouseButtonEventArgs e);

    private void Button_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isMouseDown)
            return;
        Click?.Invoke(sender, e);
        if (e.Handled)
            return;
        // 没有外部订阅时由控件自行处理点击（虚拟化列表由 DataTemplate 创建，无法预先挂接事件）
        if (Click is null)
            HandleDefaultClick();
        Common.Log("[Control] 按下本地 Mod 列表项：" + LabTitle.Text);
    }

    /// <summary>默认点击行为：文件夹双击进入，其余情况切换选中状态。</summary>
    private void HandleDefaultClick()
    {
        var vm = ViewModel;
        if (vm?.Entry is null || vm.Host is null)
            return;
        var currentTime = DateTime.Now;
        var timeDiff = (currentTime - _lastClickTime).TotalMilliseconds;
        if (vm.Entry.IsFolder && timeDiff <= 300d)
        {
            // 300ms 内双击，进入文件夹
            vm.EnterFolderCommand.Execute(null);
            _lastClickTime = DateTime.MinValue;
        }
        else
        {
            // 单击切换选中状态
            vm.IsChecked = !vm.IsChecked;
            _lastClickTime = currentTime;
        }
    }

    // 鼠标点击判定
    private bool _isMouseDown;

    private void Button_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsMouseDirectlyOver)
            return;
        _isMouseDown = true;
        buttonStack?.IsHitTestVisible = false;
    }

    private void Button_MouseLeave(object sender, object e)
    {
        _isMouseDown = false;
        buttonStack?.IsHitTestVisible = true;
    }

    // 右键查看详情
    private void Button_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel?.Host is null)
            return;
        e.Handled = true;
        ViewModel.ShowInfoCommand.Execute(null);
    }

    // 滑动选中
    public class SwipeSelect
    {
        public int Start { get; set; }
        public int End { get; set; }

        public bool Swiping
        {
            get;
            set
            {
                field = value;
                if (TargetFrm is not null)
                    try
                    {
                        var cardSelect = Interaction.CallByName(TargetFrm, "CardSelect", CallType.Get);
                        Interaction.CallByName(cardSelect, "IsHitTestVisible", CallType.Set, !value);
                    }
                    catch
                    {
                    }
            }
        }

        public bool SwipeToState { get; set; }
        public object TargetFrm { get; set; } = null!;
    }

    /// <summary>滑动选中状态；虚拟化列表中由宿主页面提供共享实例。</summary>
    public SwipeSelect CurrentSwipe
    {
        get
        {
            if (_currentSwipe is not null)
                return _currentSwipe;
            _currentSwipe = ViewModel?.Host?.SwipeState ?? new SwipeSelect();
            return _currentSwipe;
        }
        set => _currentSwipe = value;
    }

    private void Button_MouseSwipeStart(object sender, object e)
    {
        var vm = ViewModel;
        if (vm?.Host is not null)
        {
            vm.Host.SwipeStart(vm);
            return;
        }

        if (Parent is null)
            return; // Mod 可能已被删除（#3824）
        // 开始滑动
        var index = ((StackPanel)Parent).Children.IndexOf(this);
        CurrentSwipe.Start = index;
        CurrentSwipe.End = index;
        CurrentSwipe.Swiping = true;
        CurrentSwipe.SwipeToState = !Checked;
    }

    private void Button_MouseSwipe(object sender, object e)
    {
        var vm = ViewModel;
        var isPressed = Mouse.LeftButton == MouseButtonState.Pressed && Mouse.DirectlyOver is MyLocalCompItem;
        if (vm?.Host is not null)
        {
            vm.Host.SwipeMove(vm, isPressed);
            return;
        }

        if (Parent is null)
            return; // Mod 可能已被删除（#3824）
        // 结束滑动
        if (!isPressed) // #5771
        {
            CurrentSwipe.Swiping = false;
            return;
        }

        // 计算滑动范围
        var elements = ((StackPanel)Parent).Children;
        var index = elements.IndexOf(this);
        CurrentSwipe.Start =
            (int)Math.Round(Common.MathClamp(Math.Min(CurrentSwipe.Start, index), 0d,
                elements.Count - 1));
        CurrentSwipe.End =
            (int)Math.Round(Common.MathClamp(Math.Max(CurrentSwipe.End, index), 0d,
                elements.Count - 1));
        // 勾选所有范围中的项
        if (CurrentSwipe.Start == CurrentSwipe.End)
            return;
        for (int i = CurrentSwipe.Start, loopTo = CurrentSwipe.End; i <= loopTo; i++)
        {
            var item = (MyLocalCompItem)elements[i];
            item.InitLate(item, (EventArgs)e);
            item.Checked = CurrentSwipe.SwipeToState;
        }
    }

    // 勾选状态
    public event CheckEventHandler? Check;

    public delegate void CheckEventHandler(object sender, Common.RouteEventArgs e);

    public event ChangedEventHandler? Changed;

    public delegate void ChangedEventHandler(object sender, Common.RouteEventArgs e);

    public bool Checked
    {
        get => ViewModel?.IsChecked == true;
        set
        {
            if (ViewModel is null || ViewModel.IsChecked == value)
                return;
            ViewModel.IsChecked = value;
        }
    }

    #endregion

    #region 后加载内容

    // 指向背景
    public Border RectBack
    {
        get
        {
            if (field is null)
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
                field = rect;
                // <!--<corelocal:BlurBorder x:Name = "RectBack" CornerRadius="3" RenderTransformOrigin="0.5,0.5" SnapsToDevicePixels="True" 
                // IsHitTestVisible = "False" Opacity="0" BorderThickness="1" 
                // Grid.ColumnSpan = "4" Background="{DynamicResource ColorBrush7}" BorderBrush="{DynamicResource ColorBrush6}"/>-->
            }

            return field;
        }
    }

    // 按钮
    public Action<MyLocalCompItem, EventArgs>? buttonHandler;
    public FrameworkElement? buttonStack;

    /// <summary>当前按钮数量中实际可见的数量（虚拟化列表由 ViewModel 决定）。</summary>
    private int VisibleButtonCount => _defaultButtonsCreated
        ? ViewModel?.VisibleButtonCount ?? 3
        : Buttons.Count();

    public IEnumerable<MyIconButton> Buttons
    {
        get => _buttons ?? Enumerable.Empty<MyIconButton>();
        set
        {
            _externalButtons = true;
            ApplyButtons(value.ToList());
        }
    }

    /// <summary>安装按钮栈；默认按钮与外部设置共用此入口，避免默认按钮被误标记为外部按钮。</summary>
    private void ApplyButtons(List<MyIconButton> buttons)
    {
        _buttons = buttons;
        // 移除原 Stack
        if (buttonStack is not null)
        {
            Children.Remove(buttonStack);
            buttonStack = null;
        }

        if (!_buttons.Any())
            return;
        // 添加新 Stack
        buttonStack = new StackPanel
        {
            Opacity = 0d,
            Margin = new Thickness(0d, 0d, 5d, 0d),
            SnapsToDevicePixels = false,
            Orientation = (Orientation)System.Windows.Forms.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            UseLayoutRounding = false
        };
        SetColumnSpan(buttonStack, 10);
        SetRowSpan(buttonStack, 10);
        // 构造按钮
        foreach (var btn in _buttons)
        {
            if (btn.Height.Equals(double.NaN))
                btn.Height = 25d;
            if (btn.Width.Equals(double.NaN))
                btn.Width = 25d;
            ((StackPanel)buttonStack).Children.Add(btn);
        }

        Children.Add(buttonStack);
    }

    /// <summary>虚拟化列表使用：构造由 ViewModel 命令驱动的默认按钮组（只构造一次）。</summary>
    private void EnsureDefaultButtons()
    {
        var vm = ViewModel;
        if (vm?.Host is null || _defaultButtonsCreated || _externalButtons)
            return;
        _defaultButtonsCreated = true;

        var btnCont = CreateDefaultButton("lucide/info", "ViewModel.InfoToolTip.Value", "ViewModel.ShowInfoCommand");
        var btnOpen = CreateDefaultButton("lucide/folder-open", "ViewModel.OpenFolderToolTip.Value",
            "ViewModel.OpenFileLocationCommand", 1.05d);
        _btnEd = CreateDefaultButton("lucide/circle-minus", "ViewModel.EnabledButtonToolTip.Value",
            "ViewModel.ToggleEnabledCommand");
        var btnDelete = CreateDefaultButton("lucide/trash-2", "ViewModel.DeleteToolTip.Value", "ViewModel.DeleteCommand");

        // 直接安装按钮栈，不经过 Buttons 的 setter，避免默认按钮被标记为外部设置
        ApplyButtons(new List<MyIconButton> { btnCont, btnOpen, _btnEd, btnDelete });
        UpdateEnabledButton();
    }

    private MyIconButton CreateDefaultButton(string icon, string toolTipPath, string commandPath,
        double logoScale = 1d)
    {
        var button = new MyIconButton { LogoScale = logoScale, SvgIcon = icon };
        button.SetBinding(FrameworkElement.ToolTipProperty,
            new Binding(toolTipPath) { Source = this, Mode = BindingMode.OneWay });
        button.SetBinding(MyIconButton.CommandProperty,
            new Binding(commandPath) { Source = this, Mode = BindingMode.OneWay });
        ToolTipService.SetPlacement(button, System.Windows.Controls.Primitives.PlacementMode.Center);
        ToolTipService.SetVerticalOffset(button, 30d);
        ToolTipService.SetHorizontalOffset(button, 2d);
        return button;
    }

    // 勾选条
    public Border RectCheck
    {
        get
        {
            if (field is null)
            {
                field = new Border
                {
                    Width = 5d,
                    Height = Checked ? double.NaN : 0d,
                    CornerRadius = new CornerRadius(2d, 2d, 2d, 2d),
                    VerticalAlignment = Checked ? VerticalAlignment.Stretch : VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    UseLayoutRounding = false,
                    SnapsToDevicePixels = false,
                    Margin = Checked ? new Thickness(-3, 6d, 0d, 6d) : new Thickness(-3, 0d, 0d, 0d)
                };
                field.SetResourceReference(Border.BackgroundProperty, "ColorBrush3");
                SetRowSpan(field, 10);
                Children.Add(field);
            }

            return field;
        }
    }

    #endregion
}
