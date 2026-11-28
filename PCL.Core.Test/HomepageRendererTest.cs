using Avalonia.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.UI.Xaml;

namespace PCL.Test;

[TestClass]
public sealed class HomepageRendererTest
{
    [TestMethod]
    public void RendersAxamlWithoutExplicitNamespace()
    {
        var result = HomepageRenderer.Render(
            """<StackPanel><TextBlock Text="主页" /></StackPanel>""");

        Assert.IsNull(result.Error);
        Assert.IsInstanceOfType<StackPanel>(result.Root);
    }

    [TestMethod]
    public void RendersMultiRootFragment()
    {
        var result = HomepageRenderer.RenderFragment(
            """<TextBlock Text="a" /><TextBlock Text="b" />""");

        Assert.IsNull(result.Error);
        var panel = result.Root as StackPanel;
        Assert.IsNotNull(panel);
        Assert.HasCount(2, panel.Children);
    }
}