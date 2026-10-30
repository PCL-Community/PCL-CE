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
        LoaderEnter, // 加载环进入
        LoaderStayForce, // 加载环正常显示（强制等待）
        LoaderStay, // 加载环正常显示
        LoaderExit, // 加载环退出
        ContentEnter, // 内容进入
        ContentStay, // 内容正常显示
        ContentExit, // 刷新导致的全部退出，或页面内容退出（子页面更改）导致的全部退出
        PageExit // 切换页面导致的全部退出
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
                    Thread.Sleep(10); // 不在页面退出时执行 UI 线程操作，避免退出过程被重置
                ModBase.RunInUiWait(() =>
                {
                    using var scopeFinish = TracePerf.Measure("PageLoaderInit 加载完成回调");
                    finishedInvoke(realLoader);
                });
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
                    ShowPage(panAlways, (FrameworkElement)(panContent ?? Child));
                }
                else if (pageLoader.State == ModBase.LoadState.Loading)
                {
                    PageState = PageStates.LoaderWait;
                    ModBase.RunInUiLater(PageOnLoaderWaitFinished, 400);
                }
                else // PageLoader.State = LoadState.Failed
                {
                    PageState = PageStates.LoaderEnter;
                    ShowPage(panAlways, panLoader);
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
                    ShowPage((FrameworkElement)(panContent ?? Child));
                }
                else if (pageLoader.State == ModBase.LoadState.Loading)
                {
                    PageState = PageStates.LoaderWait;
                    ModBase.RunInUiLater(PageOnLoaderWaitFinished, 400);
                }
                else // PageLoader.State = LoadState.Failed
                {
                    PageState = PageStates.LoaderEnter;
                    ShowPage(panLoader);
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
                HidePage(panAlways, (FrameworkElement)(panContent ?? Child));
                break;
            }
            case PageStates.LoaderEnter:
            case PageStates.LoaderStayForce:
            case PageStates.LoaderStay:
            {
                PageState = PageStates.PageExit;
                HidePage(panAlways, panLoader);
                break;
            }
            case PageStates.LoaderWait:
            {
                PageState = PageStates.PageExit;
                HidePage(panAlways);
                break;
            }
            case PageStates.LoaderExit:
            case PageStates.ContentExit:
            {
                PageState = PageStates.PageExit;
                if (panAlways is not null)
                    HidePage(panAlways, (FrameworkElement)(panContent ?? Child));
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
        // 直接进行隐藏
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
        // Loading 的加载器可能触发进一步变化，难以预测会触发子页面的切换还是加载器完成的切换
        switch (PageState)
        {
            case PageStates.ContentEnter:
            case PageStates.ContentStay:
            {
                PageState = PageStates.ContentExit;
                HidePage(panContent);
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
                HidePage(panLoader);
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
    ///     页面已显示完成。
    ///     需要根据目前状态，从 ContentEnter 切换到 ContentStay，或从 LoaderEnter 切换到 LoaderStayForce。
    /// </summary>
    private void PageOnEnterShown()
    {
        if (ModBase.modeDebug)
            ModBase.Log("[UI] 已触发 PageOnEnterShown");
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
                ModBase.RunInUiLater(PageOnLoaderStayFinished, 400);
                break;
            }

            default:
            {
                throw new Exception("在状态为 " + ModBase.GetStringFromEnum(PageState) +
                                    " 时触发了 PageOnEnterShown 事件。");
            }
        }
    }

    /// <summary>
    ///     页面已隐藏完成。
    ///     需要根据目前状态，从 AllExit 切换到 Empty，或从 LoaderExit 切换到 ContentEnter，或从 ContentExit 重新触发 PageOnEnter。
    /// </summary>
    private void PageOnExitHidden()
    {
        if (ModBase.modeDebug)
            ModBase.Log("[UI] 已触发 PageOnExitHidden");
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
                ShowPage(panContent);
                break;
            }

            default:
            {
                throw new Exception("在状态为 " + ModBase.GetStringFromEnum(PageState) +
                                    " 时触发了 PageOnExitHidden 事件。");
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
                    ShowPage(panAlways, panLoader);
                else
                    ShowPage(panLoader);

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
                    HidePage(panLoader);
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
                        HidePage(panContent);
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
                            ShowPage(panAlways, panContent);
                        else
                            ShowPage(panContent);

                        break;
                    }
                    case PageStates.LoaderStay:
                    {
                        PageState = PageStates.LoaderExit;
                        HidePage(panLoader);
                        break;
                    }
                }

                break;
            }
        }
    }

    #endregion

    #region 页面切换

    // 逐个进入
    public void ShowPage(params FrameworkElement[] elements)
    {
        var realElements = elements.Where(e => e is not null).ToList();
        foreach (var Element in realElements)
            Element.Visibility = Visibility.Visible; // 页面均处于默认的隐藏状态

        foreach (var Element in realElements)
        {
            foreach (var Control in GetAllTransitionControls(Element, true))
            {
                // 还原被隐藏的卡片的消失状态
                Control.IsHitTestVisible = true;
                Control.Opacity = 1d;
                if (Control.RenderTransform is TranslateTransform)
                    Control.RenderTransform = null;
            }

            foreach (var Control in GetAllTransitionControls(Element))
            {
                if (Control is MyExtraTextButton extraTextButton)
                {
                    extraTextButton.Show = true;
                }
                else
                {
                    Control.Opacity = 1d;
                    Control.RenderTransform = null;
                }
            }
        }

        // 滚动条
        var scroll = GetFirstScrollViewer(realElements);
        if (scroll is not null && scroll.RenderTransform is TranslateTransform)
            scroll.RenderTransform = null;

        PageOnEnterShown();
    }

    // 逐个退出
    public void HidePage(params FrameworkElement[] elements)
    {
        var realElements = elements.Where(e => e is not null).ToList();
        foreach (var Element in realElements)
        foreach (var Control in GetAllTransitionControls(Element))
            if (Control is MyExtraTextButton extraTextButton)
            {
                extraTextButton.Show = false;
            }
            else
            {
                Control.IsHitTestVisible = false;
                Control.Opacity = 0d;
            }

        foreach (var Element in realElements)
            Element.Visibility = Visibility.Collapsed;
        PageOnExitHidden();
    }

    /// <summary>
    ///     不参与页面切换的控件列表。
    /// </summary>
    public List<FrameworkElement> disabledPageTransitionControls = new();

    /// <summary>
    ///     遍历获取所有参与页面切换的控件。
    /// </summary>
    internal IEnumerable<FrameworkElement> GetAllTransitionControls(FrameworkElement element, bool ignoreInvisibility = false)
    {
        var allControls = new List<FrameworkElement>();
        _GetAllTransitionControls(element, ref allControls, ignoreInvisibility);
        return allControls.Except(disabledPageTransitionControls);
    }

    private void _GetAllTransitionControls(FrameworkElement element, ref List<FrameworkElement> allControls,
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
                    _GetAllTransitionControls((FrameworkElement)content, ref allControls, ignoreInvisibility);
        }
        else if (element is Panel)
        {
            foreach (var Element2 in ((Panel)element).Children)
                if (Element2 is FrameworkElement)
                    _GetAllTransitionControls((FrameworkElement)Element2, ref allControls, ignoreInvisibility);
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
