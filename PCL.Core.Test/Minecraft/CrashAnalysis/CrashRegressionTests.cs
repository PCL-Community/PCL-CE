using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.CrashAnalysis;

namespace PCL.Core.Test.Minecraft.CrashAnalysis;

[TestClass]
public sealed class CrashRegressionTests
{
    [TestMethod]
    public void ImportedZipWithOnlyCrashReportsUsesNewestAsPrimary()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-crash-import-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, "reports.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "crash-reports/crash-2026-06-06_12.00.00-client.txt",
                "Time: 2026-06-06 12:00:00\nDescription: newer");
            WriteEntry(archive, "crash-reports/crash-2026-06-05_12.00.00-client.txt",
                "Time: 2026-06-05 12:00:00\nDescription: older");
        }

        var bundle = new CrashInputReader().Read(new CrashAnalysisRequest
        {
            Source = CrashAnalysisSource.ImportedFile,
            ImportedFilePath = zipPath,
            TempDirectory = root
        });

        var newer = bundle.Documents.Single(document =>
            document.Name.Contains("2026-06-06", StringComparison.Ordinal));
        var older = bundle.Documents.Single(document =>
            document.Name.Contains("2026-06-05", StringComparison.Ordinal));
        Assert.AreEqual(CrashLogAnalysisRole.Primary, newer.AnalysisRole);
        Assert.AreEqual(CrashLogAnalysisRole.ReportOnly, older.AnalysisRole);
    }

    [TestMethod]
    public void BlockEntitySectionDoesNotProduceEntityFact()
    {
        var document = new CrashLogDocument
        {
            Kind = CrashLogKind.MinecraftCrashReport,
            Name = "crash-2026-06-06_12.00.00-client.txt",
            Origin = CrashLogOrigin.ImportedFile,
            AnalysisRole = CrashLogAnalysisRole.Primary,
            Text =
                "---- Minecraft Crash Report ----\n\n-- Block entity being ticked --\nDetails:\n\tName: minecraft:chest\n\tBlock location: World: (1,2,3)"
        };
        var facts = new CrashFactExtractor().Extract(
            new CrashLogBundle { Documents = [document] },
            new CrashAnalysisRequest());

        Assert.IsTrue(facts.Has(CrashFactKind.WorldBlockEntityIssueDetected));
        Assert.IsFalse(facts.Has(CrashFactKind.WorldEntityIssueDetected));
    }

    [TestMethod]
    public void GbkEncodedManualDebugCrashIsDecoded()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-crash-gbk-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var logPath = Path.Combine(root, "latest.log");
        File.WriteAllBytes(logPath,
        [
            0x44, 0x65, 0x73, 0x63, 0x72, 0x69, 0x70, 0x74, 0x69, 0x6F, 0x6E, 0x3A, 0x20,
            0x46, 0x33, 0x20, 0x2B, 0x20, 0x43, 0x20, 0xD2, 0xD1, 0xB1, 0xBB, 0xB0, 0xB4, 0xCF, 0xC2
        ]);

        var request = new CrashAnalysisRequest
        {
            Source = CrashAnalysisSource.ImportedFile,
            ImportedFilePath = logPath
        };
        var bundle = new CrashInputReader().Read(request);
        Assert.Contains("已被按下", bundle.Documents.Single().Text);

        var result = new CrashAnalyzer().Analyze(request);

        Assert.IsTrue(result.Facts.Has(CrashFactKind.ManualDebugCrashDetected));
    }

    [TestMethod]
    public void DiagnosisJsonDoesNotExportHiddenFactsOrSensitiveValues()
    {
        var result = new CrashAnalyzer().Analyze(new CrashAnalysisRequest
        {
            Source = CrashAnalysisSource.LiveGame,
            CapturedOutputLines = ["Manually triggered debug crash"],
            RuntimeContext = new CrashRuntimeContext
            {
                AccountName = "SensitivePlayer",
                InstancePath = @"C:\\Users\\Alice\\.minecraft",
                JavaPath = @"C:\\Users\\Alice\\java\\bin\\javaw.exe",
                LauncherId = "launcher-secret-id",
                InstanceName = "PrivateInstance",
                LaunchArguments = ["--accessToken secret-token", "--uuid secret-uuid"]
            }
        });

        var package = CrashReportBuilder.Build(result, new CrashReportBuildOptions());
        var json = Encoding.UTF8.GetString(package.Entries.Single(entry =>
            entry.FileName == "diagnosis.json").Content);

        Assert.IsFalse(json.Contains("secret-token", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("secret-uuid", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("SensitivePlayer", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("PrivateInstance", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("Hidden", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void NativeProblematicFramePrefersHsErrSource()
    {
        const string frame = "# C  [nvoglv64.dll+0x1234]";
        var latest = new CrashLogDocument
        {
            Kind = CrashLogKind.MinecraftLatestLog,
            Name = "latest.log",
            Origin = CrashLogOrigin.ImportedFile,
            AnalysisRole = CrashLogAnalysisRole.Primary,
            Text = "problematic frame:\n" + frame
        };
        var hsErr = new CrashLogDocument
        {
            Kind = CrashLogKind.JavaFatalErrorLog,
            Name = "hs_err_pid123.log",
            Origin = CrashLogOrigin.ImportedFile,
            AnalysisRole = CrashLogAnalysisRole.Primary,
            Text = "problematic frame:\n" + frame
        };

        var facts = new CrashFactExtractor().Extract(
            new CrashLogBundle { Documents = [latest, hsErr] },
            new CrashAnalysisRequest());
        var nativeFrame = facts.First(CrashFactKind.NativeProblematicFrameDetected);

        Assert.IsNotNull(nativeFrame);
        Assert.AreEqual(CrashLogKind.JavaFatalErrorLog, nativeFrame!.Evidence[0].SourceKind);
    }

    [TestMethod]
    public void ExportedReportKeepsDuplicateArchiveEntryNamesDistinct()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-crash-duplicate-log-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, "logs.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "logs/latest.log", "first");
            WriteEntry(archive, "nested/latest.log", "second");
        }

        var result = new CrashAnalyzer().Analyze(new CrashAnalysisRequest
        {
            Source = CrashAnalysisSource.ImportedFile,
            ImportedFilePath = zipPath,
            TempDirectory = root
        });
        var package = CrashReportBuilder.Build(result, new CrashReportBuildOptions());
        var logNames = package.Entries
            .Where(entry => entry.FileName.StartsWith("logs/", StringComparison.Ordinal))
            .Select(entry => entry.FileName)
            .ToList();

        Assert.AreEqual(logNames.Count, logNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }


    [TestMethod]
    public void LoaderConflictProducesModSetConflictDiagnosis()
    {
        var result = new CrashAnalyzer().Analyze(new CrashAnalysisRequest
        {
            Source = CrashAnalysisSource.LiveGame,
            CapturedOutputLines = ["Mod alpha conflicts with mod beta and cannot be loaded together"]
        });

        Assert.Contains(static diagnosis =>
            diagnosis.Code == CrashDiagnosisCode.ModSetConflict, result.Diagnoses);
        Assert.IsTrue(result.Facts.Has(CrashFactKind.ModSetConflictDetected));
    }

    [TestMethod]
    public void FabricNegativeHardDependencyBreaksIsModSetConflictNotMissingDependency()
    {
        var result = new CrashAnalyzer().Analyze(new CrashAnalysisRequest
        {
            Source = CrashAnalysisSource.LiveGame,
            CapturedOutputLines =
            [
                "[01:33:20] [main/INFO]: Loading Minecraft 26.1.2 with Fabric Loader 0.19.3",
                "[01:33:20] [main/WARN]: Mod resolution failed",
                "[01:33:20] [main/INFO]: Immediate reason: [NEG_HARD_DEP wi_zoom 1.7-MC26.1.2 {breaks wurst @ [*]}, ROOT_FORCELOAD_SINGLE wi_zoom 1.7-MC26.1.2, ROOT_FORCELOAD_SINGLE wurst 7.54-MC26.1.2]",
                "[01:33:20] [main/INFO]: Reason: [NEG_HARD_DEP wi_zoom 1.7-MC26.1.2 {breaks wurst @ [*]}, NEG_HARD_DEP wurst 7.54-MC26.1.2 {breaks wi_zoom @ [*]}]",
                "[01:33:20] [main/INFO]: Fix: add [], remove [], replace [[wi_zoom 1.7-MC26.1.2] -> add:wi_zoom 1 ([(-∞,∞)]), [wurst 7.54-MC26.1.2] -> add:wurst 1 ([(-∞,∞)])]",
                "[01:33:20] [main/ERROR]: Incompatible mods found!",
                "net.fabricmc.loader.impl.FormattedException: Some of your mods are incompatible with the game or each other!",
                "A potential solution has been determined, this may resolve your problem:",
                "\t - Replace mod 'Wurst Client' (wurst) 7.54-MC26.1.2 with any version that is compatible with:",
                "\t\t - wi_zoom, any version",
                "\t - Replace mod 'WI Zoom' (wi_zoom) 1.7-MC26.1.2 with any version that is compatible with:",
                "\t\t - wurst, any version",
                "More details:",
                "\t - Mod 'WI Zoom' (wi_zoom) 1.7-MC26.1.2 is incompatible with any version of mod 'Wurst Client' (wurst), yet a conflicting version is present: 7.54-MC26.1.2!",
                "\t - Mod 'Wurst Client' (wurst) 7.54-MC26.1.2 is incompatible with any version of mod 'WI Zoom' (wi_zoom), yet a conflicting version is present: 1.7-MC26.1.2!"
            ]
        });

        Assert.AreEqual(CrashDiagnosisCode.ModSetConflict, result.TopDiagnosis?.Code,
            string.Join("; ", result.Diagnoses.Select(static diagnosis => diagnosis.Code)));
        Assert.IsTrue(result.Facts.Has(CrashFactKind.ModSetConflictDetected));
        Assert.IsFalse(result.Facts.Has(CrashFactKind.MissingModDependencyDetected));
        Assert.IsFalse(result.Facts.Has(CrashFactKind.LoaderVersionRequirementDetected));
        Assert.DoesNotContain(static diagnosis =>
            diagnosis.Code is CrashDiagnosisCode.LoaderDependencyMissing
                or CrashDiagnosisCode.LoaderDependencyVersionConflict
                or CrashDiagnosisCode.LoaderVersionIncompatible, result.Diagnoses);
    }

    [TestMethod]
    public void FabricModToModConflictArchiveKeepsModSetConflictAsTopDiagnosis()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-crash-fabric-conflict-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, "fabric-conflict.zip");
        var log = string.Join("\n", "[02:11:11] [main/INFO]: Loading Minecraft 26.1.2 with Fabric Loader 0.19.3",
            "[02:11:11] [main/WARN]: Mod resolution failed",
            "[02:11:11] [main/INFO]: Immediate reason: [NEG_HARD_DEP wi_zoom 1.7-MC26.1.2 {breaks wurst @ [*]}, ROOT_FORCELOAD_SINGLE wi_zoom 1.7-MC26.1.2, ROOT_FORCELOAD_SINGLE wurst 7.54-MC26.1.2]",
            "[02:11:11] [main/INFO]: Reason: [NEG_HARD_DEP wi_zoom 1.7-MC26.1.2 {breaks wurst @ [*]}, NEG_HARD_DEP wurst 7.54-MC26.1.2 {breaks wi_zoom @ [*]}]",
            "[02:11:11] [main/INFO]: Fix: add [], remove [], replace [[wurst 7.54-MC26.1.2] -> add:wurst 1 ([(-∞,∞)]), [wi_zoom 1.7-MC26.1.2] -> add:wi_zoom 1 ([(-∞,∞)])]",
            "[02:11:11] [main/ERROR]: Incompatible mods found!",
            "net.fabricmc.loader.impl.FormattedException: Some of your mods are incompatible with the game or each other!",
            "A potential solution has been determined, this may resolve your problem:",
            "\t - Replace mod 'Wurst Client' (wurst) 7.54-MC26.1.2 with any version that is compatible with:",
            "\t\t - wi_zoom, any version",
            "\t - Replace mod 'WI Zoom' (wi_zoom) 1.7-MC26.1.2 with any version that is compatible with:",
            "\t\t - wurst, any version", "More details:",
            "\t - Mod 'WI Zoom' (wi_zoom) 1.7-MC26.1.2 is incompatible with any version of mod 'Wurst Client' (wurst), yet a conflicting version is present: 7.54-MC26.1.2!",
            "\t - Mod 'Wurst Client' (wurst) 7.54-MC26.1.2 is incompatible with any version of mod 'WI Zoom' (wi_zoom), yet a conflicting version is present: 1.7-MC26.1.2!");

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "logs/captured-output.log", log);
            WriteEntry(archive, "logs/latest.log", log);
            WriteEntry(archive, "logs/debug.log", log);
        }

        var result = new CrashAnalyzer().Analyze(new CrashAnalysisRequest
        {
            Source = CrashAnalysisSource.ImportedFile,
            ImportedFilePath = zipPath,
            TempDirectory = root
        });

        Assert.AreEqual(CrashDiagnosisCode.ModSetConflict, result.TopDiagnosis?.Code,
            string.Join("; ", result.Diagnoses.Select(static diagnosis => diagnosis.Code)));
        Assert.IsTrue(result.Facts.Has(CrashFactKind.ModSetConflictDetected));
        Assert.IsFalse(result.Facts.Has(CrashFactKind.MissingModDependencyDetected));
        Assert.DoesNotContain(static diagnosis =>
            diagnosis.Code == CrashDiagnosisCode.LoaderVersionIncompatible, result.Diagnoses);
    }


    [TestMethod]
    public void CrashReportEmitsReportedExceptionAndModListFacts()
    {
        var document = new CrashLogDocument
        {
            Kind = CrashLogKind.MinecraftCrashReport,
            Name = "crash-2026-06-06_12.30.00-client.txt",
            Origin = CrashLogOrigin.ImportedFile,
            AnalysisRole = CrashLogAnalysisRole.Primary,
            Text =
                "---- Minecraft Crash Report ----\n" +
                "Description: Exception ticking world\n\n" +
                "net.minecraft.ReportedException: Ticking block entity\n" +
                "\tat net.minecraft.CrashReport.fake(CrashReport.java:1)\n\n" +
                "-- System Details --\n" +
                "Details:\n" +
                "\tMinecraft Version: 1.20.1\n" +
                "\tMod List: examplemod-1.0.jar, librarymod-2.0.jar"
        };

        var facts = new CrashFactExtractor().Extract(
            new CrashLogBundle { Documents = [document] },
            new CrashAnalysisRequest());

        Assert.IsTrue(facts.Has(CrashFactKind.MinecraftReportedException));
        Assert.IsTrue(facts.Has(CrashFactKind.ModListDetected));
    }

    [TestMethod]
    public void ExitCodeLineEmitsExitCodeFact()
    {
        var result = new CrashAnalyzer().Analyze(new CrashAnalysisRequest
        {
            Source = CrashAnalysisSource.LiveGame,
            CapturedOutputLines = ["Process crashed with exit code -1"]
        });

        Assert.IsTrue(result.Facts.Has(CrashFactKind.MinecraftExitCodeDetected));
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        entry.LastWriteTime = DateTimeOffset.Parse("2026-06-06T12:00:00Z");
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }
}