namespace PCL.Views.Pages.Homepage;

/// <summary>主页内容的来源类别。</summary>
public enum HomepageSourceKind
{
    Blank,

    Trivia,

    Removed,

    CustomXaml,

    NewsFeed
}

/// <summary>一条主页预设：配置索引、显示名资源键与内容来源。</summary>
/// <param name="Index">与 <c>Preference.Homepage.SelectedPreset</c> 对应的索引，顺序与设置页下拉选项一致。</param>
/// <param name="NameKey">显示名使用的本地化资源键。</param>
/// <param name="Kind">该预设的内容来源类别。</param>
/// <param name="Url">联网获取地址；不需要联网的类型为 <c>null</c>。</param>
public sealed record HomepagePreset(
    int Index,
    string NameKey,
    HomepageSourceKind Kind,
    string? Url);

/// <summary>
///     主页预设表：把配置中的预设索引解释为可展示的内容来源。
///     索引顺序与设置页“主页预设”下拉的选项顺序一致，包含被隐藏的“回声洞”占位项。
/// </summary>
public static class HomepagePresetTable
{
    public const int TriviaIndex = 0;

    public const int RemovedIndex = 1;

    public const int NewsFeedIndex = 15;

    private static readonly HomepagePreset[] Presets =
    [
        new(0, "Setup.Ui.Homepage.Preset.Trivia", HomepageSourceKind.Trivia, null),
        new(1, "Setup.Ui.Homepage.Preset.EchoCave", HomepageSourceKind.Removed, null),
        new(
            2,
            "Setup.Ui.Homepage.Preset.McNews",
            HomepageSourceKind.CustomXaml,
            "https://news.bugjump.net"),
        new(
            3,
            "Setup.Ui.Homepage.Preset.DailyModpack",
            HomepageSourceKind.CustomXaml,
            "https://pclsub.sodamc.com/"),
        new(
            4,
            "Setup.Ui.Homepage.Preset.McSkin",
            HomepageSourceKind.CustomXaml,
            "https://forgepixel.com/pcl_sub_file"),
        new(
            5,
            "Setup.Ui.Homepage.Preset.OpenBmclapi",
            HomepageSourceKind.CustomXaml,
            "https://pcl-bmcl.milu.ink/"),
        new(
            6,
            "Setup.Ui.Homepage.Preset.PclManual",
            HomepageSourceKind.CustomXaml,
            "https://raw.gitcode.com/WForst-Breeze/WhatsNewPCL/raw/main/Custom.xaml"),
        new(
            7,
            "Setup.Ui.Homepage.Preset.Magazine",
            HomepageSourceKind.CustomXaml,
            "https://pclhomeplazaoss.lingyunawa.top:26994/d/Homepages/Ext1nguisher/Custom.xaml"),
        new(
            8,
            "Setup.Ui.Homepage.Preset.GitHub",
            HomepageSourceKind.CustomXaml,
            "https://ddf.pcl-community.org/Custom.xaml"),
        new(
            9,
            "Setup.Ui.Homepage.Preset.McUpdateSummary",
            HomepageSourceKind.CustomXaml,
            "https://raw.gitcode.com/ENC_Euphony/PCL-AI-Summary-HomePage/raw/master/Custom.xaml"),
        new(
            10,
            "Setup.Ui.Homepage.Preset.NewsToday",
            HomepageSourceKind.CustomXaml,
            "https://pcl.wyc-w.top/index.xaml"),
        new(
            11,
            "Setup.Ui.Homepage.Preset.MinecraftKnowledge",
            HomepageSourceKind.CustomXaml,
            "https://www.xxag.top/mkss"),
        new(
            12,
            "Setup.Ui.Homepage.Preset.ModpackRecommendation",
            HomepageSourceKind.CustomXaml,
            "https://qawsedrftgyhujiko.fun/pcl2/Custom.xaml"),
        new(
            13,
            "Setup.Ui.Homepage.Preset.Bangumi",
            HomepageSourceKind.CustomXaml,
            "https://bangumi.p.kaphia.qzz.io"),
        new(
            14,
            "Setup.Ui.Homepage.Preset.PclAnnouncement",
            HomepageSourceKind.CustomXaml,
            "https://s3.pysio.online/pcl2-ce/apiv2/pages/announce.xaml"),
        new(15, "Setup.Ui.Homepage.Preset.McOfficialFeed", HomepageSourceKind.NewsFeed, null)
    ];

    public static IReadOnlyList<HomepagePreset> All => Presets;

    public static HomepagePreset? Find(int index)
    {
        return index >= 0 && index < Presets.Length ? Presets[index] : null;
    }
}