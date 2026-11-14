using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualBasic;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.ViewModel;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace PCL;

/// <summary>
///     实例资源列表条目需要宿主页面提供的操作入口。<br />
///     把交互委托给页面，避免条目 ViewModel 反向依赖具体的页面类型。
/// </summary>
public interface ILocalModItemHost
{
    /// <summary>当前页面管理的资源类型。</summary>
    Comp.CompType CompType { get; }

    /// <summary>在资源管理器中打开该资源所在的位置。</summary>
    void OpenFileLocation(LocalComp.LocalCompFile entry);

    /// <summary>显示该资源的详细信息。</summary>
    void ShowInfo(LocalComp.LocalCompFile entry);

    /// <summary>删除该资源。</summary>
    void Delete(LocalComp.LocalCompFile entry);

    /// <summary>切换该资源的启用状态。</summary>
    void ToggleEnabled(LocalComp.LocalCompFile entry);

    /// <summary>进入该文件夹项。</summary>
    void EnterFolder(string path);

    /// <summary>页面共享的滑动选中状态。</summary>
    MyLocalCompItem.SwipeSelect SwipeState { get; }

    /// <summary>开始滑动选中。</summary>
    void SwipeStart(MyLocalModItemViewModel item);

    /// <summary>滑动选中经过某一项；<paramref name="isPressed" /> 为 false 表示滑动已结束。</summary>
    void SwipeMove(MyLocalModItemViewModel item, bool isPressed);
}

/// <summary>实例资源列表条目的视图模型。</summary>
public partial class MyLocalModItemViewModel : ViewModelBase
{
    private static readonly SolidColorBrush DarkTagForeground = CreateFrozenBrush(Color.FromArgb(88, 255, 255, 255));
    private static readonly SolidColorBrush LightTagForeground = CreateFrozenBrush(Color.FromArgb(88, 136, 136, 136));

    /// <summary>更新按钮提示中由 <see cref="GetUpdateCompareDescription" /> 解析出的版本号。</summary>
    private string? _displayVersion;

    public MyLocalModItemViewModel()
    {
        UpdateToolTip = LocalizedText.Create(BuildUpdateToolTip);
        EnabledButtonToolTip = LocalizedText.Create(() => Entry is { State: LocalComp.LocalCompFile.LocalFileStatus.Fine }
            ? Lang.Text("Instance.Resource.Disable")
            : Lang.Text("Instance.Resource.Enable"));
    }

    /// <summary>该条目对应的资源。</summary>
    public LocalComp.LocalCompFile? Entry { get; private set; }

    /// <summary>宿主页面；由页面创建时注入。</summary>
    public ILocalModItemHost? Host { get; private set; }

    [ObservableProperty] private bool _isChecked;

    [ObservableProperty] private string _titleText = "";

    [ObservableProperty] private string _subTitleText = "";

    [ObservableProperty] private Visibility _subTitleVisibility = Visibility.Collapsed;

    [ObservableProperty] private string _descriptionText = "";

    [ObservableProperty] private TextDecorationCollection? _titleDecorations;

    [ObservableProperty] private Visibility _updateVisibility = Visibility.Collapsed;

    [ObservableProperty] private GridLength _columnTitleWidth = GridLength.Auto;

    [ObservableProperty] private GridLength _columnSubtitleWidth = GridLength.Auto;

    [ObservableProperty] private GridLength _columnExtendWidth = new(1d, GridUnitType.Star);

    [ObservableProperty] private Visibility _tagsVisibility = Visibility.Collapsed;

    [ObservableProperty] private string? _logo;

    [ObservableProperty] private string? _stateImage;

    [ObservableProperty] private Visibility _stateImageVisibility = Visibility.Collapsed;

    [ObservableProperty] private bool _showEnabledButton;

    [ObservableProperty] private string _enabledButtonIcon = "lucide/circle-minus";

    [ObservableProperty] private int _visibleButtonCount = 3;

    [ObservableProperty] private Brush _tagForeground = Brushes.Gray;

    /// <summary>标签文本；用 <see cref="LocalizedText" /> 承载，语言切换时自动刷新。</summary>
    public ObservableCollection<LocalizedText> Tags { get; } = new();

    /// <summary>更新按钮的提示文本。</summary>
    public LocalizedText UpdateToolTip { get; }

