using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Download;

/// <summary>版本条目的动作模式。</summary>
internal enum DownloadVersionListMode
{
    Install,

    SaveOnly
}

internal static class DownloadVersionList
{
    private static readonly McVersionCategory[] CategoryOrder =
    [
        McVersionCategory.Release,
        McVersionCategory.Snapshot,
        McVersionCategory.BeforeRelease,
        McVersionCategory.AprilFools
    ];

    public static void Build(
        StackPanel panMain,
        JsonArray versions,
        Action<JsonObject> onSelect,
        DownloadVersionListMode mode = DownloadVersionListMode.Install,
        IDictionary<string, MyListItem>? itemMap = null)
    {
        var dictionary = CategoryOrder.ToDictionary(
            category => category,
            _ => new List<JsonObject>());
        foreach (var node in versions)
        {
            if (node is not JsonObject version)
                continue;
            var category = McVersionClassifier.ClassifyVersion(version);
            if (dictionary.TryGetValue(category, out var categoryList))
                categoryList.Add(version);
        }

        foreach (var category in CategoryOrder)
            dictionary[category] =
            [
                .. dictionary[category]
                    .OrderByDescending(McVersionClassifier.GetReleaseTime)
            ];

        var installed = GetInstalledVersionNames();
        panMain.Children.Clear();

        var latestVersions = GetLatestVersions(dictionary);
        if (latestVersions.Count > 0)
        {
            var latestCard = new MyCard
            {
                Margin = new Thickness(0d, 0d, 0d, 15d),
                Title = Lang.Text("Download.Version.Latest.Title")
            };
            var latestStack = new StackPanel
            {
                Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d)
            };
            foreach (var version in latestVersions)
                latestStack.Children.Add(CreateItem(version, onSelect, installed, mode, itemMap));
            latestCard.Children.Add(latestStack);
            panMain.Children.Add(latestCard);
        }

