using Avalonia.Media;
using PCL.Core.Platform;

namespace PCL.UI.Controls;

internal static class PlatformFontProvider
{
    public static IPlatformFontProvider Current => PlatformRuntime.Current.Kind switch
    {
        PlatformKind.Windows => new WindowsFontProvider(),
        PlatformKind.Linux => new LinuxFontProvider(),
        PlatformKind.MacOs => new MacOsFontProvider(),
        _ => throw new ArgumentOutOfRangeException()
    };
}

internal sealed class WindowsFontProvider : IPlatformFontProvider
{
    public FontFamily CreateBannerFont() => LaunchFonts.Build("Bahnschrift");
}

internal sealed class LinuxFontProvider : IPlatformFontProvider
{
    public FontFamily CreateBannerFont() => new(LaunchFonts.EmbeddedFontUri);
}

internal sealed class MacOsFontProvider : IPlatformFontProvider
{
    public FontFamily CreateBannerFont() => new(LaunchFonts.EmbeddedFontUri);
}
