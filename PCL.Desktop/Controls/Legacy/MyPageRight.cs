// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace PCL.Desktop.Controls.Legacy;

public class MyPageRight : ContentControl, IDisposable
{
    public enum PageStates
    {
        Empty,
        LoaderWait,
        LoaderEnter,
        LoaderStayForce,
        LoaderStay,
        LoaderExit,
        ContentEnter,
        ContentStay,
        ContentExit,
        PageExit
    }

    public static readonly StyledProperty<MyScrollViewer?> PanScrollProperty =
        AvaloniaProperty.Register<MyPageRight, MyScrollViewer?>(nameof(PanScroll));

    private Func<CancellationToken, Task>? _pageLoader;
    private Action? _pageLoaderFinished;
    private CancellationTokenSource? _pageLoaderCancellation;
    private Control? _pageLoaderPanel;
    private Control? _pageContentPanel;
    private Control? _pageAlwaysPanel;
    private LoaderRunState _pageLoaderState = LoaderRunState.Waiting;
    private bool _pageLoaderAutoRun;

    protected override Type StyleKeyOverride => typeof(ContentControl);

    public int PageUuid { get; } = Random.Shared.Next();

    public List<Control> DisabledPageAnimControls { get; } = [];

    public MyScrollViewer? PanScroll
    {
        get => GetValue(PanScrollProperty);
        set => SetValue(PanScrollProperty, value);
    }

    public PageStates PageState { get; set; } = PageStates.Empty;

    public event Action? PageEnter;

    public event Action? PageExit;

    private enum LoaderRunState
    {
        Waiting,
        Loading,
        Finished,
        Failed
    }

    public void PageLoaderInit(
        MyLoading loaderUi,
        Control panLoader,
        Control panContent,
        Control? panAlways,
        Func<CancellationToken, Task> realLoader,
        Action? finishedInvoke = null,
        bool autoRun = true)
    {
        _pageLoader = realLoader;
        _pageLoaderFinished = finishedInvoke;
        _pageLoaderPanel = panLoader;
        _pageContentPanel = panContent;
        _pageAlwaysPanel = panAlways;
        _pageLoaderAutoRun = autoRun;
        _pageLoaderState = LoaderRunState.Waiting;

        loaderUi.Text = "正在加载";
        panLoader.IsVisible = false;
        panContent.IsVisible = false;
        if (panAlways is not null)
            panAlways.IsVisible = false;

        if (autoRun)
            PageLoaderRestart();
    }

    public void PageLoaderRestart(object? input = null, bool isForceRestart = true)
    {
        if (!_pageLoaderAutoRun || _pageLoader is null)
            return;

        _pageLoaderCancellation?.Cancel();
        _pageLoaderCancellation?.Dispose();
        _pageLoaderCancellation = new CancellationTokenSource();

        _pageLoaderState = LoaderRunState.Loading;
        HandleLoaderStarted();
        _ = RunPageLoaderAsync(_pageLoaderCancellation);
    }

