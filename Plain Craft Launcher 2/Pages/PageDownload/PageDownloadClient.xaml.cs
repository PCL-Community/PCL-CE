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
        Load.Text = Lang.Text("Download.Version.Client.LoadingList");
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
            var categoryOrder = new[]
            {
                McVersionCategory.Release,
                McVersionCategory.Snapshot,
                McVersionCategory.BeforeRelease,
                McVersionCategory.AprilFools
            };

            var Dict = categoryOrder.ToDictionary(
                category => category,
                _ => new List<JsonObject>()
            );

            var Versions = (JsonArray)ModDownload.DlClientListLoader.Output.Value["versions"];
            foreach (JsonObject Version in Versions)
            {
                var cat = McVersionClassifier.ClassifyVersion(Version);
                Dict[cat].Add(Version);
            }

            foreach (var category in categoryOrder)
                Dict[category] = Dict[category]
                    .OrderByDescending(McVersionClassifier.GetReleaseTime)
                    .ToList();

            PanMain.Children.Clear();

            var CardInfo = new MyCard { Title = Lang.Text("Download.Version.Latest.Title"), Margin = new Thickness(0d, 0d, 0d, 15d) };
            var TopestVersions = new List<JsonObject>();
            var Release = (JsonObject)Dict[McVersionCategory.Release][0].DeepClone();
            Release["lore"] = Lang.Text("Download.Version.Latest.Release", Lang.Date(McVersionClassifier.GetReleaseTime(Release), "g"));
            TopestVersions.Add(Release);
            if (McVersionClassifier.GetReleaseTime(Dict[McVersionCategory.Release][0]) < McVersionClassifier.GetReleaseTime(Dict[McVersionCategory.Snapshot][0]))
            {
                var Snapshot = (JsonObject)Dict[McVersionCategory.Snapshot][0].DeepClone();
                Snapshot["lore"] = Lang.Text("Download.Version.Latest.Development",
                                   Lang.Date(McVersionClassifier.GetReleaseTime(Snapshot), "g"));
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

            foreach (var Pair in Dict)
            {
                if (!Pair.Value.Any())
                    continue;

                var NewCard = new MyCard
                    { Title = McVersionClassifier.GetCategoryDisplayName(Pair.Key) + " (" + Pair.Value.Count + ")", Margin = new Thickness(0d, 0d, 0d, 15d) };
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