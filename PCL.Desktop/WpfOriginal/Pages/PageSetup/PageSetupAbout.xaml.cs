using System.Windows;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageSetupAbout
{
    // 彩蛋
    private int clickCount;

    private new bool isLoaded;

    public PageSetupAbout()
    {
        InitializeComponent();
        Loaded += PageOtherAbout_Loaded;
    }

    private void PageOtherAbout_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();

        // 非重复加载部分
        if (isLoaded)
            return;
        isLoaded = true;

        ItemAboutPcl.Info = ItemAboutPcl.Info.Replace("%VERSION%", Basics.VersionName)
            .Replace("%VERSIONCODE%", Basics.VersionCode.ToString()).Replace("%BRANCH%", Basics.VersionBranch)
            .Replace("%COMMIT_HASH%", Secrets.CommitHash);

        if (!Lang.IsChineseMainland)
        {
            ItemMcmod.Visibility = Visibility.Collapsed;
            BtnMcmod.Visibility = Visibility.Collapsed;
            ImgMcmod.Visibility = Visibility.Collapsed;
        }
    }

    private void ImgPCLCommunity_Click(object sender, MouseButtonEventArgs e)
    {
        ModAnimation.AniStart(new[] { ModAnimation.AaRotateTransform(sender, 360d) });
    }

    private void ImgPCLLogo_Click(object sender, MouseButtonEventArgs e)
    {
        if (clickCount < 200)
        {
            clickCount += 1;
            switch (clickCount)
            {
                case 5:
                {
                    ModMain.Hint(Lang.Text("Setup.About.EasterEgg.NiceClick"));
                    break;
                }
                case 15:
                {
                    ModMain.Hint(Lang.Text("Setup.About.EasterEgg.StillClicking"));
                    break;
                }
                case 25:
                {
                    switch (ModMain.MyMsgBox(Lang.Text("Setup.About.EasterEgg.Bored.Message"), Lang.Text("Setup.About.EasterEgg.Bored.Title"), Lang.Text("Setup.About.EasterEgg.Bored.Yes"), Lang.Text("Setup.About.EasterEgg.Bored.No")))
                    {
                        case 2:
                        {
                            ModMain.Hint(Lang.Text("Setup.About.EasterEgg.Bored.Response"));
                            break;
                        }
                    }

                    break;
                }
                case 50:
                {
                    ModMain.Hint(Lang.Text("Setup.About.EasterEgg.Encouragement"));
                    break;
                }
                case 75:
                {
                    ModMain.Hint(Lang.Text("Setup.About.EasterEgg.HiddenTheme"));
                    break;
                }
                case 100:
                {
                    ModMain.Hint(Lang.Text("Setup.About.EasterEgg.StillStaring"));
                    break;
                }
                case 130:
                {
                    ModMain.Hint(Lang.Text("Setup.About.EasterEgg.NothingBehind"));
                    break;
                }
                case 150:
                {
                    switch (ModMain.MyMsgBox(Lang.Text("Setup.About.EasterEgg.Tired.Message1"), Lang.Text("Setup.About.EasterEgg.Tired.Title1"), Lang.Text("Setup.About.EasterEgg.Tired.Exhausted"), Lang.Text("Setup.About.EasterEgg.Tired.NotTired")))
                    {
                        case 1:
                        {
                            ModMain.Hint(Lang.Text("Setup.About.EasterEgg.Tired.StopClicking"));
                            break;
                        }
                        case 2:
                        {
                            switch (ModMain.MyMsgBox(Lang.Text("Setup.About.EasterEgg.Tired.Message2"), Lang.Text("Setup.About.EasterEgg.Tired.Title2"), Lang.Text("Setup.About.EasterEgg.Tired.Exhausted"), Lang.Text("Setup.About.EasterEgg.Tired.NotTired")))
                            {
                                case 1:
                                {
                                    ModMain.Hint(Lang.Text("Setup.About.EasterEgg.Tired.StopClicking"));
                                    break;
                                }
                                case 2:
                                {
                                    switch (ModMain.MyMsgBox(Lang.Text("Setup.About.EasterEgg.Tired.Message3"), Lang.Text("Setup.About.EasterEgg.Tired.Title3"), Lang.Text("Setup.About.EasterEgg.Tired.Exhausted"), Lang.Text("Setup.About.EasterEgg.Tired.ReallyNotTired")))
                                    {
                                        case 1:
                                        {
                                            ModMain.Hint(Lang.Text("Setup.About.EasterEgg.Tired.StopClicking"));
                                            break;
                                        }
                                        case 2:
                                        {
                                            ModMain.Hint(Lang.Text("Setup.About.EasterEgg.Tired.FinallyGiveUp"));
                                            break;
                                        }
                                    }

                                    break;
                                }
                            }

                            break;
                        }
                    }

                    break;
                }
                case 200:
                {
                    ModMain.Hint(Lang.Text("Setup.About.EasterEgg.ClickDisabled"));
                    ImgPCLLogo.IsHitTestVisible = false;
                    return;
                }
            }

            var rand = new Random();
            var mx = rand.Next(-1, 1);
            if (mx == 0)
                mx = 1;
            var my = rand.Next(-1, 1);
            if (my == 0)
                my = 1;
            ModAnimation.AniStart(new[]
            {
                ModAnimation.AaTranslateX(sender, mx, 0), ModAnimation.AaTranslateY(sender, my, 0),
                ModAnimation.AaTranslateX(sender, -mx, 0, 100), ModAnimation.AaTranslateY(sender, -my, 0, 100)
            });
        }
    }

}