namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     日志在本次崩溃分析中的角色。Primary / Supporting 会参与诊断，ReportOnly 只导出和展示。
/// </summary>
public enum CrashLogAnalysisRole
{
    Primary,
    Supporting,
    ReportOnly
}