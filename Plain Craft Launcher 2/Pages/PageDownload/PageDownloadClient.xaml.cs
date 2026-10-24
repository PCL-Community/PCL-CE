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
                McVersionCategory.AprilFools,
                McVersionCategory.Unknown
            };

            var dict = categoryOrder.ToDictionary(
                category => category,
                _ => new List<JsonObject>()
            );

            var versions = (JsonArray)ModDownload.dlClientListLoader.output.Value["versions"];
            foreach (JsonObject Version in versions)
            {
                var cat = McVersionClassifier.ClassifyVersion(Version);
                dict[cat].Add(Version);
            }

            foreach (var category in categoryOrder)
                dict[category] = dict[category]
                    .OrderByDescending(McVersionClassifier.ListedLine)
                    .ToList();

            PanMain.Children.Clear();

            var latestRelease = dict[McVersionCategory.Release].FirstOrDefault();
            var latestSnapshot = dict[McVersionCategory.Snapshot].FirstOrDefault();
            var topestVersions = new List<JsonObject>();
            if (latestRelease is not null)
            {
                var release = (JsonObject)latestRelease.DeepClone();
                release["lore"] = Lang.Text("Download.Version.Latest.Release", Lang.Date(McVersionClassifier.GetReleaseTime(release), "g"));
                topestVersions.Add(release);
            }

            if (latestSnapshot is not null &&
                (latestRelease is null || McVersionClassifier.ListedLine(latestSnapshot) > McVersionClassifier.ListedLine(latestRelease)))
            {
                var snapshot = (JsonObject)latestSnapshot.DeepClone();
                snapshot["lore"] = Lang.Text("Download.Version.Latest.Development",
                                   Lang.Date(McVersionClassifier.GetReleaseTime(snapshot), "g"));
                topestVersions.Add(snapshot);
            }

            void PutMethod(StackPanel stack)
            {
                foreach (var item in (IEnumerable)stack.Tag)
                    stack.Children.Add(ModDownloadLib.McDownloadListItem((JsonObject)item,
                        ModDownloadLib.McDownloadMenuSave, true));
            }

            if (topestVersions.Count > 0)
            {
                var cardInfo = new MyCard { Title = Lang.Text("Download.Version.Latest.Title"), Margin = new Thickness(0d, 0d, 0d, 15d) };
                var panInfo = new StackPanel
                {
                    Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d), VerticalAlignment = VerticalAlignment.Top,
                    RenderTransform = new TranslateTransform(0d, 0d), Tag = topestVersions
                };
                MyCard.StackInstall(ref panInfo, PutMethod);
                cardInfo.Children.Add(panInfo);
                PanMain.Children.Add(cardInfo);
            }

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
            ModBase.Log(
                ex,
                "可视化 MC 版本列表出错",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Error.OperationFailed"));
        }
    }
}