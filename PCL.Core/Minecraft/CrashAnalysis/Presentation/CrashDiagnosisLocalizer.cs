namespace PCL.Core.Minecraft.CrashAnalysis;

public static class CrashDiagnosisLocalizer
{
    public static string TitleKey(CrashDiagnosisCode code)
    {
        return "Crash.Diagnosis.Title." + code;
    }

    public static string DescriptionKey(CrashDiagnosisCode code)
    {
        return "Crash.Diagnosis.Description." + code;
    }

    public static string CauseKey(CrashDiagnosisCode code)
    {
        return "Crash.Diagnosis.Cause." + code;
    }

    public static string ImpactKey(CrashDiagnosisCode code)
    {
        return "Crash.Diagnosis.Impact." + code;
    }

    public static string RecommendationKey(CrashDiagnosisCode code)
    {
        return "Crash.Diagnosis.Recommendation." + code;
    }

    public static string ActionTitleKey(CrashPresentationActionKind kind)
    {
        return "Crash.Action." + kind;
    }

    public static string ActionDescriptionKey(CrashPresentationActionKind kind)
    {
        return "Crash.Action.Description." + kind;
    }

    public static string EvidenceKey(CrashFactKind kind)
    {
        return "Crash.Evidence." + kind;
    }
}