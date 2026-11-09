using System.IO.Compression;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Navigation;
using PCL.Views.Pages.Instance;

namespace PCL.Views.Shell;

/// <summary>
///     主窗口文件拖放：authlib 验证服务器链接、自定义主页 xaml、实例资源、存档、整合包和崩溃报告。
///     页面可通过 <see cref="FileDropped" /> 抢先接收拖入的文件；未被页面处理的文件由这里按类型路由。
/// </summary>
public sealed class FileDragHandler
{
    private const string AuthlibPrefix = "authlib-injector:yggdrasil-server:";

    public FileDragHandler(Window window)
    {
        DragDrop.SetAllowDrop(window, true);
        DragDrop.AddDragEnterHandler(window, OnDragOver);
        DragDrop.AddDragOverHandler(window, OnDragOver);
        DragDrop.AddDropHandler(window, OnDrop);
    }

    public static event Func<IReadOnlyList<string>, bool>? FileDropped;

    public static event Action<string>? AuthlibServerDropped;

    public static event Action<string>? CustomHomepageDropped;

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.None;
        try
        {
            var data = e.DataTransfer;
            if (data is null) return;

            var text = data.TryGetText();
            if (!string.IsNullOrEmpty(text))
            {
                e.DragEffects = text.StartsWith(AuthlibPrefix, StringComparison.Ordinal)
                    || text.StartsWith("file:///", StringComparison.Ordinal)
                        ? DragDropEffects.Copy
                        : DragDropEffects.None;
                return;
            }

            var files = data.TryGetFiles();
            if (files is { Length: > 0 }) e.DragEffects = DragDropEffects.Link;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("UI", $"处理拖放类型失败：{ex.Message}");
        }
    }

    private static void OnDrop(object? sender, DragEventArgs e)
    {
        try
        {
            var data = e.DataTransfer;
            if (data is null) return;

            var text = data.TryGetText();
            if (!string.IsNullOrEmpty(text))
            {
                if (text.StartsWith(AuthlibPrefix, StringComparison.Ordinal))
                {
                    e.DragEffects = DragDropEffects.Copy;
                    HandleAuthlibText(text);
                    return;
                }

                if (text.StartsWith("file:///", StringComparison.Ordinal))
                {
                    e.DragEffects = DragDropEffects.Copy;
                    var path = new Uri(text).LocalPath;
                    HandleFiles([path]);
                    return;
                }
            }

            var files = data.TryGetFiles();
            if (files is { Length: > 0 })
            {
                e.DragEffects = DragDropEffects.Link;
                HandleFiles([.. files.Select(GetLocalPath).OfType<string>()]);
                return;
            }

            HintService.Show(Hints.Error(Lang.Text("Main.FileDrag.ExtractFirst")));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", "接收拖放失败");
        }
    }

    private static string? GetLocalPath(IStorageItem item)
    {
        try
        {
            return item.Path.LocalPath;
        }
        catch
        {
            return null;
        }
    }

    private static void HandleAuthlibText(string text)
    {
        var server = Uri.UnescapeDataString(text[AuthlibPrefix.Length..]);
        LogWrapper.Info("UI", $"拖入 authlib 验证服务器：{server}");
        if (!Uri.TryCreate(server, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            HintService.Show(Hints.Error(Lang.Text("Main.FileDrag.AuthlibInvalid", server)));
            return;
        }

        var confirmed = DialogService.Instance.Show(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Main.FileDrag.CreateAuthlibProfileTitle"),
                        Content = Lang.Text("Main.FileDrag.CreateAuthlibProfile", server),
                        Buttons = DialogButtons.OkCancel,
                        ForceWait = true
                    })
                .Result?.Button
            == 1;
        if (!confirmed) return;

        if (AuthlibServerDropped is null)
        {
            LogWrapper.Info("UI", "启动页尚未接入 authlib 拖放，本次已忽略");
            return;
        }

        AuthlibServerDropped(server);
    }

    private static void HandleFiles(IReadOnlyList<string> rawPaths)
    {
        var paths = rawPaths.Where(path => !string.IsNullOrWhiteSpace(path)).ToList();
        if (paths.Count == 0)
        {
            HintService.Show(Hints.Error(Lang.Text("Main.FileDrag.ExtractFirst")));
            return;
        }

        if (Directory.Exists(paths[0]))
        {
            HintService.Show(Hints.Error(Lang.Text("Main.FileDrag.FileOnly")));
            return;
        }

        if (!File.Exists(paths[0]))
        {
            HintService.Show(Hints.Error(Lang.Text("Main.FileDrag.FileNotFound", paths[0])));
            return;
        }

        if (paths.Count > 1 && !IsSameAcceptedType(paths))
        {
            HintService.Show(Hints.Error(Lang.Text("Main.FileDrag.SameTypeOnly")));
            return;
        }

        var extension = Path.GetExtension(paths[0]).ToLowerInvariant();
        if (extension == ".xaml")
        {
            LoadCustomHomepage(paths[0]);
            return;
        }

        foreach (var handler in FileDropped
                     ?.GetInvocationList()
                     .Cast<Func<IReadOnlyList<string>, bool>>()
                     .ToList()
                 ?? [])
            try
            {
                if (handler(paths)) return;
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "UI", $"页面处理拖放文件失败：{paths[0]}");
            }

        if (TryInstallMods(paths))
            return;

        if (TryInstallSchematics(paths))
            return;

        if (extension == ".zip" && PageRouter.Current is { Page: PageType.InstanceSetup } current)
            switch (current.SubType)
            {
                case PageSubType.VersionWorld:
                {
                    ImportSave(paths[0]);
                    return;
                }

                case PageSubType.VersionResourcePack or PageSubType.VersionShader:
                {
                    var type = current.SubType == PageSubType.VersionResourcePack
                        ? Comp.CompType.ResourcePack
                        : Comp.CompType.Shader;
                    if (TryInstallInstanceFiles(paths, type))
                        return;
                    break;
                }
            }

        switch (extension)
        {
            case ".zip" or ".rar" or ".mrpack":
            {
                if (extension == ".zip")
                    InstallModpack(paths[0], () => AnalyzeCrashReport(paths[0]));
                else
                    InstallModpack(paths[0]);
                return;
            }

            case ".txt" or ".log":
            {
                AnalyzeCrashReport(paths[0]);
                return;
            }

            default:
            {
                LogWrapper.Info("UI", $"拖入的文件暂时没有对应操作：{paths[0]}");
                HintService.Show(Hints.Info(Lang.Text("Main.FileDrag.UnknownOperation")));
                break;
            }
        }
    }

    private static bool TryInstallMods(IReadOnlyList<string> paths)
    {
        var modExtensions = new[]
        {
            ".jar",
            ".litemod",
            ".disabled",
            ".old"
        };
        if (!paths.All(path => modExtensions.Contains(
                Path.GetExtension(path).ToLowerInvariant())))
            return false;

        var instance = ResolveTargetInstance();
        if (instance is null)
        {
            HintService.Show(Hints.Info(Lang.Text("Main.FileDrag.NoInstance")));
            return true;
        }

        if (!instance.Modable)
        {
            HintService.Show(
                Hints.Info(Lang.Text("Instance.Resource.Install.SelectModableInstance")));
            return true;
        }

        var isModPage = PageRouter.Current is
            { Page: PageType.InstanceSetup, SubType: PageSubType.VersionMod };
        if (!isModPage)
        {
            var confirmed = DialogService.Instance.Show(
                        new TextDialogRequest
                        {
                            Title = Lang.Text("Instance.Resource.Install.ModConfirm.Title"),
                            Content = Lang.Text(
                                "Instance.Resource.Install.ModConfirm.Message",
                                instance.Name),
                            Buttons = DialogButtons.OkCancel,
                            ForceWait = true
                        })
                    .Result?.Button
                == 1;
            if (!confirmed) return true;
        }

        var folder = Path.Combine(
            instance.PathIndie,
            instance.Info.HasLabyMod
                ? Path.Combine("labymod-neo", "fabric", instance.Info.VanillaName)
                : string.Empty,
            LocalComp.GetPathNameByCompType(Comp.CompType.Mod));
        return InstallInstanceFiles(paths, folder);
    }

    private static McInstance? ResolveTargetInstance()
    {
        return PageInstanceLeft.ResolveInstance() ?? InstanceList.McMcInstanceSelected;
    }

    private static bool TryInstallSchematics(IReadOnlyList<string> paths)
    {
        if (!paths.All(path => LocalComp.LocalCompFile.IsCompFile(
                path,
                Comp.CompType.Schematic)))
            return false;

        var instance = ResolveTargetInstance();
        if (instance is null)
        {
            HintService.Show(Hints.Info(Lang.Text("Main.FileDrag.NoInstance")));
            return true;
        }

        var folder = Path.Combine(
            instance.PathIndie,
            LocalComp.GetPathNameByCompType(Comp.CompType.Schematic));
        if (PageRouter.Current is
                { Page: PageType.InstanceSetup, SubType: PageSubType.VersionSchematic }
            && PageRouter.CurrentPage is ICompLocalPage
            {
                CurrentFolderPath: { Length: > 0 } currentFolder
            })
            folder = currentFolder;

        return InstallInstanceFiles(paths, folder);
    }

    private static bool TryInstallInstanceFiles(
        IReadOnlyList<string> paths,
        Comp.CompType type)
    {
        var instance = ResolveTargetInstance();
        if (instance is null)
        {
            HintService.Show(Hints.Info(Lang.Text("Main.FileDrag.NoInstance")));
            return true;
        }

        var folder = Path.Combine(
            instance.PathIndie,
            LocalComp.GetPathNameByCompType(type));
        return InstallInstanceFiles(paths, folder);
    }

    private static bool InstallInstanceFiles(
        IReadOnlyList<string> paths,
        string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var installed = 0;
            foreach (var path in paths)
            {
                var target = Path.Combine(folder, Path.GetFileName(path));
                if (File.Exists(target))
                {
                    var overwrite = DialogService.Instance.Show(
                                new TextDialogRequest
                                {
                                    Title = Lang.Text(
                                        "Instance.Resource.Install.OverwriteConfirm.Title"),
                                    Content = Lang.Text(
                                        "Instance.Resource.Install.OverwriteConfirm.Message",
                                        Path.GetFileName(target)),
                                    Buttons = DialogButtons.OkCancel,
                                    ForceWait = true
                                })
                            .Result?.Button
                        == 1;
                    if (!overwrite) continue;
                }

                File.Copy(path, target, true);
                installed++;
            }

            if (installed > 0)
            {
                HintService.Show(
                    Hints.Success(
                        Lang.Text(
                            "Main.FileDrag.Imported",
                            Path.GetFileName(paths[0]))));
                if (PageRouter.CurrentPage is ICompLocalPage page)
                    page.RefreshModList();
            }

            return true;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", $"导入实例资源失败：{paths[0]}");
            HintService.Show(Hints.Error(Lang.Text("Main.FileDrag.UnknownOperation")));
            return true;
        }
    }

    private static void ImportSave(string path)
    {
        if (ResolveTargetInstance() is not { } instance)
        {
            HintService.Show(Hints.Info(Lang.Text("Main.FileDrag.NoInstance")));
            return;
        }

        Basics.RunInNewThread(
            () =>
            {
                var temp = LauncherCommon.GetTempFolder("SaveImport");
                try
                {
                    Directory.CreateDirectory(temp);
                    ZipFile.ExtractToDirectory(path, temp, true);
                    var saveRoot = FindSaveRoot(temp);
                    if (saveRoot is null)
                    {
                        HintService.Show(Hints.Error(Lang.Text("Main.FileDrag.SaveNotFound")));
                        return;
                    }

                    var name = Path.GetFileNameWithoutExtension(path);
                    var target = Path.Combine(
                        instance.PathIndie,
                        LocalComp.GetPathNameByCompType(Comp.CompType.World),
                        name);
                    if (Directory.Exists(target))
                    {
                        HintService.Show(
                            Hints.Error(Lang.Text("Main.FileDrag.SameFolderExists", target)));
                        return;
                    }

                    LauncherCommon.CopyDirectory(saveRoot, target);
                    if (!File.Exists(Path.Combine(target, "level.dat")))
                    {
                        if (Directory.Exists(target))
                            Directory.Delete(target, true);
                        HintService.Show(Hints.Error(Lang.Text("Main.FileDrag.SaveInvalid")));
                        return;
                    }

                    HintService.Show(
                        Hints.Success(Lang.Text("Main.FileDrag.Imported", name)));
                    if (PageRouter.CurrentPage is PageInstanceSaves saves)
                        LauncherCommon.RunInUi(saves.Refresh);
                }
                catch (Exception ex)
                {
                    LogWrapper.Error(ex, "UI", $"导入存档失败：{path}");
                    HintService.Show(Hints.Error(Lang.Text("Main.FileDrag.SaveImportFailed")));
                }
                finally
                {
                    try
                    {
                        if (Directory.Exists(temp))
                            Directory.Delete(temp, true);
                    }
                    catch (Exception ex)
                    {
                        LogWrapper.Debug("UI", $"清理存档导入临时目录失败：{ex.Message}");
                    }
                }
            },
            "FileDragSave");
    }

    private static string? FindSaveRoot(string root)
    {
        if (File.Exists(Path.Combine(root, "level.dat")))
            return root;

        return Directory
            .EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .FirstOrDefault(folder => File.Exists(Path.Combine(folder, "level.dat")));
    }

    private static void AnalyzeCrashReport(string path)
    {
        Basics.RunInNewThread(
            () =>
            {
                try
                {
                    new CrashRunner(0).Import(path);
                }
                catch (Exception ex)
                {
                    LogWrapper.Error(ex, "UI", $"分析拖入的崩溃报告失败：{path}");
                    HintService.Show(Hints.Error(Lang.Text("Main.FileDrag.UnknownOperation")));
                }
            },
            "FileDragCrashReport");
    }

    private static bool IsSameAcceptedType(IReadOnlyList<string> paths)
    {
        var first = Path.GetExtension(paths[0]).ToLowerInvariant();
        var accepted = new[]
        {
            ".jar",
            ".litemod",
            ".disabled",
            ".old",
            ".zip",
            ".litematic",
            ".nbt",
            ".schematic",
            ".schem"
        };
        return accepted.Contains(first)
            && paths.All(path =>
                Path.GetExtension(path).Equals(first, StringComparison.InvariantCultureIgnoreCase));
    }

    private static void LoadCustomHomepage(string path)
    {
        var target = Path.Combine(Paths.Data, "Custom.xaml");
        if (File.Exists(target))
        {
            var overwrite = DialogService.Instance.Show(
                        new TextDialogRequest
                        {
                            Title = Lang.Text("Main.FileDrag.OverwriteTitle"),
                            Content = Lang.Text("Main.FileDrag.HomepageExists"),
                            Buttons = DialogButtons.OkCancel,
                            ForceWait = true
                        })
                    .Result?.Button
                == 1;
            if (!overwrite) return;
        }

        try
        {
            Directory.CreateDirectory(Paths.Data);
            File.Copy(path, target, true);
            Config.Preference.Homepage.Type = 1;
            HintService.Show(Hints.Success(Lang.Text("Main.FileDrag.HomepageLoaded")));
            CustomHomepageDropped?.Invoke(target);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", "导入自定义主页失败");
        }
    }

    private static void InstallModpack(string path, Action? onFailure = null)
    {
        LogWrapper.Info("UI", $"拖入整合包，开始安装：{path}");
        Basics.RunInNewThread(
            () =>
            {
                try
                {
                    var loader = Modpack.ModpackInstall(path);
                    Loading.LoaderTaskbarAdd(loader);
                    loader.Start();
                }
                catch (CancelledException)
                {
                }
                catch (Exception ex)
                {
                    LogWrapper.Error(ex, "UI", $"安装整合包失败：{path}");
                    onFailure?.Invoke();
                }
            },
            "FileDragModpack");
    }
}