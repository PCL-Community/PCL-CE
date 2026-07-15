// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using FluentValidation;
using PCL.Application.Accounts;
using PCL.Application.Downloads;
using PCL.Application.Hosting;
using PCL.Application.Instances;
using PCL.Application.Launching;
using PCL.Application.Settings;
using PCL.Core.App;
using PCL.Desktop;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Diagnostics;
using PCL.Desktop.Features.Community;
using PCL.Desktop.Theme;
using PCL.Desktop.Views;
using PCL.Desktop.Features.Downloads.Views;
using PCL.Desktop.Features.Instances.Views;
using PCL.Desktop.Features.Launching.Views;
using PCL.Desktop.Features.Settings.Views;
using PCL.Desktop.Features.Tasks.Views;
using PCL.Desktop.Localization;
using PCL.Domain.Minecraft.Java;
using PCL.UI.Abstractions.Navigation;

namespace PCL.Desktop.Test;

[TestClass]
[DoNotParallelize]
public sealed class AvaloniaHeadlessTests
{
    [TestMethod]
    public void MediaElement_VideoFramesStayInsideAvaloniaComposition()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            using MediaElement media = new();
            IntPtr chroma = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
            try
            {
                object?[] formatArguments = [IntPtr.Zero, chroma, 2u, 2u, 0u, 0u];
                object? result = typeof(MediaElement).GetMethod(
                        "ConfigureVideoFormat",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(media, formatArguments);
                Assert.AreEqual(1u, result);
                Assert.AreEqual(8u, formatArguments[4]);
                Assert.AreEqual(2u, formatArguments[5]);

                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.IsInstanceOfType<Avalonia.Media.Imaging.WriteableBitmap>(((Image)media).Source);

                IntPtr frameBuffer = (IntPtr)typeof(MediaElement).GetField(
                        "_frameBuffer",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .GetValue(media)!;
                byte[] pixels = Enumerable.Repeat((byte)0x7f, 16).ToArray();
                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, frameBuffer, pixels.Length);
                typeof(MediaElement).GetMethod(
                        "DisplayVideoFrame",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(media, [IntPtr.Zero, frameBuffer]);
                typeof(MediaElement).GetMethod(
                        "UpdateFrameBitmap",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(media, null);

                byte[] frameCopy = (byte[])typeof(MediaElement).GetField(
                        "_frameCopy",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .GetValue(media)!;
                Assert.AreEqual(0x7f, frameCopy[0]);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(chroma);
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void MainWindow_LoadsPclChromeAndCanRenderHeadless()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();
            try
            {
                MyIconButton closeButton = window.FindControl<MyIconButton>("BtnTitleClose")!;
                Assert.AreEqual(MyIconButton.Themes.White, closeButton.Theme);
                Assert.AreEqual(
                    Colors.White,
                    ((SolidColorBrush)closeButton.FindControl<SvgIcon>("ShapeSvgIcon")!.IconBrush!).Color);
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual(WindowDecorations.None, window.WindowDecorations);
                Assert.IsFalse(window.Topmost);
                Assert.IsNotNull(window.FindControl<MyIconButton>("BtnTitleClose"));
                Assert.IsNotNull(window.FindControl<MyIconButton>("BtnTitleMin"));
                Assert.IsNotNull(window.FindControl<MyIconButton>("BtnTitleMax"));
                CollectionAssert.AreEquivalent(
                    new[] { "North", "South", "West", "East", "NorthWest", "NorthEast", "SouthWest", "SouthEast" },
                    window.GetVisualDescendants().OfType<Border>()
                        .Select(border => border.Tag as string)
                        .Where(tag => tag is not null && Enum.TryParse<WindowEdge>(tag, out _))
                        .Cast<string>()
                        .ToArray());
                Assert.IsNotNull(window.FindControl<MyListItem>("BtnTitleSelect0"));
                Assert.IsNotNull(window.FindControl<MyListItem>("BtnTitleSelect3"));
                Assert.IsNull(window.FindControl<MyListItem>("BtnTitleSelect4"));
                Assert.IsNotNull(window.FindControl<AnimatedBackgroundGrid>("PanTitle"));
                Assert.IsNotNull(window.FindControl<Grid>("PanForm")!.Background);
                typeof(MainWindow).GetMethod(
                        "ShowHint",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(window, ["Headless notification", false]);
                Assert.AreEqual(
                    "Headless notification",
                    ((Border)window.FindControl<StackPanel>("PanHint")!.Children.Single()).Tag);
                Assert.AreEqual(
                    Color.FromArgb(0xd2, 0xfb, 0xfb, 0xfb),
                    ((SolidColorBrush)window.FindControl<Border>("PanNavLayer")!.Background!).Color);
                Assert.IsNotNull(window.Icon);
                Assert.IsTrue(window.FindControl<MyListItem>("BtnTitleSelect0")!.Checked);
                Assert.IsFalse(window.FindControl<MyListItem>("BtnTitleSelect1")!.Checked);
                Assert.AreEqual(20d, GetCheckIndicator(window.FindControl<MyListItem>("BtnTitleSelect0")!).Height);
                Assert.AreEqual(0d, GetCheckIndicator(window.FindControl<MyListItem>("BtnTitleSelect1")!).Height);
                Assert.IsTrue(window.FindControl<Avalonia.Controls.Shapes.Path>("ShapeTitleLogo")!.IsVisible);
                Assert.IsFalse(window.FindControl<Avalonia.Controls.Shapes.Path>("ShapeHMCLTitleLogo")!.IsVisible);
                Assert.IsFalse(window.FindControl<MyImage>("ImageHMCLTitleLogo")!.IsVisible);
                Assert.IsNotNull(FindVisual<PageLaunchLeft>(window));
                Assert.IsNotNull(FindVisual<PageLaunchRight>(window));
                Assert.IsNotNull(FindVisual<MyButton>(window, "BtnLaunch"));
                Assert.IsNotNull(FindVisual<MyButton>(window, "BtnInstance"));
                Assert.IsNotNull(FindVisual<Grid>(window, "PanLogin"));
                Assert.IsNotNull(FindVisual<Grid>(window, "PanLaunching"));
                Assert.IsNotNull(FindVisual<StackPanel>(window, "PanCustom"));
                Assert.IsNotNull(FindVisual<MyCard>(window, "PanLog"));
                Assert.IsNotNull(window.CaptureRenderedFrame());
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void MainWindow_ShowAnimationSettlesToVisibleWindow()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                ModAnimation.AdvanceUntilIdleForTesting();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual(1d, window.Opacity, 0.01d);
                Assert.IsNull(((Control)window.Content!).RenderTransform);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyImage_LoadsWpfPackResourceAndAppliesCornerRadius()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyImage image = new()
            {
                Width = 32d,
                Height = 32d,
                CornerRadius = new CornerRadius(6d),
                Source = "pack://application:,,,/images/Blocks/Grass.png"
            };
            Window window = new()
            {
                Width = 80d,
                Height = 80d,
                Content = image
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                WaitForCondition(() => ((Image)image).Source is not null);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("pack://application:,,,/images/Blocks/Grass.png", image.Source);
                Assert.AreEqual("pack://application:,,,/images/Blocks/Grass.png", image.ActualSource);
                Assert.IsInstanceOfType<RectangleGeometry>(image.Clip);
                RectangleGeometry clip = (RectangleGeometry)image.Clip!;
                Assert.AreEqual(6d, clip.RadiusX);
                Assert.AreEqual(6d, clip.RadiusY);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyImage_ReceivesStringSourceFromAxaml()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageSetupUpdate page = new();
            MyImage image = page.FindControl<MyImage>("ImgUpdateIcon")!;

            Assert.AreEqual("https://www.pclc.cc/img/pcl-ce/icon.webp", image.Source);
            Assert.IsNull(((Image)image).Source);
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MainWindow_InstanceSubPageUsesWpfTitleBackButton()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                ModAnimation.AdvanceUntilIdleForTesting();

                PageLaunchLeft launchPage = FindVisual<PageLaunchLeft>(window)!;
                LaunchInstanceInfo instance = new("1.20.1", @"D:\Minecraft\versions\1.20.1\1.20.1.json", @"D:\Minecraft\versions\1.20.1");
                launchPage.SetInstances([instance], instance);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Click(window, launchPage.FindControl<MyButton>("BtnInstance")!);
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.IsTrue(window.FindControl<Control>("PanTitleInner")!.IsVisible);
                Assert.AreEqual("选择版本", window.FindControl<TextBlock>("LabTitleInner")!.Text);
                Assert.IsNotNull(FindVisual<PageInstanceSelectRight>(window));

                Click(window, window.FindControl<MyIconButton>("BtnTitleInner")!);
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.IsFalse(window.FindControl<Control>("PanTitleInner")!.IsVisible);
                Assert.IsNotNull(FindVisual<PageLaunchLeft>(window));
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MainWindow_InstanceSelectSupportsWpfEscapeAndHiddenVersionKeys()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();
            try
            {
                MyIconButton closeButton = window.FindControl<MyIconButton>("BtnTitleClose")!;
                Assert.AreEqual(MyIconButton.Themes.White, closeButton.Theme);
                Assert.AreEqual(
                    Color.FromRgb(234, 242, 254),
                    ((SolidColorBrush)closeButton.FindControl<SvgIcon>("ShapeSvgIcon")!.IconBrush!).Color);
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                ModAnimation.AdvanceUntilIdleForTesting();

                PageLaunchLeft launchPage = FindVisual<PageLaunchLeft>(window)!;
                launchPage.SetInstances(
                [
                    new LaunchInstanceInfo(
                        "1.20.1",
                        @"D:\Minecraft\versions\1.20.1\1.20.1.json",
                        @"D:\Minecraft\versions\1.20.1")
                ]);
                Click(window, launchPage.FindControl<MyButton>("BtnInstance")!);
                ModAnimation.AdvanceUntilIdleForTesting();

                PageInstanceSelectRight selectPage = FindVisual<PageInstanceSelectRight>(window)!;
                Assert.IsFalse(selectPage.ShowHidden);

                window.KeyPress(Key.F11, RawInputModifiers.None, PhysicalKey.F11, string.Empty);
                Assert.IsTrue(selectPage.ShowHidden);
                Assert.IsTrue(window.FindControl<Control>("PanTitleInner")!.IsVisible);

                window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, string.Empty);
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.IsFalse(window.FindControl<Control>("PanTitleInner")!.IsVisible);
                Assert.IsNotNull(FindVisual<PageLaunchLeft>(window));
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MainWindow_BackToTopUsesAutoResolvedPageScroll()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                ModAnimation.AdvanceUntilIdleForTesting();

                StackPanel content = new();
                for (int i = 0; i < 60; i++)
                {
                    content.Children.Add(new MyCard
                    {
                        Title = "滚动项 " + i,
                        Height = 40d,
                        Margin = new Thickness(0d, 0d, 0d, 5d)
                    });
                }

                MyScrollViewer panBack = new()
                {
                    Name = "PanBack",
                    Width = 520d,
                    Height = 240d,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = content
                };
                MyPageRight copiedPage = new()
                {
                    Content = panBack
                };
                window.FindControl<Border>("PanMainRight")!.Child = copiedPage;
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                panBack.PerformVerticalOffsetDelta(1800d);
                ModAnimation.AdvanceUntilIdleForTesting();
                InvokePrivateNoArgs(window, "RefreshBackToTopBinding");

                MyExtraButton back = window.FindControl<MyExtraButton>("BtnExtraBack")!;
                Assert.IsTrue(back.Show);

                Click(window, back);
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.AreEqual(0d, panBack.Offset.Y, 0.01d);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void AvaloniaThemeManager_DarkSettingsUpdatesPclThemeResources()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            try
            {
                AvaloniaThemeManager.Apply(new LauncherSettings
                {
                    ColorMode = ColorMode.Dark,
                    DarkColor = ColorTheme.CatBlue
                });

                Assert.AreEqual(ThemeVariant.Dark, Avalonia.Application.Current!.RequestedThemeVariant);
                Color background = RequiredBrush("ColorBrushBackground").Color;
                Color foreground = RequiredBrush("ColorBrush1").Color;
                Color cardBackground = RequiredBrush("ColorBrushTransparentBackground").Color;
                IReadOnlyDictionary<string, Color> palette = ThemeColorPalette.Create(isDarkMode: true, ColorTheme.CatBlue);

                Assert.AreEqual(palette["ColorBrushBackground"], background);
                Assert.AreEqual(palette["ColorBrush1"], foreground);
                Assert.AreEqual(palette["ColorBrushTransparentBackground"], cardBackground);
                Assert.IsTrue(
                    background.R < 120 && background.G < 120 && background.B < 120,
                    $"Dark background should stay dark, actual: {background}.");
                Assert.IsTrue(
                    foreground.R > 180 && foreground.G > 180 && foreground.B > 180,
                    $"Dark foreground should stay readable, actual: {foreground}.");
                Assert.AreEqual((byte)Math.Round(0.824d * 255d), cardBackground.A);

                using MainWindow window = new();
                typeof(MainWindow).GetMethod(
                        "ApplyRuntimeSettings",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(window, [new LauncherSettings
                    {
                        ColorMode = ColorMode.Dark,
                        DarkColor = ColorTheme.CatBlue,
                        BooleanOptions = new Dictionary<string, bool> { ["UiBackgroundColorful"] = true }
                    }]);
                LinearGradientBrush formBackground = (LinearGradientBrush)window.FindControl<Grid>("PanForm")!.Background!;
                Assert.IsTrue(formBackground.GradientStops.All(stop =>
                    stop.Color.R < 120 && stop.Color.G < 120 && stop.Color.B < 120));
            }
            finally
            {
                AvaloniaThemeManager.Apply(new LauncherSettings
                {
                    ColorMode = ColorMode.Light,
                    LightColor = ColorTheme.CatBlue
                });
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void AvaloniaThemeManager_LightSettingsUpdatesPclThemeResources()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            AvaloniaThemeManager.Apply(new LauncherSettings
            {
                ColorMode = ColorMode.Light,
                LightColor = ColorTheme.CatBlue
            });

            Assert.AreEqual(ThemeVariant.Light, Avalonia.Application.Current!.RequestedThemeVariant);
            IReadOnlyDictionary<string, Color> palette = ThemeColorPalette.Create(isDarkMode: false, ColorTheme.CatBlue);
            Assert.AreEqual(palette["ColorBrushBackground"], RequiredBrush("ColorBrushBackground").Color);
            Assert.AreEqual(palette["ColorBrushTransparentBackground"], RequiredBrush("ColorBrushTransparentBackground").Color);
            Assert.AreEqual(palette["ColorBrushHalfWhite"], RequiredBrush("ColorBrushHalfWhite").Color);
            Assert.AreEqual(palette["ColorBrush7"], RequiredBrush("ColorBrush7").Color);
        }, CancellationToken.None);
    }

    [TestMethod]
    public void LegacyControls_HandleHeadlessPointerInput()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

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
    public void MyButton_UsesWpfBackgroundColorsDuringHoverAnimation()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyButton button = new()
            {
                Text = "下载游戏",
                Width = 130,
                Height = 36
            };
            Window window = new()
            {
                Width = 240,
                Height = 120,
                Content = new Border
                {
                    Margin = new Thickness(20),
                    Child = button
                }
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Border fore = button.FindControl<Border>("PanFore")!;
                Assert.AreEqual(
                    ThemeColorPalette.Create(isDarkMode: false, ColorTheme.CatBlue)["ColorBrushHalfWhite"],
                    ((SolidColorBrush)fore.Background!).Color);

                MoveTo(window, button);
                Assert.IsTrue(ModAnimation.AniIsRun("MyButton Color " + button.Uuid));
                Assert.IsTrue(ModAnimation.AniIsRun("MyButton TextColor " + button.Uuid));
                Assert.IsTrue(ModAnimation.AniIsRun("MyButton Background " + button.Uuid));
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.AreEqual(
                    ThemeColorPalette.Create(isDarkMode: false, ColorTheme.CatBlue)["ColorBrush7"],
                    ((SolidColorBrush)fore.Background!).Color);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyButton_UsesWpfTextPaddingInlinesAndCenteredPressScale()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyButton button = new()
            {
                Text = "开始登录",
                Width = 130,
                Height = 36,
                TextPadding = new Thickness(7d, 1d, 8d, 2d)
            };
            Window window = new()
            {
                Width = 240,
                Height = 120,
                Content = new Border
                {
                    Margin = new Thickness(20),
                    Child = button
                }
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Border fore = button.FindControl<Border>("PanFore")!;
                TextBlock label = button.FindControl<TextBlock>("LabText")!;
                Assert.AreEqual(new Thickness(7d, 1d, 8d, 2d), label.Padding);
                Assert.IsNotNull(button.Inlines);

                Point center = button.TranslatePoint(
                    new Point(button.Bounds.Width / 2d, button.Bounds.Height / 2d),
                    window) ?? throw new InvalidOperationException("Button is not attached.");

                window.MouseDown(center, MouseButton.Left);
                Assert.IsTrue(ModAnimation.AniIsRun("MyButton Scale " + button.Uuid));
                ModAnimation.AdvanceForTesting(16, 8);

                ScaleTransform scale = (ScaleTransform)fore.RenderTransform!;
                Assert.AreEqual(new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative), fore.RenderTransformOrigin);
                Assert.IsTrue(scale.ScaleX < 1d);
                Assert.AreEqual(scale.ScaleX, scale.ScaleY, 0.0001d);

                window.MouseUp(center, MouseButton.Left);
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.AreEqual(1d, scale.ScaleX, 0.01d);
                Assert.AreEqual(1d, scale.ScaleY, 0.01d);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyButton_ExposesWpfRealRenderTransformSetterAndReleaseEvent()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            ScaleTransform injectedTransform = new()
            {
                ScaleX = 0.9d,
                ScaleY = 0.9d
            };
            MyButton button = new()
            {
                Text = "确定",
                Width = 120,
                Height = 36,
                RealRenderTransform = injectedTransform
            };
            Window window = new()
            {
                Width = 220,
                Height = 120,
                Content = new Border
                {
                    Margin = new Thickness(20),
                    Child = button
                }
            };

            object? clickSender = null;
            EventArgs? clickArgs = null;
            PointerReleasedEventArgs? releasedArgs = null;
            button.Click += (sender, args) =>
            {
                clickSender = sender;
                clickArgs = args;
            };
            button.ClickReleased += (_, args) => releasedArgs = args;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Border fore = button.FindControl<Border>("PanFore")!;
                Assert.AreSame(injectedTransform, fore.RenderTransform);
                Assert.AreSame(injectedTransform, button.RealRenderTransform);

                Click(window, button);

                Assert.AreSame(button, clickSender);
                Assert.AreSame(EventArgs.Empty, clickArgs);
                Assert.IsNotNull(releasedArgs);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyButton_UsesDarkThemePaletteDuringHoverAnimation()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            try
            {
                AvaloniaThemeManager.Apply(new LauncherSettings
                {
                    ColorMode = ColorMode.Dark,
                    DarkColor = ColorTheme.CatBlue
                });
                IReadOnlyDictionary<string, Color> palette = ThemeColorPalette.Create(isDarkMode: true, ColorTheme.CatBlue);

                MyButton button = new()
                {
                    Text = "下载游戏",
                    Width = 130,
                    Height = 36
                };
                Window window = new()
                {
                    Width = 240,
                    Height = 120,
                    Content = new Border
                    {
                        Margin = new Thickness(20),
                        Child = button
                    }
                };

                try
                {
                    window.Show();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Border fore = button.FindControl<Border>("PanFore")!;
                    Assert.AreEqual(palette["ColorBrushHalfWhite"], ((SolidColorBrush)fore.Background!).Color);

                    MoveTo(window, button);
                    ModAnimation.AdvanceUntilIdleForTesting();

                    Assert.AreEqual(palette["ColorBrush7"], ((SolidColorBrush)fore.Background!).Color);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                AvaloniaThemeManager.Apply(new LauncherSettings
                {
                    ColorMode = ColorMode.Light,
                    LightColor = ColorTheme.CatBlue
                });
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void ModAnimation_AaScaleUsesWpfSymmetricMarginDelta()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            Border border = new()
            {
                Width = 20d,
                Height = 20d,
                Margin = new Thickness(10d, 20d, 30d, 40d)
            };

            ModAnimation.AniStart(
                ModAnimation.AaScale(border, 10d, 100, ease: new ModAnimation.AniEaseLinear(), absolute: true),
                "ModAnimation Scale Symmetric");
            ModAnimation.AdvanceUntilIdleForTesting();

            Assert.AreEqual(new Thickness(5d, 15d, 25d, 35d), border.Margin);
            Assert.AreEqual(30d, border.Width);
            Assert.AreEqual(30d, border.Height);
        }, CancellationToken.None);
    }

    [TestMethod]
    public void ModAnimation_AaTranslateRespectsWpfAlignmentMargins()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            Border rightAligned = new()
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(1d, 2d, 30d, 4d)
            };
            Border bottomAligned = new()
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(1d, 2d, 3d, 40d)
            };

            ModAnimation.AniStart(
                new List<ModAnimation.AniData>
                {
                    ModAnimation.AaX(rightAligned, 5d, 100, ease: new ModAnimation.AniEaseLinear()),
                    ModAnimation.AaY(bottomAligned, 7d, 100, ease: new ModAnimation.AniEaseLinear())
                },
                "ModAnimation Alignment Margins");
            ModAnimation.AdvanceUntilIdleForTesting();

            Assert.AreEqual(new Thickness(1d, 2d, 25d, 4d), rightAligned.Margin);
            Assert.AreEqual(new Thickness(1d, 2d, 3d, 33d), bottomAligned.Margin);
        }, CancellationToken.None);
    }

    [TestMethod]
    public void ModAnimation_AaScaleTransformClampsLikeWpf()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            Border border = new()
            {
                RenderTransformOrigin = new RelativePoint(0d, 0d, RelativeUnit.Relative),
                RenderTransform = new ScaleTransform(0.2d, 0.2d)
            };

            ModAnimation.AniStart(
                ModAnimation.AaScaleTransform(border, -1d, 100, ease: new ModAnimation.AniEaseLinear()),
                "ModAnimation Scale Clamp");
            ModAnimation.AdvanceUntilIdleForTesting();

            ScaleTransform scale = (ScaleTransform)border.RenderTransform!;
            Assert.AreEqual(new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative), border.RenderTransformOrigin);
            Assert.AreEqual(0d, scale.ScaleX);
            Assert.AreEqual(0d, scale.ScaleY);
        }, CancellationToken.None);
    }

    [TestMethod]
    public void ModAnimation_AaColorInterpolatesFromStableStartColor()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            Border border = new()
            {
                Background = new SolidColorBrush(Color.FromArgb(0x55, 0xff, 0xff, 0xff))
            };
            Color target = Color.FromArgb(0xff, 0x13, 0x70, 0xf3);

            ModAnimation.AniStart(
                ModAnimation.AaColor(
                    border,
                    Border.BackgroundProperty,
                    target,
                    100,
                    ease: new ModAnimation.AniEaseLinear()),
                "ModAnimation Color Stable");
            ModAnimation.AdvanceForTesting(50);

            Color mid = ((SolidColorBrush)border.Background!).Color;
            Assert.AreEqual(170, mid.A);
            Assert.AreEqual(137, mid.R);
            Assert.AreEqual(184, mid.G);
            Assert.AreEqual(249, mid.B);

            ModAnimation.AdvanceUntilIdleForTesting();

            Assert.AreEqual(target, ((SolidColorBrush)border.Background!).Color);
        }, CancellationToken.None);
    }

    [TestMethod]
    public void ModAnimation_ExtendedWpfApisApplyExpectedDeltas()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            Border border = new()
            {
                BorderThickness = new Thickness(1d, 2d, 3d, 4d),
                Width = 10d
            };
            Rectangle rectangle = new()
            {
                StrokeThickness = 1d
            };
            ColumnDefinition column = new(new GridLength(2d, GridUnitType.Star));
            TextBlock textBlock = new()
            {
                Text = "PCL"
            };
            MySlider slider = new()
            {
                Value = 2
            };
            MyDropShadow shadow = new()
            {
                ShadowRadius = 4d
            };
            StackPanel stack = new()
            {
                Children =
                {
                    new Border(),
                    new Border()
                }
            };

            List<ModAnimation.AniData> animations =
            [
                ModAnimation.AaBorderThickness(border, 2d, 100, ease: new ModAnimation.AniEaseLinear()),
                ModAnimation.AaStrokeThickness(rectangle, 3d, 100, ease: new ModAnimation.AniEaseLinear()),
                ModAnimation.AaGridLengthWidth(column, 1d, 100, ease: new ModAnimation.AniEaseLinear()),
                ModAnimation.AaDouble(border, Border.WidthProperty, 5d, 100, ease: new ModAnimation.AniEaseLinear()),
                ModAnimation.AaValue(slider, 4d, 100, ease: new ModAnimation.AniEaseLinear()),
                ModAnimation.AaRadius(shadow, -2d, 100, ease: new ModAnimation.AniEaseLinear()),
                ModAnimation.AaTextAppear(textBlock, timePerText: false, time: 100, ease: new ModAnimation.AniEaseLinear())
            ];
            animations.AddRange(ModAnimation.AaStack(stack, time: 100, delay: 0));

            Assert.IsTrue(stack.Children.OfType<Control>().All(child => child.Opacity == 0d));

            ModAnimation.AniStart(animations, "ModAnimation Extended WPF APIs");
            ModAnimation.AdvanceUntilIdleForTesting();

            Assert.AreEqual(new Thickness(6d), border.BorderThickness);
            Assert.AreEqual(4d, rectangle.StrokeThickness);
            Assert.AreEqual(new GridLength(3d, GridUnitType.Star), column.Width);
            Assert.AreEqual(15d, border.Width);
            Assert.AreEqual(6, slider.Value);
            Assert.AreEqual(2d, shadow.ShadowRadius);
            Assert.AreEqual("PCL", textBlock.Text);
            Assert.IsTrue(stack.Children.OfType<Control>().All(child => Math.Abs(child.Opacity - 1d) < 0.001d));
        }, CancellationToken.None);
    }

    [TestMethod]
    public void ModAnimation_CarAndInitialFluentEasesMatchWpfContracts()
    {
        ModAnimation.AniEase initial = new ModAnimation.AniEaseOutFluentWithInitial(
            initialPixelPerSecond: 400d,
            totalSecond: 1d,
            totalDistance: 100d);
        ModAnimation.AniEase carIn = new ModAnimation.AniEaseInCar();
        ModAnimation.AniEase carOut = new ModAnimation.AniEaseOutCar();

        Assert.AreEqual(0d, initial.GetValue(0d));
        Assert.AreEqual(1d, initial.GetValue(1d));
        Assert.AreEqual(0.7272727272727273d, initial.GetValue(0.4d), 0.0000001d);
        Assert.AreEqual(0d, carIn.GetValue(0d));
        Assert.AreEqual(1d, carIn.GetValue(1d));
        Assert.AreEqual(0d, carOut.GetValue(0d));
        Assert.AreEqual(1d, carOut.GetValue(1d));
        Assert.IsTrue(carOut.GetValue(0.85d) > 1d);
    }

    [TestMethod]
    public void MyListItem_ExposesWpfCheckEventAndInlineButtons()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyIconButton inlineButton = new()
            {
                SvgIcon = "lucide/refresh-cw",
                ToolTip = "刷新"
            };
            MyListItem item = new()
            {
                Title = "设置项",
                Type = MyListItem.CheckType.RadioBox,
                Width = 180,
                Height = 36,
                MinPaddingRight = 35,
                Buttons = [inlineButton]
            };
            Window window = new()
            {
                Width = 260,
                Height = 120,
                Content = new Border
                {
                    Margin = new Thickness(20),
                    Child = item
                }
            };
            bool checkedRaised = false;
            bool changedRaised = false;
            bool contentHandlerRaised = false;
            item.tag = "legacy-tag";
            item.ContentHandler = (sender, _) =>
            {
                Assert.AreSame(item, sender);
                contentHandlerRaised = true;
            };
            item.Check += (_, e) =>
            {
                checkedRaised = true;
                Assert.IsTrue(e.RaiseByMouse);
            };
            item.Changed += (_, e) =>
            {
                changedRaised = true;
                Assert.IsTrue(e.RaiseByMouse);
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsInstanceOfType<IMyRadio>(item);
                Assert.AreEqual("legacy-tag", item.Tag);
                Assert.IsNotNull(item.Inlines);
                Assert.AreEqual(0d, inlineButton.Opacity);
                MoveTo(window, item);
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.IsTrue(inlineButton.Opacity > 0d);
                Assert.IsTrue(contentHandlerRaised);

                Click(window, item);
                Assert.IsTrue(item.Checked);
                Assert.IsTrue(checkedRaised);
                Assert.IsTrue(changedRaised);
                Assert.AreEqual("刷新", Avalonia.Controls.ToolTip.GetTip(inlineButton));
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyListItem_TagsMatchWpfLazyTagSurface()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyListItem item = new()
            {
                Title = "1.20.1",
                Info = "Fabric",
                Width = 240,
                Height = 42,
                Tags = "推荐|已收藏"
            };
            Window window = new()
            {
                Width = 320,
                Height = 120,
                Content = new Border
                {
                    Margin = new Thickness(20),
                    Child = item
                }
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                TextBlock[] visibleTagTexts = item.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Where(static block => block.Text is "推荐" or "已收藏")
                    .ToArray();
                Assert.AreEqual(2, visibleTagTexts.Length);

                item.Tags = new List<string> { "整合包", "本地" };
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                visibleTagTexts = item.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Where(static block => block.Text is "整合包" or "本地")
                    .ToArray();
                Assert.AreEqual(2, visibleTagTexts.Length);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyLocalModItem_PortsWpfLocalResourceChrome()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyIconButton openButton = new()
            {
                SvgIcon = "lucide/folder-open",
                ToolTip = "打开"
            };
            MyLocalModItem item = new()
            {
                Title = "disabled.jar",
                SubTitle = "  |  1.0.0",
                Description = "禁用 · 8 KB · 修改于今天",
                Logo = "avares://PCL.Desktop/Assets/Legacy/Blocks/CommandBlock.png",
                State = ResourceItemState.Disabled,
                Tags = ["本地"],
                Buttons = [openButton],
                Width = 360
            };
            Window window = new()
            {
                Width = 440,
                Height = 140,
                Content = new Border
                {
                    Margin = new Thickness(20),
                    Child = item
                }
            };
            bool clicked = false;
            item.Click += (_, _) => clicked = true;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual(44d, item.Height);
                Assert.IsNotNull(item.FindControl<MyImage>("PathLogo")!.Source);
                Assert.IsNotNull(item.GetVisualDescendants()
                    .OfType<Image>()
                    .SingleOrDefault(image => Math.Abs(image.Width - 20d) < 0.01d && Grid.GetColumn(image) == 1));
                Assert.IsNotNull(item.FindControl<TextBlock>("LabTitle")!.TextDecorations);
                Assert.IsTrue(item.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == "本地"));

                StackPanel buttonStack = item.Children.OfType<StackPanel>()
                    .Single(panel => panel.Children.Contains(openButton));
                Assert.AreEqual(0d, buttonStack.Opacity);
                MoveTo(window, item);
                Assert.IsTrue(ModAnimation.AniIsRun("LocalModItem Color " + item.Uuid));
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.IsTrue(buttonStack.Opacity > 0d);

                item.SetChecked(true);
                Assert.IsTrue(ModAnimation.AniIsRun("MyLocalCompItem Checked " + item.Uuid));
                ModAnimation.AdvanceUntilIdleForTesting();
                Border check = item.Children.OfType<Border>()
                    .Single(border => Math.Abs(border.Width - 5d) < 0.01d);
                Assert.AreEqual(32d, check.Height);

                Click(window, item);
                Assert.IsTrue(clicked);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyScrollViewer_ExposesWpfDeltaMultProperty()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyScrollViewer viewer = new()
            {
                DeltaMult = 0.5d,
                Width = 120,
                Height = 100,
                Content = new Border
                {
                    Width = 100,
                    Height = 600
                }
            };
            Window window = new()
            {
                Width = 180,
                Height = 160,
                Content = viewer
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                viewer.PerformVerticalOffsetDelta(120d);
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.AreEqual(0.5d, viewer.DeltaMult);
                Assert.AreEqual(60d, viewer.Offset.Y, 0.01d);

                viewer.ScrollToHome();
                Assert.AreEqual(0d, viewer.Offset.Y);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyMsgLogin_UsesWpfDialogAnimationAndButtons()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            Grid host = new();
            MyMsgLogin dialog = new()
            {
                Title = "Microsoft 正版档案登录",
                Caption = "请在网页中输入授权码 ABCD-EFGH",
                UserCode = "ABCD-EFGH",
                Website = "https://www.microsoft.com/link"
            };
            host.Children.Add(dialog);
            Window window = new()
            {
                Width = 640,
                Height = 420,
                Content = host
            };
            int reopenCount = 0;
            int copyCount = 0;
            int cancelCount = 0;
            dialog.ReopenWebpageRequested += (_, _) => reopenCount++;
            dialog.CopyCodeRequested += (_, _) => copyCount++;
            dialog.CancelRequested += (_, _) => cancelCount++;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("Microsoft 正版档案登录", dialog.FindControl<TextBlock>("LabTitle")!.Text);
                Assert.AreEqual("请在网页中输入授权码 ABCD-EFGH", dialog.FindControl<TextBlock>("LabCaption")!.Text);
                Assert.IsInstanceOfType<TransformGroup>(dialog.RenderTransform);
                Assert.AreEqual(
                    BoxShadows.Parse("0 4 20 0 #cc3c3c3c"),
                    dialog.FindControl<BlurBorder>("PanBorder")!.BoxShadow);
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual(1d, dialog.Opacity, 0.0001d);

                Click(window, dialog.FindControl<MyButton>("Btn1")!);
                Click(window, dialog.FindControl<MyButton>("Btn2")!);
                Click(window, dialog.FindControl<MyButton>("Btn3")!);
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.AreEqual(1, reopenCount);
                Assert.AreEqual(1, copyCount);
                Assert.AreEqual(1, cancelCount);
                Assert.IsFalse(host.Children.Contains(dialog));
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyListItem_ProgrammaticSelectionDoesNotRaiseUserCheck()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            StackPanel panel = new()
            {
                Children =
                {
                    new MyListItem
                    {
                        Name = "First",
                        Title = "第一页",
                        Type = MyListItem.CheckType.RadioBox,
                        Width = 180,
                        Height = 36
                    },
                    new MyListItem
                    {
                        Name = "Second",
                        Title = "第二页",
                        Type = MyListItem.CheckType.RadioBox,
                        Width = 180,
                        Height = 36
                    }
                }
            };
            Window window = new()
            {
                Width = 260,
                Height = 140,
                Content = new Border
                {
                    Margin = new Thickness(20),
                    Child = panel
                }
            };

            MyListItem first = (MyListItem)panel.Children[0];
            MyListItem second = (MyListItem)panel.Children[1];
            int checkCount = 0;
            first.Check += (_, _) => checkCount++;
            second.Check += (_, _) => checkCount++;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                first.SetChecked(true, user: false, animate: false);
                second.SetChecked(true, user: false, animate: false);
                first.SetChecked(false, user: false, animate: false);

                Assert.AreEqual(0, checkCount);
                Assert.IsFalse(first.Checked);
                Assert.IsTrue(second.Checked);

                Click(window, first);
                Assert.AreEqual(1, checkCount);
                Assert.IsTrue(first.Checked);
                Assert.IsFalse(second.Checked);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MainWindow_SettingsNavDoesNotExposePluginPageWithoutHostModule()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Click(window, window.FindControl<MyListItem>("BtnTitleSelect3")!);
                ModAnimation.AdvanceUntilIdleForTesting();
                PageSetupLeft setupLeft = FindVisual<PageSetupLeft>(window)!;

                Assert.IsNotNull(setupLeft);
                Assert.IsTrue(setupLeft.FindControl<MyListItem>("ItemLaunch")!.Checked);
                Assert.IsNull(setupLeft.FindControl<MyListItem>("ItemPlugin"));
                Assert.IsNull(setupLeft.FindControl<MyListItem>("ItemHostSettings_pcl_plugin_installed"));
                Assert.IsNull(setupLeft.FindControl<TextBlock>("TextHostSettingsGroup_pcl_plugin"));
                Assert.AreEqual(
                    SetupPageSubType.Java,
                    setupLeft.FindControl<MyListItem>("ItemJava")!.Buttons.Single().Tag);
                Assert.IsInstanceOfType<PageSetupLaunch>(FindVisual<MyPageRight>(window));

            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MainWindow_SettingsNavCanOpenPersonalizationPage()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Click(window, window.FindControl<MyListItem>("BtnTitleSelect3")!);
                ModAnimation.AdvanceUntilIdleForTesting();

                PageSetupLeft setupLeft = FindVisual<PageSetupLeft>(window)!;
                Click(window, setupLeft.FindControl<MyListItem>("ItemUI")!);
                ModAnimation.AdvanceUntilIdleForTesting();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                PageSetupUI uiPage = FindVisual<PageSetupUI>(window)!;
                Assert.IsNotNull(uiPage);
                Assert.IsTrue(setupLeft.FindControl<MyListItem>("ItemUI")!.Checked);
                Assert.IsNotNull(uiPage.FindControl<MyCard>("CardLauncher"));
                Assert.IsNotNull(uiPage.FindControl<MyComboBox>("ComboDarkMode"));
                Assert.IsNotNull(uiPage.FindControl<MyCard>("CardCustom"));
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MainWindow_SavesVisualDiagnosticsWhenRequested()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("PCLN_SAVE_UI_SNAPSHOT"), "1", StringComparison.Ordinal))
            return;

        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                ModAnimation.AdvanceUntilIdleForTesting();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                SaveUiSnapshot(window, "main-home");

                Click(window, window.FindControl<MyListItem>("BtnTitleSelect3")!);
                ModAnimation.AdvanceUntilIdleForTesting();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                SaveUiSnapshot(window, "main-settings");
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyCheckBox_UsesWpfThreeStatePreviewAndScaleAnimations()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyCheckBox checkBox = new()
            {
                Text = "三态复选框",
                IsThreeState = true,
                Width = 160,
                Height = 28
            };
            Window window = new()
            {
                Width = 260,
                Height = 120,
                Content = new Border
                {
                    Margin = new Thickness(20),
                    Child = checkBox
                }
            };
            bool blockNextTrue = true;
            int changeCount = 0;
            bool? lastChecked = false;
            bool? lastUser = null;
            checkBox.PreviewChange += (_, e) =>
            {
                if (blockNextTrue)
                    e.handled = true;
            };
            checkBox.Change += (_, user) =>
            {
                changeCount++;
                lastChecked = checkBox.Checked;
                lastUser = user;
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Avalonia.Controls.Shapes.Path check = FindVisual<Avalonia.Controls.Shapes.Path>(checkBox, "ShapeCheck")!;
                Border indeterminate = FindVisual<Border>(checkBox, "ShapeIndeterminate")!;

                Assert.IsNotNull(checkBox.Inlines);
                Click(window, checkBox);
                Assert.AreEqual(false, checkBox.Checked);
                Assert.AreEqual(0, changeCount);

                blockNextTrue = false;
                Click(window, checkBox);
                Assert.AreEqual(true, checkBox.Checked);
                Assert.AreEqual(1, changeCount);
                Assert.AreEqual(true, lastChecked);
                Assert.AreEqual(true, lastUser);
                Assert.IsTrue(ModAnimation.AniIsRun("MyCheckBox Background Scale " + checkBox.Uuid));
                Assert.IsTrue(ModAnimation.AniIsRun("MyCheckBox Check Scale Show" + checkBox.Uuid));
                Assert.IsTrue(ModAnimation.AniIsRun("MyCheckBox BorderColor " + checkBox.Uuid));
                ModAnimation.AdvanceForTesting(16, 32);
                Assert.AreEqual(1d, ((ScaleTransform)check.RenderTransform!).ScaleX, 0.01d);

                Click(window, checkBox);
                Assert.IsNull(checkBox.Checked);
                Assert.IsTrue(ModAnimation.AniIsRun("MyCheckBox Check Scale Hide" + checkBox.Uuid));
                Assert.IsTrue(ModAnimation.AniIsRun("MyCheckBox Indeterminate Scale Show" + checkBox.Uuid));
                ModAnimation.AdvanceForTesting(16, 32);
                Assert.AreEqual(0d, ((ScaleTransform)check.RenderTransform!).ScaleX, 0.01d);
                Assert.AreEqual(1d, ((ScaleTransform)indeterminate.RenderTransform!).ScaleX, 0.01d);

                Click(window, checkBox);
                Assert.AreEqual(false, checkBox.Checked);
                Assert.IsTrue(ModAnimation.AniIsRun("MyCheckBox Indeterminate Scale Hide" + checkBox.Uuid));
                ModAnimation.AdvanceForTesting(16, 32);
                Assert.AreEqual(0d, ((ScaleTransform)indeterminate.RenderTransform!).ScaleX, 0.01d);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyIconButton_AnimatesPathAndSvgIconLikeWpfControl()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyIconButton pathButton = new()
            {
                Width = 30,
                Height = 30,
                Logo = "M0,0 L10,5 L0,10Z"
            };
            MyIconButton svgButton = new()
            {
                Width = 30,
                Height = 30,
                SvgIcon = "lucide/play"
            };
            StackPanel panel = new()
            {
                Margin = new Thickness(20),
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Children = { pathButton, svgButton }
            };
            Window window = new()
            {
                Width = 160,
                Height = 90,
                Content = panel
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Avalonia.Controls.Shapes.Path path = pathButton.FindControl<Avalonia.Controls.Shapes.Path>("Path")!;
                SvgIcon svg = svgButton.FindControl<SvgIcon>("ShapeSvgIcon")!;
                Assert.IsTrue(path.IsVisible);
                Assert.IsTrue(svg.IsVisible);

                MoveTo(window, pathButton);
                Assert.IsTrue(ModAnimation.AniIsRun("MyIconButton Color " + pathButton.Uuid));
                ModAnimation.AdvanceForTesting(16, 16);
                Assert.AreEqual(
                    Color.Parse("#0b5bcb"),
                    ((SolidColorBrush)path.Fill!).Color);

                MoveTo(window, svgButton);
                ModAnimation.AdvanceForTesting(16, 16);
                Assert.AreEqual(
                    Color.Parse("#4890f5"),
                    ((SolidColorBrush)path.Fill!).Color);
                Assert.AreEqual(
                    Color.Parse("#0b5bcb"),
                    ((SolidColorBrush)svg.IconBrush!).Color);

                bool clicked = false;
                svgButton.Click += (_, _) => clicked = true;
                Click(window, svgButton);
                Assert.IsTrue(ModAnimation.AniIsRun("MyIconButton Scale " + svgButton.Uuid));
                ModAnimation.AdvanceForTesting(16, 32);
                Border back = svgButton.FindControl<Border>("PanBack")!;
                Assert.IsTrue(clicked);
                Assert.AreEqual(1d, ((ScaleTransform)back.RenderTransform!).ScaleX, 0.01d);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyIconButton_BlackThemeFollowsWpfDarkModeColors()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            try
            {
                AvaloniaThemeManager.Apply(new LauncherSettings
                {
                    ColorMode = ColorMode.Dark,
                    DarkColor = ColorTheme.CatBlue
                });

                MyIconButton button = new()
                {
                    Width = 30d,
                    Height = 30d,
                    Logo = "M0,0 L10,5 L0,10Z",
                    Theme = MyIconButton.Themes.Black
                };
                Window window = new()
                {
                    Width = 120d,
                    Height = 80d,
                    Content = new Border
                    {
                        Margin = new Thickness(20d),
                        Child = button
                    }
                };

                try
                {
                    window.Show();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Avalonia.Controls.Shapes.Path path = button.FindControl<Avalonia.Controls.Shapes.Path>("Path")!;
                    Assert.AreEqual(Color.FromArgb(160, 255, 255, 255), ((SolidColorBrush)path.Fill!).Color);

                    MoveTo(window, button);
                    ModAnimation.AdvanceUntilIdleForTesting();

                    Assert.AreEqual(Color.FromArgb(230, 255, 255, 255), ((SolidColorBrush)path.Fill!).Color);
                }
                finally
                {
                    window.Close();
                }

                AvaloniaThemeManager.Apply(new LauncherSettings
                {
                    ColorMode = ColorMode.Light,
                    LightColor = ColorTheme.CatBlue
                });

                MyIconButton lightButton = new()
                {
                    Width = 30d,
                    Height = 30d,
                    Logo = "M0,0 L10,5 L0,10Z",
                    Theme = MyIconButton.Themes.Black
                };
                Assert.AreEqual(
                    Color.FromArgb(160, 0, 0, 0),
                    ((SolidColorBrush)lightButton.FindControl<Avalonia.Controls.Shapes.Path>("Path")!.Fill!).Color);
            }
            finally
            {
                AvaloniaThemeManager.Apply(new LauncherSettings
                {
                    ColorMode = ColorMode.Light,
                    LightColor = ColorTheme.CatBlue
                });
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyExtraTextButton_ShowFalseHidesControlLikeWpf()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyExtraTextButton button = new()
            {
                Text = "开始下载",
                Show = false
            };
            Window window = new()
            {
                Width = 220d,
                Height = 120d,
                Content = button
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsFalse(button.IsVisible);
                Assert.IsFalse(button.IsHitTestVisible);

                button.Show = true;
                ModAnimation.AdvanceUntilIdleForTesting();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.IsTrue(button.IsVisible);
                Assert.IsTrue(button.IsHitTestVisible);

                button.Show = false;
                ModAnimation.AdvanceUntilIdleForTesting();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.IsFalse(button.IsVisible);
                Assert.IsFalse(button.IsHitTestVisible);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyMenuItem_UsesWpfResourceStatesAndAnimatedIconColor()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            AvaloniaThemeManager.Apply(new LauncherSettings
            {
                ColorMode = ColorMode.Light,
                LightColor = ColorTheme.CatBlue
            });

            MyMenuItem item = new()
            {
                Header = "刷新",
                SvgIcon = "lucide/refresh-cw",
                Width = 120d,
                Height = 32d
            };
            Window window = new()
            {
                Width = 180d,
                Height = 90d,
                Content = new Border
                {
                    Margin = new Thickness(20d),
                    Child = item
                }
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual(RequiredBrush("ColorBrushTransparent").Color, BrushColor(item.Background));
                Assert.AreEqual(RequiredBrush("ColorBrush1").Color, BrushColor(item.Foreground));

                MoveTo(window, item);
                Assert.IsTrue(ModAnimation.AniIsRun("MyMenuItem Color " + item.Uuid));
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.AreEqual(RequiredBrush("ColorBrush6").Color, BrushColor(item.Background));
                Assert.AreEqual(RequiredBrush("ColorBrush2").Color, BrushColor(item.Foreground));
                Assert.AreEqual(
                    RequiredBrush("ColorBrush2").Color,
                    BrushColor(FindVisual<SvgIcon>(item)!.IconBrush));

                item.IsEnabled = false;
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.AreEqual(RequiredBrush("ColorBrushTransparent").Color, BrushColor(item.Background));
                Assert.AreEqual(RequiredBrush("ColorBrushGray5").Color, BrushColor(item.Foreground));
                Assert.AreEqual(
                    RequiredBrush("ColorBrushGray5").Color,
                    BrushColor(FindVisual<SvgIcon>(item)!.IconBrush));
            }
            finally
            {
                window.Close();
            }

            static Color BrushColor(IBrush? brush) =>
                ((SolidColorBrush)(brush ?? throw new InvalidOperationException("Expected a solid brush."))).Color;
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyHint_UsesWpfThemeCompatibilityAndDisposeAnimation()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            AvaloniaThemeManager.Apply(new LauncherSettings
            {
                ColorMode = ColorMode.Light,
                LightColor = ColorTheme.CatBlue
            });

            MyHint hint = new()
            {
                Text = "提示内容",
                Theme = MyHint.Themes.Blue,
                CanClose = true,
                Width = 220d
            };
            Window window = new()
            {
                Width = 300d,
                Height = 120d,
                Content = new Border
                {
                    Margin = new Thickness(20d),
                    Child = hint
                }
            };

            bool clicked = false;
            hint.Click += (_, _) => clicked = true;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("提示内容", hint.FindControl<TextBlock>("LabText")!.Text);
                Assert.IsTrue(hint.FindControl<MyIconButton>("BtnClose")!.IsVisible);
                Assert.AreEqual(Color.FromRgb(13, 128, 242), ((SolidColorBrush)hint.BorderBrush!).Color);
                Assert.AreEqual(Color.FromRgb(226, 240, 253), ((SolidColorBrush)hint.Background!).Color);

                Click(window, hint);
                Assert.IsTrue(clicked);

#pragma warning disable CS0618
                hint.IsWarn = false;
                Assert.AreEqual(MyHint.Themes.Blue, hint.Theme);
                hint.IsWarn = true;
#pragma warning restore CS0618
                Assert.AreEqual(MyHint.Themes.Red, hint.Theme);

                Click(window, hint.FindControl<MyIconButton>("BtnClose")!);
                Assert.IsTrue(ModAnimation.AniIsRun("MyCard Dispose " + hint.Uuid));
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.IsFalse(hint.IsVisible);
                Assert.IsFalse(hint.IsHitTestVisible);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyIconTextButton_UsesWpfIconVisibilityMarginsAndColorAnimation()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyIconTextButton button = new()
            {
                Width = 130,
                Height = 27,
                Text = "操作"
            };
            Window window = new()
            {
                Width = 220,
                Height = 90,
                Content = new Border
                {
                    Margin = new Thickness(20),
                    Child = button
                }
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Grid logoHost = button.FindControl<Grid>("LogoHost")!;
                TextBlock label = button.FindControl<TextBlock>("LabText")!;
                Assert.IsNotNull(button.Inlines);
                Assert.IsFalse(logoHost.IsVisible);
                Assert.AreEqual(12d, label.Margin.Left);

                button.SvgIcon = "lucide/play";
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.IsTrue(logoHost.IsVisible);
                Assert.AreEqual(16d, logoHost.Width);
                Assert.AreEqual(7d, label.Margin.Left);

                MoveTo(window, button);
                Assert.IsTrue(ModAnimation.AniIsRun("MyIconTextButton Checked " + button.Uuid));
                Assert.IsTrue(ModAnimation.AniIsRun("MyIconTextButton Color " + button.Uuid));
                ModAnimation.AdvanceForTesting(16, 16);
                Assert.AreEqual(
                    Color.Parse("#1370f3"),
                    ((SolidColorBrush)label.Foreground!).Color);

                bool clicked = false;
                bool raiseByMouse = false;
                button.Click += (_, e) =>
                {
                    clicked = true;
                    raiseByMouse = e.raiseByMouse;
                };
                Click(window, button);
                Assert.IsTrue(clicked);
                Assert.IsTrue(raiseByMouse);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void LegacyInputsAndTextLinksExposeInteractiveVisualStates()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyTextBox textBox = new()
            {
                Width = 180,
                Height = 28,
                HintText = "输入内容"
            };
            MyTextButton link = new()
            {
                Text = "Minecraft 官网",
                Width = 120,
                Height = 28
            };
            StackPanel panel = new()
            {
                Margin = new Thickness(20),
                Spacing = 12,
                Children =
                {
                    textBox,
                    link
                }
            };
            Window window = new()
            {
                Width = 260,
                Height = 150,
                Content = panel
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                TextBlock hint = textBox.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Single(block => block.Name == "labHint");
                TextPresenter presenter = textBox.GetVisualDescendants()
                    .OfType<TextPresenter>()
                    .Single(block => block.Name == "PART_TextPresenter");
                Assert.AreEqual("输入内容", hint.Text);

                Assert.IsTrue(textBox.IsEnabled);
                Click(window, textBox);
                Assert.IsTrue(textBox.IsKeyboardFocusWithin);
                window.KeyPress(Key.S, RawInputModifiers.None, PhysicalKey.S, "S");
                window.KeyPress(Key.T, RawInputModifiers.None, PhysicalKey.T, "t");
                window.KeyPress(Key.E, RawInputModifiers.None, PhysicalKey.E, "e");
                window.KeyPress(Key.V, RawInputModifiers.None, PhysicalKey.V, "v");
                window.KeyPress(Key.E, RawInputModifiers.None, PhysicalKey.E, "e");
                Assert.AreEqual("Steve", textBox.Text);
                Assert.AreEqual(string.Empty, hint.Text);
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual(
                    RequiredBrush("ColorBrush3").Color,
                    ((SolidColorBrush)textBox.BorderBrush!).Color);
                Assert.AreEqual(
                    RequiredBrush("ColorBrush1").Color,
                    ((SolidColorBrush)presenter.Foreground!).Color);

                MoveTo(window, link);
                Assert.IsTrue(ModAnimation.AniIsRun("MyTextButton Color " + link.Uuid));
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.IsNotNull(link.Cursor);
                Assert.AreEqual(
                    RequiredBrush("ColorBrush3").Color,
                    ((SolidColorBrush)link.Foreground!).Color);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyTextBox_UsesWpfValidationApiAndValidatedTextChanged()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            InlineValidator<string> validator = new();
            validator.RuleFor(static text => text).MinimumLength(3).WithMessage("至少 3 个字符");
            MyTextBox textBox = new()
            {
                Width = 180,
                Height = 28,
                HintText = "请输入名称",
                ValidateRules = [validator]
            };
            Window window = new()
            {
                Width = 260,
                Height = 110,
                Content = new Border
                {
                    Margin = new Thickness(20),
                    Child = textBox
                }
            };

            int validatedChangeCount = 0;
            int validateChangedCount = 0;
            textBox.ValidatedTextChanged += (_, _) => validatedChangeCount++;
            textBox.ValidateChanged += (_, _) => validateChangedCount++;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                TextPresenter presenter = textBox.GetVisualDescendants()
                    .OfType<TextPresenter>()
                    .Single();
                textBox.Text = "ab";
                Assert.IsFalse(textBox.IsValidated);
                Assert.AreEqual("至少 3 个字符", textBox.ValidateResult);
                Assert.AreEqual(0, validatedChangeCount);

                textBox.Text = "abc";
                Assert.IsTrue(textBox.IsValidated);
                Assert.AreEqual(string.Empty, textBox.ValidateResult);
                Assert.AreEqual(1, validatedChangeCount);
                Assert.IsTrue(validateChangedCount > 0);

                textBox.IsEnabled = false;
                Assert.IsTrue(ModAnimation.AniIsRun("MyTextBox TextColor " + textBox.Uuid));
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual(
                    RequiredBrush("ColorBrushGray4").Color,
                    ((SolidColorBrush)textBox.Foreground!).Color);
                Assert.AreEqual(
                    RequiredBrush("ColorBrushGray4").Color,
                    ((SolidColorBrush)presenter.Foreground!).Color);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyTextBox_DoesNotShowValidationFailureBeforeUserInput()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            InlineValidator<string> validator = new();
            validator.RuleFor(static text => text).MinimumLength(3).WithMessage("至少 3 个字符");
            MyTextBox textBox = new()
            {
                Width = 180,
                Height = 28,
                Text = "ab",
                ValidateRules = [validator]
            };
            Window window = new()
            {
                Width = 260,
                Height = 110,
                Content = new Border
                {
                    Margin = new Thickness(20),
                    Child = textBox
                }
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                TextBlock wrong = textBox.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Single(block => block.Name == "labWrong");
                Assert.IsFalse(textBox.IsValidated);
                Assert.AreEqual("至少 3 个字符", textBox.ValidateResult);
                Assert.IsFalse(wrong.IsVisible);
                Assert.AreEqual(
                    RequiredBrush("ColorBrushBg0").Color,
                    ((SolidColorBrush)textBox.BorderBrush!).Color);

                textBox.Text = "a";
                Assert.IsTrue(ModAnimation.AniIsRun("MyTextBox Color " + textBox.Uuid));
                Assert.IsTrue(ModAnimation.AniIsRun("MyTextBox Validate " + textBox.Uuid));
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.IsFalse(textBox.IsValidated);
                Assert.IsTrue(wrong.IsVisible);
                Assert.AreEqual("至少 3 个字符", wrong.Text);
                Assert.AreEqual(21d, wrong.Height);
                Assert.AreEqual(
                    RequiredBrush("ColorBrushRedLight").Color,
                    ((SolidColorBrush)textBox.BorderBrush!).Color);

                textBox.Text = "abcd";
                Assert.IsTrue(ModAnimation.AniIsRun("MyTextBox Validate " + textBox.Uuid));
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.IsTrue(textBox.IsValidated);
                Assert.IsFalse(wrong.IsVisible);
                Assert.AreEqual(string.Empty, wrong.Text);
                Assert.AreEqual(0d, wrong.Height);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void NativeDerivedLegacyControlsReuseAvaloniaBaseThemes()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            StackPanel panel = new()
            {
                Margin = new Thickness(20),
                Spacing = 8,
                Children =
                {
                    new MyTextBox { Name = "Text", Width = 160, Height = 28 },
                    new MyComboBox
                    {
                        Name = "Combo",
                        Width = 160,
                        Height = 28,
                        Items = { new MyComboBoxItem { Content = "选项" } }
                    },
                    new MyTextButton { Name = "TextButton", Text = "文字按钮" },
                    new MyScrollViewer
                    {
                        Name = "Scroll",
                        Width = 160,
                        Height = 40,
                        Content = new TextBlock { Text = "滚动内容" }
                    },
                    new MyPageRight
                    {
                        Name = "Page",
                        Width = 160,
                        Height = 40,
                        Content = new TextBlock { Text = "右侧页面" }
                    }
                }
            };
            Window window = new()
            {
                Width = 260,
                Height = 260,
                Content = panel
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsTrue(panel.Children.OfType<MyTextBox>().Single().GetVisualDescendants().Any());
                Assert.IsTrue(panel.Children.OfType<MyComboBox>().Single().GetVisualDescendants().Any());
                Assert.IsTrue(panel.Children.OfType<MyTextButton>().Single().GetVisualDescendants().Any());
                Assert.IsTrue(panel.Children.OfType<MyScrollViewer>().Single().GetVisualDescendants().Any());
                Assert.IsTrue(panel.Children.OfType<MyPageRight>().Single().GetVisualDescendants().Any());
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
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                MyListItem launch = window.FindControl<MyListItem>("BtnTitleSelect0")!;
                MyListItem download = window.FindControl<MyListItem>("BtnTitleSelect1")!;

                Assert.IsInstanceOfType<NavigationRouteId>(launch.Tag);
                Assert.IsInstanceOfType<NavigationRouteId>(download.Tag);
                Assert.AreEqual("pcl.launch", ((NavigationRouteId)launch.Tag!).Value);
                Assert.AreEqual("pcl.download", ((NavigationRouteId)download.Tag!).Value);

                Click(window, download);

                Assert.IsFalse(launch.Checked);
                Assert.IsTrue(download.Checked);
                Assert.AreEqual(0d, GetCheckIndicator(launch).Height);
                Assert.AreEqual(20d, GetCheckIndicator(download).Height);
                AdvancePageChangeAnimation(window);
                Assert.IsNotNull(FindVisual<PageDownloadLeft>(window));
                Assert.IsNotNull(FindVisual<PageDownloadInstall>(window));
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyListItem_UsesWpfHoverBackAndCheckedScaleAnimation()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyListItem item = new()
            {
                Title = "启动",
                Type = MyListItem.CheckType.RadioBox,
                Width = 180,
                Height = 42
            };
            Window window = new()
            {
                Width = 260,
                Height = 120,
                Content = new Border
                {
                    Margin = new Thickness(20),
                    Child = item
                }
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsNull(item.Children.OfType<Border>().FirstOrDefault(border => border.Name == "RectBack"));
                MoveTo(window, item);
                Assert.IsTrue(ModAnimation.AniIsRun("ListItem Color " + item.Uuid));
                Assert.IsNotNull(item.Children.OfType<Border>().FirstOrDefault(border => border.Name == "RectBack"));

                Click(window, item);
                Assert.IsTrue(ModAnimation.AniIsRun("MyListItem Checked " + item.Uuid));
                Border check = GetCheckIndicator(item);
                Assert.AreEqual(20d, check.Height);
                Assert.IsInstanceOfType<ScaleTransform>(check.RenderTransform);
                ModAnimation.AdvanceForTesting(16, 32);
                Assert.AreEqual(1d, ((ScaleTransform)check.RenderTransform!).ScaleY, 0.01d);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyListItem_CheckedAnimationMovesTextIconAndIndicatorTogether()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyListItem item = new()
            {
                Title = "设置",
                SvgIcon = "lucide/settings",
                Type = MyListItem.CheckType.RadioBox,
                Width = 180,
                Height = 42
            };
            Window window = new()
            {
                Width = 260,
                Height = 120,
                Content = new Border
                {
                    Margin = new Thickness(20),
                    Child = item
                }
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Click(window, item);
                ModAnimation.AdvanceUntilIdleForTesting();

                Color expected = Color.Parse("#0b5bcb");
                Assert.AreEqual(20d, GetCheckIndicator(item).Height);
                Assert.AreEqual(expected, ((SolidColorBrush)item.FindControl<TextBlock>("LabTitle")!.Foreground!).Color);
                Assert.AreEqual(expected, ((SolidColorBrush)FindVisual<SvgIcon>(item)!.IconBrush!).Color);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyPageLeft_SuspendsListItemHoverDuringShowAnimation()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyListItem visibleItem = new()
            {
                Title = "游戏",
                Type = MyListItem.CheckType.Clickable,
                Width = 180,
                Height = 42
            };
            MyListItem collapsedItem = new()
            {
                Title = "隐藏",
                Type = MyListItem.CheckType.Clickable,
                Width = 180,
                Height = 42,
                IsVisible = false
            };
            StackPanel content = new();
            content.Children.Add(visibleItem);
            content.Children.Add(collapsedItem);
            MyPageLeft page = new()
            {
                AnimatedControl = content,
                Width = 220,
                Height = 120
            };
            page.Children.Add(content);
            Window window = new()
            {
                Width = 260,
                Height = 180,
                Content = page
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                page.TriggerShowAnimation();

                Assert.IsFalse(visibleItem.isMouseOverAnimationEnabled);
                Assert.IsTrue(collapsedItem.isMouseOverAnimationEnabled);

                ModAnimation.AdvanceForTesting(16, 20);

                Assert.IsTrue(visibleItem.isMouseOverAnimationEnabled);
                visibleItem.RefreshColor(page, EventArgs.Empty);
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
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Control right = window.FindControl<Control>("PanMainRight")!;
                MyListItem communityNav = window.FindControl<Panel>("PanTitleSelect")!.Children
                    .OfType<MyListItem>()
                    .Single(item => item.Tag?.ToString() == "pcl.community");
                var selectNavPage = typeof(MainWindow).GetMethod(
                    "SelectNavPage",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("SelectNavPage was not found.");
                selectNavPage.Invoke(window, [communityNav.Tag, true]);

                ModAnimation.AdvanceForTesting(16, 3);
                Assert.IsTrue(right.Opacity < 1d);

                AdvancePageChangeAnimation(window);
                Assert.AreEqual(1d, right.Opacity, 0.01d);
                Assert.IsNotNull(FindVisual<PageCommunityLeft>(window));
                Assert.IsNotNull(FindVisual<PageCommunityRight>(window));
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void PageNavigationLeftColumns_UseWpfWidthAndStretchItems()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            foreach (MyPageLeft page in new MyPageLeft[]
                     {
                         new PageDownloadLeft(),
                         new PageCommunityLeft(),
                         new PageSetupLeft(),
                         new PageInstanceLeft()
                     })
            {
                Grid layout = new()
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                    Children =
                    {
                        page,
                        new Border { [Grid.ColumnProperty] = 1 }
                    }
                };
                Window window = new() { Width = 850, Height = 760, Content = layout };
                try
                {
                    window.Show();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    double[] itemWidths = page.GetVisualDescendants().OfType<MyListItem>()
                        .Where(item => item.IsVisible)
                        .Select(item => item.Bounds.Width)
                        .ToArray();

                    Assert.AreEqual(152d, page.Bounds.Width, 0.01d, page.GetType().Name);
                    Assert.IsTrue(itemWidths.Length > 0, page.GetType().Name);
                    Assert.IsTrue(itemWidths.Min() >= 140d, page.GetType().Name);
                    Assert.AreEqual(itemWidths.Min(), itemWidths.Max(), 0.01d, page.GetType().Name);
                }
                finally
                {
                    window.Close();
                }
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public async Task ModrinthCommunityResourceCatalog_UsesDocumentedFacetsAndParsesResults()
    {
        List<Uri> requestedUris = [];
        using HttpClient client = new(new DelegateHttpMessageHandler(request =>
        {
            requestedUris.Add(request.RequestUri!);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "hits": [
                        {
                          "project_id": "AANobbMI",
                          "slug": "sodium",
                          "title": "Sodium",
                          "description": "性能优化 Mod",
                          "project_type": "mod",
                          "icon_url": "https://cdn.modrinth.com/data/AANobbMI/icon.png",
                          "downloads": 12345,
                          "date_modified": "2026-01-01T00:00:00Z"
                        }
                      ]
                    }
                    """,
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
        }));
        using ModrinthCommunityResourceCatalog catalog = new(client);

        IReadOnlyList<CommunityResourceEntry> entries = await catalog.SearchAsync(
            CommunityResourceCategory.Mod,
            " sodium ");

        CommunityResourceEntry entry = entries.Single();
        Assert.AreEqual("AANobbMI", entry.ProjectId);
        Assert.AreEqual("https://modrinth.com/mod/sodium", entry.WebsiteUrl);
        Assert.AreEqual(12_345L, entry.Downloads);
        Assert.AreEqual(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), entry.UpdatedAt);
        string modQuery = Uri.UnescapeDataString(requestedUris.Single().Query);
        StringAssert.Contains(modQuery, "query=sodium");
        StringAssert.Contains(modQuery, "[[\"project_type:mod\"]]");

        await catalog.SearchAsync(CommunityResourceCategory.DataPack, string.Empty);
        string dataPackQuery = Uri.UnescapeDataString(requestedUris[1].Query);
        StringAssert.Contains(dataPackQuery, "[[\"all_project_types:datapack\"]]");
    }

    [TestMethod]
    public void CommunityPages_SwitchCategoriesAndRenderCatalogResults()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(async () =>
        {
            FakeCommunityResourceCatalog catalog = new();
            PageCommunityLeft left = new();
            PageCommunityRight right = new(catalog);
            Window window = new()
            {
                Width = 620,
                Height = 520,
                Content = right
            };
            CommunityResourceEntry? opened = null;
            right.OpenProjectRequested += (_, entry) => opened = entry;
            left.CategoryChanged += (_, category) => _ = right.SetCategoryAsync(category);

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                right.PageOnEnter();
                ModAnimation.AdvanceUntilIdleForTesting();
                await right.RefreshAsync().ConfigureAwait(true);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                MyListItem[] renderedItems = right.GetVisualDescendants().OfType<MyListItem>().ToArray();
                Assert.IsTrue(renderedItems.Any(item => item.Title == "Sodium"));
                MyListItem result = renderedItems.Single(item => item.Title == "Sodium");
                Assert.AreEqual(CommunityResourceCategory.Mod, catalog.LastCategory);
                Assert.IsTrue(result.Info.Contains("性能优化", StringComparison.Ordinal));
                ClickAt(window, result, new Point(80d, result.Bounds.Height / 2d));
                Assert.AreEqual("sodium", opened?.Slug);

                Assert.IsTrue(left.TrySelectCategory(CommunityResourceCategory.Shader));
                await WaitForConditionAsync(() => catalog.LastCategory == CommunityResourceCategory.Shader);
                await right.RefreshAsync().ConfigureAwait(true);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.AreEqual(CommunityResourceCategory.Shader, right.Category);
                Assert.IsTrue(left.FindControl<StackPanel>("PanItem")!.Children
                    .OfType<MyListItem>()
                    .Single(item => item.Tag is CommunityResourceCategory.Shader)
                    .Checked);
            }
            finally
            {
                window.Close();
                right.Dispose();
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void CommunityDetail_ShowsRequiredDependencies()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(async () =>
        {
            CommunityResourceDownloadFile file = new("root.jar", "https://example.test/root.jar", 10, "v1", "1.0");
            CommunityResourceVersion version = new(
                "v1",
                "Root 1.0",
                "1.0",
                null,
                DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
                ["1.21.1"],
                ["fabric"],
                [file])
            {
                Dependencies =
                [
                    new CommunityResourceDependency(
                        "fabric-api",
                        null,
                        null,
                        CommunityResourceDependencyType.Required,
                        CommunityResourceSource.Modrinth)
                ]
            };
            FakeCommunityResourceCatalog catalog = new()
            {
                Versions = [version],
                Projects =
                [
                    new CommunityResourceEntry(
                        "fabric-api",
                        "fabric-api",
                        "Fabric API",
                        string.Empty,
                        "mod",
                        null,
                        0,
                        null)
                ]
            };
            PageCommunityDetail detail = new(catalog);
            Window window = new() { Width = 720, Height = 560, Content = detail };
            try
            {
                window.Show();
                await detail.ShowAsync(
                    new CommunityResourceEntry("root", "root", "Root Mod", string.Empty, "mod", null, 0, null),
                    CommunityResourceCategory.Mod,
                    new CommunitySearchOptions(GameVersion: "1.21.1", Loader: "fabric"));
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                MyListItem rendered = detail.GetVisualDescendants()
                    .OfType<MyListItem>()
                    .Single(item => item.Title == "Root 1.0");
                StringAssert.Contains(rendered.Info, "必需前置：Fabric API");
            }
            finally
            {
                window.Close();
                detail.Dispose();
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void SplashWindow_RendersStartupIcon()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            SplashWindow splash = new();
            try
            {
                splash.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsNotNull(splash.CaptureRenderedFrame());
                Assert.IsNotNull(splash.FindControl<Image>("SplashIcon")?.Source);
                Assert.IsFalse(splash.GetVisualDescendants().OfType<Border>()
                    .Any(border => border.Background is SolidColorBrush { Color.R: > 240, Color.G: > 240, Color.B: > 240, Color.A: > 0 }));
            }
            finally
            {
                splash.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void LaunchInstanceDiscovery_FindsVersionJsons()
    {
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-launch-discovery-" + Guid.NewGuid().ToString("N"));
        try
        {
            string versionDirectory = System.IO.Path.Combine(root, "versions", "1.20.1");
            Directory.CreateDirectory(versionDirectory);
            File.WriteAllText(System.IO.Path.Combine(versionDirectory, "1.20.1.json"), "{}");

            List<LaunchInstanceDiscoveryProgress> progressEvents = [];
            IReadOnlyList<LaunchInstanceInfo> instances = LaunchInstanceDiscovery.Discover(
                [root],
                new CallbackProgress<LaunchInstanceDiscoveryProgress>(progressEvents.Add));

            Assert.AreEqual(1, instances.Count);
            Assert.AreEqual("1.20.1", instances[0].Name);
            Assert.AreEqual(versionDirectory, instances[0].InstanceDirectory);
            Assert.IsTrue(progressEvents.Any(progress => progress.Stage == "正在扫描游戏文件夹"));
            Assert.IsTrue(progressEvents.Any(progress =>
                progress.Stage == "正在检查游戏版本" && progress.Current == progress.Total && progress.Found == 1));
            Assert.AreEqual("游戏版本检查完成", progressEvents[^1].Stage);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void LaunchInstanceDiscovery_UsesConfiguredMinecraftRoots()
    {
        string? previousRoots = Environment.GetEnvironmentVariable("PCLN_MINECRAFT_ROOTS");
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-launch-discovery-env-" + Guid.NewGuid().ToString("N"));
        try
        {
            string versionDirectory = System.IO.Path.Combine(root, "versions", "1.21");
            Directory.CreateDirectory(versionDirectory);
            File.WriteAllText(System.IO.Path.Combine(versionDirectory, "1.21.json"), "{}");
            Environment.SetEnvironmentVariable("PCLN_MINECRAFT_ROOTS", root);

            IReadOnlyList<LaunchInstanceInfo> instances = LaunchInstanceDiscovery.Discover(LaunchInstanceDiscovery.GetCandidateRoots());

            Assert.IsTrue(instances.Any(instance => instance.Name == "1.21"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PCLN_MINECRAFT_ROOTS", previousRoots);
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageLaunchLeft_FallsBackToDownloadWhenNoVersions()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageLaunchLeft page = new();
            Window window = new()
            {
                Width = 420,
                Height = 360,
                Content = page
            };

            try
            {
                window.Show();
                page.SetInstances([]);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.AreEqual("下载游戏", page.FindControl<MyButton>("BtnLaunch")!.Text);
                Assert.IsTrue(page.FindControl<MyButton>("BtnLaunch")!.IsEnabled);
                Assert.AreEqual("未找到可启动的游戏版本", page.FindControl<TextBlock>("LabVersion")!.Text);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageDownloadLeft_UsesWpfVersionFilterList()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageDownloadInstall installPage = new();
            PageDownloadLeft page = new(() => installPage);
            Window window = new()
            {
                Width = 220,
                Height = 320,
                Content = page
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsNotNull(page.FindControl<MyListItem>("ItemAll"));
                Assert.IsNotNull(page.FindControl<MyListItem>("ItemRelease"));
                Assert.IsNotNull(page.FindControl<MyListItem>("ItemSnapshot"));
                Assert.IsNotNull(page.FindControl<MyListItem>("ItemBeforeRelease"));
                Assert.IsNotNull(page.FindControl<MyListItem>("ItemAprilFools"));
                Assert.AreEqual("版本类型", page.GetVisualDescendants().OfType<TextBlock>().First().Text);
                Assert.AreEqual("全部版本", page.FindControl<MyListItem>("ItemAll")!.Title);
                Assert.AreEqual(
                    "全部版本",
                    page.FindControl<MyListItem>("ItemAll")!.FindControl<TextBlock>("LabTitle")!.Text);
                Assert.AreSame(installPage, page.GetOrCreateCurrentPage());
                Assert.AreEqual(DownloadVersionFilter.All, page.FindControl<MyListItem>("ItemAll")!.Tag);
                Assert.AreEqual(DownloadVersionFilter.Release, page.FindControl<MyListItem>("ItemRelease")!.Tag);

                Click(window, page.FindControl<MyListItem>("ItemRelease")!);

                Assert.AreEqual(DownloadVersionFilter.Release, page.VersionFilter);
                Assert.AreEqual(DownloadPageSubType.Install, page.PageId);
                Assert.AreSame(installPage, page.GetOrCreateCurrentPage());
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageDownloadInstall_FiltersAndSelectsVanillaVersion()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageDownloadInstall page = new(new MinecraftVanillaInstallService(), new FakeMinecraftLoaderMetadataService(), new FakeMinecraftInstallAddonMetadataService());
            SetPrivateField(
                page,
                "_versions",
                new[]
                {
                    new MinecraftVersionManifestEntry("1.20.1", "release", "https://example.invalid/1.20.1.json", DateTimeOffset.Parse("2023-06-12T00:00:00Z")),
                    new MinecraftVersionManifestEntry("24w14a", "snapshot", "https://example.invalid/24w14a.json", DateTimeOffset.Parse("2024-04-03T00:00:00Z")),
                    new MinecraftVersionManifestEntry("20w14infinite", "snapshot", "https://example.invalid/20w14infinite.json", DateTimeOffset.Parse("2020-04-01T00:00:00Z"))
                });
            Window window = new()
            {
                Width = 520,
                Height = 420,
                Content = page
            };
            DownloadInstallRequest? requested = null;
            page.InstallRequested += (_, version) => requested = version;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                page.ApplyVersionFilter(DownloadVersionFilter.Release);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                MyCard[] cards = page.FindControl<StackPanel>("PanMinecraft")!.Children.OfType<MyCard>().ToArray();
                Assert.AreEqual(2, cards.Length);
                Assert.AreEqual("最新版本", cards[0].Title);
                Assert.AreEqual("其他版本", cards[1].Title);
                Assert.AreEqual(2, page.GetVisualDescendants().OfType<MyListItem>().Count(listItem => listItem.Title == "1.20.1" && listItem.IsVisible));
                Assert.AreEqual(1, page.GetVisualDescendants().OfType<MyListItem>().Count(listItem => listItem.Title == "24w14a" && listItem.IsVisible));

                MyListItem item = page.GetVisualDescendants().OfType<MyListItem>().First(listItem => listItem.Title == "1.20.1" && listItem.IsVisible);

                Click(window, item);

                Assert.IsTrue(page.FindControl<StackPanel>("PanSelect")!.IsVisible);
                Assert.AreEqual("1.20.1", page.FindControl<MyTextBox>("TextSelectName")!.Text);

                page.FindControl<MyTextBox>("TextSelectName")!.Text = "我的 1.20.1";
                Click(window, page.FindControl<MyExtraTextButton>("BtnStart")!);

                Assert.AreEqual("我的 1.20.1", requested?.VersionId);
                Assert.AreEqual("1.20.1", requested?.BaseVersionId);
                Assert.AreEqual("https://example.invalid/1.20.1.json", requested?.VersionJsonUrl);

                requested = null;
                page.FindControl<MyTextBox>("TextSelectName")!.Text = "Bad/Name";
                Assert.IsFalse(page.FindControl<MyExtraTextButton>("BtnStart")!.IsEnabled);
                Click(window, page.FindControl<MyExtraTextButton>("BtnStart")!);
                Assert.IsNull(requested);

                page.FocusVersionAsync("24w14a").GetAwaiter().GetResult();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsTrue(page.FindControl<StackPanel>("PanSelect")!.IsVisible);
                Assert.AreEqual("24w14a", page.FindControl<MyTextBox>("TextSelectName")!.Text);
                Click(window, page.FindControl<MyExtraTextButton>("BtnStart")!);

                Assert.AreEqual("24w14a", requested?.VersionId);

                page.FocusVersionAsync("20w14∞").GetAwaiter().GetResult();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("20w14∞", page.FindControl<MyTextBox>("TextSelectName")!.Text);
                Click(window, page.FindControl<MyExtraTextButton>("BtnStart")!);

                Assert.AreEqual("20w14∞", requested?.VersionId);
                Assert.AreEqual("https://example.invalid/20w14infinite.json", requested?.VersionJsonUrl);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageDownloadInstall_SelectingVersionResetsLoaderCardsLikeWpf()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageDownloadInstall page = new();
            SetPrivateField(
                page,
                "_versions",
                new[]
                {
                    new MinecraftVersionManifestEntry("1.20.1", "release", "https://example.invalid/1.20.1.json", DateTimeOffset.Parse("2023-06-12T00:00:00Z")),
                    new MinecraftVersionManifestEntry("24w14a", "snapshot", "https://example.invalid/24w14a.json", DateTimeOffset.Parse("2024-04-03T00:00:00Z"))
                });
            Window window = new()
            {
                Width = 560,
                Height = 440,
                Content = page
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                page.ApplyVersionFilter(DownloadVersionFilter.All);
                MyListItem first = page.GetVisualDescendants().OfType<MyListItem>().First(listItem => listItem.Title == "1.20.1");
                Click(window, first);
                ModAnimation.AdvanceUntilIdleForTesting();

                MyCard forge = page.FindControl<MyCard>("CardForge")!;
                MyCard optiFine = page.FindControl<MyCard>("CardOptiFine")!;
                forge.IsSwapped = false;
                optiFine.IsSwapped = false;
                Assert.IsFalse(forge.IsSwapped);
                Assert.IsFalse(optiFine.IsSwapped);

                page.FocusVersionAsync("24w14a").GetAwaiter().GetResult();
                ModAnimation.AdvanceUntilIdleForTesting();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("24w14a", page.FindControl<MyTextBox>("TextSelectName")!.Text);
                Assert.IsTrue(forge.IsSwapped);
                Assert.IsTrue(optiFine.IsSwapped);
                Assert.IsTrue(page.FindControl<MyScrollViewer>("PanBack")!.IsHitTestVisible);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageDownloadInstall_AppliesWpfLoaderCardAvailability()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageDownloadInstall page = new(new MinecraftVanillaInstallService(), new FakeMinecraftLoaderMetadataService(), new FakeMinecraftInstallAddonMetadataService());
            GetPrivateField<Dictionary<(MinecraftLoaderKind Kind, string GameVersion), IReadOnlyList<MinecraftLoaderVersionEntry>>>(
                page,
                "_loaderVersionCache")[(MinecraftLoaderKind.NeoForge, "1.20.1")] = [];
            SetPrivateField(
                page,
                "_versions",
                new[]
                {
                    new MinecraftVersionManifestEntry("1.20.1", "release", "https://example.invalid/1.20.1.json", DateTimeOffset.Parse("2023-06-12T00:00:00Z")),
                    new MinecraftVersionManifestEntry("1.12.2", "release", "https://example.invalid/1.12.2.json", DateTimeOffset.Parse("2017-09-18T00:00:00Z"))
                });
            Window window = new()
            {
                Width = 620,
                Height = 520,
                Content = page
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                page.FocusVersionAsync("1.20.1").GetAwaiter().GetResult();
                ModAnimation.AdvanceUntilIdleForTesting();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                AssertLoaderVisible(page, "Forge", "可以添加");
                AssertLoaderVisible(page, "NeoForge", "暂无可用版本");
                Assert.IsFalse(page.FindControl<MyCard>("CardNeoForge")!.MainSwap.IsVisible);
                AssertLoaderVisible(page, "Fabric", "可以添加");
                AssertLoaderVisible(page, "Quilt", "可以添加");
                AssertLoaderVisible(page, "LabyMod", "可以添加");
                AssertLoaderVisible(page, "OptiFine", "可以添加");
                AssertLoaderHidden(page, "Cleanroom");
                AssertLoaderHidden(page, "LiteLoader");
                AssertLoaderHidden(page, "LegacyFabric");
                AssertLoaderHidden(page, "FabricApi");
                AssertLoaderHidden(page, "OptiFabric");

                page.FocusVersionAsync("1.12.2").GetAwaiter().GetResult();
                ModAnimation.AdvanceUntilIdleForTesting();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                AssertLoaderVisible(page, "Forge", "可以添加");
                AssertLoaderVisible(page, "Cleanroom", "可以添加");
                AssertLoaderVisible(page, "LegacyFabric", "可以添加");
                AssertLoaderVisible(page, "LiteLoader", "可以添加");
                AssertLoaderVisible(page, "LabyMod", "可以添加");
                AssertLoaderVisible(page, "OptiFine", "可以添加");
                AssertLoaderHidden(page, "NeoForge");
                AssertLoaderHidden(page, "Fabric");
                AssertLoaderHidden(page, "Quilt");
                AssertLoaderHidden(page, "FabricApi");
                AssertLoaderHidden(page, "LegacyFabricApi");
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageDownloadInstall_SelectsFabricLoaderAndRaisesInstallRequest()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageDownloadInstall page = new(new MinecraftVanillaInstallService(), new FakeMinecraftLoaderMetadataService(), new FakeMinecraftInstallAddonMetadataService());
            SetPrivateField(
                page,
                "_versions",
                new[]
                {
                    new MinecraftVersionManifestEntry("1.20.1", "release", "https://example.invalid/1.20.1.json", DateTimeOffset.Parse("2023-06-12T00:00:00Z"))
                });
            Dictionary<(MinecraftLoaderKind Kind, string GameVersion), IReadOnlyList<MinecraftLoaderVersionEntry>> cache =
                GetPrivateField<Dictionary<(MinecraftLoaderKind Kind, string GameVersion), IReadOnlyList<MinecraftLoaderVersionEntry>>>(
                    page,
                    "_loaderVersionCache");
            cache[(MinecraftLoaderKind.Fabric, "1.20.1")] =
            [
                new MinecraftLoaderVersionEntry(MinecraftLoaderKind.Fabric, "0.16.14", true)
            ];

            Window window = new()
            {
                Width = 620,
                Height = 520,
                Content = page
            };
            DownloadInstallRequest? requested = null;
            page.InstallRequested += (_, request) => requested = request;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                page.FocusVersionAsync("1.20.1").GetAwaiter().GetResult();
                ModAnimation.AdvanceUntilIdleForTesting();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                MyCard fabricCard = page.FindControl<MyCard>("CardFabric")!;
                Click(window, fabricCard);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                MyListItem loaderItem = page.GetVisualDescendants()
                    .OfType<MyListItem>()
                    .First(item => item.Title == "0.16.14");
                Click(window, loaderItem);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("fabric-loader-0.16.14-1.20.1", page.FindControl<MyTextBox>("TextSelectName")!.Text);
                Assert.AreEqual("0.16.14", page.FindControl<TextBlock>("LabFabric")!.Text);
                Assert.IsTrue(page.FindControl<Control>("BtnFabricClear")!.IsVisible);
                Assert.IsTrue(page.FindControl<Control>("HintFabricAPI")!.IsVisible);

                MyCard fabricApiCard = page.FindControl<MyCard>("CardFabricApi")!;
                Assert.IsTrue(fabricApiCard.IsVisible);
                Assert.IsTrue(fabricApiCard.MainSwap.IsVisible);
                Click(window, fabricApiCard);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                MyListItem apiItem = page.GetVisualDescendants()
                    .OfType<MyListItem>()
                    .First(item => item.Title == "0.100.0+1.20.1");
                Click(window, apiItem);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.IsFalse(page.FindControl<Control>("HintFabricAPI")!.IsVisible);
                Assert.AreEqual("与 Fabric 不兼容", page.FindControl<TextBlock>("LabOptiFine")!.Text);
                Assert.IsFalse(page.FindControl<MyCard>("CardOptiFine")!.MainSwap.IsVisible);
                Assert.IsFalse(page.FindControl<MyCard>("CardOptiFabric")!.IsVisible);

                Click(window, page.FindControl<MyExtraTextButton>("BtnStart")!);

                Assert.AreEqual("fabric-loader-0.16.14-1.20.1", requested?.VersionId);
                Assert.AreEqual("1.20.1", requested?.BaseVersionId);
                Assert.AreEqual("https://example.invalid/1.20.1.json", requested?.VersionJsonUrl);
                Assert.AreEqual(MinecraftLoaderKind.Fabric, requested?.Loader?.Kind);
                Assert.AreEqual("0.16.14", requested?.Loader?.LoaderVersion);
                CollectionAssert.AreEquivalent(
                    new[]
                    {
                        MinecraftInstallAddonKind.FabricApi
                    },
                    requested?.Addons?.Select(addon => addon.Kind).ToArray());
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageDownloadInstall_PreservesInstanceInstallTargetWhenSelectingLoader()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageDownloadInstall page = new(new MinecraftVanillaInstallService(), new FakeMinecraftLoaderMetadataService(), new FakeMinecraftInstallAddonMetadataService());
            SetPrivateField(
                page,
                "_versions",
                new[]
                {
                    new MinecraftVersionManifestEntry("1.20.1", "release", "https://example.invalid/1.20.1.json", DateTimeOffset.Parse("2023-06-12T00:00:00Z"))
                });
            Dictionary<(MinecraftLoaderKind Kind, string GameVersion), IReadOnlyList<MinecraftLoaderVersionEntry>> cache =
                GetPrivateField<Dictionary<(MinecraftLoaderKind Kind, string GameVersion), IReadOnlyList<MinecraftLoaderVersionEntry>>>(
                    page,
                    "_loaderVersionCache");
            cache[(MinecraftLoaderKind.Fabric, "1.20.1")] =
            [
                new MinecraftLoaderVersionEntry(MinecraftLoaderKind.Fabric, "0.16.14", true)
            ];

            Window window = new()
            {
                Width = 620,
                Height = 520,
                Content = page
            };
            DownloadInstallRequest? requested = null;
            page.InstallRequested += (_, request) => requested = request;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                page.FocusVersionAsync(
                        "1.20.1",
                        "My Fabric Pack",
                        preserveInstallNameOnLoaderSelect: true,
                        minecraftRootDirectory: @"D:\Games\.minecraft",
                        openLoaderKind: MinecraftLoaderKind.Fabric,
                        replaceExistingVersion: true)
                    .GetAwaiter()
                    .GetResult();
                ModAnimation.AdvanceUntilIdleForTesting();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("My Fabric Pack", page.FindControl<MyTextBox>("TextSelectName")!.Text);
                Assert.IsFalse(page.FindControl<MyCard>("CardFabric")!.IsSwapped);

                MyListItem loaderItem = page.GetVisualDescendants()
                    .OfType<MyListItem>()
                    .First(item => item.Title == "0.16.14");
                Click(window, loaderItem);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("My Fabric Pack", page.FindControl<MyTextBox>("TextSelectName")!.Text);

                Click(window, page.FindControl<MyExtraTextButton>("BtnStart")!);

                Assert.AreEqual("My Fabric Pack", requested?.VersionId);
                Assert.AreEqual("1.20.1", requested?.BaseVersionId);
                Assert.AreEqual(@"D:\Games\.minecraft", requested?.MinecraftRootDirectory);
                Assert.IsTrue(requested?.ReplaceExistingVersion);
                Assert.AreEqual(MinecraftLoaderKind.Fabric, requested?.Loader?.Kind);
                Assert.AreEqual("0.16.14", requested?.Loader?.LoaderVersion);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    private static void AssertLoaderVisible(PageDownloadInstall page, string name, string status)
    {
        MyCard card = page.FindControl<MyCard>("Card" + name)!;
        TextBlock label = page.FindControl<TextBlock>("Lab" + name)!;
        Control info = page.FindControl<Control>("Pan" + name + "Info")!;
        Control clear = page.FindControl<Control>("Btn" + name + "Clear")!;

        Assert.IsTrue(card.IsVisible, name + " card should follow WPF visibility rules.");
        Assert.IsTrue(card.IsSwapped, name + " card should reset to collapsed after selecting vanilla.");
        Assert.IsTrue(info.IsVisible, name + " summary row should be visible while collapsed.");
        Assert.IsFalse(clear.IsVisible, name + " clear button should be hidden without a selected loader.");
        Assert.AreEqual(status, label.Text);
    }

    private static void AssertLoaderHidden(PageDownloadInstall page, string name)
    {
        Assert.IsFalse(page.FindControl<MyCard>("Card" + name)!.IsVisible, name + " card should be hidden.");
    }

    [TestMethod]
    public void PageDownloadInstall_SelectPageSwitchUsesWpfAnimationStates()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageDownloadInstall page = new();
            SetPrivateField(
                page,
                "_versions",
                new[]
                {
                    new MinecraftVersionManifestEntry("1.20.1", "release", "https://example.invalid/1.20.1.json", DateTimeOffset.Parse("2023-06-12T00:00:00Z"))
                });
            Window window = new()
            {
                Width = 560,
                Height = 440,
                Content = page
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                page.ApplyVersionFilter(DownloadVersionFilter.All);
                MyListItem item = page.GetVisualDescendants().OfType<MyListItem>().First(listItem => listItem.Title == "1.20.1");
                StackPanel minecraft = page.FindControl<StackPanel>("PanMinecraft")!;
                StackPanel select = page.FindControl<StackPanel>("PanSelect")!;
                MyScrollViewer scroll = page.FindControl<MyScrollViewer>("PanBack")!;

                Click(window, item);

                Assert.IsTrue(minecraft.IsVisible);
                Assert.AreEqual(1d, minecraft.Opacity, 0.01d);
                Assert.IsTrue(select.IsVisible);
                Assert.AreEqual(0d, select.Opacity, 0.01d);
                Assert.IsFalse(scroll.IsHitTestVisible);

                ModAnimation.AdvanceUntilIdleForTesting();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsFalse(minecraft.IsVisible);
                Assert.IsTrue(select.IsVisible);
                Assert.AreEqual(1d, select.Opacity, 0.01d);
                Assert.IsTrue(scroll.IsHitTestVisible);

                Click(window, page.FindControl<MyIconButton>("BtnBack")!);

                Assert.IsTrue(select.IsVisible);
                Assert.IsTrue(minecraft.IsVisible);
                Assert.IsFalse(scroll.IsHitTestVisible);
                Assert.IsTrue(page.FindControl<Control>("TextSearchVersion")!.IsVisible);

                ModAnimation.AdvanceUntilIdleForTesting();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsFalse(select.IsVisible);
                Assert.IsTrue(minecraft.IsVisible);
                Assert.AreEqual(1d, minecraft.Opacity, 0.01d);
                Assert.IsTrue(scroll.IsHitTestVisible);
                Assert.AreEqual(new Thickness(25d, 10d, 25d, 25d), page.FindControl<Grid>("PanInner")!.Margin);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageDownloadInstall_RendersManifestEntriesWithoutBlockingUi()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        IReadOnlyList<MinecraftVersionManifestEntry> versions =
        [
            new("1.21.5", "release", "https://example.invalid/versions/1.21.5.json", DateTimeOffset.Parse("2025-03-25T00:00:00Z")),
            new("25w14craftmine", "snapshot", "https://example.invalid/versions/25w14craftmine.json", DateTimeOffset.Parse("2025-04-01T00:00:00Z"))
        ];

        session.Dispatch(() =>
        {
            PageDownloadInstall page = new();
            SetPrivateField(page, "_versions", versions);
            SetPrivateField(page, "_isLoading", false);
            Window window = new()
            {
                Width = 560,
                Height = 420,
                Content = page
            };

            try
            {
                window.Show();
                InvokePrivateNoArgs(page, "ReloadVersionList");
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.IsFalse(page.FindControl<Control>("PanLoad")!.IsVisible);

                MyCard[] cards = page.FindControl<StackPanel>("PanMinecraft")!
                    .Children
                    .OfType<MyCard>()
                    .ToArray();
                Assert.AreEqual(2, cards.Length);
                Assert.AreEqual("最新版本", cards[0].Title);
                Assert.AreEqual("其他版本", cards[1].Title);
                MyListItem releaseItem = page.GetVisualDescendants()
                    .OfType<MyListItem>()
                    .First(item => item.Title == "1.21.5");
                MyListItem aprilFoolsItem = page.GetVisualDescendants()
                    .OfType<MyListItem>()
                    .First(item => item.Title == "25w14craftmine");
                AssertRenderedVersionItem(releaseItem, "1.21.5");
                AssertRenderedVersionItem(aprilFoolsItem, "25w14craftmine");
                Assert.AreEqual("avares://PCL.Desktop/Assets/Legacy/Blocks/Grass.png", releaseItem.Logo);
                Assert.AreEqual("avares://PCL.Desktop/Assets/Legacy/Blocks/GoldBlock.png", aprilFoolsItem.Logo);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    private static void AssertRenderedVersionItem(MyListItem item, string title)
    {
        TextBlock titleBlock = item.FindControl<TextBlock>("LabTitle")!;
        Assert.AreEqual(title, DisplayText(titleBlock));
        Assert.IsTrue(titleBlock.IsVisible);
        string layoutDiagnostics = $"item={item.Bounds}, titleBounds={titleBlock.Bounds}, titleDesired={titleBlock.DesiredSize}, " +
                                   $"titleVisible={titleBlock.IsVisible}, titleOpacity={titleBlock.Opacity}, " +
                                   $"titleFont={titleBlock.FontSize}, titleParent={titleBlock.Parent?.GetType().Name}, " +
                                   $"row={Grid.GetRow(titleBlock)}, column={Grid.GetColumn(titleBlock)}, span={Grid.GetColumnSpan(titleBlock)}, " +
                                   $"children={item.Children.Count}, " +
                                   $"columns={string.Join(',', item.ColumnDefinitions.Select(column => column.Width.ToString()))}";
        Assert.IsTrue(titleBlock.Bounds.Width > 1d, "Version title should be measured wider than 1px. " + layoutDiagnostics);
        Assert.IsTrue(titleBlock.Bounds.Height > 1d, "Version title should be measured taller than 1px. " + layoutDiagnostics);
        Assert.AreEqual(RequiredBrush("ColorBrush1").Color, ((SolidColorBrush)titleBlock.Foreground!).Color);
    }

    private static string DisplayText(TextBlock textBlock)
    {
        if (!string.IsNullOrEmpty(textBlock.Text))
            return textBlock.Text;

        return textBlock.Inlines is null
            ? string.Empty
            : string.Concat(textBlock.Inlines.OfType<Run>().Select(run => run.Text));
    }

    [TestMethod]
    public void PageSpeedRight_UsesWpfTaskCardListAndCancelButton()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageSpeedRight page = new();
            Window window = new()
            {
                Width = 520,
                Height = 260,
                Content = page
            };
            int cancelCount = 0;
            int dismissCount = 0;
            string? canceledTask = null;
            string? dismissedTask = null;
            page.CancelRequested += (_, args) =>
            {
                cancelCount++;
                canceledTask = args.TaskId;
            };
            page.DismissRequested += (_, args) =>
            {
                dismissCount++;
                dismissedTask = args.TaskId;
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                page.UpsertTask(new TaskManagerEntrySnapshot(
                    "install:1.20.1",
                    "安装 1.20.1",
                    "下载版本描述",
                    "1.20.1.json",
                    0.42d,
                    3,
                    10,
                    2048,
                    TaskManagerTaskState.Running,
                    Steps:
                    [
                        new TaskManagerSubTaskSnapshot("下载版本描述", "1.20.1.json", 0.42d, TaskManagerTaskState.Running),
                        new TaskManagerSubTaskSnapshot("下载运行库", "3 / 10 个文件", 0.3d, TaskManagerTaskState.Running)
                    ]));
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                MyCard card = page.GetVisualDescendants().OfType<MyCard>().Single(card => card.Title == "安装 1.20.1");
                string[] text = page.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Select(block => block.Text ?? string.Empty)
                    .ToArray();
                Assert.AreEqual(1, page.TaskCount);
                Assert.IsTrue(page.HasActiveTasks);
                Assert.IsTrue(text.Any(value => value.Contains("下载版本描述", StringComparison.Ordinal)));
                Assert.IsTrue(text.Any(value => value.Contains("1.20.1.json", StringComparison.Ordinal)));
                Assert.IsTrue(text.Any(value => value.Contains("下载运行库", StringComparison.Ordinal)));
                Assert.IsTrue(text.Contains("42%"));

                page.UpsertTask(new TaskManagerEntrySnapshot(
                    "install:1.20.1",
                    "安装 1.20.1",
                    "下载运行库",
                    "5 / 10 个文件",
                    0.55d,
                    5,
                    10,
                    4096,
                    TaskManagerTaskState.Running,
                    Steps:
                    [
                        new TaskManagerSubTaskSnapshot("下载版本描述", "1.20.1.json", 1d, TaskManagerTaskState.Finished),
                        new TaskManagerSubTaskSnapshot("下载运行库", "5 / 10 个文件", 0.5d, TaskManagerTaskState.Running)
                    ]));
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.AreSame(card, page.GetVisualDescendants().OfType<MyCard>().Single(card => card.Title == "安装 1.20.1"));
                Assert.IsTrue(page.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == "50%"));
                Assert.IsTrue(page.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text?.Contains("5 / 10 个文件", StringComparison.Ordinal) == true));

                page.UpsertTask(new TaskManagerEntrySnapshot(
                    "repair:demo",
                    "修复 demo",
                    "下载资源文件",
                    "2 / 5 个文件",
                    0.2d,
                    2,
                    5,
                    1024,
                    TaskManagerTaskState.Running));
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.AreEqual(2, page.TaskCount);

                MyIconButton cancelButton = card.GetVisualDescendants()
                    .OfType<MyIconButton>()
                    .Single(button => Equals(button.ToolTip, "取消任务"));
                Assert.IsTrue(cancelButton.IsVisible);
                Click(window, cancelButton);
                Assert.AreEqual(1, cancelCount);
                Assert.AreEqual("install:1.20.1", canceledTask);

                MyCard repairCard = page.GetVisualDescendants().OfType<MyCard>().Single(card => card.Title == "修复 demo");
                MyIconButton repairCancelButton = repairCard.GetVisualDescendants()
                    .OfType<MyIconButton>()
                    .Single(button => Equals(button.ToolTip, "取消任务"));
                Click(window, repairCancelButton);
                Assert.AreEqual(2, cancelCount);
                Assert.AreEqual("repair:demo", canceledTask);
                page.RemoveTask("repair:demo");
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.AreEqual(1, page.TaskCount);

                page.UpsertTask(new TaskManagerEntrySnapshot(
                    "failed:demo",
                    "失败任务",
                    "任务失败",
                    "请查看错误信息",
                    0.3d,
                    1,
                    3,
                    0,
                    TaskManagerTaskState.Failed,
                    ErrorMessage: "网络连接失败"));
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                MyCard failedCard = page.GetVisualDescendants().OfType<MyCard>().Single(item => item.Title == "失败任务");
                MyIconButton dismissButton = failedCard.GetVisualDescendants()
                    .OfType<MyIconButton>()
                    .Single(button => Equals(button.ToolTip, "移除任务"));
                Assert.IsTrue(dismissButton.IsVisible);
                Click(window, dismissButton);
                Assert.AreEqual(1, dismissCount);
                Assert.AreEqual("failed:demo", dismissedTask);
                page.RemoveTask("failed:demo");

                page.UpsertTask(new TaskManagerEntrySnapshot(
                    "install:1.20.1",
                    "安装 1.20.1",
                    "安装完成",
                    "任务已完成",
                    1d,
                    10,
                    10,
                    0,
                    TaskManagerTaskState.Finished));
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsFalse(cancelButton.IsVisible);
                Assert.IsFalse(page.HasActiveTasks);
                Assert.IsTrue(page.GetVisualDescendants().OfType<TextBlock>().Any(block => block.Text == "√"));

                page.RemoveTask("install:1.20.1");
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.AreEqual(0, page.TaskCount);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void HostModuleSettingsPage_RetainsOnlyRouteIdWithoutGeneratedHeader()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageSetupHostModule page = new(new HostSettingsPageDescriptor(
                "pcl.plugin.settings",
                "插件",
                "lucide/plug",
                "PCL.Plugin 已加载",
                "此页面由插件 HostModule 注册。",
                [new HostSettingsHintDescriptor("重启后应用插件变更。", HostSettingsHintKind.Warning)]));
            Window window = new() { Width = 600d, Height = 400d, Content = page };
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("pcl.plugin.settings", page.PageId);
                Assert.IsNull(page.FindControl<TextBlock>("LabHostHeading"));
                Assert.IsNull(page.FindControl<TextBlock>("LabHostDescription"));
                Assert.AreEqual(0, page.GetVisualDescendants().OfType<MyHint>().Count());
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PluginHostSettingsPages_WithoutFactoryRemainEmptyRouteTargets()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            HostSettingsPageDescriptor[] descriptors =
            [
                new("pcl.plugin.installed", "已安装", "lucide/package", "已安装插件", "管理本地安装的第三方插件。", []),
                new("pcl.plugin.market", "市场", "lucide/store", "插件市场", "扫描本地 .pnp 包。", []),
                new("pcl.plugin.developer", "开发者模式", "lucide/code-2", "插件开发者模式", "配置插件开发选项。", []),
                new("pcl.plugin.safety", "安全", "lucide/shield", "插件安全", "配置安全模式。", []),
                new("pcl.plugin.ui-patches", "UI Patch", "lucide/panel-top", "UI Patch", "查看 UI Patch 计划。", []),
                new("pcl.plugin.compatibility", "兼容性", "lucide/git-compare", "兼容性", "查看离线兼容性记录。", [])
            ];

            foreach (HostSettingsPageDescriptor descriptor in descriptors)
            {
                Type factoryType = typeof(PageSetupHostModule).Assembly.GetType(
                    "PCL.Desktop.Features.Settings.Views.HostSettingsPageFactory",
                    throwOnError: true)!;
                MyPageRight page = (MyPageRight)factoryType.GetMethod(
                    "Create",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
                    .Invoke(null, [descriptor])!;
                Window window = new() { Width = 600d, Height = 400d, Content = page };
                try
                {
                    window.Show();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    Assert.IsNull(page.FindControl<TextBlock>("LabHostHeading"));
                    Assert.IsNull(page.FindControl<TextBlock>("LabHostDescription"));
                    Assert.IsInstanceOfType<PageSetupHostModule>(page);
                    Assert.AreEqual(descriptor.Id, ((PageSetupHostModule)page).PageId);
                }
                finally
                {
                    window.Close();
                }
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    [TestCategory("InjectedPlugin")]
    public void InjectedPlugin_PageFactoriesAreOwnedByPluginAssembly()
    {
        bool pluginExpected = string.Equals(
            Environment.GetEnvironmentVariable("PCLN_EXPECT_PLUGIN_UI"),
            "1",
            StringComparison.Ordinal);
        if (!pluginExpected)
            return;

        Type loaderType = typeof(MainWindow).Assembly.GetType(
            "PCL.Desktop.Hosting.EmbeddedRuntimeExtensionLoader",
            throwOnError: true)!;
        object? rawModules = loaderType.GetMethod(
                "LoadHostModules",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .Invoke(null, null);
        IReadOnlyList<IPclHostModule> modules = Assert.IsInstanceOfType<IReadOnlyList<IPclHostModule>>(rawModules);
        PclHostBuilder builder = new();
        foreach (IPclHostModule module in modules)
            builder.AddModule(module);
        IPclHost host = builder.Build();

        foreach (HostSettingsPageDescriptor page in host.SettingsPages.Pages)
        {
            Assert.IsNotNull(page.PageFactory, $"Missing page factory: {page.Id}");
            Assert.AreEqual("PCL.Plugin", page.PageFactory.Method.DeclaringType?.Assembly.GetName().Name);
        }
    }

    [TestMethod]
    [TestCategory("InjectedPlugin")]
    public void InjectedPlugin_DeveloperPageFactoryRendersHeadless()
    {
        bool pluginExpected = string.Equals(
            Environment.GetEnvironmentVariable("PCLN_EXPECT_PLUGIN_UI"),
            "1",
            StringComparison.Ordinal);
        if (!pluginExpected)
            return;

        Type loaderType = typeof(MainWindow).Assembly.GetType(
            "PCL.Desktop.Hosting.EmbeddedRuntimeExtensionLoader",
            throwOnError: true)!;
        IReadOnlyList<IPclHostModule> modules = Assert.IsInstanceOfType<IReadOnlyList<IPclHostModule>>(
            loaderType.GetMethod(
                    "LoadHostModules",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
                .Invoke(null, null));
        PclHostBuilder builder = new();
        foreach (IPclHostModule module in modules)
            builder.AddModule(module);
        HostSettingsPageDescriptor descriptor = builder.Build().SettingsPages.Pages.Single(page =>
            page.Id == "pcl.plugin.developer");

        using SafeHeadlessUnitTestSession session = CreateSession();
        session.Dispatch(() =>
        {
            MyPageRight page = Assert.IsInstanceOfType<MyPageRight>(descriptor.PageFactory!());
            Window window = new() { Width = 700d, Height = 600d, Content = page };
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.IsNotNull(page.FindControl<MyCard>("CardPluginDeveloperAuthorization"));
                Assert.IsNotNull(page.FindControl<MyCard>("CardPluginDeveloperOptions"));
                Assert.IsNotNull(page.FindControl<TextBox>("TextPluginDeveloperOrderNumber"));
                Assert.IsNotNull(page.FindControl<MyButton>("BtnPluginVerifyDeveloperOrder"));
                Assert.IsNotNull(page.FindControl<MyButton>("BtnPluginReplaceDeveloperAuthorization"));
                Assert.IsNotNull(page.FindControl<MyCheckBox>("CheckPluginDeveloperMode"));
                Assert.IsNotNull(page.FindControl<MyCheckBox>("CheckPluginDeveloperDiagnostics"));
                Assert.IsNull(page.FindControl<TextBlock>("LabHostHeading"));
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    [TestCategory("InjectedPlugin")]
    public void InjectedPlugin_VerifiedDeveloperAuthorizationSwitchesToExpiryAndReplacement()
    {
        bool pluginExpected = string.Equals(
            Environment.GetEnvironmentVariable("PCLN_EXPECT_PLUGIN_UI"),
            "1",
            StringComparison.Ordinal);
        if (!pluginExpected)
            return;

        Type loaderType = typeof(MainWindow).Assembly.GetType(
            "PCL.Desktop.Hosting.EmbeddedRuntimeExtensionLoader",
            throwOnError: true)!;
        System.Reflection.Assembly pluginAssembly = Assert.IsInstanceOfType<System.Reflection.Assembly>(
            loaderType.GetMethod(
                    "Load",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
                .Invoke(null, null));
        Type bootstrapType = pluginAssembly.GetType(
            "PCL.Plugin.Host.PluginPlatformBootstrap",
            throwOnError: true)!;
        Task? priorShutdown = bootstrapType.GetMethod("ShutdownAsync")!.Invoke(null, null) as Task;
        priorShutdown?.GetAwaiter().GetResult();

        string runtimeRoot = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "pcl-plugin-verified-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runtimeRoot);
        try
        {
            bootstrapType.GetMethod("Initialize")!.Invoke(null, [runtimeRoot]);
            object session = bootstrapType.GetField(
                    "_session",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .GetValue(null)!;
            Type grantType = pluginAssembly.GetType(
                "PCL.Plugin.Security.DeveloperAccess.DeveloperAccessGrant",
                throwOnError: true)!;
            object grant = Activator.CreateInstance(
                grantType,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic,
                binder: null,
                args:
                [
                    "headless-order-hash",
                    "54ee4e286b4211f1851c52540025c377",
                    "126.60",
                    DateTimeOffset.UtcNow.AddDays(-1),
                    null,
                    true,
                    DateTimeOffset.UtcNow,
                    "headless-signature"
                ],
                culture: null)!;
            session.GetType().GetField(
                    "_developerAccess",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(session, grant);

            IReadOnlyList<IPclHostModule> modules = Assert.IsInstanceOfType<IReadOnlyList<IPclHostModule>>(
                loaderType.GetMethod(
                        "LoadHostModules",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
                    .Invoke(null, null));
            PclHostBuilder builder = new();
            foreach (IPclHostModule module in modules)
                builder.AddModule(module);
            HostSettingsPageDescriptor descriptor = builder.Build().SettingsPages.Pages.Single(page =>
                page.Id == "pcl.plugin.developer");

            using SafeHeadlessUnitTestSession headless = CreateSession();
            headless.Dispatch(() =>
            {
                MyPageRight page = Assert.IsInstanceOfType<MyPageRight>(descriptor.PageFactory!());
                Window window = new() { Width = 700d, Height = 600d, Content = page };
                try
                {
                    window.Show();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    TextBox order = page.FindControl<TextBox>("TextPluginDeveloperOrderNumber")!;
                    MyButton verify = page.FindControl<MyButton>("BtnPluginVerifyDeveloperOrder")!;
                    MyButton replace = page.FindControl<MyButton>("BtnPluginReplaceDeveloperAuthorization")!;
                    TextBlock expiry = page.FindControl<TextBlock>("TextPluginDeveloperAuthorizationExpiry")!;
                    Assert.IsFalse(order.IsEffectivelyVisible);
                    Assert.IsFalse(verify.IsEffectivelyVisible);
                    Assert.IsTrue(replace.IsEffectivelyVisible);
                    Assert.AreEqual("授权到期时间：永久", expiry.Text);

                    Click(window, replace);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    Assert.IsTrue(order.IsEffectivelyVisible);
                    Assert.IsTrue(verify.IsEffectivelyVisible);
                    Assert.IsFalse(replace.IsEffectivelyVisible);
                    Assert.AreEqual("验证新订单", verify.Text);
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None);
        }
        finally
        {
            Task? shutdown = bootstrapType.GetMethod("ShutdownAsync")!.Invoke(null, null) as Task;
            shutdown?.GetAwaiter().GetResult();
            try
            {
                if (Directory.Exists(runtimeRoot))
                    Directory.Delete(runtimeRoot, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    [TestMethod]
    [TestCategory("InjectedPlugin")]
    public void InjectedPlugin_RegistersSettingsPageInHeadlessUi()
    {
        bool pluginExpected = string.Equals(
            Environment.GetEnvironmentVariable("PCLN_EXPECT_PLUGIN_UI"),
            "1",
            StringComparison.Ordinal);
        if (!pluginExpected)
            return;

        Type loaderType = typeof(MainWindow).Assembly.GetType(
            "PCL.Desktop.Hosting.EmbeddedRuntimeExtensionLoader",
            throwOnError: true)!;
        object? modules = loaderType.GetMethod(
                "LoadHostModules",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!
            .Invoke(null, null);
        Assert.IsNotNull(modules, "Embedded plugin modules must load before starting the UI thread.");

        using SafeHeadlessUnitTestSession session = CreateSession();
        session.Dispatch(() =>
        {
            PageSetupLeft setupLeft = new();
            ContentControl pageHost = new();
            setupLeft.PageChanged += (_, args) => pageHost.Content = args.Page;

            Grid testRoot = new()
            {
                ColumnDefinitions = new ColumnDefinitions("190,*")
            };
            Grid.SetColumn(setupLeft, 0);
            Grid.SetColumn(pageHost, 1);
            testRoot.Children.Add(setupLeft);
            testRoot.Children.Add(pageHost);

            Window window = new()
            {
                Width = 980d,
                Height = 720d,
                Content = testRoot
            };
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                TextBlock pluginGroup = setupLeft.FindControl<TextBlock>("TextHostSettingsGroup_pcl_plugin")!;
                Assert.IsNotNull(pluginGroup);
                Assert.AreEqual("插件", pluginGroup.Text);
                MyListItem installedItem = setupLeft.FindControl<MyListItem>("ItemHostSettings_pcl_plugin_installed")!;
                MyListItem marketItem = setupLeft.FindControl<MyListItem>("ItemHostSettings_pcl_plugin_market")!;
                MyListItem developerItem = setupLeft.FindControl<MyListItem>("ItemHostSettings_pcl_plugin_developer")!;
                Assert.AreEqual("已安装", installedItem.Title);
                Assert.AreEqual("市场", marketItem.Title);
                Assert.AreEqual("开发者模式", developerItem.Title);
                StackPanel navigation = setupLeft.FindControl<StackPanel>("PanItem")!;
                int miscIndex = navigation.Children.IndexOf(setupLeft.FindControl<MyListItem>("ItemLauncherMisc")!);
                int developerIndex = navigation.Children.IndexOf(developerItem);
                int aboutCategoryIndex = navigation.Children.IndexOf(setupLeft.FindControl<TextBlock>("TextAboutCategory")!);
                Assert.IsTrue(developerIndex > miscIndex && developerIndex < aboutCategoryIndex,
                    "开发者模式应位于启动器分类内、关于分类之前。");
                Assert.IsNull(setupLeft.FindControl<MyListItem>("ItemHostSettings_pcl_plugin_safety"));
                Assert.IsNull(setupLeft.FindControl<MyListItem>("ItemHostSettings_pcl_plugin_ui_patches"));
                Assert.IsNull(setupLeft.FindControl<MyListItem>("ItemHostSettings_pcl_plugin_compatibility"));

                Click(window, developerItem);
                ModAnimation.AdvanceUntilIdleForTesting();
                MyPageRight developerPage = FindVisual<MyPageRight>(window)!;
                Assert.IsNotNull(developerPage.FindControl<MyCard>("CardPluginDeveloperAuthorization"));
                Assert.IsNotNull(developerPage.FindControl<MyCard>("CardPluginDeveloperOptions"));
                Assert.IsNotNull(developerPage.FindControl<TextBox>("TextPluginDeveloperOrderNumber"));
                Assert.IsNotNull(developerPage.FindControl<MyButton>("BtnPluginVerifyDeveloperOrder"));
                Assert.IsNotNull(developerPage.FindControl<MyButton>("BtnPluginReplaceDeveloperAuthorization"));
                Assert.IsNotNull(developerPage.FindControl<MyCheckBox>("CheckPluginDeveloperMode"));
                Assert.IsNotNull(developerPage.FindControl<MyCheckBox>("CheckPluginDeveloperDiagnostics"));
                Assert.IsNotNull(developerPage.FindControl<MyCheckBox>("CheckPluginShowSafetyPage"));
                Assert.IsNotNull(developerPage.FindControl<MyCheckBox>("CheckPluginShowUiPatchesPage"));
                Assert.IsNotNull(developerPage.FindControl<MyCheckBox>("CheckPluginShowCompatibilityPage"));

                Click(window, marketItem);
                ModAnimation.AdvanceUntilIdleForTesting();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                MyPageRight marketPage = FindVisual<MyPageRight>(window)!;
                Assert.IsNotNull(marketPage.FindControl<MyCard>("CardPluginOnlineMarket"));
                Assert.IsNotNull(marketPage.FindControl<MyTextBox>("TextPluginMarketSearch"));
                Assert.IsNotNull(marketPage.FindControl<MyButton>("BtnPluginMarketSearch"));
                Assert.IsNotNull(marketPage.FindControl<MyButton>("BtnPluginMarketRefresh"));
                Assert.IsNotNull(marketPage.FindControl<StackPanel>("PanPluginOnlineMarketList"));
                Assert.IsNull(marketPage.FindControl<TextBlock>("LabHostHeading"));

                Click(window, installedItem);
                ModAnimation.AdvanceUntilIdleForTesting();

                MyPageRight page = FindVisual<MyPageRight>(window)!;
                Assert.IsNull(page.FindControl<TextBlock>("LabHostHeading"));
                Assert.IsNull(page.FindControl<TextBlock>("LabHostDescription"));
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageDownloadInstall_RestoresExistingLoaderBeforeAddingAddon()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageDownloadInstall page = new(
                new MinecraftVanillaInstallService(),
                new FakeMinecraftLoaderMetadataService(),
                new FakeMinecraftInstallAddonMetadataService());
            SetPrivateField(
                page,
                "_versions",
                new[]
                {
                    new MinecraftVersionManifestEntry("1.20.1", "release", "https://example.invalid/1.20.1.json", DateTimeOffset.Parse("2023-06-12T00:00:00Z"))
                });
            Window window = new() { Width = 620, Height = 520, Content = page };
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                page.FocusExistingInstallAddonAsync(
                        "1.20.1",
                        "Existing Fabric Pack",
                        @"D:\Games\.minecraft",
                        MinecraftLoaderKind.Fabric,
                        "0.16.10",
                        MinecraftInstallAddonKind.FabricApi)
                    .GetAwaiter()
                    .GetResult();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("0.16.10", page.FindControl<TextBlock>("LabFabric")!.Text);
                Assert.IsFalse(page.FindControl<MyCard>("CardFabricApi")!.IsSwapped);
                Assert.IsTrue(page.FindControl<StackPanel>("PanFabricApi")!.Children
                    .OfType<MyListItem>()
                    .Any(item => item.Title == "0.100.0+1.20.1"));
                Assert.AreEqual("Existing Fabric Pack", page.FindControl<MyTextBox>("TextSelectName")!.Text);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageDownloadInstall_PreloadsLoaderListsBeforeAllowingExpansion()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            DelayedMinecraftLoaderMetadataService metadata = new();
            PageDownloadInstall page = new(new MinecraftVanillaInstallService(), metadata);
            SetPrivateField(
                page,
                "_versions",
                new[]
                {
                    new MinecraftVersionManifestEntry("1.20.1", "release", "https://example.invalid/1.20.1.json", DateTimeOffset.Parse("2023-06-12T00:00:00Z"))
                });
            Window window = new()
            {
                Width = 620,
                Height = 520,
                Content = page
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                page.FocusVersionAsync("1.20.1").GetAwaiter().GetResult();

                MyCard fabricCard = page.FindControl<MyCard>("CardFabric")!;
                Assert.AreEqual(1, metadata.GetRequestCount(MinecraftLoaderKind.Fabric));
                Assert.AreEqual(1, metadata.GetRequestCount(MinecraftLoaderKind.Quilt));
                Assert.IsTrue(fabricCard.IsSwapped);
                Assert.IsFalse(fabricCard.MainSwap.IsVisible);
                Assert.AreEqual("正在获取版本列表", page.FindControl<TextBlock>("LabFabric")!.Text);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageSetupLog_ListsThePersistentDesktopSessionLog()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        DesktopFileLog.Write("headless persistent log marker");

        session.Dispatch(() =>
        {
            PageSetupLog page = new();
            Window window = new() { Width = 700, Height = 500, Content = page };
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                page.RefreshPage();

                Assert.IsTrue(File.Exists(DesktopFileLog.CurrentLogPath));
                Assert.IsTrue(File.ReadAllText(DesktopFileLog.CurrentLogPath)
                    .Contains("headless persistent log marker", StringComparison.Ordinal));
                Assert.IsTrue(page.FindControl<StackPanel>("PanList")!.Children
                    .OfType<MyListItem>()
                    .Any(item => item.Title.EndsWith(".log", StringComparison.OrdinalIgnoreCase)));
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void MainWindow_RepeatedMainNavigationRequestDoesNotReenterPage()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                int before = GetPrivateField<int>(window, "_registeredPageRequestId");
                NavigationRouteId settings = NavigationRouteId.Parse("pcl.settings");

                InvokePrivateMethod(window, "SelectNavPage", settings, true);
                InvokePrivateMethod(window, "SelectNavPage", settings, true);
                Assert.AreEqual(before, GetPrivateField<int>(window, "_registeredPageRequestId"));

                AdvancePageChangeAnimation(window);
                Assert.AreEqual(before + 1, GetPrivateField<int>(window, "_registeredPageRequestId"));
                Border leftHost = window.FindControl<Border>("PanMainLeft")!;
                Border rightHost = window.FindControl<Border>("PanMainRight")!;
                Control? left = leftHost.Child;
                Control? right = rightHost.Child;

                InvokePrivateMethod(window, "SelectNavPage", settings, true);
                Assert.AreEqual(before + 1, GetPrivateField<int>(window, "_registeredPageRequestId"));
                Assert.AreSame(left, leftHost.Child);
                Assert.AreSame(right, rightHost.Child);
                Assert.IsFalse(ModAnimation.AniIsRun("FrmMain PageChangeRight"));
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void MainWindow_InstanceSubpageSwitchDoesNotReplayLeftNavigationEntrance()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-subnav-" + Guid.NewGuid().ToString("N"));
        try
        {
            string versionDirectory = System.IO.Path.Combine(root, "versions", "1.20.1");
            Directory.CreateDirectory(versionDirectory);
            string jsonPath = System.IO.Path.Combine(versionDirectory, "1.20.1.json");
            File.WriteAllText(jsonPath, "{\"id\":\"1.20.1\"}");
            LaunchInstanceInfo instance = new("1.20.1", jsonPath, versionDirectory);

            session.Dispatch(() =>
            {
                MainWindow window = new();
                try
                {
                    window.Show();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    InvokePrivateMethod(window, "ApplyInstanceManagePage", instance, InstancePageSubType.Overall);
                    ModAnimation.AdvanceUntilIdleForTesting();

                    Border leftHost = window.FindControl<Border>("PanMainLeft")!;
                    MyPageLeft left = (MyPageLeft)leftHost.Child!;
                    int pageUuid = GetPrivateField<int>(left, "_uuid");
                    InvokePrivateMethod(window, "ApplyInstanceManagePage", instance, InstancePageSubType.Export);

                    Assert.AreSame(left, leftHost.Child);
                    Assert.IsFalse(ModAnimation.AniIsRun("PageLeft PageChange " + pageUuid));
                    Assert.IsInstanceOfType<PageInstanceExportRight>(window.FindControl<Border>("PanMainRight")!.Child);
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void MyButton_RedPaletteIsAppliedOnAttachBeforeFirstHover()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyButton button = new()
            {
                Text = "删除",
                ColorType = MyButton.ColorState.Red,
                Width = 130,
                Height = 36
            };
            Window window = new()
            {
                Width = 240,
                Height = 120,
                Content = new Border { Margin = new Thickness(20), Child = button }
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Border fore = button.FindControl<Border>("PanFore")!;
                TextBlock label = button.FindControl<TextBlock>("LabText")!;
                Assert.IsTrue(ModAnimation.AniIsRun("MyButton Color " + button.Uuid));
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual(RequiredBrush("ColorBrushRedDark").Color, ((SolidColorBrush)fore.BorderBrush!).Color);
                Assert.AreEqual(RequiredBrush("ColorBrushRedDark").Color, ((SolidColorBrush)label.Foreground!).Color);

                MoveTo(window, button);
                Assert.IsTrue(ModAnimation.AniIsRun("MyButton Color " + button.Uuid));
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual(RequiredBrush("ColorBrushRedLight").Color, ((SolidColorBrush)fore.BorderBrush!).Color);
                Assert.AreEqual(RequiredBrush("ColorBrushRedBack").Color, ((SolidColorBrush)fore.Background!).Color);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void MainWindow_TaskManagerReturnsToExactInstanceSubPage()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-task-back-instance-" + Guid.NewGuid().ToString("N"));
        string instanceDirectory = System.IO.Path.Combine(root, "versions", "CustomPack");
        string jsonPath = System.IO.Path.Combine(instanceDirectory, "CustomPack.json");

        try
        {
            Directory.CreateDirectory(instanceDirectory);
            File.WriteAllText(jsonPath, """{"id":"CustomPack"}""");

            session.Dispatch(() =>
            {
                MainWindow window = new();
                LaunchInstanceInfo instance = new("CustomPack", jsonPath, instanceDirectory);
                try
                {
                    window.Show();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    ModAnimation.AdvanceUntilIdleForTesting();

                    var applyInstance = typeof(MainWindow).GetMethod(
                        "ApplyInstanceManagePage",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        ?? throw new InvalidOperationException("ApplyInstanceManagePage was not found.");
                    applyInstance.Invoke(window, [instance, InstancePageSubType.Setup]);
                    Assert.IsNotNull(FindVisual<PageInstanceSetupRight>(window));

                    var applyTasks = typeof(MainWindow).GetMethod(
                        "ApplyTaskManagerPage",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        ?? throw new InvalidOperationException("ApplyTaskManagerPage was not found.");
                    applyTasks.Invoke(window, [false]);
                    Assert.IsNotNull(FindVisual<PageSpeedRight>(window));
                    Assert.AreEqual("任务管理", window.FindControl<TextBlock>("LabTitleInner")!.Text);

                    Click(window, window.FindControl<MyIconButton>("BtnTitleInner")!);
                    ModAnimation.AdvanceUntilIdleForTesting();

                    Assert.IsNotNull(FindVisual<PageInstanceSetupRight>(window));
                    Assert.AreEqual(InstancePageSubType.Setup, FindVisual<PageInstanceLeft>(window)!.PageId);
                    StringAssert.Contains(window.FindControl<TextBlock>("LabTitleInner")!.Text, "CustomPack");
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void MainWindow_FailedTaskCanReenterAndDismissFromTaskManager()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                ModAnimation.AdvanceUntilIdleForTesting();

                var trackFailed = typeof(MainWindow).GetMethod(
                    "TrackTaskFailed",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException("TrackTaskFailed was not found.");
                trackFailed.Invoke(window, ["failed:test", "测试失败任务", "网络连接失败", false]);
                ModAnimation.AdvanceUntilIdleForTesting();

                MyExtraButton taskButton = window.FindControl<MyExtraButton>("BtnExtraDownload")!;
                Assert.IsTrue(taskButton.Show);
                Click(window, taskButton);
                ModAnimation.AdvanceUntilIdleForTesting();

                PageSpeedRight taskPage = FindVisual<PageSpeedRight>(window)!;
                MyCard failedCard = taskPage.GetVisualDescendants().OfType<MyCard>()
                    .Single(card => card.Title == "测试失败任务");
                MyIconButton dismiss = failedCard.GetVisualDescendants().OfType<MyIconButton>()
                    .Single(button => Equals(button.ToolTip, "移除任务"));
                Click(window, dismiss);
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.IsNotNull(FindVisual<PageLaunchLeft>(window));
                Assert.IsFalse(taskButton.Show);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageSpeedPages_AllowProgressUpdatesFromBackgroundThread()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(async () =>
        {
            PageSpeedLeft left = new();
            PageSpeedRight right = new();
            Window window = new()
            {
                Width = 520,
                Height = 260,
                Content = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(200d, GridUnitType.Pixel),
                        new ColumnDefinition(1d, GridUnitType.Star)
                    },
                    Children =
                    {
                        left,
                        right
                    }
                }
            };
            Grid.SetColumn(right, 1);

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                await Task.Run(() =>
                {
                    left.UpdateSummary(new TaskManagerSummary(0.425d, 2048, 7, 2, 4));
                    right.UpsertTask(new TaskManagerEntrySnapshot(
                        "install:1.21.5",
                        "安装 1.21.5",
                        "下载版本描述",
                        "1.21.5.json",
                        0.425d,
                        3,
                        10,
                        2048,
                        TaskManagerTaskState.Running,
                        Steps:
                        [
                            new TaskManagerSubTaskSnapshot("下载版本描述", "1.21.5.json", 0.425d, TaskManagerTaskState.Running),
                            new TaskManagerSubTaskSnapshot("下载运行库", "3 / 10 个文件", 0.3d, TaskManagerTaskState.Waiting)
                        ]));
                });

                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                string[] leftText = left.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Select(block => block.Text ?? string.Empty)
                    .ToArray();
                string[] rightText = right.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Select(block => block.Text ?? string.Empty)
                    .ToArray();

                Assert.IsTrue(leftText.Any(text => text.StartsWith("42", StringComparison.Ordinal)));
                CollectionAssert.IsSubsetOf(
                    new[]
                    {
                        AvaloniaLocalizationManager.GetText("Speed.Progress.Total", "总进度"),
                        AvaloniaLocalizationManager.GetText("Speed.Progress.Speed", "下载速度"),
                        AvaloniaLocalizationManager.GetText("Speed.Progress.RemainingFiles", "剩余文件"),
                        AvaloniaLocalizationManager.GetText("Speed.Progress.RemainingThreads", "剩余线程")
                    },
                    leftText);
                Assert.IsTrue(leftText.Contains("2.0 KB/s"));
                Assert.IsTrue(leftText.Contains("7"));
                Assert.IsTrue(leftText.Contains("2 / 4"));
                Assert.IsTrue(rightText.Any(text => text.Contains("下载版本描述", StringComparison.Ordinal)));
                Assert.IsTrue(rightText.Any(text => text.Contains("1.21.5.json", StringComparison.Ordinal)));
                Assert.IsTrue(rightText.Any(text => text.Contains("下载运行库", StringComparison.Ordinal)));
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public async Task MainWindow_CreateLaunchPlanAppliesInstanceAndGlobalLaunchSettings()
    {
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-desktop-launch-plan-" + Guid.NewGuid().ToString("N"));
        string settingsPath = System.IO.Path.Combine(root, "launcher-settings.json");
        string? previousOverride = Environment.GetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH");

        try
        {
            Environment.SetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH", settingsPath);
            string instanceDirectory = System.IO.Path.Combine(root, ".minecraft", "versions", "CustomPack");
            string versionJsonPath = System.IO.Path.Combine(instanceDirectory, "CustomPack.json");
            string versionJarPath = System.IO.Path.Combine(instanceDirectory, "CustomPack.jar");
            string headJar = System.IO.Path.Combine(root, "head.jar");
            Directory.CreateDirectory(instanceDirectory);
            await File.WriteAllTextAsync(
                versionJsonPath,
                """
                {
                  "mainClass": "net.minecraft.client.main.Main",
                  "releaseTime": "2024-01-01T00:00:00Z",
                  "arguments": {
                    "jvm": ["-cp", "${classpath}"],
                    "game": [
                      "--username", "${auth_player_name}",
                      "--gameDir", "${game_directory}",
                      "--versionType", "${version_type}",
                      "--width", "${resolution_width}",
                      "--height", "${resolution_height}"
                    ]
                  }
                }
                """);
            await File.WriteAllTextAsync(versionJarPath, string.Empty);

            using (LauncherSettingsStore store = new(settingsPath))
            {
                await store.SaveAsync(
                    new LauncherSettings
                    {
                        IntegerOptions = new Dictionary<string, int>
                        {
                            ["LaunchArgumentWindowType"] = 3,
                            ["LaunchPreferredIpStack"] = 2,
                            ["LaunchRamType"] = 1,
                            ["LaunchRamCustom"] = 20,
                            ["SystemHttpProxyType"] = 2
                        },
                        TextOptions = new Dictionary<string, string>
                        {
                            ["LaunchArgumentWindowWidth"] = "1280",
                            ["LaunchArgumentWindowHeight"] = "720",
                            ["LaunchAdvanceJvm"] = "-Dglobal=true",
                            ["LaunchAdvanceGame"] = "--global",
                            ["SystemHttpProxy"] = "http://127.0.0.1:7890"
                        }
                    });
            }

            await InstanceMetadataStore.SaveAsync(
                instanceDirectory,
                new InstanceMetadata
                {
                    InstanceIsolation = true,
                    MemorySolution = 1,
                    CustomMemorySize = 13,
                    JvmArguments = "-Dinstance=true",
                    GameArguments = "--instance",
                    ClasspathHead = headJar,
                    ServerToEnter = "play.example.com",
                    CustomInfo = "Fabric 测试实例",
                    UseProxy = true
                });

            LaunchInstanceInfo instance = new("CustomPack", versionJsonPath, instanceDirectory);
            LoginProfileInfo profile = new("Steve", "离线档案", LaunchLoginProfileKind.Offline, "Steve");
            var method = typeof(MainWindow).GetMethod(
                "CreateLaunchPlanAsync",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("CreateLaunchPlanAsync was not found.");

            var task = (Task<MinecraftProcessLaunchPlan>)method.Invoke(
                null,
                [instance, profile, "java", CancellationToken.None, null, null, null])!;
            MinecraftProcessLaunchPlan plan = await task;

            Assert.AreEqual(instanceDirectory, plan.StartInfo.WorkingDirectory);
            StringAssert.Contains(plan.StartInfo.Arguments, "-Xmx2048m");
            StringAssert.Contains(plan.StartInfo.Arguments, "-Dinstance=true");
            StringAssert.Contains(plan.StartInfo.Arguments, "-Dhttp.proxyHost=127.0.0.1");
            StringAssert.Contains(plan.StartInfo.Arguments, "-Dhttp.proxyPort=7890");
            StringAssert.Contains(plan.StartInfo.Arguments, "-Djava.net.preferIPv6Stack=true");
            StringAssert.Contains(plan.StartInfo.Arguments, "--instance");
            StringAssert.Contains(plan.StartInfo.Arguments, "--versionType \"Fabric 测试实例\"");
            StringAssert.Contains(plan.StartInfo.Arguments, "--quickPlayMultiplayer \"play.example.com\"");
            StringAssert.Contains(plan.StartInfo.Arguments, "--width 1280");
            StringAssert.Contains(plan.StartInfo.Arguments, "--height 720");
            CollectionAssert.AreEqual(new[] { headJar, versionJarPath }, plan.ClasspathEntries.ToArray());
        }
        finally
        {
            Environment.SetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH", previousOverride);
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceSelectRight_UsesWpfSearchEmptyAndCardStructure()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-select-" + Guid.NewGuid().ToString("N"));
        string selectedDirectory = System.IO.Path.Combine(root, "versions", "1.20.1");
        string secondDirectory = System.IO.Path.Combine(root, "versions", "1.21");

        try
        {
            Directory.CreateDirectory(selectedDirectory);
            Directory.CreateDirectory(secondDirectory);
            File.WriteAllText(System.IO.Path.Combine(selectedDirectory, "1.20.1.json"), "{}" );
            File.WriteAllText(System.IO.Path.Combine(secondDirectory, "1.21.json"), "{}" );

            session.Dispatch(() =>
            {
                PageInstanceSelectRight page = new();
                LaunchInstanceInfo selected = new("1.20.1", System.IO.Path.Combine(selectedDirectory, "1.20.1.json"), selectedDirectory);
                LaunchInstanceInfo second = new("1.21", System.IO.Path.Combine(secondDirectory, "1.21.json"), secondDirectory);
                Window window = new()
                {
                    Width = 560,
                    Height = 420,
                    Content = page
                };
                LaunchInstanceInfo? chosen = null;
                page.InstanceSelected += (_, instance) => chosen = instance;

                try
                {
                    window.Show();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    page.SetInstances([selected, second], selected);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Assert.IsNotNull(page.FindControl<MySearchBox>("PanVerSearchBox"));
                    Assert.IsNotNull(page.FindControl<MyCard>("PanEmpty"));
                    Assert.IsNotNull(page.FindControl<MyCard>("PanEmptySearch"));
                    Assert.IsFalse(page.FindControl<MyCard>("PanEmpty")!.IsVisible);
                    Assert.AreEqual("常规版本 (2)", page.GetVisualDescendants().OfType<MyCard>().First(card => card.Title.StartsWith("常规版本", StringComparison.Ordinal)).Title);

                    MyListItem item = page.GetVisualDescendants().OfType<MyListItem>().Single(listItem => listItem.Title == "1.21");
                    Assert.AreEqual("avares://PCL.Desktop/Assets/Legacy/Blocks/Grass.png", item.Logo);
                    Assert.AreEqual("1.21", DisplayText(item.FindControl<TextBlock>("LabTitle")!));
                    Click(window, item);

                    Assert.AreEqual("1.21", chosen?.Name);
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceSelectLeft_ListsMinecraftRootsAndRaisesSelection()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageInstanceSelectLeft page = new();
            MinecraftFolderInfo first = new("主目录", @"D:\Minecraft-A");
            MinecraftFolderInfo second = new("测试目录", @"D:\Minecraft-B", IsCustom: true);
            MinecraftFolderInfo? selected = null;
            page.FolderSelected += (_, folder) => selected = folder;
            page.SetFolders([first, second], second.RootDirectory);

            MyListItem[] folders = page.FindControl<StackPanel>("PanList")!.Children
                .OfType<MyListItem>()
                .Where(item => item.Tag is MinecraftFolderInfo)
                .ToArray();
            Assert.AreEqual(2, folders.Length);
            Assert.IsFalse(folders[0].Checked);
            Assert.IsTrue(folders[1].Checked);
            Assert.AreEqual(4, folders[1].Buttons.Count);

            Assert.IsTrue(page.TrySelectFolder(first));
            Assert.AreSame(first, selected);
            Assert.AreEqual(first.RootDirectory, page.SelectedRootDirectory);
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MainWindow_InstanceSelectUsesFolderLeftPageAndScopesDiscovery()
    {
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-folder-select-" + Guid.NewGuid().ToString("N"));
        string firstRoot = System.IO.Path.Combine(root, "Minecraft-A");
        string secondRoot = System.IO.Path.Combine(root, "Minecraft-B");
        string? previousRoots = Environment.GetEnvironmentVariable("PCLN_MINECRAFT_ROOTS");

        try
        {
            CreateDiscoveredInstance(firstRoot, "First");
            CreateDiscoveredInstance(secondRoot, "Second");
            Environment.SetEnvironmentVariable("PCLN_MINECRAFT_ROOTS", string.Join(System.IO.Path.PathSeparator, firstRoot, secondRoot));
            using SafeHeadlessUnitTestSession session = CreateSession();

            session.Dispatch(async () =>
            {
                MainWindow window = new();
                try
                {
                    window.Show();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    ModAnimation.AdvanceUntilIdleForTesting();
                    PageLaunchLeft launchPage = FindVisual<PageLaunchLeft>(window)!;
                    await launchPage.EnsureInstancesLoadedAsync().ConfigureAwait(true);

                    Click(window, launchPage.FindControl<MyButton>("BtnInstance")!);
                    ModAnimation.AdvanceUntilIdleForTesting();

                    PageInstanceSelectLeft left = FindVisual<PageInstanceSelectLeft>(window)!;
                    PageInstanceSelectRight right = FindVisual<PageInstanceSelectRight>(window)!;
                    Assert.IsNotNull(left);
                    Assert.IsTrue(right.GetVisualDescendants().OfType<MyListItem>().Any(item => item.Title == "First"));

                    MyListItem secondFolder = left.FindControl<StackPanel>("PanList")!.Children
                        .OfType<MyListItem>()
                        .Single(item => item.Tag is MinecraftFolderInfo folder &&
                            string.Equals(folder.RootDirectory, secondRoot, StringComparison.OrdinalIgnoreCase));
                    Click(window, secondFolder);
                    await WaitForConditionAsync(() =>
                        right.GetVisualDescendants().OfType<MyListItem>().Any(item => item.Title == "Second"));

                    Assert.IsFalse(right.GetVisualDescendants().OfType<MyListItem>().Any(item => item.Title == "First"));
                    string settingsPath = Environment.GetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH")!;
                    using LauncherSettingsStore settingsStore = new(settingsPath);
                    LauncherSettings settings = (await settingsStore.LoadAsync().ConfigureAwait(true)).Settings;
                    Assert.AreEqual(
                        System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(secondRoot)),
                        settings.GetTextOption(LauncherSettingKeys.LaunchSelectedMinecraftRoot));
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            Environment.SetEnvironmentVariable("PCLN_MINECRAFT_ROOTS", previousRoots);
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceSelectRight_InvalidJsonOpensFolderInsteadOfSelectingOrManaging()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-invalid-" + Guid.NewGuid().ToString("N"));
        string instanceDirectory = System.IO.Path.Combine(root, "versions", "Broken");
        string jsonPath = System.IO.Path.Combine(instanceDirectory, "Broken.json");

        try
        {
            Directory.CreateDirectory(instanceDirectory);
            File.WriteAllText(jsonPath, "{ this is not json }");

            session.Dispatch(() =>
            {
                PageInstanceSelectRight page = new();
                LaunchInstanceInfo broken = new("Broken", jsonPath, instanceDirectory);
                LaunchInstanceInfo? selected = null;
                LaunchInstanceInfo? opened = null;
                LaunchInstanceInfo? managed = null;
                page.InstanceSelected += (_, instance) => selected = instance;
                page.InstanceOpenFolderRequested += (_, instance) => opened = instance;
                page.InstanceManageRequested += (_, instance) => managed = instance;
                page.SetInstances([broken], null);

                Assert.IsFalse(page.TrySelectInstance(broken));
                Assert.IsNull(selected);
                Assert.AreSame(broken, opened);
                Assert.IsNull(managed);

                MyListItem item = page.GetVisualDescendants().OfType<MyListItem>().Single(listItem => listItem.Title == "Broken");
                Assert.AreEqual("avares://PCL.Desktop/Assets/Legacy/Blocks/RedstoneBlock.png", item.Logo);
                Assert.AreEqual("lucide/folder-open", item.Buttons.Last().SvgIcon);
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceSelectRight_InfersLoaderIconFromVersionJson()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-loader-icon-" + Guid.NewGuid().ToString("N"));
        string versionDirectory = System.IO.Path.Combine(root, "versions", "fabric-1.20.1");
        string jsonPath = System.IO.Path.Combine(versionDirectory, "fabric-1.20.1.json");

        try
        {
            Directory.CreateDirectory(versionDirectory);
            File.WriteAllText(
                jsonPath,
                """
                {
                  "id": "fabric-loader-0.15.11-1.20.1",
                  "inheritsFrom": "1.20.1",
                  "mainClass": "net.fabricmc.loader.impl.launch.knot.KnotClient",
                  "libraries": [
                    { "name": "net.fabricmc:fabric-loader:0.15.11" }
                  ]
                }
                """);

            session.Dispatch(() =>
            {
                PageInstanceSelectRight page = new();
                LaunchInstanceInfo instance = new("fabric-1.20.1", jsonPath, versionDirectory);
                Window window = new()
                {
                    Width = 560,
                    Height = 420,
                    Content = page
                };

                try
                {
                    window.Show();
                    page.SetInstances([instance], instance);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    MyListItem item = page.GetVisualDescendants().OfType<MyListItem>().Single(listItem => listItem.Title == "fabric-1.20.1");
                    Assert.IsTrue(item.Logo.EndsWith("Fabric.png", StringComparison.OrdinalIgnoreCase));
                    Assert.AreEqual("fabric-1.20.1", DisplayText(item.FindControl<TextBlock>("LabTitle")!));
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceSelectRight_UsesWpfBlockIconsForVersionStates()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-state-icons-" + Guid.NewGuid().ToString("N"));

        try
        {
            (string Name, string Json, string Icon)[] cases =
            [
                ("1.21", """{"id":"1.21","type":"release"}""", "Grass.png"),
                ("24w13a", """{"id":"24w13a","type":"snapshot"}""", "CommandBlock.png"),
                ("b1.7.3", """{"id":"b1.7.3","type":"old_beta"}""", "CobbleStone.png"),
                ("24w14potato", """{"id":"24w14potato","type":"snapshot"}""", "GoldBlock.png"),
                ("OptiFine_1.20.1", """{"id":"OptiFine_1.20.1","inheritsFrom":"1.20.1","libraries":[{"name":"optifine:OptiFine:1.20.1_HD_U_I6"}]}""", "GrassPath.png"),
                ("ForgePack", """{"id":"ForgePack","inheritsFrom":"1.20.1","libraries":[{"name":"net.minecraftforge:forge:1.20.1-47.2.0"}]}""", "Anvil.png"),
                ("NeoForgePack", """{"id":"NeoForgePack","inheritsFrom":"1.20.1","libraries":[{"name":"net.neoforged:neoforge:20.6.119-beta"}]}""", "NeoForge.png"),
                ("LiteLoaderPack", """{"id":"LiteLoaderPack","inheritsFrom":"1.12.2","libraries":[{"name":"com.mumfrey:liteloader:1.12.2"}]}""", "Egg.png"),
                ("LabyModPack", """{"id":"LabyModPack","inheritsFrom":"1.20.1","labymod_data":{"version":"4.2.0"}}""", "LabyMod.png")
            ];

            List<LaunchInstanceInfo> instances = [];
            foreach ((string name, string json, _) in cases)
            {
                string versionDirectory = System.IO.Path.Combine(root, "versions", name);
                Directory.CreateDirectory(versionDirectory);
                string jsonPath = System.IO.Path.Combine(versionDirectory, name + ".json");
                File.WriteAllText(jsonPath, json);
                instances.Add(new LaunchInstanceInfo(name, jsonPath, versionDirectory));
            }

            session.Dispatch(() =>
            {
                PageInstanceSelectRight page = new();
                Window window = new()
                {
                    Width = 620,
                    Height = 520,
                    Content = page
                };

                try
                {
                    window.Show();
                    page.SetInstances(instances, null);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    foreach ((string name, _, string icon) in cases)
                    {
                        MyListItem item = page.GetVisualDescendants().OfType<MyListItem>().Single(listItem => listItem.Title == name);
                        StringAssert.EndsWith(item.Logo, icon);
                    }
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceManageRight_SavesPersonalizationSelections()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-personalization-" + Guid.NewGuid().ToString("N"));
        string versionDirectory = System.IO.Path.Combine(root, "versions", "1.20.1");
        string jsonPath = System.IO.Path.Combine(versionDirectory, "1.20.1.json");
        PageInstanceManageRight? page = null;
        Window? window = null;

        try
        {
            Directory.CreateDirectory(versionDirectory);
            File.WriteAllText(jsonPath, "{\"id\":\"1.20.1\"}");
            LaunchInstanceInfo instance = new("1.20.1", jsonPath, versionDirectory);
            InstanceMetadataStore.SaveAsync(
                versionDirectory,
                new InstanceMetadata
                {
                    Description = "Fabric 整合包",
                    CardType = 2,
                    LogoPath = "pack://application:,,,/images/Blocks/Fabric.png"
                }).GetAwaiter().GetResult();

            session.Dispatch(() =>
            {
                page = new PageInstanceManageRight();
                window = new Window
                {
                    Width = 720,
                    Height = 520,
                    Content = page
                };
                window.Show();
                page.SetInstance(instance);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual(2, page.FindControl<MyComboBox>("ComboDisplayType")!.SelectedIndex);
                Assert.IsTrue(((MyComboBoxItem)page.FindControl<MyComboBox>("ComboDisplayLogo")!.SelectedItem!).Tag?.ToString()
                    ?.EndsWith("Fabric.png", StringComparison.OrdinalIgnoreCase));

                MyListItem displayItem = page.GetVisualDescendants().OfType<MyListItem>().First(item => item.Title == "1.20.1");
                Assert.AreEqual("Fabric 整合包", displayItem.Info);
                Assert.IsTrue(displayItem.Logo.EndsWith("Fabric.png", StringComparison.OrdinalIgnoreCase));
                StringAssert.StartsWith(displayItem.Logo, "avares://PCL.Desktop/Assets/Legacy/Blocks/");

                MyComboBox logoCombo = page.FindControl<MyComboBox>("ComboDisplayLogo")!;
                MyComboBoxItem quilt = logoCombo.Items.OfType<MyComboBoxItem>().First(item =>
                    item.Tag?.ToString()?.EndsWith("Quilt.png", StringComparison.OrdinalIgnoreCase) == true);
                logoCombo.SelectedItem = quilt;
                page.FindControl<MyComboBox>("ComboDisplayType")!.SelectedIndex = 4;
            }, CancellationToken.None).GetAwaiter().GetResult();

            page!.WaitForPendingMetadataWritesAsync().GetAwaiter().GetResult();
            InstanceMetadata savedMetadata = InstanceMetadataStore.LoadAsync(versionDirectory).GetAwaiter().GetResult();
            Assert.AreEqual(4, savedMetadata.CardType);
            Assert.IsTrue(savedMetadata.LogoPath.EndsWith("Quilt.png", StringComparison.OrdinalIgnoreCase));

            session.Dispatch(() =>
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                MyListItem displayItem = page!.GetVisualDescendants().OfType<MyListItem>().First(item => item.Title == "1.20.1");
                Assert.IsTrue(displayItem.Logo.EndsWith("Quilt.png", StringComparison.OrdinalIgnoreCase));
            }, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            if (window is not null)
            {
                session.Dispatch(() => window.Close(), CancellationToken.None).GetAwaiter().GetResult();
            }

            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceSelectRight_UsesInstanceMetadataCardsAndIcons()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-select-metadata-" + Guid.NewGuid().ToString("N"));

        try
        {
            string fabricDirectory = System.IO.Path.Combine(root, "versions", "FabricPack");
            string starDirectory = System.IO.Path.Combine(root, "versions", "StarPack");
            Directory.CreateDirectory(fabricDirectory);
            Directory.CreateDirectory(starDirectory);
            string fabricJson = System.IO.Path.Combine(fabricDirectory, "FabricPack.json");
            string starJson = System.IO.Path.Combine(starDirectory, "StarPack.json");
            File.WriteAllText(fabricJson, "{\"id\":\"FabricPack\"}");
            File.WriteAllText(starJson, "{\"id\":\"StarPack\"}");

            InstanceMetadataStore.SaveAsync(
                fabricDirectory,
                new InstanceMetadata
                {
                    Description = "Fabric 整合包",
                    CardType = 2,
                    LogoPath = "avares://PCL.Desktop/Assets/Legacy/Blocks/Fabric.png"
                }).GetAwaiter().GetResult();
            InstanceMetadataStore.SaveAsync(
                starDirectory,
                new InstanceMetadata
                {
                    IsStarred = true,
                    LogoPath = "avares://PCL.Desktop/Assets/Legacy/Blocks/GoldBlock.png"
                }).GetAwaiter().GetResult();

            session.Dispatch(async () =>
            {
                PageInstanceSelectRight page = new();
                Window window = new()
                {
                    Width = 560,
                    Height = 420,
                    Content = page
                };

                window.Show();
                page.SetInstances(
                    [
                        new LaunchInstanceInfo("FabricPack", fabricJson, fabricDirectory),
                        new LaunchInstanceInfo("StarPack", starJson, starDirectory)
                    ],
                    null);
                await page.ReloadMetadataAsync().ConfigureAwait(true);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                await WaitForConditionAsync(() =>
                    page.GetVisualDescendants().OfType<MyCard>().Any(card => card.Title == "收藏夹"))
                    .ConfigureAwait(true);

                try
                {
                    Assert.IsTrue(page.GetVisualDescendants().OfType<MyCard>().Any(card => card.Title == "收藏夹"));
                    Assert.IsTrue(page.GetVisualDescendants().OfType<MyCard>().Any(card => card.Title == "可安装 Mod (1)"));

                    MyListItem fabricItem = page.GetVisualDescendants().OfType<MyListItem>().Single(item => item.Title == "FabricPack");
                    Assert.AreEqual("Fabric 整合包", fabricItem.Info);
                    Assert.IsTrue(fabricItem.Logo.EndsWith("Fabric.png", StringComparison.OrdinalIgnoreCase));
                    MyListItem starItem = page.GetVisualDescendants().OfType<MyListItem>().Single(item => item.Title == "StarPack");
                    Assert.IsTrue(starItem.Logo.EndsWith("GoldBlock.png", StringComparison.OrdinalIgnoreCase));
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceSelectRight_FollowsWpfHiddenAndCollapsedCardRules()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-select-rules-" + Guid.NewGuid().ToString("N"));

        try
        {
            (string Name, int CardType)[] cases =
            [
                ("RegularPack", 0),
                ("HiddenPack", 1),
                ("LessUsedPack", 4),
                ("FoolPack", 5)
            ];
            List<LaunchInstanceInfo> instances = [];
            foreach ((string name, int cardType) in cases)
            {
                string directory = System.IO.Path.Combine(root, "versions", name);
                Directory.CreateDirectory(directory);
                string json = System.IO.Path.Combine(directory, name + ".json");
                File.WriteAllText(json, "{\"id\":\"" + name + "\"}");
                InstanceMetadataStore.SaveAsync(directory, new InstanceMetadata { CardType = cardType }).GetAwaiter().GetResult();
                instances.Add(new LaunchInstanceInfo(name, json, directory));
            }

            session.Dispatch(async () =>
            {
                PageInstanceSelectRight page = new();
                Window window = new()
                {
                    Width = 620,
                    Height = 520,
                    Content = page
                };
                window.Show();
                page.SetInstances(instances, null);
                await page.ReloadMetadataAsync().ConfigureAwait(true);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                await WaitForConditionAsync(() =>
                    page.GetVisualDescendants().OfType<MyListItem>().Any(item => item.Title == "RegularPack"))
                    .ConfigureAwait(true);

                try
                {
                    Assert.IsNotNull(page.GetVisualDescendants().OfType<MyListItem>().SingleOrDefault(item => item.Title == "RegularPack"));
                    Assert.IsNull(page.GetVisualDescendants().OfType<MyListItem>().SingleOrDefault(item => item.Title == "HiddenPack"));
                    Assert.IsTrue(page.GetVisualDescendants().OfType<MyCard>().Single(card => card.Title == "不常用版本 (1)").IsSwapped);
                    Assert.IsTrue(page.GetVisualDescendants().OfType<MyCard>().Single(card => card.Title == "愚人节版本 (1)").IsSwapped);

                    page.ShowHidden = true;
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    Assert.IsNull(page.GetVisualDescendants().OfType<MyListItem>().SingleOrDefault(item => item.Title == "RegularPack"));
                    Assert.IsNotNull(page.GetVisualDescendants().OfType<MyListItem>().SingleOrDefault(item => item.Title == "HiddenPack"));
                    Assert.IsTrue(page.GetVisualDescendants().OfType<MyCard>().Any(card => card.Title == "隐藏版本 (1)"));
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void MainWindow_SelectedInstanceSurvivesRefreshAndWindowRecreation()
    {
        string root = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "pcl-selected-instance-" + Guid.NewGuid().ToString("N"));
        string minecraftRoot = System.IO.Path.Combine(root, ".minecraft");
        string settingsPath = System.IO.Path.Combine(root, "launcher-settings.json");
        string? previousRoots = Environment.GetEnvironmentVariable("PCLN_MINECRAFT_ROOTS");
        string? previousSettings = Environment.GetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH");

        try
        {
            string firstDirectory = CreateDiscoveredInstance(minecraftRoot, "FirstPack");
            string selectedDirectory = CreateDiscoveredInstance(minecraftRoot, "SelectedPack");
            Directory.SetLastWriteTimeUtc(firstDirectory, DateTime.UtcNow.AddMinutes(1));
            Directory.SetLastWriteTimeUtc(selectedDirectory, DateTime.UtcNow);
            Environment.SetEnvironmentVariable("PCLN_MINECRAFT_ROOTS", minecraftRoot);
            Environment.SetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH", settingsPath);

            using SafeHeadlessUnitTestSession session = CreateSession();
            session.Dispatch(async () =>
            {
                MainWindow firstWindow = new();
                try
                {
                    firstWindow.Show();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    ModAnimation.AdvanceUntilIdleForTesting();

                    PageLaunchLeft firstLaunchPage = FindVisual<PageLaunchLeft>(firstWindow)!;
                    await firstLaunchPage.EnsureInstancesLoadedAsync().ConfigureAwait(true);
                    LaunchInstanceInfo firstInstance = new(
                        "FirstPack",
                        System.IO.Path.Combine(firstDirectory, "FirstPack.json"),
                        firstDirectory);
                    LaunchInstanceInfo selectedInstance = new(
                        "SelectedPack",
                        System.IO.Path.Combine(selectedDirectory, "SelectedPack.json"),
                        selectedDirectory);
                    firstLaunchPage.SetInstances([firstInstance, selectedInstance], firstInstance);

                    Click(firstWindow, firstLaunchPage.FindControl<MyButton>("BtnInstance")!);
                    ModAnimation.AdvanceUntilIdleForTesting();
                    PageInstanceSelectRight selectPage = FindVisual<PageInstanceSelectRight>(firstWindow)!;
                    Assert.IsTrue(selectPage.TrySelectInstance(selectedInstance));
                    ModAnimation.AdvanceUntilIdleForTesting();

                    Assert.AreEqual("SelectedPack", firstLaunchPage.SelectedInstance?.Name);
                    await firstLaunchPage.RefreshInstancesAsync().ConfigureAwait(true);
                    Assert.AreEqual("SelectedPack", firstLaunchPage.SelectedInstance?.Name);
                }
                finally
                {
                    firstWindow.Close();
                }

                using (LauncherSettingsStore store = new(settingsPath))
                {
                    LauncherSettings saved = (await store.LoadAsync().ConfigureAwait(true)).Settings;
                    Assert.AreEqual(
                        System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(selectedDirectory)),
                        saved.GetTextOption(LauncherSettingKeys.LaunchSelectedInstanceDirectory));
                }

                MainWindow secondWindow = new();
                try
                {
                    secondWindow.Show();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    ModAnimation.AdvanceUntilIdleForTesting();

                    PageLaunchLeft secondLaunchPage = FindVisual<PageLaunchLeft>(secondWindow)!;
                    await secondLaunchPage.EnsureInstancesLoadedAsync().ConfigureAwait(true);

                    Assert.AreEqual("SelectedPack", secondLaunchPage.SelectedInstance?.Name);
                    Assert.AreEqual(
                        System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(selectedDirectory)),
                        secondLaunchPage.PreferredInstanceDirectory);
                }
                finally
                {
                    secondWindow.Close();
                }
            }, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            Environment.SetEnvironmentVariable("PCLN_MINECRAFT_ROOTS", previousRoots);
            Environment.SetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH", previousSettings);
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void SettingsPages_LoadAndPersistTaggedWpfControls()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-settings-pages-" + Guid.NewGuid().ToString("N"));
        string settingsPath = System.IO.Path.Combine(root, "launcher-settings.json");
        string? previousOverride = Environment.GetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH");
        Window? settingsWindow = null;

        try
        {
            Directory.CreateDirectory(root);
            Environment.SetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH", settingsPath);
            using (LauncherSettingsStore store = new(settingsPath))
            {
                store.SaveAsync(new LauncherSettings
                {
                    AutomaticallyRepairGameIssues = false,
                    ColorMode = ColorMode.Dark,
                    LightColor = ColorTheme.CatBlue,
                    DarkColor = ColorTheme.DeathBlue,
                    DownloadSource = DownloadSourcePreference.OfficialOnly,
                    IntegerOptions = new Dictionary<string, int>
                    {
                        ["LaunchRamType"] = 1,
                        ["LaunchRamCustom"] = 20,
                        ["SystemUpdateMode"] = 3
                    },
                    TextOptions = new Dictionary<string, string>
                    {
                        ["LaunchArgumentInfo"] = "PCL N"
                    }
                }).AsTask().GetAwaiter().GetResult();
            }

            session.Dispatch(() =>
            {
                PageSetupUI ui = new();
                PageSetupLaunch launch = new();
                PageSetupGameManage gameManage = new();
                PageSetupUpdate update = new();

                settingsWindow = new Window
                {
                    Width = 900,
                    Height = 640,
                    Content = new StackPanel
                    {
                        Children = { ui, launch, gameManage, update }
                    }
                };

                settingsWindow.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual(1, ui.FindControl<MyComboBox>("ComboDarkMode")!.SelectedIndex);
                Assert.IsNull(ui.FindControl<MyCard>("CardSwitch"));
                Assert.AreEqual(1, ui.FindControl<MyComboBox>("ComboLightColor")!.SelectedIndex);
                Assert.AreEqual(2, ui.FindControl<MyComboBox>("ComboDarkColor")!.SelectedIndex);
                Assert.AreEqual(2, gameManage.FindControl<MyComboBox>("ComboDownloadSource")!.SelectedIndex);
                Assert.IsFalse(launch.FindControl<MyCheckBox>("CheckAutoRepairGame")!.Checked);
                Assert.IsTrue(launch.FindControl<MyRadioBox>("RadioRamType1")!.Checked);
                Assert.AreEqual(20, launch.FindControl<MySlider>("SliderRamCustom")!.Value);
                Assert.AreEqual("PCL N", launch.FindControl<MyTextBox>("TextArgumentInfo")!.Text);
                Assert.AreEqual(3, update.FindControl<MyComboBox>("ComboSystemUpdateMode")!.SelectedIndex);

                ui.FindControl<MyComboBox>("ComboDarkMode")!.SelectedIndex = 0;
                using (LauncherSettingsStore store = new(settingsPath))
                {
                    LauncherSettings afterDarkMode = store.LoadAsync().AsTask().GetAwaiter().GetResult().Settings;
                    Assert.AreEqual(ColorMode.Light, afterDarkMode.ColorMode);
                }

                ui.FindControl<MyComboBox>("ComboLightColor")!.SelectedIndex = 2;
                gameManage.FindControl<MyComboBox>("ComboDownloadSource")!.SelectedIndex = 0;
                launch.FindControl<MyCheckBox>("CheckAutoRepairGame")!.Checked = true;
                launch.FindControl<MySlider>("SliderRamCustom")!.Value = 23;
                launch.FindControl<MyTextBox>("TextArgumentInfo")!.Text = "新的标识";
                Assert.AreEqual(0, ui.FindControl<MyComboBox>("ComboDarkMode")!.SelectedIndex);

                LauncherSettings saved;
                using (LauncherSettingsStore store = new(settingsPath))
                    saved = store.LoadAsync().AsTask().GetAwaiter().GetResult().Settings;

                Assert.AreEqual(ColorMode.Light, saved.ColorMode);
                Assert.AreEqual(ColorTheme.DeathBlue, saved.LightColor);
                Assert.AreEqual(DownloadSourcePreference.MirrorOnly, saved.DownloadSource);
                Assert.IsTrue(saved.AutomaticallyRepairGameIssues);
                Assert.AreEqual(23, saved.IntegerOptions["LaunchRamCustom"]);
                Assert.AreEqual("新的标识", saved.TextOptions["LaunchArgumentInfo"]);
            }, CancellationToken.None);
        }
        finally
        {
            if (settingsWindow is not null)
                session.Dispatch(() => settingsWindow.Close(), CancellationToken.None);

            Environment.SetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH", previousOverride);
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageSetupUI_UsesLegacyDefaultsHintsAndDependentVisibility()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageSetupUI page = new();
            Window window = new() { Width = 900, Height = 640, Content = page };
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual(600, page.FindControl<MySlider>("SliderLauncherOpacity")!.Value);
                Assert.AreEqual(1000, page.FindControl<MySlider>("SliderBackgroundOpacity")!.Value);
                Assert.AreEqual(16, page.FindControl<MySlider>("SliderBlurValue")!.Value);
                Assert.AreEqual(70, page.FindControl<MySlider>("SliderBlurSamplingRate")!.Value);
                Assert.AreEqual(500, page.FindControl<MySlider>("SliderMusicVolume")!.Value);
                Assert.AreEqual("100%", page.FindControl<MySlider>("SliderLauncherOpacity")!.getHintText!(600));
                Assert.AreEqual("50%", page.FindControl<MySlider>("SliderMusicVolume")!.getHintText!(500));
                Assert.AreEqual("16 px", page.FindControl<MySlider>("SliderBlurValue")!.getHintText!(16));
                Assert.AreEqual("70%", page.FindControl<MySlider>("SliderBlurSamplingRate")!.getHintText!(70));
                Assert.IsFalse(page.FindControl<MyRadioBox>("RadioCustomType3")!.IsEnabled);
                Assert.AreEqual(0, page.FindControl<MyComboBox>("ComboCustomPreset")!.ItemCount);

                Grid blurOptions = page.FindControl<Grid>("PanBlurValue")!;
                Assert.IsFalse(blurOptions.IsVisible);
                page.FindControl<MyCheckBox>("CheckBlur")!.Checked = true;
                Assert.IsTrue(blurOptions.IsVisible);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void PageSetupUI_ConfiguresTextAndImageTitleModesAtRuntime()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string settingsPath = Environment.GetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH")!;

        session.Dispatch(() =>
        {
            PageSetupUI page = new();
            Window window = new() { Width = 900, Height = 640, Content = page };
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                page.FindControl<MyRadioBox>("RadioLogoType2")!.SetChecked(true, user: true);
                Assert.IsTrue(page.FindControl<Grid>("PanLogoText")!.IsVisible);
                MyTextBox titleText = page.FindControl<MyTextBox>("TextLogoText")!;
                titleText.Text = "Headless title";
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                using (LauncherSettingsStore store = new(settingsPath))
                {
                    LauncherSettings saved = store.LoadAsync().AsTask().GetAwaiter().GetResult().Settings;
                    Assert.AreEqual(2, saved.IntegerOptions["UiLogoType"]);
                    Assert.AreEqual("Headless title", saved.TextOptions["UiLogoText"]);
                }

                using MainWindow main = new();
                Assert.IsTrue(main.FindControl<TextBlock>("LabTitleLogo")!.IsVisible);
                Assert.AreEqual("Headless title", main.FindControl<TextBlock>("LabTitleLogo")!.Text);

                string logoPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(settingsPath)!, "Logo.png");
                using (Stream source = Avalonia.Platform.AssetLoader.Open(new Uri("avares://PCL.Desktop/Assets/icon.png")))
                using (FileStream target = File.Create(logoPath))
                    source.CopyTo(target);
                page.FindControl<MyRadioBox>("RadioLogoType3")!.SetChecked(true, user: true);
                page.RefreshPage();
                Assert.IsTrue(page.FindControl<Grid>("PanLogoChange")!.IsVisible);
                Assert.IsTrue(page.FindControl<MyButton>("BtnLogoDelete")!.IsVisible);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void PageSetupAbout_UsesUnifiedMetadataAndSponsorLink()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageSetupAbout page = new();
            Window window = new() { Width = 900, Height = 640, Content = page };
            string? openedUrl = null;
            page.OpenUrlRequested += (_, args) => openedUrl = args.Url;
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                MyListItem about = page.FindControl<MyListItem>("ItemAboutPcl")!;
                Assert.IsFalse(string.IsNullOrWhiteSpace(about.Info));
                Assert.IsFalse(about.Info.Contains("%VERSION", StringComparison.Ordinal));
                Assert.IsTrue(
                    about.Info.Any(static ch => char.IsDigit(ch)),
                    "About metadata should include a display version number.");
                Assert.AreEqual(6, page.FindControl<ItemsControl>("LicenseList")!.Items.Count);

                MyButton sponsor = page.FindControl<MyButton>("BtnCommunityHome")!;
                Assert.AreEqual(
                    AvaloniaLocalizationManager.GetText("Setup.About.SponsorCommunityAuthor", "赞助作者"),
                    sponsor.Text);
                Click(window, sponsor);
                Assert.AreEqual("https://ifdian.net/a/pclne", openedUrl);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void PageSetupFeedback_LoadsEveryGitHubPageAndExcludesPullRequests()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        int requestCount = 0;
        using HttpClient client = new(new StubHttpMessageHandler(request =>
        {
            requestCount++;
            bool firstPage = request.RequestUri?.Query.EndsWith("page=1", StringComparison.Ordinal) == true;
            int first = firstPage ? 1 : 101;
            int count = firstPage ? 100 : 1;
            object[] issues = Enumerable.Range(first, count)
                .Select(number => new
                {
                    number,
                    title = "Issue " + number,
                    user = new { login = "tester" },
                    html_url = "https://github.com/MuXue1230-owo/PCL-N/issues/" + number,
                    created_at = "2026-07-11T00:00:00Z",
                    state = number == 101 ? "closed" : "open",
                    labels = number == 101 ? new[] { new { id = 11215658137L, name = "完成" } } : [],
                    pull_request = firstPage && number == 1 ? new { url = "https://api.github.com/pulls/1" } : null
                })
                .Cast<object>()
                .ToArray();
            string json = System.Text.Json.JsonSerializer.Serialize(issues);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
        }));

        session.Dispatch(async () =>
        {
            using PageSetupFeedback page = new(client);
            await WaitForConditionAsync(() =>
                page.LoadedIssueCount == 100 &&
                page.FindControl<StackPanel>("PanListWait")!.Children.Count +
                page.FindControl<StackPanel>("PanListCompleted")!.Children.Count == 100).ConfigureAwait(true);

            Assert.AreEqual(2, requestCount);
            Assert.AreEqual(100, page.LoadedIssueCount);
            Assert.AreEqual(99, page.FindControl<StackPanel>("PanListWait")!.Children.Count);
            Assert.AreEqual(1, page.FindControl<StackPanel>("PanListCompleted")!.Children.Count);
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void MainWindow_AppliesPersistedRuntimeAppearanceSettings()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string settingsPath = Environment.GetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH")!;
        string backgroundPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(settingsPath)!,
            "Backgrounds",
            "background.png");
        using (LauncherSettingsStore store = new(settingsPath))
        {
            string homepageDirectory = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(settingsPath)!,
                "CustomHomepage");
            Directory.CreateDirectory(homepageDirectory);
            File.WriteAllText(System.IO.Path.Combine(homepageDirectory, "Custom.md"), "Headless custom homepage");
            string backgroundDirectory = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(settingsPath)!,
                "Backgrounds");
            Directory.CreateDirectory(backgroundDirectory);
            LauncherSettings settings = new()
            {
                BooleanOptions = new Dictionary<string, bool>
                {
                    ["UiLockWindowSize"] = true,
                    ["UiBlur"] = true,
                    ["UiLogoLeft"] = true,
                    ["UiShowLaunchingHint"] = false,
                    ["SystemDebugMode"] = true
                },
                IntegerOptions = new Dictionary<string, int>
                {
                    ["UiLauncherTransparent"] = 300,
                    ["UiLogoType"] = 2,
                    ["UiCustomType"] = 1,
                    ["UiBackgroundOpacity"] = 500,
                    ["UiBackgroundBlur"] = 4,
                    ["UiBackgroundSuit"] = 2
                },
                TextOptions = new Dictionary<string, string>
                {
                    ["UiLogoText"] = "Headless PCL"
                }
            };
            store.SaveAsync(settings).AsTask().GetAwaiter().GetResult();
        }

        session.Dispatch(() =>
        {
            using (Stream source = Avalonia.Platform.AssetLoader.Open(new Uri("avares://PCL.Desktop/Assets/icon.png")))
            using (FileStream target = File.Create(backgroundPath))
                source.CopyTo(target);
            using MainWindow window = new();
            window.Show();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Assert.IsFalse(window.CanResize);
            CollectionAssert.Contains(window.TransparencyLevelHint.ToArray(), WindowTransparencyLevel.None);
            Assert.IsFalse(window.TransparencyLevelHint.Contains(WindowTransparencyLevel.AcrylicBlur));
            Assert.IsNotNull(window.FindControl<Grid>("PanForm")!.Background);
            Assert.AreEqual("Headless PCL", window.FindControl<TextBlock>("LabTitleLogo")!.Text);
            Assert.IsTrue(window.FindControl<TextBlock>("LabTitleLogo")!.IsVisible);
            Assert.IsFalse(window.FindControl<Avalonia.Controls.Shapes.Path>("ShapeTitleLogo")!.IsVisible);
            Assert.AreEqual(HorizontalAlignment.Left, window.FindControl<Grid>("PanTitleMain")!.HorizontalAlignment);
            Image background = window.FindControl<Image>("ImageBack")!;
            Assert.IsNotNull(background.Source);
            Assert.AreEqual(0.5d, background.Opacity, 0.001d);
            Assert.AreEqual(Stretch.Uniform, background.Stretch);
            Assert.AreEqual(4d, ((BlurEffect)background.Effect!).Radius);
            PageLaunchLeft launch = window.GetVisualDescendants().OfType<PageLaunchLeft>().Single();
            launch.ShowLaunching(null);
            Assert.IsFalse(launch.FindControl<Grid>("PanLaunchingHint")!.IsVisible);
            Assert.IsTrue(window.GetVisualDescendants().OfType<PageLaunchRight>().Single().IsDebugLogVisible);
            Assert.IsTrue(window.GetVisualDescendants().OfType<TextBlock>()
                .Any(text => text.Text?.Contains("Headless custom homepage", StringComparison.Ordinal) == true));
            window.Close();
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void SettingsPageReset_RestoresOnlyTheSelectedPageDefaults()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string settingsPath = Environment.GetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH")!;
        using (LauncherSettingsStore store = new(settingsPath))
        {
            LauncherSettings configured = new()
            {
                AutomaticallyRepairGameIssues = false,
                IntegerOptions = new Dictionary<string, int>
                {
                    ["LaunchRamCustom"] = 24,
                    ["UiBackgroundOpacity"] = 7
                },
                TextOptions = new Dictionary<string, string>
                {
                    ["LaunchArgumentInfo"] = "custom",
                    ["UiLogoText"] = "keep-me"
                }
            };
            store.SaveAsync(configured).AsTask().GetAwaiter().GetResult();
        }

        session.Dispatch(() =>
        {
            PageSetupLeft left = new();
            PageSetupLaunch launch = (PageSetupLaunch)left.PageGet(SetupPageSubType.Launch);
            left.Reset(new MyIconButton { Tag = SetupPageSubType.Launch }, EventArgs.Empty);

            using LauncherSettingsStore store = new(settingsPath);
            LauncherSettings saved = store.LoadAsync().AsTask().GetAwaiter().GetResult().Settings;
            Assert.IsTrue(saved.AutomaticallyRepairGameIssues);
            Assert.IsFalse(saved.IntegerOptions.ContainsKey("LaunchRamCustom"));
            Assert.IsFalse(saved.TextOptions.ContainsKey("LaunchArgumentInfo"));
            Assert.AreEqual(7, saved.IntegerOptions["UiBackgroundOpacity"]);
            Assert.AreEqual("keep-me", saved.TextOptions["UiLogoText"]);
            Assert.IsTrue(launch.FindControl<MyCheckBox>("CheckAutoRepairGame")!.Checked);
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void PageSetupUI_PreservesAndPersistsFontSelectionAcrossAsyncFontLoad()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string settingsPath = Environment.GetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH")!;

        const string fontName = "PCL Headless Test Font";
        using (LauncherSettingsStore store = new(settingsPath))
        {
            LauncherSettings settings = new();
            settings.SetTextOption("UiFont", fontName);
            store.SaveAsync(settings).AsTask().GetAwaiter().GetResult();
        }

        session.Dispatch(() =>
        {
            PageSetupUI page = new();
            FontSelector selector = page.FindControl<FontSelector>("ComboUiFont")!;
            selector.EnsureFontsLoadedAsync([new FontFamily(fontName)]).GetAwaiter().GetResult();

            Assert.AreEqual(fontName, selector.SelectedFontTag, ignoreCase: true);
            selector.SelectedIndex = 0;
            Assert.AreEqual(
                new FontFamily("Microsoft YaHei UI, Segoe UI, Arial"),
                Avalonia.Application.Current!.Resources["LaunchFontFamily"]);

            using LauncherSettingsStore savedStore = new(settingsPath);
            LauncherSettings saved = savedStore.LoadAsync().AsTask().GetAwaiter().GetResult().Settings;
            Assert.IsFalse(saved.TextOptions.ContainsKey("UiFont"));
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void PageSetupLauncherMisc_UpdatesDependentControlsAndHints()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageSetupLauncherMisc misc = new();
            Window window = new() { Width = 900, Height = 640, Content = misc };
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.IsFalse(misc.FindControl<Grid>("HttpProxyCustom")!.IsVisible);
                misc.FindControl<MyRadioBox>("RadioHttpProxyType2")!.Checked = true;
                Assert.IsTrue(misc.FindControl<Grid>("HttpProxyCustom")!.IsVisible);
                Assert.AreEqual("1 FPS", misc.FindControl<MySlider>("SliderAniFPS")!.getHintText!(0));
                Assert.AreEqual("不限量", misc.FindControl<MySlider>("SliderMaxLog")!.getHintText!(29));
                Assert.AreEqual("关闭", misc.FindControl<MySlider>("SliderDebugAnim")!.getHintText!(30));
                Assert.IsNull(misc.FindControl<MyCheckBox>("CheckDebugMode"));

                bool confirmationRequested = false;
                misc.ConfirmRequested += (_, args) =>
                {
                    confirmationRequested = true;
                    args.Complete(false);
                };
                MyComboBox activity = misc.FindControl<MyComboBox>("ComboSystemActivity")!;
                activity.SelectedIndex = 1;
                activity.SelectedIndex = 2;
                Assert.IsTrue(confirmationRequested);
                Assert.AreEqual(1, activity.SelectedIndex);

            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void PageSetupUpdate_RequiresConfirmationForPreviewChannels()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageSetupUpdate page = new();
            Window window = new() { Width = 800, Height = 600, Content = page };
            MyComboBox channel = page.FindControl<MyComboBox>("ComboSystemUpdateChannel")!;
            bool confirmationRequested = false;
            string? messageTitle = null;
            page.ConfirmRequested += (_, args) =>
            {
                confirmationRequested = true;
                Assert.IsTrue(args.IsWarn);
                args.Complete(false);
            };
            page.MessageRequested += (_, args) => messageTitle = args.Title;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Type metadataType = typeof(MainWindow).Assembly.GetType("PCL.Desktop.Hosting.PclMetadata")!;
                object metadata = metadataType.GetProperty("Current")!.GetValue(null)!;
                string expectedVersion = "PCL N " + metadataType.GetProperty("DisplayVersion")!.GetValue(metadata);
                Assert.AreEqual(expectedVersion, page.FindControl<TextBlock>("TextCurrentVersion")!.Text);
                Assert.AreEqual(expectedVersion, page.FindControl<TextBlock>("TextUpdateName")!.Text);
                Assert.IsFalse(page.FindControl<MyCard>("CardUpdate")!.IsVisible);
                Assert.IsTrue(page.FindControl<MyCard>("CardCheck")!.IsVisible);

                Click(window, page.FindControl<MyButton>("BtnCheckAgain")!);
                Assert.AreEqual("暂不支持检查更新", messageTitle);

                channel.SelectedIndex = 1;
                Assert.IsTrue(confirmationRequested);
                Assert.AreEqual(0, channel.SelectedIndex);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void PageSetupLauncherLanguage_PersistsAndResetsSelections()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string settingsPath = Environment.GetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH")!;

        session.Dispatch(() =>
        {
            PageSetupLauncherLanguage page = new();
            page.Reload();
            MyComboBox language = page.FindControl<MyComboBox>("ComboUiLanguage")!;
            MyComboBox format = page.FindControl<MyComboBox>("ComboUiFormatCulture")!;
            language.SelectedItem = language.Items.OfType<MyComboBoxItem>()
                .Single(item => string.Equals(item.Tag?.ToString(), "en-US", StringComparison.OrdinalIgnoreCase));
            format.SelectedItem = format.Items.OfType<MyComboBoxItem>()
                .Single(item => string.Equals(item.Tag?.ToString(), "follow-language", StringComparison.OrdinalIgnoreCase));

            Assert.AreEqual("en-US", System.Globalization.CultureInfo.CurrentUICulture.Name);
            Assert.AreEqual("en-US", System.Globalization.CultureInfo.CurrentCulture.Name);
            Assert.AreEqual(
                "Personalization",
                AvaloniaLocalizationManager.GetText("Setup.Left.Item.Ui", "missing"));
            PageSetupLeft localizedNavigation = new();
            Window localizedWindow = new() { Content = localizedNavigation };
            try
            {
                localizedWindow.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.AreEqual(
                    "Personalization",
                    localizedNavigation.FindControl<MyListItem>("ItemUI")!.Title);
            }
            finally
            {
                localizedWindow.Close();
            }
            using (MainWindow localizedMain = new())
            {
                MyListItem[] navigationItems = localizedMain.FindControl<Panel>("PanTitleSelect")!.Children
                    .OfType<MyListItem>()
                    .ToArray();
                Assert.AreEqual("Launch", navigationItems[0].Title);
                Assert.AreEqual("Settings", navigationItems[3].Title);
            }

            using (LauncherSettingsStore store = new(settingsPath))
            {
                LauncherSettings saved = store.LoadAsync().AsTask().GetAwaiter().GetResult().Settings;
                Assert.AreEqual("en-US", saved.GetTextOption("UiLanguage"));
                Assert.AreEqual("follow-language", saved.GetTextOption("UiFormatCulture"));
            }

            page.Reset();
            using LauncherSettingsStore resetStore = new(settingsPath);
            LauncherSettings reset = resetStore.LoadAsync().AsTask().GetAwaiter().GetResult().Settings;
            Assert.IsFalse(reset.TextOptions.ContainsKey("UiLanguage"));
            Assert.IsFalse(reset.TextOptions.ContainsKey("UiFormatCulture"));
            Assert.AreEqual("auto", ((MyComboBoxItem)language.SelectedItem!).Tag);
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void PageSetupLaunch_RefreshesMemoryDisplayFromLiveProvider()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MutableSystemInfoProvider provider = new(16L << 30, 12L << 30);
            PageSetupLaunch page = new(provider);
            Window window = new() { Width = 900, Height = 640, Content = page };
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.AreEqual("4.0 GB", page.FindControl<TextBlock>("LabRamUsed")!.Text);

                provider.AvailableBytes = 8L << 30;
                page.RefreshMemoryDisplay();
                Assert.AreEqual("8.0 GB", page.FindControl<TextBlock>("LabRamUsed")!.Text);

                Avalonia.Controls.Shapes.Rectangle usedBar = page.FindControl<Avalonia.Controls.Shapes.Rectangle>("RectRamUsed")!;
                TextBlock game = page.FindControl<TextBlock>("LabRamGame")!;
                game.Text = "64.0 GB (可用 1.0 GB)";
                InvokePrivateNoArgs(page, "RefreshRamText");
                Point usedRight = usedBar.TranslatePoint(new Point(usedBar.Bounds.Width, 0d), page)!.Value;
                Point gameLeft = game.TranslatePoint(new Point(0d, 0d), page)!.Value;
                Assert.IsTrue(
                    gameLeft.X >= usedRight.X,
                    $"RAM game text must not jump across the stable used-memory bar. usedRight={usedRight.X}, gameLeft={gameLeft.X}");
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void SettingsPages_PreserveScrollOffsetAcrossVisualReattachment()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageSetupLaunch page = new();
            Window window = new() { Width = 900, Height = 360, Content = page };
            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                MyScrollViewer scroll = page.FindControl<MyScrollViewer>("PanBack")!;
                scroll.Offset = new Vector(0d, 180d);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                double preservedOffset = scroll.Offset.Y;
                Assert.IsTrue(preservedOffset > 0d);

                window.Content = new Border();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                window.Content = page;
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.AreEqual(preservedOffset, scroll.Offset.Y, 0.01d);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void PageSetupLaunch_UpdatesDependentOptionsAndResetsJvmArguments()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-launch-settings-" + Guid.NewGuid().ToString("N"));
        string settingsPath = System.IO.Path.Combine(root, "launcher-settings.json");
        string? previousOverride = Environment.GetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH");
        Window? window = null;

        try
        {
            Directory.CreateDirectory(root);
            Environment.SetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH", settingsPath);

            session.Dispatch(() =>
            {
                PageSetupLaunch page = new();
                string? messageTitle = null;
                page.MessageRequested += (_, args) => messageTitle = args.Title;
                window = new Window
                {
                    Width = 900,
                    Height = 640,
                    Content = page
                };

                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                MyComboBox isolation = page.FindControl<MyComboBox>("ComboArgumentIndieV2")!;
                Assert.AreEqual(1, isolation.SelectedIndex);
                Assert.IsFalse(string.IsNullOrWhiteSpace(isolation.SelectionText));
                Assert.IsNull(messageTitle, "Applying the saved isolation option must not show a user-action warning.");
                isolation.SelectedIndex = 0;
                Assert.IsNull(messageTitle, "A closed, programmatic selection must not show the isolation warning.");
                isolation.IsDropDownOpen = true;
                isolation.SelectedIndex = 1;
                Assert.AreEqual("实例隔离说明", messageTitle);
                isolation.IsDropDownOpen = false;
                messageTitle = null;

                MySlider customRam = page.FindControl<MySlider>("SliderRamCustom")!;
                Assert.IsFalse(customRam.IsEnabled);
                page.FindControl<MyRadioBox>("RadioRamType1")!.SetChecked(true, user: true);
                Assert.IsTrue(customRam.IsEnabled);
                customRam.Value = 18;
                Assert.AreEqual(18, customRam.Value);

                TextBlock titleLabel = page.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .First(text => text.Text == "游戏窗口标题");
                MyComboBox titleCombo = page.FindControl<MyComboBox>("TextArgumentTitle")!;
                Point titleLabelRight = titleLabel.TranslatePoint(new Point(titleLabel.Bounds.Width, 0d), page)
                    ?? throw new InvalidOperationException("Title label is not attached.");
                Point titleComboLeft = titleCombo.TranslatePoint(new Point(0d, 0d), page)
                    ?? throw new InvalidOperationException("Title combo is not attached.");
                Assert.IsTrue(
                    titleLabelRight.X + 8d <= titleComboLeft.X,
                    $"Window title label and editor should not overlap. labelRight={titleLabelRight.X}, comboLeft={titleComboLeft.X}");

                TextBlock ramUsed = page.FindControl<TextBlock>("LabRamUsed")!;
                TextBlock ramGame = page.FindControl<TextBlock>("LabRamGame")!;
                if (ramUsed.Opacity > 0.01d && ramGame.Opacity > 0.01d)
                {
                    Point usedRight = ramUsed.TranslatePoint(new Point(ramUsed.Bounds.Width, 0d), page)
                        ?? throw new InvalidOperationException("RAM used text is not attached.");
                    Point gameLeft = ramGame.TranslatePoint(new Point(0d, 0d), page)
                        ?? throw new InvalidOperationException("RAM game text is not attached.");
                    Assert.IsTrue(
                        usedRight.X + 6d <= gameLeft.X,
                        $"RAM labels should use the WPF collision-avoidance layout. usedRight={usedRight.X}, gameLeft={gameLeft.X}");
                }

                MyTextBox widthBox = page.FindControl<MyTextBox>("TextArgumentWindowWidth")!;
                MyTextBox heightBox = page.FindControl<MyTextBox>("TextArgumentWindowHeight")!;
                TextBlock separator = page.FindControl<TextBlock>("LabArgumentWindowMiddle")!;
                Assert.IsFalse(widthBox.IsVisible);
                Assert.IsFalse(heightBox.IsVisible);
                Assert.IsFalse(separator.IsVisible);

                MyComboBox priority = page.FindControl<MyComboBox>("ComboArgumentPriority")!;
                Assert.AreEqual(3, priority.SelectedIndex);
                priority.SelectedIndex = 0;
                using (LauncherSettingsStore priorityStore = new(settingsPath))
                {
                    LauncherSettings prioritySettings = priorityStore.LoadAsync().AsTask().GetAwaiter().GetResult().Settings;
                    Assert.AreEqual(4, prioritySettings.IntegerOptions["LaunchArgumentPriority"]);
                }

                page.FindControl<MyComboBox>("ComboArgumentWindowType")!.SelectedIndex = 3;
                Assert.IsTrue(widthBox.IsVisible);
                Assert.IsTrue(heightBox.IsVisible);
                Assert.IsTrue(separator.IsVisible);

                MyCheckBox waitCheck = page.FindControl<MyCheckBox>("CheckAdvanceRunWait")!;
                Assert.IsFalse(waitCheck.IsVisible);
                page.FindControl<MyTextBox>("TextAdvanceRun")!.Text = "echo preparing";
                Assert.IsTrue(waitCheck.IsVisible);

                MyTextBox jvmText = page.FindControl<MyTextBox>("TextAdvanceJvm")!;
                MyIconButton resetButton = page.FindControl<MyIconButton>("BtnAdvanceJvmReset")!;
                jvmText.Text = "-Xmx2G";
                Assert.IsTrue(resetButton.IsVisible);
                Click(window, resetButton);

                Assert.AreEqual("已恢复默认 JVM 参数", messageTitle);
                Assert.IsTrue(jvmText.Text?.Contains("-XX:+UseG1GC", StringComparison.Ordinal) == true);
                Assert.IsFalse(resetButton.IsVisible);

                using LauncherSettingsStore store = new(settingsPath);
                LauncherSettings saved = store.LoadAsync().AsTask().GetAwaiter().GetResult().Settings;
                Assert.IsTrue(saved.TextOptions["LaunchAdvanceJvm"].Contains("-XX:+UseG1GC", StringComparison.Ordinal));
            }, CancellationToken.None);
        }
        finally
        {
            if (window is not null)
                session.Dispatch(() => window.Close(), CancellationToken.None);

            Environment.SetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH", previousOverride);
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageSetupJava_KeepsContentHiddenWhileJavaListLoads()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageSetupJava page = new();
            Window window = new()
            {
                Width = 900,
                Height = 640,
                Content = page
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                page.PageOnEnter();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsTrue(page.FindControl<MyCard>("CardLoad")!.IsVisible);
                Assert.IsFalse(page.FindControl<StackPanel>("PanMain")!.IsVisible);
                Assert.IsFalse(page.FindControl<MyCard>("CardContent")!.IsVisible);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageSetupJava_ShowsContentCardAfterJavaListRenders()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageSetupJava page = new();
            Window window = new()
            {
                Width = 900,
                Height = 640,
                Content = page
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                SetPrivateField(
                    page,
                    "_javaCandidates",
                    new List<JavaRuntimeCandidate>
                    {
                        new(
                            new JavaInstallation(
                                @"D:\Java\jdk-21",
                                @"D:\Java\jdk-21\bin\java.exe",
                                @"D:\Java\jdk-21\bin\javaw.exe",
                                new Version(21, 0, 1),
                                JavaBrand.OpenJDK,
                                JavaArchitecture.X64,
                                Is64Bit: true,
                                IsJre: false))
                    });

                InvokePrivateNoArgs(page, "RenderJavaList");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsTrue(page.FindControl<MyCard>("CardContent")!.IsVisible);
                Assert.IsTrue(ModAnimation.AniIsRun("Java Runtime List"));
                Assert.IsTrue(page.FindControl<StackPanel>("PanContent")!.Children.OfType<MyListItem>().Any(item =>
                    item.Title.StartsWith("JDK 21", StringComparison.Ordinal)));

                page.RefreshPage();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsFalse(page.FindControl<MyCard>("CardContent")!.IsVisible);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageSetupGameManage_WarnsOnceForHighUserSelectedDownloadThreads()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-game-manage-settings-" + Guid.NewGuid().ToString("N"));
        string settingsPath = System.IO.Path.Combine(root, "launcher-settings.json");
        string? previousOverride = Environment.GetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH");
        Window? window = null;

        try
        {
            Directory.CreateDirectory(root);
            Environment.SetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH", settingsPath);

            session.Dispatch(() =>
            {
                PageSetupGameManage page = new();
                List<string> messageTitles = [];
                page.MessageRequested += (_, args) => messageTitles.Add(args.Title);
                window = new Window
                {
                    Width = 900,
                    Height = 640,
                    Content = page
                };

                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                MySlider threadSlider = page.FindControl<MySlider>("SliderDownloadThread")!;
                threadSlider.Value = 99;
                Assert.AreEqual(0, messageTitles.Count);

                threadSlider.Focus();
                window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, string.Empty);
                Assert.AreEqual("下载线程过高", messageTitles.Single());

                using (LauncherSettingsStore store = new(settingsPath))
                {
                    LauncherSettings saved = store.LoadAsync().AsTask().GetAwaiter().GetResult().Settings;
                    Assert.IsTrue(saved.BooleanOptions["HintDownloadThread"]);
                }

                messageTitles.Clear();
                threadSlider.Value = 99;
                window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, string.Empty);
                Assert.AreEqual(0, messageTitles.Count);
            }, CancellationToken.None);
        }
        finally
        {
            if (window is not null)
                session.Dispatch(() => window.Close(), CancellationToken.None);

            Environment.SetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH", previousOverride);
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceLeftAndToolsRight_ExposePortableManagementPages()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-tools-" + Guid.NewGuid().ToString("N"));

        try
        {
            string versionDirectory = System.IO.Path.Combine(root, "versions", "1.20.1");
            Directory.CreateDirectory(versionDirectory);
            LaunchInstanceInfo instance = new("1.20.1", System.IO.Path.Combine(versionDirectory, "1.20.1.json"), versionDirectory);

            session.Dispatch(() =>
            {
                PageInstanceLeft left = new();
                PageInstanceToolsRight right = new();
                PageInstanceSetupRight setupRight = new();
                PageInstanceServerRight serverRight = new();
                Grid host = new()
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(new GridLength(180)),
                        new ColumnDefinition(GridLength.Star)
                    },
                    Children =
                    {
                        left,
                        right,
                        setupRight,
                        serverRight
                    }
                };
                Grid.SetColumn(right, 1);
                Grid.SetColumn(setupRight, 1);
                Grid.SetColumn(serverRight, 1);
                setupRight.IsVisible = false;
                serverRight.IsVisible = false;
                Window window = new()
                {
                    Width = 720,
                    Height = 420,
                    Content = host
                };
                InstancePageSubType? changed = null;
                string? openedPath = null;
                bool serverAddRequested = false;
                MinecraftServerEntry? serverConnectRequested = null;
                left.PageChanged += (_, page) => changed = page;
                right.OpenFolderRequested += (_, path) => openedPath = path;
                serverRight.AddServerRequested += (_, _) => serverAddRequested = true;
                serverRight.ConnectServerRequested += (_, entry) => serverConnectRequested = entry;

                try
                {
                    window.Show();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Assert.AreEqual(InstancePageSubType.Export, left.FindControl<MyListItem>("ItemExport")!.Tag);
                    Assert.AreEqual(
                        InstancePageSubType.Install,
                        left.FindControl<MyListItem>("ItemInstall")!.Buttons.Single().Tag);

                    Click(window, left.FindControl<MyListItem>("ItemExport")!);

                    Assert.AreEqual(InstancePageSubType.Export, changed);

                    right.SetContext(instance, InstancePageSubType.Mods);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Assert.AreEqual("Mod", right.GetVisualDescendants().OfType<MyCard>().First().Title);
                    MyButton openButton = right.GetVisualDescendants().OfType<MyButton>().First(button => button.Text == "打开文件夹");
                    Click(window, openButton);

                    Assert.AreEqual(System.IO.Path.Combine(root, "mods"), openedPath);

                    right.IsVisible = false;
                    setupRight.IsVisible = true;
                    setupRight.SetInstance(instance);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Assert.IsNotNull(setupRight.FindControl<MyComboBox>("ComboArgumentIndieV2"));
                    Assert.IsNotNull(setupRight.FindControl<MyCheckBox>("CheckAdvanceAssetsV2"));
                    Assert.AreEqual("启动选项", setupRight.FindControl<MyCard>("CardArgument")!.Title);
                    Assert.AreEqual("服务器", setupRight.FindControl<MyCard>("CardServer")!.Title);
                    Assert.AreEqual("高级启动选项", setupRight.FindControl<MyCard>("CardAdvance")!.Title);
                    Assert.IsTrue(setupRight.GetVisualDescendants().OfType<TextBlock>()
                        .Any(text => text.Text == "实例隔离"));
                    Assert.AreEqual(
                        "开启",
                        setupRight.FindControl<MyComboBox>("ComboArgumentIndieV2")!.Items
                            .OfType<MyComboBoxItem>()
                            .First()
                            .Content?.ToString());
                    setupRight.FindControl<MyTextBox>("TextServerEnter")!.Text = "mc.example.net";
                    setupRight.FindControl<MyCheckBox>("CheckAdvanceAssetsV2")!.Checked = true;
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    WaitForCondition(() =>
                    {
                        InstanceMetadata metadata = InstanceMetadataStore.LoadAsync(versionDirectory).GetAwaiter().GetResult();
                        return metadata.ServerToEnter == "mc.example.net" && metadata.DisableAssetVerification;
                    });

                    setupRight.IsVisible = false;
                    right.IsVisible = false;
                    serverRight.IsVisible = true;
                    serverRight.SetInstance(instance);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Assert.IsTrue(serverRight.FindControl<MyCard>("PanNoServer")!.IsVisible);
                    Click(window, serverRight.FindControl<MyButton>("BtnAddServerTop")!);
                    Assert.IsTrue(serverAddRequested);

                    WriteServersDat(root);
                    serverRight.Reload();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    Assert.IsFalse(serverRight.FindControl<MyCard>("PanNoServer")!.IsVisible);
                    ServerCard serverCard = serverRight.GetVisualDescendants().OfType<ServerCard>().Single();
                    Assert.IsTrue(serverCard.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Hypixel"));
                    Assert.IsTrue(serverCard.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "mc.hypixel.net"));
                    Click(window, serverCard.FindControl<MyIconButton>("BtnConnect")!);
                    Assert.AreEqual("mc.hypixel.net", serverConnectRequested?.Address);
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceSetupRight_RefreshesWpfRamDisplayForCurrentInstance()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-ram-" + Guid.NewGuid().ToString("N"));

        try
        {
            string versionDirectory = System.IO.Path.Combine(root, "versions", "Fabric 1.20.1");
            string versionJsonPath = System.IO.Path.Combine(versionDirectory, "Fabric 1.20.1.json");
            Directory.CreateDirectory(versionDirectory);
            File.WriteAllText(versionJsonPath, """{"id":"Fabric 1.20.1","libraries":[{"name":"net.fabricmc:fabric-loader:0.16.14"}]}""");
            InstanceMetadataStore.SaveAsync(
                versionDirectory,
                new InstanceMetadata
                {
                    MemorySolution = 1,
                    CustomMemorySize = 25,
                    ServerLoginRequirement = 2,
                    AuthServerAddress = "https://example.com/api/yggdrasil"
                }).GetAwaiter().GetResult();
            LaunchInstanceInfo instance = new("Fabric 1.20.1", versionJsonPath, versionDirectory);

            session.Dispatch(() =>
            {
                const long gibibyte = 1024L * 1024L * 1024L;
                PageInstanceSetupRight page = new(new FixedSystemInfoProvider(16 * gibibyte, 8 * gibibyte));
                page.SetInstance(instance);
                page.ConfirmRequested += (_, args) => args.Complete(true);
                Window window = new()
                {
                    Width = 760,
                    Height = 620,
                    Content = page
                };

                try
                {
                    window.Show();
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Grid ramDisplay = page.FindControl<Grid>("PanRamDisplay")!;
                    Assert.AreEqual(8d, ramDisplay.ColumnDefinitions[0].Width.Value, 0.01d);
                    Assert.AreEqual(8d, ramDisplay.ColumnDefinitions[1].Width.Value, 0.01d);
                    Assert.AreEqual(0d, ramDisplay.ColumnDefinitions[2].Width.Value, 0.01d);
                    StringAssert.StartsWith(page.FindControl<TextBlock>("LabRamGame")!.Text, "8");
                    Assert.AreEqual("8.0 GB", page.FindControl<TextBlock>("LabRamUsed")!.Text);
                    Assert.AreEqual(" / 16.0 GB", page.FindControl<TextBlock>("LabRamTotal")!.Text);
                    Assert.AreEqual(33, page.FindControl<MySlider>("SliderRamCustom")!.MaxValue);
                    Assert.IsFalse(page.FindControl<MyHint>("HintRamTooHigh")!.IsVisible);
                    Assert.IsTrue(page.FindControl<MyTextBox>("TextServerAuthServer")!.IsVisible);
                    Assert.IsTrue(page.FindControl<MyButton>("BtnServerAuthLittle")!.IsVisible);
                    Assert.IsTrue(page.FindControl<MyButton>("BtnServerAuthLock")!.IsEnabled);

                    MyComboBox title = page.FindControl<MyComboBox>("TextArgumentTitle")!;
                    title.Text = "独立标题";
                    page.FindControl<MyTextBox>("TextArgumentInfo")!.Text = "Fabric 测试实例";
                    page.FindControl<MyTextBox>("TextServerEnter")!.Text = "localhost：25565";
                    Assert.AreEqual("localhost:25565", page.FindControl<MyTextBox>("TextServerEnter")!.Text);

                    MyButton littleSkinButton = page.FindControl<MyButton>("BtnServerAuthLittle")!;
                    littleSkinButton.BringIntoView();
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    Click(window, littleSkinButton);
                    Assert.AreEqual("https://littleskin.cn/api/yggdrasil", page.FindControl<MyTextBox>("TextServerAuthServer")!.Text);
                    Assert.AreEqual("LittleSkin", page.FindControl<MyTextBox>("TextServerAuthName")!.Text);

                    Click(window, page.FindControl<MyButton>("BtnServerAuthLock")!);
                    Assert.IsFalse(page.FindControl<MyComboBox>("ComboServerLoginRequire")!.IsEnabled);
                    Assert.IsTrue(page.FindControl<MyHint>("HintServerLoginLock")!.IsVisible);

                    page.FindControl<MySlider>("SliderRamCustom")!.Value = 33;
                    Assert.IsTrue(page.FindControl<TextBlock>("LabRamGame")!.Text!.Contains("可用 8.0 GB", StringComparison.Ordinal));
                    Assert.IsTrue(page.FindControl<MyHint>("HintRamTooHigh")!.IsVisible);

                    page.WaitForPendingMetadataWritesAsync().GetAwaiter().GetResult();
                    InstanceMetadata saved = InstanceMetadataStore.LoadAsync(versionDirectory).GetAwaiter().GetResult();
                    Assert.AreEqual("独立标题", saved.WindowTitle);
                    Assert.AreEqual("Fabric 测试实例", saved.CustomInfo);
                    Assert.AreEqual("localhost:25565", saved.ServerToEnter);
                    Assert.AreEqual("LittleSkin", saved.AuthServerDisplayName);
                    Assert.IsTrue(saved.AuthSettingsLocked);
                    Assert.AreEqual(33, saved.CustomMemorySize);
                }
                finally
                {
                    window.Close();
                    page.Dispose();
                }
            }, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceExportRight_UsesCopiedWpfOptionTreeAndRaisesExportRequest()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-export-ui-" + Guid.NewGuid().ToString("N"));

        try
        {
            string versionDirectory = System.IO.Path.Combine(root, "versions", "1.20.1");
            Directory.CreateDirectory(versionDirectory);
            File.WriteAllText(System.IO.Path.Combine(root, "options.txt"), "settings");
            LaunchInstanceInfo instance = new("1.20.1", System.IO.Path.Combine(versionDirectory, "1.20.1.json"), versionDirectory);

            session.Dispatch(() =>
            {
                PageInstanceExportRight page = new();
                Window window = new()
                {
                    Width = 720,
                    Height = 480,
                    Content = page
                };
                InstanceExportPageRequest? exportRequest = null;
                page.ExportRequested += (_, request) => exportRequest = request;

                try
                {
                    window.Show();
                    page.SetInstance(instance);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Assert.IsNotNull(page.FindControl<MyTextBox>("TextExportName"));
                    Assert.IsNotNull(page.FindControl<MyCard>("CardOptions"));
                    Assert.IsTrue(page.FindControl<MyCheckBox>("CheckOptionsBasic")!.Inlines.Count > 0);
                    Assert.IsTrue(page.FindControl<MyCheckBox>("CheckOptionsOptions")!.IsVisible);
                    Assert.AreEqual(
                        "游戏本体",
                        DisplayText(page.FindControl<MyCheckBox>("CheckOptionsBasic")!.FindControl<TextBlock>("LabText")!));
                    StringAssert.Contains(
                        DisplayText(page.FindControl<MyCheckBox>("CheckOptionsOptions")!.FindControl<TextBlock>("LabText")!),
                        "游戏本体设置");
                    StringAssert.Contains(
                        DisplayText(page.FindControl<MyCheckBox>("CheckOptionsOptions")!.FindControl<TextBlock>("LabText")!),
                        "键位、音量、视频设置等");

                    Click(window, page.FindControl<MyExtraTextButton>("BtnExport")!);

                    Assert.IsNotNull(exportRequest);
                    Assert.AreEqual("1.20.1", exportRequest!.PackageName);
                    CollectionAssert.Contains(exportRequest.Rules.ToList(), "options.txt");
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceInstallRight_UsesCopiedWpfCardsAndCurrentInstance()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-install-ui-" + Guid.NewGuid().ToString("N"));

        try
        {
            string versionDirectory = System.IO.Path.Combine(root, "versions", "1.20.1");
            Directory.CreateDirectory(versionDirectory);
            File.WriteAllText(System.IO.Path.Combine(versionDirectory, "fabric-loader-0.16.10.jar"), "loader");
            File.WriteAllText(
                System.IO.Path.Combine(versionDirectory, "1.20.1.json"),
                """
                {
                  "id": "fabric-loader-0.16.10-1.20.1",
                  "inheritsFrom": "1.20.1",
                  "libraries": [
                    { "name": "net.fabricmc:fabric-loader:0.16.10" }
                  ]
                }
                """);
            InstanceMetadataStore.SaveAsync(
                versionDirectory,
                new InstanceMetadata
                {
                    LogoPath = "avares://PCL.Desktop/Assets/Legacy/Blocks/Fabric.png"
                }).GetAwaiter().GetResult();
            LaunchInstanceInfo instance = new("1.20.1", System.IO.Path.Combine(versionDirectory, "1.20.1.json"), versionDirectory);

            session.Dispatch(() =>
            {
                PageInstanceInstallRight page = new();
                Window window = new()
                {
                    Width = 720,
                    Height = 480,
                    Content = page
                };
                InstanceInstallModifyRequest? modifyRequest = null;
                page.ModifyRequested += (_, request) => modifyRequest = request;

                try
                {
                    window.Show();
                    page.SetInstance(instance);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Assert.AreEqual("1.20.1", page.FindControl<MyListItem>("ItemSelect")!.Title);
                    Assert.IsTrue(page.FindControl<MyListItem>("ItemSelect")!.Logo.EndsWith("Fabric.png", StringComparison.OrdinalIgnoreCase));
                    Assert.AreEqual("1.20.1  |  Fabric 0.16.10", page.FindControl<MyListItem>("ItemSelect")!.Info);
                    Assert.AreEqual("1.20.1", page.FindControl<TextBlock>("LabMinecraft")!.Text);
                    Assert.IsTrue((page.FindControl<Image>("ImgMinecraft")!.Tag as string)?.EndsWith("Fabric.png", StringComparison.OrdinalIgnoreCase) == true);
                    Assert.AreEqual("0.16.10", page.FindControl<TextBlock>("LabFabric")!.Text);
                    Assert.IsTrue(page.FindControl<MyCard>("CardFabric")!.IsVisible);
                    Assert.IsTrue(page.FindControl<MyCard>("CardNeoForge")!.IsVisible);
                    Assert.IsFalse(page.FindControl<MyCard>("CardLiteLoader")!.IsVisible);
                    Assert.IsFalse(page.FindControl<MyCard>("CardLegacyFabric")!.IsVisible);
                    Assert.IsFalse(page.FindControl<MyCard>("CardCleanroom")!.IsVisible);
                    Assert.IsFalse(page.FindControl<Control>("PanLoad")!.IsVisible);
                    Assert.IsTrue(page.FindControl<Control>("PanSelect")!.IsVisible);

                    Click(window, page.FindControl<MyExtraTextButton>("BtnSelectStart")!);

                    Assert.AreEqual(instance, modifyRequest?.Instance);
                    Assert.AreEqual("1.20.1", modifyRequest?.MinecraftVersionId);
                    Assert.IsNull(modifyRequest?.LoaderKind);

                    modifyRequest = null;
                    Click(window, page.FindControl<MyCard>("CardFabric")!);

                    Assert.AreEqual(instance, modifyRequest?.Instance);
                    Assert.AreEqual("1.20.1", modifyRequest?.MinecraftVersionId);
                    Assert.AreEqual(MinecraftLoaderKind.Fabric, modifyRequest?.LoaderKind);
                    Assert.IsTrue(page.FindControl<MyCard>("CardFabric")!.IsSwapped);

                    foreach ((string cardName, MinecraftLoaderKind kind) in new[]
                             {
                                 ("Forge", MinecraftLoaderKind.Forge),
                                 ("NeoForge", MinecraftLoaderKind.NeoForge),
                                 ("Quilt", MinecraftLoaderKind.Quilt),
                                 ("LabyMod", MinecraftLoaderKind.LabyMod),
                                 ("OptiFine", MinecraftLoaderKind.OptiFine)
                             })
                    {
                        modifyRequest = null;
                        Click(window, page.FindControl<MyCard>("Card" + cardName)!);
                        Assert.AreEqual(kind, modifyRequest?.LoaderKind, cardName);
                        Assert.IsTrue(page.FindControl<MyCard>("Card" + cardName)!.IsSwapped, cardName);
                    }

                    modifyRequest = null;
                    Assert.IsTrue(page.FindControl<MyCard>("CardFabricApi")!.IsVisible);
                    Click(window, page.FindControl<MyCard>("CardFabricApi")!);
                    Assert.AreEqual(MinecraftInstallAddonKind.FabricApi, modifyRequest?.AddonKind);
                    Assert.AreEqual(MinecraftLoaderKind.Fabric, modifyRequest?.CurrentLoaderKind);
                    Assert.AreEqual("0.16.10", modifyRequest?.CurrentLoaderVersion);
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceInstallRight_SelectsMinecraftVersionInCopiedWpfList()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-install-minecraft-" + Guid.NewGuid().ToString("N"));

        try
        {
            string versionDirectory = System.IO.Path.Combine(root, "versions", "fabric-pack");
            Directory.CreateDirectory(versionDirectory);
            File.WriteAllText(
                System.IO.Path.Combine(versionDirectory, "fabric-pack.json"),
                """
                {
                  "id": "fabric-pack",
                  "inheritsFrom": "1.20.1",
                  "libraries": [
                    { "name": "net.fabricmc:fabric-loader:0.16.10" }
                  ]
                }
                """);
            LaunchInstanceInfo instance = new("fabric-pack", System.IO.Path.Combine(versionDirectory, "fabric-pack.json"), versionDirectory);

            session.Dispatch(() =>
            {
                PageInstanceInstallRight page = new();
                SetPrivateField(
                    page,
                    "_versions",
                    new[]
                    {
                        new MinecraftVersionManifestEntry("1.20.2", "release", "https://example.invalid/1.20.2.json", DateTimeOffset.Parse("2023-09-21T00:00:00Z")),
                        new MinecraftVersionManifestEntry("1.20.1", "release", "https://example.invalid/1.20.1.json", DateTimeOffset.Parse("2023-06-12T00:00:00Z")),
                        new MinecraftVersionManifestEntry("24w14a", "snapshot", "https://example.invalid/24w14a.json", DateTimeOffset.Parse("2024-04-03T00:00:00Z"))
                    });
                Window window = new()
                {
                    Width = 720,
                    Height = 520,
                    Content = page
                };
                InstanceInstallModifyRequest? modifyRequest = null;
                page.ModifyRequested += (_, request) => modifyRequest = request;

                try
                {
                    window.Show();
                    page.SetInstance(instance);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Click(window, page.FindControl<MyCard>("CardMinecraft")!);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Assert.IsFalse(page.FindControl<StackPanel>("PanSelect")!.IsVisible);
                    Assert.IsTrue(page.FindControl<StackPanel>("PanMinecraft")!.IsVisible);
                    Assert.IsFalse(page.FindControl<MyExtraTextButton>("BtnSelectStart")!.Show);
                    Assert.IsTrue(page.GetVisualDescendants().OfType<MyCard>().Any(card => card.Title == "最新版本"));
                    Assert.IsTrue(page.GetVisualDescendants().OfType<MyCard>().Any(card => card.Title == "正式版 (2)"));

                    MyListItem versionItem = page.GetVisualDescendants()
                        .OfType<MyListItem>()
                        .First(item => item.Title == "1.20.2" && item.IsVisible);
                    Click(window, versionItem);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Assert.IsTrue(page.FindControl<StackPanel>("PanSelect")!.IsVisible);
                    Assert.IsFalse(page.FindControl<StackPanel>("PanMinecraft")!.IsVisible);
                    Assert.AreEqual("1.20.2", page.FindControl<TextBlock>("LabMinecraft")!.Text);
                    Assert.AreEqual("1.20.2  |  无额外安装", page.FindControl<MyListItem>("ItemSelect")!.Info);
                    Assert.AreEqual("可添加", page.FindControl<TextBlock>("LabFabric")!.Text);

                    Click(window, page.FindControl<MyExtraTextButton>("BtnSelectStart")!);

                    Assert.AreEqual(instance, modifyRequest?.Instance);
                    Assert.AreEqual("1.20.2", modifyRequest?.MinecraftVersionId);
                    Assert.IsNull(modifyRequest?.LoaderKind);
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceInstallRight_RefreshResetsWpfSelectState()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-install-state-" + Guid.NewGuid().ToString("N"));

        try
        {
            string versionDirectory = System.IO.Path.Combine(root, "versions", "1.20.1");
            Directory.CreateDirectory(versionDirectory);
            File.WriteAllText(System.IO.Path.Combine(versionDirectory, "forge-47.3.0.jar"), "loader");
            LaunchInstanceInfo instance = new("1.20.1", System.IO.Path.Combine(versionDirectory, "1.20.1.json"), versionDirectory);

            session.Dispatch(() =>
            {
                PageInstanceInstallRight page = new();
                Window window = new()
                {
                    Width = 720,
                    Height = 480,
                    Content = page
                };

                try
                {
                    window.Show();
                    page.SetInstance(instance);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    MyCard forge = page.FindControl<MyCard>("CardForge")!;
                    StackPanel select = page.FindControl<StackPanel>("PanSelect")!;
                    StackPanel minecraft = page.FindControl<StackPanel>("PanMinecraft")!;
                    MyScrollViewer scroll = page.FindControl<MyScrollViewer>("PanBack")!;

                    forge.IsSwapped = false;
                    select.Opacity = 0.25d;
                    minecraft.IsVisible = true;
                    minecraft.Opacity = 1d;
                    select.RenderTransform = new TranslateTransform { X = 46d };
                    minecraft.RenderTransform = new TranslateTransform { X = -38d };
                    scroll.IsHitTestVisible = false;

                    page.RefreshAll();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Assert.IsTrue(forge.IsSwapped);
                    Assert.IsTrue(select.IsVisible);
                    Assert.IsTrue(select.IsHitTestVisible);
                    Assert.AreEqual(1d, select.Opacity, 0.01d);
                    Assert.IsFalse(minecraft.IsVisible);
                    Assert.IsFalse(minecraft.IsHitTestVisible);
                    Assert.AreEqual(0d, minecraft.Opacity, 0.01d);
                    Assert.AreEqual(0d, ((TranslateTransform)select.RenderTransform!).X, 0.01d);
                    Assert.AreEqual(0d, ((TranslateTransform)minecraft.RenderTransform!).X, 0.01d);
                    Assert.IsTrue(scroll.IsHitTestVisible);
                    Assert.IsTrue(page.FindControl<MyExtraTextButton>("BtnSelectStart")!.Show);
                    Assert.AreEqual("forge-47.3.0", page.FindControl<TextBlock>("LabForge")!.Text);
                    Assert.IsTrue(page.FindControl<Control>("BtnForgeClear")!.IsVisible);
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceLeft_SwitchesModEntryToDisabledPromptForVanillaInstance()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-left-modable-" + Guid.NewGuid().ToString("N"));

        try
        {
            string vanillaDirectory = System.IO.Path.Combine(root, "versions", "1.20.1");
            string fabricDirectory = System.IO.Path.Combine(root, "versions", "fabric-1.20.1");
            Directory.CreateDirectory(vanillaDirectory);
            Directory.CreateDirectory(fabricDirectory);
            string vanillaJson = System.IO.Path.Combine(vanillaDirectory, "1.20.1.json");
            string fabricJson = System.IO.Path.Combine(fabricDirectory, "fabric-1.20.1.json");
            File.WriteAllText(vanillaJson, """{ "id": "1.20.1", "libraries": [] }""");
            File.WriteAllText(fabricJson, """
                {
                  "id": "fabric-1.20.1",
                  "libraries": [
                    { "name": "net.fabricmc:fabric-loader:0.16.10" }
                  ]
                }
                """);

            LaunchInstanceInfo vanilla = new("1.20.1", vanillaJson, vanillaDirectory);
            LaunchInstanceInfo fabric = new("fabric-1.20.1", fabricJson, fabricDirectory);

            session.Dispatch(() =>
            {
                PageInstanceLeft page = new();
                Window window = new()
                {
                    Width = 260,
                    Height = 520,
                    Content = page
                };

                try
                {
                    window.Show();
                    page.SetInstance(vanilla);
                    page.SelectPage(InstancePageSubType.Mods);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Assert.IsFalse(page.FindControl<MyListItem>("ItemMod")!.IsVisible);
                    Assert.IsTrue(page.FindControl<MyListItem>("ItemModDisabled")!.IsVisible);
                    Assert.AreEqual(InstancePageSubType.ModsDisabled, page.PageId);
                    Assert.IsTrue(page.FindControl<MyListItem>("ItemModDisabled")!.Checked);

                    page.SetInstance(fabric);
                    page.SelectPage(InstancePageSubType.Mods);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Assert.IsTrue(page.FindControl<MyListItem>("ItemMod")!.IsVisible);
                    Assert.IsFalse(page.FindControl<MyListItem>("ItemModDisabled")!.IsVisible);
                    Assert.AreEqual(InstancePageSubType.Mods, page.PageId);
                    Assert.IsTrue(page.FindControl<MyListItem>("ItemMod")!.Checked);
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceModDisabledRight_RendersCopiedWpfPromptAndActions()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageInstanceModDisabledRight page = new();
            Window window = new()
            {
                Width = 620,
                Height = 360,
                Content = page
            };
            bool downloadRequested = false;
            bool instanceSelectRequested = false;
            page.DownloadRequested += (_, _) => downloadRequested = true;
            page.InstanceSelectRequested += (_, _) => instanceSelectRequested = true;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                string[] text = page.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Select(block => block.Text ?? string.Empty)
                    .ToArray();
                Assert.IsTrue(text.Contains("这个版本不能安装 Mod"));
                Assert.IsTrue(text.Any(value => value.Contains("当前版本没有安装 Forge、Fabric、Quilt", StringComparison.Ordinal)));

                Click(window, page.FindControl<MyButton>("BtnDownload")!);
                Click(window, page.FindControl<MyButton>("BtnVersion")!);

                Assert.IsTrue(downloadRequested);
                Assert.IsTrue(instanceSelectRequested);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageInstanceResourceRight_ListsAndManagesLocalMods()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-resource-ui-" + Guid.NewGuid().ToString("N"));

        try
        {
            string versionDirectory = System.IO.Path.Combine(root, "versions", "1.20.1");
            string modsDirectory = System.IO.Path.Combine(root, "mods");
            Directory.CreateDirectory(versionDirectory);
            Directory.CreateDirectory(modsDirectory);
            string enabledMod = System.IO.Path.Combine(modsDirectory, "enabled.jar");
            string disabledMod = System.IO.Path.Combine(modsDirectory, "disabled.jar.disabled");
            File.WriteAllText(enabledMod, "enabled");
            File.WriteAllText(disabledMod, "disabled");
            string jsonPath = System.IO.Path.Combine(versionDirectory, "1.20.1.json");
            File.WriteAllText(jsonPath, """{ "id": "1.20.1" }""");
            LaunchInstanceInfo instance = new("1.20.1", jsonPath, versionDirectory);

            session.Dispatch(async () =>
            {
                PageInstanceResourceRight page = new();
                Window window = new()
                {
                    Width = 760,
                    Height = 520,
                    Content = page
                };
                string? openedFolder = null;
                string? status = null;
                bool downloadRequested = false;
                page.OpenFolderRequested += (_, path) => openedFolder = path;
                page.StatusMessage += (_, message) => status = message;
                page.DownloadRequested += (_, subPage) => downloadRequested = subPage == InstancePageSubType.Mods;

                try
                {
                    window.Show();
                    page.SetContext(instance, InstancePageSubType.Mods);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    StackPanel list = page.FindControl<StackPanel>("PanList")!;
                    Assert.AreEqual("Mod 列表 (2)", page.FindControl<MyCard>("PanListBack")!.Title);
                    Assert.IsFalse(page.FindControl<MyCard>("PanEmpty")!.IsVisible);
                    Assert.IsTrue(list.Children.OfType<MyLocalModItem>().Any(item => item.Title == "enabled.jar"));
                    Assert.IsTrue(list.Children.OfType<MyLocalModItem>().Any(item => item.Title == "disabled.jar"));

                    Click(window, page.FindControl<MyButton>("BtnManageOpen")!);
                    Assert.AreEqual(modsDirectory, openedFolder);

                    Click(window, page.FindControl<MyButton>("BtnManageDownload")!);
                    Assert.IsTrue(downloadRequested);

                    MyLocalModItem disabledItem = list.Children.OfType<MyLocalModItem>().Single(item => item.Title == "disabled.jar");
                    MyIconButton enableButton = disabledItem.Buttons.Single(button => Equals(button.ToolTip, "启用"));
                    Click(window, enableButton);
                    await WaitForConditionAsync(() => File.Exists(System.IO.Path.Combine(modsDirectory, "disabled.jar"))).ConfigureAwait(true);
                    Assert.AreEqual("已启用。", status);

                    page.FindControl<MySearchBox>("SearchBox")!.Text = "disabled";
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    Assert.AreEqual(1, list.Children.OfType<MyLocalModItem>().Count());
                    Assert.AreEqual("disabled.jar", list.Children.OfType<MyLocalModItem>().Single().Title);

                    MyIconButton deleteButton = list.Children.OfType<MyLocalModItem>().Single().Buttons.Single(button => Equals(button.ToolTip, "删除"));
                    Click(window, deleteButton);
                    await WaitForConditionAsync(() => !File.Exists(System.IO.Path.Combine(modsDirectory, "disabled.jar"))).ConfigureAwait(true);
                    Assert.AreEqual("项目已删除。", status);
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceResourceRight_ListsDatapacksFromSaveFolder()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-datapack-resource-ui-" + Guid.NewGuid().ToString("N"));

        try
        {
            string worldDirectory = System.IO.Path.Combine(root, "saves", "Modern World");
            string datapacksDirectory = System.IO.Path.Combine(worldDirectory, "datapacks");
            Directory.CreateDirectory(datapacksDirectory);
            File.WriteAllText(System.IO.Path.Combine(datapacksDirectory, "zip-pack.zip"), "zip");
            Directory.CreateDirectory(System.IO.Path.Combine(datapacksDirectory, "folder-pack"));

            session.Dispatch(() =>
            {
                PageInstanceResourceRight page = new();
                Window window = new()
                {
                    Width = 760,
                    Height = 520,
                    Content = page
                };

                try
                {
                    window.Show();
                    page.SetDataPackFolder(worldDirectory);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    StackPanel list = page.FindControl<StackPanel>("PanList")!;
                    Assert.AreEqual("数据包 列表 (2)", page.FindControl<MyCard>("PanListBack")!.Title);
                    Assert.IsTrue(list.Children.OfType<MyLocalModItem>().Any(item => item.Title == "zip-pack.zip"));
                    Assert.IsTrue(list.Children.OfType<MyLocalModItem>().Any(item => item.Title == "folder-pack"));
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceScreenshotRight_UsesCopiedWpfGalleryAndActions()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-screenshot-ui-" + Guid.NewGuid().ToString("N"));

        try
        {
            string versionDirectory = System.IO.Path.Combine(root, "versions", "1.20.1");
            string screenshotDirectory = System.IO.Path.Combine(root, "screenshots");
            Directory.CreateDirectory(versionDirectory);
            Directory.CreateDirectory(screenshotDirectory);
            string screenshotPath = System.IO.Path.Combine(screenshotDirectory, "shot.png");
            File.WriteAllBytes(
                screenshotPath,
                Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="));
            LaunchInstanceInfo instance = new("1.20.1", System.IO.Path.Combine(versionDirectory, "1.20.1.json"), versionDirectory);

            session.Dispatch(() =>
            {
                PageInstanceScreenshotRight page = new();
                Window window = new()
                {
                    Width = 720,
                    Height = 480,
                    Content = page
                };
                string? openedFolder = null;
                string? openedFile = null;
                string? status = null;
                page.OpenFolderRequested += (_, path) => openedFolder = path;
                page.OpenFileRequested += (_, path) => openedFile = path;
                page.StatusMessage += (_, message) => status = message;

                try
                {
                    window.Show();
                    page.SetInstance(instance);
                    page.Reload().GetAwaiter().GetResult();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Assert.IsFalse(page.FindControl<MyCard>("PanNoPic")!.IsVisible);
                    Assert.IsTrue(page.FindControl<StackPanel>("PanContent")!.IsVisible);
                    Assert.AreEqual(1, page.FindControl<WrapPanel>("PanList")!.Children.OfType<MyCard>().Count());

                    Click(window, page.FindControl<MyButton>("BtnOpenFolder")!);
                    Assert.AreEqual(screenshotDirectory, openedFolder);

                    MyIconTextButton openButton = page.GetVisualDescendants()
                        .OfType<MyIconTextButton>()
                        .Single(button => button.Name == "BtnOpen");
                    Click(window, openButton);
                    Assert.AreEqual(screenshotPath, openedFile);

                    MyIconTextButton deleteButton = page.GetVisualDescendants()
                        .OfType<MyIconTextButton>()
                        .Single(button => button.Name == "BtnDelete");
                    Click(window, deleteButton);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Assert.IsFalse(File.Exists(screenshotPath));
                    Assert.AreEqual("截图已删除。", status);
                    Assert.IsTrue(page.FindControl<MyCard>("PanNoPic")!.IsVisible);
                    Assert.IsFalse(page.FindControl<StackPanel>("PanContent")!.IsVisible);
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceSavesRight_UsesCopiedWpfListAndActions()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-saves-ui-" + Guid.NewGuid().ToString("N"));

        try
        {
            string versionDirectory = System.IO.Path.Combine(root, "versions", "1.20.1");
            string savesDirectory = System.IO.Path.Combine(root, "saves");
            string worldDirectory = System.IO.Path.Combine(savesDirectory, "New World");
            Directory.CreateDirectory(versionDirectory);
            Directory.CreateDirectory(worldDirectory);
            File.WriteAllText(System.IO.Path.Combine(versionDirectory, "1.20.1.json"), "{}");
            LaunchInstanceInfo instance = new("1.20.1", System.IO.Path.Combine(versionDirectory, "1.20.1.json"), versionDirectory);

            session.Dispatch(() =>
            {
                PageInstanceSavesRight page = new();
                Window window = new()
                {
                    Width = 720,
                    Height = 480,
                    Content = page
                };
                string? openedFolder = null;
                string? selectedSave = null;
                string? quickPlayWorld = null;
                string? status = null;
                page.OpenFolderRequested += (_, path) => openedFolder = path;
                page.SaveDetailsRequested += (_, path) => selectedSave = path;
                page.QuickPlayRequested += (_, world) => quickPlayWorld = world;
                page.StatusMessage += (_, message) => status = message;

                try
                {
                    window.Show();
                    page.SetInstance(instance);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Assert.IsFalse(page.FindControl<MyCard>("PanNoWorld")!.IsVisible);
                    Assert.IsTrue(page.FindControl<StackPanel>("PanContent")!.IsVisible);
                    MyListItem item = page.GetVisualDescendants().OfType<MyListItem>().Single(listItem => listItem.Title == "New World");
                    Assert.AreEqual("存档列表 (1)", page.FindControl<MyCard>("PanListBack")!.Title);

                    Click(window, page.GetVisualDescendants().OfType<MyButton>().First(button => button.Text == "打开存档文件夹"));
                    Assert.AreEqual(savesDirectory, openedFolder);

                    Click(window, item);
                    Assert.AreEqual(worldDirectory, selectedSave);

                    MyIconButton launchButton = item.Buttons.Single(button => Equals(button.ToolTip, "进入存档"));
                    Click(window, launchButton);
                    Assert.AreEqual("New World", quickPlayWorld);

                    MyIconButton deleteButton = item.Buttons.Single(button => Equals(button.ToolTip, "删除"));
                    Click(window, deleteButton);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Assert.IsFalse(Directory.Exists(worldDirectory));
                    Assert.AreEqual("存档已删除。", status);
                    Assert.IsTrue(page.FindControl<MyCard>("PanNoWorld")!.IsVisible);
                }
                finally
                {
                    page.Dispose();
                    window.Close();
                }
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceSavesInfoRight_LoadsCopiedWpfDetailsAndSettings()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-save-info-ui-" + Guid.NewGuid().ToString("N"));

        try
        {
            string worldDirectory = System.IO.Path.Combine(root, "saves", "New World");
            Directory.CreateDirectory(worldDirectory);
            WriteLevelDat(worldDirectory, data =>
            {
                data.Add(new fNbt.NbtString("LevelName", "New World"));
                data.Add(new fNbt.NbtLong("RandomSeed", 123456789L));
                data.Add(new fNbt.NbtLong("LastPlayed", DateTimeOffset.Parse("2026-01-02T03:04:05Z").ToUnixTimeMilliseconds()));
                data.Add(new fNbt.NbtLong("Time", 2400L));
                data.Add(new fNbt.NbtInt("DataVersion", 1343));
                data.Add(new fNbt.NbtInt("GameType", 1));
                data.Add(new fNbt.NbtInt("SpawnX", 12));
                data.Add(new fNbt.NbtInt("SpawnY", 65));
                data.Add(new fNbt.NbtInt("SpawnZ", -34));
                data.Add(new fNbt.NbtByte("allowCommands", 1));
                data.Add(new fNbt.NbtByte("Difficulty", 2));
                data.Add(new fNbt.NbtByte("DifficultyLocked", 0));
                fNbt.NbtCompound version = new("Version");
                version.Add(new fNbt.NbtString("Name", "1.12.2"));
                version.Add(new fNbt.NbtInt("Id", 1343));
                data.Add(version);
            });

            session.Dispatch(async () =>
            {
                PageInstanceSavesInfoRight page = new();
                Window window = new()
                {
                    Width = 720,
                    Height = 480,
                    Content = page
                };
                try
                {
                    window.Show();
                    await page.SetSaveFolderAsync(worldDirectory).ConfigureAwait(true);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    string[] text = page.GetVisualDescendants()
                        .OfType<TextBlock>()
                        .Select(block => block.Text ?? string.Empty)
                        .ToArray();
                    Assert.IsTrue(text.Contains("游戏版本"));
                    Assert.IsTrue(text.Contains("1.12.2 (1343)"));
                    Assert.IsTrue(text.Contains("存档名称"));
                    Assert.IsTrue(text.Contains("New World"));
                    Assert.IsTrue(text.Contains("世界种子"));
                    Assert.IsTrue(text.Contains("123456789"));
                    Assert.IsTrue(text.Contains("出生点"));
                    Assert.IsTrue(text.Contains("12 / 65 / -34"));
                    Assert.IsTrue(text.Contains("游戏模式"));
                    Assert.IsTrue(text.Contains("创造模式"));
                    Assert.IsTrue(text.Contains("游玩时间"));
                    Assert.IsTrue(text.Contains("2 分钟 0 秒"));

                    Assert.IsTrue(page.FindControl<MyCard>("PanContent")!.IsVisible);
                    Assert.IsTrue(page.FindControl<MyCard>("PanSettings")!.IsVisible);
                    Assert.AreEqual("版本信息", page.FindControl<MyCard>("PanContent")!.Title);
                    Assert.AreEqual("存档设置", page.FindControl<MyCard>("PanSettings")!.Title);
                    Assert.AreEqual(2, page.GetVisualDescendants().OfType<MyComboBox>().Count());
                    Assert.IsTrue(page.GetVisualDescendants().OfType<MyCheckBox>().Any(checkBox => checkBox.Text == "锁定难度"));
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceSavesInfoRight_ShowsDatapackEntryForModernSave()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-save-datapack-ui-" + Guid.NewGuid().ToString("N"));

        try
        {
            string worldDirectory = System.IO.Path.Combine(root, "saves", "Modern World");
            Directory.CreateDirectory(worldDirectory);
            WriteLevelDat(worldDirectory, data =>
            {
                data.Add(new fNbt.NbtString("LevelName", "Modern World"));
                data.Add(new fNbt.NbtLong("LastPlayed", 0));
                data.Add(new fNbt.NbtLong("Time", 0));
                data.Add(new fNbt.NbtInt("DataVersion", 1443));
                data.Add(new fNbt.NbtInt("GameType", 0));
                data.Add(new fNbt.NbtByte("allowCommands", 0));
                data.Add(new fNbt.NbtByte("Difficulty", 1));
                fNbt.NbtCompound version = new("Version");
                version.Add(new fNbt.NbtString("Name", "1.13"));
                version.Add(new fNbt.NbtInt("Id", 1443));
                data.Add(version);
            });

            session.Dispatch(async () =>
            {
                PageInstanceSavesInfoRight page = new();
                Window window = new()
                {
                    Width = 720,
                    Height = 480,
                    Content = page
                };
                string? datapackFolder = null;
                page.DatapackManageRequested += (_, folder) => datapackFolder = folder;

                try
                {
                    window.Show();
                    await page.SetSaveFolderAsync(worldDirectory).ConfigureAwait(true);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    MyButton button = page.GetVisualDescendants().OfType<MyButton>().Single(button => button.Text == "管理数据包");
                    Click(window, button);

                    Assert.AreEqual(worldDirectory, datapackFolder);
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteServersDat(string root)
    {
        fNbt.NbtCompound rootTag = new("");
        fNbt.NbtList servers = new("servers", fNbt.NbtTagType.Compound)
        {
            new fNbt.NbtCompound
            {
                new fNbt.NbtString("name", "Hypixel"),
                new fNbt.NbtString("ip", "mc.hypixel.net")
            }
        };
        rootTag.Add(servers);
        fNbt.NbtFile file = new(rootTag);
        using FileStream stream = File.Create(System.IO.Path.Combine(root, "servers.dat"));
        file.SaveToStream(stream, fNbt.NbtCompression.GZip);
    }

    private static void WriteLevelDat(string folderPath, Action<fNbt.NbtCompound> populateData)
    {
        fNbt.NbtCompound root = new("");
        fNbt.NbtCompound data = new("Data");
        populateData(data);
        root.Add(data);

        fNbt.NbtFile file = new(root);
        using FileStream stream = File.Create(System.IO.Path.Combine(folderPath, "level.dat"));
        file.SaveToStream(stream, fNbt.NbtCompression.GZip);
    }

    private static void WaitForCondition(Func<bool> condition)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
                return;

            Thread.Sleep(25);
        }

        Assert.Fail("Timed out waiting for condition.");
    }

    private static async Task WaitForConditionAsync(Func<bool> condition)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
                return;

            await Task.Delay(25).ConfigureAwait(true);
        }

        Assert.Fail("Timed out waiting for condition.");
    }

    [TestMethod]
    public void PageLaunchLeft_PreservesLoginAndLaunchExtensionSurfaces()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageLaunchLeft page = new();
            Window window = new()
            {
                Width = 420,
                Height = 360,
                Content = page
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                TextBlock loginPage = new() { Text = "登录分页" };
                page.SetLoginPage(loginPage, animate: false);
                page.SetInstances([new LaunchInstanceInfo("1.20.1", @"D:\Minecraft\versions\1.20.1\1.20.1.json", @"D:\Minecraft\versions\1.20.1")]);

                Assert.AreSame(loginPage, page.CurrentLoginPage);
                Assert.AreEqual("1.20.1", page.SelectedInstance!.Name);
                Assert.AreEqual("启动游戏", page.FindControl<MyButton>("BtnLaunch")!.Text);
                Assert.IsFalse(page.FindControl<MyButton>("BtnLaunch")!.IsEnabled);
                Assert.IsTrue(page.FindControl<Control>("BtnMore")!.IsVisible);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageLaunchLeft_AnimatedLoginPageSwitchMatchesWpfTiming()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageLaunchLeft page = new();
            TextBlock first = new() { Text = "第一个登录页" };
            TextBlock second = new() { Text = "第二个登录页" };
            Grid loginHost = page.FindControl<Grid>("PanLogin")!;

            page.SetLoginPage(first, animate: false);
            page.SetLoginPage(second, animate: true);

            Assert.AreSame(second, page.CurrentLoginPage);
            Assert.AreSame(first, loginHost.Children.Single());

            ModAnimation.AdvanceForTesting(99);
            Assert.AreSame(first, loginHost.Children.Single());

            ModAnimation.AdvanceForTesting(1);
            Assert.AreSame(second, loginHost.Children.Single());

            ModAnimation.AdvanceUntilIdleForTesting();
            Assert.AreEqual(1d, loginHost.Opacity, 0.001d);
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageLaunchLeft_FollowsWpfLaunchButtonStateRules()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageLaunchLeft page = new();
            LaunchInstanceInfo instance = new("1.20.1", @"D:\Minecraft\versions\1.20.1\1.20.1.json", @"D:\Minecraft\versions\1.20.1");

            page.SetInstanceLoading(isLoading: true);
            Assert.AreEqual("正在加载", page.FindControl<MyButton>("BtnLaunch")!.Text);
            Assert.IsFalse(page.FindControl<MyButton>("BtnLaunch")!.IsEnabled);
            Assert.IsFalse(page.FindControl<Control>("BtnInstance")!.IsEnabled);
            Assert.IsTrue(page.FindControl<MyLoading>("LoadInstanceCheck")!.IsVisible);
            Assert.AreEqual(
                MyLoading.MyLoadingState.Run,
                page.FindControl<MyLoading>("LoadInstanceCheck")!.State.LoadingState);

            page.SetInstances([instance]);
            Assert.AreEqual("启动游戏", page.FindControl<MyButton>("BtnLaunch")!.Text);
            Assert.IsFalse(page.FindControl<MyButton>("BtnLaunch")!.IsEnabled);
            Assert.IsFalse(page.FindControl<MyLoading>("LoadInstanceCheck")!.IsVisible);
            Assert.AreEqual(
                MyLoading.MyLoadingState.Stop,
                page.FindControl<MyLoading>("LoadInstanceCheck")!.State.LoadingState);

            page.SetSelectedProfilePresent(true);
            Assert.IsTrue(page.FindControl<MyButton>("BtnLaunch")!.IsEnabled);

            page.SetInstances([]);
            Assert.AreEqual("下载游戏", page.FindControl<MyButton>("BtnLaunch")!.Text);
            Assert.IsTrue(page.FindControl<MyButton>("BtnLaunch")!.IsEnabled);
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageInstanceManageRight_RendersWpfCopiedTextAndDefaultSelections()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-overall-ui-" + Guid.NewGuid().ToString("N"));

        try
        {
            string versionDirectory = System.IO.Path.Combine(root, "versions", "1.20.1");
            Directory.CreateDirectory(versionDirectory);
            string jsonPath = System.IO.Path.Combine(versionDirectory, "1.20.1.json");
            File.WriteAllText(jsonPath, "{\"id\":\"1.20.1\"}");
            LaunchInstanceInfo instance = new("1.20.1", jsonPath, versionDirectory);

            session.Dispatch(() =>
            {
                PageInstanceManageRight page = new();
                Window window = new()
                {
                    Width = 720,
                    Height = 520,
                    Content = page
                };

                try
                {
                    window.Show();
                    page.SetInstance(instance);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    MyListItem displayItem = page.GetVisualDescendants().OfType<MyListItem>().First(item => item.Title == "1.20.1");
                    Assert.AreEqual("1.20.1", DisplayText(displayItem.FindControl<TextBlock>("LabTitle")!));
                    Assert.IsTrue(page.GetVisualDescendants().OfType<MyListItem>().Any(item =>
                        item.Title == "Minecraft" &&
                        item.Info == "1.20.1" &&
                        DisplayText(item.FindControl<TextBlock>("LabTitle")!) == "Minecraft"));
                    Assert.IsTrue(page.GetVisualDescendants().OfType<MyListItem>().Any(item =>
                        item.Title == "启动次数" &&
                        item.Info == "从未启动" &&
                        item.Logo.EndsWith("RedstoneLampOff.png", StringComparison.OrdinalIgnoreCase)));
                    Assert.AreEqual("自动", page.FindControl<MyComboBox>("ComboDisplayLogo")!.Text);
                    Assert.AreEqual("自动", page.FindControl<MyComboBox>("ComboDisplayType")!.Text);
                    Assert.IsFalse(page.FindControl<MyButton>("BtnFolderMods")!.IsVisible);
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageInstanceManageRight_RendersDetectedLoaderInfoLikeWpf()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-instance-overall-loader-" + Guid.NewGuid().ToString("N"));

        try
        {
            string versionDirectory = System.IO.Path.Combine(root, "versions", "1.20.1");
            Directory.CreateDirectory(versionDirectory);
            string jsonPath = System.IO.Path.Combine(versionDirectory, "1.20.1.json");
            File.WriteAllText(jsonPath, """
                {
                  "id": "1.20.1",
                  "libraries": [
                    { "name": "net.fabricmc:fabric-loader:0.16.10" },
                    { "name": "net.minecraftforge:forge:1.20.1-47.3.0" }
                  ]
                }
                """);
            InstanceMetadataStore.SaveAsync(
                versionDirectory,
                new InstanceMetadata
                {
                    LaunchCount = 7,
                    ModpackVersion = "2.4.1"
                }).GetAwaiter().GetResult();
            LaunchInstanceInfo instance = new("1.20.1", jsonPath, versionDirectory);

            session.Dispatch(() =>
            {
                PageInstanceManageRight page = new();
                Window window = new()
                {
                    Width = 720,
                    Height = 520,
                    Content = page
                };

                try
                {
                    window.Show();
                    page.SetInstance(instance);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    MyListItem forge = page.GetVisualDescendants().OfType<MyListItem>().Single(item => item.Title == "Forge");
                    MyListItem fabric = page.GetVisualDescendants().OfType<MyListItem>().Single(item => item.Title == "Fabric");

                    Assert.AreEqual("47.3.0", forge.Info);
                    Assert.AreEqual("0.16.10", fabric.Info);
                    Assert.IsTrue(forge.Logo.EndsWith("Anvil.png", StringComparison.OrdinalIgnoreCase));
                    Assert.IsTrue(fabric.Logo.EndsWith("Fabric.png", StringComparison.OrdinalIgnoreCase));
                    Assert.IsTrue(page.GetVisualDescendants().OfType<MyListItem>().Any(item =>
                        item.Title == "启动次数" &&
                        item.Info == "已启动 7 次" &&
                        item.Logo.EndsWith("RedstoneLampOn.png", StringComparison.OrdinalIgnoreCase)));
                    Assert.IsTrue(page.GetVisualDescendants().OfType<MyListItem>().Any(item =>
                        item.Title == "整合包版本" &&
                        item.Info == "2.4.1"));
                    Assert.IsTrue(page.FindControl<MyButton>("BtnFolderMods")!.IsVisible);
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageLaunchLeft_CancelRestoresInputSurface()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageLaunchLeft page = new();
            LaunchInstanceInfo instance = new("1.20.1", @"D:\Minecraft\versions\1.20.1\1.20.1.json", @"D:\Minecraft\versions\1.20.1");
            Window window = new()
            {
                Width = 420,
                Height = 360,
                Content = page
            };
            bool cancelRequested = false;
            page.CancelLaunchRequested += (_, _) => cancelRequested = true;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                page.ShowLaunching(instance);
                Assert.IsTrue(page.IsLaunchInProgress);
                Assert.IsTrue(page.FindControl<Grid>("PanLaunching")!.IsVisible);
                Assert.IsFalse(page.FindControl<Grid>("PanLaunching")!.IsHitTestVisible);
                Assert.AreEqual(
                    MyLoading.MyLoadingState.Run,
                    page.FindControl<MyLoading>("LoadLaunching")!.State.LoadingState);

                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.IsTrue(page.FindControl<Grid>("PanLaunching")!.IsHitTestVisible);
                Assert.AreEqual(0d, page.FindControl<Grid>("PanInput")!.Opacity, 0.001d);
                Assert.AreEqual(
                    1.2d,
                    ((ScaleTransform)page.FindControl<Grid>("PanInput")!.RenderTransform!).ScaleX,
                    0.001d);

                Click(window, page.FindControl<MyButton>("BtnCancel")!);

                Assert.IsTrue(cancelRequested);
                Assert.IsFalse(page.IsLaunchInProgress);
                Assert.IsFalse(page.FindControl<Grid>("PanInput")!.IsHitTestVisible);
                Assert.AreEqual(
                    MyLoading.MyLoadingState.Stop,
                    page.FindControl<MyLoading>("LoadLaunching")!.State.LoadingState);

                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.IsTrue(page.FindControl<Grid>("PanLaunching")!.IsVisible);
                Assert.AreEqual(0d, page.FindControl<Grid>("PanLaunching")!.Opacity, 0.001d);
                Assert.AreEqual(
                    0.8d,
                    ((ScaleTransform)page.FindControl<Grid>("PanLaunching")!.RenderTransform!).ScaleX,
                    0.001d);
                Assert.IsTrue(page.FindControl<Grid>("PanInput")!.IsVisible);
                Assert.IsTrue(page.FindControl<Grid>("PanInput")!.IsHitTestVisible);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageLaunchLeft_UsesAvaloniaRelativeTransformOriginsForWpfPivots()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageLaunchLeft page = new();
            Window window = new()
            {
                Width = 420,
                Height = 360,
                Content = page
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual(
                    new RelativePoint(0.5d, -0.2d, RelativeUnit.Relative),
                    page.FindControl<TextBlock>("LabVersion")!.RenderTransformOrigin);
                Assert.AreEqual(
                    new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative),
                    page.FindControl<Grid>("PanInput")!.RenderTransformOrigin);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageLaunchLeft_GuardsLaunchLikeWpf()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-launch-guard-" + Guid.NewGuid().ToString("N"));

        try
        {
            session.Dispatch(() =>
            {
                string instanceDirectory = System.IO.Path.Combine(root, "versions", "1.20.1");
                Directory.CreateDirectory(instanceDirectory);
                LaunchInstanceInfo instance = new(
                    "1.20.1",
                    System.IO.Path.Combine(instanceDirectory, "1.20.1.json"),
                    instanceDirectory);
                PageLaunchLeft page = new();
                int launchCount = 0;
                int statusCount = 0;
                page.LaunchRequested += (_, _) => launchCount++;
                page.StatusMessage += (_, _) => statusCount++;
                page.SetInstances([instance]);
                page.SetSelectedProfilePresent(true);
                page.CanLaunchByPageState = () => false;

                page.LaunchButtonClick();
                Assert.AreEqual(0, launchCount);

                page.CanLaunchByPageState = () => true;
                File.WriteAllText(instanceDirectory + ".pclignore", "");
                page.LaunchButtonClick();

                Assert.AreEqual(0, launchCount);
                Assert.AreEqual(1, statusCount);
            }, CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageLoginProfile_SelectsProfileAndSkinPageDisplaysIt()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            string skinPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "pcl-headless-skin-" + Guid.NewGuid().ToString("N") + ".png");
            using (Stream source = Avalonia.Platform.AssetLoader.Open(new Uri("avares://PCL.Desktop/Assets/icon.png")))
            using (FileStream target = File.Create(skinPath))
                source.CopyTo(target);
            LoginProfileInfo profile = new(
                "Steve",
                "离线登录",
                LaunchLoginProfileKind.Offline,
                SkinAddress: skinPath);
            PageLoginProfile profilePage = new();
            Window window = new()
            {
                Width = 320,
                Height = 260,
                Content = profilePage
            };
            LoginProfileInfo? selected = null;
            profilePage.ProfileSelected += (_, value) => selected = value;

            try
            {
                profilePage.SetProfiles([profile]);
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();

                MySkin profileHead = profilePage.GetVisualDescendants().OfType<MySkin>().Single();
                Assert.IsNotNull(profileHead.FindControl<Image>("ImgBack")!.Source);

                Click(window, FindVisual<MyListItem>(profilePage)!);

                Assert.AreEqual(profile, selected);

                PageLoginProfileSkin skinPage = new();
                skinPage.SetProfile(profile);
                window.Content = skinPage;
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.AreEqual("Steve", skinPage.FindControl<TextBlock>("TextName")!.Text);
                Assert.IsFalse(skinPage.FindControl<MyIconButton>("BtnEdit")!.IsVisible);
                Assert.IsNotNull(skinPage.FindControl<MySkin>("Skin")!.FindControl<Image>("ImgBack")!.Source);
            }
            finally
            {
                window.Close();
                File.Delete(skinPath);
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MainWindow_WiresLaunchLoginPagesInsteadOfPlaceholders()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                PageLaunchLeft launchPage = FindVisual<PageLaunchLeft>(window)!;

                launchPage.RefreshPage(anim: true, PageLaunchLeft.LaunchLoginPageType.Ms);
                Assert.IsInstanceOfType<PageLoginMs>(launchPage.CurrentLoginPage);

                launchPage.RefreshPage(anim: true, PageLaunchLeft.LaunchLoginPageType.Auth);
                Assert.IsInstanceOfType<PageLoginAuth>(launchPage.CurrentLoginPage);

                launchPage.RefreshPage(anim: true, PageLaunchLeft.LaunchLoginPageType.Offline);
                Assert.IsInstanceOfType<PageLoginOffline>(launchPage.CurrentLoginPage);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MainWindow_ProfileCreateUsesAccountTypeDialog()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                PageLaunchLeft launchPage = FindVisual<PageLaunchLeft>(window)!;
                launchPage.RefreshPage(anim: true, PageLaunchLeft.LaunchLoginPageType.Profile);
                PageLoginProfile profilePage = (PageLoginProfile)launchPage.CurrentLoginPage!;

                Click(window, profilePage.FindControl<MyIconButton>("BtnNew")!);
                MyMsgSelect dialog = FindVisual<MyMsgSelect>(window)!;

                Assert.IsNotNull(dialog);
                Assert.IsTrue(window.FindControl<BlurBorder>("PanMsgBackground")!.IsVisible);
                Assert.IsTrue(dialog.Opacity < 1d);
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual(1d, dialog.Opacity, 0.001d);
                Assert.AreEqual(3, dialog.Items.Count);
                Assert.IsFalse(dialog.FindControl<MyButton>("Btn1")!.IsEnabled);

                Click(window, dialog.Items[1]);
                Assert.IsTrue(dialog.FindControl<MyButton>("Btn1")!.IsEnabled);

                Click(window, dialog.FindControl<MyButton>("Btn1")!);
                Assert.IsTrue(dialog.IsClosing);
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.IsInstanceOfType<PageLoginAuth>(launchPage.CurrentLoginPage);
                Assert.IsFalse(window.FindControl<BlurBorder>("PanMsgBackground")!.IsVisible);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MainWindow_MicrosoftLoginClickShowsUserVisibleFeedback()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string? previousClientId = Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID");
        string? previousShortClientId = Environment.GetEnvironmentVariable("MS_CLIENT_ID");
        Environment.SetEnvironmentVariable("PCL_MS_CLIENT_ID", null);
        Environment.SetEnvironmentVariable("MS_CLIENT_ID", null);

        session.Dispatch(() =>
        {
            MainWindow window = new();

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                PageLaunchLeft launchPage = FindVisual<PageLaunchLeft>(window)!;
                launchPage.RefreshPage(anim: true, PageLaunchLeft.LaunchLoginPageType.Ms);
                PageLoginMs loginPage = (PageLoginMs)launchPage.CurrentLoginPage!;
                ModAnimation.AdvanceUntilIdleForTesting();
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                launchPage.SetLoginPage(
                    loginPage,
                    animate: false,
                    PageLaunchLeft.LaunchLoginPageType.Ms);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                loginPage.RequestLogin();
                MyMsgText dialog = FindVisual<MyMsgText>(window)!;

                Assert.IsNotNull(dialog);
                Assert.IsTrue(window.FindControl<BlurBorder>("PanMsgBackground")!.IsVisible);
                Assert.AreEqual("开始登录", loginPage.FindControl<MyButton>("BtnLogin")!.Text);
                Assert.IsFalse(loginPage.IsLoggingIn);
                Assert.IsTrue(dialog.FindControl<TextBlock>("LabTitle")!.Text!.Contains("登录配置缺失", StringComparison.Ordinal));
                Assert.IsTrue(dialog.FindControl<TextBlock>("LabCaption")!.Text!.Contains("缺少 Microsoft 登录配置", StringComparison.Ordinal));

                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual(1d, dialog.Opacity, 0.001d);

                InvokePrivateMethod(dialog, "CloseWithResult", 1);
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.IsFalse(window.FindControl<BlurBorder>("PanMsgBackground")!.IsVisible);
            }
            finally
            {
                window.Close();
                Environment.SetEnvironmentVariable("PCL_MS_CLIENT_ID", previousClientId);
                Environment.SetEnvironmentVariable("MS_CLIENT_ID", previousShortClientId);
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void MainWindow_MicrosoftLoginCompletesInMainProgramWithoutOnlinePlugin()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string? previousClientId = Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID");
        Environment.SetEnvironmentVariable("PCL_MS_CLIENT_ID", "test-client-id");
        FakeMicrosoftMinecraftAuthService authService = new();

        try
        {
            session.Dispatch(async () =>
            {
                MainWindow window = new(authService);
                try
                {
                    window.Show();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    PageLaunchLeft launchPage = FindVisual<PageLaunchLeft>(window)!;
                    launchPage.RefreshPage(anim: true, PageLaunchLeft.LaunchLoginPageType.Ms);
                    PageLoginMs loginPage = (PageLoginMs)launchPage.CurrentLoginPage!;
                    ModAnimation.AdvanceUntilIdleForTesting();
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    launchPage.SetLoginPage(
                        loginPage,
                        animate: false,
                        PageLaunchLeft.LaunchLoginPageType.Ms);
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    loginPage.RequestLogin();
                    await WaitForConditionAsync(() => FindVisual<MyMsgLogin>(window) is not null);
                    MyMsgLogin dialog = FindVisual<MyMsgLogin>(window)!;
                    Assert.AreEqual("ABCD-EFGH", dialog.UserCode);
                    Assert.AreEqual("https://microsoft.com/link?otc=ABCD-EFGH", dialog.Website);
                    Assert.IsTrue(loginPage.IsLoggingIn);
                    Assert.AreEqual("test-client-id", authService.RequestedClientId);

                    authService.Completion.SetResult(new MicrosoftMinecraftLoginResult(
                        "Steve",
                        "0123456789abcdef0123456789abcdef",
                        "minecraft-access",
                        "microsoft-refresh",
                        "https://textures.example/skin.png",
                        true));
                    await WaitForConditionAsync(() => launchPage.CurrentLoginPage is PageLoginProfileSkin);

                    PageLoginProfileSkin profileSkin = (PageLoginProfileSkin)launchPage.CurrentLoginPage!;
                    Assert.AreEqual("Steve", profileSkin.Profile?.Username);
                    Assert.AreEqual(LaunchLoginProfileKind.Microsoft, profileSkin.Profile?.Kind);
                    Assert.AreEqual("minecraft-access", profileSkin.Profile?.AccessToken);
                    Assert.AreEqual("microsoft-refresh", profileSkin.Profile?.RefreshToken);
                    Assert.AreEqual("Steve", profileSkin.FindControl<TextBlock>("TextName")!.Text);
                    Assert.IsFalse(loginPage.IsLoggingIn);
                    Assert.IsNotNull(FindVisual<MyMsgText>(window));
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            Environment.SetEnvironmentVariable("PCL_MS_CLIENT_ID", previousClientId);
        }
    }

    [TestMethod]
    public void MessageDialogs_UseAvaloniaRelativeLeftCenterTransformOrigin()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyMsgInput input = new();
            MyMsgMarkdown markdown = new();
            MyMsgSelect select = new();
            MyMsgText text = new();

            Assert.AreEqual(new RelativePoint(0d, 0.5d, RelativeUnit.Relative), input.RenderTransformOrigin);
            Assert.AreEqual(new RelativePoint(0d, 0.5d, RelativeUnit.Relative), markdown.RenderTransformOrigin);
            Assert.AreEqual(new RelativePoint(0d, 0.5d, RelativeUnit.Relative), select.RenderTransformOrigin);
            Assert.AreEqual(new RelativePoint(0d, 0.5d, RelativeUnit.Relative), text.RenderTransformOrigin);
            Assert.AreEqual(BoxShadows.Parse("0 4 28 0 #663c3c3c"), input.FindControl<Border>("PanBorder")!.BoxShadow);
            Assert.AreEqual(BoxShadows.Parse("0 4 28 0 #cc3c3c3c"), markdown.FindControl<Border>("PanBorder")!.BoxShadow);
            Assert.AreEqual(BoxShadows.Parse("0 4 28 0 #663c3c3c"), select.FindControl<Border>("PanBorder")!.BoxShadow);
            Assert.AreEqual(BoxShadows.Parse("0 4 28 0 #663c3c3c"), text.FindControl<Border>("PanBorder")!.BoxShadow);
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyMsgText_UsesWpfThreeButtonWarningAndActionContract()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            int actionCount = 0;
            MyMsgText dialog = new();
            dialog.Configure(
                title: "删除档案",
                caption: "此操作不可撤销。",
                primaryButton: "删除",
                secondaryButton: "取消",
                thirdButton: "查看详情",
                isWarn: true,
                primaryAction: () => actionCount++);
            Window window = new()
            {
                Width = 620,
                Height = 320,
                Content = dialog
            };

            int closedResult = 0;
            dialog.Closed += (_, e) => closedResult = e.Result;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                TextBlock title = dialog.FindControl<TextBlock>("LabTitle")!;
                Rectangle line = dialog.FindControl<Rectangle>("ShapeLine")!;
                MyButton primary = dialog.FindControl<MyButton>("Btn1")!;
                MyButton secondary = dialog.FindControl<MyButton>("Btn2")!;
                MyButton third = dialog.FindControl<MyButton>("Btn3")!;

                Assert.AreEqual("删除档案", title.Text);
                Assert.AreEqual("此操作不可撤销。", dialog.FindControl<TextBlock>("LabCaption")!.Text);
                Assert.AreEqual(MyButton.ColorState.Red, primary.ColorType);
                Assert.IsTrue(secondary.IsVisible);
                Assert.IsTrue(third.IsVisible);
                Assert.AreEqual(RequiredBrush("ColorBrushRedLight").Color, ((SolidColorBrush)title.Foreground!).Color);
                Assert.AreEqual(RequiredBrush("ColorBrushRedLight").Color, ((SolidColorBrush)line.Fill!).Color);

                dialog.BeginShowAnimation();
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual(1d, dialog.Opacity, 0.01d);

                Click(window, primary);
                Assert.AreEqual(1, actionCount);
                Assert.AreEqual(0, closedResult);

                Click(window, third);
                Assert.IsTrue(dialog.IsClosing);
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual(3, closedResult);

                MyMsgText highlightDialog = new();
                highlightDialog.Configure("普通提示", "需要确认。", secondaryButton: "取消");
                Assert.AreEqual(MyButton.ColorState.Highlight, highlightDialog.FindControl<MyButton>("Btn1")!.ColorType);

                MyMsgText thirdOnlyDialog = new();
                thirdOnlyDialog.Configure("普通提示", "第三按钮不触发强调。", thirdButton: "更多");
                Assert.AreEqual(MyButton.ColorState.Normal, thirdOnlyDialog.FindControl<MyButton>("Btn1")!.ColorType);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyMsgInput_UsesWpfLayoutValidationAndOpenCloseAnimations()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            InlineValidator<string> validator = new();
            validator.RuleFor(static text => text).MinimumLength(3).WithMessage("至少 3 个字符");
            MyMsgInput dialog = new();
            dialog.Configure(
                title: "创建档案",
                text: "请输入一个用于启动游戏的名称。",
                content: "ab",
                hintText: "玩家名",
                primaryButton: "创建",
                secondaryButton: "取消",
                validateRules: [validator]);
            Window window = new()
            {
                Width = 620,
                Height = 320,
                Content = dialog
            };

            string? closedResult = "not-closed";
            dialog.Closed += (_, e) => closedResult = e.Result;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                MyTextBox input = dialog.FindControl<MyTextBox>("TextArea")!;
                MyButton confirm = dialog.FindControl<MyButton>("Btn1")!;
                MyButton cancel = dialog.FindControl<MyButton>("Btn2")!;
                Assert.AreEqual("创建档案", dialog.FindControl<TextBlock>("LabTitle")!.Text);
                Assert.AreEqual("请输入一个用于启动游戏的名称。", dialog.FindControl<TextBlock>("LabText")!.Text);
                Assert.AreEqual("玩家名", input.HintText);
                Assert.IsFalse(input.IsValidated);
                Assert.IsFalse(confirm.IsEnabled);
                Assert.IsTrue(cancel.IsVisible);

                dialog.BeginShowAnimation();
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual(1d, dialog.Opacity, 0.01d);

                Click(window, confirm);
                Assert.AreEqual("not-closed", closedResult);

                input.Text = "Steve";
                Assert.IsTrue(confirm.IsEnabled);
                Click(window, confirm);
                Assert.IsTrue(dialog.IsClosing);
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual("Steve", closedResult);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyMsgMarkdown_UsesWpfThreeButtonContractAndAnimations()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            int actionCount = 0;
            MyMsgMarkdown dialog = new();
            dialog.Configure(
                title: "更新日志",
                markdown: "## PCL N\n- 修复输入框",
                primaryButton: "继续",
                secondaryButton: "稍后",
                thirdButton: "关闭",
                primaryAction: () => actionCount++);
            Window window = new()
            {
                Width = 620,
                Height = 360,
                Content = dialog
            };

            int closedResult = 0;
            dialog.Closed += (_, e) => closedResult = e.Result;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("更新日志", dialog.FindControl<TextBlock>("LabTitle")!.Text);
                Assert.AreEqual("## PCL N\n- 修复输入框", dialog.FindControl<MyMarkdownViewer>("LabCaption")!.Markdown);
                Assert.IsTrue(dialog.FindControl<MyButton>("Btn2")!.IsVisible);
                Assert.IsTrue(dialog.FindControl<MyButton>("Btn3")!.IsVisible);

                dialog.BeginShowAnimation();
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual(1d, dialog.Opacity, 0.01d);

                Click(window, dialog.FindControl<MyButton>("Btn1")!);
                Assert.AreEqual(1, actionCount);
                Assert.AreEqual(0, closedResult);

                Click(window, dialog.FindControl<MyButton>("Btn3")!);
                Assert.IsTrue(dialog.IsClosing);
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual(3, closedResult);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyVirtualizingElement_ReplacesPlaceholderLikeWpfControl()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyVirtualizingElement<TextBlock> generic = new(() => new TextBlock { Text = "泛型懒加载" });
            MyVirtualizingElement plain = new(() => new MyButton { Text = "普通懒加载" });
            StackPanel panel = new()
            {
                Children =
                {
                    generic,
                    plain
                }
            };
            Window window = new()
            {
                Width = 260,
                Height = 120,
                Content = panel
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Control first = MyVirtualizingElement.TryInit(generic);
                Control second = MyVirtualizingElement.TryInit(plain);

                Assert.IsInstanceOfType<TextBlock>(first);
                Assert.IsInstanceOfType<MyButton>(second);
                Assert.AreSame(first, panel.Children[0]);
                Assert.AreSame(second, panel.Children[1]);
                Assert.AreEqual("泛型懒加载", ((TextBlock)first).Text);
                Assert.AreEqual("普通懒加载", ((MyButton)second).Text);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageLoginMs_UsesWpfStartAndFinishState()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageLoginMs page = new();
            Window window = new()
            {
                Width = 260,
                Height = 260,
                Content = page
            };
            int loginCount = 0;
            page.LoginRequested += (_, _) => loginCount++;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Click(window, page.FindControl<MyButton>("BtnLogin")!);

                Assert.AreEqual(1, loginCount);
                Assert.IsTrue(page.IsLoggingIn);
                Assert.IsFalse(page.FindControl<MyButton>("BtnLogin")!.IsEnabled);
                Assert.IsFalse(page.FindControl<MyTextButton>("BtnBack")!.IsVisible);

                page.UpdateProgress(0.42d);
                Assert.AreEqual("42 %", page.FindControl<MyButton>("BtnLogin")!.Text);

                page.FinishLogin();
                Assert.IsFalse(page.IsLoggingIn);
                Assert.IsTrue(page.FindControl<MyButton>("BtnLogin")!.IsEnabled);
                Assert.IsTrue(page.FindControl<MyTextButton>("BtnBack")!.IsVisible);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageLoginAuth_ValidatesAndRaisesLoginRequest()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageLoginAuth page = new();
            Window window = new()
            {
                Width = 320,
                Height = 260,
                Content = page
            };
            string? validation = null;
            AuthLoginRequest? request = null;
            page.ValidationFailed += (_, message) => validation = message;
            page.LoginRequested += (_, value) => request = value;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Click(window, page.FindControl<MyButton>("BtnLogin")!);
                Assert.AreEqual("请填写认证服务器、邮箱和密码。", validation);

                page.FindControl<MyComboBox>("TextServer")!.Text = "LittleSkin";
                Assert.AreEqual("https://littleskin.cn/api/yggdrasil", page.FindControl<MyComboBox>("TextServer")!.Text);
                page.FindControl<MyTextBox>("TextName")!.Text = "steve@example.com";
                page.FindControl<MyTextBox>("TextPass")!.Text = "secret";

                Click(window, page.FindControl<MyButton>("BtnLogin")!);

                Assert.IsNotNull(request);
                Assert.AreEqual("https://littleskin.cn/api/yggdrasil", request!.Server);
                Assert.AreEqual("steve@example.com", request.Username);
                Assert.AreEqual("secret", request.Password);
                Assert.IsFalse(page.FindControl<MyButton>("BtnLogin")!.IsEnabled);

                page.FinishLogin();
                Assert.IsTrue(page.FindControl<MyButton>("BtnLogin")!.IsEnabled);
                Assert.AreEqual("登录", page.FindControl<MyButton>("BtnLogin")!.Text);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageLoginOffline_ValidatesUuidAndCreatesProfileRequest()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageLoginOffline page = new();
            Window window = new()
            {
                Width = 360,
                Height = 360,
                Content = page
            };
            string? validation = null;
            OfflineProfileCreateRequest? request = null;
            page.ValidationFailed += (_, message) => validation = message;
            page.ProfileCreateRequested += (_, value) => request = value;

            try
            {
                page.SetSkinSources(
                [
                    new LoginProfileInfo(
                        "Alex",
                        "正版登录",
                        LaunchLoginProfileKind.Microsoft,
                        Uuid: "alex-uuid")
                ]);
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual(2, page.FindControl<MyComboBox>("ComboSkinSource")!.Items.Count);

                page.FindControl<MyTextBox>("TextName")!.Text = "St";
                Click(window, page.FindControl<MyButton>("BtnLogin")!);
                Assert.AreEqual("玩家 ID 应为 3-16 位字母、数字或下划线。", validation);

                page.FindControl<MyTextBox>("TextName")!.Text = "Steve";
                page.FindControl<MyRadioBox>("RadioUuidCustom")!.SetChecked(true, user: true);
                Assert.IsTrue(page.FindControl<MyTextBox>("TextUuid")!.IsVisible);
                page.FindControl<MyTextBox>("TextUuid")!.Text = "not-a-uuid";
                Click(window, page.FindControl<MyButton>("BtnLogin")!);
                Assert.AreEqual("自定义 UUID 应为 32 位十六进制字符。", validation);

                page.FindControl<MyTextBox>("TextUuid")!.Text = "00112233445566778899aabbccddeeff";
                Click(window, page.FindControl<MyButton>("BtnLogin")!);

                Assert.IsNotNull(request);
                Assert.AreEqual("Steve", request!.Username);
                Assert.AreEqual("00112233445566778899aabbccddeeff", request.Uuid);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageLoginOffline_TextBoxesAcceptRealKeyboardInput()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                PageLaunchLeft launchPage = FindVisual<PageLaunchLeft>(window)!;
                launchPage.RefreshPage(anim: true, PageLaunchLeft.LaunchLoginPageType.Offline);
                PageLoginOffline offlinePage = (PageLoginOffline)launchPage.CurrentLoginPage!;
                MyTextBox nameBox = offlinePage.FindControl<MyTextBox>("TextName")!;

                Click(window, nameBox);
                Assert.IsTrue(nameBox.IsKeyboardFocusWithin);
                TypeText(window, "Steve");

                Assert.AreEqual("Steve", nameBox.Text);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MainWindow_AccountTypeDialogReleasesOverlayBeforeLoginTextInput()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MainWindow window = new();

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                PageLaunchLeft launchPage = FindVisual<PageLaunchLeft>(window)!;
                launchPage.RefreshPage(anim: true, PageLaunchLeft.LaunchLoginPageType.Profile);
                PageLoginProfile profilePage = (PageLoginProfile)launchPage.CurrentLoginPage!;

                Click(window, profilePage.FindControl<MyIconButton>("BtnNew")!);
                MyMsgSelect dialog = FindVisual<MyMsgSelect>(window)!;
                Click(window, dialog.Items[2]);
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.IsFalse(window.FindControl<BlurBorder>("PanMsgBackground")!.IsVisible);
                PageLoginOffline offlinePage = (PageLoginOffline)launchPage.CurrentLoginPage!;
                MyTextBox nameBox = offlinePage.FindControl<MyTextBox>("TextName")!;

                Click(window, nameBox);
                Assert.IsTrue(nameBox.IsKeyboardFocusWithin);
                TypeText(window, "Alex");

                Assert.AreEqual("Alex", nameBox.Text);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void RemainingWpfMigratedControls_LoadAndKeepCompatibilitySurfaces()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            bool lazyLoaded = false;
            Border lazyTarget = new()
            {
                Width = 40,
                Height = 20,
                Background = Brushes.Transparent
            };
            lazyTarget.EnableLazyLoad(() => lazyLoaded = true);

            MyTextBox safeClipboardTextBox = new()
            {
                Width = 180,
                Height = 28,
                Text = "abcdef"
            };
            ClipboardInterceptor.SetEnableSafeClipboard(safeClipboardTextBox, true);

            FontSelector fontSelector = new()
            {
                Width = 220
            };
            fontSelector.CustomFontCollection.Add(
                new FontSelector.CustomFontProperties("Arial", new FontFamily("Arial"), "Arial"));
            fontSelector.SelectedFontTag = "Arial";
            fontSelector.Tooltip = "选择字体";
            Assert.AreEqual("Arial", fontSelector.CustomFontCollection[0].ToString());

            MinecraftServerQuery serverQuery = new()
            {
                Width = 360
            };
            StackPanel panel = new()
            {
                Margin = new Thickness(20),
                Spacing = 12,
                Children =
                {
                    lazyTarget,
                    safeClipboardTextBox,
                    fontSelector,
                    serverQuery
                }
            };
            Window window = new()
            {
                Width = 460,
                Height = 280,
                Content = panel
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsTrue(lazyLoaded);
                Assert.IsTrue(ClipboardInterceptor.GetEnableSafeClipboard(safeClipboardTextBox));
                Assert.AreEqual("Arial", fontSelector.SelectedFontTag);

                serverQuery.FindControl<MyTextBox>("LabServerIp")!.Text = "example.com/server";
                serverQuery.ServerQueryAsync().GetAwaiter().GetResult();

                Assert.IsTrue(serverQuery.FindControl<Border>("ServerInfo")!.IsVisible);
                Assert.AreEqual(
                    "服务器地址中不应包含 /。",
                    serverQuery.FindControl<MinecraftServer>("PanMcServer")!
                        .FindControl<TextBlock>("LabServerDesc")!
                        .Text);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageLaunchRight_PreservesCustomHomepageSurface()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageLaunchRight page = new();
            Window window = new()
            {
                Width = 520,
                Height = 360,
                Content = page
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                TextBlock customBlock = new()
                {
                    Tag = "homepage-title",
                    Text = "自定义主页"
                };

                page.AddCustomContent(customBlock);
                page.AppendLog("测试日志");

                Assert.AreSame(customBlock, page.CustomPanel!.Children.Single());
                Assert.IsTrue(page.FindControl<TextBlock>("LabLog")!.Text!.Contains("测试日志", StringComparison.Ordinal));
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageLaunchRight_LivePatchUsesMappingTargetAndAllowedProperties()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string directory = System.IO.Path.Combine(AppContext.BaseDirectory, "PCL");
        string patchFile = System.IO.Path.Combine(directory, "CustomLive.json");
        string? originalPatch = File.Exists(patchFile) ? File.ReadAllText(patchFile) : null;

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                patchFile,
                """
                {
                  "homepage-title": {
                    "properties": {
                      "text": "实时更新后的主页",
                      "opacity": "0.42",
                      "isEnabled": "false"
                    },
                    "tooltip": "来自 live patch"
                  }
                }
                """);

            session.Dispatch(() =>
            {
                PageLaunchRight page = new();
                TextBlock target = new()
                {
                    Tag = "homepage-title",
                    Text = "更新前"
                };
                page.AddCustomContent(target);

                InvokePrivateMethod(page, "ApplyHomepageLivePatchesFromFile");

                Assert.AreEqual("实时更新后的主页", target.Text);
                Assert.AreEqual(0.42d, target.Opacity, 0.001d);
                Assert.IsFalse(target.IsEnabled);
                Assert.AreEqual("来自 live patch", ToolTip.GetTip(target));
                page.Dispose();
            }, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            if (originalPatch is null)
                File.Delete(patchFile);
            else
                File.WriteAllText(patchFile, originalPatch);
        }
    }

    [TestMethod]
    public void MyLoading_AnimatesPickaxeLoop()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

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
                ModAnimation.AdvanceForTesting(16, 50);

                var pickaxe = loading.FindControl<Avalonia.Controls.Shapes.Path>("PathPickaxe")!;
                var rotate = (Avalonia.Media.RotateTransform)pickaxe.RenderTransform!;
                Assert.AreEqual(new Thickness(10d, 6d, 0d, 0d), pickaxe.Margin);
                Assert.AreEqual(HorizontalAlignment.Left, pickaxe.HorizontalAlignment);
                Assert.AreEqual(VerticalAlignment.Top, pickaxe.VerticalAlignment);
                Assert.AreEqual(30d, rotate.CenterX, 0.01d);
                Assert.AreEqual(30d, rotate.CenterY, 0.01d);
                Assert.AreEqual(new RelativePoint(0d, 0d, RelativeUnit.Relative), pickaxe.RenderTransformOrigin);
                Assert.IsTrue(rotate.Angle < 35d, $"Expected the WPF strike posture, got {rotate.Angle:0.00} degrees.");
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyLoading_UsesWpfStateProgressErrorAndClickContracts()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyLoadingStateSimulator simulator = new()
            {
                IsLoader = true,
                Progress = 0.42d,
                LoadingState = MyLoading.MyLoadingState.Run
            };
            MyLoading loading = new()
            {
                AutoRun = false,
                Text = "正在加载",
                TextError = "加载失败",
                TextErrorInherit = false,
                ShowProgress = true,
                State = simulator
            };
            Window window = new()
            {
                Width = 220,
                Height = 140,
                Content = loading
            };

            List<MyLoading.MyLoadingState> stateEvents = [];
            bool isErrorChanged = false;
            bool clicked = false;
            loading.StateChanged += (_, state, _) => stateEvents.Add(state);
            loading.IsErrorChanged += (_, isError) => isErrorChanged = isError;
            loading.Click += (_, _) => clicked = true;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("正在加载 - 42%", loading.FindControl<TextBlock>("LabText")!.Text);
                Assert.IsTrue(stateEvents.Contains(MyLoading.MyLoadingState.Run));

                Click(window, loading);
                Assert.IsTrue(clicked);

                simulator.LoadingState = MyLoading.MyLoadingState.Error;
                ModAnimation.AdvanceUntilIdleForTesting();

                Avalonia.Controls.Shapes.Path errorIcon = loading.FindControl<Avalonia.Controls.Shapes.Path>("PathError")!;
                Assert.IsTrue(isErrorChanged);
                Assert.AreEqual("加载失败", loading.FindControl<TextBlock>("LabText")!.Text);
                Assert.AreEqual(1d, errorIcon.Opacity, 0.01d);
                Assert.AreEqual(1d, ((ScaleTransform)errorIcon.RenderTransform!).ScaleX, 0.01d);
                Assert.AreEqual(RequiredBrush("ColorBrushRedLight").Color, ((SolidColorBrush)loading.Foreground!).Color);

                simulator.LoadingState = MyLoading.MyLoadingState.Stop;
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.AreEqual(0d, errorIcon.Opacity, 0.01d);
                Assert.AreEqual(0.5d, ((ScaleTransform)errorIcon.RenderTransform!).ScaleX, 0.01d);
                Assert.AreEqual(RequiredBrush("ColorBrush3").Color, ((SolidColorBrush)loading.Foreground!).Color);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyCard_UsesWpfChromeAndTitleLayer()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            Grid content = new()
            {
                Margin = new Thickness(20d, 40d, 20d, 16d)
            };
            MyCard card = new()
            {
                Title = "高级设置",
                Width = 240,
                Height = 90,
                Children =
                {
                    content
                }
            };
            Window window = new()
            {
                Width = 320,
                Height = 160,
                Content = new Border
                {
                    Padding = new Thickness(20),
                    Child = card
                }
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsInstanceOfType<MyDropShadow>(card.Children[0]);
                Assert.IsInstanceOfType<BlurBorder>(card.Children[1]);
                Assert.IsInstanceOfType<Grid>(card.Children[2]);
                Assert.AreSame(content, card.Children[3]);
                Assert.IsTrue(card.Background is null or SolidColorBrush { Color.A: 0 });
                Assert.AreEqual(Color.Parse("#d2fbfbfb"), ((SolidColorBrush)((BlurBorder)card.Children[1]).Background!).Color);
                Assert.AreEqual("高级设置", card.MainTextBlock.Text);
                Assert.AreEqual(new Thickness(15d, 12d, 0d, 0d), card.MainTextBlock.Margin);
                Assert.AreEqual(0.07d, card.MainChrome.Opacity, 0.01d);
                Assert.AreEqual(new CornerRadius(8d), card.CornerRadius);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyCard_UsesDarkThemePaletteForChromeAndTitleLayer()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            try
            {
                AvaloniaThemeManager.Apply(new LauncherSettings
                {
                    ColorMode = ColorMode.Dark,
                    DarkColor = ColorTheme.CatBlue
                });
                IReadOnlyDictionary<string, Color> palette = ThemeColorPalette.Create(isDarkMode: true, ColorTheme.CatBlue);

                MyCard card = new()
                {
                    Title = "高级设置",
                    Width = 240,
                    Height = 90
                };
                Window window = new()
                {
                    Width = 320,
                    Height = 160,
                    Content = new Border
                    {
                        Padding = new Thickness(20),
                        Child = card
                    }
                };

                try
                {
                    window.Show();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    BlurBorder chrome = (BlurBorder)card.Children[1];
                    Assert.AreEqual(palette["ColorBrushTransparentBackground"], ((SolidColorBrush)chrome.Background!).Color);
                    Assert.AreEqual(palette["ColorBrush1"], ((SolidColorBrush)card.MainTextBlock.Foreground!).Color);
                    Assert.AreEqual(palette["ColorObject1"], card.MainChrome.Color);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                AvaloniaThemeManager.Apply(new LauncherSettings
                {
                    ColorMode = ColorMode.Light,
                    LightColor = ColorTheme.CatBlue
                });
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyCard_TracksThemeResourceChangesLikeWpfResourceReference()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            try
            {
                AvaloniaThemeManager.Apply(new LauncherSettings
                {
                    ColorMode = ColorMode.Light,
                    LightColor = ColorTheme.CatBlue
                });
                MyCard card = new()
                {
                    Title = "资源跟随",
                    Width = 240,
                    Height = 90
                };
                Window window = new()
                {
                    Width = 320,
                    Height = 160,
                    Content = new Border
                    {
                        Padding = new Thickness(20),
                        Child = card
                    }
                };

                try
                {
                    window.Show();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    BlurBorder chrome = (BlurBorder)card.Children[1];
                    IReadOnlyDictionary<string, Color> lightPalette = ThemeColorPalette.Create(isDarkMode: false, ColorTheme.CatBlue);
                    Assert.AreEqual(lightPalette["ColorBrushTransparentBackground"], ((SolidColorBrush)chrome.Background!).Color);

                    AvaloniaThemeManager.Apply(new LauncherSettings
                    {
                        ColorMode = ColorMode.Dark,
                        DarkColor = ColorTheme.CatBlue
                    });
                    Assert.IsTrue(ModAnimation.AniIsRun("MyCard Theme " + card.uuid));
                    ModAnimation.AdvanceUntilIdleForTesting();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    IReadOnlyDictionary<string, Color> darkPalette = ThemeColorPalette.Create(isDarkMode: true, ColorTheme.CatBlue);
                    Assert.AreEqual(darkPalette["ColorBrushTransparentBackground"], ((SolidColorBrush)chrome.Background!).Color);
                    Assert.AreEqual(darkPalette["ColorBrush1"], ((SolidColorBrush)card.MainTextBlock.Foreground!).Color);
                    Assert.AreEqual(darkPalette["ColorObject1"], card.MainChrome.Color);
                }
                finally
                {
                    window.Close();
                }
            }
            finally
            {
                AvaloniaThemeManager.Apply(new LauncherSettings
                {
                    ColorMode = ColorMode.Light,
                    LightColor = ColorTheme.CatBlue
                });
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyCard_SwapClickTogglesContentAndCanBeCancelled()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            StackPanel lazyContent = new()
            {
                Tag = "lazy"
            };
            MyCard card = new()
            {
                Title = "高级",
                Width = 240,
                CanSwap = true,
                IsSwapped = true,
                InstallMethod = panel => panel.Children.Add(new TextBlock { Text = "已加载" }),
                Children =
                {
                    lazyContent
                }
            };
            Window window = new()
            {
                Width = 320,
                Height = 180,
                Content = new Border
                {
                    Padding = new Thickness(20),
                    Child = card
                }
            };

            int swapCount = 0;
            card.Swap += (_, _) => swapCount++;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual(MyCard.SwapedHeight, card.Height);
                Assert.IsFalse(lazyContent.IsVisible);
                Assert.AreEqual(0d, ((Avalonia.Media.RotateTransform)card.MainSwap.RenderTransform!).Angle, 0.01d);

                ClickAt(window, card, new Point(20d, 20d));

                Assert.IsFalse(card.IsSwapped);
                Assert.IsTrue(lazyContent.IsVisible);
                Assert.IsNull(lazyContent.Tag);
                Assert.AreEqual(2, lazyContent.Children.Count);
                Assert.IsTrue(ModAnimation.AniIsRun("MyCard Swap " + card.uuid));
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                ModAnimation.AdvanceForTesting(16, 3);
                Assert.IsTrue(card.Height >= MyCard.SwapedHeight);
                Assert.IsTrue(((Avalonia.Media.RotateTransform)card.MainSwap.RenderTransform!).Angle is > 0d and < 180d);
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.IsTrue(double.IsNaN(card.Height));
                Assert.AreEqual(180d, ((Avalonia.Media.RotateTransform)card.MainSwap.RenderTransform!).Angle, 0.01d);
                Assert.AreEqual(1, swapCount);

                card.PreviewSwap += (_, e) => e.handled = true;
                ClickAt(window, card, new Point(20d, 20d));

                Assert.IsFalse(card.IsSwapped);
                Assert.IsTrue(lazyContent.IsVisible);
                Assert.AreEqual(1, swapCount);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyCard_AniDisposeUsesWpfExitAnimationAndFallback()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyCard animatedCard = new()
            {
                Title = "可移除",
                Width = 220,
                Height = 80
            };
            MyCard collapsedCard = new()
            {
                Title = "可隐藏",
                Width = 220,
                Height = 80,
                IsHitTestVisible = false
            };
            StackPanel panel = new()
            {
                Children =
                {
                    animatedCard,
                    collapsedCard
                }
            };
            Window window = new()
            {
                Width = 320,
                Height = 220,
                Content = new Border
                {
                    Padding = new Thickness(20),
                    Child = panel
                }
            };

            int callbackCount = 0;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                ModAnimation.AniDispose(animatedCard, removeFromChildren: true, _ => callbackCount++);
                Assert.IsFalse(animatedCard.IsHitTestVisible);
                Assert.IsTrue(ModAnimation.AniIsRun("MyCard Dispose " + animatedCard.uuid));
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.IsFalse(panel.Children.Contains(animatedCard));
                Assert.AreEqual(1, callbackCount);

                ModAnimation.AniDispose(collapsedCard, removeFromChildren: false, _ => callbackCount++);

                Assert.IsFalse(collapsedCard.IsVisible);
                Assert.AreEqual(2, callbackCount);
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
        using SafeHeadlessUnitTestSession session = CreateSession();

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
                Assert.IsTrue(ModAnimation.AniIsRun("MyRadioBox Border " + second.Uuid));
                Assert.IsTrue(ModAnimation.AniIsRun("MyRadioBox Dot " + second.Uuid));
                Assert.IsTrue(ModAnimation.AniIsRun("MyRadioBox BorderColor " + second.Uuid));
                ModAnimation.AdvanceUntilIdleForTesting();
                Ellipse selectedDot = second.FindControl<Ellipse>("ShapeDot")!;
                Ellipse selectedBorder = second.FindControl<Ellipse>("ShapeBorder")!;
                Assert.AreEqual(8d, selectedDot.Width, 0.001d);
                Assert.AreEqual(6d, selectedDot.Margin.Left, 0.001d);
                Assert.AreEqual(10d, selectedDot.Margin.Left + selectedDot.Width / 2d, 0.001d);
                Assert.AreEqual(
                    selectedBorder.Margin.Left + selectedBorder.Width / 2d,
                    selectedDot.Margin.Left + selectedDot.Width / 2d,
                    0.001d);

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
    public void MyRadioButton_KeepsWpfSingleSelectionPreviewAndIconBehavior()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyRadioButton first = new()
            {
                Text = "全部",
                Width = 80,
                Height = 27,
                Checked = true
            };
            MyRadioButton second = new()
            {
                Text = "可更新",
                Width = 100,
                Height = 27,
                Logo = "M0,0 L10,5 L0,10Z",
                LogoScale = 1.4d,
                ColorType = MyRadioButton.ColorState.Highlight
            };
            StackPanel panel = new()
            {
                Margin = new Thickness(20),
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    first,
                    second
                }
            };
            Window window = new()
            {
                Width = 260,
                Height = 100,
                Content = panel
            };

            bool secondCheckedByMouse = false;
            second.Check += (_, raiseByMouse) => secondCheckedByMouse = raiseByMouse;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("全部", first.FindControl<TextBlock>("LabText")!.Text);
                Assert.AreEqual("可更新", second.FindControl<TextBlock>("LabText")!.Text);
                Assert.IsTrue(first.Checked);
                Assert.IsFalse(second.Checked);
                Assert.AreEqual(0d, first.FindControl<Grid>("LogoHost")!.Width);
                Assert.AreEqual(16d, second.FindControl<Grid>("LogoHost")!.Width);
                Assert.AreEqual(1.4d, ((ScaleTransform)second.FindControl<Grid>("LogoHost")!.RenderTransform!).ScaleX, 0.01d);

                MoveTo(window, second);
                Assert.IsTrue(ModAnimation.AniIsRun("MyRadioButton Checked " + second.Uuid));
                Assert.IsTrue(ModAnimation.AniIsRun("MyRadioButton Color " + second.Uuid));
                ModAnimation.AdvanceForTesting(16, 16);
                Assert.AreEqual(
                    Color.Parse("#1370f3"),
                    ((SolidColorBrush)second.FindControl<TextBlock>("LabText")!.Foreground!).Color);

                Click(window, second);
                Assert.IsTrue(ModAnimation.AniIsRun("MyRadioButton Checked " + second.Uuid));
                Assert.IsTrue(ModAnimation.AniIsRun("MyRadioButton Color " + second.Uuid));
                ModAnimation.AdvanceForTesting(16, 16);

                Assert.IsFalse(first.Checked);
                Assert.IsTrue(second.Checked);
                Assert.IsTrue(secondCheckedByMouse);
                Assert.AreEqual(
                    Color.Parse("#1370f3"),
                    ((SolidColorBrush)second.Background!).Color);

                first.PreviewClick += (_, e) => e.handled = true;
                Click(window, first);
                Assert.IsFalse(first.Checked);
                Assert.IsTrue(second.Checked);

                second.SvgIcon = "lucide/play";
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsFalse(second.FindControl<Avalonia.Controls.Shapes.Path>("ShapeLogo")!.IsVisible);
                Assert.IsTrue(second.FindControl<SvgIcon>("ShapeSvgIcon")!.IsVisible);
                Assert.AreEqual(1d, ((ScaleTransform)second.FindControl<Grid>("LogoHost")!.RenderTransform!).ScaleX, 0.01d);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyCollapseBar_UsesWpfHeaderToggleHeightAndCardAnimationSilence()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyCollapseBar collapse = new()
            {
                Title = "更多设置",
                Width = 240
            };
            collapse.ContentPanel.Children.Add(new TextBlock { Text = "折叠内容", Height = 60 });
            MyCard card = new()
            {
                Width = 280,
                BorderChild = collapse
            };
            Window window = new()
            {
                Width = 340,
                Height = 180,
                Content = new Border
                {
                    Padding = new Thickness(20),
                    Child = card
                }
            };

            int toggleCount = 0;
            collapse.Toggled += (_, _) => toggleCount++;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("更多设置", collapse.Title);
                Assert.IsFalse(collapse.IsCollapsed);
                Assert.IsTrue(collapse.ContentPanel.IsVisible);

                Click(window, (Control)collapse.Children[0]);
                Assert.IsTrue(collapse.IsCollapsed);
                Assert.IsFalse(card.UseAnimation);
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.IsFalse(collapse.ContentPanel.IsVisible);
                Assert.IsTrue(card.UseAnimation);

                Click(window, (Control)collapse.Children[0]);
                Assert.IsFalse(collapse.IsCollapsed);
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.IsTrue(collapse.ContentPanel.IsVisible);
                Assert.AreEqual(2, toggleCount);

                Avalonia.Controls.Shapes.Path triangle = collapse
                    .GetVisualDescendants()
                    .OfType<Avalonia.Controls.Shapes.Path>()
                    .Single();
                Assert.AreEqual(180d, ((RotateTransform)triangle.RenderTransform!).Angle, 0.01d);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyScrollBar_UsesWpfOpacityAndForegroundStates()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyScrollBar scrollBar = new()
            {
                Width = 18,
                Height = 120,
                Minimum = 0,
                Maximum = 100,
                ViewportSize = 20,
                Value = 10
            };
            Window window = new()
            {
                Width = 100,
                Height = 180,
                Content = new Border
                {
                    Padding = new Thickness(30),
                    Child = scrollBar
                }
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.AreEqual(0.5d, scrollBar.Opacity, 0.01d);

                MoveTo(window, scrollBar);
                Assert.IsTrue(ModAnimation.AniIsRun("MyScrollBar Color " + scrollBar.Uuid));
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual(0.9d, scrollBar.Opacity, 0.01d);
                Assert.AreEqual(
                    Color.Parse("#1370f3"),
                    ((SolidColorBrush)scrollBar.Foreground!).Color);

                Click(window, scrollBar);
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual(
                    Color.Parse("#1370f3"),
                    ((SolidColorBrush)scrollBar.Foreground!).Color);
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
        using SafeHeadlessUnitTestSession session = CreateSession();

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
    public void MySearchBox_UsesWpfPropertiesSearchButtonAndClearAnimation()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MySearchBox searchBox = new()
            {
                Width = 280,
                HintText = "搜索组件",
                Text = "forge"
            };
            Window window = new()
            {
                Width = 360,
                Height = 120,
                Content = new Border
                {
                    Padding = new Thickness(20),
                    Child = searchBox
                }
            };

            bool textChanged = false;
            object? searchSender = null;
            searchBox.TextChanged += (_, _) => textChanged = true;
            searchBox.Search += (sender, _) => searchSender = sender;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                MyTextBox textBox = searchBox.FindControl<MyTextBox>("TextBox")!;
                MyIconButton clear = searchBox.FindControl<MyIconButton>("BtnClear")!;
                MyButton search = searchBox.FindControl<MyButton>("BtnSearch")!;

                Assert.AreEqual("搜索组件", textBox.HintText);
                Assert.AreEqual("forge", textBox.Text);
                Assert.AreEqual(1d, clear.Opacity, 0.01d);
                Assert.IsTrue(clear.IsHitTestVisible);
                Assert.IsFalse(search.IsVisible);

                searchBox.SearchButtonVisibility = true;
                Assert.IsTrue(search.IsVisible);
                Assert.AreEqual(new Thickness(0d, 0d, 70d, 0d), clear.Margin);

                Click(window, search);
                Assert.AreSame(search, searchSender);

                textBox.Text = "fabric";
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual("fabric", searchBox.Text);
                Assert.IsTrue(textChanged);

                Click(window, clear);
                Assert.IsTrue(ModAnimation.AniIsRun("MySearchBox ClearBtn " + searchBox.Uuid));
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.AreEqual(string.Empty, searchBox.Text);
                Assert.AreEqual(string.Empty, textBox.Text);
                Assert.AreEqual(0d, clear.Opacity, 0.01d);
                Assert.IsFalse(clear.IsHitTestVisible);
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
        using SafeHeadlessUnitTestSession session = CreateSession();

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
                Assert.IsNotNull(button.Inlines);
                Assert.IsFalse(button.FindControl<Grid>("IconHost")!.IsVisible);
                Assert.AreEqual(1d, ((Avalonia.Media.ScaleTransform)button.RenderTransform!).ScaleX, 0.01d);
                Assert.AreEqual(new CornerRadius(20.8d), button.FindControl<Border>("PanClick")!.CornerRadius);
                Assert.AreEqual(new CornerRadius(20.8d), button.FindControl<Border>("PanColor")!.CornerRadius);

                button.Height = 60d;
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual(new CornerRadius(24d), button.FindControl<Border>("PanClick")!.CornerRadius);
                Assert.AreEqual(new CornerRadius(24d), button.FindControl<Border>("PanColor")!.CornerRadius);

                button.Logo = "M0,0 L10,5 L0,10 Z";
                Assert.IsTrue(button.FindControl<Grid>("IconHost")!.IsVisible);

                Click(window, button);
                Assert.IsTrue(ModAnimation.AniIsRun("MyExtraTextButton Scale " + button.Uuid));
                Assert.IsTrue(clicked);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyExtraButton_UsesWpfShowProgressRightClickAndRibbleAnimations()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyExtraButton button = new()
            {
                Logo = "M0,0 L10,5 L0,10 Z",
                CanRightClick = true
            };
            Window window = new()
            {
                Width = 140,
                Height = 140,
                Content = new Border
                {
                    Padding = new Thickness(40),
                    Child = button
                }
            };
            bool shouldShow = true;
            bool clicked = false;
            bool rightClicked = false;
            button.showCheck = () => shouldShow;
            Assert.AreSame(button.showCheck, button.ShowCheck);
            button.Click += (_, _) => clicked = true;
            button.RightClick += (_, _) => rightClicked = true;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.IsFalse(button.IsVisible);

                button.ShowRefresh();
                Assert.IsTrue(ModAnimation.AniIsRun("MyExtraButton MainScale " + button.Uuid));
                ModAnimation.AdvanceForTesting(16, 48);
                Assert.IsTrue(button.IsVisible);
                Assert.AreEqual(50d, button.Height, 0.01d);
                Assert.AreEqual(1d, ((ScaleTransform)button.RenderTransform!).ScaleX, 0.01d);

                button.Progress = 0.25d;
                Border progress = button.FindControl<Border>("PanProgress")!;
                Assert.IsTrue(progress.IsVisible);
                RectangleGeometry clip = (RectangleGeometry)progress.Clip!;
                Assert.AreEqual(30d, clip.Rect.Y, 0.01d);
                Assert.AreEqual(10d, clip.Rect.Height, 0.01d);

                MoveTo(window, button);
                Assert.IsTrue(ModAnimation.AniIsRun("MyExtraButton Color " + button.Uuid));
                ModAnimation.AdvanceForTesting(16, 16);
                Border color = button.FindControl<Border>("PanColor")!;
                Assert.AreEqual(Color.Parse("#4890f5"), ((SolidColorBrush)color.Background!).Color);

                Click(window, button);
                Assert.IsTrue(ModAnimation.AniIsRun("MyExtraButton Scale " + button.Uuid));
                ModAnimation.AdvanceForTesting(16, 48);
                Assert.IsTrue(clicked);
                Grid scale = button.FindControl<Grid>("PanScale")!;
                Assert.AreEqual(1d, ((ScaleTransform)scale.RenderTransform!).ScaleX, 0.01d);

                RightClick(window, button);
                Assert.IsTrue(rightClicked);

                int beforeRibble = scale.Children.Count;
                button.Ribble();
                Assert.AreEqual(beforeRibble + 1, scale.Children.Count);
                ModAnimation.AdvanceForTesting(16, 80);
                Assert.AreEqual(beforeRibble, scale.Children.Count);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MySlider_TracksValueKeyboardDragAndPopupLikeWpf()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MySlider slider = new()
            {
                Width = 200,
                MaxValue = 100,
                Value = 50,
                ValueByKey = 5,
                getHintText = value => $"值 {value}"
            };
            Window window = new()
            {
                Width = 300,
                Height = 120,
                Content = new Border
                {
                    Padding = new Thickness(20),
                    Child = slider
                }
            };

            int changeCount = 0;
            bool lastChangeUserFlag = true;
            slider.Change += (_, user) =>
            {
                changeCount++;
                lastChangeUserFlag = user;
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Border lineFore = slider.FindControl<Border>("LineFore")!;
                Border lineBack = slider.FindControl<Border>("LineBack")!;
                Ellipse dot = slider.FindControl<Ellipse>("ShapeDot")!;
                Popup popup = slider.FindControl<Popup>("Popup")!;
                TextBlock textHint = slider.FindControl<TextBlock>("TextHint")!;

                Assert.IsFalse(ScrollViewer.GetBringIntoViewOnFocusChange(slider));
                Assert.AreEqual(95.5d, lineFore.Width, 0.01d);
                Assert.AreEqual(95.5d, lineBack.Width, 0.01d);
                Assert.AreEqual(new Thickness(95d, 0d, 0d, 0d), dot.Margin);

                slider.Focus();
                window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, string.Empty);

                Assert.AreEqual(55, slider.Value);
                Assert.IsFalse(lastChangeUserFlag);
                Assert.AreEqual("值 55", textHint.Text);
                Assert.IsTrue(popup.IsOpen);
                Assert.IsTrue(ModAnimation.AniIsRun("MySlider Progress " + slider.Uuid));
                Assert.IsTrue(ModAnimation.AniIsRun("MySlider KeyPopup " + slider.Uuid));
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.IsFalse(popup.IsOpen);

                Drag(window, slider, new Point(10d, 8d), new Point(157d, 8d));

                Assert.AreEqual(80, slider.Value);
                Assert.IsFalse(lastChangeUserFlag);
                Assert.IsFalse(popup.IsOpen);
                Assert.IsTrue(changeCount >= 2);
                Assert.IsTrue(ModAnimation.AniIsRun("MySlider Progress " + slider.Uuid));
                Assert.IsTrue(ModAnimation.AniIsRun("MySlider Scale " + slider.Uuid));
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual(152.5d, lineFore.Width, 0.01d);
                Assert.AreEqual(new Thickness(152d, 0d, 0d, 0d), dot.Margin);
                Assert.AreEqual(1d, ((ScaleTransform)dot.RenderTransform!).ScaleX, 0.01d);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MySlider_PreviewChangeCanCancelValueMutation()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MySlider slider = new()
            {
                Width = 200,
                MaxValue = 100,
                Value = 25,
                ValueByKey = 10
            };
            Window window = new()
            {
                Width = 300,
                Height = 120,
                Content = new Border
                {
                    Padding = new Thickness(20),
                    Child = slider
                }
            };

            int changeCount = 0;
            slider.Change += (_, _) => changeCount++;
            slider.PreviewChange += (_, e) => e.handled = true;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                slider.Focus();
                window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, string.Empty);

                Assert.AreEqual(25, slider.Value);
                Assert.AreEqual(0, changeCount);
                Assert.AreEqual(new Thickness(47.5d, 0d, 0d, 0d), slider.FindControl<Ellipse>("ShapeDot")!.Margin);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyComboBox_UsesWpfTextHintAndContainerBehavior()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyComboBox comboBox = new()
            {
                Width = 180,
                ItemsSource = new[] { "全部", "热门" },
                SelectedIndex = 1
            };
            Window window = new()
            {
                Width = 320,
                Height = 180,
                Content = new Border
                {
                    Padding = new Thickness(20),
                    Child = comboBox
                }
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("热门", comboBox.Text);
                Assert.AreEqual("PART_Content", comboBox.ContentPresenter?.Name);
                Avalonia.Controls.Shapes.Path arrow = comboBox.GetVisualDescendants()
                    .OfType<Avalonia.Controls.Shapes.Path>()
                    .Single(path => path.Name == "PART_DropDownArrow");
                Point arrowCenter = arrow.TranslatePoint(new Point(arrow.Bounds.Width / 2d, arrow.Bounds.Height / 2d), comboBox)
                    ?? throw new InvalidOperationException("ComboBox arrow is not attached.");
                Assert.IsTrue(
                    arrowCenter.X > comboBox.Bounds.Width - 25d,
                    $"Drop-down arrow should stay on the right. arrowX={arrowCenter.X}, width={comboBox.Bounds.Width}");

                comboBox.IsDropDownOpen = true;
                Assert.IsTrue(ModAnimation.AniIsRun("MyComboBox Color " + comboBox.Uuid));
                Assert.IsTrue(ModAnimation.AniIsRun("MyComboBox Arrow " + comboBox.Uuid));
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsTrue(comboBox.GetRealizedContainers().All(container => container is MyComboBoxItem));
                Assert.AreEqual("全部", comboBox.GetRealizedContainers().First().ToString());
                Assert.AreEqual(180d, comboBox.Width, 0.01d);

                comboBox.IsDropDownOpen = false;
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.AreEqual(180d, comboBox.Width, 0.01d);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyComboBox_KeepsSelectedItemVisibleAndClosesAfterSelection()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyComboBoxItem automatic = new() { Content = "自动" };
            MyComboBoxItem custom = new() { Content = "自定义" };
            MyComboBox comboBox = new()
            {
                Width = 180,
                Items = { automatic, custom },
                SelectedItem = automatic
            };
            Window window = new() { Width = 320, Height = 180, Content = comboBox };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.AreEqual("自动", comboBox.SelectionText);
                Assert.AreEqual("自动", comboBox.ContentPresenter?.Content);

                comboBox.IsDropDownOpen = true;
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                CollectionAssert.Contains(comboBox.GetRealizedContainers().ToArray(), automatic);

                comboBox.SelectedItem = custom;
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.IsFalse(comboBox.IsDropDownOpen);
                Assert.AreEqual("自定义", comboBox.SelectionText);
                Assert.AreEqual("自定义", comboBox.ContentPresenter?.Content);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyPageRight_PageOnEnterKeepsContentHiddenWhileLoaderRuns()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            TaskCompletionSource loaderHold = new();
            MyPageRight page = new();
            MyLoading loading = new();
            MyCard loaderPanel = new()
            {
                Width = 180,
                Height = 90,
                Children = { loading }
            };
            StackPanel contentPanel = new()
            {
                Children =
                {
                    new MyCard
                    {
                        Title = "内容",
                        Height = 60
                    }
                }
            };
            Grid root = new()
            {
                Children =
                {
                    contentPanel,
                    loaderPanel
                }
            };
            page.Content = root;
            page.PageLoaderInit(
                loading,
                loaderPanel,
                contentPanel,
                null,
                _ => loaderHold.Task);

            Window window = new()
            {
                Width = 360,
                Height = 220,
                Content = page
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                page.PageOnEnter();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsFalse(loaderPanel.IsVisible);
                Assert.IsFalse(contentPanel.IsVisible);

                ModAnimation.AdvanceForTesting(16, 26);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsTrue(loaderPanel.IsVisible);
                Assert.IsFalse(contentPanel.IsVisible);
            }
            finally
            {
                loaderHold.SetResult();
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyPageRight_SkipsLoaderWhenAutoRunCompletesBeforeEnter()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(async () =>
        {
            bool finished = false;
            MyPageRight page = new();
            MyLoading loading = new();
            MyCard loaderPanel = new()
            {
                Width = 180,
                Height = 90,
                Children = { loading }
            };
            StackPanel contentPanel = new()
            {
                Children =
                {
                    new MyCard
                    {
                        Title = "内容",
                        Height = 60
                    }
                }
            };
            page.Content = new Grid
            {
                Children =
                {
                    contentPanel,
                    loaderPanel
                }
            };
            page.PageLoaderInit(
                loading,
                loaderPanel,
                contentPanel,
                null,
                _ => Task.CompletedTask,
                () => finished = true);

            Window window = new()
            {
                Width = 360,
                Height = 220,
                Content = page
            };

            try
            {
                window.Show();
                await WaitForConditionAsync(() => finished).ConfigureAwait(true);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                page.PageOnEnter();
                Assert.IsTrue(ModAnimation.AniIsRun("PageRight PageChange " + page.PageUuid));
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.IsFalse(loaderPanel.IsVisible);
                Assert.IsTrue(contentPanel.IsVisible);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void MyPageRight_AnimatesVisibleScrollBarLikeWpf()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyPageRight page = new();
            StackPanel content = new();
            for (int i = 0; i < 16; i++)
            {
                content.Children.Add(new MyCard
                {
                    Title = "项目 " + i,
                    Height = 42d,
                    Margin = new Thickness(0d, 0d, 0d, 4d)
                });
            }

            MyScrollViewer viewer = new()
            {
                Width = 240d,
                Height = 96d,
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = content
            };
            page.Content = viewer;
            Window window = new()
            {
                Width = 320d,
                Height = 180d,
                Content = page
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                ScrollBar scrollBar = viewer.GetVisualDescendants()
                    .OfType<ScrollBar>()
                    .First(scrollBar => scrollBar.Orientation == Orientation.Vertical && scrollBar.IsVisible);

                page.TriggerEnterAnimation(viewer);
                Assert.IsTrue(ModAnimation.AniIsRun("PageRight PageChange " + page.PageUuid));
                Assert.IsInstanceOfType<TranslateTransform>(scrollBar.RenderTransform);
                Assert.AreEqual(10d, ((TranslateTransform)scrollBar.RenderTransform!).X, 0.01d);

                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual(0d, ((TranslateTransform)scrollBar.RenderTransform!).X, 0.01d);

                page.TriggerExitAnimation(viewer);
                Assert.IsTrue(ModAnimation.AniIsRun("PageRight PageChange " + page.PageUuid));
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.AreEqual(10d, ((TranslateTransform)scrollBar.RenderTransform!).X, 0.01d);
                Assert.IsFalse(viewer.IsVisible);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyPageRight_AutoResolvesCopiedPageScrollViewer()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyScrollViewer first = new()
            {
                Name = "FirstScroll",
                Content = new Border { Height = 200d }
            };
            MyScrollViewer panBack = new()
            {
                Name = "PanBack",
                Content = new Border { Height = 200d }
            };
            MyScrollViewer explicitScroll = new()
            {
                Name = "ExplicitScroll",
                Content = new Border { Height = 200d }
            };
            MyPageRight page = new()
            {
                Content = new StackPanel
                {
                    Children =
                    {
                        first,
                        new Border { Child = panBack }
                    }
                }
            };
            Window window = new()
            {
                Width = 320d,
                Height = 180d,
                Content = page
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreSame(panBack, page.PanScroll);

                page.PanScroll = explicitScroll;
                Assert.AreSame(explicitScroll, page.PanScroll);

                page.PanScroll = null;
                page.Content = new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = "无命名滚动区前的内容" },
                        first
                    }
                };
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreSame(first, page.PanScroll);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyComboBox_SelectsWpfMarkedChildItemOnAttach()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyComboBox comboBox = new()
            {
                Width = 120,
                Items =
                {
                    new MyComboBoxItem { Content = "自动", IsSelected = true },
                    new MyComboBoxItem { Content = "自定义" }
                }
            };
            Window window = new()
            {
                Width = 260,
                Height = 140,
                Content = new Border
                {
                    Padding = new Thickness(20),
                    Child = comboBox
                }
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("自动", comboBox.Text);
                Assert.AreEqual(0, comboBox.SelectedIndex);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyListItem_SyncsCopiedWpfTitleAndInfoIntoVisualTextBlocks()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyListItem item = new()
            {
                Title = "1.20.1",
                Info = @"D:\Minecraft\versions\1.20.1",
                Logo = "pack://application:,,,/images/Blocks/Grass.png"
            };
            Window window = new()
            {
                Width = 360,
                Height = 120,
                Content = new Border
                {
                    Padding = new Thickness(20),
                    Child = item
                }
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("1.20.1", DisplayText(item.FindControl<TextBlock>("LabTitle")!));
                Assert.AreEqual(@"D:\Minecraft\versions\1.20.1", DisplayText(item.FindControl<TextBlock>("LabInfo")!));
                Assert.IsTrue(item.FindControl<TextBlock>("LabInfo")!.IsVisible);
                Assert.IsTrue(item.FindControl<TextBlock>("LabTitle")!.Bounds.Width > 1d);
                Assert.IsTrue(item.FindControl<TextBlock>("LabInfo")!.Bounds.Width > 1d);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyListItem_UsesCurrentThemeForegroundForCopiedWpfTitleAndIcon()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            AvaloniaThemeManager.Apply(new LauncherSettings
            {
                ColorMode = ColorMode.Dark,
                DarkColor = ColorTheme.CatBlue
            });

            MyListItem item = new()
            {
                Title = "1.21.5",
                Info = "正式版 · 2025-03-25",
                SvgIcon = "lucide/box",
                Type = MyListItem.CheckType.Clickable,
                Width = 320,
                Height = 42
            };
            Window window = new()
            {
                Width = 420,
                Height = 120,
                Content = new Border
                {
                    Padding = new Thickness(20),
                    Child = item
                }
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                TextBlock title = item.FindControl<TextBlock>("LabTitle")!;
                TextBlock info = item.FindControl<TextBlock>("LabInfo")!;

                Assert.AreEqual("1.21.5", DisplayText(title));
                Assert.AreEqual("正式版 · 2025-03-25", DisplayText(info));
                Assert.IsTrue(info.IsVisible);
                Assert.IsTrue(title.Bounds.Width > 1d);
                Assert.IsTrue(info.Bounds.Width > 1d);
                Assert.AreEqual(RequiredBrush("ColorBrush1").Color, ((SolidColorBrush)title.Foreground!).Color);
                Assert.AreEqual(RequiredBrush("ColorBrushGray2").Color, ((SolidColorBrush)info.Foreground!).Color);
                Assert.AreEqual(
                    RequiredBrush("ColorBrush1").Color,
                    ((SolidColorBrush)FindVisual<SvgIcon>(item)!.IconBrush!).Color);
            }
            finally
            {
                window.Close();
                AvaloniaThemeManager.Apply(new LauncherSettings
                {
                    ColorMode = ColorMode.Light,
                    LightColor = ColorTheme.CatBlue
                });
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyListItem_WithInlineButtonStillMeasuresTitleAndInfo()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyListItem item = new()
            {
                Title = "1.21.5",
                Info = "正式版 · 2025-03-25",
                SvgIcon = "lucide/box",
                Type = MyListItem.CheckType.Clickable,
                Height = 42,
                Buttons =
                [
                    new MyIconButton
                    {
                        SvgIcon = "lucide/download",
                        ToolTip = "选择并下载"
                    }
                ]
            };
            Window window = new()
            {
                Width = 420,
                Height = 120,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Children = { item }
                }
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                TextBlock title = item.FindControl<TextBlock>("LabTitle")!;
                TextBlock info = item.FindControl<TextBlock>("LabInfo")!;

                Assert.AreEqual("1.21.5", DisplayText(title));
                Assert.AreEqual("正式版 · 2025-03-25", DisplayText(info));
                Assert.IsTrue(title.Bounds.Width > 1d);
                Assert.IsTrue(info.Bounds.Width > 1d);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyCard_StackInstallKeepsListItemTitleMeasured()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            StackPanel stack = new()
            {
                Margin = new Thickness(20, MyCard.SwapedHeight, 18, 0),
                Tag = true
            };
            MyCard card = new()
            {
                Title = "Minecraft (1)",
                SwapControl = stack
            };
            card.Children.Add(stack);
            MyCard.StackInstall(ref stack, target =>
            {
                target.Children.Add(new MyListItem
                {
                    Title = "1.21.5",
                    Info = "正式版 · 2025-03-25",
                    SvgIcon = "lucide/box",
                    Type = MyListItem.CheckType.Clickable,
                    Height = 42,
                    Buttons =
                    [
                        new MyIconButton
                        {
                            SvgIcon = "lucide/download",
                            ToolTip = "选择并下载"
                        }
                    ]
                });
            });

            Window window = new()
            {
                Width = 560,
                Height = 220,
                Content = new Border
                {
                    Padding = new Thickness(25),
                    Child = card
                }
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                MyListItem item = card.GetVisualDescendants().OfType<MyListItem>().Single();
                TextBlock title = item.FindControl<TextBlock>("LabTitle")!;
                Assert.AreEqual("1.21.5", DisplayText(title));
                Assert.IsTrue(title.Bounds.Width > 1d);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyComboBox_EditableTextClearsStaleSelectionLikeWpf()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyComboBox comboBox = new()
            {
                Width = 180,
                IsEditable = true,
                HintText = "搜索字体",
                ItemsSource = new[] { "默认", "自定义" },
                SelectedIndex = 0
            };
            Window window = new()
            {
                Width = 320,
                Height = 180,
                Content = new Border
                {
                    Padding = new Thickness(20),
                    Child = comboBox
                }
            };

            int textChangedCount = 0;
            comboBox.TextChanged += (_, _) => textChangedCount++;

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual("搜索字体", comboBox.PlaceholderText);
                Assert.AreEqual(RequiredBrush("ColorBrushBg0").Color, ((SolidColorBrush)comboBox.Foreground!).Color);
                Assert.AreEqual(RequiredBrush("ColorBrushHalfWhite").Color, ((SolidColorBrush)comboBox.Background!).Color);

                MoveTo(window, comboBox);
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual(RequiredBrush("ColorBrush4").Color, ((SolidColorBrush)comboBox.Foreground!).Color);
                Assert.AreEqual(RequiredBrush("ColorBrush7").Color, ((SolidColorBrush)comboBox.Background!).Color);

                comboBox.IsDropDownOpen = true;
                ModAnimation.AdvanceUntilIdleForTesting();
                Assert.AreEqual(RequiredBrush("ColorBrush3").Color, ((SolidColorBrush)comboBox.Foreground!).Color);
                Assert.AreEqual(RequiredBrush("ColorBrush7").Color, ((SolidColorBrush)comboBox.Background!).Color);
                comboBox.IsDropDownOpen = false;

                MyTextBox editableTextBox = comboBox.GetVisualDescendants()
                    .OfType<MyTextBox>()
                    .Single(textBox => textBox.Name == "PART_EditableTextBox");
                Assert.IsTrue(editableTextBox.IsVisible);
                Assert.IsNull(comboBox.ItemTemplate);

                editableTextBox.CaretIndex = 1;
                comboBox.Text = "手动输入";

                Assert.AreEqual("手动输入", comboBox.Text);
                Assert.IsNull(comboBox.SelectedItem);
                Assert.AreEqual(1, editableTextBox.CaretIndex);
                Assert.AreEqual(1, textChangedCount);

                editableTextBox.Text = string.Empty;
                editableTextBox.CaretIndex = 0;
                editableTextBox.Focus();
                TypeText(window, "custom");
                Assert.AreEqual("custom", editableTextBox.Text);
                Assert.AreEqual("custom", comboBox.Text);
                Assert.IsNull(comboBox.SelectedItem);

                comboBox.SelectedIndex = 1;
                Assert.AreEqual("自定义", comboBox.Text);
                Assert.AreEqual("自定义", editableTextBox.Text);

                MyComboBoxItem item = new() { Content = "选项" };
                string implicitText = item;
                Assert.AreEqual("选项", item.ToString());
                Assert.AreEqual("选项", implicitText);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MyComboBoxItem_AnimatesWpfBackgroundAndOpacityStates()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MyComboBoxItem item = new()
            {
                Content = "选项",
                Width = 120d,
                Height = 28d
            };
            Window window = new()
            {
                Width = 180d,
                Height = 90d,
                Content = new Border
                {
                    Margin = new Thickness(20d),
                    Child = item
                }
            };

            try
            {
                window.Show();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                Assert.AreEqual(RequiredBrush("ColorBrushTransparent").Color, BrushColor(item.Background));
                Assert.AreEqual(1d, item.Opacity, 0.001d);

                MoveTo(window, item);
                Assert.IsTrue(ModAnimation.AniIsRun("ComboBoxItem Color " + item.Uuid));
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.AreEqual(RequiredBrush("ColorBrush8").Color, BrushColor(item.Background));
                Assert.AreEqual(1d, item.Opacity, 0.001d);

                window.MouseMove(new Point(1d, 1d), RawInputModifiers.None);
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.AreEqual(RequiredBrush("ColorBrushTransparent").Color, BrushColor(item.Background));

                item.IsEnabled = false;
                ModAnimation.AdvanceUntilIdleForTesting();

                Assert.AreEqual(RequiredBrush("ColorBrushTransparent").Color, BrushColor(item.Background));
                Assert.AreEqual(0.4d, item.Opacity, 0.01d);
            }
            finally
            {
                window.Close();
            }

            static Color BrushColor(IBrush? brush) =>
                ((SolidColorBrush)(brush ?? throw new InvalidOperationException("Expected a solid brush."))).Color;
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MainWindow_NavigationToggleUsesMeasuredAnimatedWidth()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();

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
        using SafeHeadlessUnitTestSession session = CreateSession();

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

    private static string CreateDiscoveredInstance(string minecraftRoot, string name)
    {
        string instanceDirectory = System.IO.Path.Combine(minecraftRoot, "versions", name);
        Directory.CreateDirectory(instanceDirectory);
        File.WriteAllText(
            System.IO.Path.Combine(instanceDirectory, name + ".json"),
            "{\"id\":\"" + name + "\"}");
        return instanceDirectory;
    }

    private static SafeHeadlessUnitTestSession CreateSession()
    {
        string? previousLaunchProfilesPath = Environment.GetEnvironmentVariable("PCLN_LAUNCH_PROFILES_PATH");
        string? previousSettingsPath = Environment.GetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH");
        string? previousDisableFirstRun = Environment.GetEnvironmentVariable("PCL_DISABLE_FIRST_RUN");
        string? previousDisableDebugHint = Environment.GetEnvironmentVariable("PCL_DISABLE_DEBUG_HINT");
        Environment.SetEnvironmentVariable(
            "PCLN_LAUNCH_PROFILES_PATH",
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "pcl-desktop-test-profiles-" + Guid.NewGuid().ToString("N") + ".json"));
        if (string.IsNullOrWhiteSpace(previousSettingsPath))
        {
            Environment.SetEnvironmentVariable(
                "PCLN_LAUNCHER_SETTINGS_PATH",
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "pcl-desktop-test-settings-" + Guid.NewGuid().ToString("N") + ".json"));
        }

        // MainWindow first-run EULA / community / special-build dialogs block headless tests.
        Environment.SetEnvironmentVariable("PCL_DISABLE_FIRST_RUN", "1");
        Environment.SetEnvironmentVariable("PCL_DISABLE_DEBUG_HINT", "1");

        try
        {
            return new SafeHeadlessUnitTestSession(
                HeadlessUnitTestSession.StartNew(
                    typeof(App),
                    AvaloniaTestIsolationLevel.PerTest),
                previousLaunchProfilesPath,
                previousSettingsPath,
                previousDisableFirstRun,
                previousDisableDebugHint);
        }
        catch
        {
            Environment.SetEnvironmentVariable("PCLN_LAUNCH_PROFILES_PATH", previousLaunchProfilesPath);
            Environment.SetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH", previousSettingsPath);
            Environment.SetEnvironmentVariable("PCL_DISABLE_FIRST_RUN", previousDisableFirstRun);
            Environment.SetEnvironmentVariable("PCL_DISABLE_DEBUG_HINT", previousDisableDebugHint);
            throw;
        }
    }

    [TestMethod]
    public void MainWindow_InstanceMinecraftChangeMarksInstallAsReplacement()
    {
        using SafeHeadlessUnitTestSession session = CreateSession();
        string root = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "pcl-instance-replace-route-" + Guid.NewGuid().ToString("N"));

        try
        {
            string minecraftRoot = System.IO.Path.Combine(root, ".minecraft");
            string instanceDirectory = System.IO.Path.Combine(minecraftRoot, "versions", "CustomPack");
            string instanceJson = System.IO.Path.Combine(instanceDirectory, "CustomPack.json");
            Directory.CreateDirectory(instanceDirectory);
            File.WriteAllText(instanceJson, """{"id":"CustomPack","inheritsFrom":"1.20.1"}""");

            session.Dispatch(async () =>
            {
                PageDownloadInstall installPage = new(
                    new MinecraftVanillaInstallService(),
                    new FakeMinecraftLoaderMetadataService());
                SetPrivateField(
                    installPage,
                    "_versions",
                    new[]
                    {
                        new MinecraftVersionManifestEntry(
                            "1.20.2",
                            "release",
                            "https://example.invalid/1.20.2.json",
                            DateTimeOffset.Parse("2023-09-21T00:00:00Z"))
                    });

                MainWindow window = new();
                SetPrivateField(window, "_downloadInstallPage", installPage);
                DownloadInstallRequest? requested = null;
                installPage.InstallRequested += (_, request) => requested = request;

                try
                {
                    window.Show();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                    ModAnimation.AdvanceUntilIdleForTesting();

                    var method = typeof(MainWindow).GetMethod(
                        "OpenDownloadInstallForInstanceAsync",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        ?? throw new InvalidOperationException("OpenDownloadInstallForInstanceAsync was not found.");
                    Task routeTask = (Task)method.Invoke(
                        window,
                        [new InstanceInstallModifyRequest(
                            new LaunchInstanceInfo("CustomPack", instanceJson, instanceDirectory),
                            "1.20.2")])!;
                    await routeTask.ConfigureAwait(true);
                    ModAnimation.AdvanceUntilIdleForTesting();
                    AvaloniaHeadlessPlatform.ForceRenderTimerTick();

                    Assert.AreSame(installPage, FindVisual<PageDownloadInstall>(window));
                    Assert.AreEqual("CustomPack", installPage.FindControl<MyTextBox>("TextSelectName")!.Text);
                    Assert.IsTrue(installPage.FindControl<StackPanel>("PanSelect")!.IsVisible);
                    InvokePrivateMethod(installPage, "StartSelectedInstall");

                    Assert.IsNotNull(requested);
                    Assert.AreEqual("CustomPack", requested.VersionId);
                    Assert.AreEqual("1.20.2", requested.BaseVersionId);
                    Assert.AreEqual(minecraftRoot, requested.MinecraftRootDirectory);
                    Assert.IsTrue(requested.ReplaceExistingVersion);
                }
                finally
                {
                    window.Close();
                }
            }, CancellationToken.None).GetAwaiter().GetResult();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private sealed class FakeCommunityResourceCatalog : ICommunityResourceCatalog
    {
        public CommunityResourceCategory LastCategory { get; private set; }

        public IReadOnlyList<CommunityResourceVersion> Versions { get; init; } = [];

        public IReadOnlyList<CommunityResourceEntry> Projects { get; init; } = [];

        public Task<IReadOnlyList<CommunityResourceEntry>> SearchAsync(
            CommunityResourceCategory category,
            string query,
            CommunitySearchOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastCategory = category;
            IReadOnlyList<CommunityResourceEntry> entries =
            [
                new CommunityResourceEntry(
                    "AANobbMI",
                    "sodium",
                    category == CommunityResourceCategory.Shader ? "Iris Shaders" : "Sodium",
                    category == CommunityResourceCategory.Shader ? "光影加载器" : "性能优化 Mod",
                    category == CommunityResourceCategory.Shader ? "shader" : "mod",
                    null,
                    12_345,
                    DateTimeOffset.Parse("2026-01-01T00:00:00Z"))
            ];
            return Task.FromResult(entries);
        }

        public Task<CommunityResourceDownloadFile?> ResolveDownloadAsync(
            CommunityResourceEntry entry,
            CommunitySearchOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CommunityResourceDownloadFile?>(null);

        public Task<IReadOnlyList<CommunityResourceVersion>> GetVersionsAsync(
            CommunityResourceEntry entry,
            CommunitySearchOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Versions);

        public Task<CommunityResourceEntry?> GetProjectAsync(
            CommunityResourceSource source,
            string projectId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Projects.FirstOrDefault(project =>
                project.Source == source &&
                string.Equals(project.ProjectId, projectId, StringComparison.OrdinalIgnoreCase)));

        public Task<CommunityResourceFileIdentity?> LookupFileBySha1Async(
            string sha1Hex,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CommunityResourceFileIdentity?>(null);

        public Task<CommunityResourceVersion?> GetLatestVersionAsync(
            string projectId,
            CommunitySearchOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CommunityResourceVersion?>(null);
    }

    private sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }

    private sealed class FakeMicrosoftMinecraftAuthService : IMicrosoftMinecraftAuthService
    {
        public TaskCompletionSource<MicrosoftMinecraftLoginResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string RequestedClientId { get; private set; } = string.Empty;

        public Task<MicrosoftDeviceCodeInfo> RequestDeviceCodeAsync(
            string clientId,
            CancellationToken cancellationToken = default)
        {
            RequestedClientId = clientId;
            return Task.FromResult(new MicrosoftDeviceCodeInfo(
                "device-code",
                "ABCD-EFGH",
                "https://microsoft.com/link",
                "https://microsoft.com/link?otc=ABCD-EFGH",
                "请登录 Microsoft 账户。",
                TimeSpan.FromMinutes(15),
                TimeSpan.FromSeconds(5)));
        }

        public async Task<MicrosoftMinecraftLoginResult> CompleteDeviceLoginAsync(
            string clientId,
            MicrosoftDeviceCodeInfo deviceCode,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            progress?.Report(0.5d);
            return await Completion.Task.WaitAsync(cancellationToken);
        }

        public Task<MicrosoftMinecraftLoginResult> RefreshAsync(
            string clientId,
            string refreshToken,
            CancellationToken cancellationToken = default) =>
            Completion.Task.WaitAsync(cancellationToken);
    }

    private sealed class FixedSystemInfoProvider(long totalBytes, long availableBytes)
        : PCL.Platform.Abstractions.System.ISystemInfoProvider
    {
        public PCL.Platform.Abstractions.System.OperatingSystemInfo GetOperatingSystem() =>
            new("Test", "1", "x64", true);

        public PCL.Platform.Abstractions.System.MemoryInfo GetMemoryInfo() =>
            new(totalBytes, availableBytes);

        public PCL.Platform.Abstractions.System.CpuInfo GetCpuInfo() =>
            new("Test CPU", 8, "x64");
    }

    private sealed class MutableSystemInfoProvider(long totalBytes, long availableBytes)
        : PCL.Platform.Abstractions.System.ISystemInfoProvider
    {
        public long AvailableBytes { get; set; } = availableBytes;

        public PCL.Platform.Abstractions.System.OperatingSystemInfo GetOperatingSystem() =>
            new("Test", "1", "x64", true);

        public PCL.Platform.Abstractions.System.MemoryInfo GetMemoryInfo() =>
            new(totalBytes, AvailableBytes);

        public PCL.Platform.Abstractions.System.CpuInfo GetCpuInfo() =>
            new("Test CPU", 8, "x64");
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(handler(request));
        }
    }

    private sealed class SafeHeadlessUnitTestSession : IDisposable
    {
        private readonly HeadlessUnitTestSession _inner;
        private readonly string? _previousLaunchProfilesPath;
        private readonly string? _previousSettingsPath;
        private readonly string? _previousDisableFirstRun;
        private readonly string? _previousDisableDebugHint;

        public SafeHeadlessUnitTestSession(
            HeadlessUnitTestSession inner,
            string? previousLaunchProfilesPath,
            string? previousSettingsPath,
            string? previousDisableFirstRun,
            string? previousDisableDebugHint)
        {
            _inner = inner;
            _previousLaunchProfilesPath = previousLaunchProfilesPath;
            _previousSettingsPath = previousSettingsPath;
            _previousDisableFirstRun = previousDisableFirstRun;
            _previousDisableDebugHint = previousDisableDebugHint;
        }

        public Task Dispatch(Action action, CancellationToken cancellationToken) =>
            _inner.Dispatch(action, cancellationToken);

        public Task<T> Dispatch<T>(Func<T> action, CancellationToken cancellationToken) =>
            _inner.Dispatch(action, cancellationToken);

        public Task Dispatch(Func<Task> action, CancellationToken cancellationToken) =>
            _inner.Dispatch(async () =>
            {
                await action().ConfigureAwait(true);
                return true;
            }, cancellationToken);

        public Task<T> Dispatch<T>(Func<Task<T>> action, CancellationToken cancellationToken) =>
            _inner.Dispatch(action, cancellationToken);

        public void Dispose()
        {
            try
            {
                _inner.Dispatch(
                    () => ModAnimation.ResetForTesting(),
                    CancellationToken.None).GetAwaiter().GetResult();
                _inner.Dispose();
            }
            catch (Exception ex) when (IsAvaloniaHeadlessTeardown(ex))
            {
            }
            finally
            {
                Environment.SetEnvironmentVariable("PCLN_LAUNCH_PROFILES_PATH", _previousLaunchProfilesPath);
                Environment.SetEnvironmentVariable("PCLN_LAUNCHER_SETTINGS_PATH", _previousSettingsPath);
                Environment.SetEnvironmentVariable("PCL_DISABLE_FIRST_RUN", _previousDisableFirstRun);
                Environment.SetEnvironmentVariable("PCL_DISABLE_DEBUG_HINT", _previousDisableDebugHint);
            }
        }

        private static bool IsAvaloniaHeadlessTeardown(Exception ex)
        {
            if (ex.StackTrace?.Contains(
                "Avalonia.Headless.HeadlessUnitTestSession.Dispose",
                StringComparison.Ordinal) != true)
            {
                return false;
            }

            if (ex is NullReferenceException)
                return true;

            if (ex is AggregateException aggregate)
                return aggregate.Flatten().InnerExceptions.Any(IsKnownAvaloniaHeadlessTeardownInnerException);

            return IsKnownAvaloniaHeadlessTeardownInnerException(ex);
        }

        private static bool IsKnownAvaloniaHeadlessTeardownInnerException(Exception ex) =>
            ex is NullReferenceException ||
            ex is InvalidOperationException invalidOperation &&
            (invalidOperation.Message.Contains("different thread owns it", StringComparison.OrdinalIgnoreCase) ||
             invalidOperation.Message.Contains("Avalonia.Input.IInputManager", StringComparison.Ordinal));
    }

    private static SolidColorBrush RequiredBrush(string key) =>
        Avalonia.Application.Current!.Resources[key] as SolidColorBrush
        ?? throw new InvalidOperationException($"Resource '{key}' is not a SolidColorBrush.");

    private static void SaveUiSnapshot(TopLevel topLevel, string name)
    {
        using Avalonia.Media.Imaging.Bitmap? bitmap = topLevel.CaptureRenderedFrame();
        Assert.IsNotNull(bitmap);

        string directory = System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "TestResults", "ui");
        directory = System.IO.Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        string path = System.IO.Path.Combine(directory, $"{name}.png");
        bitmap.Save(path);
        Console.WriteLine($"Saved UI snapshot: {path}");
    }

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

    private static void RightClick(Window window, Control control)
    {
        Point center = control
            .TranslatePoint(
                new Point(control.Bounds.Width / 2, control.Bounds.Height / 2),
                window)
            ?? throw new InvalidOperationException("Control is not attached.");

        window.MouseDown(center, MouseButton.Right);
        window.MouseUp(center, MouseButton.Right);
    }

    private static void TypeText(Window window, string text)
    {
        foreach (char ch in text)
        {
            Key key = ch switch
            {
                >= 'A' and <= 'Z' => (Key)Enum.Parse(typeof(Key), ch.ToString()),
                >= 'a' and <= 'z' => (Key)Enum.Parse(typeof(Key), char.ToUpperInvariant(ch).ToString()),
                >= '0' and <= '9' => (Key)Enum.Parse(typeof(Key), "D" + ch),
                _ => Key.None
            };
            PhysicalKey physicalKey = ch switch
            {
                >= 'A' and <= 'Z' => (PhysicalKey)Enum.Parse(typeof(PhysicalKey), ch.ToString()),
                >= 'a' and <= 'z' => (PhysicalKey)Enum.Parse(typeof(PhysicalKey), char.ToUpperInvariant(ch).ToString()),
                >= '0' and <= '9' => (PhysicalKey)Enum.Parse(typeof(PhysicalKey), "Digit" + ch),
                _ => PhysicalKey.None
            };
            window.KeyPress(key, RawInputModifiers.None, physicalKey, ch.ToString());
        }
    }

    private static void ClickAt(Window window, Control control, Point position)
    {
        Point point = control.TranslatePoint(position, window)
            ?? throw new InvalidOperationException("Control is not attached.");

        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    private static void MoveTo(Window window, Control control)
    {
        Point center = control
            .TranslatePoint(
                new Point(control.Bounds.Width / 2, control.Bounds.Height / 2),
                window)
            ?? throw new InvalidOperationException("Control is not attached.");

        window.MouseMove(center, RawInputModifiers.None);
    }

    private static void Drag(Window window, Control control, Point from, Point to)
    {
        Point start = control.TranslatePoint(from, window)
            ?? throw new InvalidOperationException("Control is not attached.");
        Point end = control.TranslatePoint(to, window)
            ?? throw new InvalidOperationException("Control is not attached.");

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(end, RawInputModifiers.LeftMouseButton);
        window.MouseUp(end, MouseButton.Left);
    }

    private static Border GetCheckIndicator(MyListItem item) =>
        item.Children
            .OfType<Border>()
            .Single(border => Math.Abs(border.Width - 5d) < 0.01d);

    private static T? FindVisual<T>(Control root, string? name = null)
        where T : Control =>
        root.GetVisualDescendants()
            .OfType<T>()
            .FirstOrDefault(control => name is null || string.Equals(control.Name, name, StringComparison.Ordinal));

    private static void AdvanceNavigationAnimation(MainWindow window)
    {
        InvokePrivateTick(window, "NavAnimTimer_Tick", 14);
    }

    private static void AdvancePageChangeAnimation(MainWindow window)
    {
        ModAnimation.AdvanceForTesting(16, 32);
    }

    private static void InvokePrivateMethod(object target, string methodName)
    {
        var method = target.GetType().GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");
        method.Invoke(target, null);
    }

    private static void InvokePrivateMethod(object target, string methodName, params object?[] arguments)
    {
        var method = target.GetType().GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");
        method.Invoke(target, arguments);
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

    private static void InvokePrivateNoArgs(object target, string methodName)
    {
        var method = target.GetType().GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found.");
        method.Invoke(target, null);
    }

    private static double GetPrivateDouble(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        return (double)field.GetValue(instance)!;
    }

    private static void SetPrivateField(object instance, string fieldName, object value)
    {
        var field = instance.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        field.SetValue(instance, value);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        Type? type = instance.GetType();
        System.Reflection.FieldInfo? field = null;
        while (type is not null && field is null)
        {
            field = type.GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            type = type.BaseType;
        }

        if (field is null)
            throw new InvalidOperationException($"Field '{fieldName}' was not found.");
        return (T)field.GetValue(instance)!;
    }

    private sealed class FakeMinecraftLoaderMetadataService : IMinecraftLoaderMetadataService
    {
        public Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetLoaderVersionsAsync(
            MinecraftLoaderKind kind,
            string gameVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MinecraftLoaderVersionEntry>>(
            [
                new MinecraftLoaderVersionEntry(
                    kind,
                    kind == MinecraftLoaderKind.OptiFine ? "1.20.1_HD_U_I6" : "0.16.14",
                    true)
            ]);

        public Task<MinecraftLoaderInstallMetadata> GetLoaderInstallMetadataAsync(
            MinecraftLoaderInstallRequest request,
            string gameVersion,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class DelayedMinecraftLoaderMetadataService : IMinecraftLoaderMetadataService
    {
        private readonly Dictionary<MinecraftLoaderKind, TaskCompletionSource<IReadOnlyList<MinecraftLoaderVersionEntry>>> _completions = [];
        private readonly Dictionary<MinecraftLoaderKind, int> _requestCounts = [];

        public Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetLoaderVersionsAsync(
            MinecraftLoaderKind kind,
            string gameVersion,
            CancellationToken cancellationToken = default)
        {
            _requestCounts[kind] = GetRequestCount(kind) + 1;
            if (!_completions.TryGetValue(kind, out TaskCompletionSource<IReadOnlyList<MinecraftLoaderVersionEntry>>? completion))
            {
                completion = new TaskCompletionSource<IReadOnlyList<MinecraftLoaderVersionEntry>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _completions[kind] = completion;
            }

            return completion.Task.WaitAsync(cancellationToken);
        }

        public Task<MinecraftLoaderInstallMetadata> GetLoaderInstallMetadataAsync(
            MinecraftLoaderInstallRequest request,
            string gameVersion,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public int GetRequestCount(MinecraftLoaderKind kind) =>
            _requestCounts.TryGetValue(kind, out int count) ? count : 0;

        public void Complete(MinecraftLoaderKind kind)
        {
            if (_completions.TryGetValue(kind, out TaskCompletionSource<IReadOnlyList<MinecraftLoaderVersionEntry>>? completion))
            {
                completion.TrySetResult(
                [
                    new MinecraftLoaderVersionEntry(kind, "0.16.14", true)
                ]);
            }
        }
    }

    private sealed class FakeMinecraftInstallAddonMetadataService : IMinecraftInstallAddonMetadataService
    {
        public Task<IReadOnlyList<MinecraftInstallAddonVersionEntry>> GetVersionsAsync(
            MinecraftInstallAddonKind kind,
            string gameVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MinecraftInstallAddonVersionEntry>>(
            [
                new MinecraftInstallAddonVersionEntry(
                    kind,
                    "0.100.0+1.20.1",
                    kind.ToString().ToLowerInvariant() + ".jar",
                    "https://cdn.example/" + kind.ToString().ToLowerInvariant() + ".jar",
                    null,
                    1234,
                    true)
            ]);
    }

    private sealed class DelegateHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(handle(request));
    }
}
