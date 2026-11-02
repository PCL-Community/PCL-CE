using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using static PCL.Animations;

namespace PCL.UI.Controls;

/// <summary>
///     页面右栏容器：管理内容页与加载环的显示状态。
///     页面状态机（Empty/LoaderWait/LoaderEnter/.../PageExit）决定何时播放入场、退场与加载环等待；
///     所有转场挂在“PageRight PageChange”动画键下，快速切换时旧回调随动画组一起丢弃。
/// </summary>
public class MyPageRight : Decorator
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

    private static readonly StyledProperty<MyScrollViewer?> PanScrollProperty =
        AvaloniaProperty.Register<MyPageRight, MyScrollViewer?>(nameof(PanScroll));

    private bool _panScrollNullWarned;

    public int pageUuid = LauncherCommon.GetUuid();

    // “返回顶部” 按钮检测的滚动区域
    public MyScrollViewer? PanScroll
    {
        get
        {
            var res = GetValue(PanScrollProperty);
            if (res is null && !_panScrollNullWarned)
            {
                _panScrollNullWarned = true;
                LauncherCommon.Log(
                    $"[MyPageRight] 获取到 PanScroll(来自 {Name}) 的值为 null",
                    LauncherLogLevel.Debug);
            }

            return res;
        }
        set => SetValue(PanScrollProperty, value);
    }

    public PageStates PageState
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            if (LauncherCommon.ModeDebug)
                LauncherCommon.Log("[UI] 页面状态切换为 " + LauncherCommon.GetStringFromEnum(value));
        }
    } = PageStates.Empty;

    #region 加载器

    private Loading.LoaderBase? _pageLoader;
    private Func<object>? _pageLoaderInputInvoke;
    private MyLoading? _pageLoaderUi;
    private Control? _panLoader;
    private Control? _panContent;
    private Control? _panAlways;
    private bool _pageLoaderAutoRun;

    // 初始化
    /// <summary>
    ///     表明页面存在需要在后台执行的加载器。
    /// </summary>
    /// <param name="loaderUi">MyLoading 控件。</param>
    /// <param name="panLoader">MyLoading 控件对应的卡片。</param>
    /// <param name="panContent">加载结束后出现的内容容器。</param>
    /// <param name="panAlways">无论是否在加载总是要显示的容器。可以为 null。</param>
    /// <param name="realLoader">在工作线程执行的加载器。</param>
    /// <param name="finishedInvoke">当加载器执行完成，在 UI 线程触发的 UI 初始化事件。</param>
    public void PageLoaderInit(
        MyLoading loaderUi,
        Control panLoader,
        Control panContent,
        Control? panAlways,
        Loading.LoaderBase realLoader,
        Action<Loading.LoaderBase>? finishedInvoke = null,
        Func<object>? inputInvoke = null,
        bool autoRun = true)
    {
        // 初始化参数
        _panLoader = panLoader;
        _panContent = panContent;
        _panAlways = panAlways;
        _pageLoader = realLoader;
        _pageLoaderUi = loaderUi;
        _pageLoaderInputInvoke = inputInvoke;
        _pageLoaderAutoRun = autoRun;
        // 添加结束 Invoke
        if (finishedInvoke is not null)
            realLoader.PreviewFinish += _ =>
            {
                while (PageState is PageStates.PageExit or PageStates.ContentExit)
                    Thread.Sleep(10); // 不在退出动画时执行 UI 线程操作，避免退出动画被重置
                LauncherCommon.RunInUiWait(() => finishedInvoke(realLoader));
                Thread.Sleep(20); // 由于大量初始化控件会导致掉帧，延迟触发 State 改变事件
            };
        realLoader.OnStateChangedUi += (loader, newState, oldState) =>
            LauncherCommon.RunInUi(() => PageLoaderState(loader, newState, oldState));
        // 隐藏 UI
        panLoader.IsVisible = false;
        _panContent.IsVisible = false;
        panAlways?.SetCurrentValue(IsVisibleProperty, false);
        // 初次运行加载器
        if (_pageLoaderAutoRun)
        {
            if (_pageLoader is Loading.LoaderTask task)
                task.Start(task.StartGetInputNoType(null, _pageLoaderInputInvoke));
            else
                _pageLoader.Start(_pageLoaderInputInvoke?.Invoke());
        }

        if (_pageLoader.State == LoadState.Finished && finishedInvoke is not null)
            LauncherCommon.RunInUiWait(() => finishedInvoke(realLoader)); // 加载器已提前完成，直接触发事件
        // 设置加载环
        _pageLoaderUi.State = realLoader;
        _pageLoaderUi.Click += (_, _) =>
        {
            if (realLoader.State == LoadState.Failed) PageLoaderRestart();
        }; // 点击重试事件
    }

    private void RefreshLocalizationIfStale()
    {
        if (_localizationRevision == LocalizationService.UiRevision)
            return;
        _localizationRevision = LocalizationService.UiRevision;
        try
        {
            RefreshLocalization();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "刷新页面本地化文本失败");
        }
    }

    protected virtual void RefreshLocalization()
    {
    }

    private int _localizationRevision = LocalizationService.UiRevision;

    // 重试
    public void PageLoaderRestart(object? input = null, bool isForceRestart = true) // 由外部调用的重试
    {
        if (!_pageLoaderAutoRun || _pageLoader is null)
            return;
        if (_pageLoader is Loading.LoaderTask task)
            _pageLoader.Start(
                task.StartGetInputNoType(input, _pageLoaderInputInvoke),
                isForceRestart);
        else
            _pageLoader.Start(input ?? _pageLoaderInputInvoke?.Invoke(), isForceRestart);
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
        if (LauncherCommon.ModeDebug)
            LauncherCommon.Log("[UI] 已触发 PageOnEnter");
        RefreshLocalizationIfStale();
        PageEnter?.Invoke();
        switch (PageState)
        {
            case PageStates.Empty:
            {
                if (_pageLoader is null
                    || _pageLoader.State is LoadState.Finished
                        or LoadState.Waiting
                        or LoadState.Aborted)
                {
                    // 如果加载器在进入页面时不启动（例如联机），那么在此时就会有 State = Waiting
                    PageState = PageStates.ContentEnter;
                    TriggerEnterAnimation(_panAlways, _panContent ?? Child);
                }
                else if (_pageLoader.State == LoadState.Loading)
                {
                    PageState = PageStates.LoaderWait;
                    ScheduleLoaderWaitFinished();
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
                if (_pageLoader is null
                    || _pageLoader.State is LoadState.Finished
                        or LoadState.Waiting
                        or LoadState.Aborted)
                {
                    PageState = PageStates.ContentEnter;
                    TriggerEnterAnimation(_panContent ?? Child);
                }
                else if (_pageLoader.State == LoadState.Loading)
                {
                    PageState = PageStates.LoaderWait;
                    ScheduleLoaderWaitFinished();
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

            case PageStates.LoaderWait:
            case PageStates.LoaderEnter:
            case PageStates.LoaderStayForce:
            case PageStates.LoaderStay:
            case PageStates.LoaderExit:
            case PageStates.ContentStay:
            case PageStates.PageExit:
            default:
            {
                throw new Exception(
                    $"在状态为 {LauncherCommon.GetStringFromEnum(PageState)} 时触发了 PageOnEnter 事件。");
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
        if (LauncherCommon.ModeDebug)
            LauncherCommon.Log("[UI] 已触发 PageOnExit");
        PageExit?.Invoke();
        switch (PageState)
        {
            case PageStates.ContentEnter:
            case PageStates.ContentStay:
            {
                PageState = PageStates.PageExit;
                TriggerExitAnimation(_panAlways, _panContent ?? Child);
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
                    TriggerExitAnimation(_panAlways, _panContent ?? Child);
                break;
            }

            case PageStates.PageExit:
            case PageStates.Empty:
            {
                break;
            }

            default:
                throw new ArgumentOutOfRangeException();
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
        if (LauncherCommon.ModeDebug)
            LauncherCommon.Log("[UI] 已触发 PageOnForceExit");
        PageState = PageStates.Empty;
        // 由于动画会被强制中止，所以需要手动进行隐藏
        AniStop(PageChangeAnimationKey);
        if (_pageLoader is null && Child is not null)
        {
            Child.IsVisible = false;
        }
        else
        {
            _panContent?.IsVisible = false;
            _panLoader?.IsVisible = false;
            _panAlways?.IsVisible = false;
        }
    }

    /// <summary>
    ///     PanContent 中的子页面改变，需要让当前内容退出，再显示新的内容。
    ///     需要在 PageEnter 事件确认要显示的子页面有哪些。
    /// </summary>
    public void PageOnContentExit()
    {
        if (LauncherCommon.ModeDebug)
            LauncherCommon.Log("[UI] 已触发 PageOnContentExit");
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

            case PageStates.ContentExit:
            case PageStates.PageExit:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    // 内部触发的事件
    // 400 ms 等待挂在动画键上：页面切换时会随同名动画一起取消；
    // 动画关闭时改为下一轮消息循环立即推进，不再人为等待。
    private void ScheduleLoaderWaitFinished()
    {
        if (!ContainerAnimation.IsEnabled)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (PageState == PageStates.LoaderWait)
                    PageOnLoaderWaitFinished();
            });
            return;
        }

        AniStart(
            AaCode(
                () =>
                {
                    // 页面可能在等待期间退出；同名动画被顶掉后这里不会再执行
                    if (PageState == PageStates.LoaderWait)
                        PageOnLoaderWaitFinished();
                },
                400),
            PageChangeAnimationKey);
    }

    private void ScheduleLoaderStayFinished()
    {
        if (!ContainerAnimation.IsEnabled)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (PageState == PageStates.LoaderStayForce)
                    PageOnLoaderStayFinished();
            });
            return;
        }

        AniStart(
            AaCode(
                () =>
                {
                    if (PageState == PageStates.LoaderStayForce)
                        PageOnLoaderStayFinished();
                },
                400),
            PageChangeAnimationKey);
    }

    /// <summary>
    ///     逐个进入动画已执行完成。
    ///     需要根据目前状态，从 ContentEnter 切换到 ContentStay，或从 LoaderEnter 切换到 LoaderStayForce。
    /// </summary>
    private void PageOnEnterAnimationFinished()
    {
        if (LauncherCommon.ModeDebug)
            LauncherCommon.Log("[UI] 已触发 PageOnEnterAnimationFinished");
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
                ScheduleLoaderStayFinished();
                break;
            }

            case PageStates.Empty:
            case PageStates.LoaderWait:
            case PageStates.LoaderStayForce:
            case PageStates.LoaderStay:
            case PageStates.LoaderExit:
            case PageStates.ContentStay:
            case PageStates.ContentExit:
            case PageStates.PageExit:
            default:
            {
                throw new Exception(
                    $"在状态为 {LauncherCommon.GetStringFromEnum(PageState)} 时触发了 PageOnEnterAnimationFinished 事件。");
            }
        }
    }

    /// <summary>
    ///     逐个退出动画已执行完成。
    ///     需要根据目前状态，从 AllExit 切换到 Empty，或从 LoaderExit 切换到 ContentEnter，或从 ContentExit 重新触发 PageOnEnter。
    /// </summary>
    private void PageOnExitAnimationFinished()
    {
        if (LauncherCommon.ModeDebug)
            LauncherCommon.Log("[UI] 已触发 PageOnExitAnimationFinished");
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

            case PageStates.Empty:
            case PageStates.LoaderWait:
            case PageStates.LoaderEnter:
            case PageStates.LoaderStayForce:
            case PageStates.LoaderStay:
            case PageStates.ContentEnter:
            case PageStates.ContentStay:
            default:
            {
                throw new Exception(
                    $"在状态为 {LauncherCommon.GetStringFromEnum(PageState)} 时触发了 PageOnExitAnimationFinished 事件。");
            }
        }
    }

    /// <summary>
    ///     加载环进入等待已结束。
    ///     需要从 LoaderWait 切换到 LoaderEnter。
    /// </summary>
    private void PageOnLoaderWaitFinished()
    {
        if (LauncherCommon.ModeDebug)
            LauncherCommon.Log("[UI] 已触发 PageOnLoaderWaitFinished");
        switch (PageState)
        {
            case PageStates.LoaderWait:
            {
                PageState = PageStates.LoaderEnter;
                if (_panAlways is not null && !_panAlways.IsVisible)
                    TriggerEnterAnimation(_panAlways, _panLoader);
                else
                    TriggerEnterAnimation(_panLoader);

                break;
            }

            case PageStates.Empty:
            case PageStates.LoaderEnter:
            case PageStates.LoaderStayForce:
            case PageStates.LoaderStay:
            case PageStates.LoaderExit:
            case PageStates.ContentEnter:
            case PageStates.ContentStay:
            case PageStates.ContentExit:
            case PageStates.PageExit:
            default:
            {
                throw new Exception(
                    $"在状态为 {LauncherCommon.GetStringFromEnum(PageState)} 时触发了 PageOnLoaderWaitFinished 事件。");
            }
        }
    }

    /// <summary>
    ///     加载环展示等待已结束。
    ///     需要从 LoaderStayForce 切换到 LoaderStay 或 LoaderExit。
    /// </summary>
    private void PageOnLoaderStayFinished()
    {
        if (LauncherCommon.ModeDebug)
            LauncherCommon.Log("[UI] 已触发 PageOnLoaderStayFinished");
        switch (PageState)
        {
            case PageStates.LoaderStayForce:
            {
                if (_pageLoader?.State == LoadState.Finished)
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

            case PageStates.Empty:
            case PageStates.LoaderWait:
            case PageStates.LoaderEnter:
            case PageStates.LoaderStay:
            case PageStates.LoaderExit:
            case PageStates.ContentEnter:
            case PageStates.ContentStay:
            case PageStates.ContentExit:
            case PageStates.PageExit:
            default:
            {
                throw new Exception(
                    $"在状态为 {LauncherCommon.GetStringFromEnum(PageState)} 时触发了 PageOnLoaderWaitFinished 事件。");
            }
        }
    }

    /// <summary>
    ///     全局加载状态已改变。
    /// </summary>
    private void PageLoaderState(
        Loading.LoaderBase sender,
        LoadState newState,
        LoadState oldState)
    {
        switch (newState)
        {
            // 加载开始或重新加载
            case LoadState.Failed:
            case LoadState.Loading:
            {
                if (oldState is LoadState.Failed or LoadState.Loading)
                    return;

                if (LauncherCommon.ModeDebug)
                    LauncherCommon.Log("[UI] 已触发 PageLoaderState (Start/Refresh)");

                HandleLoadStarted();
                return;
            }

            // 加载完成、中止或等待
            case LoadState.Finished:
            case LoadState.Aborted:
            case LoadState.Waiting:
            {
                if (oldState is not (LoadState.Failed or LoadState.Loading))
                    return;

                if (LauncherCommon.ModeDebug)
                    LauncherCommon.Log("[UI] 已触发 PageLoaderState (Stop/Abort)");

                HandleLoadStopped();
                return;
            }

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(newState),
                    newState,
                    null);
        }
    }

    /// <summary>
    ///     处理加载开始或重新加载时的页面状态转换。
    /// </summary>
    private void HandleLoadStarted()
    {
        switch (PageState)
        {
            // 内容区域正在显示，需要退出
            case PageStates.ContentEnter:
            case PageStates.ContentStay:
                PageState = PageStates.ContentExit;
                TriggerExitAnimation(_panContent);
                break;

            // Loader 正在退出，直接切换状态
            case PageStates.LoaderExit:
                PageState = PageStates.ContentExit;
                break;

            // 其他状态无需处理
            case PageStates.Empty:
            case PageStates.LoaderWait:
            case PageStates.LoaderEnter:
            case PageStates.LoaderStayForce:
            case PageStates.LoaderStay:
            case PageStates.ContentExit:
            case PageStates.PageExit:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    ///     处理加载结束、中止或进入等待状态时的页面状态转换。
    /// </summary>
    private void HandleLoadStopped()
    {
        switch (PageState)
        {
            // Loader 等待状态，切换到内容显示
            case PageStates.LoaderWait:
                PageState = PageStates.ContentEnter;

                if (_panAlways is not null && !_panAlways.IsVisible)
                    TriggerEnterAnimation(_panAlways, _panContent);
                else
                    TriggerEnterAnimation(_panContent);

                break;

            // Loader 常驻状态，退出 Loader
            case PageStates.LoaderStay:
                PageState = PageStates.LoaderExit;
                TriggerExitAnimation(_panLoader);
                break;

            // 其他状态无需处理
            case PageStates.Empty:
            case PageStates.LoaderEnter:
            case PageStates.LoaderStayForce:
            case PageStates.LoaderExit:
            case PageStates.ContentEnter:
            case PageStates.ContentStay:
            case PageStates.ContentExit:
            case PageStates.PageExit:
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    #endregion

    #region 动画

    private string PageChangeAnimationKey => "PageRight PageChange " + pageUuid;

    // 逐个进入动画
    public void TriggerEnterAnimation(params Control?[] elements)
    {
        var realElements = elements.Where(e => e is not null).Cast<Control>().ToList();
        foreach (var element in realElements)
            element.IsVisible = true; // 页面均处于默认的隐藏状态

        // 动画关闭、控件未加载或页面已被强制退出：直接落到终值，状态机同步推进
        if (!ContainerAnimation.CanAnimate(this))
        {
            AniStop(PageChangeAnimationKey);
            foreach (var element in realElements)
            {
                foreach (var control in GetAllAnimControls(element, true))
                {
                    control.IsHitTestVisible = true;
                    if (control.RenderTransform is TranslateTransform)
                        control.RenderTransform = null;
                }

                foreach (var control in GetAllAnimControls(element))
                    if (control is MyExtraTextButton extraTextButton)
                    {
                        extraTextButton.Show = true;
                    }
                    else
                    {
                        control.Opacity = 1d;
                        control.RenderTransform = null;
                    }
            }

            ResetScrollTransform(GetFirstScrollViewer(realElements));
            PageOnEnterAnimationFinished();
            return;
        }

        var aniList = new List<AniData>();
        var delay = 0;
        foreach (var element in realElements)
        {
            foreach (var control in GetAllAnimControls(element, true))
            {
                // 还原被隐藏的卡片的消失动画
                control.IsHitTestVisible = true;
                if (control.RenderTransform is TranslateTransform)
                    control.RenderTransform = null;
            }

            foreach (var control in GetAllAnimControls(element))
                if (control is MyExtraTextButton extraTextButton)
                {
                    extraTextButton.Show = true;
                }
                else
                {
                    control.Opacity = 0d;
                    control.RenderTransform = new TranslateTransform(0d, -16d);
                    aniList.Add(
                        AaOpacity(
                            control,
                            1d,
                            100,
                            delay,
                            new AniEaseOutFluent(AniEasePower.Weak)));
                    aniList.Add(AaTranslateY(control, 5d, 250, delay, new AniEaseOutFluent()));
                    aniList.Add(AaTranslateY(control, 11d, 350, delay, new AniEaseOutBack()));
                    delay += 25;
                }
        }

        // 滚动条从右侧滑入
        var scroll = GetFirstScrollViewer(realElements);
        if (scroll is not null)
        {
            if (scroll.RenderTransform is not TranslateTransform)
            {
                scroll.RenderTransformOrigin = RelativePoint.Center;
                scroll.RenderTransform = new TranslateTransform(10d, 0d);
            }

            aniList.Add(
                AaTranslateX(
                    scroll,
                    -((TranslateTransform)scroll.RenderTransform).X,
                    350,
                    0,
                    new AniEaseOutFluent()));
        }

        // 同名动画组会顶掉上一组，快速切换时只有最后这一段回调会执行
        aniList.Add(AaCode(PageOnEnterAnimationFinished, after: true));
        AniStart(aniList, PageChangeAnimationKey, true);
    }

    // 逐个退出动画
    public void TriggerExitAnimation(params Control?[] elements)
    {
        var realElements = elements.Where(e => e is not null).Cast<Control>().ToList();

        if (!ContainerAnimation.CanAnimate(this))
        {
            AniStop(PageChangeAnimationKey);
            foreach (var control in realElements.SelectMany(element => GetAllAnimControls(element)))
                if (control is MyExtraTextButton extraTextButton)
                {
                    extraTextButton.Show = false;
                }
                else
                {
                    control.IsHitTestVisible = false;
                    control.Opacity = 0d;
                    control.RenderTransform = new TranslateTransform(0d, -6d);
                }

            SetScrollExitTransform(GetFirstScrollViewer(realElements));
            foreach (var element in realElements)
                element.IsVisible = false;

            PageOnExitAnimationFinished();
            return;
        }

        var aniList = new List<AniData>();
        var delay = 0;
        foreach (var control in realElements.SelectMany(element => GetAllAnimControls(element)))
            if (control is MyExtraTextButton extraTextButton)
            {
                extraTextButton.Show = false;
            }
            else
            {
                control.IsHitTestVisible = false;
                aniList.Add(AaOpacity(control, -control.Opacity, 70, delay));
                aniList.Add(AaTranslateY(control, -6d, 70, delay));
                delay += 15;
            }

        // 滚动条向右滑出
        var scroll = GetFirstScrollViewer(realElements);
        if (scroll is not null)
        {
            if (scroll.RenderTransform is not TranslateTransform)
            {
                scroll.RenderTransformOrigin = RelativePoint.Center;
                scroll.RenderTransform = new TranslateTransform();
            }

            aniList.Add(
                AaTranslateX(
                    scroll,
                    10d - ((TranslateTransform)scroll.RenderTransform).X,
                    90,
                    0,
                    new AniEaseInFluent()));
        }

        aniList.Add(
            AaCode(
                () =>
                {
                    foreach (var element in realElements)
                        element.IsVisible = false;
                    PageOnExitAnimationFinished();
                },
                after: true));
        AniStart(aniList, PageChangeAnimationKey);
    }

    /// <summary>找到页面中第一个 <see cref="MyScrollViewer" />，用于页面转场时的横向位移。</summary>
    private static MyScrollViewer? GetFirstScrollViewer(IEnumerable<Control> elements)
    {
        foreach (var element in elements)
            if (FindScrollViewer(element) is { } scroll)
                return scroll;
        return null;
    }

    private static MyScrollViewer? FindScrollViewer(Control element)
    {
        while (true)
        {
            switch (element)
            {
                case MyScrollViewer scroll:
                    return scroll;

                case ContentControl { Content: Control content }:
                {
                    element = content;
                    continue;
                }

                case Decorator { Child: { } child }:
                {
                    element = child;
                    continue;
                }

                case Panel panel:
                {
                    foreach (var child in panel.Children)
                        if (child is Control && FindScrollViewer(child) is { } found)
                            return found;
                    break;
                }
            }

            return null;
        }
    }

    /// <summary>进入页面前把滚动容器的横向位移还原。</summary>
    private static void ResetScrollTransform(MyScrollViewer? scroll)
    {
        if (scroll?.RenderTransform is TranslateTransform transform)
            transform.X = 0d;
    }

    /// <summary>无动画退出时把滚动容器放到滑出位置。</summary>
    private static void SetScrollExitTransform(MyScrollViewer? scroll)
    {
        if (scroll is null)
            return;

        if (scroll.RenderTransform is not TranslateTransform)
        {
            scroll.RenderTransformOrigin = RelativePoint.Center;
            scroll.RenderTransform = new TranslateTransform();
        }

        ((TranslateTransform)scroll.RenderTransform).X = 10d;
    }

    /// <summary>
    ///     禁用页面切换动画的控件列表。
    /// </summary>
    public List<Control> disabledPageAnimControls = [];

    /// <summary>
    ///     遍历获取所有需要生成动画的控件。
    /// </summary>
    internal IEnumerable<Control> GetAllAnimControls(
        Control element,
        bool ignoreInvisibility = false)
    {
        var allControls = new List<Control>();
        _GetAllAnimControls(element, allControls, ignoreInvisibility);
        return allControls.Except(disabledPageAnimControls);
    }

    private static void _GetAllAnimControls(
        Control element,
        List<Control> allControls,
        bool ignoreInvisibility)
    {
        while (true)
        {
            if (!ignoreInvisibility && !element.IsVisible) return;

            switch (element)
            {
                case MyCard:
                case MyHint:
                case MyExtraTextButton:
                case TextBlock:
                case MyTextButton:
                {
                    allControls.Add(element);
                    break;
                }

                case ContentControl contentControl:
                {
                    if (contentControl.Content is Control content)
                    {
                        element = content;
                        continue;
                    }

                    break;
                }

                case Decorator decorator:
                {
                    if (decorator.Child is not null)
                    {
                        element = decorator.Child;
                        continue;
                    }

                    break;
                }

                case Panel panel:
                {
                    foreach (var child in panel.Children.OfType<Control>())
                        _GetAllAnimControls(child, allControls, ignoreInvisibility);
                    break;
                }
            }

            break;
        }
    }

    #endregion
}