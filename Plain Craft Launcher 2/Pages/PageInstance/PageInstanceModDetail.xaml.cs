using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using PCL.Core.Minecraft.ResourceProject;
using PCL.Network;

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

        ModMain.Hint($"正在解析 {file.FileName} 的依赖...", ModMain.HintType.Finish);
        ModBase.RunInNewThread(() =>
        {
            try
            {
                // 解析依赖
                var deps = new List<DownloadFile>();
                ModBase.Log($"[ModDetail] 依赖检查: Deps={file.Dependencies.Count}, RawDeps={file.RawDependencies.Count}, Optional={file.OptionalDependencies.Count}");
                if ((file.Dependencies is { Count: > 0 } || file.RawDependencies is { Count: > 0 }) && !string.IsNullOrEmpty(mcVersion))
                {
                    var request = ModCompDependency.BuildRequest(file, project, mcVersion, loaders, modsFolder);
                    request.InstalledMods.Clear();
                    var resolver = new ModDependencyResolver();
                    var result = resolver.Resolve(request);
                    ModBase.Log($"[ModDetail] 依赖解析结果: ToInstall={result.ToInstall?.Count ?? 0}, Unresolved={result.Unresolved?.Count ?? 0}");

                    if (result.ToInstall is { Count: > 0 })
                    {
                        var depNames = string.Join(", ",
                            result.ToInstall.Select(d => d.ProjectName ?? d.ProjectId));
                        ModBase.RunInUi(() =>
                            ModMain.Hint($"正在下载前置：{depNames}", ModMain.HintType.Finish));

                        deps = ModCompDependency.BuildDependencyDownloads(result, modsFolder);
                    }
                }

                // 下载主文件
                var localPath = Path.Combine(modsFolder, file.FileName);
                var allUrls = new List<IEnumerable<string>> { file.DownloadUrls };
                var allPaths = new List<string> { localPath };
                foreach (var dl in deps)
                {
                    allUrls.Add(dl.Urls);
                    allPaths.Add(dl.LocalPath);
                }
                for (var i = 0; i < allUrls.Count; i++)
                    FileDownloader.Download(allUrls[i], allPaths[i]).GetAwaiter().GetResult();

                // 清理主文件旧版本
                CleanOldVersions(modsFolder, file.FileName);
                // 清理每个前置的旧版本
                foreach (var dl in deps)
                {
                    var depName = Path.GetFileName(dl.LocalPath);
                    if (!string.IsNullOrEmpty(depName))
                        CleanOldVersions(modsFolder, depName);
                }

                ModBase.RunInUi(() =>
                    ModMain.Hint($"下载完成{(deps.Count > 0 ? $"（含 {deps.Count} 个前置）" : "")}", ModMain.HintType.Finish));
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "下载失败");
                ModBase.RunInUi(() => ModMain.Hint("下载失败", ModMain.HintType.Critical));
            }
        });
    }

    private static void CleanOldVersions(string modsFolder, string newFileName)
    {
        if (!Directory.Exists(modsFolder)) return;
        var prefix = GetNamePrefix(newFileName);
        var shortPrefix = prefix.Contains('-') ? prefix[..prefix.LastIndexOf('-')] : prefix;
        if (string.IsNullOrEmpty(prefix) || prefix.Length < 2) return;

        try
        {
            var files = Directory.GetFiles(modsFolder, "*.jar")
                .Where(f =>
                {
                    var n = Path.GetFileName(f).ToLower();
                    return n.StartsWith(prefix.ToLower() + "-") ||
                           n.StartsWith(prefix.ToLower() + ".") ||
                           n.StartsWith(shortPrefix.ToLower() + "-") ||
                           n.StartsWith(shortPrefix.ToLower() + ".");
                });

            foreach (var f in files)
            {
                var n = Path.GetFileName(f);
                if (string.Equals(n, newFileName, StringComparison.OrdinalIgnoreCase)) continue;
                try { File.Delete(f); ModBase.Log($"[ModDetail] 清理旧版: {n}"); } catch { }
            }
        }
        catch { }
    }

    private static string GetNamePrefix(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        // 跳过文件名开头的非 ASCII 字符（中文译名）
        var start = 0;
        while (start < name.Length && name[start] > 127)
            start++;
        if (start >= name.Length) start = 0;
        name = name[start..];

        for (var i = name.Length - 1; i >= 0; i--)
        {
            if (char.IsDigit(name[i]) && i > 0 && (name[i - 1] == '-' || name[i - 1] == '_'))
            {
                return name[..(i - 1)].TrimEnd('-', '_', '.');
            }
        }
        return name;
    }
}
