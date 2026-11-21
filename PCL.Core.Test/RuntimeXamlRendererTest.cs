using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.UI.Xaml;

namespace PCL.Test;

[TestClass]
public sealed class RuntimeXamlRendererTest
{
    [TestMethod]
    public void RendersContentControlTemplateAndTextBoxTemplate()
    {
        const string xaml =
            /* language=XAML */
            """
            <StackPanel>
            <StackPanel.Resources>
                <ControlTemplate x:Key="Separator" TargetType="ContentControl">
                    <Grid>
                        <TextBlock Text="{TemplateBinding Content}" />
                    </Grid>
                </ControlTemplate>
                <Style x:Key="InlineCode" TargetType="TextBox">
                    <Setter Property="Template">
                        <Setter.Value>
                            <ControlTemplate TargetType="TextBox">
                                <Border Background="#112233" Padding="4,1">
                                    <ScrollViewer Name="PART_ContentHost" />
                                </Border>
                            </ControlTemplate>
                        </Setter.Value>
                    </Setter>
                </Style>
            </StackPanel.Resources>
            <ContentControl Content="历史版本" Template="{StaticResource Separator}" />
            <TextBox Style="{StaticResource InlineCode}">terrain.vsh</TextBox>
            <FlowDocumentScrollViewer>
                <FlowDocument>
                    <Paragraph>正文</Paragraph>
                    <List MarkerStyle="9">
                        <ListItem><Paragraph>第一条</Paragraph></ListItem>
                        <ListItem><Paragraph>第二条</Paragraph></ListItem>
                    </List>
                </FlowDocument>
            </FlowDocumentScrollViewer>
            </StackPanel>
            """;

        var result = RuntimeXamlRenderer.RenderFragment(xaml, _ => null);

        Assert.IsTrue(
            result.Success,
            result.Error ?? string.Join("；", result.Warnings));
        Assert.HasCount(1, result.Controls);

        var panel = (StackPanel)result.Controls[0];
        var contentControl = (ContentControl)panel.Children[0];
        Assert.IsInstanceOfType<Control>(contentControl.Content);
        Assert.AreEqual("历史版本", FindText(contentControl.Content as Control));

        var textBox = panel.Children[1];
        Assert.AreEqual("terrain.vsh", FindText(textBox));

        Assert.Contains("正文", FlattenText(panel.Children[2]));
        Assert.Contains("1.", FlattenText(panel.Children[2]));
        Assert.Contains("2.", FlattenText(panel.Children[2]));
    }

    [TestMethod]
    public void RendersExtendedControls()
    {
        const string xaml =
            /* language=XAML */
            """
            <StackPanel>
                <ItemsControl>
                    <TextBlock Text="item" />
                </ItemsControl>
                <ProgressBar Value="40" Maximum="100" />
                <Expander Header="更多">
                    <TextBlock Text="内容" />
                </Expander>
                <UniformGrid Columns="2">
                    <TextBlock Text="a" />
                    <TextBlock Text="b" />
                </UniformGrid>
                <Polygon Points="0,0 10,0 10,10" Fill="#112233" />
            </StackPanel>
            """;

        var result = RuntimeXamlRenderer.RenderFragment(xaml, _ => null);

        Assert.IsTrue(
            result.Success,
            result.Error ?? string.Join("；", result.Warnings));
        var panel = (StackPanel)result.Controls[0];
        Assert.AreEqual(5, panel.Children.Count);
        CollectionAssert.DoesNotContain(
            result.Warnings,
            "不支持的元素：ItemsControl");
        Assert.IsTrue(panel.Children[0] is ItemsControl);
        Assert.IsTrue(panel.Children[1] is ProgressBar);
        Assert.IsTrue(panel.Children[2] is Expander);
        Assert.IsTrue(panel.Children[3] is UniformGrid);
    }

    private static string? FindText(Control? control)
    {
        if (control is null)
            return null;
        if (control is TextBlock textBlock)
            return BlockText(textBlock);
        foreach (var child in Children(control))
            if (FindText(child) is { Length: > 0 } text)
                return text;
        return null;
    }

    private static string FlattenText(Control? control)
    {
        if (control is null)
            return "";
        var builder = new StringBuilder();
        if (control is TextBlock textBlock)
            builder.Append(BlockText(textBlock));
        foreach (var child in Children(control))
            builder.Append(FlattenText(child));
        return builder.ToString();
    }

    private static string BlockText(TextBlock block)
    {
        if (!string.IsNullOrEmpty(block.Text))
            return block.Text;
        if (block.Inlines is null)
            return "";
        var builder = new StringBuilder();
        foreach (var inline in block.Inlines)
            builder.Append(InlineText(inline));
        return builder.ToString();
    }

    private static string InlineText(Inline inline)
    {
        return inline switch
        {
            Run run => run.Text ?? "",
            InlineUIContainer { Child: Control child } => FlattenText(child),
            Span span => string.Concat(span.Inlines?.Select(InlineText) ?? []),
            _ => ""
        };
    }

    private static IEnumerable<Control> Children(Control control)
    {
        return control switch
        {
            Panel panel => panel.Children,
            Decorator { Child: not null } decorator => [decorator.Child],
            ContentControl { Content: Control content } => [content],
            _ => []
        };
    }
}