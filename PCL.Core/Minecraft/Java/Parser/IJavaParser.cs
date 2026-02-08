namespace PCL.Core.Minecraft.Java.Parser;
public interface IJavaParser
{
    JavaInstallation? Parse(string javaExePath);
}
