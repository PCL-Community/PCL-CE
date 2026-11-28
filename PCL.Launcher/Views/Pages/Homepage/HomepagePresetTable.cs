namespace PCL.Views.Pages.Homepage;

/// <summary>主页内容的来源类别。</summary>
public enum HomepageSourceKind
{
    Blank,

    Trivia,

    CustomAxaml,

    NewsFeed
}

/// <summary>一条主页预设：配置索引、显示名资源键与内容来源。</summary>
/// <param name="Index">与 <c>Preference.Homepage.SelectedPreset</c> 对应的索引。</param>
/// <param name="NameKey">显示名使用的本地化资源键。</param>
/// <param name="Kind">该预设的内容来源类别。</param>
/// <param name="Url">AXAML 获取地址；不需要联网的类型为 <c>null</c>。</param>
public sealed record HomepagePreset(
    int Index,
    string NameKey,
    HomepageSourceKind Kind,
    string? Url);

/// <summary>主页预设表：把配置中的预设索引解释为可展示的内容来源。</summary>
public static class HomepagePresetTable
{
    public const int TriviaIndex = 0;

    public const int MagazineIndex = 7;

    public const int NewsFeedIndex = 15;

    private static readonly HomepagePreset[] Presets =
    [
        new(
            TriviaIndex,
            "Setup.Ui.Homepage.Preset.Trivia",
            HomepageSourceKind.Trivia,
            null),
        new(
            MagazineIndex,
            "Setup.Ui.Homepage.Preset.Magazine",
            HomepageSourceKind.CustomAxaml,
            "https://github.com/Neclyon/Magazine-Homepage-PCL/raw/main/output/Custom.axaml"),
        new(
            NewsFeedIndex,
            "Setup.Ui.Homepage.Preset.McOfficialFeed",
            HomepageSourceKind.NewsFeed,
            null)
    ];

    public static HomepagePreset? Find(int index)
    {
        return Presets.FirstOrDefault(preset => preset.Index == index);
    }
}