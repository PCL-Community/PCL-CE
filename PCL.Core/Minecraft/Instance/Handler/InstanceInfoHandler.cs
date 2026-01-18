using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Instance.Handler;

public static class InstanceInfoHandler {
    public static string GetFormattedVersion(string version) {
        return version == string.Empty ? "未知版本" : McFormatter.FormatVersion(version);
    }

    public static bool IsNormalVersion(string version) {
        return RegexPatterns.McNormalVersion.IsMatch(version);
    }
}
