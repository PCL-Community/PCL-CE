using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using static PCL.ModLoader;

namespace PCL;

public class MyPageRight : AdornerDecorator
{
    // 当前状态
    public enum PageStates
    {
        Empty, // 默认状态，页面全空
        LoaderWait, // 加载环初始等待
        LoaderEnter, // 加载环进入动画
        LoaderStayForce, // 加载环正常显示（强制等待）
        LoaderStay, // 加载环正常显示
        LoaderExit, // 加载环退出动画
        ContentEnter, // 内容进入动画
        ContentStay, // 内容正常显示
        ContentExit, // 刷新导致的全部退出动画，或页面内容退出（子页面更改）导致的全部退出动画
        PageExit // 切换页面导致的全部退出动画
    }

    private static readonly DependencyProperty PanScrollProperty =
    DependencyProperty.Register("PanScroll", typeof(MyScrollViewer), typeof(MyPageRight));

    private bool _panScrollNullWarned;

    public int pageUuid = ModBase.GetUuid();

    // “返回顶部” 按钮检测的滚动区域
    public MyScrollViewer PanScroll
    {
        get
        {
            var res = GetValue((DependencyProperty)PanScrollProperty);
            if (res is null && !_panScrollNullWarned)
            {
                _panScrollNullWarned = true;
                ModBase.Log($"[MyPageRight] 获取到 PanScroll(来自 {Name}) 的值为 null", ModBase.LogLevel.Debug);
            }

            return (MyScrollViewer)res;
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
            if (ModBase.modeDebug)
                ModBase.Log("[UI] 页面状态切换为 " + ModBase.GetStringFromEnum(value));
        }
    } = PageStates.Empty;

    #region 加载器

    private ModLoader.LoaderBase pageLoader;
    private Func<object>? pageLoaderInputInvoke;
    private MyLoading? pageLoaderUi;
    private FrameworkElement panLoader;
    private FrameworkElement panContent;
    private FrameworkElement? panAlways;
    private bool pageLoaderAutoRun;

    // 初始化
    /// <summary>
    ///     表明页面存在需要在后台执行的加载器。
    /// </summary>
    /// <param name="loaderUi">MyLoading 控件。</param>
    /// <param name="panLoader">MyLoading 控件对应的卡片。</param>
    /// <param name="panContent">加载结束后出现的内容容器。</param>
    /// <param name="panAlways">无论是否在加载总是要显示的容器。可以为 Nothing。</param>
    /// <param name="realLoader">在工作线程执行的加载器。</param>
    /// <param name="finishedInvoke">当加载器执行完成，在 UI 线程触发的 UI 初始化事件。</param>
    public void PageLoaderInit(MyLoading loaderUi, FrameworkElement panLoader, FrameworkElement panContent,
        FrameworkElement? panAlways, ModLoader.LoaderBase realLoader, Action<ModLoader.LoaderBase>? finishedInvoke = null,
        Func<object>? inputInvoke = null, bool autoRun = true)
    {
        // 初始化参数
        this.panLoader = panLoader;
        this.panContent = panContent;
        this.panAlways = panAlways;
        pageLoader = realLoader;
        pageLoaderUi = loaderUi;
        pageLoaderInputInvoke = inputInvoke;
        pageLoaderAutoRun = autoRun;
        // 添加结束 Invoke
        if (finishedInvoke is not null)
            realLoader.PreviewFinish += _ =>
            {
                while (PageState == PageStates.PageExit || PageState == PageStates.ContentExit)
                    Thread.Sleep(10); // 不在退出动画时执行 UI 线程操作，避免退出动画被重置
                ModBase.RunInUiWait(() => finishedInvoke(realLoader));
                Thread.Sleep(20); // 由于大量初始化控件会导致掉帧，延迟触发 State 改变事件
            };
        realLoader.OnStateChangedUi += (loader, newState, oldState) =>
            ModBase.RunInUi(() => PageLoaderState(loader, newState, oldState));
        // 隐藏 UI
        panLoader.Visibility = Visibility.Collapsed;
        panContent.Visibility = Visibility.Collapsed;
        panAlways?.Visibility = Visibility.Collapsed;
        // 初次运行加载器
        if (pageLoaderAutoRun)
        {
            if (pageLoader is ModLoader.LoaderTask task)
            {
                task.Start(task.StartGetInputNoType(null, pageLoaderInputInvoke));
            }
            else
            {
                object? input = null;
                if (pageLoaderInputInvoke is not null)
                    input = pageLoaderInputInvoke();
                pageLoader.Start(input);
            }
        }

        if (pageLoader.State == ModBase.LoadState.Finished && finishedInvoke is not null)
            ModBase.RunInUiWait(() => finishedInvoke(realLoader)); // 加载器已提前完成，直接触发事件
        // 设置加载环
        pageLoaderUi.State = realLoader;
        pageLoaderUi.Click += (_, _) =>
        {
            if (realLoader.State == ModBase.LoadState.Failed) PageLoaderRestart();
        }; // 点击重试事件
    }

