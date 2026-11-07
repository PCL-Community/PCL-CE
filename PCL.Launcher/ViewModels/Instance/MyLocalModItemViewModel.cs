using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>
///     实例资源列表条目需要宿主页面提供的操作入口。<br />
///     把交互委托给页面，避免条目 ViewModel 反向依赖具体的页面类型。
/// </summary>
public interface ILocalModItemHost
{
    /// <summary>当前页面管理的资源类型。</summary>
    Comp.CompType CompType { get; }

    void OpenFileLocation(LocalComp.LocalCompFile entry);

    void ShowInfo(LocalComp.LocalCompFile entry);

    void Delete(LocalComp.LocalCompFile entry);

    void ToggleEnabled(LocalComp.LocalCompFile entry);

    void EnterFolder(string path);

    void Update(LocalComp.LocalCompFile entry);

    void ShowChangelog(LocalComp.LocalCompFile entry);
}

/// <summary>实例资源列表条目的视图模型。</summary>
public partial class MyLocalModItemViewModel : ViewModelBase
{
    private const string CustomInfoUnavailable = "Instance.Resource.Item.InfoUnavailable";

    private string? _displayVersion;

    [ObservableProperty] public partial string DescriptionText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EnabledButtonIcon { get; set; } = "lucide/circle-minus";

    [ObservableProperty] public partial string InfoToolTip { get; set; } = string.Empty;

    [ObservableProperty] public partial bool IsChecked { get; set; }

    [ObservableProperty] public partial bool IsEnabledButtonVisible { get; set; }

    [ObservableProperty] public partial bool IsFolder { get; set; }

    [ObservableProperty] public partial bool IsStateImageVisible { get; set; }

    [ObservableProperty] public partial bool IsSubTitleVisible { get; set; }

    [ObservableProperty] public partial bool IsTagsVisible { get; set; }

    [ObservableProperty] public partial bool IsTitleStrikethrough { get; set; }

    [ObservableProperty] public partial bool IsUpdateVisible { get; set; }

    [ObservableProperty] public partial string Logo { get; set; } = string.Empty;

    [ObservableProperty] public partial string? StateImage { get; set; }

    [ObservableProperty] public partial string SubTitleText { get; set; } = string.Empty;

    [ObservableProperty] public partial string TitleText { get; set; } = string.Empty;

    [ObservableProperty] public partial string UpdateToolTip { get; set; } = string.Empty;

    /// <summary>该条目对应的资源。</summary>
    public LocalComp.LocalCompFile? Entry { get; private set; }

    /// <summary>宿主页面；由页面创建时注入。</summary>
    public ILocalModItemHost? Host { get; private set; }

    /// <summary>本地化标签键；页面用这些键渲染标签以保证切换语言后自动刷新。</summary>
    public IReadOnlyList<string> TagKeys { get; private set; } = [];

    /// <summary>标签的当前显示文本。</summary>
    public IReadOnlyList<string> Tags { get; private set; } = [];

    /// <summary>“打开文件位置”按钮的提示文本。</summary>
    public string OpenFolderToolTip => Lang.Text("Instance.Saves.OpenFileLocation");

    /// <summary>“详情”按钮的提示文本。</summary>
    public string InfoButtonToolTip => Lang.Text("Instance.Saves.Detail");

    /// <summary>“删除”按钮的提示文本。</summary>
    public string DeleteButtonToolTip => Lang.Text("Common.Action.Delete");

    public void Initialize(LocalComp.LocalCompFile entry, ILocalModItemHost? host)
    {
        Entry = entry;
        Host = host;
        Refresh();
    }

