namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     表示事实本身的证据强度，用于诊断评分时区分“直接证据”和“弱信号”。
/// </summary>
public enum CrashFactStrength
{
    /// <summary>
    ///     弱信号。通常只能作为辅助信息，不能单独形成高置信度诊断。
    /// </summary>
    Weak,

    /// <summary>
    ///     中等信号。通常需要和其他事实组合后再形成诊断。
    /// </summary>
    Medium,

    /// <summary>
    ///     强信号。通常能够明显支持某一类诊断。
    /// </summary>
    Strong,

    /// <summary>
    ///     直接证据。通常来自日志明确报告的根因或错误对象。
    /// </summary>
    Direct
}