// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

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
    private bool _pageLoaderAutoRun;
    private readonly string _pageUuid = Guid.NewGuid().ToString("N");

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

        loaderUi.Text = "正在加载";
        panLoader.IsVisible = false;
        panContent.IsVisible = false;
        if (panAlways is not null)
            panAlways.IsVisible = false;

        if (autoRun)
            PageLoaderRestart();
    }

    public async void PageLoaderRestart(object? input = null, bool isForceRestart = true)
    {
        if (!_pageLoaderAutoRun || _pageLoader is null)
            return;

        _pageLoaderCancellation?.Cancel();
        _pageLoaderCancellation?.Dispose();
        _pageLoaderCancellation = new CancellationTokenSource();

        PageState = PageStates.LoaderEnter;
        if (_pageContentPanel is not null)
            _pageContentPanel.IsVisible = false;
        TriggerEnterAnimation(_pageAlwaysPanel, _pageLoaderPanel);
        try
        {
            await _pageLoader(_pageLoaderCancellation.Token).ConfigureAwait(true);
            _pageLoaderFinished?.Invoke();
            PageState = PageStates.ContentEnter;
            TriggerExitAnimation(_pageLoaderPanel);
            TriggerEnterAnimation(_pageAlwaysPanel, _pageContentPanel);
        }
        catch (OperationCanceledException)
        {
            PageState = PageStates.Empty;
        }
        catch
        {
            PageState = PageStates.LoaderStay;
            TriggerEnterAnimation(_pageAlwaysPanel, _pageLoaderPanel);
        }
    }

    public void PageOnEnter()
    {
        PageEnter?.Invoke();
        if (PageState is PageStates.LoaderEnter or PageStates.LoaderStayForce or PageStates.LoaderStay or PageStates.LoaderWait)
        {
            if (_pageContentPanel is not null)
                _pageContentPanel.IsVisible = false;
            TriggerEnterAnimation(_pageAlwaysPanel, _pageLoaderPanel);
            return;
        }

        PageState = PageStates.ContentEnter;
        if (_pageContentPanel is not null)
            TriggerEnterAnimation(_pageAlwaysPanel, _pageContentPanel);
        else if (Content is Control content)
            TriggerEnterAnimation(content);
    }

    public void PageOnExit()
    {
        PageExit?.Invoke();
        PageState = PageStates.PageExit;
        if (_pageContentPanel is not null)
            TriggerExitAnimation(_pageAlwaysPanel, _pageContentPanel);
        else if (Content is Control content)
            TriggerExitAnimation(content);
    }

    public void PageOnForceExit()
    {
        _pageLoaderCancellation?.Cancel();
        PageState = PageStates.Empty;
        ModAnimation.AniStop($"PageRight PageChange {_pageUuid}");
        if (_pageContentPanel is not null)
            _pageContentPanel.IsVisible = false;
        if (_pageLoaderPanel is not null)
            _pageLoaderPanel.IsVisible = false;
        if (_pageAlwaysPanel is not null)
            _pageAlwaysPanel.IsVisible = false;
    }

    public void PageOnContentExit()
    {
        PageState = PageStates.ContentExit;
        if (_pageContentPanel is not null)
            TriggerExitAnimation(_pageContentPanel);
        else if (Content is Control content)
            TriggerExitAnimation(content);
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

        animations.Add(ModAnimation.AaCode(PageOnEnterAnimationFinished, after: true));
        ModAnimation.AniStart(animations, $"PageRight PageChange {_pageUuid}", true);
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

        animations.Add(ModAnimation.AaCode(() =>
        {
            foreach (Control element in realElements)
                element.IsVisible = false;
            PageOnExitAnimationFinished();
        }, after: true));
        ModAnimation.AniStart(animations, $"PageRight PageChange {_pageUuid}");
    }

    private void PageOnEnterAnimationFinished()
    {
        PageState = PageState switch
        {
            PageStates.ContentEnter => PageStates.ContentStay,
            PageStates.LoaderEnter => PageStates.LoaderStayForce,
            _ => PageState
        };
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
                TriggerEnterAnimation(_pageContentPanel);
                break;
        }
    }

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
}
