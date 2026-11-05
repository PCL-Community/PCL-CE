using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Platform;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.Validate;
using PCL.Network.Loaders;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Download;

public partial class PageDownloadCompDetail : MyPageRight
{
    private MyCompItem? _compItem;

    public PageDownloadCompDetail()
    {
        InitializeComponent();
        PanScroll = PanBack;
        Load.State = ViewModel.EnsureLoader();
        ViewModel.StateChanged += ApplyLoadState;
        ViewModel.FiltersChanged += BuildFilters;
        ViewModel.FileGroupsChanged += BuildFileList;
        ViewModel.IntroChanged += ApplyIntroState;

        BtnOpenWebsite.Click += (_, _) =>
        {
            if (ViewModel.Project is { Website.Length: > 0 } project)
                Basics.OpenPath(project.Website);
        };
        BtnIntroWiki.Click += (_, _) =>
        {
            if (ViewModel.Project is { WikiId: > 0 } project)
                Basics.OpenPath($"https://www.mcmod.cn/class/{project.WikiId}.html");
        };
        BtnCopyName.Click += (_, _) =>
        {
            if (ViewModel.Project is not { } project) return;
            var title = project.GetControlTitle(true);
            PlatformServices.Clipboard?.SetText(title.Key + title.Value);
            HintService.Show(Hints.Success(Lang.Text("Common.Status.Success"), false));
        };
        BtnCopyLink.Click += (_, _) =>
        {
            if (ViewModel.Project is not { } project) return;
            var link = !string.IsNullOrEmpty(project.Website)
                ? project.Website
                : $"https://modrinth.com/project/{project.Slug}";
            PlatformServices.Clipboard?.SetText(link);
            HintService.Show(Hints.Success(Lang.Text("Common.Status.Success"), false));
        };
        BtnTranslate.Click += async (_, _) => await TranslateDescriptionAsync();
        BtnFavorites.Click += (_, _) =>
        {
            if (ViewModel.Project is { } project)
                CompFavoritesMenu.Show(project, BtnFavorites, RefreshFavoriteButton);
        };
        Load.Click += (_, _) => ViewModel.RetryCommand.Execute(null);
        Grid.SetIsSharedSizeScope(PanResults, true);

        LocalizationService.LanguageChanged += OnLanguageChanged;
    }

    public PageDownloadCompDetailViewModel ViewModel { get; } = new();

    private void OnLanguageChanged()
    {
        if (ViewModel.Project is null) return;
        ApplyButtonLocalization();
        ViewModel.RefreshLocalization();
        RefreshIntro();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        PanBack.ScrollToHome();
        var applied = parameter switch
        {
            Comp.CompProject project => ViewModel.ApplyContext(
                project,
                project.Type,
                null,
                Comp.CompLoaderType.Any),
            CompDetailParameter context => ViewModel.ApplyContext(
                context.Project,
                context.TargetType,
                context.TargetGameVersion,
                context.TargetModLoader),
            _ => ViewModel.ApplyContext(
                null,
                Comp.CompType.Any,
                null,
                Comp.CompLoaderType.Any)
        };
        if (!applied) return;
        Refresh();
    }

    public override void OnNavigatedFrom()
    {
    }

    private void Refresh()
    {
        if (ViewModel.Project is not { } project) return;

        RefreshIntro();

        BtnIntroWiki.IsVisible = Lang.IsChineseMainland && project.WikiId > 0;
        BtnTranslate.IsVisible = Lang.IsChineseMainland;
        ApplyButtonLocalization();
        RefreshFavoriteButton();

        PanResults.Children.Clear();
        CardFilter.IsVisible = false;
        PanLoad.IsVisible = true;
        ViewModel.StartLoad();
    }

    private void ApplyLoadState()
    {
        PanLoad.IsVisible = ViewModel.IsLoadingVisible || ViewModel.IsErrorVisible;
        CardFilter.IsVisible = ViewModel.FilterVisible;
    }

