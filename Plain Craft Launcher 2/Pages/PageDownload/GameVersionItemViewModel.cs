using System.Globalization;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.Utils;

namespace PCL;

/// <summary>
///     版本列表中的一个版本，对应界面上由 DataTemplate 生成的一个 <see cref="MyListItem" />。<br />
///     映射逻辑集中在这里，并由 <see cref="DownloadUi.McDownloadListItem" />（其它页面仍在使用的控件工厂）复用，
///     避免数据模板与控件工厂各写一份格式化代码。
/// </summary>
public sealed class GameVersionItemViewModel
{
    private GameVersionItemViewModel()
    {
    }

    /// <summary>行标题，即格式化后的版本号。</summary>
    public required string Title { get; init; }

    /// <summary>行图标路径。</summary>
    public required string Logo { get; init; }

    /// <summary>行副标题；随语言切换自动刷新，在 XAML 中绑定 <c>Info.Value</c>。</summary>
    public required LocalizedText Info { get; init; }

    /// <summary>来源标签（如 UVMC），无标签时为 <see langword="null" />。</summary>
    public string? Tags { get; init; }

    /// <summary>原始版本数据，用于点击后的下载菜单。</summary>
    public required JsonObject Entry { get; init; }

    /// <summary>按原始版本数据构建行视图模型。</summary>
    public static GameVersionItemViewModel FromEntry(JsonObject entry)
    {
        var logo = (entry["type"]?.ToString() ?? "") switch
        {
            "release" => $"{Common.PathImage}Blocks/Grass.png",
            "snapshot" or "pending" => $"{Common.PathImage}Blocks/CommandBlock.png",
            "special" => $"{Common.PathImage}Blocks/GoldBlock.png",
            _ => $"{Common.PathImage}Blocks/CobbleStone.png"
        };

        var versionId = entry["id"]?.ToString() ?? "";
        var formattedVersion = McFormatter.FormatVersion(versionId).Replace("_", " ");

        LocalizedText info;
        if (entry["lore"] is null)
        {
            var releaseTime = entry["releaseTime"].ToObject<DateTime>();
            info = LocalizedText.Create(() => formattedVersion != versionId
                ? $"{Lang.Date(releaseTime, "g")} | {versionId}"
                : Lang.Date(releaseTime, "g"));
        }
        else
        {
            var loreKey = entry["lore"].ToString();
            var loreRawArgs = (entry["loreArgs"] as JsonArray)
                ?.Select(object? (node) => node?.ToString())
                .ToArray() ?? [];
            var isDateArg = (string)entry["loreArgsType"] == "date";
            info = LocalizedText.Create(() =>
            {
                var loreArgs = isDateArg &&
                               loreRawArgs.Length == 1 &&
                               DateTime.TryParse(
                                   loreRawArgs[0]?.ToString(),
                                   CultureInfo.InvariantCulture,
                                   DateTimeStyles.RoundtripKind, out var loreDate)
                    ? [Lang.Date(loreDate, "g")]
                    : loreRawArgs;
                return formattedVersion != versionId
                    ? $"{Lang.Text(loreKey, loreArgs)} | {versionId}"
                    : Lang.Text(loreKey, loreArgs);
            });
        }

        return new GameVersionItemViewModel
        {
            Title = formattedVersion,
            Logo = logo,
            Info = info,
            Tags = (entry["url"]?.ToString() ?? "").Contains("unlisted-versions-of-minecraft")
                ? "Download.Source.Tag.Uvmc"
                : null,
            Entry = entry
        };
    }
}
