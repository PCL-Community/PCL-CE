using Avalonia;
using Avalonia.Controls;
using PCL.Views.Pages.Download;

namespace PCL.UI.Controls.Minecraft;

public class MinecraftInstallWarningsView : UserControl
{
    private readonly MyHint _fabricApi;
    private readonly MyHint _legacyFabricApi;
    private readonly MyHint _legacyOptiFabric;
    private readonly MyHint _modOptiFine;
    private readonly MyHint _optiFabric;
    private readonly MyHint _optiFabricOld;

    public MinecraftInstallWarningsView()
    {
        var panel = new StackPanel();
        _legacyOptiFabric = CreateHint(
            "Download.Install.Warning.LegacyOptiFabric",
            MyHint.Themes.Yellow,
            new Thickness(0d, 10d, 0d, 0d));
        _legacyFabricApi = CreateHint(
            "Download.Install.Warning.LegacyFabricApi",
            MyHint.Themes.Red,
            new Thickness(0d, 10d, 0d, 0d));
        _fabricApi = CreateHint(
            "Download.Install.Warning.FabricApi",
            MyHint.Themes.Red,
            new Thickness(0d, 1d, 0d, 7d));
        _optiFabric = CreateHint(
            "Download.Install.Warning.OptiFabric",
            MyHint.Themes.Red,
            new Thickness(0d, 1d, 0d, 7d));
        _optiFabricOld = CreateHint(
            "Download.Install.Warning.OptiFabricOld",
            MyHint.Themes.Blue,
            new Thickness(0d, 1d, 0d, 7d));
        _modOptiFine = CreateHint(
            "Download.Install.Warning.ModOptiFine",
            MyHint.Themes.Blue,
            new Thickness(0d, 1d, 0d, 7d));
        panel.Children.Add(_legacyOptiFabric);
        panel.Children.Add(_legacyFabricApi);
        panel.Children.Add(_fabricApi);
        panel.Children.Add(_optiFabric);
        panel.Children.Add(_optiFabricOld);
        panel.Children.Add(_modOptiFine);
        Content = panel;
    }

    internal void SetWarnings(InstallLoaderSelector.InstallLoaderWarningState flags)
    {
        _legacyOptiFabric.IsVisible = flags.LegacyOptiFabric;
        _legacyFabricApi.IsVisible = flags.LegacyFabricApi;
        _fabricApi.IsVisible = flags.FabricApi;
        _optiFabric.IsVisible = flags.OptiFabric;
        _optiFabricOld.IsVisible = flags.OptiFabricOld;
        _modOptiFine.IsVisible = flags.ModOptiFine;
    }

    private static MyHint CreateHint(string key, MyHint.Themes theme, Thickness margin)
    {
        var hint = new MyHint
        {
            Theme = theme,
            Margin = margin,
            IsVisible = false
        };
        hint.SetLocalizedValue(MyHint.TextProperty, key);
        return hint;
    }
}