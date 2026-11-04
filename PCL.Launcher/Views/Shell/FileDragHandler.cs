using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;

namespace PCL.Views.Shell;

/// <summary>
///     主窗口文件拖放：authlib 验证服务器链接、自定义主页 xaml、整合包安装，其余资源文件交给页面注册的处理器。
///     页面可通过 <see cref="FileDropped" /> 抢先接收拖入的文件（如 Mod、存档、资源包）。
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

        if (extension is ".zip" or ".rar" or ".mrpack")
        {
            InstallModpack(paths[0]);
            return;
        }

        // TODO(SHL-05): Mod/原理图/存档/资源包安装依赖实例资源页注册 FileDropped；错误报告分析依赖崩溃分析服务
        LogWrapper.Info("UI", $"拖入的文件暂时没有对应操作：{paths[0]}");
        HintService.Show(Hints.Info(Lang.Text("Main.FileDrag.UnknownOperation")));
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
            ".litematic",
            ".nbt",
            ".schematic",
            ".schem"
        };
        if (!accepted.Contains(first)) return false;
        return paths.All(path => Path.GetExtension(path).ToLowerInvariant() == first);
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

    private static void InstallModpack(string path)
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
                }
            },
            "FileDragModpack");
    }
}