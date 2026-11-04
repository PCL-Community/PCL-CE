using PCL.Core.App.Localization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using static PCL.Loading;

namespace PCL;

public class MyPageRight : AdornerDecorator
{
    // 当前状态
    public enum PageStates
    {
        /// <summary>
        /// 默认状态，页面全空
        /// </summary>
        Empty,

        /// <summary>
        /// 加载环初始等待
        /// </summary>
        LoaderWait,

        /// <summary>
        /// 加载环进入动画
        /// </summary>
        LoaderEnter,

        /// <summary>
        /// 加载环正常显示（强制等待）
        /// </summary>
        LoaderStayForce,

        /// <summary>
        /// 加载环正常显示
        /// </summary>
        LoaderStay,

        /// <summary>
        /// 加载环退出动画
        /// </summary>
        LoaderExit,

        /// <summary>
        /// 内容进入动画
        /// </summary>
        ContentEnter,

        /// <summary>
        /// 内容正常显示
        /// </summary>
        ContentStay,

        /// <summary>
        /// 刷新导致的全部退出动画，或页面内容退出（子页面更改）导致的全部退出动画
        /// </summary>
        ContentExit,

        /// <summary>
        /// 切换页面导致的全部退出动画
        /// </summary>
        PageExit
    }

    private static readonly DependencyProperty PanScrollProperty =
    DependencyProperty.Register(nameof(PanScroll), typeof(MyScrollViewer), typeof(MyPageRight));

    private bool _panScrollNullWarned;

    public int pageUuid = Common.GetUuid();

    /// <summary>
    /// “返回顶部” 按钮检测的滚动区域
    /// </summary>
    public MyScrollViewer? PanScroll
    {
        get
        {
            var res = GetValue(PanScrollProperty);
            if (res is null && !_panScrollNullWarned)
            {
                _panScrollNullWarned = true;
                Common.Log($"[MyPageRight] 获取到 PanScroll(来自 {Name}) 的值为 null", Common.LogLevel.Debug);
            }

            return res as MyScrollViewer;
        }
        set => SetValue(PanScrollProperty, value);
    }

    public PageStates PageState
    {
        get => field;
        set
        {
            if (field == value)
                return;
            field = value;
            // 通知等待退出动画结束的加载器线程：页面已离开退出状态（或已进入退出状态）
            if (value is PageStates.PageExit or PageStates.ContentExit)
                _pageExitDone.Reset();
            else
                _pageExitDone.Set();
            if (Common.modeDebug)
                Common.Log("[UI] 页面状态切换为 " + Common.GetStringFromEnum(value));
        }
    } = PageStates.Empty;

    /// <summary>
    ///     页面是否处于退出动画中。用于让加载器线程等待退出动画结束，而不是忙等。
    ///     初始为已置位（没有退出动画在进行）。
    /// </summary>
    private readonly ManualResetEventSlim _pageExitDone = new(true);

    #region 加载器

    private LoaderBase? _pageLoader;
    private Func<object>? _pageLoaderInputInvoke;
    private MyLoading? _pageLoaderUi;
    private FrameworkElement? _panLoader;
    private FrameworkElement? _panContent;
    private FrameworkElement? _panAlways;
    private bool _pageLoaderAutoRun;

