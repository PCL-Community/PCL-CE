using PCL.Core.Minecraft.Java.Parser;
using PCL.Core.Minecraft.Java.Scanner;

namespace PCL.Core.Platform;

public interface IJavaPlatformInfo
{
    string JavaExecutableName { get; }
    string? JavawExecutableName { get; }
    bool SupportsJavaw { get; }
    IJavaParser CreateParser();
    IJavaScanner[] CreateScanners();
}