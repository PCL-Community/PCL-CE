using System;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Java.Parser;
public interface IJavaParser
{
    /// Parse java file binary to get Java version and brand info.
    /// This should be implemented in different platform.
    Result<JavaInstallation, Exception> Parse(string javaExePath);
}
