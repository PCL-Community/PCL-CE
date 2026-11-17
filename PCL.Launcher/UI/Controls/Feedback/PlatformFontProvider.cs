using Avalonia.Media;
using PCL.Core.Platform;

namespace PCL.UI.Controls;

internal static class PlatformFontProvider
{
    public static IPlatformFontProvider Current =>
        ApplicationRuntime.GetService<IPlatformFontProvider>()
        ?? throw new InvalidOperationException("字体提供器尚未初始化");
}

internal sealed class WindowsFontProvider : IPlatformFontProvider
{
    public FontFamily CreateBannerFont()
    {
        return LaunchFonts.Build("Bahnschrift");
    }
}

internal sealed class LinuxFontProvider : IPlatformFontProvider
{
    public FontFamily CreateBannerFont()
    {
        return new FontFamily(LaunchFonts.EmbeddedFontUri);
    }
}

internal sealed class MacOsFontProvider : IPlatformFontProvider
{
    public FontFamily CreateBannerFont()
    {
        return new FontFamily(LaunchFonts.EmbeddedFontUri);
    }
}