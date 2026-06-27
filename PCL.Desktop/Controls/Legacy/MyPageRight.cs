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
        PageState = PageStates.ContentEnter;
        if (_pageContentPanel is not null)
            TriggerEnterAnimation(_pageAlwaysPanel, _pageContentPanel);
        else if (Content is Control content)
            TriggerEnterAnimation(content);
        PageState = PageStates.ContentStay;
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
        PageOnEnter();
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
        foreach (Control element in elements.OfType<Control>())
        {
            element.IsVisible = true;
            foreach (Control control in GetAllAnimControls(element, ignoreInvisibility: true))
            {
                control.IsHitTestVisible = true;
                if (!DisabledPageAnimControls.Contains(control))
                {
                    control.Opacity = 1d;
                    control.RenderTransform = null;
                }
            }
        }
    }

    public void TriggerExitAnimation(params Control?[] elements)
    {
        foreach (Control element in elements.OfType<Control>())
        {
            foreach (Control control in GetAllAnimControls(element))
            {
                control.IsHitTestVisible = false;
                if (!DisabledPageAnimControls.Contains(control))
                {
                    control.Opacity = 0d;
                    control.RenderTransform = new TranslateTransform(0d, -6d);
                }
            }

            element.IsVisible = false;
        }

        if (PageState is PageStates.PageExit or PageStates.ContentExit)
            PageState = PageStates.Empty;
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