    /// <summary>启用/禁用按钮的提示文本。</summary>
    public LocalizedText EnabledButtonToolTip { get; }

    /// <summary>“打开文件位置”按钮的提示文本。</summary>
    public LocalizedText OpenFolderToolTip { get; } =
        LocalizedText.Create(() => Lang.Text("Instance.Saves.OpenFileLocation"));

    /// <summary>“详情”按钮的提示文本。</summary>
    public LocalizedText InfoToolTip { get; } = LocalizedText.Create(() => Lang.Text("Instance.Saves.Detail"));

    /// <summary>“删除”按钮的提示文本。</summary>
    public LocalizedText DeleteToolTip { get; } = LocalizedText.Create(() => Lang.Text("Common.Action.Delete"));

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <summary>
    ///     绑定资源并初始化显示内容。<br />
    ///     同一个条目只应绑定一次，之后复用该实例。
    /// </summary>
    public void Initialize(LocalComp.LocalCompFile entry, ILocalModItemHost? host)
    {
        Entry = entry;
        Host = host;
        ShowEnabledButton = host is not null && host.CompType == Comp.CompType.Mod &&
                            entry.State != LocalComp.LocalCompFile.LocalFileStatus.Unavailable;
        VisibleButtonCount = ShowEnabledButton ? 4 : 3;
        EnabledButtonIcon = entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine
            ? "lucide/circle-minus"
            : "lucide/circle-check";
        Refresh();
    }

    /// <summary>重新计算标题、描述、标签、图标等显示内容。</summary>
    [RelayCommand]
    public void Refresh()
    {
        if (Entry is null)
            return;
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        dispatcher?.BeginInvoke(new Func<Task>(RefreshAsync));
    }

    private async Task RefreshAsync()
    {
        var entry = Entry;
        if (entry is null)
            return;
        try
        {
            // 更新
            if (entry.CanUpdate)
            {
                UpdateVisibility = Visibility.Visible;
                _ = GetUpdateCompareDescription();
            }
            else
            {
                UpdateVisibility = Visibility.Collapsed;
                _displayVersion = null;
            }

            UpdateToolTip.SetFactory(BuildUpdateToolTip);

            // 标题与描述
            string descFileName;
            if (entry.IsFolder)
                // 文件夹项的特殊处理
                descFileName = entry.Name;
            else
                switch (entry.State)
                {
                    case LocalComp.LocalCompFile.LocalFileStatus.Fine:
                        {
                            descFileName = Path.GetFileNameWithoutExtension(entry.path);
                            break;
                        }
                    case LocalComp.LocalCompFile.LocalFileStatus.Disabled:
                        {
                            descFileName =
                                Path.GetFileNameWithoutExtension(entry.path.Replace(".disabled", "")
                                    .Replace(".old", "")); // McMod.McModState.Unavailable
                            break;
                        }

                    default:
                        {
                            descFileName = Common.GetFileNameFromPath(entry.path);
                            break;
                        }
                }

            string title;
            string subTitle = "";
            string newDescription;
            var displayVersion = _displayVersion ?? entry.Version;
            var compTemp = entry.Comp;
            if (entry.IsFolder)
            {
                // 文件夹项的特殊显示
                title = entry.Name;
                newDescription = entry.Description ?? "";
            }
            else if (Config.Download.Comp.UiCompNameSolution == 1)
            {
                // 标题显示文件名，详情显示译名
                title = descFileName;
                if (entry.Comp is null)
                {
                    newDescription = entry.Name;
                }
                else
                {
                    var titles = await Task.Run(() => compTemp!.GetControlTitle(false)).ConfigureAwait(true);
                    newDescription = titles.Key + titles.Value;
                }

                newDescription = newDescription.Replace("  |  ", " / ");
                if (displayVersion is not null)
                    newDescription += $" ({displayVersion})";
            }
            else
            {
                // 标题显示译名，详情显示文件名
                if (entry.Comp is null)
                {
                    title = entry.Name;
                    subTitle = displayVersion is null ? "" : "  |  " + displayVersion;
                }
                else
                {
                    var titles = await Task.Run(() => compTemp!.GetControlTitle(false)).ConfigureAwait(true);
                    title = titles.Key;
                    subTitle = titles.Value + (displayVersion is null ? "" : "  |  " + displayVersion);
                }

                // 描述
                newDescription = descFileName;
            }

            if (entry.Comp is not null)
                newDescription += ": " + entry.Comp.Description.Replace("\r", "").Replace("\n", "");
            else if (entry.Description is not null)
                newDescription += ": " + entry.Description.Replace("\r", "").Replace("\n", "");
            else if (!entry.IsFileAvailable)
                newDescription += ": " + Lang.Text("Instance.Resource.Item.InfoUnavailable");

            // 标题：不可用时追加错误后缀并加删除线
            TitleText = entry.State == LocalComp.LocalCompFile.LocalFileStatus.Unavailable
                ? title + Lang.Text("Instance.Resource.Item.ErrorSuffix")
                : title;
            TitleDecorations = entry.State is LocalComp.LocalCompFile.LocalFileStatus.Disabled
                or LocalComp.LocalCompFile.LocalFileStatus.Unavailable
                ? TextDecorations.Strikethrough
                : null;
            SubTitleText = subTitle;
            // TEMP-DIAG2
            SubTitleVisibility = string.IsNullOrEmpty(subTitle) ? Visibility.Collapsed : Visibility.Visible;
            DescriptionText = newDescription;

            // 主 Logo 与右下角状态图标
            Logo = entry.GetLogo();
            StateImage = entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine
                ? null
                : Common.PathImage + $"Icons/{entry.State}.png";
            StateImageVisibility = entry.State == LocalComp.LocalCompFile.LocalFileStatus.Fine
                ? Visibility.Collapsed
                : Visibility.Visible;

            // 标签
            SetTagsFromEntry();
        }
        catch (Exception ex)
        {
            Common.Log(ex, $"刷新资源列表项失败：{entry.RawPath}");
        }
    }

