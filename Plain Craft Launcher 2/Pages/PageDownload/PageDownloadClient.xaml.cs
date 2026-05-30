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
        PageLoaderInit(Load, PanLoad, PanBack, null, ModDownload.dlClientListLoader, _ => Load_OnFinish());
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
            var categoryOrder = new[]
            {
                McVersionCategory.Release,
                McVersionCategory.Snapshot,
                McVersionCategory.BeforeRelease,
                McVersionCategory.AprilFools
            };

            var dict = categoryOrder.ToDictionary(
                category => category,
                _ => new List<JsonObject>()
            );

            var versions = (JsonArray)ModDownload.dlClientListLoader.output.value["versions"];
            foreach (JsonObject Version in versions)
            {
                var cat = McVersionClassifier.ClassifyVersion(Version);
                dict[cat].Add(Version);
            }

            foreach (var category in categoryOrder)
                dict[category] = dict[category]
                    .OrderByDescending(McVersionClassifier.GetReleaseTime)
                    .ToList();

            PanMain.Children.Clear();

            var cardInfo = new MyCard { Title = Lang.Text("Download.Version.Latest.Title"), Margin = new Thickness(0d, 0d, 0d, 15d) };
            var topestVersions = new List<JsonObject>();
            var release = (JsonObject)dict[McVersionCategory.Release][0].DeepClone();
            release["lore"] = Lang.Text("Download.Version.Latest.Release", Lang.Date(McVersionClassifier.GetReleaseTime(release), "g"));
            topestVersions.Add(release);
            if (McVersionClassifier.GetReleaseTime(dict[McVersionCategory.Release][0]) < McVersionClassifier.GetReleaseTime(dict[McVersionCategory.Snapshot][0]))
            {
                var snapshot = (JsonObject)dict[McVersionCategory.Snapshot][0].DeepClone();
                snapshot["lore"] = Lang.Text("Download.Version.Latest.Development",
                                   Lang.Date(McVersionClassifier.GetReleaseTime(snapshot), "g"));
                topestVersions.Add(snapshot);
            }

            var panInfo = new StackPanel
            {
                Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d), VerticalAlignment = VerticalAlignment.Top,
                RenderTransform = new TranslateTransform(0d, 0d), Tag = topestVersions
            };

            void PutMethod(StackPanel Stack)
            {
                foreach (var item in (IEnumerable)Stack.Tag)
                    Stack.Children.Add(ModDownloadLib.McDownloadListItem((JsonObject)item,
                        ModDownloadLib.McDownloadMenuSave, true));
            }

            ;
            MyCard.StackInstall(ref panInfo, PutMethod);
            cardInfo.Children.Add(panInfo);
            PanMain.Children.Add(cardInfo);

            foreach (var Pair in dict)
            {
                if (!Pair.Value.Any())
                    continue;

                var newCard = new MyCard
                    { Title = McVersionClassifier.GetCategoryDisplayName(Pair.Key) + " (" + Pair.Value.Count + ")", Margin = new Thickness(0d, 0d, 0d, 15d) };
                var newStack = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                    VerticalAlignment = VerticalAlignment.Top, RenderTransform = new TranslateTransform(0d, 0d),
                    Tag = Pair.Value
                };
                newCard.Children.Add(newStack);
                newCard.SwapControl = newStack;
                newCard.InstallMethod = PutMethod;
                newCard.IsSwapped = true;
                PanMain.Children.Add(newCard);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "可视化 MC 版本列表出错", ModBase.LogLevel.Feedback);
        }
    }
}