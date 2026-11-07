using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App.Localization;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Homepage;

public static class HomepageCardRenderer
{
    private const double HintFontSize = 13.5d;

    public static MyCard CreateTrivia(
        string text,
        Action onRefresh,
        Func<string, IBrush?> resolveBrush)
    {
        var card = new MyCard
        {
            Margin = new Thickness(0, 0, 0, 15)
        };
        LocalizedText.Bind(card, MyCard.TitleProperty, () => Lang.Text("Launch.Status.Trivia"));

        var refreshButton = new MyIconButton
        {
            Width = 22,
            Height = 22,
            Margin = new Thickness(9),
            SvgIcon = "lucide/refresh-cw",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top
        };
        ToolTip.SetTip(refreshButton, Lang.Text("Common.Action.Refresh"));

        var body = new TextBlock
        {
            Margin = new Thickness(25, 38, 23, 15),
            FontSize = HintFontSize,
            IsHitTestVisible = false,
            TextWrapping = TextWrapping.Wrap,
            Text = text,
            Foreground = resolveBrush("ColorBrush1")
        };
        refreshButton.Click += (_, _) => onRefresh();

        card.Children.Add(refreshButton);
        card.Children.Add(body);
        return card;
    }

}