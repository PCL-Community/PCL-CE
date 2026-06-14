using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.ResourceProject;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public partial class PageInstanceModDetail
{
    private static ModComp.CompProject? _project;
    private static InstanceModContext? _context;
    private MyCompItem? _compItem;

    public PageInstanceModDetail()
    {
        InitializeComponent();
        PageEnter += DoLoad;
        BtnIntroWeb.Click += BtnIntroWeb_Click;
        BtnIntroWiki.Click += BtnIntroWiki_Click;
        BtnIntroCopy.Click += BtnIntroCopy_Click;
        BtnIntroLinkCopy.Click += BtnIntroLinkCopy_Click;
        BtnTranslate.Click += BtnTranslate_Click;
        BtnFavorites.Click += BtnFavorites_Click;
    }

    public static void SetContext(ModComp.CompProject project, InstanceModContext context)
    {
        _project = project;
        _context = context;
    }

    public static void SetContext(ModComp.CompProject project, McInstance instance)
    {
        var context = PageInstanceModBrowser.CreateContext(instance);
        if (context is null)
            return;
        SetContext(project, context);
    }

    private void DoLoad()
    {
        if (_project is null || _context is null)
            return;

        PanBack.ScrollToHome();
        ModAnimation.AniControlEnabled += 1;
        try
        {
            if (_compItem is not null)
                PanIntro.Children.Remove(_compItem);
            _compItem = _project.ToCompItem(true, true);
            _compItem.CanInteraction = false;
            _compItem.ShowFavoriteBtn = false;
            _compItem.Margin = new Thickness(-7, -7, 0, 8);
            PanIntro.Children.Insert(0, _compItem);

            BtnIntroWeb.Text = _project.FromCurseForge ? "CurseForge" : "Modrinth";
            BtnIntroWiki.Visibility = Lang.IsChineseMainland && _project.WikiId != 0
                ? Visibility.Visible
                : Visibility.Collapsed;
            BtnTranslate.Visibility = Lang.IsChineseMainland
                ? Visibility.Visible
                : Visibility.Collapsed;
            RefreshFavoriteButton();

            LabDescription.Text = string.IsNullOrWhiteSpace(_project.Description)
                ? Lang.Text("Instance.ModDetail.Description.Empty")
                : _project.Description;
            RefreshProjectInfo();

            var loaderName = _context.PrimaryLoader == ModComp.CompLoaderType.Any
                ? string.Empty
                : _context.PrimaryLoader + " ";
            CardFiles.Title = Lang.Text("Download.Comp.Detail.SelectedVersion", loaderName, _context.VanillaName);
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }

        LoadFiles();
    }

    private void RefreshProjectInfo()
    {
        if (_project is null)
            return;

        PanInfo.Children.Clear();
        AddInfoLine(Lang.Text("Instance.ModDetail.Info.Source"),
            _project.FromCurseForge ? "CurseForge" : "Modrinth");
        AddInfoLine(Lang.Text("Instance.ModDetail.Info.Downloads"),
            Lang.CompactNumber(_project.DownloadCount));
        if (_project.LastUpdate is not null)
            AddInfoLine(Lang.Text("Instance.ModDetail.Info.Updated"),
                Lang.TimeSpan(_project.LastUpdate.Value - DateTime.Now, 1));

        var loaders = _project.ModLoaders.Count == 0
            ? Lang.Text("Instance.ModDetail.Info.Any")
            : string.Join(" / ", _project.ModLoaders);
        AddInfoLine(Lang.Text("Instance.ModDetail.Info.Loaders"), loaders);

        if (_project.Tags.Count > 0)
            AddInfoLine(Lang.Text("Instance.ModDetail.Info.Categories"), string.Join(" / ", _project.Tags));
    }

    private void AddInfoLine(string label, string value)
    {
        var text = new TextBlock
        {
            Text = $"{label}{value}",
            Margin = new Thickness(0, 0, 0, 6),
            TextWrapping = TextWrapping.Wrap
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush2");
        PanInfo.Children.Add(text);
    }

    private void LoadFiles()
    {
        if (_project is null || _context is null)
            return;

        PanFiles.Children.Clear();
        PanLoad.Visibility = Visibility.Visible;
        PanMain.Visibility = Visibility.Collapsed;
        HintError.Visibility = Visibility.Collapsed;

        var project = _project;
        var context = _context;

        ModBase.RunInNewThread(() =>
        {
            try
            {
                var compatible = (ModComp.CompFilesGet(project.Id, project.FromCurseForge) ?? [])
                    .Where(file => file.Available)
                    .Where(file => file.GameVersions is not null &&
                                   file.GameVersions.Contains(context.VanillaName))
                    .Where(file => file.ModLoaders is null || file.ModLoaders.Count == 0 ||
                                   context.Loaders.Count == 0 ||
                                   file.ModLoaders.Any(context.Loaders.Contains))
                    .OrderByDescending(file => file.ReleaseDate)
                    .ToList();

                ModBase.RunInUi(() =>
                {
                    PanLoad.Visibility = Visibility.Collapsed;
                    PanMain.Visibility = Visibility.Visible;

                    if (compatible.Count == 0)
                    {
                        CardFiles.Visibility = Visibility.Collapsed;
                        HintError.Text = Lang.Text("Instance.ModDetail.Error.NoCompatibleVersion",
                            context.VanillaName);
                        HintError.Visibility = Visibility.Visible;
                        return;
                    }

                    CardFiles.Visibility = Visibility.Visible;
                    var badDisplayName = compatible.Select(file => file.DisplayName).Distinct().Count() !=
                                         compatible.Count;
                    ModComp.CompFilesCardPreload(PanFiles, compatible);
                    foreach (var file in compatible)
                        PanFiles.Children.Add(file.ToListItem(
                            (_, _) => DownloadAndReplace(file, context),
                            badDisplayName: badDisplayName));
                });
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[ModDetail] 加载版本失败");
                ModBase.RunInUi(() =>
                {
                    PanLoad.Visibility = Visibility.Collapsed;
                    PanMain.Visibility = Visibility.Visible;
                    CardFiles.Visibility = Visibility.Collapsed;
                    HintError.Text = Lang.Text("Instance.ModDetail.Error.LoadFailed", ex.Message);
                    HintError.Visibility = Visibility.Visible;
                });
            }
        });
    }

    private static void DownloadAndReplace(ModComp.CompFile file, InstanceModContext context)
    {
        if (_project is null)
            return;

        var modsFolder = context.ModsFolder;
        Directory.CreateDirectory(modsFolder);

        var project = _project;
        var localPath = Path.Combine(modsFolder, ModComp.CompFileNameGet(project, file));
        var downloadFiles = new List<DownloadFile> { file.ToNetFile(localPath) };
        var installedIds = new HashSet<string>(
            ModCompDependency.ScanInstalledMods(modsFolder)
                .Where(mod => !string.IsNullOrEmpty(mod.SourceProjectId))
                .Select(mod => mod.SourceProjectId!),
            StringComparer.OrdinalIgnoreCase);

        if ((file.Dependencies.Count > 0 || file.RawDependencies.Count > 0) &&
            !string.IsNullOrEmpty(context.VanillaName))
            try
            {
                var request = ModCompDependency.BuildRequest(file, project, context.VanillaName,
                    context.Loaders, modsFolder);
                var resolver = new ModDependencyResolver();
                var result = resolver.Resolve(request);

                if (result.Unresolved.Any() || result.ToInstall.Any())
                {
                    if (!ModCompDependency.ConfirmDependencyInstall(result))
                        return;

                    var dependencies = ModCompDependency.BuildDependencyDownloads(result, modsFolder, installedIds);
                    downloadFiles = dependencies.Concat(downloadFiles).ToList();
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[ModDetail] 依赖解析失败，跳过前置安装");
                ModMain.MyMsgBox(Lang.Text("Instance.ModDetail.DependencyFailed.Message", ex.Message),
                    Lang.Text("Instance.ModDetail.DependencyFailed.Title"),
                    button1: Lang.Text("Instance.ModDetail.DependencyFailed.Continue"),
                    isWarn: true, forceWait: true);
            }

        var loaderName = file.FileName ?? project.TranslatedName ?? project.RawName;
        var loader = new ModLoader.LoaderCombo<int>(loaderName,
        [
            new LoaderDownload(Lang.Text("Instance.ModDetail.Task.DownloadFile"), downloadFiles)
            {
                ProgressWeight = 6,
                block = true
            }
        ]);
        loader.OnStateChanged = state =>
        {
            if (state.State != ModBase.LoadState.Finished)
                return;
            MyCompItem.DownloadedProjectIds.Add(project.Id);
            CleanOldVersions(modsFolder, project.Id, file.FileName);
        };
        loader.Start(1);
        ModLoader.LoaderTaskbarAdd(loader);
        ModMain.frmMain.BtnExtraDownload.ShowRefresh();
        ModMain.frmMain.BtnExtraDownload.Ribble();
    }

    private void BtnIntroWeb_Click(object sender, EventArgs e)
    {
        if (_project is not null)
            ModBase.OpenWebsite(_project.Website);
    }

    private void BtnIntroWiki_Click(object sender, EventArgs e)
    {
        if (_project is not null)
            ModBase.OpenWebsite($"https://www.mcmod.cn/class/{_project.WikiId}.html");
    }

    private void BtnIntroCopy_Click(object sender, EventArgs e)
    {
        if (_compItem is not null)
            ModBase.ClipboardSet(_compItem.LabTitle.Text + _compItem.LabTitleRaw.Text);
    }

    private void BtnIntroLinkCopy_Click(object sender, EventArgs e)
    {
        if (_project is null)
            return;
        ModComp.CompClipboard.currentText = _project.Website;
        ModBase.ClipboardSet(_project.Website);
    }

    private async void BtnTranslate_Click(object sender, EventArgs e)
    {
        if (_project is null)
            return;
        ModMain.Hint(Lang.Text("Download.Comp.Detail.DescriptionTranslating", _project.TranslatedName));
        var translated = await _project.ChineseDescription;
        if (translated is null)
            return;
        ModMain.MyMsgBox(Lang.Text("Download.Comp.Detail.DescriptionTranslationResult",
            _project.Description, translated));
    }

    private void BtnFavorites_Click(object sender, EventArgs e)
    {
        if (_project is not null)
            ModComp.CompFavorites.ShowMenu(_project, (UIElement)sender, RefreshFavoriteButton);
    }

    private void RefreshFavoriteButton()
    {
        if (_project is null)
            return;
        BtnFavorites.SvgIcon = ModComp.CompFavorites.IsFavourite(_project.Id)
            ? "lucide/heart-filled"
            : "lucide/heart";
        _compItem?.RefreshFavoriteStatus();
    }

    private static void CleanOldVersions(string modsFolder, string projectId, string newFileName)
    {
        if (!Directory.Exists(modsFolder))
            return;
        try
        {
            foreach (var file in Directory.GetFiles(modsFolder, "*.jar"))
            {
                var name = Path.GetFileName(file);
                if (string.Equals(name, newFileName, StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    var local = new ModLocalComp.LocalCompFile(file);
                    local.Load();
                    if (!string.Equals(local.compFile?.ProjectId ?? local.Comp?.Id, projectId,
                            StringComparison.OrdinalIgnoreCase))
                        continue;
                    File.Delete(file);
                    ModBase.Log($"[ModDetail] 清理旧版: {name}");
                }
                catch
                {
                    // 单个旧文件解析失败不应阻止其余下载收尾。
                }
            }
        }
        catch
        {
            // 清理旧文件失败不影响新版本安装结果。
        }
    }
}
