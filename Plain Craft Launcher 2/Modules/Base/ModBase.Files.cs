using System.Text.RegularExpressions;

namespace PCL;

public static partial class ModBase
{
    public static string VersionBaseName => LauncherEnvironment.VersionBaseName;
    public static int VersionCode => LauncherEnvironment.VersionCode;

    public static object GetJson(string Data) => LauncherSerialization.GetJson(Data);
    public static string EscapeXML(string Str) => LauncherSerialization.EscapeXml(Str);
}
