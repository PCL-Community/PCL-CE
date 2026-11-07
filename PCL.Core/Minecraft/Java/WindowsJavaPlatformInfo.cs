using PCL.Core.Minecraft.Java.Parser;
using PCL.Core.Minecraft.Java.Scanner;
using PCL.Core.Platform;

namespace PCL.Core.Minecraft.Java;

public sealed class WindowsJavaPlatformInfo : IJavaPlatformInfo
{
    public string JavaExecutableName => "java.exe";
    public string? JavawExecutableName => "javaw.exe";
    public bool SupportsJavaw => true;

    public IJavaParser CreateParser()
    {
        return new WindowsJavaParser();
    }

    public IJavaScanner[] CreateScanners()
    {
        return
        [
            new RegistryJavaScanner(),
            new DefaultPathsScanner(),
            new PathEnvironmentScanner(),
            new MicrosoftStoreJavaScanner(),
            new WindowsWhereCommandScanner()
        ];
    }
}