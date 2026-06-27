// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using PCL.Desktop;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Views;

namespace PCL.Desktop.Test;

[TestClass]
public sealed class AvaloniaHeadlessTests
{
    [TestMethod]
    public void MainWindow_LoadsPclChromeAndCanRenderHeadless()
    {
        using HeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual(WindowDecorations.None, window.WindowDecorations);
                Assert.IsNotNull(window.FindControl<MyIconButton>("BtnTitleClose"));
                Assert.IsNotNull(window.FindControl<MyIconButton>("BtnTitleMin"));
                Assert.IsNotNull(window.FindControl<MyListItem>("BtnTitleSelect0"));
                Assert.IsNotNull(window.FindControl<MyListItem>("BtnTitleSelect4"));
                Assert.IsNotNull(window.FindControl<AnimatedBackgroundGrid>("PanTitle"));
                Assert.IsNotNull(window.Icon);
                Assert.IsTrue(window.FindControl<MyListItem>("BtnTitleSelect0")!.Checked);
                Assert.IsFalse(window.FindControl<MyListItem>("BtnTitleSelect1")!.Checked);
                Assert.AreEqual(20d, GetCheckIndicator(window.FindControl<MyListItem>("BtnTitleSelect0")!).Height);
                Assert.AreEqual(0d, GetCheckIndicator(window.FindControl<MyListItem>("BtnTitleSelect1")!).Height);
                Assert.IsTrue(window.FindControl<Avalonia.Controls.Shapes.Path>("ShapeTitleLogo")!.IsVisible);
                Assert.IsFalse(window.FindControl<Avalonia.Controls.Shapes.Path>("ShapeHMCLTitleLogo")!.IsVisible);
                Assert.IsFalse(window.FindControl<MyImage>("ImageHMCLTitleLogo")!.IsVisible);
                Assert.AreEqual("正在加载启动页面", window.FindControl<MyLoading>("LoadMain")!.Text);
                Assert.IsNotNull(window.CaptureRenderedFrame());
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void LegacyControls_HandleHeadlessPointerInput()
    {
        using HeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyButton button = new()
            {
                Text = "测试按钮",
                Width = 120,
                Height = 36
            };
            MyCheckBox checkBox = new()
            {
                Text = "测试复选框",
                Width = 150,
                Height = 30
            };
            StackPanel panel = new()
            {
                Margin = new Thickness(20),
                Spacing = 12,
                Children =
                {
                    button,
                    checkBox
                }
            };
            Window window = new()
            {
                Width = 320,
                Height = 200,
                Content = panel
            };

            bool buttonClicked = false;
            button.Click += (_, _) => buttonClicked = true;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Click(window, button);
                Click(window, checkBox);

                Assert.IsTrue(buttonClicked);
                Assert.AreEqual(true, checkBox.Checked);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MainWindow_NavigationListKeepsSingleSelection()
    {
        using HeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                MyListItem launch = window.FindControl<MyListItem>("BtnTitleSelect0")!;
                MyListItem download = window.FindControl<MyListItem>("BtnTitleSelect1")!;

                Click(window, download);

                Assert.IsFalse(launch.Checked);
                Assert.IsTrue(download.Checked);
                Assert.AreEqual(0d, GetCheckIndicator(launch).Height);
                Assert.AreEqual(20d, GetCheckIndicator(download).Height);
                AdvancePageChangeAnimation(window);
                Assert.AreEqual("正在加载下载页面", window.FindControl<MyLoading>("LoadMain")!.Text);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MainWindow_NavigationSwitchFadesPageContent()
    {
        using HeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Control right = window.FindControl<Control>("PanMainRight")!;
                Click(window, window.FindControl<MyListItem>("BtnTitleSelect2")!);

                InvokePrivateTick(window, "PageChangeTimer_Tick", 3);
                Assert.IsTrue(right.Opacity < 1d);

                AdvancePageChangeAnimation(window);
                Assert.AreEqual(1d, right.Opacity, 0.01d);
                Assert.AreEqual("正在加载社区页面", window.FindControl<MyLoading>("LoadMain")!.Text);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void SplashWindow_RendersStartupIcon()
    {
        using HeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            SplashWindow splash = new();
            try
            {
                splash.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsNotNull(splash.CaptureRenderedFrame());
            }
            finally
            {
                splash.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyLoading_AnimatesPickaxeLoop()
    {
        using HeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyLoading loading = new()
            {
                Text = "正在加载"
            };
            Window window = new()
            {
                Width = 220,
                Height = 140,
                Content = loading
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Thread.Sleep(650);
                InvokePrivateTick(loading, "LoopTimer_Tick", 1);

                var pickaxe = loading.FindControl<Avalonia.Controls.Shapes.Path>("PathPickaxe")!;
                var rotate = (Avalonia.Media.RotateTransform)pickaxe.RenderTransform!;
                Assert.AreEqual(new Thickness(10d, 6d, 0d, 0d), pickaxe.Margin);
                Assert.AreEqual(HorizontalAlignment.Left, pickaxe.HorizontalAlignment);
                Assert.AreEqual(VerticalAlignment.Top, pickaxe.VerticalAlignment);
                Assert.AreEqual(40d, rotate.CenterX, 0.01d);
                Assert.AreEqual(36d, rotate.CenterY, 0.01d);
                Assert.IsTrue(rotate.Angle < 35d, $"Expected the WPF strike posture, got {rotate.Angle:0.00} degrees.");
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyRadioBox_KeepsWpfSingleSelectionBehavior()
    {
        using HeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyRadioBox first = new() { Text = "默认", Width = 120, Height = 24, Checked = true };
            MyRadioBox second = new() { Text = "自定义", Width = 120, Height = 24 };
            StackPanel panel = new()
            {
                Margin = new Thickness(20),
                Spacing = 8,
                Children =
                {
                    first,
                    second
                }
            };
            Window window = new()
            {
                Width = 220,
                Height = 130,
                Content = panel
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("默认", first.FindControl<TextBlock>("LabText")!.Text);
                Assert.AreEqual("自定义", second.FindControl<TextBlock>("LabText")!.Text);
                Assert.IsTrue(first.Checked);
                Assert.IsFalse(second.Checked);

                Click(window, second);

                Assert.IsFalse(first.Checked);
                Assert.IsTrue(second.Checked);

                first.PreviewCheck += (_, e) => e.Handled = true;
                Click(window, first);
                Assert.IsFalse(first.Checked);
                Assert.IsTrue(second.Checked);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MySearchBar_SyncsTextAndClearButton()
    {
        using HeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MySearchBar searchBar = new()
            {
                Width = 260,
                HintText = "搜索版本",
                Text = "1.20"
            };
            Window window = new()
            {
                Width = 320,
                Height = 120,
                Content = new Border
                {
                    Padding = new Thickness(20),
                    Child = searchBar
                }
            };

            bool changed = false;
            searchBar.TextChanged += (_, _) => changed = true;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                MyTextBox textBox = searchBar.FindControl<MyTextBox>("TextBox")!;
                MyIconButton clear = searchBar.FindControl<MyIconButton>("BtnClear")!;
                Assert.AreEqual("搜索版本", textBox.HintText);
                Assert.AreEqual("1.20", textBox.Text);
                Assert.AreEqual(1d, clear.Opacity, 0.01d);
                Assert.IsTrue(clear.IsHitTestVisible);

                Click(window, clear);

                Assert.AreEqual(string.Empty, searchBar.Text);
                Assert.AreEqual(string.Empty, textBox.Text);
                Assert.IsFalse(clear.IsHitTestVisible);
                Assert.IsTrue(changed);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyExtraTextButton_UsesWpfStructureAndRaisesClick()
    {
        using HeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyExtraTextButton button = new()
            {
                Text = "开始下载",
                Show = true,
                Width = 180
            };
            Window window = new()
            {
                Width = 260,
                Height = 150,
                Content = new Border
                {
                    Padding = new Thickness(20),
                    Child = button
                }
            };

            bool clicked = false;
            button.Click += (_, _) => clicked = true;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("开始下载", button.FindControl<TextBlock>("LabText")!.Text);
                Assert.IsFalse(button.FindControl<Grid>("IconHost")!.IsVisible);
                Assert.AreEqual(1d, ((Avalonia.Media.ScaleTransform)button.RenderTransform!).ScaleX, 0.01d);

                button.Logo = "M0,0 L10,5 L0,10 Z";
                Assert.IsTrue(button.FindControl<Grid>("IconHost")!.IsVisible);

                Click(window, button);
                Assert.IsTrue(clicked);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MainWindow_NavigationToggleUsesMeasuredAnimatedWidth()
    {
        using HeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Control navLayer = window.FindControl<Control>("PanNavLayer")!;
                Control toggle = window.FindControl<Control>("BtnNavToggle")!;
                window.FindControl<MyListItem>("BtnTitleSelect1")!.Title = "下载资源与游戏版本管理";

                Click(window, toggle);
                double expandedTarget = GetPrivateDouble(window, "_navAnimTarget");
                Assert.IsTrue(expandedTarget > 138d);

                AdvanceNavigationAnimation(window);
                Assert.AreEqual(expandedTarget, navLayer.Width, 0.5d);

                Click(window, toggle);
                double collapsedTarget = GetPrivateDouble(window, "_navAnimTarget");
                Assert.AreEqual(50d, collapsedTarget, 0.01d);

                AdvanceNavigationAnimation(window);
                Assert.AreEqual(50d, navLayer.Width, 0.5d);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void SvgIcon_LoadsLucideAssetsThroughDesktopResources()
    {
        using HeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            SvgIcon icon = new()
            {
                Icon = "lucide/settings",
                IconBrush = Avalonia.Media.Brushes.Black,
                Width = 24,
                Height = 24
            };
            Window window = new()
            {
                Width = 80,
                Height = 80,
                Content = icon
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsNotNull(window.CaptureRenderedFrame());
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    private static HeadlessUnitTestSession CreateSession() =>
        HeadlessUnitTestSession.StartNew(
            typeof(App),
            AvaloniaTestIsolationLevel.PerTest);

    private static void Click(Window window, Control control)
    {
        Point center = control
            .TranslatePoint(
                new Point(control.Bounds.Width / 2, control.Bounds.Height / 2),
                window)
            ?? throw new InvalidOperationException("Control is not attached.");

        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
    }

    private static Border GetCheckIndicator(MyListItem item) =>
        item.Children
            .OfType<Border>()
            .Single(border => Math.Abs(border.Width - 5d) < 0.01d);

    private static void AdvanceNavigationAnimation(MainWindow window)
    {
        InvokePrivateTick(window, "NavAnimTimer_Tick", 14);
    }

    private static void AdvancePageChangeAnimation(MainWindow window)
    {
        InvokePrivateTick(window, "PageChangeTimer_Tick", 22);
    }

    private static void InvokePrivateTick(MainWindow window, string methodName, int count)
    {
        InvokePrivateTick((object)window, methodName, count);
    }

    private static void InvokePrivateTick(object target, string methodName, int count)
    {
        var method = target.GetType().GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");
        for (int i = 0; i < count; i++)
            method.Invoke(target, [null, EventArgs.Empty]);
    }

    private static double GetPrivateDouble(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        return (double)field.GetValue(instance)!;
    }
}
