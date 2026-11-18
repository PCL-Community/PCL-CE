using Avalonia.Controls;
using PCL.Views.Pages.Download;

namespace PCL.UI.Controls.Minecraft;

public class MinecraftInstallLoaderCardsView : UserControl
{
    private static readonly string[] TitleKeys =
    [
        "Common.Installation.Forge",
        "Common.Installation.Cleanroom",
        "Common.Installation.NeoForge",
        "Common.Installation.Fabric",
        "Common.Installation.LegacyFabric",
        "Common.Installation.FabricApi",
        "Common.Installation.LegacyFabricApi",
        "Common.Installation.LabyMod",
        "Common.Installation.OptiFine",
        "Common.Installation.OptiFabric",
        "Common.Installation.LiteLoader"
    ];

    public MinecraftInstallLoaderCardsView()
    {
        var panel = new StackPanel();
        foreach (var titleKey in TitleKeys)
        {
            var card = new InstallLoaderCard();
            card.SetLocalizedValue(InstallLoaderCard.TitleProperty, titleKey);
            panel.Children.Add(card);
        }

        Content = panel;
        Host = panel;
    }

    internal StackPanel Host { get; }
}