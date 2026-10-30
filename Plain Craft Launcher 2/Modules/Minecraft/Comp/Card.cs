using System.Windows;
using System.Windows.Controls;
using PCL.Core.App.Localization;

namespace PCL;

public static partial class Comp
{
    /// <summary>
    ///     预载包含大量 CompFile 的卡片，添加必要的元素和前置列表。
    ///     前置列表（必要 / 可选）会被放入可折叠栏：必要前置默认展开，可选前置默认收起。
    /// </summary>
    public static void CompFilesCardPreload(StackPanel stack, List<CompFile> files)
    {
        var deps = files.SelectMany(f => f.Dependencies).Distinct().ToList();
        var optionalDeps = files.SelectMany(f => f.OptionalDependencies).Distinct().ToList();
        if (deps.Count == 0 && optionalDeps.Count == 0)
            return;

        _AddDependencyBar(
            stack,
            deps,
            Lang.Text("Download.Comp.Detail.FileList.RequiredDependencies"),
            false);
        _AddDependencyBar(
            stack,
            optionalDeps,
            Lang.Text("Download.Comp.Detail.FileList.OptionalDependencies"),
            true);

        stack.Children.Add(
            new TextBlock
            {
                Text = Lang.Text("Download.Comp.Detail.FileList.VersionList"),
                FontSize = 14d,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(6d, 12d, 0d, 5d)
            });
    }

    /// <summary>
    ///     将一组前置依赖（按工程 ID）渲染为一个可折叠栏并加入 <paramref name="stack" />。
    ///     仅保留在 compProjectCache 中有信息的前置；若过滤后为空则不添加任何折叠栏。
    /// </summary>
    /// <param name="collapsed">是否默认收起。前置 item 全部加入即可，靠 MyVirtualizingElement 在可见时才实例化。</param>
    private static void _AddDependencyBar(
        StackPanel stack,
        List<string>? depIds,
        string title,
        bool collapsed)
    {
        if (depIds is null || depIds.Count == 0)
            return;

        depIds.Sort();
        var projects = new List<CompProject>();
        foreach (var dep in depIds)
            if (compProjectCache.TryGetValue(dep, out var project))
                projects.Add(project);
            else
                Common.Log($"[Comp] 未找到 ID {dep} 的前置信息", Common.LogLevel.Debug);
        if (projects.Count == 0)
            return;

        var bar = new MyCollapseBar
        {
            Title = $"{title} ({projects.Count})",
            IsCollapsed = collapsed
        };
        foreach (var project in projects)
            bar.ContentPanel.Children.Add(
                project.ToCompItem(false, false));

        stack.Children.Add(bar);
    }
}
