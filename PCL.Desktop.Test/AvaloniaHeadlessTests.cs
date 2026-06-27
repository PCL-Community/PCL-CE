// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using PCL.Desktop;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Views;
using PCL.Desktop.Views.Launch;

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
                Assert.AreEqual("正在加载下载页面", FindVisual<MyLoading>(window, "LoadMain")!.Text);
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
                Assert.AreEqual("正在加载社区页面", FindVisual<MyLoading>(window, "LoadMain")!.Text);
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
    public void LaunchInstanceDiscovery_FindsVersionJsons()
    {
        string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcl-launch-discovery-" + Guid.NewGuid().ToString("N"));
        try
        {
            string versionDirectory = System.IO.Path.Combine(root, "versions", "1.20.1");
            Directory.CreateDirectory(versionDirectory);
            File.WriteAllText(System.IO.Path.Combine(versionDirectory, "1.20.1.json"), "{}");

            IReadOnlyList<LaunchInstanceInfo> instances = LaunchInstanceDiscovery.Discover([root]);

            Assert.AreEqual(1, instances.Count);
            Assert.AreEqual("1.20.1", instances[0].Name);
            Assert.AreEqual(versionDirectory, instances[0].InstanceDirectory);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void PageLaunchLeft_PreservesLoginAndLaunchExtensionSurfaces()
    {
        using HeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageLaunchLeft page = new();
            TextBlock loginPage = new() { Text = "登录分页" };
            page.SetLoginPage(loginPage, animate: false);
            page.SetInstances([new LaunchInstanceInfo("1.20.1", @"D:\Minecraft\versions\1.20.1\1.20.1.json", @"D:\Minecraft\versions\1.20.1")]);

            Assert.AreSame(loginPage, page.CurrentLoginPage);
            Assert.AreEqual("1.20.1", page.SelectedInstance!.Name);
            Assert.AreEqual("启动游戏", page.FindControl<MyButton>("BtnLaunch")!.Text);
            Assert.IsFalse(page.FindControl<MyButton>("BtnLaunch")!.IsEnabled);
            Assert.IsTrue(page.FindControl<Control>("BtnMore")!.IsVisible);
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageLaunchLeft_FollowsWpfLaunchButtonStateRules()
    {
        using HeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageLaunchLeft page = new();
            LaunchInstanceInfo instance = new("1.20.1", @"D:\Minecraft\versions\1.20.1\1.20.1.json", @"D:\Minecraft\versions\1.20.1");

            page.SetInstanceLoading(isLoading: true);
            Assert.AreEqual("正在加载", page.FindControl<MyButton>("BtnLaunch")!.Text);
            Assert.IsFalse(page.FindControl<MyButton>("BtnLaunch")!.IsEnabled);
            Assert.IsFalse(page.FindControl<Control>("BtnInstance")!.IsEnabled);

            page.SetInstances([instance]);
            Assert.AreEqual("启动游戏", page.FindControl<MyButton>("BtnLaunch")!.Text);
            Assert.IsFalse(page.FindControl<MyButton>("BtnLaunch")!.IsEnabled);

            page.SetSelectedProfilePresent(true);
            Assert.IsTrue(page.FindControl<MyButton>("BtnLaunch")!.IsEnabled);

            page.SetInstances([]);
            page.SetPreferenceState(isDownloadPageHidden: false, isFunctionSelectHidden: false);
            Assert.AreEqual("下载游戏", page.FindControl<MyButton>("BtnLaunch")!.Text);
            Assert.IsTrue(page.FindControl<MyButton>("BtnLaunch")!.IsEnabled);

            page.SetPreferenceState(isDownloadPageHidden: true, isFunctionSelectHidden: false);
            Assert.AreEqual("启动游戏", page.FindControl<MyButton>("BtnLaunch")!.Text);
            Assert.IsFalse(page.FindControl<MyButton>("BtnLaunch")!.IsEnabled);
        }, CancellationToken.None);
    }

    [TestMethod]
    public void PageLaunchLeft_GuardsLaunchLikeWpf()
    {
        using HeadlessUnitTestSession session = CreateSession();
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
        using HeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            LoginProfileInfo profile = new("Steve", "离线登录", LaunchLoginProfileKind.Offline);
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

                Click(window, FindVisual<MyListItem>(profilePage)!);

                Assert.AreEqual(profile, selected);

                PageLoginProfileSkin skinPage = new();
                skinPage.SetProfile(profile);
                Assert.AreEqual("Steve", skinPage.FindControl<TextBlock>("TextName")!.Text);
                Assert.IsFalse(skinPage.FindControl<MyIconButton>("BtnEdit")!.IsVisible);
            }
            finally
            {
                window.Close();
            }
        }, CancellationToken.None);
    }

    [TestMethod]
    public void MainWindow_WiresLaunchLoginPagesInsteadOfPlaceholders()
    {
        using HeadlessUnitTestSession session = CreateSession();

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
        using HeadlessUnitTestSession session = CreateSession();

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
                Assert.AreEqual(3, dialog.Items.Count);
                Assert.IsFalse(dialog.FindControl<MyButton>("Btn1")!.IsEnabled);

                Click(window, dialog.Items[1]);
                Assert.IsTrue(dialog.FindControl<MyButton>("Btn1")!.IsEnabled);

                Click(window, dialog.FindControl<MyButton>("Btn1")!);

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
    public void PageLoginMs_UsesWpfStartAndFinishState()
    {
        using HeadlessUnitTestSession session = CreateSession();

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
        using HeadlessUnitTestSession session = CreateSession();

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
        using HeadlessUnitTestSession session = CreateSession();

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
    public void PageLaunchRight_PreservesCustomHomepageSurface()
    {
        using HeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            PageLaunchRight page = new();
            TextBlock customBlock = new()
            {
                Tag = "homepage-title",
                Text = "自定义主页"
            };

            page.AddCustomContent(customBlock);
            page.AppendLog("测试日志");

            Assert.AreSame(customBlock, page.CustomPanel!.Children.Single());
            Assert.IsTrue(page.FindControl<TextBlock>("LabLog")!.Text!.Contains("测试日志", StringComparison.Ordinal));
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
    public void MyCard_UsesWpfChromeAndTitleLayer()
    {
        using HeadlessUnitTestSession session = CreateSession();

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
    public void MyCard_SwapClickTogglesContentAndCanBeCancelled()
    {
        using HeadlessUnitTestSession session = CreateSession();

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
                Assert.IsTrue(double.IsNaN(card.Height));
                Assert.IsNull(lazyContent.Tag);
                Assert.AreEqual(2, lazyContent.Children.Count);
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
    public void MySlider_TracksValueKeyboardDragAndPopupLikeWpf()
    {
        using HeadlessUnitTestSession session = CreateSession();

        session.Dispatch(() =>
        {
            MySlider slider = new()
            {
                Width = 200,
                MaxValue = 100,
                Value = 50,
                ValueByKey = 5,
                getHintText = new Func<object, object>(value => $"值 {value}")
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

                Line lineFore = slider.FindControl<Line>("LineFore")!;
                Line lineBack = slider.FindControl<Line>("LineBack")!;
                Ellipse dot = slider.FindControl<Ellipse>("ShapeDot")!;
                Popup popup = slider.FindControl<Popup>("Popup")!;
                TextBlock textHint = slider.FindControl<TextBlock>("TextHint")!;

                Assert.AreEqual(95.5d, lineFore.Width, 0.01d);
                Assert.AreEqual(95.5d, lineBack.Width, 0.01d);
                Assert.AreEqual(new Thickness(95d, 0d, 0d, 0d), dot.Margin);

                slider.Focus();
                window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, string.Empty);

                Assert.AreEqual(55, slider.Value);
                Assert.IsFalse(lastChangeUserFlag);
                Assert.AreEqual("值 55", textHint.Text);
                Assert.IsTrue(popup.IsOpen);

                Drag(window, slider, new Point(10d, 8d), new Point(157d, 8d));

                Assert.AreEqual(80, slider.Value);
                Assert.IsFalse(lastChangeUserFlag);
                Assert.IsFalse(popup.IsOpen);
                Assert.IsTrue(changeCount >= 2);
                Assert.AreEqual(152.5d, lineFore.Width, 0.01d);
                Assert.AreEqual(new Thickness(152d, 0d, 0d, 0d), dot.Margin);
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
        using HeadlessUnitTestSession session = CreateSession();

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
        using HeadlessUnitTestSession session = CreateSession();

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

                comboBox.IsDropDownOpen = true;
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
    public void MyComboBox_EditableTextClearsStaleSelectionLikeWpf()
    {
        using HeadlessUnitTestSession session = CreateSession();

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
                comboBox.Text = "手动输入";

                Assert.AreEqual("手动输入", comboBox.Text);
                Assert.IsNull(comboBox.SelectedItem);
                Assert.AreEqual(1, textChangedCount);

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

    private static HeadlessUnitTestSession CreateSession()
    {
        Environment.SetEnvironmentVariable(
            "PCLN_LAUNCH_PROFILES_PATH",
            System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "pcl-desktop-test-profiles-" + Guid.NewGuid().ToString("N") + ".json"));
        return HeadlessUnitTestSession.StartNew(
            typeof(App),
            AvaloniaTestIsolationLevel.PerTest);
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

    private static void ClickAt(Window window, Control control, Point position)
    {
        Point point = control.TranslatePoint(position, window)
            ?? throw new InvalidOperationException("Control is not attached.");

        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
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
