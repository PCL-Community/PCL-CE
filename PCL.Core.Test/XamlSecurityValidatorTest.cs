using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Xaml;

namespace PCL.Test;

[TestClass]
public sealed class XamlSecurityValidatorTest
{
    [TestMethod]
    public void AllowsWhitelistedHomepageFragment()
    {
        const string xaml =
            /* language=XAML */
            """
            <local:MyHint
                    Margin="0,0,0,15"
                    Text="欢迎"
                    IsWarn="False" />
            <local:MyCard
                    Title="公告"
                    Margin="0,0,0,15"
                    CanSwap="True">
                <StackPanel Margin="25,40,23,11">
                    <TextBlock
                            Text="正文"
                            TextWrapping="Wrap"
                            Foreground="{DynamicResource ColorBrush1}" />
                    <local:MyButton
                            Text="打开网页"
                            EventType="打开网页"
                            EventData="https://example.com" />
                    <Image
                            Source="https://example.com/a.png"
                            Height="64" />
                </StackPanel>
            </local:MyCard>
            """;

        var result = XamlSecurityValidator.ValidateFragment(xaml);

        Assert.IsTrue(result.IsValid, string.Join("；", result.Errors));
    }

    [TestMethod]
    [DataRow("""<local:MyCard x:Class="Evil.Page" />""")]
    [DataRow("""<TextBlock Click="Hack" Text="x" />""")]
    [DataRow("""<local:MyButton Command="{Binding Hack}" Text="x" />""")]
    [DataRow("""<Image Source="file:///etc/passwd" />""")]
    [DataRow("""<local:MyListItem Logo="file:///etc/passwd.png" />""")]
    [DataRow("""<Image Source="\\\\server\\share\\a.png" />""")]
    [DataRow("""<Image Source="../../secret.png" />""")]
    [DataRow("""<ResourceDictionary Source="https://evil.example/x.xaml" />""")]
    [DataRow("<local:WebBrowser />")]
    [DataRow("<ObjectDataProvider />")]
    [DataRow("""<TextBlock Text="{x:Static System.IO.File}" />""")]
    [DataRow("""<TextBlock Text="{Binding Name}" />""")]
    [DataRow(
        "<StackPanel.Resources><ResourceDictionary><local:WebBrowser /></ResourceDictionary></StackPanel.Resources>")]
    [DataRow(
        """<Style TargetType="TextBlock"><Setter Property="Source" Value="file:///etc/passwd" /></Style>""")]
    [DataRow("""<local:MyCard xmlns:local="clr-namespace:Evil;assembly=Evil" />""")]
    [DataRow("""<sys:Object xmlns:sys="clr-namespace:System;assembly=mscorlib" />""")]
    [DataRow(
        """<!DOCTYPE Root [<!ENTITY xxe SYSTEM "file:///etc/passwd">]><TextBlock Text="&xxe;" />""")]
    public void RejectsUnsafeFragment(string xaml)
    {
        var result = XamlSecurityValidator.ValidateFragment(xaml);

        Assert.IsFalse(result.IsValid, "危险 XAML 不应通过安全校验");
        Assert.IsNotEmpty(result.Errors);
    }

    [TestMethod]
    public void AllowsWpfRichDocumentHomepageFragment()
    {
        const string xaml = """
                            <StackPanel.Resources>
                                <sys:String xmlns:sys="clr-namespace:System;assembly=mscorlib"
                                            x:Key="Icon">M0,0 L10,10</sys:String>
                                <Style TargetType="Paragraph">
                                    <Setter Property="Margin" Value="0,8" />
                                </Style>
                            </StackPanel.Resources>
                            <FlowDocumentScrollViewer>
                                <FlowDocument>
                                    <Paragraph>
                                        <Bold>更新内容</Bold>
                                        <TextBox Style="{StaticResource InlineCode}">terrain.vsh</TextBox>
                                    </Paragraph>
                                    <List>
                                        <ListItem><Paragraph>第一条</Paragraph></ListItem>
                                    </List>
                                    <Underline><local:MyTextButton Text="链接" EventType="打开网页" EventData="https://example.com" /></Underline>
                                    <Path Data="{StaticResource Icon}" Fill="{DynamicResource ColorBrush1}" />
                                </FlowDocument>
                            </FlowDocumentScrollViewer>
                            """;

        var result = XamlSecurityValidator.ValidateFragment(xaml);

        Assert.IsTrue(result.IsValid, string.Join("；", result.Errors));
    }

    [TestMethod]
    public void DetectsHomepageXamlFormat()
    {
        Assert.AreEqual(
            HomepageXamlFormat.Avalonia,
            HomepageXamlFormatDetector.Detect(
                """<StackPanel xmlns="https://github.com/avaloniaui" />"""));
        Assert.AreEqual(
            HomepageXamlFormat.Avalonia,
            HomepageXamlFormatDetector.Detect(
                """<StackPanel xmlns:ui="using:PCL.UI.Controls" />"""));
        Assert.AreEqual(
            HomepageXamlFormat.Wpf,
            HomepageXamlFormatDetector.Detect(
                """<local:MyCard xmlns:local="clr-namespace:PCL;assembly=Plain Craft Launcher 2" />"""));
    }

    [TestMethod]
    public void AllowsSafeAvaloniaHomepageFragment()
    {
        const string xaml = """
                            <StackPanel xmlns="https://github.com/avaloniaui"
                                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                        xmlns:ui="using:PCL.UI.Controls">
                                <StackPanel.Styles>
                                    <Style Selector="ui|MyCard">
                                        <Setter Property="Margin" Value="0,0,0,15" />
                                    </Style>
                                </StackPanel.Styles>
                                <ui:MyCard Title="AXAML 主页">
                                    <ui:MyButton Text="打开" EventType="打开网页" EventData="https://example.com" />
                                </ui:MyCard>
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
    [DataRow("pack://application:,,,/images/icon.png", true)]
    [DataRow("avares://PCL.Launcher/Assets/Images/icon.png", true)]
    [DataRow("file:///etc/passwd", false)]
    [DataRow(@"\\server\share\a.png", false)]
    [DataRow("../../a.png", false)]
    [DataRow("/images/../secret.png", false)]
    [DataRow("pack://application:,,,/images/../secret.png", false)]
    [DataRow("/etc/passwd", false)]
    [DataRow(@"C:\Windows\win.ini", false)]
    public void SourceWhitelist(string source, bool expected)
    {
        Assert.AreEqual(expected, XamlSecurityValidator.IsSafeSource(source));
    }
}