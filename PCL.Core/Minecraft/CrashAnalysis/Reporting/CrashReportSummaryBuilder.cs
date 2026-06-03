namespace PCL.Core.Minecraft.CrashAnalysis;

public static class CrashReportSummaryBuilder
{
    public static string Build(CrashAnalysisResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Crash diagnosis summary");
        builder.AppendLine("CreatedAt: " + result.CreatedAt.ToString("O"));
        foreach (var diagnosis in result.Diagnoses)
            builder.AppendLine("- " + diagnosis.Code + " / " + diagnosis.Confidence + " / " + diagnosis.Score);
        return builder.ToString();
    }
}