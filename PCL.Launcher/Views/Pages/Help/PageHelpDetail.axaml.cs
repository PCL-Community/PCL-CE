using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.UI.Controls;

namespace PCL.Views.Pages;

/// <summary>
///     帮助详情页：展示帮助 JSON 的标题与正文；正文由 <see cref="HelpXamlRenderer" /> 转换，无法转换时退回纯文本。
/// </summary>
public partial class PageHelpDetail : MyPageRight, IRefreshable
{
    public PageHelpDetail()
    {
        InitializeComponent();
        PanScroll = PanBack;
        DataContext = ViewModel;
    }

    public string Title => ViewModel.Title;

    public PageHelpDetailViewModel ViewModel { get; } = new();

    public void Refresh()
    {
        _ = RefreshAsync();
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
                _ = LoadFromPathAsync(sourcePath);
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

    private async Task RefreshAsync()
    {
        if (!await ViewModel.RefreshAsync())
            return;
        RenderContent();
    }

    private async Task LoadFromPathAsync(string sourcePath)
    {
        if (!await ViewModel.LoadFromPathAsync(sourcePath))
        {
            ShowPlainText(ViewModel.PlainText);
            return;
        }

        RenderContent();
    }

    public static string ReadTitle(string jsonPath)
    {
        return PageHelpDetailViewModel.ReadTitle(jsonPath);
    }

    public static HelpContent LoadContent(string jsonPath)
    {
        return PageHelpDetailViewModel.LoadContent(jsonPath);
    }

    private void ApplyContent(HelpContent content)
    {
        ViewModel.ApplyContent(content);
        RenderContent();
    }

    private void RenderContent()
    {
        if (ViewModel.Content is not { } content)
        {
            PanCustom.Children.Clear();
            return;
        }

        var rendered = HelpXamlRenderer.Render(
            PageHelpDetailViewModel.ReplaceArguments(content.Xaml),
            ResolveBrush);
        if (rendered is not null)
        {
            PanCustom.Children.Clear();
            PanCustom.Children.Add(rendered);
        }
        else
        {
            ShowPlainText(
                _ExtractPlainText(
                    PageHelpDetailViewModel.ReplaceArguments(content.Xaml)));
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