using System.Text.Json.Nodes;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.Core.App.Localization;
using PCL.UI.Controls;

namespace PCL.Views.Pages;

/// <summary>
///     帮助详情页：展示帮助 JSON 的标题与正文；正文由 <see cref="HelpXamlRenderer" /> 转换，无法转换时退回纯文本。
/// </summary>
public partial class PageHelpDetail : MyPageRight, IRefreshable
{
    private HelpContent? _content;

    public PageHelpDetail()
    {
        InitializeComponent();
        PanScroll = PanBack;
    }

    public string Title => _content?.Title ?? string.Empty;

    public void Refresh()
    {
        if (_content?.SourcePath is not { } sourcePath || !File.Exists(sourcePath))
            return;
        ApplyFromPath(sourcePath);
    }

    public event Action<string>? TitleChanged;

    public override void OnNavigatedTo(object? parameter)
    {
        switch (parameter)
        {
            case HelpContent content:
                ApplyContent(content);
                break;
            case string sourcePath when File.Exists(sourcePath):
                ApplyFromPath(sourcePath);
                break;
        }

        PanBack.ScrollToHome();
    }

    public override void OnNavigatedFrom()
    {
    }

    protected override void RefreshLocalization()
    {
        Refresh();
    }

    private void ApplyFromPath(string sourcePath)
    {
        try
        {
            ApplyContent(LoadContent(sourcePath));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "刷新帮助详情页失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Event.Error.ExecutionFailed", "OpenHelp", sourcePath));
            ShowPlainText(ex.Message);
        }
    }

    public static string ReadTitle(string jsonPath)
    {
        try
        {
            if (!File.Exists(jsonPath)) return "";
            var json = (JsonObject)LauncherCommon.GetJson(LauncherCommon.ReadFile(jsonPath));
            return json["Title"]?.ToString()?.Trim() ?? "";
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "读取帮助标题失败");
            return "";
        }
    }

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
        var rendered = HelpXamlRenderer.Render(content.Xaml, ResolveBrush);
        if (rendered is not null)
        {
            PanCustom.Children.Clear();
            PanCustom.Children.Add(rendered);
        }
        else
        {
            ShowPlainText(_ExtractPlainText(content.Xaml));
        }

        TitleChanged?.Invoke(content.Title);
    }

    private void ShowPlainText(string text)
    {
        PanCustom.Children.Clear();
        var body = new SelectableTextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap
        };
        var card = new Border
        {
            Child = body,
            Padding = new Thickness(18),
            Margin = new Thickness(0, 0, 0, 15)
        };
        card.Classes.Add("Card");
        PanCustom.Children.Add(card);
    }

    private IBrush? ResolveBrush(string key)
    {
        return this.TryFindResource(key, out var resource) && resource is IBrush brush
            ? brush
            : null;
    }

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

    /// <summary>帮助内容：来源 JSON 路径、标题与正文 XAML 片段。</summary>
    public sealed record HelpContent(
        string SourcePath,
        string Title,
        string Xaml);
}