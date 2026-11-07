using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Xaml;

namespace PCL.Test;

[TestClass]
public sealed class XamlSecurityValidatorTest
{
    [TestMethod]
    public void AllowsWhitelistedHomepageFragment()
    {
        const string xaml = """
                            <local:MyHint Margin="0,0,0,15" Text="欢迎" IsWarn="False" />
                            <local:MyCard Title="公告" Margin="0,0,0,15" CanSwap="True">
                                <StackPanel Margin="25,40,23,11">
                                    <TextBlock Text="正文" TextWrapping="Wrap"
                                               Foreground="{DynamicResource ColorBrush1}" />
                                    <local:MyButton Text="打开网页" EventType="打开网页"
                                                    EventData="https://example.com" />
                                    <Image Source="https://example.com/a.png" Height="64" />
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
    [DataRow("""<sys:String xmlns:sys="clr-namespace:System;assembly=mscorlib" />""")]
    [DataRow(
        """<!DOCTYPE Root [<!ENTITY xxe SYSTEM "file:///etc/passwd">]><TextBlock Text="&xxe;" />""")]
    public void RejectsUnsafeFragment(string xaml)
    {
        var result = XamlSecurityValidator.ValidateFragment(xaml);

        Assert.IsFalse(result.IsValid, "危险 XAML 不应通过安全校验");
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