    // 重试
    public void PageLoaderRestart(object input = null, bool isForceRestart = true) // 由外部调用的重试
    {
        if (!pageLoaderAutoRun)
            return;
        if (pageLoader is LoaderTask task)
        {
            pageLoader.Start(task.StartGetInputNoType(input, pageLoaderInputInvoke), isForceRestart);
        }
        else
        {
            if (input is null && pageLoaderInputInvoke is not null)
                input = pageLoaderInputInvoke;
            pageLoader.Start(input, isForceRestart);
        }
    }

    #endregion

    #region 事件

    // 外部触发的事件
    /// <summary>
    ///     需要切换到当前页面，并且原本的 Loaded 事件已执行完成。
    ///     需要根据加载器状态，从 Empty 切换到 ContentEnter、LoaderWait、LoaderEnter。
    /// </summary>
    public void PageOnEnter()
    {
        if (ModBase.modeDebug)
            ModBase.Log("[UI] 已触发 PageOnEnter");
        PageEnter?.Invoke();
        switch (PageState)
        {
            case PageStates.Empty:
            {
                if (pageLoader is null || pageLoader.State == ModBase.LoadState.Finished ||
                    pageLoader.State == ModBase.LoadState.Waiting || pageLoader.State == ModBase.LoadState.Aborted)
                {
                    // 如果加载器在进入页面时不启动（例如联机），那么在此时就会有 State = Waiting
                    PageState = PageStates.ContentEnter;
                    TriggerEnterAnimation(panAlways, (FrameworkElement)(panContent ?? Child));
                }
                else if (pageLoader.State == ModBase.LoadState.Loading)
                {
                    PageState = PageStates.LoaderWait;
                    ModAnimation.AniStart(ModAnimation.AaCode(PageOnLoaderWaitFinished, 400),
                        "PageRight PageChange " + pageUuid);
                }
                else // PageLoader.State = LoadState.Failed
                {
                    PageState = PageStates.LoaderEnter;
                    TriggerEnterAnimation(panAlways, panLoader);
                }

                break;
            }
            case PageStates.ContentExit:
            {
                // 和上面的一样，但是不管 PanAlways
                if (pageLoader is null || pageLoader.State == ModBase.LoadState.Finished ||
                    pageLoader.State == ModBase.LoadState.Waiting || pageLoader.State == ModBase.LoadState.Aborted)
                {
                    PageState = PageStates.ContentEnter;
                    TriggerEnterAnimation((FrameworkElement)(panContent ?? Child));
                }
                else if (pageLoader.State == ModBase.LoadState.Loading)
                {
                    PageState = PageStates.LoaderWait;
                    ModAnimation.AniStart(ModAnimation.AaCode(PageOnLoaderWaitFinished, 400),
                        "PageRight PageChange " + pageUuid);
                }
                else // PageLoader.State = LoadState.Failed
                {
                    PageState = PageStates.LoaderEnter;
                    TriggerEnterAnimation(panLoader);
                }

                break;
            }
            case PageStates.ContentEnter: // 重复调用 PageOnEnter，直接忽略
            {
                break;
            }

            default:
            {
                throw new Exception("在状态为 " + ModBase.GetStringFromEnum(PageState) + " 时触发了 PageOnEnter 事件。");
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
        if (ModBase.modeDebug)
            ModBase.Log("[UI] 已触发 PageOnExit");
        PageExit?.Invoke();
        switch (PageState)
        {
            case PageStates.ContentEnter:
            case PageStates.ContentStay:
            {
                PageState = PageStates.PageExit;
                TriggerExitAnimation(panAlways, (FrameworkElement)(panContent ?? Child));
                break;
            }
            case PageStates.LoaderEnter:
            case PageStates.LoaderStayForce:
            case PageStates.LoaderStay:
            {
                PageState = PageStates.PageExit;
                TriggerExitAnimation(panAlways, panLoader);
                break;
            }
            case PageStates.LoaderWait:
            {
                PageState = PageStates.PageExit;
                TriggerExitAnimation(panAlways);
                break;
            }
            case PageStates.LoaderExit:
            case PageStates.ContentExit:
            {
                PageState = PageStates.PageExit;
                if (panAlways is not null)
                    TriggerExitAnimation(panAlways, (FrameworkElement)(panContent ?? Child));
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
        if (ModBase.modeDebug)
            ModBase.Log("[UI] 已触发 PageOnForceExit");
        PageState = PageStates.Empty;
        ModAnimation.AniStop("PageRight PageChange " + pageUuid);
        // 由于动画会被强制中止，所以需要手动进行隐藏
        if (pageLoader is null && Child is not null)
        {
            Child.Visibility = Visibility.Collapsed;
        }
        else
        {
            panContent.Visibility = Visibility.Collapsed;
            panLoader.Visibility = Visibility.Collapsed;
            if (panAlways is not null)
                panAlways.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    ///     PanContent 中的子页面改变，需要让当前内容退出，再显示新的内容。
    ///     需要在 PageEnter 事件确认要显示的子页面有哪些。
    /// </summary>
    public void PageOnContentExit()
    {
        if (ModBase.modeDebug)
            ModBase.Log("[UI] 已触发 PageOnContentExit");
        if (pageLoader is not null && pageLoader.State == ModBase.LoadState.Loading)
            throw new Exception("在调用 PageOnContentExit 时，加载器不能为 Loading 状态");
        // Loading 的加载器可能触发进一步变化，难以预测会触发子页面的动画还是加载器完成的动画
        switch (PageState)
        {
            case PageStates.ContentEnter:
            case PageStates.ContentStay:
            {
                PageState = PageStates.ContentExit;
                TriggerExitAnimation(panContent);
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
                TriggerExitAnimation(panLoader);
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

    // 内部触发的事件
    /// <summary>
    ///     逐个进入动画已执行完成。
    ///     需要根据目前状态，从 ContentEnter 切换到 ContentStay，或从 LoaderEnter 切换到 LoaderStayForce。
    /// </summary>
    private void PageOnEnterAnimationFinished()
    {
        if (ModBase.modeDebug)
            ModBase.Log("[UI] 已触发 PageOnEnterAnimationFinished");
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
                ModAnimation.AniStart(ModAnimation.AaCode(PageOnLoaderStayFinished, 400),
                    "PageRight PageChange " + pageUuid);
                break;
            }

            default:
            {
                throw new Exception("在状态为 " + ModBase.GetStringFromEnum(PageState) +
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
        if (ModBase.modeDebug)
            ModBase.Log("[UI] 已触发 PageOnExitAnimationFinished");
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
                TriggerEnterAnimation(panContent);
                break;
            }

            default:
            {
                throw new Exception("在状态为 " + ModBase.GetStringFromEnum(PageState) +
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
        if (ModBase.modeDebug)
            ModBase.Log("[UI] 已触发 PageOnLoaderWaitFinished");
        switch (PageState)
        {
            case PageStates.LoaderWait:
            {
                PageState = PageStates.LoaderEnter;
                if (panAlways is not null && panAlways.Visibility == Visibility.Collapsed)
                    TriggerEnterAnimation(panAlways, panLoader);
                else
                    TriggerEnterAnimation(panLoader);

                break;
            }

            default:
            {
                throw new Exception("在状态为 " + ModBase.GetStringFromEnum(PageState) +
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
        if (ModBase.modeDebug)
            ModBase.Log("[UI] 已触发 PageOnLoaderStayFinished");
        switch (PageState)
        {
            case PageStates.LoaderStayForce:
            {
                if (pageLoader.State == ModBase.LoadState.Finished)
                {
                    PageState = PageStates.LoaderExit;
                    TriggerExitAnimation(panLoader);
                }
                else
                {
                    PageState = PageStates.LoaderStay;
                }

                break;
            }

            default:
            {
                throw new Exception("在状态为 " + ModBase.GetStringFromEnum(PageState) +
                                    " 时触发了 PageOnLoaderWaitFinished 事件。");
            }
        }
    }

    /// <summary>
    ///     全局加载状态已改变。
    /// </summary>
    private void PageLoaderState(object sender, ModBase.LoadState newState, ModBase.LoadState oldState)
    {
        switch (newState)
        {
            case ModBase.LoadState.Failed:
            case ModBase.LoadState.Loading:
            {
                if (oldState == ModBase.LoadState.Failed || oldState == ModBase.LoadState.Loading)
                    return;
                if (ModBase.modeDebug)
                    ModBase.Log("[UI] 已触发 PageLoaderState (Start/Refresh)");
                // （重新）开始运行
                // 需要从部分状态切换到 ReloadExit
                switch (PageState)
                {
                    case PageStates.ContentEnter:
                    case PageStates.ContentStay:
                    {
                        PageState = PageStates.ContentExit;
                        TriggerExitAnimation(panContent);
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
            case ModBase.LoadState.Finished:
            case ModBase.LoadState.Aborted:
            case ModBase.LoadState.Waiting:
            {
                if (oldState != ModBase.LoadState.Failed && oldState != ModBase.LoadState.Loading)
                    return;
                if (ModBase.modeDebug)
                    ModBase.Log("[UI] 已触发 PageLoaderState (Stop/Abort)");
                // 运行结束
                // 需要从 LoaderWait 切换到 ContentEnter，或从 LoaderStay 切换到 LoaderExit
                switch (PageState)
                {
                    case PageStates.LoaderWait:
                    {
                        PageState = PageStates.ContentEnter;
                        if (panAlways is not null && panAlways.Visibility == Visibility.Collapsed)
                            TriggerEnterAnimation(panAlways, panContent);
                        else
                            TriggerEnterAnimation(panContent);

                        break;
                    }
                    case PageStates.LoaderStay:
                    {
                        PageState = PageStates.LoaderExit;
                        TriggerExitAnimation(panLoader);
                        break;
                    }
                }

                break;
            }
        }
    }

    #endregion

    #region 动画

    // 逐个进入动画
    public void TriggerEnterAnimation(params FrameworkElement[] elements)
    {
        var realElements = elements.Where(e => e is not null);
        foreach (var Element in realElements)
            Element.Visibility = Visibility.Visible; // 页面均处于默认的隐藏状态
        var aniList = new List<ModAnimation.AniData>();
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
                if (Control is MyExtraTextButton)
                {
                    ((MyExtraTextButton)Control).Show = true;
                }
                else
                {
                    Control.Opacity = 0d;
                    Control.RenderTransform = new TranslateTransform(0d, -16);
                    aniList.Add(ModAnimation.AaOpacity(Control, 1d, 100, delay,
                        new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
                    aniList.Add(ModAnimation.AaTranslateY(Control, 5d, 250, delay,
                        new ModAnimation.AniEaseOutFluent()));
                    aniList.Add(ModAnimation.AaTranslateY(Control, 11d, 350, delay, new ModAnimation.AniEaseOutBack()));
                    delay += 25;
                }
        }

        // 滚动条动画
        var scroll = GetFirstScrollViewer(realElements);
        if (scroll is not null)
        {
            if (scroll.RenderTransform is not TranslateTransform)
                scroll.RenderTransform = new TranslateTransform(10d, 0d);
            aniList.Add(ModAnimation.AaTranslateX(scroll, -((TranslateTransform)scroll.RenderTransform).X, 350, 0,
                new ModAnimation.AniEaseOutFluent()));
        }

        // 结束
        aniList.Add(ModAnimation.AaCode(() => PageOnEnterAnimationFinished(), after: true));
        ModAnimation.AniStart(aniList, "PageRight PageChange " + pageUuid, true);
    }

    // 逐个退出动画
    public void TriggerExitAnimation(params FrameworkElement[] elements)
    {
        var realElements = elements.Where(e => e is not null);
        var aniList = new List<ModAnimation.AniData>();
        var delay = 0;
        foreach (var Element in realElements)
        foreach (var Control in GetAllAnimControls(Element))
            if (Control is MyExtraTextButton)
            {
                ((MyExtraTextButton)Control).Show = false;
            }
            else
            {
                Control.IsHitTestVisible = false;
                aniList.Add(ModAnimation.AaOpacity(Control, -1, 70, delay));
                aniList.Add(ModAnimation.AaTranslateY(Control, -6, 70, delay));
                delay += 15;
            }

        // 滚动条动画
        var scroll = GetFirstScrollViewer(realElements);
        if (scroll is not null)
        {
            if (scroll.RenderTransform is not TranslateTransform)
                scroll.RenderTransform = new TranslateTransform();
            aniList.Add(ModAnimation.AaTranslateX(scroll, 10d - ((TranslateTransform)scroll.RenderTransform).X, 90, 0,
                new ModAnimation.AniEaseInFluent()));
        }

        // 结束
        aniList.Add(ModAnimation.AaCode(() =>
        {
            foreach (var Element in realElements)
                Element.Visibility = Visibility.Collapsed;
            PageOnExitAnimationFinished();
        }, after: true));
        ModAnimation.AniStart(aniList, "PageRight PageChange " + pageUuid);
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

    private void _GetAllAnimControls(FrameworkElement element, ref List<FrameworkElement> allControls,
        bool ignoreInvisibility)
    {
        if (!ignoreInvisibility && element.Visibility == Visibility.Collapsed)
            return;
        if (element is MyCard || element is MyHint || element is MyExtraTextButton || element is TextBlock ||
            element is MyTextButton)
        {
            allControls.Add(element);
        }
        else if (element is ContentControl)
        {
                var content = ((ContentControl)element).Content;
                if (content is FrameworkElement)
                    _GetAllAnimControls((FrameworkElement)content, ref allControls, ignoreInvisibility);
        }
        else if (element is Panel)
        {
            foreach (var Element2 in ((Panel)element).Children)
                if (Element2 is FrameworkElement)
                    _GetAllAnimControls((FrameworkElement)Element2, ref allControls, ignoreInvisibility);
        }
    }

    // 查找列表中的第一个滚动条
    private MyScrollBar GetFirstScrollViewer(IEnumerable<FrameworkElement> elements)
    {
        foreach (var Element in elements)
        {
            if (Element is MyScrollViewer Viewer)
            {
                if (Viewer.ComputedVerticalScrollBarVisibility != Visibility.Visible)
                    continue;
                return Viewer.scrollBar;
            }

            foreach (var Control in LogicalTreeHelper.GetChildren(Element))
                if (Control is MyScrollViewer ChildViewer)
                {
                    if (ChildViewer.ComputedVerticalScrollBarVisibility != Visibility.Visible)
                        return null;
                    return ChildViewer.scrollBar;
                }
        }

        return null;
    }

    #endregion
}
