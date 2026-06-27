// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Desktop.Controls.Legacy;

namespace PCL.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly Stopwatch _showAnimationClock = new();
    private Control? _showAnimationRoot;
    private RotateTransform? _showAnimationRotate;
    private TranslateTransform? _showAnimationTranslate;
    private DispatcherTimer? _showAnimationTimer;
    private bool _showAnimationStarted;
    private bool _isNavExpanded;

    private static readonly Dictionary<int, string> NavPageTitles = new()
    {
        [0] = "启动",
        [1] = "下载",
        [2] = "社区",
        [3] = "设置",
        [4] = "在线"
    };

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        Opacity = 0d;
        CanResize = true;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
        CaptureShowAnimationTransforms();
        Opened += (_, _) => StartShowAnimation();
        SyncTitleOverlayWidth();
        SelectNavPage(0);
    }

    private void FormMain_KeyDown(object? sender, KeyEventArgs e)
    {
    }

    private void FormMain_MouseDown(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            e.GetPosition(this).Y <= 48)
        {
            BeginMoveDrag(e);
        }
    }

    private void FormMain_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
    }

    private void FormMain_Closing(object? sender, WindowClosingEventArgs e)
    {
    }

    private void FormMain_Activated(object? sender, EventArgs e)
    {
    }

    private void FrmMain_Drop(object? sender, DragEventArgs e)
    {
    }

    private void FormMain_MouseMove(object? sender, PointerEventArgs e)
    {
    }

    private void VideoEnded(object? sender, EventArgs e)
    {
    }

    private void PanTitle_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        SyncTitleOverlayWidth();
    }

    private void BtnTitleClose_Click(object? sender, EventArgs e) => Close();

    private void BtnTitleMin_Click(object? sender, EventArgs e) =>
        WindowState = WindowState.Minimized;

    private void BtnTitleHelp_Click(object? sender, EventArgs e)
    {
    }

    private void BtnTitleInner_Click(object? sender, EventArgs e)
    {
    }

    private void BtnNavItem_Click(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not MyListItem item || !TryGetNavPage(item, out int page))
            return;

        SelectNavPage(page);
        e.Handled = true;
    }

    private void BtnNavToggle_Click(object? sender, EventArgs e)
    {
        _isNavExpanded = !_isNavExpanded;
        if (this.FindControl<Control>("PanNavLayer") is { } navLayer)
            navLayer.Width = _isNavExpanded ? 138d : 48d;
    }

    private void PanMainLeft_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
    }

    private void BtnExtraUpdateRestart_Click(object? sender, EventArgs e)
    {
    }

    private void BtnExtraBack_Click(object? sender, EventArgs e)
    {
    }

    private void BtnExtraDownload_Click(object? sender, EventArgs e)
    {
    }

    private void BtnExtraApril_Click(object? sender, EventArgs e)
    {
    }

    private void BtnExtraShutdown_Click(object? sender, EventArgs e)
    {
    }

    private void BtnExtraLog_Click(object? sender, EventArgs e)
    {
    }

    private void BtnExtraMusic_Click(object? sender, EventArgs e)
    {
    }

    private void BtnExtraMusic_RightClick(object? sender, PointerReleasedEventArgs e)
    {
    }

    private void FormDragMove(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void SyncTitleOverlayWidth()
    {
        Control? panTitle = this.FindControl<Control>("PanTitle");
        Control? panTitleMain = this.FindControl<Control>("PanTitleMain");
        Control? panTitleInner = this.FindControl<Control>("PanTitleInner");
        if (panTitle is null)
            return;

        double width = panTitle.Bounds.Width;
        if (width <= 0)
            width = Width;
        if (panTitleMain is not null)
            panTitleMain.Width = width;
        if (panTitleInner is not null)
            panTitleInner.Width = width;
    }

    private void SelectNavPage(int page)
    {
        if (!NavPageTitles.ContainsKey(page))
            page = 0;

        MyListItem? selected = null;
        foreach (MyListItem item in GetNavItems())
        {
            if (TryGetNavPage(item, out int itemPage) && itemPage == page)
            {
                selected = item;
                break;
            }
        }

        if (selected is null)
            return;

        selected.Checked = true;
        foreach (MyListItem item in GetNavItems())
        {
            if (!ReferenceEquals(item, selected))
                item.Checked = false;
        }

        if (this.FindControl<MyLoading>("LoadMain") is { } loading)
            loading.Text = $"正在加载{NavPageTitles[page]}页面";
    }

    private IEnumerable<MyListItem> GetNavItems()
    {
        if (this.FindControl<Panel>("PanTitleSelect") is not { } panel)
            yield break;

        foreach (Control child in panel.Children)
        {
            if (child is MyListItem item)
                yield return item;
        }
    }

    private static bool TryGetNavPage(MyListItem item, out int page)
    {
        page = 0;
        return item.Tag switch
        {
            int value => SetPage(value, out page),
            string text => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out page),
            _ => false
        };
    }

    private static bool SetPage(int value, out int page)
    {
        page = value;
        return true;
    }

    private void CaptureShowAnimationTransforms()
    {
        if (Content is not Control root)
            return;

        _showAnimationRoot = root;
        if (root.RenderTransform is not TransformGroup group)
            return;

        foreach (ITransform transform in group.Children)
        {
            _showAnimationRotate ??= transform as RotateTransform;
            _showAnimationTranslate ??= transform as TranslateTransform;
        }
    }

    private void StartShowAnimation()
    {
        if (_showAnimationStarted)
            return;

        _showAnimationStarted = true;
        _showAnimationClock.Restart();
        _showAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _showAnimationTimer.Tick += ShowAnimationTimer_Tick;
        _showAnimationTimer.Start();
        ShowAnimationTimer_Tick(this, EventArgs.Empty);
    }

    private void ShowAnimationTimer_Tick(object? sender, EventArgs e)
    {
        double elapsed = _showAnimationClock.Elapsed.TotalMilliseconds;
        double delayed = Math.Max(0d, elapsed - 100d);

        Opacity = EaseOutCubic(Normalize(delayed, 250d));

        if (_showAnimationTranslate is not null)
            _showAnimationTranslate.Y = 60d * (1d - EaseOutBack(Normalize(delayed, 600d)));
        if (_showAnimationRotate is not null)
            _showAnimationRotate.Angle = -4d * (1d - EaseOutBack(Normalize(delayed, 500d)));

        if (elapsed < 720d)
            return;

        _showAnimationTimer?.Stop();
        _showAnimationTimer = null;
        Opacity = 1d;
        if (_showAnimationTranslate is not null)
            _showAnimationTranslate.Y = 0d;
        if (_showAnimationRotate is not null)
            _showAnimationRotate.Angle = 0d;
        if (_showAnimationRoot is not null)
            _showAnimationRoot.RenderTransform = null;
    }

    private static double Normalize(double elapsedMilliseconds, double durationMilliseconds)
    {
        return Math.Clamp(elapsedMilliseconds / durationMilliseconds, 0d, 1d);
    }

    private static double EaseOutCubic(double progress)
    {
        double inverse = 1d - progress;
        return 1d - inverse * inverse * inverse;
    }

    private static double EaseOutBack(double progress)
    {
        const double overshoot = 1.15d;
        double shifted = progress - 1d;
        return 1d + shifted * shifted * ((overshoot + 1d) * shifted + overshoot);
    }
}
