namespace PCL;

public abstract class DlForgelikeEntry : IComparable<DlForgelikeEntry>
{
    /// Forge、NeoForge、Cleanroom 等 Forge-like 加载器的版本列表条目的抽象基类。

    public enum ForgelikeType
    {
        Forge,
        NeoForge,
        Cleanroom
    }

    /// <summary>
    ///     Forgelike 种类。Forge、NeoForge、Cleanroom。
    /// </summary>
    public ForgelikeType ForgeType;

    /// <summary>
    ///     对应的 Minecraft 版本，如“1.12.2”。
    /// </summary>
    public string Inherit;

    /// <summary>
    ///     标准化后的版本号，仅可用于比较与排序。
    ///     格式：Major.Minor.Build.Revision
    ///     Forge：如 “50.1.9.0”（最后一位固定为 0）、“14.22.1.2478”（Legacy）。
    ///     NeoForge：如 “20.4.30.0”（最后一位固定为 0）、“19.47.1.99”（Legacy：第一位固定为 19）。
    ///     Cleanroom：如 “0.2.4.1”（Alpha：最后一位固定为 1）。
    /// </summary>
    public Version Version;

    /// <summary>
    ///     可对玩家显示的非格式化版本名。
    ///     Forge：如 “50.1.9”、“14.22.1.2478”（Legacy）。
    ///     NeoForge：如 “20.4.30-beta”、“47.1.99”（Legacy）。
    ///     Cleanroom：如 “0.2.4-alpha”。
    /// </summary>
    public string VersionName;

    /// <summary>
    ///     加载器名称。Forge / NeoForge / Cleanroom。
    /// </summary>
    public string LoaderName => ForgeType.ToString();

    /// <summary>
    ///     文件扩展名。不以小数点开头。
    /// </summary>
    public string FileExtension
    {
        get
        {
            if (ForgeType == ForgelikeType.Forge) return ((DlForgeVersion.DlForgeVersionEntry)this).Category == "installer" ? "jar" : "zip";

            return "jar";
        }
    }

    /// <summary>
    ///     Forge：MC 版本是否小于 1.13。
    ///     NeoForge：MC 版本是否为 1.20.1。
    ///     Cleanroom：固定为 False。
    /// </summary>
    public bool IsLegacy
    {
        get
        {
            // Cleanroom 始终为 False
            if (ForgeType == ForgelikeType.Cleanroom)
                return false;
            // 虽然很抽象，但确实可以这样判断
            // Forge：1.13+ 的版本号首位都大于 20
            // NeoForge：1.20.1 的版本号首位人为规定为 19 开头
            return Version.Major < 20;
        }
    }

    public int CompareTo(DlForgelikeEntry other)
    {
        if (Version != other.Version) return Version.CompareTo(other.Version);

        return ModMinecraft.CompareVersion(VersionName, other.VersionName);
    }
}