    /// <summary>
    ///     表明页面存在需要在后台执行的加载器。
    /// </summary>
    /// <param name="loaderUi">MyLoading 控件。</param>
    /// <param name="panLoader">MyLoading 控件对应的卡片。</param>
    /// <param name="panContent">加载结束后出现的内容容器。</param>
    /// <param name="panAlways">无论是否在加载总是要显示的容器。可以为 Nothing。</param>
    /// <param name="realLoader">在工作线程执行的加载器。</param>
    /// <param name="finishedInvoke">当加载器执行完成，在 UI 线程触发的 UI 初始化事件。</param>
    public void PageLoaderInit(MyLoading loaderUi,
        FrameworkElement panLoader,
        FrameworkElement panContent,
        FrameworkElement? panAlways,
        LoaderBase realLoader,
        Action<LoaderBase>? finishedInvoke = null,
        Func<object>? inputInvoke = null,
        bool autoRun = true)
    {
        // 初始化参数
        _panLoader = panLoader;
        this._panContent = panContent;
        _panAlways = panAlways;
        _pageLoader = realLoader;
        _pageLoaderUi = loaderUi;
        _pageLoaderInputInvoke = inputInvoke;
        _pageLoaderAutoRun = autoRun;
        // 添加结束 Invoke
        if (finishedInvoke is not null)
            realLoader.PreviewFinish += _ =>
            {
                // 不在退出动画时执行 UI 线程操作，避免退出动画被重置。
                _pageExitDone.Wait(TimeSpan.FromSeconds(2));
                Common.RunInUiWait(() => finishedInvoke(realLoader));

                // 让 UI 线程完成一次布局与渲染后再触发 State 改变事件，避免大量控件初始化导致的掉帧。
                // 使用 Render 优先级排队（低于 Normal），因此会排在 finishedInvoke 引发的布局之后。
                if (!Common.RunInUi())
                    System.Windows.Application.Current?.Dispatcher.Invoke(() => { },
                        System.Windows.Threading.DispatcherPriority.Render);
            };
        realLoader.OnStateChangedUi += (loader, newState, oldState) =>
            Common.RunInUi(() => PageLoaderState(loader, newState, oldState));
        // 隐藏 UI
        panLoader.Visibility = Visibility.Collapsed;
        panContent.Visibility = Visibility.Collapsed;
        panAlways?.Visibility = Visibility.Collapsed;
        // 初次运行加载器
        if (_pageLoaderAutoRun)
        {
            if (_pageLoader is LoaderTask task)
            {
                task.Start(task.StartGetInputNoType(null, _pageLoaderInputInvoke));
            }
            else
            {
                object? input = null;
                if (_pageLoaderInputInvoke is not null)
                    input = _pageLoaderInputInvoke();
                _pageLoader.Start(input);
            }
        }

        if (_pageLoader.State == LoadState.Finished && finishedInvoke is not null)
            Common.RunInUiWait(() => finishedInvoke(realLoader)); // 加载器已提前完成，直接触发事件
        // 设置加载环
        _pageLoaderUi.State = realLoader;
        _pageLoaderUi.Click += (_, _) =>
        {
            if (realLoader.State == LoadState.Failed) PageLoaderRestart();
        }; // 点击重试事件
    }

    private void RefreshLocalizationIfStale()
    {
        if (_localizationRevision == LocalizationService.UiRevision) return;
        _localizationRevision = LocalizationService.UiRevision;
        try
        {
            RefreshLocalization();
        }
        catch (Exception ex)
        {
            Common.Log(ex, "刷新页面本地化文本失败");
        }
    }

    protected virtual void RefreshLocalization()
    {
    }

    private int _localizationRevision = LocalizationService.UiRevision;

    public void PageLoaderRestart(object? input = null, bool isForceRestart = true)
    {
        if (!_pageLoaderAutoRun)
            return;
        if (_pageLoader is LoaderTask task)
        {
            _pageLoader.Start(task.StartGetInputNoType(input, _pageLoaderInputInvoke),
                isForceRestart);
        }
        else
        {
            if (input is null && _pageLoaderInputInvoke is not null)
                input = _pageLoaderInputInvoke;
            _pageLoader?.Start(input, isForceRestart);
        }
    }

    #endregion

    #region 事件

