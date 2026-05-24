using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Network;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageDownloadClient
{
    public PageDownloadClient()
    {
        Initialized += (_, _) => LoaderInit();
        Loaded += (_, _) => Init();
        InitializeComponent();
    }

    private void LoaderInit()
    {
        PageLoaderInit(Load, PanLoad, PanBack, null, ModDownload.DlClientListLoader, _ => Load_OnFinish());
    }

    private void Init()
    {
        PanBack.ScrollToHome();
    }

    private void Load_OnFinish()
    {
        // 结果数据化
        try
        {
            // 归类
            var Dict = new Dictionary<string, List<JsonObject>>
            {
                { "正式版", new List<JsonObject>() }, { "预览版", new List<JsonObject>() }, { "远古版", new List<JsonObject>() },
                { "愚人节版", new List<JsonObject>() }
            };
            var Versions = (JsonArray)ModDownload.DlClientListLoader.Output.Value["versions"];
            foreach (JsonObject Version in Versions)
            {
                // 确定分类
                var Type = (string)Version["type"];
                switch (Type ?? "")
                {
                    case "release":
                    {
                        Type = "正式版";
                        break;
                    }
                    case "snapshot":
                    case "pending":
                    {
                        Type = "预览版";
                        // Mojang 误分类
                        if (Version["id"].ToString().StartsWith("1.") &&
                            !Version["id"].ToString().ToLower().Contains("combat") &&
                            !Version["id"].ToString().ToLower().Contains("rc") &&
                            !Version["id"].ToString().ToLower().Contains("experimental") &&
                            !Version["id"].ToString().ToLower().Equals("1.2") &&
                            !Version["id"].ToString().ToLower().Contains("pre"))
                        {
                            Type = "正式版";
                            Version["type"] = "release";
                        }

                        // 愚人节版本
                        switch (Version["id"].ToString().ToLower() ?? "")
                        {
                            case "2point0_blue":
                            case "2point0_red":
                            case "2point0_purple":
                            case "2.0_blue":
                            case "2.0_red":
                            case "2.0_purple":
                            case "2.0":
                            {
                                Type = "愚人节版";
                                Version["id"] = Version["id"].ToString().Replace("point", ".");
                                Version["type"] = "special";
                                Version.Add("lore", ModMinecraft.GetMcFoolName((string)Version["id"]));
                                break;
                            }
                            case "20w14infinite":
                            case "20w14∞":
                            {
                                Type = "愚人节版";
                                Version["id"] = "20w14∞";
                                Version["type"] = "special";
                                Version.Add("lore", ModMinecraft.GetMcFoolName((string)Version["id"]));
                                break;
                            }
                            case "3d shareware v1.34":
                            case "1.rv-pre1":
                            case "15w14a":
                            case var @case when @case == "2.0":
                            case "22w13oneblockatatime":
                            case "23w13a_or_b":
                            case "24w14potato":
                            case "25w14craftmine":
                            case "26w14a":
                            {
                                Type = "愚人节版";
                                Version["type"] = "special";
                                Version.Add("lore", ModMinecraft.GetMcFoolName((string)Version["id"])); // 4/1 自动视作愚人节版
                                break;
                            }

                            default:
                            {
                                var ReleaseDate = Version["releaseTime"].GetValue<DateTime>().ToUniversalTime()
                                    .AddHours(2d);
                                if (ReleaseDate.Month == 4 && ReleaseDate.Day == 1)
                                {
                                    Type = "愚人节版";
                                    Version["type"] = "special";
                                }

                                break;
                            }
                        }

                        break;
                    }
                    case "special":
                    {
                        // 已被处理的愚人节版
                        Type = "愚人节版";
                        break;
                    }

                    default:
                    {
                        Type = "远古版";
                        break;
                    }
                }

                // 加入辞典
                Dict[Type].Add(Version);
            }

            // 排序
            for (int i = 0, loopTo = Dict.Keys.Count - 1; i <= loopTo; i++)
                Dict[Dict.Keys.ElementAtOrDefault(i)] = Dict.Values.ElementAtOrDefault(i)
                    .OrderByDescending(v => v["releaseTime"].GetValue<DateTime>()).ToList();
            // 清空当前
            PanMain.Children.Clear();
            // 添加最新版本
            var CardInfo = new MyCard { Title = "最新版本", Margin = new Thickness(0d, 0d, 0d, 15d) };
            var TopestVersions = new List<JsonObject>();
            var Release = (JsonObject)Dict["正式版"][0].DeepClone();
            Release["lore"] =
                "最新正式版，发布于 " + Lang.Date(Release["releaseTime"].GetValue<DateTime>(), "g");
            TopestVersions.Add(Release);
            if (Dict["正式版"][0]["releaseTime"].GetValue<DateTime>() < Dict["预览版"][0]["releaseTime"].GetValue<DateTime>())
            {
                var Snapshot = (JsonObject)Dict["预览版"][0].DeepClone();
                Snapshot["lore"] = "最新预览版，发布于 " +
                                   Lang.Date(Snapshot["releaseTime"].GetValue<DateTime>(), "g");
                TopestVersions.Add(Snapshot);
            }

            var PanInfo = new StackPanel
            {
                Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d), VerticalAlignment = VerticalAlignment.Top,
                RenderTransform = new TranslateTransform(0d, 0d), Tag = TopestVersions
            };

            void PutMethod(StackPanel Stack)
            {
                foreach (var item in (IEnumerable)Stack.Tag)
                    Stack.Children.Add(ModDownloadLib.McDownloadListItem((JsonObject)item,
                        ModDownloadLib.McDownloadMenuSave, true));
            }

            ;
            MyCard.StackInstall(ref PanInfo, PutMethod);
            CardInfo.Children.Add(PanInfo);
            PanMain.Children.Add(CardInfo);
            // 添加其他版本
            foreach (var Pair in Dict)
            {
                if (!Pair.Value.Any())
                    continue;
                // 增加卡片
                var NewCard = new MyCard
                    { Title = Pair.Key + " (" + Pair.Value.Count + ")", Margin = new Thickness(0d, 0d, 0d, 15d) };
                var NewStack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d),
                    Tag = Pair.Value
                };
                NewCard.Children.Add(NewStack);
                NewCard.SwapControl = NewStack;
                NewCard.InstallMethod = PutMethod;
                NewCard.IsSwapped = true;
                PanMain.Children.Add(NewCard);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 MC 版本列表出错", ModBase.LogLevel.Feedback);
        }
    }
}