using System.Text.Json.Nodes;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL.Views.Pages;

/// <summary>
///     帮助详情页：展示帮助 JSON 对应的标题与正文。
///     WPF 端把正文当作 WPF XAML 片段渲染，Avalonia 控件与其不通用，
///     因此当前以纯文本形式呈现正文，待内容控件迁移后替换渲染方式。
/// </summary>
public partial class PageHelpDetail : PageBase
{
    /// <summary>帮助内容：来源 JSON 路径、标题与正文 XAML 片段。</summary>
    public sealed record HelpContent(
        string SourcePath,
        string Title,
        string Xaml);

    private readonly ScrollViewer? _panBack;
    private readonly StackPanel? _panCustom;

    private HelpContent? _content;

    public PageHelpDetail()
    {
        InitializeComponent();
        _panBack = this.FindControl<ScrollViewer>("PanBack");
        _panCustom = this.FindControl<StackPanel>("PanCustom");
    }

    public string Title => _content?.Title ?? string.Empty;

    /// <summary>页面标题变化，供主窗口刷新标题栏。</summary>
    public event Action<string>? TitleChanged;

    public override void OnNavigatedTo(object? parameter)
    {
        _panBack?.Offset = default;

        switch (parameter)
        {
            case HelpContent content:
                ApplyContent(content);
                break;
            case string sourcePath when File.Exists(sourcePath):
                ApplyContent(LoadContent(sourcePath));
                break;
        }
    }

    public void Refresh()
    {
        if (_content?.SourcePath is { } sourcePath && File.Exists(sourcePath))
            ApplyContent(LoadContent(sourcePath));
    }

    /// <summary>从帮助 JSON 及同名 XAML 文件读取内容。</summary>
    public static HelpContent LoadContent(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            throw new FileNotFoundException("未找到帮助 JSON 文件", jsonPath);

        var json = (JsonObject)LauncherCommon.GetJson(LauncherCommon.ReadFile(jsonPath));
        var title = json["Title"]?.ToString();
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("帮助 JSON 中未找到有效的 Title 项", nameof(jsonPath));

        var xamlPath = Path.ChangeExtension(jsonPath, ".xaml");
        if (!File.Exists(xamlPath))
            throw new FileNotFoundException("未找到帮助 JSON 对应的 XAML 文件", xamlPath);
        var xaml = LauncherCommon.ReadFile(xamlPath);
        return string.IsNullOrWhiteSpace(xaml)
            ? throw new InvalidDataException("帮助 XAML 文件为空")
            : new HelpContent(jsonPath, title, xaml);
    }

    private void ApplyContent(HelpContent content)
    {
        _content = content;
        if (_panCustom is null)
            return;

        _panCustom.Children.Clear();

        var title = new TextBlock
        {
            Text = content.Title
        };
        title.Classes.Add("PageTitle");
        _panCustom.Children.Add(title);

        var body = new SelectableTextBlock
        {
            Text = _ExtractPlainText(content.Xaml),
            TextWrapping = TextWrapping.Wrap
        };
        var card = new Border
        {
            Child = body,
            Padding = new Thickness(18),
            Margin = new Thickness(0, 0, 0, 15)
        };
        card.Classes.Add("Card");
        _panCustom.Children.Add(card);

        TitleChanged?.Invoke(content.Title);
    }

    // TODO: 帮助正文是 WPF 控件片段的运行时 XAML，当前降级为纯文本提取；待帮助内容控件迁移后恢复富文本渲染。
    /// <summary>帮助正文是 WPF 控件片段，这里只提取其中的文本节点作为过渡展示。</summary>
    private static string _ExtractPlainText(string xaml)
    {
        try
        {
            var document = XDocument.Parse(
                "<Root xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\""
                + " xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">"
                + xaml
                + "</Root>");
            var lines = document
                .DescendantNodes()
                .OfType<XText>()
                .Select(text => text.Value.Trim())
                .Where(text => text.Length > 0);
            return string.Join("\n", lines);
        }
        catch (Exception)
        {
            return xaml;
        }
    }
}