    /// <summary>
    ///     需要切换到当前页面，并且原本的 Loaded 事件已执行完成。
    ///     需要根据加载器状态，从 Empty 切换到 ContentEnter、LoaderWait、LoaderEnter。
    /// </summary>
    public void PageOnEnter()
    {
        if (Common.modeDebug)
            Common.Log("[UI] 已触发 PageOnEnter");
        RefreshLocalizationIfStale();
        PageEnter?.Invoke();
        switch (PageState)
        {
            case PageStates.Empty:
                {
                    if (_pageLoader is null || _pageLoader.State == LoadState.Finished ||
                        _pageLoader.State == LoadState.Waiting ||
                        _pageLoader.State == LoadState.Aborted)
                    {
                        // 如果加载器在进入页面时不启动（例如联机），那么在此时就会有 State = Waiting
                        PageState = PageStates.ContentEnter;
                        TriggerEnterAnimation(_panAlways, (FrameworkElement)(_panContent ?? Child));
                    }
                    else if (_pageLoader.State == LoadState.Loading)
                    {
                        PageState = PageStates.LoaderWait;
                        Animations.AniStart(Animations.AaCode(PageOnLoaderWaitFinished, 400),
                            "PageRight PageChange " + pageUuid);
                    }
                    else // PageLoader.State = LoadState.Failed
                    {
                        PageState = PageStates.LoaderEnter;
                        TriggerEnterAnimation(_panAlways, _panLoader);
                    }

                    break;
                }
            case PageStates.ContentExit:
                {
                    // 和上面的一样，但是不管 PanAlways
                    if (_pageLoader is null || _pageLoader.State == LoadState.Finished ||
                        _pageLoader.State == LoadState.Waiting ||
                        _pageLoader.State == LoadState.Aborted)
                    {
                        PageState = PageStates.ContentEnter;
                        TriggerEnterAnimation((FrameworkElement)(_panContent ?? Child));
                    }
                    else if (_pageLoader.State == LoadState.Loading)
                    {
                        PageState = PageStates.LoaderWait;
                        Animations.AniStart(Animations.AaCode(PageOnLoaderWaitFinished, 400),
                            "PageRight PageChange " + pageUuid);
                    }
                    else // PageLoader.State = LoadState.Failed
                    {
                        PageState = PageStates.LoaderEnter;
                        TriggerEnterAnimation(_panLoader);
                    }

                    break;
                }
            case PageStates.ContentEnter: // 重复调用 PageOnEnter，直接忽略
                {
                    break;
                }

            default:
                {
                    throw new Exception("在状态为 " + Common.GetStringFromEnum(PageState) +
                                        " 时触发了 PageOnEnter 事件。");
                }
        }
    }

    public event PageEnterEventHandler? PageEnter;

    public delegate void PageEnterEventHandler();

    /// <summary>
    ///     需要切换到其他页面。
    ///     需要立即切换至 PageExit 或 Empty。
    /// </summary>
    public void PageOnExit()
    {
        if (Common.modeDebug)
            Common.Log("[UI] 已触发 PageOnExit");
        PageExit?.Invoke();
        switch (PageState)
        {
            case PageStates.ContentEnter:
            case PageStates.ContentStay:
                {
                    PageState = PageStates.PageExit;
                    TriggerExitAnimation(_panAlways, (FrameworkElement)(_panContent ?? Child));
                    break;
                }
            case PageStates.LoaderEnter:
            case PageStates.LoaderStayForce:
            case PageStates.LoaderStay:
                {
                    PageState = PageStates.PageExit;
                    TriggerExitAnimation(_panAlways, _panLoader);
                    break;
                }
            case PageStates.LoaderWait:
                {
                    PageState = PageStates.PageExit;
                    TriggerExitAnimation(_panAlways);
                    break;
                }
            case PageStates.LoaderExit:
            case PageStates.ContentExit:
                {
                    PageState = PageStates.PageExit;
                    if (_panAlways is not null)
                        TriggerExitAnimation(_panAlways, (FrameworkElement)(_panContent ?? Child));
                    break;
                }
            case PageStates.PageExit:
            case PageStates.Empty:
                {
                    break;
                }
        }
    }

    public event PageExitEventHandler? PageExit;

    public delegate void PageExitEventHandler();

