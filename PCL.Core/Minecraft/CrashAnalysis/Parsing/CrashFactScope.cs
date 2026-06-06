namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     表示事实对错误分析的作用范围，用于避免把环境信息或后续症状误判为根因。
/// </summary>
public enum CrashFactScope
{
    /// <summary>
    ///     后续症状。例如 Mixin 失败、普通 OpenGL error 或泛用异常。
    /// </summary>
    Symptom,

    /// <summary>
    ///     根因或强根因信号。例如缺依赖、Java 版本不兼容、明确 OOM。
    /// </summary>
    RootCause,

    /// <summary>
    ///     环境事实。例如 Java 版本、Minecraft 版本、系统信息、显卡信息。
    /// </summary>
    Environment,

    /// <summary>
    ///     上下文信息。通常用于展示或辅助诊断，不应单独产生结论。
    /// </summary>
    Context
}