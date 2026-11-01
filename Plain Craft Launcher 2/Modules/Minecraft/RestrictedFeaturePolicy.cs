using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.App.Region;

namespace PCL;

internal static class RestrictedFeaturePolicy
{
    internal static RegionAssessment Assess() => RegionPolicy.Evaluate(RegionPolicy.ReadWindowsSignals());

    internal static bool IsRestricted => Assess().IsRestricted;

    internal static bool Check(string feature, bool showHint = true)
    {
        var assessment = Assess();
        if (!assessment.IsRestricted) return true;

        LauncherCommon.Log($"[RegionPolicy] 已阻止功能 {feature}，判定原因：{assessment.Reason}");
        if (showHint)
        {
            HintService.Show(Hints.Error(Lang.Text(assessment.Classification == RegionClassification.Unknown
                ? "Region.Feature.Unknown"
                : "Region.Feature.Restricted")));
        }
        return false;
    }

    internal static void ThrowIfRestricted(string feature)
    {
        if (Check(feature, false)) return;
        throw new CancelledException();
    }
}
