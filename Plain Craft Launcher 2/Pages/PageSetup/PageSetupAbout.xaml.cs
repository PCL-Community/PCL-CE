using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using PCL.Core.IO.Net.Http;

namespace PCL;

public partial class PageSetupAbout
{
    // 彩蛋
    private int ClickCount;

    private new bool IsLoaded;

    public PageSetupAbout()
    {
        InitializeComponent();
        Loaded += PageOtherAbout_Loaded;
    }

    public ObservableCollection<GitHubContributor> Contributors { get; set; } = new();

    private void PageOtherAbout_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();

        // 非重复加载部分
        if (IsLoaded)
            return;
        IsLoaded = true;

        ItemAboutPcl.Info = ItemAboutPcl.Info.Replace("%VERSION%", ModBase.VersionBaseName)
            .Replace("%VERSIONCODE%", ModBase.VersionCode.ToString()).Replace("%BRANCH%", ModBase.VersionBranchName)
            .Replace("%COMMIT_HASH%", ModBase.CommitHashShort);
        LoadContributersAsync();
    }

    private async void LoadContributersAsync()
    {
        try
        {
            using (var response = await HttpRequest
                       .Create("https://api.github.com/repos/PCL-Community/PCL2-CE/contributors").SendAsync())
            {
                response.EnsureSuccessStatusCode();
                var cos = await response.AsJsonAsync<List<GitHubContributor>>();
                Contributors.Clear();
                foreach (var item in cos)
                    Contributors.Add((GitHubContributor)item);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "加载贡献者信息失败");
        }
    }

    private void ImgPCLCommunity_Click(object sender, MouseButtonEventArgs e)
    {
        ModAnimation.AniStart(new[] { ModAnimation.AaRotateTransform(sender, 360d) });
    }

    private void ImgPCLLogo_Click(object sender, MouseButtonEventArgs e)
    {
        if (ClickCount < 200)
        {
            ClickCount += 1;
            switch (ClickCount)
            {
                case 5:
                {
                    ModMain.Hint("点这个很好玩么……");
                    break;
                }
                case 15:
                {
                    ModMain.Hint("还点？");
                    break;
                }
                case 25:
                {
                    switch (ModMain.MyMsgBox("你现在是不是超无聊的？", "咕咕咕？", "是的", "并不是"))
                    {
                        case 2:
                        {
                            ModMain.Hint("那你还点啥……真是搞不懂。");
                            break;
                        }
                    }

                    break;
                }
                case 50:
                {
                    ModMain.Hint("嗯，加油吧，嗯……");
                    break;
                }
                case 75:
                {
                    ModMain.Hint("隐藏主题 混乱黄 已……嗯不对，这是 PCL 社区版，应该没有这玩意……");
                    break;
                }
                case 100:
                {
                    ModMain.Hint("你咋还这么无聊啊？");
                    break;
                }
                case 130:
                {
                    ModMain.Hint("后面什么都没有了哦！");
                    break;
                }
                case 150:
                {
                    switch (ModMain.MyMsgBox("你真的不累么？", "温馨提示", "累死了", "真的不累"))
                    {
                        case 1:
                        {
                            ModMain.Hint("那你就别点了喂……后面真的真的真的什么都没有了！");
                            break;
                        }
                        case 2:
                        {
                            switch (ModMain.MyMsgBox("你真的真的不累么？", "超温馨的温馨提示", "累死了", "真的真的不累"))
                            {
                                case 1:
                                {
                                    ModMain.Hint("那你就别点了喂……后面真的真的真的什么都没有了！");
                                    break;
                                }
                                case 2:
                                {
                                    switch (ModMain.MyMsgBox("你真的真的真的不累么？", "超超超温馨的温馨提示", "累死了", "真的真的真的不累"))
                                    {
                                        case 1:
                                        {
                                            ModMain.Hint("那你就别点了喂……后面真的真的真的什么都没有了！");
                                            break;
                                        }
                                        case 2:
                                        {
                                            ModMain.Hint("好吧……不过后面是真的啥也没了，不用点了真的。");
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
                    ModMain.Hint("还点，还点就不让你点了……");
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

    public class GitHubContributor
    {
        [JsonPropertyName("login")] public string Login { get; set; }

        [JsonPropertyName("avatar_url")] public string AvatarUrl { get; set; }

        [JsonPropertyName("html_url")] public string HtmlUrl { get; set; }

        [JsonPropertyName("contributions")] public int Contributions { get; set; }
    }
}