    private string BuildUpdateToolTip()
    {
        if (Entry is not { CanUpdate: true })
            return string.Empty;
        return
            $"{GetUpdateCompareDescription()}\r\n{Lang.Text("Instance.Resource.Item.UpdateToolTip")}";
    }

    /// <summary>构造更新对比说明；同时记录解析出的当前版本号。</summary>
    public string GetUpdateCompareDescription()
    {
        _displayVersion = null;
        if (Entry is null)
            return "";
        var currentName = Entry.CompFile.FileName.Replace(".jar", "");
        var newestName = Entry.UpdateFile.FileName.Replace(".jar", "");
        // 简化名称对比
        var currentSegs = currentName.Split('-').ToList();
        var newestSegs = newestName.Split('-').ToList();
        var shortened = false;
        foreach (var seg in currentSegs.ToList())
        {
            if (!newestSegs.Contains(seg))
                continue;
            currentSegs.Remove(seg);
            newestSegs.Remove(seg);
            shortened = true;
        }

        if (shortened && currentSegs.Any() && newestSegs.Any())
        {
            currentName = currentSegs.Join("-");
            newestName = newestSegs.Join("-");
            _displayVersion = currentName;
        }

        return
            Lang.Text("Instance.Resource.Item.UpdateCompare", currentName,
                Lang.TimeSpan(Entry.CompFile.ReleaseDate - DateTime.Now), newestName,
                Lang.TimeSpan(Entry.UpdateFile.ReleaseDate - DateTime.Now));
    }

    /// <summary>打开更新日志。</summary>
    public void ShowUpdateLog()
    {
        if (Entry is null)
            return;
        if (Entry.Comp is not null)
        {
            if (!Information.IsNumeric(Entry.Comp.Id))
            {
                var modrinthUrl = Entry.ChangelogUrls.FirstOrDefault(x => x.Contains("modrinth.com"));
                if (modrinthUrl is not null)
                {
                    Common.OpenWebsite(modrinthUrl);
                    return;
                }
            }
            else
            {
                var curseForgeUrl = Entry.ChangelogUrls.FirstOrDefault(x => x.Contains("curseforge.com"));
                if (curseForgeUrl is not null)
                {
                    Common.OpenWebsite(curseForgeUrl);
                    return;
                }
            }
        }

        Common.Log(
            Lang.Text("Instance.Resource.Item.OpenChangelogFailed"),
            Common.LogLevel.Hint,
            userSummary: Lang.Text("Instance.Resource.Item.OpenChangelogFailed"));
    }

