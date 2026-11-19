using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Views.Pages.Download;

namespace PCL.UI.Install;

internal static class McInstallFlow
{
    public static Loading.LoaderCombo<string>? TryStart(
        McInstallRequest request,
        bool overwrite,
        string displayName,
        string input,
        string logMessage,
        Action<Loading.LoaderBase, LoadState> onTerminal)
    {
        var identifier = McInstallPipeline.GetIdentifier(request.targetInstanceFolder);
        if (Loading.loaderTaskbar.Any(loader => loader.Identifier == identifier))
        {
            HintService.Show(
                Hints.Error(Lang.Text("Minecraft.Download.Error.InstanceDownloading")));
            return null;
        }

        List<Loading.LoaderBase> loaders;
        try
        {
            loaders = McInstallPipeline.CreateInstallLoader(request, overwrite);
        }
        catch (CancelledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            HintService.Show(Hints.Error(ex.Message));
            LauncherCommon.Log(ex, logMessage);
            return null;
        }

        var combo = new Loading.LoaderCombo<string>(displayName, loaders)
        {
            Identifier = identifier
        };
        Loading.LoaderBase.OnStateChangedUiEventHandler? handler = null;
        handler = (loader, newState, _) =>
        {
            if (newState is LoadState.Waiting or LoadState.Loading)
                return;
            if (handler is not null)
                loader.OnStateChangedUi -= handler;
            onTerminal(loader, newState);
        };
        combo.OnStateChangedUi += handler;
        combo.Start(input);
        Loading.LoaderTaskbarAdd(combo);
        LoadingUi.Current?.RefreshDownloadButton();
        return combo;
    }

    public static void ApplyFinished(string instanceName)
    {
        try
        {
            var mcFolder = GameFolders.mcFolderSelected;
            if (string.IsNullOrEmpty(mcFolder))
                return;
            var iniPath = Path.Combine(mcFolder, "PCL.ini");
            if (!string.IsNullOrEmpty(instanceName) && Config.Download.AutoSelectInstance)
                Ini.Write(iniPath, "Version", instanceName);
            Ini.Write(iniPath, "InstanceCache", "");
            var root = mcFolder.EndsWith(Path.DirectorySeparatorChar)
                ? mcFolder
                : mcFolder + Path.DirectorySeparatorChar;
            Loading.LoaderFolderRun(
                InstanceList.mcInstanceListLoader,
                root,
                Loading.LoaderFolderRunType.ForceRun,
                1,
                "versions" + Path.DirectorySeparatorChar);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "安装完成后刷新实例列表失败");
        }
    }

    public static async Task<bool> ConfirmInstanceIsolationAsync()
    {
        var isolation = await DialogService.Instance.ShowAsync(
            new TextDialogRequest
            {
                Title = Lang.Text("Download.Install.InstanceIsolation.Warning.Title"),
                Content = Lang.Text("Download.Install.InstanceIsolation.Warning.Message"),
                Buttons = DialogButtons.From(
                    DialogButton.Create(
                        Lang.Text("Download.Install.InstanceIsolation.Warning.Cancel")),
                    DialogButton.Create(
                        Lang.Text("Download.Install.InstanceIsolation.Warning.Continue"))),
                IsWarn = true
            });
        return isolation.Button == 2;
    }

    public static string ResolveSelectionIcon(InstallLoaderSelection selection, string fallback)
    {
        return selection switch
        {
            { FabricVersion: not null } or { LegacyFabricVersion: not null }
                => $"{LauncherPaths.ImageRoot}Blocks/Fabric.png",
            { Forge: not null }
                => $"{LauncherPaths.ImageRoot}Blocks/Anvil.png",
            { NeoForge: not null }
                => $"{LauncherPaths.ImageRoot}Blocks/NeoForge.png",
            { LiteLoader: not null }
                => $"{LauncherPaths.ImageRoot}Blocks/Egg.png",
            { OptiFine: not null }
                => $"{LauncherPaths.ImageRoot}Blocks/GrassPath.png",
            { Cleanroom: not null }
                => $"{LauncherPaths.ImageRoot}Blocks/Cleanroom.png",
            { LabyModCommitRef: not null } or { LabyModVersion: not null }
                => $"{LauncherPaths.ImageRoot}Blocks/LabyMod.png",
            _ => fallback
        };
    }
}