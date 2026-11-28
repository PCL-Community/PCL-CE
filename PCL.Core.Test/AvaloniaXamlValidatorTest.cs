using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Xaml;

namespace PCL.Test;

[TestClass]
public sealed class AvaloniaXamlValidatorTest
{
    [TestMethod]
    public void AllowsSafeAvaloniaHomepageFragment()
    {
        const string xaml =
            // language=axaml
            """
            <StackPanel xmlns="https://github.com/avaloniaui"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        xmlns:ui="using:PCL.UI.Controls"
                        xmlns:pclcontrols="using:PCL.UI.Controls">
                <StackPanel.Styles>
                    <Style Selector="ui|MyCard">
                        <Setter Property="Margin" Value="0,0,0,15" />
                    </Style>
                </StackPanel.Styles>
                <ui:MyCard Title="AXAML 主页">
                    <ui:MyButton Text="打开" EventType="打开网页" EventData="https://example.com" />
                </ui:MyCard>
                <pclcontrols:MyTextButton Text="更多" EventType="打开网页" EventData="https://example.com" />
            </StackPanel>
            """;

        var result = AvaloniaXamlValidator.Validate(xaml);

        Assert.IsTrue(result.IsValid, string.Join("；", result.Errors));
    }

    [TestMethod]
    [DataRow("""<Window xmlns="https://github.com/avaloniaui" />""")]
    [DataRow("""<TextBlock xmlns="https://github.com/avaloniaui" Text="{Binding Name}" />""")]
    [DataRow(
        """<TextBlock xmlns="https://github.com/avaloniaui" Text="{x:Static System.IO.File}" />""")]
    [DataRow("""<TextBlock xmlns="https://github.com/avaloniaui" Click="Hack" />""")]
    [DataRow(
        """<TextBlock xmlns="https://github.com/avaloniaui" Text="{CompiledBinding Name}" />""")]
    [DataRow("""<StackPanel xmlns:evil="using:Evil"><evil:Thing /></StackPanel>""")]
    [DataRow(
        """<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" />""")]
    public void RejectsUnsafeAvaloniaFragment(string xaml)
    {
        var result = AvaloniaXamlValidator.Validate(xaml);

        Assert.IsFalse(result.IsValid, "危险 AXAML 不应通过安全校验");
        Assert.IsNotEmpty(result.Errors);
    }

    [TestMethod]
    [DataRow("https://example.com/a.png", true)]
    [DataRow("http://example.com/a.png", true)]
    [DataRow("/images/icon.png", true)]
    [DataRow("avares://PCL.Launcher/Assets/Images/icon.png", true)]
    [DataRow("pack://application:,,,/images/icon.png", false)]
    [DataRow("file:///etc/passwd", false)]
    [DataRow(@"\\server\share\a.png", false)]
    [DataRow("../../a.png", false)]
    [DataRow("/images/../secret.png", false)]
    [DataRow("/etc/passwd", false)]
    [DataRow(@"C:\Windows\win.ini", false)]
    public void SourceWhitelist(string source, bool expected)
    {
        Assert.AreEqual(expected, AvaloniaXamlValidator.IsSafeSource(source));
    }
}