using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using PCL.Core.Minecraft.ResourceProject;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public partial class PageInstanceModDetail
{
    private static ModComp.CompProject? _project;
    private static McInstance? _instance;
    private static string? _downloadedFileName;

    public PageInstanceModDetail()
    {
        InitializeComponent();
        PageEnter += DoLoad;
    }

    private void DoLoad()
    {
        if (_project is null || _instance is null) return;
        PathLogo.Source = _project.LogoUrl ?? "pack://application:,,,/images/Icons/NoIcon.png";
        LabTitle.Text = _project.TranslatedName;
        LabAuthor.Text = _project.RawName;
        LoadFiles();
    }

    public static void SetContext(ModComp.CompProject project, McInstance instance)
    {
        _project = project;
        _instance = instance;
        _downloadedFileName = null;
    }

    private static ModComp.CompLoaderType GetLoaderType(McInstance inst)
    {
        if (inst.Info.HasFabric) return ModComp.CompLoaderType.Fabric;
        if (inst.Info.HasForge) return ModComp.CompLoaderType.Forge;
        if (inst.Info.HasNeoForge) return ModComp.CompLoaderType.NeoForge;
        if (inst.Info.HasQuilt) return ModComp.CompLoaderType.Quilt;
        return ModComp.CompLoaderType.Any;
    }

    private static string GetDisplayVersion(ModComp.CompFile f, string vanilla)
    {
        // 优先用 DisplayName
        if (!string.IsNullOrEmpty(f.DisplayName) && f.DisplayName != f.FileName)
            return f.DisplayName;

        // 从 FileName 提取版本：去掉 vanilla 版本号、去掉扩展名
        var name = f.FileName ?? "";
        name = name.Replace(".jar", "").Replace(vanilla, "").Trim('-', '_', ' ');
        // 去掉常见的 loader 前缀
        name = name.Replace("fabric-", "").Replace("Fabric-", "")
                   .Replace("forge-", "").Replace("Forge-", "")
                   .Replace("neoforge-", "").Replace("NeoForge-", "")
                   .Trim('-', '_', ' ');
        return string.IsNullOrEmpty(name) ? vanilla : name;
    }

    private void LoadFiles()
    {
        PanFiles.Children.Clear();
        PanLoad.Visibility = Visibility.Visible;
        CardFiles.Visibility = Visibility.Collapsed;
        HintError.Visibility = Visibility.Collapsed;

        var project = _project!;
        var instance = _instance!;
        var vanillaName = instance.Info.VanillaName;
        var targetLoader = GetLoaderType(instance);

        ModBase.RunInNewThread(() =>
        {
            try
            {
                var files = ModComp.CompFilesGet(project.Id, project.FromCurseForge);
                var compatible = (files ?? [])
                    .Where(f => f.GameVersions is not null && f.GameVersions.Contains(vanillaName))
                    .Where(f => f.ModLoaders is null || f.ModLoaders.Count == 0 ||
                                f.ModLoaders.Contains(targetLoader))
                    .OrderByDescending(f => f.ReleaseDate)
                    .ToList();

                ModBase.RunInUi(() =>
                {
                    PanLoad.Visibility = Visibility.Collapsed;
                    CardFiles.Visibility = Visibility.Visible;

                    if (compatible.Count == 0)
                    {
                        HintError.Text = $"没有找到兼容 {vanillaName} 的版本";
                        HintError.Visibility = Visibility.Visible;
                        return;
                    }

                    foreach (var file in compatible)
                    {
                        var bar = new System.Windows.Controls.Border
                        {
                            Height = 38,
                            Margin = new Thickness(0, 1, 0, 1),
                            Background = System.Windows.Media.Brushes.Transparent
                        };
                        var grid = new System.Windows.Controls.Grid();
                        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition
                            { Width = new GridLength(1, GridUnitType.Star) });
                        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition
                            { Width = GridLength.Auto });

                        var info = new System.Windows.Controls.StackPanel
                        {
                            Orientation = System.Windows.Controls.Orientation.Horizontal,
                            VerticalAlignment = System.Windows.VerticalAlignment.Center
                        };
                        System.Windows.Controls.Grid.SetColumn(info, 0);
                        var displayVer = GetDisplayVersion(file, vanillaName);
                        info.Children.Add(new System.Windows.Controls.TextBlock
                        {
                            Text = displayVer,
                            FontSize = 13,
                            VerticalAlignment = System.Windows.VerticalAlignment.Center,
                            Margin = new Thickness(8, 0, 12, 0),
                            ToolTip = file.FileName
                        });
                        grid.Children.Add(info);

                        var btn = new MyIconButton
                        {
                            SvgIcon = "lucide/download",
                            Height = 28, Width = 28,
                            LogoScale = 0.8,
                            ToolTip = "下载并替换旧版本",
                            Margin = new Thickness(0, 0, 8, 0),
                            VerticalAlignment = System.Windows.VerticalAlignment.Center
                        };
                        System.Windows.Controls.Grid.SetColumn(btn, 1);
                        btn.Click += (_, _) => DownloadAndReplace(file, instance);
                        grid.Children.Add(btn);

                        bar.Child = grid;
                        PanFiles.Children.Add(bar);
                    }
                });
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[ModDetail] 加载版本失败");
                ModBase.RunInUi(() =>
                {
                    PanLoad.Visibility = Visibility.Collapsed;
                    HintError.Text = "加载失败：" + ex.Message;
                    HintError.Visibility = Visibility.Visible;
                });
            }
        });
    }

    private static void DownloadAndReplace(ModComp.CompFile file, McInstance instance)
    {
        if (_project is null) return;
        var modsFolder = Path.Combine(instance.PathIndie, "mods");
        Directory.CreateDirectory(modsFolder);

        var project = _project;
        var mcVersion = instance.Info.VanillaName;
        var loaders = new List<ModComp.CompLoaderType>();
        if (instance.Info.HasFabric) loaders.Add(ModComp.CompLoaderType.Fabric);
        if (instance.Info.HasForge) loaders.Add(ModComp.CompLoaderType.Forge);
        if (instance.Info.HasNeoForge) loaders.Add(ModComp.CompLoaderType.NeoForge);
        if (instance.Info.HasQuilt) loaders.Add(ModComp.CompLoaderType.Quilt);

        // 构建下载列表
        var localPath = Path.Combine(modsFolder, ModComp.CompFileNameGet(project, file));
        var downloadFiles = new List<DownloadFile> { file.ToNetFile(localPath) };

        // 扫描已安装 Mod，按 ModId 去重
        var installedIds = new HashSet<string>(
            ModCompDependency.ScanInstalledMods(modsFolder)
                .Where(m => !string.IsNullOrEmpty(m.SourceProjectId))
                .Select(m => m.SourceProjectId!),
            StringComparer.OrdinalIgnoreCase);

        // 解析必需前置
        if (file.Dependencies is { Count: > 0 } && !string.IsNullOrEmpty(mcVersion))
        {
            try
            {
                var request = ModCompDependency.BuildRequest(file, project, mcVersion, loaders, modsFolder);
                var resolver = new ModDependencyResolver();
                var result = resolver.Resolve(request);

                if (result.Unresolved.Any() || result.ToInstall.Any())
                {
                    if (!ModCompDependency.ConfirmDependencyInstall(result))
                        return;

                    var depDownloads = ModCompDependency.BuildDependencyDownloads(result, modsFolder, installedIds);
                    downloadFiles = depDownloads.Concat(downloadFiles).ToList();
                }
            }
            catch (Exception depEx)
            {
                ModBase.Log(depEx, "[ModDetail] 依赖解析失败，跳过前置安装");
                ModMain.MyMsgBox("前置 Mod 解析失败，将仅下载本体。\n\n" + depEx.Message,
                    "前置解析失败", button1: "继续下载", isWarn: true, forceWait: true);
            }
        }

        // 通过 LoaderDownload 接入下载界面（支持进度显示、去重）
        var loaderName = file.FileName ?? project.TranslatedName ?? project.RawName;
        var subLoaders = new List<ModLoader.LoaderBase>
        {
            new LoaderDownload("下载文件", downloadFiles)
            {
                ProgressWeight = 6,
                block = true
            }
        };

        var loader = new ModLoader.LoaderCombo<int>(loaderName, subLoaders);
        loader.OnStateChanged = _ =>
        {
            if (_.State == ModBase.LoadState.Finished)
            {
                MyCompItem.DownloadedProjectIds.Add(project.Id);
                CleanOldVersions(modsFolder, project.Id, file.FileName);
            }
        };
        loader.Start(1);
        ModLoader.LoaderTaskbarAdd(loader);
    }

    private static void CleanOldVersions(string modsFolder, string projectId, string newFileName)
    {
        if (!Directory.Exists(modsFolder)) return;
        try
        {
            foreach (var f in Directory.GetFiles(modsFolder, "*.jar"))
            {
                var name = Path.GetFileName(f);
                if (string.Equals(name, newFileName, StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var local = new ModLocalComp.LocalCompFile(f);
                    local.Load();
                    if (string.Equals(local.compFile?.ProjectId ?? local.Comp?.Id, projectId, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(f);
                        ModBase.Log($"[ModDetail] 清理旧版: {name}");
                    }
                }
                catch { }
            }
        }
        catch { }
    }

}