    private void ApplyIntroState()
    {
        BtnIntroWiki.IsVisible = ViewModel.IntroWikiVisible;
        BtnTranslate.IsVisible = ViewModel.TranslateVisible;
        RefreshIntro();
    }

    private void RefreshIntro()
    {
        if (ViewModel.IntroItem is not { } introItem)
        {
            if (_compItem is not null)
            {
                PanIntro.Children.Remove(_compItem);
                _compItem = null;
            }

            return;
        }

        if (_compItem is not null)
            PanIntro.Children.Remove(_compItem);

        _compItem = new MyCompItem(introItem)
        {
            CanInteraction = false,
            ShowFavoriteBtn = false,
            ShowDownloadBtn = false,
            Margin = new Thickness(-7d, -7d, 0d, 8d)
        };
        PanIntro.Children.Insert(0, _compItem);
    }

    private void ApplyButtonLocalization()
    {
        BtnOpenWebsite.Text = ViewModel.IntroWebText;
        BtnIntroWiki.Text = Lang.Text("Download.Comp.Detail.McMod");
        BtnTranslate.Text = Lang.Text("Download.Comp.Detail.TranslateDescription");
        BtnFavorites.Text = Lang.Text("Download.Comp.Detail.Favorite");
    }

    private void RefreshFavoriteButton()
    {
        ViewModel.RefreshFavorite();
        BtnFavorites.SvgIcon = ViewModel.IsFavorite
            ? "lucide/heart-filled"
            : "lucide/heart";
        _compItem?.RefreshFavoriteStatus();
    }

