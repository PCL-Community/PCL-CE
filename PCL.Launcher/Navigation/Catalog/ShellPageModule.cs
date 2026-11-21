using Microsoft.Extensions.DependencyInjection;
using PCL.Views.Pages.Download;
using PCL.Views.Pages.Help;
using PCL.Views.Pages.Log;
using PCL.Views.Pages.Speed;

namespace PCL.Navigation.Catalog;

public sealed class ShellPageModule : IPageModule
{
    public IReadOnlyList<PageDescriptor> Pages { get; } =
    [
        new(
            PageIds.CompDetail,
            "Download.Left.CommunityResources",
            null,
            null,
            0d,
            null,
            [],
            [],
            (sp, _) => ActivatorUtilities.CreateInstance<CompDetailHost>(sp)),
        new(
            PageIds.HelpDetail,
            "Main.Title.Help",
            null,
            null,
            0d,
            null,
            [],
            [],
            (sp, _) => ActivatorUtilities.CreateInstance<HelpDetailHost>(sp)),
        new(
            PageIds.TaskManager,
            "Main.Title.TaskManager",
            null,
            null,
            200d,
            null,
            [],
            [],
            (sp, _) => ActivatorUtilities.CreateInstance<TaskManagerHost>(sp)),
        new(
            PageIds.GameLog,
            "Main.Title.GameLog",
            null,
            null,
            300d,
            null,
            [],
            [],
            (sp, _) => ActivatorUtilities.CreateInstance<GameLogHost>(sp))
    ];
}