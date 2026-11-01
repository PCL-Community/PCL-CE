using PCL.Core.App.Localization;
using PCL.Core.Minecraft.ResourceProject;
using PCL.Core.UI.Dialogs;
using CompFileStatus = PCL.Comp.CompFileStatus;
using CompLoaderType = PCL.Comp.CompLoaderType;

namespace PCL;

public static partial class CompDependency
{
    public static Comp.CompDepsInstallTypes ConfirmDependencyInstall(
        ModDependencyResolutionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Unresolved is { Count: > 0 })
        {
            Common.Log($"[CompDeps] 无法解析: {result.Unresolved.Count} 个必需前置");
            var dependencies = string.Join(
                Environment.NewLine,
                result.Unresolved.Select(dep => Lang.Text(
                    "Download.Comp.Dependency.Unresolved.ListItem",
                    dep.Source,
                    dep.ProjectId,
                    dep.Reason)));
            var message = Lang.Text("Download.Comp.Dependency.Unresolved.Message", dependencies);
            var selectedButton = DialogService.Instance.Show(new TextDialogRequest
            {
                Title = Lang.Text("Download.Comp.Dependency.Unresolved.Title"),
                Content = message,
                Buttons = DialogButtons.From(
                    DialogButton.Create(Lang.Text("Download.Comp.Dependency.Unresolved.ContinueWithoutDependencies")),
                    DialogButton.Create(Lang.Text("Common.Action.Cancel"))),
                IsWarn = true,
                ForceWait = true
            }).Result?.Button;

            return selectedButton == 1
                ? Comp.CompDepsInstallTypes.Unresolved
                : Comp.CompDepsInstallTypes.Cancel;
        }

        if (result.ToInstall is { Count: > 0 })
        {
            var dependencies = string.Join(
                Environment.NewLine,
                result.ToInstall.Select(install => Lang.Text(
                    "Download.Comp.Dependency.Install.ListItem",
                    install.ProjectName,
                    install.Source,
                    install.File.DisplayName,
                    install.File.Version)));
            var message = Lang.Text("Download.Comp.Dependency.Install.Message", dependencies);
            var dialogResult = DialogService.Instance.Show(new TextDialogRequest
            {
                Title = Lang.Text("Download.Comp.Dependency.Install.Title"),
                Content = message,
                Buttons = DialogButtons.From(
                    DialogButton.Create(Lang.Text("Download.Comp.Dependency.Install.WithDependencies")),
                    DialogButton.Create(Lang.Text("Download.Comp.Dependency.Install.WithoutDependencies")),
                    DialogButton.Create(Lang.Text("Download.Comp.Dependency.Install.Cancel"))),
                ForceWait = true
            }).Result?.Button;

            return dialogResult switch
            {
                1 => Comp.CompDepsInstallTypes.WithDeps,
                2 => Comp.CompDepsInstallTypes.WithoutDeps,
                3 => Comp.CompDepsInstallTypes.Cancel,
                _ => Comp.CompDepsInstallTypes.Cancel
            };
        }

        return Comp.CompDepsInstallTypes.WithDeps;
    }

    private static string GetSource(bool fromCurseForge)
    {
        return fromCurseForge ? "CurseForge" : "Modrinth";
    }

    private static List<string> ToLoaderNames(IEnumerable<CompLoaderType>? loaders)
    {
        if (loaders is null) return [];

        return
        [
            .. loaders
                .Where(static loader => loader != CompLoaderType.Any)
                .Select(static loader => loader.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }

    private static int MapReleaseType(CompFileStatus status)
    {
        return status switch
        {
            CompFileStatus.Release => 1,
            CompFileStatus.Beta => 2,
            CompFileStatus.Alpha => 3,
            _ => 1
        };
    }
}
