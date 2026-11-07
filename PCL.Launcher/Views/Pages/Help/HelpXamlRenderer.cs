using Avalonia.Controls;
using PCL.UI.Xaml;

namespace PCL.Views.Pages;

/// <summary>
///     帮助内容的运行时 XAML 渲染入口。与自定义主页共用
///     <see cref="RuntimeXamlRenderer" /> 的白名单渲染链路，避免两套解析规则不一致。
/// </summary>
internal static class HelpXamlRenderer
{
    public static RuntimeXamlRenderResult Render(
        string fragment,
        Func<string, object?> resolveResource)
    {
        var result = RuntimeXamlRenderer.RenderFragment(fragment, resolveResource);
        if (!result.Success)
            return result;

        var panel = new StackPanel
        {
            Spacing = 12d
        };
        foreach (var control in result.Controls)
            panel.Children.Add(control);
        result.Controls.Clear();
        if (panel.Children.Count > 0)
            result.Controls.Add(panel);
        return result;
    }
}