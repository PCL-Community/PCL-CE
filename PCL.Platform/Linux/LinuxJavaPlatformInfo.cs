using PCL.Core.Minecraft.Java.Parser;
using PCL.Core.Minecraft.Java.Scanner;
using PCL.Core.Platform;

namespace PCL.Platform;

public sealed class LinuxJavaPlatformInfo : IJavaPlatformInfo
{
    public string JavaExecutableName => "java";
    public string? JavawExecutableName => null;
    public string JstackExecutableName => "jstack";
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
            new LinuxJavaScanner()
        ];
    }
}