    private async Task RunPageLoaderAsync(CancellationTokenSource cancellation)
    {
        CancellationToken cancellationToken = cancellation.Token;
        try
        {
            await _pageLoader!(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                _pageLoaderFinished?.Invoke();
                _pageLoaderState = LoaderRunState.Finished;
                HandleLoaderFinished();
            });
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (ReferenceEquals(_pageLoaderCancellation, cancellation))
                    _pageLoaderState = LoaderRunState.Waiting;
            });
        }
        catch
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                _pageLoaderState = LoaderRunState.Failed;
                HandleLoaderFailed();
            });
        }
    }

    public void PageOnEnter()
    {
        PageEnter?.Invoke();
        switch (PageState)
        {
            case PageStates.Empty:
                EnterFromEmpty(includeAlways: true);
                break;
            case PageStates.ContentExit:
                EnterFromEmpty(includeAlways: false);
                break;
            case PageStates.ContentEnter:
                break;
        }
    }

    public void PageOnExit()
    {
        PageExit?.Invoke();
        switch (PageState)
        {
            case PageStates.ContentEnter:
            case PageStates.ContentStay:
                PageState = PageStates.PageExit;
                TriggerExitAnimation(_pageAlwaysPanel, GetContentTarget());
                break;
            case PageStates.LoaderEnter:
            case PageStates.LoaderStayForce:
            case PageStates.LoaderStay:
                PageState = PageStates.PageExit;
                TriggerExitAnimation(_pageAlwaysPanel, _pageLoaderPanel);
                break;
            case PageStates.LoaderWait:
                PageState = PageStates.PageExit;
                TriggerExitAnimation(_pageAlwaysPanel);
                break;
            case PageStates.LoaderExit:
            case PageStates.ContentExit:
                PageState = PageStates.PageExit;
                if (_pageAlwaysPanel is not null)
                    TriggerExitAnimation(_pageAlwaysPanel, GetContentTarget());
                break;
        }
    }

    public void PageOnForceExit()
    {
        _pageLoaderCancellation?.Cancel();
        PageState = PageStates.Empty;
        ModAnimation.AniStop("PageRight PageChange " + PageUuid);
        if (_pageContentPanel is not null)
            _pageContentPanel.IsVisible = false;
        if (_pageLoaderPanel is not null)
            _pageLoaderPanel.IsVisible = false;
        if (_pageAlwaysPanel is not null)
            _pageAlwaysPanel.IsVisible = false;
        if (_pageContentPanel is null && Content is Control content)
            content.IsVisible = false;
    }

    public void PageOnContentExit()
    {
        switch (PageState)
        {
            case PageStates.ContentEnter:
            case PageStates.ContentStay:
                PageState = PageStates.ContentExit;
                TriggerExitAnimation(GetContentTarget());
                break;
            case PageStates.LoaderExit:
                PageState = PageStates.ContentExit;
                break;
            case PageStates.LoaderEnter:
            case PageStates.LoaderStayForce:
            case PageStates.LoaderStay:
                PageState = PageStates.ContentExit;
                TriggerExitAnimation(_pageLoaderPanel);
                break;
            case PageStates.LoaderWait:
            case PageStates.Empty:
                PageOnEnter();
                break;
        }
    }

    public virtual void Dispose()
    {
        _pageLoaderCancellation?.Cancel();
        _pageLoaderCancellation?.Dispose();
        _pageLoaderCancellation = null;
        GC.SuppressFinalize(this);
    }

    public void TriggerEnterAnimation(params Control?[] elements)
    {
        Control[] realElements = elements.OfType<Control>().ToArray();
        foreach (Control element in realElements)
        {
            element.IsVisible = true;
            foreach (Control control in GetAllAnimControls(element, ignoreInvisibility: true))
            {
                control.IsHitTestVisible = true;
                if (control.RenderTransform is TranslateTransform)
                    control.RenderTransform = null;
            }
        }

        List<ModAnimation.AniData> animations = [];
        int delay = 0;
        foreach (Control element in realElements)
        {
            foreach (Control control in GetAllAnimControls(element))
            {
                if (DisabledPageAnimControls.Contains(control))
                    continue;
                if (control is MyExtraTextButton extraTextButton)
                {
                    extraTextButton.Show = true;
                    continue;
                }

                control.Opacity = 0d;
                control.RenderTransform = new TranslateTransform(0d, -16d);
                animations.Add(ModAnimation.AaOpacity(
                    control,
                    1d,
                    100,
                    delay,
                    new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
                animations.Add(ModAnimation.AaTranslateY(control, 5d, 250, delay, new ModAnimation.AniEaseOutFluent()));
                animations.Add(ModAnimation.AaTranslateY(control, 11d, 350, delay, new ModAnimation.AniEaseOutBack()));
                delay += 25;
            }
        }

        Control? scrollBar = GetFirstScrollBar(realElements);
        if (scrollBar is not null)
        {
            if (scrollBar.RenderTransform is not TranslateTransform)
                scrollBar.RenderTransform = new TranslateTransform(10d, 0d);
            animations.Add(ModAnimation.AaTranslateX(
                scrollBar,
                -((TranslateTransform)scrollBar.RenderTransform).X,
                350,
                0,
                new ModAnimation.AniEaseOutFluent()));
        }

        animations.Add(ModAnimation.AaCode(PageOnEnterAnimationFinished, after: true));
        ModAnimation.AniStart(animations, "PageRight PageChange " + PageUuid, true);
    }

    public void TriggerExitAnimation(params Control?[] elements)
    {
        Control[] realElements = elements.OfType<Control>().ToArray();
        List<ModAnimation.AniData> animations = [];
        int delay = 0;
        foreach (Control element in realElements)
        {
            foreach (Control control in GetAllAnimControls(element))
            {
                if (DisabledPageAnimControls.Contains(control))
                    continue;
                if (control is MyExtraTextButton extraTextButton)
                {
                    extraTextButton.Show = false;
                    continue;
                }

                control.IsHitTestVisible = false;
                animations.Add(ModAnimation.AaOpacity(control, -1d, 70, delay));
                animations.Add(ModAnimation.AaTranslateY(control, -6d, 70, delay));
                delay += 15;
            }
        }

        Control? scrollBar = GetFirstScrollBar(realElements);
        if (scrollBar is not null)
        {
            if (scrollBar.RenderTransform is not TranslateTransform)
                scrollBar.RenderTransform = new TranslateTransform();
            animations.Add(ModAnimation.AaTranslateX(
                scrollBar,
                10d - ((TranslateTransform)scrollBar.RenderTransform).X,
                90,
                0,
                new ModAnimation.AniEaseInFluent()));
        }

        animations.Add(ModAnimation.AaCode(() =>
        {
            foreach (Control element in realElements)
                element.IsVisible = false;
            PageOnExitAnimationFinished();
        }, after: true));
        ModAnimation.AniStart(animations, "PageRight PageChange " + PageUuid);
    }

    private void PageOnEnterAnimationFinished()
    {
        PageState = PageState switch
        {
            PageStates.ContentEnter => PageStates.ContentStay,
            PageStates.LoaderEnter => PageStates.LoaderStayForce,
            _ => PageState
        };
        if (PageState == PageStates.LoaderStayForce)
            ModAnimation.AniStart(ModAnimation.AaCode(PageOnLoaderStayFinished, 400), "PageRight PageChange " + PageUuid);
    }

    private void PageOnExitAnimationFinished()
    {
        switch (PageState)
        {
            case PageStates.PageExit:
                PageState = PageStates.Empty;
                break;
            case PageStates.ContentExit:
                PageOnEnter();
                break;
            case PageStates.LoaderExit:
                PageState = PageStates.ContentEnter;
                TriggerEnterAnimation(GetContentTarget());
                break;
        }
    }

    private void PageOnLoaderWaitFinished()
    {
        if (PageState != PageStates.LoaderWait)
            return;

        switch (_pageLoaderState)
        {
            case LoaderRunState.Loading:
            case LoaderRunState.Failed:
                PageState = PageStates.LoaderEnter;
                TriggerEnterAnimation(GetHiddenAlwaysPanel(), _pageLoaderPanel);
                break;
            case LoaderRunState.Finished:
            case LoaderRunState.Waiting:
                PageState = PageStates.ContentEnter;
                TriggerEnterAnimation(GetHiddenAlwaysPanel(), GetContentTarget());
                break;
        }
    }

    private void PageOnLoaderStayFinished()
    {
        if (PageState != PageStates.LoaderStayForce)
            return;

        if (_pageLoaderState == LoaderRunState.Finished)
        {
            PageState = PageStates.LoaderExit;
            TriggerExitAnimation(_pageLoaderPanel);
        }
        else
        {
            PageState = PageStates.LoaderStay;
        }
    }

    private void EnterFromEmpty(bool includeAlways)
    {
        switch (_pageLoaderState)
        {
            case LoaderRunState.Loading:
                PageState = PageStates.LoaderWait;
                ModAnimation.AniStart(ModAnimation.AaCode(PageOnLoaderWaitFinished, 400), "PageRight PageChange " + PageUuid);
                break;
            case LoaderRunState.Failed:
                PageState = PageStates.LoaderEnter;
                TriggerEnterAnimation(includeAlways ? _pageAlwaysPanel : null, _pageLoaderPanel);
                break;
            case LoaderRunState.Finished:
            case LoaderRunState.Waiting:
                PageState = PageStates.ContentEnter;
                TriggerEnterAnimation(includeAlways ? _pageAlwaysPanel : null, GetContentTarget());
                break;
        }
    }

    private void HandleLoaderStarted()
    {
        switch (PageState)
        {
            case PageStates.ContentEnter:
            case PageStates.ContentStay:
                PageState = PageStates.ContentExit;
                TriggerExitAnimation(GetContentTarget());
                break;
            case PageStates.LoaderExit:
                PageState = PageStates.ContentExit;
                break;
        }
    }

    private void HandleLoaderFinished()
    {
        switch (PageState)
        {
            case PageStates.LoaderWait:
                PageState = PageStates.ContentEnter;
                TriggerEnterAnimation(GetHiddenAlwaysPanel(), GetContentTarget());
                break;
            case PageStates.LoaderStay:
                PageState = PageStates.LoaderExit;
                TriggerExitAnimation(_pageLoaderPanel);
                break;
        }
    }

    private void HandleLoaderFailed()
    {
        if (PageState == PageStates.LoaderWait)
        {
            PageState = PageStates.LoaderEnter;
            TriggerEnterAnimation(GetHiddenAlwaysPanel(), _pageLoaderPanel);
        }
    }

    private Control? GetContentTarget() => _pageContentPanel ?? Content as Control;

    private Control? GetHiddenAlwaysPanel() => IsControlVisible(_pageAlwaysPanel) ? null : _pageAlwaysPanel;

    private static bool IsControlVisible(Control? control) => control?.IsVisible == true;

    internal static IEnumerable<Control> GetAllAnimControls(Control element, bool ignoreInvisibility = false)
    {
        if (!ignoreInvisibility && !element.IsVisible)
            yield break;

        if (element is MyCard or MyHint or MyExtraTextButton or TextBlock or MyTextButton)
        {
            yield return element;
            yield break;
        }

        if (element is ContentControl { Content: Control content })
        {
            foreach (Control child in GetAllAnimControls(content, ignoreInvisibility))
                yield return child;
            yield break;
        }

        if (element is Panel panel)
        {
            foreach (Control child in panel.Children)
            {
                foreach (Control nested in GetAllAnimControls(child, ignoreInvisibility))
                    yield return nested;
            }
        }
    }

    private static Control? GetFirstScrollBar(IEnumerable<Control> elements)
    {
        foreach (Control element in elements)
        {
            if (TryGetVisibleVerticalScrollBar(element, out Control? directScrollBar))
                return directScrollBar;

            foreach (ScrollBar scrollBar in element.GetVisualDescendants().OfType<ScrollBar>())
            {
                if (TryGetVisibleVerticalScrollBar(scrollBar, out Control? nestedScrollBar))
                    return nestedScrollBar;
            }
        }

        return null;
    }

    private static bool TryGetVisibleVerticalScrollBar(Control control, out Control? scrollBar)
    {
        scrollBar = null;
        if (control is not ScrollBar { Orientation: Orientation.Vertical } bar || !bar.IsVisible)
            return false;

        scrollBar = bar;
        return true;
    }
}
