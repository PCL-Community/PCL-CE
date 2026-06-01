using System;

namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed class CrashAnalyzer(
    CrashLogCollector collector,
    CrashLogImporter importer,
    CrashLogPreparer preparer,
    CrashRuleEngine ruleEngine)
{
    public CrashAnalyzer()
        : this(new CrashLogCollector(), new CrashLogImporter(), new CrashLogPreparer(), new CrashRuleEngine())
    {
    }

    public CrashAnalysisReport Analyze(CrashAnalysisRequest request)
    {
        var rawLogs = request.Source switch
        {
            CrashAnalysisSource.LiveGame => CrashLogCollector.Collect(request),
            CrashAnalysisSource.ImportedFile => CrashLogImporter.Import(request),
            _ => throw new ArgumentOutOfRangeException(nameof(request.Source))
        };

        var preparedLogs = preparer.Prepare(rawLogs, request);
        var findings = CrashRuleEngine.Analyze(preparedLogs, request);

        return CrashAnalysisReport.Create(request, preparedLogs, findings);
    }
}