    /// <summary>
    ///     即将切换到其他页面，需要强制完成页面状态清理。
    ///     需要立即切换至 Empty。
    /// </summary>
    public void PageOnForceExit()
    {
        if (PageState == PageStates.Empty)
            return;
        if (Common.modeDebug)
            Common.Log("[UI] 已触发 PageOnForceExit");
        PageState = PageStates.Empty;
        Animations.AniStop("PageRight PageChange " + pageUuid);
        // 由于动画会被强制中止，所以需要手动进行隐藏
        if (_pageLoader is null && Child is not null)
        {
            Child.Visibility = Visibility.Collapsed;
        }
        else
        {
            _panContent.Visibility = Visibility.Collapsed;
            _panLoader.Visibility = Visibility.Collapsed;
            if (_panAlways is not null)
                _panAlways.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    ///     PanContent 中的子页面改变，需要让当前内容退出，再显示新的内容。
    ///     需要在 PageEnter 事件确认要显示的子页面有哪些。
    /// </summary>
    public void PageOnContentExit()
    {
        if (Common.modeDebug)
            Common.Log("[UI] 已触发 PageOnContentExit");
        if (_pageLoader is not null && _pageLoader.State == LoadState.Loading)
            throw new Exception("在调用 PageOnContentExit 时，加载器不能为 Loading 状态");
        // Loading 的加载器可能触发进一步变化，难以预测会触发子页面的动画还是加载器完成的动画
        switch (PageState)
        {
            case PageStates.ContentEnter:
            case PageStates.ContentStay:
                {
                    PageState = PageStates.ContentExit;
                    TriggerExitAnimation(_panContent);
                    break;
                }
            case PageStates.LoaderExit:
                {
                    PageState = PageStates.ContentExit;
                    break;
                }
            case PageStates.LoaderEnter:
            case PageStates.LoaderStayForce:
            case PageStates.LoaderStay:
                {
                    PageState = PageStates.ContentExit;
                    TriggerExitAnimation(_panLoader);
                    break;
                }
            case PageStates.LoaderWait:
            case PageStates.Empty:
                {
                    PageOnEnter();
                    break;
                }
        }
    }

    /// <summary>
    ///     逐个进入动画已执行完成。
    ///     需要根据目前状态，从 ContentEnter 切换到 ContentStay，或从 LoaderEnter 切换到 LoaderStayForce。
    /// </summary>
    private void PageOnEnterAnimationFinished()
    {
        if (Common.modeDebug)
            Common.Log("[UI] 已触发 PageOnEnterAnimationFinished");
        switch (PageState)
        {
            case PageStates.ContentEnter:
                {
                    PageState = PageStates.ContentStay;
                    break;
                }
            case PageStates.LoaderEnter:
                {
                    PageState = PageStates.LoaderStayForce;
                    Animations.AniStart(Animations.AaCode(PageOnLoaderStayFinished, 400),
                        "PageRight PageChange " + pageUuid);
                    break;
                }

            default:
                {
                    throw new Exception("在状态为 " + Common.GetStringFromEnum(PageState) +
                                        " 时触发了 PageOnEnterAnimationFinished 事件。");
                }
        }
    }

    /// <summary>
    ///     逐个退出动画已执行完成。
    ///     需要根据目前状态，从 AllExit 切换到 Empty，或从 LoaderExit 切换到 ContentEnter，或从 ContentExit 重新触发 PageOnEnter。
    /// </summary>
    private void PageOnExitAnimationFinished()
    {
        if (Common.modeDebug)
            Common.Log("[UI] 已触发 PageOnExitAnimationFinished");
        switch (PageState)
        {
            case PageStates.PageExit:
                {
                    PageState = PageStates.Empty;
                    break;
                }
            case PageStates.ContentExit:
                {
                    PageOnEnter();
                    break;
                }
            case PageStates.LoaderExit:
                {
                    PageState = PageStates.ContentEnter;
                    TriggerEnterAnimation(_panContent);
                    break;
                }

            default:
                {
                    throw new Exception("在状态为 " + Common.GetStringFromEnum(PageState) +
                                        " 时触发了 PageOnExitAnimationFinished 事件。");
                }
        }
    }

    /// <summary>
    ///     加载环进入等待已结束。
    ///     需要从 LoaderWait 切换到 LoaderEnter。
    /// </summary>
    private void PageOnLoaderWaitFinished()
    {
        if (Common.modeDebug)
            Common.Log("[UI] 已触发 PageOnLoaderWaitFinished");
        switch (PageState)
        {
            case PageStates.LoaderWait:
                {
                    PageState = PageStates.LoaderEnter;
                    if (_panAlways is not null && _panAlways.Visibility == Visibility.Collapsed)
                        TriggerEnterAnimation(_panAlways, _panLoader);
                    else
                        TriggerEnterAnimation(_panLoader);

                    break;
                }

            default:
                {
                    throw new Exception("在状态为 " + Common.GetStringFromEnum(PageState) +
                                        " 时触发了 PageOnLoaderWaitFinished 事件。");
                }
        }
    }

    /// <summary>
    ///     加载环展示等待已结束。
    ///     需要从 LoaderStayForce 切换到 LoaderStay 或 LoaderExit。
    /// </summary>
    private void PageOnLoaderStayFinished()
    {
        if (Common.modeDebug)
            Common.Log("[UI] 已触发 PageOnLoaderStayFinished");
        switch (PageState)
        {
            case PageStates.LoaderStayForce:
                {
                    if (_pageLoader.State == LoadState.Finished)
                    {
                        PageState = PageStates.LoaderExit;
                        TriggerExitAnimation(_panLoader);
                    }
                    else
                    {
                        PageState = PageStates.LoaderStay;
                    }

                    break;
                }

            default:
                {
                    throw new Exception("在状态为 " + Common.GetStringFromEnum(PageState) +
                                        " 时触发了 PageOnLoaderWaitFinished 事件。");
                }
        }
    }

    /// <summary>
    ///     全局加载状态已改变。
    /// </summary>
    private void PageLoaderState(object sender, LoadState newState,
        LoadState oldState)
    {
        switch (newState)
        {
            case LoadState.Failed:
            case LoadState.Loading:
                {
                    if (oldState == LoadState.Failed || oldState == LoadState.Loading)
                        return;
                    if (Common.modeDebug)
                        Common.Log("[UI] 已触发 PageLoaderState (Start/Refresh)");
                    // （重新）开始运行
                    // 需要从部分状态切换到 ReloadExit
                    switch (PageState)
                    {
                        case PageStates.ContentEnter:
                        case PageStates.ContentStay:
                            {
                                PageState = PageStates.ContentExit;
                                TriggerExitAnimation(_panContent);
                                break;
                            }
                        case PageStates.LoaderExit:
                            {
                                PageState = PageStates.ContentExit;
                                break;
                            }
                    }

                    break;
                }
            case LoadState.Finished:
            case LoadState.Aborted:
            case LoadState.Waiting:
                {
                    if (oldState != LoadState.Failed && oldState != LoadState.Loading)
                        return;
                    if (Common.modeDebug)
                        Common.Log("[UI] 已触发 PageLoaderState (Stop/Abort)");
                    // 运行结束
                    // 需要从 LoaderWait 切换到 ContentEnter，或从 LoaderStay 切换到 LoaderExit
                    switch (PageState)
                    {
                        case PageStates.LoaderWait:
                            {
                                PageState = PageStates.ContentEnter;
                                if (_panAlways is not null && _panAlways.Visibility == Visibility.Collapsed)
#pragma warning disable CS8604 // 引用类型参数可能为 null。
                                    // at there, this method will check nullable
                                    // so we ignore the warning
                                    TriggerEnterAnimation(_panAlways, _panContent);
                                else
                                    TriggerEnterAnimation(_panContent);

                                break;
                            }
                        case PageStates.LoaderStay:
                            {
                                PageState = PageStates.LoaderExit;
                                TriggerExitAnimation(_panLoader);
#pragma warning restore CS8604 // 引用类型参数可能为 null。
                                break;
                            }
                    }

                    break;
                }
        }
    }

    #endregion

    #region 动画

    public void TriggerEnterAnimation(params FrameworkElement[] elements)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        var realElements = elements.Where(e => e is not null).ToArray();
        foreach (var element in realElements)
            element.Visibility = Visibility.Visible; // 页面均处于默认的隐藏状态
        var aniList = new List<Animations.AniData>();
        var delay = 0;
        // 基础动画
        foreach (var Element in realElements)
        {
            foreach (var Control in GetAllAnimControls(Element, true))
            {
                // 还原被隐藏的卡片的消失动画
                Control.IsHitTestVisible = true;
                if (Control.RenderTransform is TranslateTransform)
                    Control.RenderTransform = null;
            }

            foreach (var Control in GetAllAnimControls(Element))
                if (Control is MyExtraTextButton button)
                {
                    button.Show = true;
                }
                else
                {
                    Control.Opacity = 0d;
                    Control.RenderTransform = new TranslateTransform(0d, -16);
                    aniList.Add(Animations.AaOpacity(Control, 1d, 100, delay,
                        new Animations.AniEaseOutFluent(Animations.AniEasePower.Weak)));
                    aniList.Add(Animations.AaTranslateY(Control, 5d, 250, delay,
                        new Animations.AniEaseOutFluent()));
                    aniList.Add(Animations.AaTranslateY(Control, 11d, 350, delay,
                        new Animations.AniEaseOutBack()));
                    delay += 25;
                }
        }

        // 滚动条动画
        var scroll = GetFirstScrollViewer(realElements);
        if (scroll is not null)
        {
            if (scroll.RenderTransform is not TranslateTransform)
                scroll.RenderTransform = new TranslateTransform(10d, 0d);
            aniList.Add(Animations.AaTranslateX(scroll,
                -((TranslateTransform)scroll.RenderTransform).X, 350, 0,
                new Animations.AniEaseOutFluent()));
        }

        // 结束
        aniList.Add(Animations.AaCode(PageOnEnterAnimationFinished, after: true));
        Animations.AniStart(aniList, "PageRight PageChange " + pageUuid, true);
    }

    public void TriggerExitAnimation(params FrameworkElement[] elements)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        // element has the possibility to be null
        var realElements = elements.Where(e => e is not null).ToArray();
        var aniList = new List<Animations.AniData>();
        var delay = 0;
        foreach (var element in realElements)
            foreach (var control in GetAllAnimControls(element))
                if (control is MyExtraTextButton button)
                {
                    button.Show = false;
                }
                else
                {
                    control.IsHitTestVisible = false;
                    aniList.Add(Animations.AaOpacity(control, -1, 70, delay));
                    aniList.Add(Animations.AaTranslateY(control, -6, 70, delay));
                    delay += 15;
                }

        // 滚动条动画
        var scroll = GetFirstScrollViewer(realElements);
        if (scroll is not null)
        {
            if (scroll.RenderTransform is not TranslateTransform)
                scroll.RenderTransform = new TranslateTransform();
            aniList.Add(Animations.AaTranslateX(scroll,
                10d - ((TranslateTransform)scroll.RenderTransform).X, 90, 0,
                new Animations.AniEaseInFluent()));
        }

        // 结束
        aniList.Add(Animations.AaCode(() =>
        {
            foreach (var element in realElements)
                element.Visibility = Visibility.Collapsed;
            PageOnExitAnimationFinished();
        }, after: true));
        Animations.AniStart(aniList, "PageRight PageChange " + pageUuid);
    }

