namespace PCL.Core.Minecraft.CrashAnalysis;

public static class CrashScore
{
    public static CrashDiagnosisConfidence ToConfidence(int score)
    {
        return score switch
        {
            >= 90 => CrashDiagnosisConfidence.Certain,
            >= 70 => CrashDiagnosisConfidence.High,
            >= 45 => CrashDiagnosisConfidence.Medium,
            _ => CrashDiagnosisConfidence.Low
        };
    }

    public static int Clamp(int score)
    {
        return score switch
        {
            < 0 => 0,
            > 100 => 100,
            _ => score
        };
    }
}