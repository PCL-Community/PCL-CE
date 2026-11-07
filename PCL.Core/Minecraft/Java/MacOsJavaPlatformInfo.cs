using PCL.Core.Minecraft.Java.Parser;
using PCL.Core.Minecraft.Java.Scanner;
using PCL.Core.Platform;

namespace PCL.Core.Minecraft.Java;

public sealed class MacOsJavaPlatformInfo : IJavaPlatformInfo
{
    public string JavaExecutableName => "java";
    public string? JavawExecutableName => null;
    public bool SupportsJavaw => false;

    public IJavaParser CreateParser()
    {
        return new PosixJavaParser();
    }

    public IJavaScanner[] CreateScanners()
    {
        return
        [
            new PathEnvironmentScanner(),
            new MacOsJavaScanner()
        ];
    }
}