    /// <summary>替换标签文本。标签可能是本地化资源键，需要先翻译再展示。</summary>
    public void SetTags(IEnumerable<string>? tags)
    {
        Tags.Clear();
        if (tags is not null)
            foreach (var tag in tags.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct())
            {
                var key = tag;
                if (string.IsNullOrEmpty(Lang.Text(key)))
                    continue;
                Tags.Add(LocalizedText.Create(() => Lang.Text(key)));
            }

        TagsVisibility = Tags.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        TagForeground = ThemeManager.IsDarkMode ? DarkTagForeground : LightTagForeground;
    }

    private void SetTagsFromEntry()
    {
        if (Entry is null)
        {
            SetTags(null);
            return;
        }

        if (Entry.IsFolder)
        {
            Tags.Clear();
            Tags.Add(LocalizedText.Create(() => Lang.Text("Instance.Resource.Item.FolderTag")));
            TagsVisibility = Visibility.Visible;
            TagForeground = ThemeManager.IsDarkMode ? DarkTagForeground : LightTagForeground;
            return;
        }

        SetTags(Entry.Comp?.Tags);
    }

    #region 命令

    /// <summary>在资源管理器中打开该资源所在的位置。</summary>
    [RelayCommand]
    private void OpenFileLocation()
    {
        if (Entry is not null)
            Host?.OpenFileLocation(Entry);
    }

    /// <summary>显示资源详情。</summary>
    [RelayCommand]
    private void ShowInfo()
    {
        if (Entry is not null)
            Host?.ShowInfo(Entry);
    }

    /// <summary>删除资源。</summary>
    [RelayCommand]
    private void Delete()
    {
        if (Entry is not null)
            Host?.Delete(Entry);
    }

    /// <summary>切换启用状态。</summary>
    [RelayCommand]
    private void ToggleEnabled()
    {
        if (Entry is not null)
            Host?.ToggleEnabled(Entry);
    }

    /// <summary>进入文件夹项。</summary>
    [RelayCommand]
    private void EnterFolder()
    {
        if (Entry is { IsFolder: true })
            Host?.EnterFolder(Entry.ActualPath);
    }

    /// <summary>切换选中状态。</summary>
    [RelayCommand]
    private void ToggleChecked() => IsChecked = !IsChecked;

    /// <summary>触发资源更新（沿用原有的交互流程）。</summary>
    [RelayCommand]
    private void Update()
    {
        if (Entry is null)
            return;
        switch (DialogService.Instance.Show(new TextDialogRequest
        {
            Title = Lang.Text("Instance.Resource.Item.UpdateConfirm.Title"),
            Content =
                $"{Lang.Text("Instance.Resource.Item.UpdateConfirm.Message", Entry.Name)}\r\n\r\n{GetUpdateCompareDescription()}",
            Buttons = DialogButtons.Create("Instance.Resource.Item.Update",
                "Instance.Resource.Item.ViewChangelog", "Common.Action.Cancel"),
            ForceWait = true
        }).Result?.Button ?? 0)
        {
            case 1: // 更新
                {
                    switch (Entry.Comp!.Type)
                    {
                        case Comp.CompType.Mod:
                            {
                                Launcher.frmInstanceMod ??= new PageInstanceCompResource(Comp.CompType.Mod);
                                Launcher.frmInstanceMod.UpdateResource([Entry]);
                                break;
                            }
                        case Comp.CompType.ResourcePack:
                            {
                                Launcher.frmInstanceResourcePack ??=
                                    new PageInstanceCompResource(Comp.CompType.ResourcePack);
                                Launcher.frmInstanceResourcePack.UpdateResource([Entry]);
                                break;
                            }
                        case Comp.CompType.Shader:
                            {
                                Launcher.frmInstanceShader ??= new PageInstanceCompResource(Comp.CompType.Shader);
                                Launcher.frmInstanceShader.UpdateResource([Entry]);
                                break;
                            }
                        case Comp.CompType.DataPack:
                            {
                                Launcher.frmInstanceSavesDatapack ??= new PageInstanceSavesDatapack();
                                Launcher.frmInstanceSavesDatapack.UpdateResource([Entry]);
                                break;
                            }
                    }

                    break;
                }
            case 2: // 查看更新日志
                {
                    ShowUpdateLog();
                    break;
                }
        }
    }

    #endregion
}