    /// <summary>重新计算标题、描述、标签、图标等显示内容。</summary>
    [RelayCommand]
    public void Refresh()
    {
        if (Entry is null)
            return;

        try
        {
            RefreshCore(Entry);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, $"刷新资源列表项失败：{Entry.RawPath}");
        }
    }

    private void RefreshCore(LocalComp.LocalCompFile entry)
    {
        if (entry is { CanUpdate: true, UpdateFile: not null })
        {
            IsUpdateVisible = true;
            _ = GetUpdateCompareDescription();
            UpdateToolTip =
                $"{GetUpdateCompareDescription()}\r\n{Lang.Text("Instance.Resource.Item.UpdateToolTip")}";
        }
        else
        {
            IsUpdateVisible = false;
            _displayVersion = null;
            UpdateToolTip = string.Empty;
        }

        var displayVersion = _displayVersion ?? entry.Version;
        string descFileName;
        if (entry.IsFolder)
            descFileName = entry.Name;
        else
            descFileName = entry.State switch
            {
                LocalComp.LocalCompFile.LocalFileStatus.Fine => Path.GetFileNameWithoutExtension(
                    entry.path),
                LocalComp.LocalCompFile.LocalFileStatus.Disabled =>
                    Path.GetFileNameWithoutExtension(
                        entry
                            .path.Replace(".disabled", string.Empty)
                            .Replace(".old", string.Empty)),
                _ => LauncherCommon.GetFileNameFromPath(entry.path)
            };

        string title;
        var subTitle = string.Empty;
        string description;
        if (entry.IsFolder)
        {
            title = entry.Name;
            description = entry.Description ?? string.Empty;
        }
        else if (Config.Download.Comp.UiCompNameSolution == 1)
        {
            title = descFileName;
            if (entry.Comp is null)
            {
                description = entry.Name;
            }
            else
            {
                var titles = entry.Comp.GetControlTitle(false);
                description = titles.Key + titles.Value;
            }

            description = description.Replace("  |  ", " / ");
            if (!string.IsNullOrEmpty(displayVersion))
                description += $" ({displayVersion})";
        }
        else
        {
            if (entry.Comp is null)
            {
                title = entry.Name;
                subTitle = string.IsNullOrEmpty(displayVersion)
                    ? string.Empty
                    : $"  |  {displayVersion}";
            }
            else
            {
                var titles = entry.Comp.GetControlTitle(false);
                title = titles.Key;
                subTitle = titles.Value
                    + (string.IsNullOrEmpty(displayVersion)
                        ? string.Empty
                        : $"  |  {displayVersion}");
            }

            description = descFileName;
        }

        if (entry.Comp is not null)
            description +=
                $": {entry.Comp.Description.Replace("\r", string.Empty).Replace("\n", string.Empty)}";
        else if (!string.IsNullOrEmpty(entry.Description))
            description +=
                $": {entry.Description.Replace("\r", string.Empty).Replace("\n", string.Empty)}";
        else if (!entry.IsFileAvailable)
            description += $": {Lang.Text(CustomInfoUnavailable)}";

        TitleText = entry.State == LocalComp.LocalCompFile.LocalFileStatus.Unavailable
            ? title + Lang.Text("Instance.Resource.Item.ErrorSuffix")
            : title;
        IsTitleStrikethrough = entry.State is LocalComp.LocalCompFile.LocalFileStatus.Disabled
            or LocalComp.LocalCompFile.LocalFileStatus.Unavailable;
        SubTitleText = subTitle;
        IsSubTitleVisible = !string.IsNullOrEmpty(subTitle);
        DescriptionText = description;

        IsFolder = entry.IsFolder;
        Logo = entry.GetLogo();
        StateImage = entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine
            ? null
            : $"{LauncherPaths.ImageRoot}Icons/{entry.State}.png";
        IsStateImageVisible = entry.State != LocalComp.LocalCompFile.LocalFileStatus.Fine;

        IsEnabledButtonVisible = Host is not null
            && Host.CompType == Comp.CompType.Mod
            && !entry.IsFolder
            && entry.State != LocalComp.LocalCompFile.LocalFileStatus.Unavailable;
        EnabledButtonIcon = entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine
            ? "lucide/circle-minus"
            : "lucide/circle-check";

        SetTagsFromEntry(entry);
        InfoToolTip = BuildInfoToolTip(entry);
    }

    private static string BuildInfoToolTip(LocalComp.LocalCompFile entry)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(entry.Comp?.Description))
            lines.Add(entry.Comp.Description);
        if (!string.IsNullOrWhiteSpace(entry.Authors))
            lines.Add(Lang.Text("Instance.Resource.Item.Info.Author", entry.Authors));
        if (entry.CompFile is { } compFile && compFile.ReleaseDate != default)
            lines.Add(
                $"{Lang.Text("Minecraft.Update.UpdateTime")}{Lang.Date(compFile.ReleaseDate, "g")}");
        if (!string.IsNullOrWhiteSpace(entry.Description))
            lines.Add(entry.Description);
        var dependencies = entry
            .Dependencies
            .Where(pair => !DependencyIds.IsPlatform(pair.Key))
            .ToList();
        if (dependencies.Count > 0)
        {
            lines.Add(Lang.Text("Instance.Resource.Item.Info.Dependency"));
            lines.AddRange(
                dependencies.Select(dependency => $" - {(dependency.Value is null
                    ? dependency.Key
                    : Lang.Text(
                        "Instance.Resource.Item.Info.DependencyVersion",
                        dependency.Key,
                        dependency.Value))}"));
        }

        return lines.Count == 0 ? string.Empty : string.Join("\r\n", lines);
    }

    private string BuildUpdateToolTip()
    {
        return Entry is { CanUpdate: true, UpdateFile: not null }
            ? $"{GetUpdateCompareDescription()}\r\n{Lang.Text("Instance.Resource.Item.UpdateToolTip")}"
            : string.Empty;
    }

    /// <summary>构造更新对比说明；同时记录解析出的当前版本号。</summary>
    public string GetUpdateCompareDescription()
    {
        _displayVersion = null;
        if (Entry?.UpdateFile is null)
            return string.Empty;

        var currentName =
            Path.GetFileNameWithoutExtension(Entry.CompFile?.FileName ?? Entry.FileName);
        var newestName = Path.GetFileNameWithoutExtension(Entry.UpdateFile.FileName);
        var currentSegments = currentName.Split('-').ToList();
        var newestSegments = newestName.Split('-').ToList();
        var shortened = false;
        foreach (var segment in currentSegments.ToList().Where(newestSegments.Contains))
        {
            currentSegments.Remove(segment);
            newestSegments.Remove(segment);
            shortened = true;
        }

        if (shortened && currentSegments.Count != 0 && newestSegments.Count != 0)
        {
            currentName = string.Join("-", currentSegments);
            newestName = string.Join("-", newestSegments);
            _displayVersion = currentName;
        }

        return Lang.Text(
            "Instance.Resource.Item.UpdateCompare",
            currentName,
            Lang.TimeSpan((Entry.CompFile?.ReleaseDate ?? DateTime.Now) - DateTime.Now),
            newestName,
            Lang.TimeSpan(Entry.UpdateFile.ReleaseDate - DateTime.Now));
    }

    private void SetTagsFromEntry(LocalComp.LocalCompFile entry)
    {
        if (entry.IsFolder)
            TagKeys = ["Instance.Resource.Item.FolderTag"];
        else
            TagKeys =
            [
                .. (entry.Comp?.Tags ?? [])
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct()
                .Where(tag => !string.IsNullOrEmpty(Lang.Text(tag)))
            ];

        Tags = [.. TagKeys.Select(Lang.Text)];
        IsTagsVisible = Tags.Count > 0;
    }

    [RelayCommand]
    private void OpenFileLocation()
    {
        if (Entry is not null)
            Host?.OpenFileLocation(Entry);
    }

    [RelayCommand]
    private void ShowInfo()
    {
        if (Entry is not null)
            Host?.ShowInfo(Entry);
    }

    [RelayCommand]
    private void Delete()
    {
        if (Entry is not null)
            Host?.Delete(Entry);
    }

    [RelayCommand]
    private void ToggleEnabled()
    {
        if (Entry is not null)
            Host?.ToggleEnabled(Entry);
    }

    [RelayCommand]
    private void EnterFolder()
    {
        if (Entry is { IsFolder: true })
            Host?.EnterFolder(Entry.ActualPath);
    }

    [RelayCommand]
    private void ToggleChecked()
    {
        IsChecked = !IsChecked;
    }

    [RelayCommand]
    private void Update()
    {
        if (Entry is not null)
            Host?.Update(Entry);
    }

    [RelayCommand]
    private void ShowUpdateLog()
    {
        if (Entry is not null)
            Host?.ShowChangelog(Entry);
    }

    /// <summary>在页面没有提供宿主操作时直接打开更新日志。</summary>
    public void OpenUpdateLog()
    {
        if (Entry is null)
            return;
        try
        {
            if (Entry.Comp is not null)
            {
                var isNumeric = long.TryParse(Entry.Comp.Id, out _);
                var url = isNumeric
                    ? Entry.ChangelogUrls.FirstOrDefault(item =>
                        item.Contains("curseforge.com", StringComparison.OrdinalIgnoreCase))
                    : Entry.ChangelogUrls.FirstOrDefault(item =>
                        item.Contains("modrinth.com", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(url))
                {
                    Basics.OpenPath(url);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "打开更新日志失败", LauncherLogLevel.Developer);
        }

        HintService.Show(Hints.Info(Lang.Text("Instance.Resource.Item.OpenChangelogFailed")));
    }
}