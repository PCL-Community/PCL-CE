using System.Collections.Generic;

namespace PCL.Core.Minecraft.Modpack.Model;

/// <summary>
/// 整合包声明的实例启动设置。
/// <para>
/// 只包含整合包「明确要求」的项：MultiMC 的 <c>Override*</c> 开关为 <c>false</c> 时，
/// 对应字段保持 <c>null</c>，表示沿用启动器全局设置而非强制覆盖。
/// </para>
/// </summary>
public sealed record ModpackLaunchOptions
{
    /// <summary>附加的 JVM 参数。</summary>
    public IReadOnlyList<string> JvmArguments { get; init; } = [];

    /// <summary>附加的游戏参数。</summary>
    public IReadOnlyList<string> GameArguments { get; init; } = [];

    /// <summary>最小内存（MB）。</summary>
    public int? MinMemoryMegabytes { get; init; }

    /// <summary>最大内存（MB）。</summary>
    public int? MaxMemoryMegabytes { get; init; }

    /// <summary>指定的 Java 可执行文件路径。</summary>
    public string? JavaPath { get; init; }

    /// <summary>整合包声明支持的 Java 主版本号列表。</summary>
    public IReadOnlyList<int> SupportedJavaMajors { get; init; } = [];

    /// <summary>启动前执行的命令。</summary>
    public string? PreLaunchCommand { get; init; }

    /// <summary>退出后执行的命令。</summary>
    public string? PostExitCommand { get; init; }

    /// <summary>包装命令。</summary>
    public string? WrapperCommand { get; init; }

    /// <summary>启动后自动加入的服务器地址。</summary>
    public string? ServerToJoin { get; init; }

    /// <summary>是否忽略 Java 兼容性警告。</summary>
    public bool? IgnoreJavaCompatibility { get; init; }

    /// <summary>压缩包内实例图标的路径，相对于逻辑根。</summary>
    public string? IconArchivePath { get; init; }

    /// <summary>整合包备注 / 说明。</summary>
    public string? Notes { get; init; }

    public static ModpackLaunchOptions None { get; } = new();

    /// <summary>是否不含任何需要写入实例设置的内容。</summary>
    public bool IsEmpty =>
        JvmArguments.Count == 0 &&
        GameArguments.Count == 0 &&
        MinMemoryMegabytes is null &&
        MaxMemoryMegabytes is null &&
        JavaPath is null &&
        SupportedJavaMajors.Count == 0 &&
        PreLaunchCommand is null &&
        PostExitCommand is null &&
        WrapperCommand is null &&
        ServerToJoin is null &&
        IgnoreJavaCompatibility is null &&
        IconArchivePath is null &&
        Notes is null;
}