    private async Task TranslateDescriptionAsync()
    {
        if (ViewModel.Project is not { } project) return;
        try
        {
            BtnTranslate.IsEnabled = false;
            HintService.Show(
                Hints.Info(
                    Lang.Text(
                        "Download.Comp.Detail.DescriptionTranslating",
                        project.TranslatedName)));
            var chineseDescription = await project.ChineseDescription;
            if (chineseDescription is null) return;
            await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Common.Dialog.Title"),
                    Content = Lang.Text(
                        "Download.Comp.Detail.DescriptionTranslationResult",
                        project.Description,
                        chineseDescription),
                    Buttons = DialogButtons.Ok
                });
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "翻译资源简介失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }
        finally
        {
            BtnTranslate.IsEnabled = true;
        }
    }

    #region 筛选器

    private void BuildFilters()
    {
        try
        {
            CardFilter.IsVisible = ViewModel.FilterVisible;
            PanInstanceFilter.Children.Clear();
            PanModLoaderFilter.Children.Clear();
            PanModLoaderFilter.IsVisible = false;
            if (!ViewModel.FilterVisible)
                return;

            foreach (var chip in ViewModel.InstanceFilterLabelChips)
                PanInstanceFilter.Children.Add(CreateFilterLabel(chip.Text, true));
            foreach (var option in ViewModel.InstanceFilterOptions)
                PanInstanceFilter.Children.Add(
                    CreateFilterChip(
                        "InstanceFilter",
                        option,
                        selected => ViewModel.SelectInstanceFilterCommand.Execute(selected)));

            PanModLoaderFilter.IsVisible = ViewModel is
            {
                TargetType: Comp.CompType.Mod,
                ModLoaderFilterOptions.Count: > 0
            };
            if (!PanModLoaderFilter.IsVisible)
                return;

            foreach (var chip in ViewModel.ModLoaderFilterLabelChips)
                PanModLoaderFilter.Children.Add(CreateFilterLabel(chip.Text, false));
            foreach (var option in ViewModel.ModLoaderFilterOptions)
                PanModLoaderFilter.Children.Add(
                    CreateFilterChip(
                        "ModLoaderFilter",
                        option,
                        selected => ViewModel.SelectModLoaderFilterCommand.Execute(selected)));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "构建资源筛选器失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }
    }

    private static TextBlock CreateFilterLabel(string text, bool withBottomMargin)
    {
        return new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2d, 0d, 8d, withBottomMargin ? 4d : 0d)
        };
    }

    private static MyRadioButton CreateFilterChip(
        string groupName,
        CompFilterOption option,
        Action<CompFilterOption> onChecked)
    {
        var chip = new MyRadioButton
        {
            Text = option.Text,
            GroupName = groupName,
            ColorType = MyRadioButton.ColorState.Highlight,
            Checked = option.IsSelected,
            Margin = new Thickness(2d, 0d, 2d, 4d)
        };
        chip.Check += (_, _) => onChecked(option);
        return chip;
    }

    #endregion

    #region 文件列表

    private void BuildFileList()
    {
        try
        {
            PanResults.Children.Clear();
            if (ViewModel.FileGroups.Count == 0)
            {
                PanResults.Children.Add(
                    new TextBlock
                    {
                        Text = Lang.Text("Download.Comp.List.NoResults"),
                        Margin = new Thickness(4, 6, 4, 6)
                    });
                return;
            }

            foreach (var group in ViewModel.FileGroups)
            {
                var card = new MyCard
                {
                    Title = group.Title,
                    Margin = new Thickness(0, 0, 0, 15),
                    Tag = group
                };
                var stack = new StackPanel
                {
                    Margin = new Thickness(20, MyCard.SwapedHeight, 18, 0)
                };
                card.Children.Add(stack);
                card.SwapControl = stack;
                card.InstallMethod = target =>
                {
                    target.Children.Clear();
                    foreach (var file in group.Items)
                        target.Children.Add(CreateFileRow(file));
                    if (group.ShowRecognitionHint)
                        target.Children.Add(
                            new MyHint
                            {
                                Text = Lang.Text(
                                    "Download.Comp.Detail.VersionRecognitionDelayHint"),
                                Theme = MyHint.Themes.Yellow,
                                Margin = new Thickness(5, 0, 0, 8)
                            });
                };

                PanResults.Children.Add(card);
                if (!group.IsSwapped)
                    card.StackInstall();
                else
                    card.IsSwapped = true;
                card.Swap += (_, _) =>
                    ViewModel.SetGroupExpanded(group.Title, !card.IsSwapped);
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "可视化资源文件列表失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }
    }

    private Control CreateFileRow(CompFileItemViewModel file)
    {
        return new MyVirtualizingElement<MyListItem>(() => BuildFileRow(file))
        {
            Height = 42d
        };
    }

    private MyListItem BuildFileRow(CompFileItemViewModel fileItem)
    {
        var file = fileItem.File;
        var action = fileItem.Action;
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.Clickable,
            Height = 42d,
            Title = fileItem.TitleText,
            Info = fileItem.InfoText,
            Logo = fileItem.Logo,
            tag = file
        };

        var saveButton = new MyIconButton
        {
            SvgIcon = "lucide/save"
        };
        ToolTip.SetTip(saveButton, Lang.Text("Download.Version.SaveAs"));
        saveButton.Click += async (_, _) => await SaveFileAsync(file);

        if (action == CompFileAction.Save)
        {
            item.Buttons = [saveButton];
            item.Click += async (_, _) => await SaveFileAsync(file);
            return item;
        }

        var installButton = new MyIconButton
        {
            SvgIcon = "lucide/download"
        };
        ToolTip.SetTip(installButton, Lang.Text("Common.Action.Install"));
        installButton.Click += async (_, _) => await RunInstallAsync(action, file);
        item.Buttons = [installButton, saveButton];
        item.Click += async (_, _) => await RunInstallAsync(action, file);
        return item;
    }

    private Task RunInstallAsync(CompFileAction action, Comp.CompFile file)
    {
        return action switch
        {
            CompFileAction.InstallModpack => InstallModpackAsync(file),
            CompFileAction.InstallWorld => InstallWorldAsync(file),
            _ => SaveFileAsync(file)
        };
    }

    private static async Task SaveFileAsync(Comp.CompFile file)
    {
        try
        {
            var folder =
                SystemDialogs.SelectFolder(Lang.Text("Download.Comp.Detail.SelectSaveLocation"));
            if (string.IsNullOrEmpty(folder)) return;
            var target = folder.EndsWith(Path.DirectorySeparatorChar.ToString())
                ? folder
                : folder + Path.DirectorySeparatorChar;
            var downloadFile = file.ToNetFile(target);
            var loader = new LoaderDownload(
                Lang.Text("Download.Comp.Detail.DownloadFile"),
                [downloadFile]);
            loader.OnStateChangedUi += (sender, newState, _) =>
            {
                switch (newState)
                {
                    case LoadState.Finished:
                        HintService.Show(Hints.Success(Lang.Text("Common.Status.Success")));
                        break;

                    case LoadState.Failed:
                        HintService.Show(
                            Hints.Error(
                                sender.Error?.Message
                                ?? Lang.Text("Download.Comp.Error.OperationFailed")));
                        break;

                    case LoadState.Waiting:
                    case LoadState.Loading:
                    case LoadState.Aborted:
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
                }
            };
            loader.Start();
            HintService.Show(
                Hints.Info(
                    Lang.Text(
                        "Download.Comp.QuickDownload.Hint.DownloadStarted",
                        Path.GetFileName(downloadFile.LocalPath)),
                    false));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "下载资源文件失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }

        await Task.CompletedTask;
    }

    #region 安装到实例

    private readonly HashSet<string> _installingFiles = new(StringComparer.OrdinalIgnoreCase);

    private async Task InstallModpackAsync(Comp.CompFile file)
    {
        if (!RestrictedFeatures.Check("online-modpack-install"))
            return;
        if (ViewModel.Project is not { } project) return;
        var folder = GameFolders.mcFolderSelected;
        if (string.IsNullOrEmpty(folder))
        {
            HintService.Show(Hints.Error(Lang.Text("Download.Install.NoFolder")));
            return;
        }

        if (!_installingFiles.Add(file.FileName))
            return;
        var loaderStarted = false;
        try
        {
            var versionsFolder = Path.Combine(folder, "versions");
            var validator = new FolderNameValidator(versionsFolder);
            var defaultName = SanitizeInstanceName(project.TranslatedName);
            if (!(await validator.ValidateAsync(defaultName)).IsValid)
                defaultName = "";
            var nameResult = await DialogService.Instance.ShowAsync(
                new InputDialogRequest
                {
                    Title = Lang.Text("Download.Comp.Detail.InputInstanceName"),
                    DefaultValue = defaultName,
                    ValidateRules = [validator],
                    Buttons = DialogButtons.OkCancel,
                    HighLight = false
                });
            var instanceName = nameResult.IsConfirmed ? nameResult.InputText?.Trim() : null;
            if (string.IsNullOrEmpty(instanceName))
                return;

            var instanceFolder = Path.Combine(versionsFolder, instanceName);
            if (File.Exists(Path.Combine(instanceFolder, $"{instanceName}.json")))
            {
                HintService.Show(
                    Hints.Error(
                        Lang.Text(
                            "Minecraft.Download.Error.InstanceAlreadyExists",
                            instanceName,
                            "")));
                return;
            }

            // 上次失败留下的半成品目录：先按安装恢复服务清理可重试
            if (!McInstallRecovery.TryClearIncompleteInstanceFolder(instanceFolder, instanceName))
            {
                HintService.Show(
                    Hints.Error(
                        Lang.Text(
                            "Minecraft.Download.Error.InstanceAlreadyExists",
                            instanceName,
                            "")));
                return;
            }

            Directory.CreateDirectory(instanceFolder);
            var packPath = Path.Combine(
                instanceFolder,
                $"原始整合包.{(project.FromCurseForge ? "zip" : "mrpack")}");
            var fileName = file.FileName;
            var loaders = new List<Loading.LoaderBase>
            {
                new LoaderDownload(
                    Lang.Text("Download.Comp.Detail.DownloadModpackFile"),
                    [file.ToNetFile(packPath, Comp.DownloadReason.ModPack)])
                {
                    ProgressWeight = 10d,
                    block = true
                },
                new Loading.LoaderTask<int, int>(
                    Lang.Text("Download.Comp.Detail.PrepareModpackInstall"),
                    _ =>
                    {
                        var modpackLoader =
                            Modpack.ModpackInstall(packPath, instanceName, null, project.Id, true);
                        // 整合包安装继续在任务栏执行，安装结束前保持去重标记，避免同名实例被并发安装。
                        modpackLoader.OnStateChangedUi += (_, innerState, _) =>
                        {
                            if (innerState is not (LoadState.Waiting or LoadState.Loading))
                                _installingFiles.Remove(fileName);
                        };
                    })
                {
                    ProgressWeight = 0.1d
                }
            };

            var identifier = $"CompDetail:Modpack:{instanceName}";
            if (Loading.loaderTaskbar.Any(task => task.Identifier == identifier))
            {
                HintService.Show(
                    Hints.Error(Lang.Text("Minecraft.Download.Error.InstanceDownloading")));
                return;
            }

            var loaderName =
                $"{Lang.Text("Download.Comp.Detail.ModpackDownload")}：{project.TranslatedName}";
            var loader = new Loading.LoaderCombo<int>(loaderName, loaders)
            {
                Identifier = identifier
            };
            loader.OnStateChangedUi += (sender, newState, _) =>
                OnModpackInstallTerminal(sender, newState, fileName, instanceFolder, packPath);
            loader.Start();
            Loading.LoaderTaskbarAdd(loader);
            loaderStarted = true;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "开始整合包安装失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }
        finally
        {
            // 未真正启动安装（用户取消、输入不合法等）时立即解除去重标记，允许再次点击。
            if (!loaderStarted)
                _installingFiles.Remove(file.FileName);
        }
    }

    private void OnModpackInstallTerminal(
        Loading.LoaderBase loader,
        LoadState newState,
        string fileName,
        string instanceFolder,
        string packPath)
    {
        switch (newState)
        {
            case LoadState.Waiting or LoadState.Loading:
                return;
            case LoadState.Finished:
                LauncherCommon.Log(
                    $"[Comp] 已开始安装整合包：{Path.GetFileName(instanceFolder)}",
                    LauncherLogLevel.Developer);
                return;
        }

        _installingFiles.Remove(fileName);
        HintService.Show(
            newState == LoadState.Aborted
                ? Hints.Info(Lang.Text("Download.Comp.Detail.Task.Cancelled", loader.name))
                : Hints.Error(
                    Lang.Text(
                        "Download.Comp.Detail.Task.Failed",
                        loader.name,
                        loader.Error?.Message
                        ?? Lang.Text("Download.Comp.Error.OperationFailed"))));
        CleanupModpackDownload(instanceFolder, packPath);
    }

    private static void CleanupModpackDownload(string instanceFolder, string packPath)
    {
        try
        {
            McInstallRecovery.UnregisterSession(instanceFolder);
            if (File.Exists(packPath))
                File.Delete(packPath);
            if (Directory.Exists(instanceFolder)
                && !Directory.EnumerateFileSystemEntries(instanceFolder).Any())
                Directory.Delete(instanceFolder);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "清理整合包下载残留失败");
        }
    }

    private async Task InstallWorldAsync(Comp.CompFile file)
    {
        try
        {
            var defaultFolder = GetWorldDefaultFolder();
            var target = SystemDialogs.SelectSaveFile(
                Lang.Text("Download.Comp.Detail.SelectWorldInstallLocation"),
                file.FileName ?? "",
                Lang.Text("Download.Comp.Detail.WorldFile.Filter"),
                defaultFolder);
            if (string.IsNullOrEmpty(target))
                return;
            var targetFolder = Path.GetDirectoryName(target);
            if (string.IsNullOrEmpty(targetFolder))
                return;
            Directory.CreateDirectory(targetFolder);

            var loaders = new List<Loading.LoaderBase>
            {
                new LoaderDownload(
                    Lang.Text("Download.Comp.Detail.DownloadWorldFile"),
                    [
                        file.ToNetFile(
                            target,
                            Comp.DownloadReason.Standalone,
                            file.RawGameVersions.FirstOrDefault())
                    ])
                {
                    ProgressWeight = 10d,
                    block = true
                },
                new Loading.LoaderTask<int, int>(
                    Lang.Text("Download.Comp.Detail.InstallWorld"),
                    _ => LauncherCommon.ExtractFile(target, targetFolder, Encoding.UTF8))
                {
                    ProgressWeight = 0.1d,
                    block = true
                },
                new Loading.LoaderTask<int, int>(
                    Lang.Text("Download.Comp.Detail.CleanCache"),
                    _ => File.Delete(target))
            };

            var loaderName =
                $"{(ViewModel.Project?.FromCurseForge == true ? "CurseForge" : "Modrinth")} {Lang.Text("Download.Comp.Detail.WorldDownload")}：{ViewModel.Project?.TranslatedName}";
            var loader = new Loading.LoaderCombo<int>(loaderName, loaders);
            loader.OnStateChangedUi += (sender, newState, _) =>
            {
                switch (newState)
                {
                    case LoadState.Finished:
                        HintService.Show(Hints.Success(Lang.Text("Common.Status.Success")));
                        break;
                    case LoadState.Failed:
                        HintService.Show(
                            Hints.Error(
                                Lang.Text(
                                    "Download.Comp.Detail.Task.Failed",
                                    sender.name,
                                    sender.Error?.Message
                                    ?? Lang.Text("Download.Comp.Error.OperationFailed"))));
                        TryDeleteFile(target);
                        break;
                    case LoadState.Aborted:
                        HintService.Show(
                            Hints.Info(
                                Lang.Text("Download.Comp.Detail.Task.Cancelled", sender.name)));
                        TryDeleteFile(target);
                        break;
                    case LoadState.Waiting:
                    case LoadState.Loading:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
                }
            };
            loader.Start();
            Loading.LoaderTaskbarAdd(loader);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "下载世界资源失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Download.Comp.Error.OperationFailed"));
        }

        await Task.CompletedTask;
    }

    private static string GetWorldDefaultFolder()
    {
        try
        {
            var current = InstanceList.McMcInstanceSelected;
            if (current is not null)
            {
                var saves = Path.Combine(current.PathIndie, "saves");
                Directory.CreateDirectory(saves);
                return saves;
            }

            var folder = GameFolders.mcFolderSelected;
            if (!string.IsNullOrEmpty(folder))
            {
                var saves = Path.Combine(folder, "saves");
                Directory.CreateDirectory(saves);
                return saves;
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "获取世界资源默认保存目录失败");
        }

        return GameFolders.mcFolderSelected ?? "";
    }

    private static string SanitizeInstanceName(string name)
    {
        return name
            .Replace(".zip", "", StringComparison.OrdinalIgnoreCase)
            .Replace(".rar", "", StringComparison.OrdinalIgnoreCase)
            .Replace(".mrpack", "", StringComparison.OrdinalIgnoreCase)
            .Replace("\\", "＼")
            .Replace("/", "／")
            .Replace("|", "｜")
            .Replace(":", "：")
            .Replace("<", "＜")
            .Replace(">", "＞")
            .Replace("*", "＊")
            .Replace("?", "？")
            .Replace("\"", "")
            .Trim();
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "删除世界资源临时文件失败");
        }
    }

    #endregion

    #endregion
}