        foreach (var category in CategoryOrder)
        {
            var categoryVersions = dictionary[category];
            if (categoryVersions.Count == 0)
                continue;

            var card = new MyCard
            {
                Margin = new Thickness(0d, 0d, 0d, 15d),
                Title =
                    $"{Lang.Text(McVersionClassifier.GetCategoryKey(category))} ({categoryVersions.Count})"
            };
            var stack = new StackPanel
            {
                Margin = new Thickness(20d, MyCard.SwapedHeight, 18d, 0d),
                Tag = categoryVersions
            };
            card.Children.Add(stack);
            card.SwapControl = stack;
            card.InstallMethod = target =>
            {
                if (target.Tag is not List<JsonObject> list)
                    return;
                target.Children.Clear();
                foreach (var version in list)
                    target.Children.Add(CreateItem(version, onSelect, installed, mode, itemMap));
            };
            card.IsSwapped = true;
            panMain.Children.Add(card);
        }
    }

    public static HashSet<string> GetInstalledVersionNames()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var folder = GameFolders.mcFolderSelected;
            if (string.IsNullOrEmpty(folder))
                return result;
            var versionsFolder = Path.Combine(folder, "versions");
            if (!Directory.Exists(versionsFolder))
                return result;
            foreach (var instanceFolder in Directory.GetDirectories(versionsFolder))
            {
                var name = Path.GetFileName(instanceFolder);
                if (File.Exists(Path.Combine(instanceFolder, $"{name}.json")))
                    result.Add(name);
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "读取已安装版本列表失败");
        }

        return result;
    }

    private static List<JsonObject> GetLatestVersions(
        IReadOnlyDictionary<McVersionCategory, List<JsonObject>> dictionary)
    {
        var result = new List<JsonObject>();
        var latestRelease = dictionary[McVersionCategory.Release].FirstOrDefault();
        if (latestRelease is not null)
            result.Add(CloneWithLore(latestRelease, "Download.Version.Latest.Release"));

        var latestSnapshot = dictionary[McVersionCategory.Snapshot].FirstOrDefault();
        if (latestSnapshot is null)
            return result;

        if (latestRelease is null
            || McVersionClassifier.GetReleaseTime(latestRelease)
            < McVersionClassifier.GetReleaseTime(latestSnapshot))
            result.Add(CloneWithLore(latestSnapshot, "Download.Version.Latest.Development"));

        return result;
    }

    private static JsonObject CloneWithLore(JsonObject source, string loreKey)
    {
        var clone = (JsonObject)source.DeepClone();
        clone["lore"] = loreKey;
        clone["loreArgs"] = new JsonArray(
            McVersionClassifier.GetReleaseTime(clone).ToString("O"));
        clone["loreArgsType"] = "date";
        return clone;
    }

    private static MyListItem CreateItem(
        JsonObject version,
        Action<JsonObject> onSelect,
        IReadOnlySet<string> installed,
        DownloadVersionListMode mode,
        IDictionary<string, MyListItem>? itemMap)
    {
        var versionId = version["id"]?.ToString() ?? "";
        var formattedVersion = McFormatter.FormatVersion(versionId).Replace("_", " ");
        var releaseTime = McVersionClassifier.GetReleaseTime(version);

        var info = version["lore"] is { } loreNode
            ? FormatLore(version, loreNode.ToString(), formattedVersion, versionId)
            : $"{Lang.Date(releaseTime, "g")}{(formattedVersion != versionId ? $" | {versionId}" : "")}";

        var item = new MyListItem
        {
            Height = 42d,
            Title = formattedVersion,
            Info = info,
            tag = version,
            LogoScale = 0.9,
            Logo = GetLogo(version),
            Type = MyListItem.CheckType.Clickable
        };
        ApplyVersionTags(item, version, installed.Contains(versionId));

        var changelogButton = new MyIconButton
        {
            SvgIcon = "lucide/info",
            LogoScale = 1.05d,
            Theme = MyIconButton.Themes.Custom
        };
        ToolTip.SetTip(changelogButton, Lang.Text("Download.Version.Changelog"));
        changelogButton.Click += (_, _) => DownloadVersionActions.ShowChangelog(version);

        var serverButton = new MyIconButton
        {
            SvgIcon = "lucide/server",
            LogoScale = 1d,
            Theme = MyIconButton.Themes.Custom
        };
        ToolTip.SetTip(serverButton, Lang.Text("Download.Version.DownloadServer"));
        serverButton.Click +=
            (_, _) => DownloadVersionActions.SaveServer(version, formattedVersion);

        if (mode == DownloadVersionListMode.SaveOnly)
        {
            // 另存模式：点击整行另存，悬停按钮为服务端与更新日志。
            item.Buttons = [serverButton, changelogButton];
            item.Click += (_, _) =>
                DownloadVersionActions.SaveVersion(version, formattedVersion);
        }
        else
        {
            // 安装模式：点击整行进入安装配置，悬停按钮与 WPF 的 McDownloadMenu 一致。
            var saveButton = new MyIconButton
            {
                SvgIcon = "lucide/save",
                LogoScale = 1d,
                Theme = MyIconButton.Themes.Custom
            };
            ToolTip.SetTip(saveButton, Lang.Text("Download.Version.SaveAs"));
            saveButton.Click += (_, _) =>
                DownloadVersionActions.SaveVersion(version, formattedVersion);
            item.Buttons = [saveButton, changelogButton, serverButton];
            item.Click += (_, _) => onSelect(version);
        }

        itemMap?[versionId] = item;
        return item;
    }

    private static string FormatLore(
        JsonObject version,
        string loreKey,
        string formattedVersion,
        string versionId)
    {
        var rawArgs = (version["loreArgs"] as JsonArray)
            ?.Select(node => node?.ToString())
            .ToArray()
            ?? [];
        var isDateArg = (string?)version["loreArgsType"] == "date";
        var args = isDateArg
            && rawArgs.Length == 1
            && DateTime.TryParse(
                rawArgs[0],
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var loreDate)
                ? [Lang.Date(loreDate, "g")]
                : rawArgs;
        var lore = Lang.Text(loreKey, args);
        return formattedVersion != versionId ? $"{lore} | {versionId}" : lore;
    }

    public static void ApplyVersionTags(MyListItem item, JsonObject version, bool isInstalled)
    {
        var tags = new List<string>();
        if (isInstalled)
            tags.Add("Instance.Overall.Info.Installed");
        if ((version["url"]?.ToString() ?? "").Contains("unlisted-versions-of-minecraft"))
            tags.Add("Download.Source.Tag.Uvmc");
        item.Tags = tags.Count == 0 ? null : tags;
    }

    private static string GetLogo(JsonObject version)
    {
        var file = (version["type"]?.ToString() ?? "") switch
        {
            "release" => "Grass.png",
            "snapshot" or "pending" => "CommandBlock.png",
            "special" => "GoldBlock.png",
            _ => "CobbleStone.png"
        };
        return $"{LauncherPaths.ImageRoot}Blocks/{file}";
    }
}