    /// <summary>
    ///     禁用页面切换动画的控件列表。
    /// </summary>
    public List<FrameworkElement> disabledPageAnimControls = new();

    /// <summary>
    ///     遍历获取所有需要生成动画的控件。
    /// </summary>
    internal IEnumerable<FrameworkElement> GetAllAnimControls(FrameworkElement element, bool ignoreInvisibility = false)
    {
        var allControls = new List<FrameworkElement>();
        _GetAllAnimControls(element, ref allControls, ignoreInvisibility);
        return allControls.Except(disabledPageAnimControls);
    }

    private static void _GetAllAnimControls(FrameworkElement element, ref List<FrameworkElement> allControls, bool ignoreInvisibility)
    {
        if (!ignoreInvisibility && element.Visibility == Visibility.Collapsed)
            return;
        if (element is MyCard or MyHint or MyExtraTextButton or TextBlock or MyTextButton)
        {
            allControls.Add(element);
        }
        else if (element is ContentControl control)
        {
            var content = control.Content;
            if (content is FrameworkElement frameworkElement)
                _GetAllAnimControls(frameworkElement, ref allControls, ignoreInvisibility);
        }
        else if (element is Panel panel)
        {
            foreach (var element2 in panel.Children)
                if (element2 is FrameworkElement)
                    _GetAllAnimControls((FrameworkElement)element2, ref allControls, ignoreInvisibility);
        }
    }

    private static MyScrollBar? GetFirstScrollViewer(IEnumerable<FrameworkElement> elements)
    {
        foreach (var element in elements)
        {
            if (element is MyScrollViewer viewer)
            {
                if (viewer.ComputedVerticalScrollBarVisibility != Visibility.Visible)
                    continue;
                return viewer.scrollBar;
            }

            foreach (var control in LogicalTreeHelper.GetChildren(element))
                if (control is MyScrollViewer childViewer)
                {
                    if (childViewer.ComputedVerticalScrollBarVisibility != Visibility.Visible)
                        return null;
                    return childViewer.scrollBar;
                }
        }

        return null;
    }

    #endregion
}
