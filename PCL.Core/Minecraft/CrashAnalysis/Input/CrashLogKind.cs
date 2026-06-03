namespace PCL.Core.Minecraft.CrashAnalysis;

public enum CrashLogKind
{
    Unknown,
    CapturedGameOutput,
    MinecraftLatestLog,
    MinecraftDebugLog,
    MinecraftCrashReport,
    JavaFatalErrorLog,
    LauncherLog,
    LaunchScript,
    EnvironmentSnapshot,
    ImportedText
}

public enum CrashLogOrigin
{
    FileSystem,
    ImportedArchive,
    ImportedFile,
    CapturedOutput,
    Generated
}