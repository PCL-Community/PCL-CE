using PCL.Core.App.Region;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL;

internal static class RestrictedFeaturePolicy
{
    /// <summary>
    ///     是否启用地区功能限制。
    ///     <para>
    ///         上游 #3673（8cd9d374）为中国大陆地区禁用了 Minecraft 下载、补全与整合包安装功能。
    ///         本构建将其关闭，以恢复此前版本的可用行为；如需恢复上游行为，把这里改为 <see langword="true" /> 即可。
    ///         关闭后启动流程也会重新执行“补全文件”阶段（<see cref="IsRestricted" /> 恒为 <see langword="false" />）。
    ///     </para>
    /// </summary>
    private static readonly bool BlockingEnabled = false;

    internal static RegionAssessment Assess() => RegionPolicy.Evaluate(RegionPolicy.ReadWindowsSignals());

    internal static bool IsRestricted => BlockingEnabled && Assess().IsRestricted;

    internal static bool Check(string feature, bool showHint = true)
    {
        if (!BlockingEnabled) return true;

        var assessment = Assess();
        if (!assessment.IsRestricted) return true;

        ModBase.Log($"[RegionPolicy] 已阻止功能 {feature}，判定原因：{assessment.Reason}");
        if (showHint)
        {
            HintService.Hint(Lang.Text(assessment.Classification == RegionClassification.Unknown
                ? "Region.Feature.Unknown"
                : "Region.Feature.Restricted"), HintType.Error);
        }
        return false;
    }

    internal static void ThrowIfRestricted(string feature)
    {
        if (Check(feature, false)) return;
        throw new ModBase.CancelledException();
    }
}
