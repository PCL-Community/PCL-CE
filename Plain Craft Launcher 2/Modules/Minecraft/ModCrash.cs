using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.Utils.Codecs;
using PCL.Core.Utils.Exts;

namespace PCL;

public class CrashAnalyzer
{
    // 1：准备用于分析的 Log 文件
    private readonly List<KeyValuePair<string, string[]>> AnalyzeRawFiles = new(); // 暂存的日志文件：文件完整路径 -> 文件内容

    // 可能导致崩溃的原因与附加信息
    private readonly Dictionary<CrashReason, List<string>> CrashReasons = new();

    // 4：根据原因输出信息
    private readonly List<string> OutputFiles = new();

    // 构造函数
    private readonly string TempFolder;

    // 暂存分析的实例供特殊用途
    // 龙猫味石山代码小记: CrashAnalyze 猛一顿分析不知道自己在分析啥实例
    private ModMinecraft.McInstance _version;
    private KeyValuePair<string, string[]>? DirectFile; // 在弹窗中选择直接打开的文件
    private string LogAll;
    private string LogCrash;
    private string LogHs;

    // 3：根据文本分析崩溃原因
    private string LogMc;
    private string LogMcDebug;

    public CrashAnalyzer(int UUID)
    {
        // 构建文件结构
        TempFolder = ModMain.RequestTaskTempFolder();
        Directory.CreateDirectory(TempFolder + @"Temp\");
        Directory.CreateDirectory(TempFolder + @"Report\");
        ModBase.Log("[Crash] 崩溃分析暂存文件夹：" + TempFolder);
    }

    /// <summary>
    ///     将可用于分析的日志存储到 AnalyzeRawFiles。
    /// </summary>
    /// <param name="LatestLog">从 PCL 捕获到的最后 200 行程序输出。</param>
    public void Collect(string VersionPathIndie, IList<string> LatestLog = null)
    {
        ModBase.Log("[Crash] 步骤 1：收集日志文件");

        // 简单收集可能的日志文件路径
        var PossibleLogs = new List<string>();
        try
        {
            var DirInfo = new DirectoryInfo(VersionPathIndie + @"crash-reports\");
            if (DirInfo.Exists)
                foreach (var File in DirInfo.EnumerateFiles())
                    PossibleLogs.Add(File.FullName);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "收集 Minecraft 崩溃日志文件夹下的日志失败");
        }

        try
        {
            foreach (var File in new DirectoryInfo(VersionPathIndie).Parent.Parent.EnumerateFiles())
            {
                if ((File.Extension ?? "") != ".log")
                    continue;
                PossibleLogs.Add(File.FullName);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "收集 Minecraft 主文件夹下的日志失败");
        }

        try
        {
            foreach (var File in new DirectoryInfo(VersionPathIndie).EnumerateFiles())
            {
                if ((File.Extension ?? "") != ".log")
                    continue;
                PossibleLogs.Add(File.FullName);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "收集 Minecraft 隔离文件夹下的日志失败");
        }

        PossibleLogs.Add(VersionPathIndie + @"logs\latest.log"); // Minecraft 日志
        var LaunchScript = ModBase.ReadFile(ModBase.ExePath + @"PCL\LatestLaunch.bat");
        if (LaunchScript.ContainsF("-Dlog4j2.formatMsgNoLookups=false"))
            PossibleLogs.Add(VersionPathIndie + @"logs\debug.log"); // Minecraft Debug 日志
        PossibleLogs = PossibleLogs.Distinct().ToList();

        // 确定最新的日志文件
        var RightLogs = new List<string>();
        foreach (var LogFile in PossibleLogs)
            try
            {
                var Info = new FileInfo(LogFile);
                if (!Info.Exists)
                    continue;
                var Time = Math.Abs((Info.LastWriteTime - DateTime.Now).TotalMinutes);
                if (Time < 3d && Info.Length > 0L)
                {
                    RightLogs.Add(LogFile);
                    ModBase.Log("[Crash] 可能可用的日志文件：" + LogFile + "（" + Math.Round(Time, 1) + " 分钟）");
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "确认崩溃日志时间失败（" + LogFile + "）");
            }

        if (!RightLogs.Any())
            ModBase.Log("[Crash] 未发现可能可用的日志文件");

        // 将可能可用的日志文件导出
        foreach (var FilePath in RightLogs)
            try
            {
                AnalyzeRawFiles.Add(new KeyValuePair<string, string[]>(FilePath,
                    ModBase.ReadFile(FilePath).Split("\r\n".ToCharArray())));
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "读取可能的崩溃日志文件失败（" + FilePath + "）");
            }

        if (LatestLog is not null && LatestLog.Any())
        {
            var RawOutput = LatestLog.Join("\r\n");
            ModBase.Log("[Crash] 以下为游戏输出的最后一段内容：" + "\r\n" + RawOutput);
            ModBase.WriteFile(TempFolder + "RawOutput.log", RawOutput);
            AnalyzeRawFiles.Add(new KeyValuePair<string, string[]>(TempFolder + "RawOutput.log", LatestLog.ToArray()));
            LatestLog.Clear();
        }

        ModBase.Log("[Crash] 步骤 1：收集日志文件完成，收集到 " + AnalyzeRawFiles.Count + " 个文件");
    }

    /// <summary>
    ///     从文件路径直接导入日志文件或崩溃报告压缩包。
    /// </summary>
    public void Import(string FilePath)
    {
        ModBase.Log("[Crash] 步骤 1：自主导入日志文件");

        // 尝试视作压缩包解压
        try
        {
            var Info = new FileInfo(FilePath);
            if (Info.Exists && Info.Length > 0L && !FilePath.EndsWithF(".jar", true))
            {
                ModBase.ExtractFile(FilePath, TempFolder + @"Temp\");
                ModBase.Log("[Crash] 已解压导入的日志文件：" + FilePath);
                goto Extracted;
            }
        }
        catch
        {
        }

        // 并非压缩包
        ModBase.CopyFile(FilePath, TempFolder + @"Temp\" + ModBase.GetFileNameFromPath(FilePath));
        ModBase.Log("[Crash] 已复制导入的日志文件：" + FilePath);
        Extracted: ;


        // 导入其中的日志文件
        foreach (var TargetFile in new DirectoryInfo(TempFolder + @"Temp\").EnumerateFiles().ToList())
            try
            {
                if (!TargetFile.Exists || TargetFile.Length == 0L)
                    continue;
                var Ext = TargetFile.Extension.ToLower();
                if (Ext == ".log" || Ext == ".txt")
                    AnalyzeRawFiles.Add(new KeyValuePair<string, string[]>(TargetFile.FullName,
                        ModBase.ReadFile(TargetFile.FullName).Split("\r\n".ToCharArray())));
                else
                    File.Delete(TargetFile.FullName);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "导入单个日志文件失败");
            }

        ModBase.Log("[Crash] 步骤 1：自主导入日志文件，收集到 " + AnalyzeRawFiles.Count + " 个文件");
    }

    /// <summary>
    ///     从 AnalyzeRawFiles 中提取实际有用的文本片段存储到 AnalyzeFiles，并整理可用于生成报告的文件。
    ///     返回是否有足够信息可用于分析。
    /// </summary>
    public bool Prepare()
    {
        bool PrepareRet = default;
        ModBase.Log("[Crash] 步骤 2：准备日志文本");

        // 对日志文件进行分类
        DirectFile = default;
        var AllFiles = new List<KeyValuePair<AnalyzeFileType, KeyValuePair<string, string[]>>>();
        foreach (var LogFile in AnalyzeRawFiles)
        {
            var MatchName = ModBase.GetFileNameFromPath(LogFile.Key).ToLower();
            AnalyzeFileType TargetType;
            if (MatchName.StartsWithF("hs_err"))
            {
                TargetType = AnalyzeFileType.HsErr;
                DirectFile = LogFile;
            }
            else if (MatchName.StartsWithF("crash-"))
            {
                TargetType = AnalyzeFileType.CrashReport;
                DirectFile = LogFile;
            }
            else if (MatchName == "latest.log" || MatchName == "latest log.txt" || MatchName == "debug.log" ||
                     MatchName == "debug log.txt" || MatchName == "游戏崩溃前的输出.txt" || MatchName == "rawoutput.log")
            {
                TargetType = AnalyzeFileType.MinecraftLog;
                if (DirectFile is null)
                    DirectFile = LogFile;
            }
            else if (MatchName == "启动器日志.txt" || MatchName == "PCL2 启动器日志.txt" || MatchName == "PCL 启动器日志.txt" ||
                     MatchName == "log1.txt" || MatchName == "log-ce1.log")
            {
                if (LogFile.Value.Any(s => s.Contains("以下为游戏输出的最后一段内容")))
                {
                    TargetType = AnalyzeFileType.MinecraftLog;
                    if (DirectFile is null)
                        DirectFile = LogFile;
                }
                else
                {
                    TargetType = AnalyzeFileType.ExtraLogFile;
                }
            }
            else if (MatchName.EndsWithF(".log", true))
            {
                TargetType = AnalyzeFileType.ExtraLogFile;
            }
            else if (MatchName.EndsWithF(".txt", true))
            {
                TargetType = AnalyzeFileType.ExtraReportFile;
            }
            else
            {
                ModBase.Log("[Crash] " + MatchName + " 分类为 Ignore");
                continue;
            }

            if (LogFile.Value.Any())
            {
                AllFiles.Add(new KeyValuePair<AnalyzeFileType, KeyValuePair<string, string[]>>(TargetType, LogFile));
                ModBase.Log("[Crash] " + MatchName + " 分类为 " + ModBase.GetStringFromEnum(TargetType));
            }
            else
            {
                ModBase.Log("[Crash] " + MatchName + " 由于内容为空跳过");
            }
        }

        // 若只有额外日志，则将它们视作 Minecraft 日志
        if (AllFiles.Any() && AllFiles.All(p => p.Key == AnalyzeFileType.ExtraLogFile))
        {
            ModBase.Log("[Crash] 由于仅发现了额外日志，将它们视作 Minecraft 日志进行分析");
            AllFiles = AllFiles.Select(p =>
                new KeyValuePair<AnalyzeFileType, KeyValuePair<string, string[]>>(AnalyzeFileType.MinecraftLog,
                    p.Value)).ToList();
        }

        // 将分类后的文件分别写入
        foreach (var SelectType in new[]
                 {
                     AnalyzeFileType.MinecraftLog, AnalyzeFileType.HsErr, AnalyzeFileType.ExtraLogFile,
                     AnalyzeFileType.CrashReport
                 })
        {
            // 获取该种类的所有文件 {文件路径 -> 文件内容行}
            var SelectedFiles = new List<KeyValuePair<string, string[]>>();
            foreach (var File in AllFiles)
                if (SelectType == File.Key)
                    SelectedFiles.Add(File.Value);
            if (!SelectedFiles.Any())
                continue;
            try
            {
                // 根据文件类别判断
                switch (SelectType)
                {
                    case AnalyzeFileType.HsErr:
                    case AnalyzeFileType.CrashReport:
                    {
                        // 获取文件的修改日期
                        var DatedFiles = new SortedList<DateTime, KeyValuePair<string, string[]>>();
                        foreach (var File in SelectedFiles)
                            try
                            {
                                DatedFiles.Add(new FileInfo(File.Key).LastWriteTime, File);
                            }
                            catch (Exception ex)
                            {
                                ModBase.Log(ex, "获取日志文件修改时间失败");
                                DatedFiles.Add(new DateTime(1900, 1, 1), File);
                            }

                        // 输出最新的文件
                        var NewestFile = DatedFiles.Last().Value;
                        OutputFiles.Add(NewestFile.Key);
                        if (SelectType == AnalyzeFileType.HsErr)
                        {
                            LogHs = GetHeadTailLines(NewestFile.Value, 200, 100);
                            ModBase.Log("[Crash] 输出报告：" + NewestFile.Key + "，作为虚拟机错误信息");
                            ModBase.Log("[Crash] 导入分析：" + NewestFile.Key + "，作为虚拟机错误信息");
                        }
                        else
                        {
                            LogCrash = GetHeadTailLines(NewestFile.Value, 300, 700);
                            ModBase.Log("[Crash] 输出报告：" + NewestFile.Key + "，作为 Minecraft 崩溃报告");
                            ModBase.Log("[Crash] 导入分析：" + NewestFile.Key + "，作为 Minecraft 崩溃报告");
                        }

                        break;
                    }
                    case AnalyzeFileType.MinecraftLog:
                    {
                        LogMc = "";
                        LogMcDebug = "";
                        // 创建文件名词典
                        var FileNameDict = new Dictionary<string, KeyValuePair<string, string[]>>();
                        foreach (var SelectedFile in SelectedFiles)
                        {
                            FileNameDict[ModBase.GetFileNameFromPath(SelectedFile.Key).ToLower()] = SelectedFile;
                            OutputFiles.Add(SelectedFile.Key);
                            ModBase.Log("[Crash] 输出报告：" + SelectedFile.Key + "，作为 Minecraft 或启动器日志");
                        }

                        // 选择一份最佳的来自启动器的游戏日志
                        foreach (var FileName in new[]
                                 {
                                     "rawoutput.log", "启动器日志.txt", "log1.txt", "log-ce1.log", "游戏崩溃前的输出.txt",
                                     "PCL2 启动器日志.txt", "PCL 启动器日志.txt"
                                 })
                        {
                            if (!FileNameDict.ContainsKey(FileName))
                                continue;
                            var CurrentLog = FileNameDict[FileName];
                            // 截取 “以下为游戏输出的最后一段内容” 后的内容
                            var HasLauncherMark = false;
                            foreach (var Line in CurrentLog.Value)
                                if (HasLauncherMark)
                                {
                                    LogMc += Line + "\n";
                                }
                                else if (Line.Contains("以下为游戏输出的最后一段内容"))
                                {
                                    HasLauncherMark = true;
                                    ModBase.Log("[Crash] 找到 PCL 输出的游戏实时日志头");
                                }

                            // 导入后 500 行
                            if (!HasLauncherMark)
                                LogMc += GetHeadTailLines(CurrentLog.Value, 0, 500);
                            LogMc = LogMc.TrimEnd("\r\n".ToCharArray());
                            ModBase.Log("[Crash] 导入分析：" + CurrentLog.Key + "，作为启动器日志");
                            break;
                        }

                        // 选择一份最佳的 Minecraft Log
                        foreach (var FileName in new[] { "latest.log", "latest log.txt", "debug.log", "debug log.txt" })
                        {
                            if (!FileNameDict.ContainsKey(FileName))
                                continue;
                            var CurrentLog = FileNameDict[FileName];
                            LogMc += GetHeadTailLines(CurrentLog.Value, 1500, 500);
                            ModBase.Log("[Crash] 导入分析：" + CurrentLog.Key + "，作为 Minecraft 日志");
                            break;
                        }

                        // 查找 Debug Log
                        foreach (var FileName in new[] { "debug.log", "debug log.txt" })
                        {
                            if (!FileNameDict.ContainsKey(FileName))
                                continue;
                            var CurrentLog = FileNameDict[FileName];
                            LogMcDebug += GetHeadTailLines(CurrentLog.Value, 1000, 0);
                            ModBase.Log("[Crash] 导入分析：" + CurrentLog.Key + "，作为 Minecraft Debug 日志");
                            break;
                        }

                        // 兜底
                        if (string.IsNullOrEmpty(LogMc))
                        {
                            if (!string.IsNullOrEmpty(LogMcDebug)) // 如果没有找到 Minecraft 日志，则使用 Debug 日志作为兜底
                            {
                                LogMc = LogMcDebug;
                            }
                            else if (FileNameDict.Any()) // 如果都没有找到，则使用第一个文件
                            {
                                var CurrentLog = FileNameDict.First().Value;
                                LogMc += GetHeadTailLines(CurrentLog.Value, 1500, 500);
                                ModBase.Log("[Crash] 导入分析：" + CurrentLog.Key + "，作为兜底日志");
                            }
                            else
                            {
                                LogMc = null;
                                throw new Exception("无法找到匹配的 Minecraft Log");
                            }
                        }

                        if (string.IsNullOrEmpty(LogMcDebug))
                            LogMcDebug = null;
                        break;
                    }
                    case AnalyzeFileType.ExtraLogFile:
                    case AnalyzeFileType.ExtraReportFile:
                    {
                        // 全部丢过去
                        foreach (var SelectedFile in SelectedFiles)
                        {
                            OutputFiles.Add(SelectedFile.Key);
                            ModBase.Log("[Crash] 输出报告：" + SelectedFile.Key + "，不用作分析");
                        }

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "分类处理日志文件时出错");
            }
        }

        // 结束
        PrepareRet = LogMc is not null || LogHs is not null || LogCrash is not null;
        if (PrepareRet)
            ModBase.Log(("[Crash] 步骤 2：准备日志文本完成，找到" + (LogMc is null ? "" : "游戏日志、") +
                         (LogMcDebug is null ? "" : "游戏 Debug 日志、") + (LogHs is null ? "" : "虚拟机日志、") +
                         (LogCrash is null ? "" : "崩溃日志、")).TrimEnd('、') + "用作分析");
        else
            ModBase.Log("[Crash] 步骤 2：准备日志文本完成，没有任何可供分析的日志");

        return PrepareRet;
    }

    /// <summary>
    ///     输出字符串的前后某些行，并统一行尾为 vbLf (正则 \n)、删除空行和重复行。
    /// </summary>
    private string GetHeadTailLines(string[] Raw, int HeadLines, int TailLines)
    {
        if (Raw.Length <= HeadLines + TailLines)
            return Raw.Distinct().Join("\n");
        var Lines = new List<string>();
        var RealHeadLines = 0;
        int ViewedLines;
        var loopTo = Raw.Length - 1;
        for (ViewedLines = 0; ViewedLines <= loopTo; ViewedLines++)
        {
            if (Lines.Contains(Raw[ViewedLines]))
                continue;
            RealHeadLines += 1;
            Lines.Add(Raw[ViewedLines]);
            if (RealHeadLines >= HeadLines)
                break;
        }

        var RealTailLines = 0;
        for (int i = Raw.Length - 1, loopTo1 = ViewedLines; i >= loopTo1; i -= 1)
        {
            if (Lines.Contains(Raw[i]))
                continue;
            RealTailLines += 1;
            Lines.Insert(RealHeadLines, Raw[i]);
            if (RealTailLines >= TailLines)
                break;
        }

        var Result = new StringBuilder();
        foreach (var Line in Lines)
        {
            if (string.IsNullOrEmpty(Line))
                continue;
            Result.Append(Line);
            Result.Append("\n");
        }

        return Result.ToString();
    }

    /// <summary>
    ///     根据 AnalyzeLogs 与可能的实例信息分析崩溃原因。
    /// </summary>
    public void Analyze(ModMinecraft.McInstance version = null)
    {
        _version = version;
        ModBase.Log("[Crash] 步骤 3：分析崩溃原因");
        LogAll = (LogMc ?? LogMcDebug ?? "") + (LogHs ?? "") + (LogCrash ?? "");

        // 处理 Quilt Mod Table 以避免错误分析 (CE #107)
        if (LogAll.Contains("quilt") && LogAll.Contains("Mod Table Version"))
        {
            ModBase.Log("[Crash] 处理 Quilt Mod Table 后再继续分析");
            var beforeTable = LogAll.BeforeFirst("| Index");
            var afterTable = LogAll.AfterFirst("Mod Table Version:");
            LogAll = beforeTable + afterTable;
        }

        // 1. 精准日志匹配，中/高优先级
        AnalyzeCrit1();
        if (CrashReasons.Any())
            goto Done;
        AnalyzeCrit2();
        if (CrashReasons.Any())
            goto Done;

        // 2. 堆栈分析
        if (LogAll.Contains("orge") || LogAll.Contains("abric") || LogAll.Contains("uilt") ||
            LogAll.Contains("iteloader"))
        {
            var Keywords = new List<string>();
            // 崩溃日志
            if (LogCrash is not null)
            {
                ModBase.Log("[Crash] 开始进行崩溃日志堆栈分析");
                Keywords.AddRange(AnalyzeStackKeyword(LogCrash.BeforeFirst("System Details")));
            }

            // Minecraft 日志
            if (LogMc is not null)
            {
                var Fatals = LogMc.RegexSearch(@"/FATAL] .+?(?=[\n]+\[)");
                if (LogMc.Contains("Unreported exception thrown!"))
                    Fatals.Add(LogMc.Between("Unreported exception thrown!", "at oolloo.jlw.Wrapper"));
                ModBase.Log("[Crash] 开始进行 Minecraft 日志堆栈分析，发现 " + Fatals.Count + " 个报错项");
                foreach (var Fatal in Fatals)
                    Keywords.AddRange(AnalyzeStackKeyword(Fatal));
            }

            // 虚拟机日志
            if (LogHs is not null)
            {
                ModBase.Log("[Crash] 开始进行虚拟机堆栈分析");
                var StackLogs = LogHs.Between("T H R E A D", "Registers:");
                Keywords.AddRange(AnalyzeStackKeyword(StackLogs));
            }

            // Mod 名称分析
            if (Keywords.Any())
            {
                var Names = AnalyzeModName(Keywords);
                if (Names is null)
                    AppendReason(CrashReason.堆栈分析发现关键字, Keywords);
                else
                    AppendReason(CrashReason.堆栈分析发现Mod名称, Names);
                goto Done;
            }
        }
        else
        {
            ModBase.Log("[Crash] 可能并未安装 Mod，不进行堆栈分析");
        }

        // 3. 精准日志匹配，低优先级
        AnalyzeCrit3();

        // 输出到日志
        Done: ;

        if (!CrashReasons.Any())
        {
            ModBase.Log("[Crash] 步骤 3：分析崩溃原因完成，未找到可能的原因");
        }
        else
        {
            ModBase.Log("[Crash] 步骤 3：分析崩溃原因完成，找到 " + CrashReasons.Count + " 条可能的原因");
            foreach (var Reason in CrashReasons)
                ModBase.Log("[Crash]  - " + ModBase.GetStringFromEnum(Reason.Key) +
                            (Reason.Value.Any() ? "（" + Reason.Value.Join("；") + "）" : ""));
        }
    }

    /// <summary>
    ///     增加一个可能的崩溃原因。
    /// </summary>
    private void AppendReason(CrashReason Reason, ICollection<string> Additional = null)
    {
        if (CrashReasons.ContainsKey(Reason))
        {
            if (Additional is not null)
            {
                CrashReasons[Reason].AddRange(Additional);
                CrashReasons[Reason] = CrashReasons[Reason].Distinct().ToList();
            }
        }
        else
        {
            CrashReasons.Add(Reason, new List<string>(Additional ?? Array.Empty<string>()));
        }

        ModBase.Log("[Crash] 可能的崩溃原因：" + ModBase.GetStringFromEnum(Reason) +
                    (Additional is not null && Additional.Any() ? "（" + Additional.Join("；") + "）" : ""));
    }

    private void AppendReason(CrashReason Reason, string Additional)
    {
        AppendReason(Reason, string.IsNullOrEmpty(Additional) ? null : new List<string> { Additional });
    }

    // 具体的分析代码
    /// <summary>
    ///     进行精准日志匹配。匹配优先级高于堆栈分析的崩溃。
    /// </summary>
    private void AnalyzeCrit1()
    {
        // 空白分析
        if (LogMc is null && LogHs is null && LogCrash is null)
        {
            AppendReason(CrashReason.没有可用的分析文件);
            return;
        }

        // 崩溃报告分析，高优先级
        if (LogCrash is not null)
            if (LogCrash.Contains("Unable to make protected final java.lang.Class java.lang.ClassLoader.defineClass"))
                AppendReason(CrashReason.Java版本过高);

        // 游戏日志分析
        if (LogMc is not null)
        {
            if (LogMc.Contains("Found multiple arguments for option fml.forgeVersion, but you asked for only one"))
                AppendReason(CrashReason.实例Json中存在多个Forge);
            if (LogMc.Contains("The driver does not appear to support OpenGL"))
                AppendReason(CrashReason.显卡不支持OpenGL);
            if (LogMc.Contains("java.lang.ClassCastException: java.base/jdk"))
                AppendReason(CrashReason.使用JDK);
            if (LogMc.Contains("java.lang.ClassCastException: class jdk."))
                AppendReason(CrashReason.使用JDK);
            if (LogMc.Contains("TRANSFORMER/net.optifine/net.optifine.reflect.Reflector.<clinit>(Reflector.java"))
                AppendReason(CrashReason.OptiFine与Forge不兼容);
            if (LogMc.Contains(
                    "java.lang.NoSuchMethodError: 'void net.minecraft.client.renderer.texture.SpriteContents.<init>"))
                AppendReason(CrashReason.OptiFine与Forge不兼容);
            if (LogMc.Contains(
                    "java.lang.NoSuchMethodError: 'java.lang.String com.mojang.blaze3d.systems.RenderSystem.getBackendDescription"))
                AppendReason(CrashReason.OptiFine与Forge不兼容);
            if (LogMc.Contains(
                    "java.lang.NoSuchMethodError: 'void net.minecraft.client.renderer.block.model.BakedQuad.<init>"))
                AppendReason(CrashReason.OptiFine与Forge不兼容);
            if (LogMc.Contains(
                    "java.lang.NoSuchMethodError: 'void net.minecraftforge.client.gui.overlay.ForgeGui.renderSelectedItemName"))
                AppendReason(CrashReason.OptiFine与Forge不兼容);
            if (LogMc.Contains("java.lang.NoSuchMethodError: 'void net.minecraft.server.level.DistanceManager"))
                AppendReason(CrashReason.OptiFine与Forge不兼容);
            if (LogMc.Contains(
                    "java.lang.NoSuchMethodError: 'net.minecraft.network.chat.FormattedText net.minecraft.client.gui.Font.ellipsize"))
                AppendReason(CrashReason.OptiFine与Forge不兼容);
            if (LogMc.Contains("Open J9 is not supported") || LogMc.Contains("OpenJ9 is incompatible") ||
                LogMc.Contains(".J9VMInternals."))
                AppendReason(CrashReason.使用OpenJ9);
            if (LogMc.Contains("java.lang.NoSuchFieldException: ucp"))
                AppendReason(CrashReason.Java版本过高);
            if (LogMc.Contains("because module java.base does not export"))
                AppendReason(CrashReason.Java版本过高);
            if (LogMc.Contains(
                    "java.lang.ClassNotFoundException: jdk.nashorn.api.scripting.NashornScriptEngineFactory"))
                AppendReason(CrashReason.Java版本过高);
            if (LogMc.Contains("java.lang.ClassNotFoundException: java.lang.invoke.LambdaMetafactory"))
                AppendReason(CrashReason.Java版本过高);
            if (LogMc.Contains("The directories below appear to be extracted jar files. Fix this before you continue."))
                AppendReason(CrashReason.Mod文件被解压);
            if (LogMc.Contains("Extracted mod jars found, loading will NOT continue"))
                AppendReason(CrashReason.Mod文件被解压);
            if (LogMc.Contains("java.lang.ClassNotFoundException: org.spongepowered.asm.launch.MixinTweaker"))
                AppendReason(CrashReason.MixinBootstrap缺失);
            if (LogMc.Contains("Couldn't set pixel format"))
                AppendReason(CrashReason.显卡驱动不支持导致无法设置像素格式);
            if (LogMc.Contains("java.lang.OutOfMemoryError") || LogMc.Contains("an out of memory error"))
                AppendReason(CrashReason.内存不足);
            if (LogMc.Contains(
                    "java.lang.RuntimeException: Shaders Mod detected. Please remove it, OptiFine has built-in support for shaders."))
                AppendReason(CrashReason.ShadersMod与OptiFine同时安装);
            if (LogMc.Contains("java.lang.NoSuchMethodError: sun.security.util.ManifestEntryVerifier"))
                AppendReason(CrashReason.低版本Forge与高版本Java不兼容);
            if (LogMc.Contains("1282: Invalid operation"))
                AppendReason(CrashReason.光影或资源包导致OpenGL1282错误);
            if (LogMc.Contains("signer information does not match signer information of other classes in the same package"))
                AppendReason(CrashReason.文件或内容校验失败,
                    (LogMc.RegexSeek("(?<=class \")[^']+(?=\"'s signer information)") ?? "").TrimEnd('\r', '\n'));
            if (LogMc.Contains("Maybe try a lower resolution resourcepack?"))
                AppendReason(CrashReason.材质过大或显卡配置不足);
            if (LogMc.Contains(
                    "java.lang.NoSuchMethodError: net.minecraft.world.server.ChunkManager$ProxyTicketManager.shouldForceTicks(J)Z") &&
                LogMc.Contains("OptiFine"))
                AppendReason(CrashReason.OptiFine导致无法加载世界);
            if (LogMc.Contains("Unsupported class file major version"))
                AppendReason(CrashReason.Java版本不兼容);
            if (LogMc.Contains("com.electronwill.nightconfig.core.io.ParsingException: Not enough data available"))
                AppendReason(CrashReason.NightConfig的Bug);
            if (LogMc.Contains("Cannot find launch target fmlclient, unable to launch"))
                AppendReason(CrashReason.Forge安装不完整);
            if (LogMc.Contains("Invalid paths argument, contained no existing paths") &&
                LogMc.Contains(@"libraries\net\minecraftforge\fmlcore"))
                AppendReason(CrashReason.Forge安装不完整);
            if (LogMc.Contains("Invalid module name: '' is not a Java identifier"))
                AppendReason(CrashReason.Mod名称包含特殊字符);
            if (LogMc.Contains(
                    "has been compiled by a more recent version of the Java Runtime (class file version 55.0), this version of the Java Runtime only recognizes class file versions up to"))
                AppendReason(CrashReason.Mod需要Java11);
            if (LogMc.Contains(
                    "java.lang.RuntimeException: java.lang.NoSuchMethodException: no such method: sun.misc.Unsafe.defineAnonymousClass(Class,byte[],Object[])Class/invokeVirtual"))
                AppendReason(CrashReason.Mod需要Java11);
            if (LogMc.Contains(
                    "java.lang.IllegalArgumentException: The requested compatibility level JAVA_11 could not be set. Level is not supported by the active JRE or ASM version"))
                AppendReason(CrashReason.Mod需要Java11);
            if (LogMc.Contains("Unsupported major.minor version"))
                AppendReason(CrashReason.Java版本不兼容);
            if (LogMc.Contains("Invalid maximum heap size"))
                AppendReason(CrashReason.使用32位Java导致JVM无法分配足够多的内存);
            if (LogMc.Contains("Could not reserve enough space"))
            {
                if (LogMc.Contains("for 1048576KB object heap"))
                    AppendReason(CrashReason.使用32位Java导致JVM无法分配足够多的内存);
                else
                    AppendReason(CrashReason.内存不足);
            }

            // 确定的 Mod 导致崩溃
            if (LogMc.Contains("Caught exception from "))
                AppendReason(CrashReason.确定Mod导致游戏崩溃,
                    TryAnalyzeModName(LogMc.RegexSeek(@"(?<=Caught exception from )[^\n]+?")
                        ?.TrimEnd(("\r\n" + " ").ToCharArray())));
            // Mod 重复 / 前置问题
            if (LogMc.Contains("DuplicateModsFoundException"))
                AppendReason(CrashReason.Mod重复安装,
                    LogMc.RegexSearch(@"(?<=\n\t[\w]+ : [A-Z]:[^\n]+(/|\\))[^/\\\n]+?.jar", RegexOptions.IgnoreCase));
            if (LogMc.Contains("Found a duplicate mod"))
                AppendReason(CrashReason.Mod重复安装,
                    (LogMc.RegexSeek(@"Found a duplicate mod[^\n]+") ?? "").RegexSearch(@"[^\\/]+.jar",
                        RegexOptions.IgnoreCase));
            if (LogMc.Contains("Found duplicate mods"))
                AppendReason(CrashReason.Mod重复安装,
                    LogMc.RegexSearch(@"(?<=Mod ID: ')\w+?(?=' from mod files:)").Distinct().ToList());
            if (LogMc.Contains("ModResolutionException: Duplicate"))
                AppendReason(CrashReason.Mod重复安装,
                    (LogMc.RegexSeek(@"ModResolutionException: Duplicate[^\n]+") ?? "").RegexSearch(@"[^\\/]+.jar",
                        RegexOptions.IgnoreCase));
            if (LogMc.Contains("Incompatible mods found!")) // #5006
                AppendReason(CrashReason.Mod互不兼容,
                    LogMc.RegexSeek(@"(?<=Incompatible mods found![\s\S]+: )[\s\S]+?(?=\tat )") ?? "");
            if (LogMc.Contains("Missing or unsupported mandatory dependencies:"))
                AppendReason(CrashReason.Mod缺少前置或MC版本错误,
                    LogMc.RegexSearch(@"(?<=Missing or unsupported mandatory dependencies:)([\n\r]+\t(.*))+",
                            RegexOptions.IgnoreCase)
                        .Select(s => s.Trim(("\r\n" + Constants.vbTab + " ").ToCharArray())).Distinct()
                        .ToList());
        }

        // 虚拟机日志分析
        if (LogHs is not null)
        {
            if (LogHs.Contains("The system is out of physical RAM or swap space"))
                AppendReason(CrashReason.内存不足);
            if (LogHs.Contains("Out of Memory Error"))
                AppendReason(CrashReason.内存不足);
            if (LogHs.Contains("EXCEPTION_ACCESS_VIOLATION"))
            {
                if (LogHs.Contains("# C  [ig"))
                    AppendReason(CrashReason.Intel驱动不兼容导致EXCEPTION_ACCESS_VIOLATION);
                if (LogHs.Contains("# C  [atio"))
                    AppendReason(CrashReason.AMD驱动不兼容导致EXCEPTION_ACCESS_VIOLATION);
                if (LogHs.Contains("# C  [nvoglv"))
                    AppendReason(CrashReason.Nvidia驱动不兼容导致EXCEPTION_ACCESS_VIOLATION);
            }
        }

        // 崩溃报告分析
        if (LogCrash is not null)
        {
            if (LogCrash.Contains("maximum id range exceeded"))
                AppendReason(CrashReason.Mod过多导致超出ID限制);
            if (LogCrash.Contains("java.lang.OutOfMemoryError"))
                AppendReason(CrashReason.内存不足);
            if (LogCrash.Contains("Pixel format not accelerated"))
                AppendReason(CrashReason.显卡驱动不支持导致无法设置像素格式);
            if (LogCrash.Contains("Manually triggered debug crash"))
                AppendReason(CrashReason.玩家手动触发调试崩溃);
            if (LogCrash.Contains("has mods that were not found") &&
                LogCrash.RegexCheck(@"The Mod File [^\n]+optifine\\OptiFine[^\n]+ has mods that were not found"))
                AppendReason(CrashReason.OptiFine与Forge不兼容);
            // Mod 导致的崩溃
            if (LogCrash.Contains("-- MOD "))
            {
                var LogCrashMod = LogCrash.Between("-- MOD ", "Failure message:");
                if (LogCrashMod.ContainsF(".jar", true))
                    AppendReason(CrashReason.确定Mod导致游戏崩溃,
                        (LogCrashMod.RegexSeek("(?<=Mod File: ).+") ?? "").TrimEnd(
                            ("\r\n" + " ").ToCharArray()));
                else
                    AppendReason(CrashReason.Mod加载器报错,
                        (LogCrash.RegexSeek(@"(?<=Failure message: )[\w\W]+?(?=\tMod)") ?? "")
                        .Replace(Constants.vbTab, " ").TrimEnd(("\r\n" + " ").ToCharArray()));
            }

            if (LogCrash.Contains("Multiple entries with same key: "))
                AppendReason(CrashReason.确定Mod导致游戏崩溃,
                    TryAnalyzeModName(
                        (LogCrash.RegexSeek("(?<=Multiple entries with same key: )[^=]+") ?? "").TrimEnd(
                            ("\r\n" + " ").ToCharArray())));
            if (LogCrash.Contains("LoaderExceptionModCrash: Caught exception from "))
                AppendReason(CrashReason.确定Mod导致游戏崩溃,
                    TryAnalyzeModName(
                        (LogCrash.RegexSeek(@"(?<=LoaderExceptionModCrash: Caught exception from )[^\n]+") ?? "")
                        .TrimEnd(("\r\n" + " ").ToCharArray())));
            if (LogCrash.Contains("Failed loading config file "))
                AppendReason(CrashReason.Mod配置文件导致游戏崩溃,
                    new[]
                    {
                        TryAnalyzeModName(
                            (LogCrash.RegexSeek(@"(?<=Failed loading config file .+ for modid )[^\n]+") ?? "").TrimEnd('\r', '\n')).First(),
                        (LogCrash.RegexSeek("(?<=Failed loading config file ).+(?= of type)") ?? "").TrimEnd('\r', '\n')
                    });
        }
    }

    /// <summary>
    ///     进行精准日志匹配。匹配优先级高于堆栈分析的崩溃，但低于上面的。
    ///     如果第一步已经找到了原因则不执行该检测。
    /// </summary>
    private void AnalyzeCrit2()
    {
        // Mixin 分析
        bool MixinAnalyze(string LogText)
        {
            var IsMixin = LogText.Contains("Mixin prepare failed ") || LogText.Contains("Mixin apply failed ") ||
                          LogText.Contains("MixinApplyError") || LogText.Contains("MixinTransformerError") ||
                          LogText.Contains("mixin.injection.throwables.") || LogText.Contains(".json] FAILED during )");
            if (!IsMixin)
                return false;
            // Mod 名称匹配
            var ModName = LogText.RegexSeek(@"(?<=from mod )[^.\/ ]+(?=\] from)");
            if (ModName is null)
                ModName = LogText.RegexSeek(@"(?<=for mod )[^.\/ ]+(?= failed)");
            if (ModName is not null)
            {
                AppendReason(CrashReason.ModMixin失败,
                    TryAnalyzeModName(ModName.TrimEnd(("\r\n" + " ").ToCharArray())));
                return true;
            }

            // JSON 名称匹配
            foreach (var JsonName in LogText.RegexSearch(@"(?<=^[^\t]+[ \[{(]{1})[^ \[{(]+\.[^ ]+(?=\.json)",
                         RegexOptions.Multiline))
            {
                AppendReason(CrashReason.ModMixin失败,
                    TryAnalyzeModName(JsonName.Replace("mixins", "mixin").Replace(".mixin", "").Replace("mixin.", "")));
                return true;
            }

            // 没有明确匹配
            AppendReason(CrashReason.ModMixin失败);
            return true;
        }

        ;

        // 游戏日志分析
        if (LogMc is not null)
        {
            // Mixin 崩溃
            var IsMixin = MixinAnalyze(LogMc);
            // 常规信息
            if (LogMc.Contains("An exception was thrown, the game will display an error screen and halt."))
                AppendReason(CrashReason.Forge报错,
                    (LogMc.RegexSeek(@"(?<=the game will display an error screen and halt.[\n\r]+[^\n]+?Exception: )[\s\S]+?(?=\n\tat)")?.Trim('\r', '\n')) ?? "");
            if (LogMc.Contains("A potential solution has been determined:"))
                AppendReason(CrashReason.Fabric报错并给出解决方案,
                    (LogMc.RegexSeek(@"(?<=A potential solution has been determined:\n)(\s+ - [^\n]+\n)+") ?? "")
                    .RegexSearch(@"(?<=\s+)[^\n]+").Join("\n"));
            if (LogMc.Contains("A potential solution has been determined, this may resolve your problem:"))
                AppendReason(CrashReason.Fabric报错并给出解决方案,
                    (LogMc.RegexSeek(
                         @"(?<=A potential solution has been determined, this may resolve your problem:\n)(\s+ - [^\n]+\n)+") ??
                     "").RegexSearch(@"(?<=\s+)[^\n]+").Join("\n"));
            if (LogMc.Contains("确定了一种可能的解决方法，这样做可能会解决你的问题："))
                AppendReason(CrashReason.Fabric报错并给出解决方案,
                    (LogMc.RegexSeek(@"(?<=确定了一种可能的解决方法，这样做可能会解决你的问题：\n)(\s+ - [^\n]+\n)+") ?? "")
                    .RegexSearch(@"(?<=\s+)[^\n]+").Join("\n"));
            if (!IsMixin &&
                LogMc.Contains(
                    "due to errors, provided by ")) // 在 #3104 的情况下，这一句导致 OptiFabric 的 Mixin 失败错判为 Fabric Loader 加载失败
                AppendReason(CrashReason.确定Mod导致游戏崩溃,
                    TryAnalyzeModName(
                        (LogMc.RegexSeek("(?<=due to errors, provided by ')[^']+") ?? "").TrimEnd(
                            ("\r\n" + " ").ToCharArray())));
        }

        // 崩溃报告分析
        if (LogCrash is not null)
        {
            // Mixin 崩溃
            MixinAnalyze(LogCrash);
            // 常规信息
            if (LogCrash.Contains("Suspected Mod"))
            {
                var SuspectsRaw = LogCrash.Between("Suspected Mod", "Stacktrace");
                if (!SuspectsRaw.StartsWithF("s: None")) // Suspected Mods: None
                {
                    var Suspects = SuspectsRaw.RegexSearch(@"(?<=\n\t[^(\t]+\()[^)\n]+");
                    if (Suspects.Any())
                        AppendReason(CrashReason.怀疑Mod导致游戏崩溃, TryAnalyzeModName(Suspects));
                }
            }
        }
    }

    /// <summary>
    ///     进行精准日志匹配。匹配优先级低于堆栈分析的崩溃。
    /// </summary>
    private void AnalyzeCrit3()
    {
        // 游戏日志分析
        if (LogMc is not null)
        {
            // 极短的程序输出
            if (!(LogMc.Contains("at net.") || LogMc.Contains("INFO]")) && LogHs is null && LogCrash is null &&
                LogMc.Length < 100) AppendReason(CrashReason.极短的程序输出, LogMc);
            // Mod 解析错误（常见于 Fabric 前置校验失败）
            if (LogMc.Contains("Mod resolution failed"))
                AppendReason(CrashReason.Mod加载器报错);
            // Mixin 失败可以导致大量 Mod 实例创建失败
            if (LogMc.Contains("Failed to create mod instance."))
                AppendReason(CrashReason.Mod初始化失败,
                    TryAnalyzeModName(
                        (LogMc.RegexSeek("(?<=Failed to create mod instance. ModID: )[^,]+") ??
                         LogMc.RegexSeek(@"(?<=Failed to create mod instance. ModId )[^\n]+(?= for )") ?? "")
                        .TrimEnd('\r', '\n')));
            // 注意：Fabric 的 Warnings were found! 不一定是崩溃原因，它可能是单纯的警报
        }

        // 崩溃报告分析
        if (LogCrash is not null)
        {
            if (LogCrash.Contains(Constants.vbTab + "Block location: World: "))
                AppendReason(CrashReason.特定方块导致崩溃,
                    (LogCrash.RegexSeek(@"(?<=\tBlock: Block\{)[^\}]+") ?? "") + " " +
                    (LogCrash.RegexSeek(@"(?<=\tBlock location: World: )\([^\)]+\)") ?? ""));
            if (LogCrash.Contains(Constants.vbTab + "Entity's Exact location: "))
                AppendReason(CrashReason.特定实体导致崩溃,
                    (LogCrash.RegexSeek(@"(?<=\tEntity Type: )[^\n]+(?= \()") ?? "") + " (" +
                    (LogCrash.RegexSeek(@"(?<=\tEntity's Exact location: )[^\n]+") ?? "").TrimEnd(
                        "\r\n".ToCharArray()) + ")");
        }
    }

    /// <summary>
    ///     从堆栈中提取 Mod ID 关键字。若失败则返回空列表。
    /// </summary>
    private List<string> AnalyzeStackKeyword(string ErrorStack)
    {
        ErrorStack = "\n" + (ErrorStack ?? "") + "\n";

        // 进行正则匹配
        var StackSearchResults = new List<string>();
        StackSearchResults.AddRange(
            ErrorStack.RegexSearch(@"(?<=\n[^{]+)[a-zA-Z_]+\w+\.[a-zA-Z_]+[\w\.]+(?=\.[\w\.$]+\.)"));
        StackSearchResults.AddRange(ErrorStack.RegexSearch(@"(?<=at [^(]+?\.\w+\$\w+\$)[\w\$]+?(?=\$\w+\()")
            .Select(s => s.Replace("$", "."))); // Mixin 堆栈：xxx.xxx.xxxx$xxxx$xxx
        StackSearchResults = StackSearchResults.Distinct().ToList();

        // 检查堆栈开头
        var PossibleStacks = new List<string>();
        foreach (var Stack in StackSearchResults)
        {
            // If Not Stack.Contains(".") Then Continue For
            foreach (var IgnoreStack in new[]
                     {
                         "java", "sun", "javax", "jdk", "oolloo", "org.lwjgl", "com.sun", "net.minecraftforge",
                         "paulscode.sound", "com.mojang", "net.minecraft", "cpw.mods", "com.google", "org.apache",
                         "org.spongepowered", "net.fabricmc", "com.mumfrey", "org.quiltmc",
                         "com.electronwill.nightconfig", "it.unimi.dsi", "MojangTricksIntelDriversForPerformance_javaw"
                     })
                if (Stack.StartsWithF(IgnoreStack))
                    goto NextStack;
            PossibleStacks.Add(Stack.Trim());
            NextStack: ;
        }

        PossibleStacks = PossibleStacks.Distinct().ToList();

        ModBase.Log("[Crash] 找到 " + PossibleStacks.Count + " 条可能的堆栈信息");
        if (!PossibleStacks.Any())
            return new List<string>();
        foreach (var Stack in PossibleStacks)
            ModBase.Log("[Crash]  - " + Stack);

        // 检查堆栈关键词
        var PossibleWords = new List<string>();
        foreach (var Stack in PossibleStacks)
        {
            var Splited = Stack.Split(".");
            for (int i = 0, loopTo = Math.Min(3, Splited.Count() - 1); i <= loopTo; i++) // 最多取前 4 节
            {
                var Word = Splited[i];
                if (Word.Length <= 2 || Word.StartsWithF("func_"))
                    continue;
                if (new[]
                    {
                        "com", "org", "net", "asm", "fml", "mod", "jar", "sun", "lib", "map", "gui", "dev", "nio",
                        "api", "dsi", "top", "mcp", "core", "init", "mods", "main", "file", "game", "load", "read",
                        "done", "util", "tile", "item", "base", "oshi", "impl", "data", "pool", "task", "forge",
                        "setup", "block", "model", "mixin", "event", "unimi", "netty", "world", "lwjgl", "gitlab",
                        "common", "server", "config", "mixins", "compat", "loader", "launch", "entity", "assist",
                        "client", "plugin", "modapi", "mojang", "shader", "events", "github", "recipe", "render",
                        "packet", "events", "preinit", "preload", "machine", "reflect", "channel", "general", "handler",
                        "content", "systems", "modules", "service", "fastutil", "optifine", "internal", "platform",
                        "override", "fabricmc", "neoforge", "injection", "listeners", "scheduler", "minecraft",
                        "universal", "multipart", "neoforged", "microsoft", "transformer", "transformers",
                        "minecraftforge", "blockentity", "spongepowered", "electronwill"
                    }.Contains(Word.ToLower()))
                    continue;
                PossibleWords.Add(Word.Trim());
            }
        }

        PossibleWords = PossibleWords.Distinct().ToList();
        ModBase.Log("[Crash] 从堆栈信息中找到 " + PossibleWords.Count + " 个可能的 Mod ID 关键词");
        if (PossibleWords.Any())
            ModBase.Log("[Crash]  - " + PossibleWords.Join(", "));
        if (PossibleWords.Count > 10)
        {
            ModBase.Log("[Crash] 关键词过多，考虑匹配出错，不纳入考虑");
            return new List<string>();
        }

        return PossibleWords;
    }

    /// <summary>
    ///     根据 Mod 关键词尝试获取实际的 Mod 名称。
    ///     若失败则返回 Nothing。
    /// </summary>
    private List<string> AnalyzeModName(List<string> Keywords)
    {
        var ModFileNames = new List<string>();

        // 预处理关键词（分割括号）
        var RealKeywords = new List<string>();
        foreach (var Keyword in Keywords)
        foreach (var SubKeyword in Keyword.Split("("))
            RealKeywords.Add(SubKeyword.Trim(" )".ToCharArray()));
        Keywords = RealKeywords;

        // 从崩溃报告获取 Mod 信息
        if (LogCrash is not null && LogCrash.Contains("A detailed walkthrough of the error"))
        {
            var Details = LogCrash.Replace("A detailed walkthrough of the error", "¨");
            var IsFabricDetail = Details.Contains("Fabric Mods"); // 是否为 Fabric 信息格式
            if (IsFabricDetail)
            {
                Details = Details.Replace("Fabric Mods", "¨");
                ModBase.Log("[Crash] 崩溃报告中检测到 Fabric Mod 信息格式");
            }

            var isQuiltDetail = Details.Contains("quilt-loader");
            if (isQuiltDetail)
            {
                Details = Details.Replace("Mod Table Version", "¨");
                ModBase.Log("[Crash] 崩溃报告中检测到 Quilt Mod 信息格式");
            }

            Details = Details.AfterLast("¨");

            // [Forge] 获取所有包含 .jar 的行
            // [Fabric] 获取所有包含 Mod 信息的行
            var ModNameLines = new List<string>();
            foreach (var Line in Details.Split("\n"))
                if ((Line.ContainsF(".jar", true) && Line.Length - Line.Replace(".jar", "").Length == 4) ||
                    (IsFabricDetail && Line.StartsWithF(Constants.vbTab + Constants.vbTab) &&
                     !Line.RegexCheck(@"\t\tfabric[\w-]*: Fabric"))) // 只有一个 .jar
                    ModNameLines.Add(Line);
            ModBase.Log("[Crash] 崩溃报告中找到 " + ModNameLines.Count + " 个可能的 Mod 项目行");

            // 获取 Mod ID 与关键词的匹配行
            var HintLines = new List<string>();
            foreach (var KeyWord in Keywords)
            foreach (var ModString in ModNameLines)
            {
                var RealModString = ModString.ToLower().Replace("_", "");
                if (!RealModString.Contains(KeyWord.ToLower().Replace("_", "")))
                    continue;
                if (RealModString.Contains("minecraft.jar") || RealModString.Contains(" forge-") ||
                    RealModString.Contains(" mixin-"))
                    continue;
                HintLines.Add(ModString.Trim("\r\n".ToCharArray()));
                break;
            }

            HintLines = HintLines.Distinct().ToList();
            ModBase.Log("[Crash] 崩溃报告中找到 " + HintLines.Count + " 个可能的崩溃 Mod 匹配行");
            foreach (var ModLine in HintLines)
                ModBase.Log("[Crash]  - " + ModLine);

            // 从 Mod 匹配行中提取 .jar 文件的名称
            foreach (var Line in HintLines)
            {
                string Name;
                if (IsFabricDetail)
                    Name = Line.RegexSeek(@"(?<=: )[^\n]+(?= [^\n]+)");
                else
                    Name = Line.RegexSeek(@"(?<=\()[^\t]+.jar(?=\))|(?<=(\t\t)|(\| ))[^\t\|]+.jar",
                        RegexOptions.IgnoreCase);
                if (Name is not null)
                    ModFileNames.Add(Name);
            }
        }

        // 从 debug.log 获取 Mod 信息
        if (LogMcDebug is not null)
        {
            // Forge: Found valid mod file YungsBetterStrongholds-1.20-Forge-4.0.1.jar with {betterstrongholds} mods - versions {1.20-Forge-4.0.1}
            var ModNameLines = LogMcDebug.RegexSearch("(?<=valid mod file ).*", RegexOptions.Multiline);
            ModBase.Log("[Crash] Debug 信息中找到 " + ModNameLines.Count + " 个可能的 Mod 项目行");

            // 获取 Mod ID 与关键词的匹配行
            var HintLines = new List<string>();
            foreach (var KeyWord in Keywords)
            foreach (var ModString in ModNameLines)
                if (ModString.Contains($"{{{KeyWord}}}"))
                    HintLines.Add(ModString);

            HintLines = HintLines.Distinct().ToList();
            ModBase.Log("[Crash] Debug 信息中找到 " + HintLines.Count + " 个可能的崩溃 Mod 匹配行");
            foreach (var ModLine in HintLines)
                ModBase.Log("[Crash]  - " + ModLine);

            // 从 Mod 匹配行中提取 .jar 文件的名称
            foreach (var Line in HintLines)
            {
                string Name;
                Name = Line.RegexSeek(".*(?= with)");
                if (Name is not null)
                    ModFileNames.Add(Name);
            }
        }

        // 输出
        ModFileNames = ModFileNames.Distinct().ToList();
        if (!ModFileNames.Any()) return null;

        ModBase.Log("[Crash] 找到 " + ModFileNames.Count + " 个可能的崩溃 Mod 文件名");
        foreach (var ModFileName in ModFileNames)
            ModBase.Log("[Crash]  - " + ModFileName);
        return ModFileNames;
    }

    /// <summary>
    ///     尝试从关键字获取 Mod 名称，若失败则返回原关键字。
    /// </summary>
    private List<string> TryAnalyzeModName(string Keyword)
    {
        var RawList = new List<string> { Keyword ?? "" };
        if (string.IsNullOrEmpty(Keyword))
            return RawList;
        return AnalyzeModName(RawList) ?? RawList;
    }

    /// <summary>
    ///     尝试从关键字获取 Mod 名称，若失败则返回原关键字。
    /// </summary>
    private List<string> TryAnalyzeModName(List<string> Keywords)
    {
        if (!Keywords.Any())
            return Keywords;
        return AnalyzeModName(Keywords) ?? Keywords;
    }

    /// <summary>
    ///     弹出崩溃弹窗，并指导导出崩溃报告。
    /// </summary>
    public void Output(bool IsHandAnalyze, List<string> ExtraFiles = null)
    {
        // 弹窗提示
        ModMain.FrmMain.ShowWindowToTop();
        var resultText = GetAnalyzeResult(IsHandAnalyze);
        // 确定是否是加载器版本不兼容问题
        var isModLoaderIncompatible = _version is not null && resultText.StartsWith("Mod 加载器版本与 Mod 不兼容");
        // 弹窗选择：查看日志
        switch (ModMain.MyMsgBox(resultText, IsHandAnalyze ? "错误报告分析结果" : "Minecraft 出现错误", "确定",
                    IsHandAnalyze || DirectFile is null ? "" : isModLoaderIncompatible ? "前往修改" : "查看日志",
                    IsHandAnalyze ? "" : "导出错误报告",
                    Button2Action: IsHandAnalyze || DirectFile is null || isModLoaderIncompatible
                        ? null
                        : new Action(() =>
                        {
                            if (File.Exists(DirectFile.Value.Key))
                            {
                                ModBase.ShellOnly(DirectFile.Value.Key);
                            }
                            else
                            {
                                var FilePath = ModBase.PathTemp + "Crash.txt";
                                ModBase.WriteFile(FilePath, DirectFile.Value.Value.Join("\r\n"));
                                ModBase.ShellOnly(FilePath);
                            }
                        })))
        {
            case 2:
            {
                // 弹窗选择：前往修改
                PageInstanceLeft.Instance = _version;
                ModBase.RunInUi(() =>
                    ModMain.FrmMain.PageChange(FormMain.PageType.InstanceSetup, FormMain.PageSubType.VersionInstall));
                break;
            }
            case 3:
            {
                // 弹窗选择：导出错误报告
                string FileAddress = null;
                try
                {
                    // 获取文件路径
                    ModBase.RunInUiWait(() => FileAddress = SystemDialogs.SelectSaveFile("选择保存位置",
                        "错误报告-" + DateTime.Now.ToString("G").Replace("/", "-").Replace(":", ".").Replace(" ", "_") +
                        ".zip", "Minecraft 错误报告(*.zip)|*.zip"));
                    if (string.IsNullOrEmpty(FileAddress))
                        return;
                    Directory.CreateDirectory(ModBase.GetPathFromFullPath(FileAddress));
                    if (File.Exists(FileAddress))
                        File.Delete(FileAddress);
                    // 输出诊断信息
                    ModBase.FeedbackInfo();
                    // 复制文件
                    if (ExtraFiles is not null)
                        OutputFiles.AddRange(ExtraFiles);
                    foreach (var OutputFile in OutputFiles)
                    {
                        var FileName = ModBase.GetFileNameFromPath(OutputFile);
                        Encoding FileEncoding = null;
                        switch (FileName ?? "")
                        {
                            case "LatestLaunch.bat":
                            {
                                FileName = "启动脚本.bat";
                                break;
                            }
                            case "RawOutput.log":
                            {
                                FileName = "游戏崩溃前的输出.txt";
                                FileEncoding = Encoding.UTF8;
                                break;
                            }
                        }

                        if (LogWrapper.CurrentLogger.CurrentLogFiles.Last().AfterLast(@"\") == FileName)
                        {
                            FileName = "PCL 启动器日志.txt";
                            FileEncoding = Encoding.UTF8;
                        }

                        if (File.Exists(OutputFile))
                        {
                            if (FileEncoding is null)
                                FileEncoding = EncodingDetector.DetectEncoding(ModBase.ReadFileBytes(OutputFile));
                            var FileContent = ModBase.ReadFile(OutputFile, FileEncoding);
                            FileContent = ModMinecraft.FilterAccessToken(FileContent,
                                FileName == "启动脚本.bat" ? 'F' : '*');
                            FileContent = ModMinecraft.FilterUserName(FileContent, '*');
                            ModBase.WriteFile(TempFolder + @"Report\" + FileName, FileContent, Encoding: FileEncoding);
                            ModBase.Log($"[Crash] 导出文件：{FileName}，编码：{FileEncoding.HeaderName}");
                        }
                    }

                    // 输出环境与启动信息
                    string EnvInfo = null;
                    string McLauncherLog = null;
                    McLauncherLog = ModBase.ReadFile(TempFolder + @"Report\PCL 启动器日志.txt")
                        .AfterLast("[Launch] ~ 基础参数 ~").BeforeFirst("开始 Minecraft 日志监控");
                    var LaunchScript = ModBase.ReadFile(TempFolder + @"Report\启动脚本.bat");
                    EnvInfo += $"PCL CE 版本：{ModBase.VersionBaseName} {"\r\n"}";
                    EnvInfo += $"识别码：{ModBase.UniqueAddress}{"\r\n"}";
                    EnvInfo += $"{"\r\n"}- 档案信息 -{"\r\n"}";
                    EnvInfo +=
                        $"档案名称：{McLauncherLog.Between("玩家用户名：", "[").TrimEnd('[').Trim()} (验证方式：{McLauncherLog.Between("验证方式：", "[").TrimEnd('[').Trim()}){"\r\n"}";
                    EnvInfo += $"{"\r\n"}- 实例信息 -{"\r\n"}";
                    EnvInfo +=
                        $"选定的 Java 虚拟机：{McLauncherLog.Between("Java 信息：", "[").TrimEnd('[').Trim()}{"\r\n"}";
                    EnvInfo +=
                        $"Log4j2 NoLookups：{!LaunchScript.ContainsF("-Dlog4j2.formatMsgNoLookups=false")}{"\r\n"}";
                    EnvInfo += $"MC 文件夹：{McLauncherLog.Between("MC 文件夹：", "[").TrimEnd('[').Trim()}{"\r\n"}";
                    EnvInfo += $"{"\r\n"}- 环境信息 -{"\r\n"}";
                    EnvInfo +=
                        $"操作系统：{ModSecret.OSInfo}（64 位：{!ModBase.Is32BitSystem}, ARM64: {ModBase.IsArm64System}）{"\r\n"}";
                    EnvInfo += $"CPU：{ModSecret.CPUName}{"\r\n"}";
                    EnvInfo +=
                        $"内存分配 (分配的内存 / 已安装物理内存)：{McLauncherLog.Between("分配的内存：", "[").TrimEnd('[').Trim()} / {Math.Round(ModSecret.SystemMemorySize / 1024d, 2)} GB ({ModSecret.SystemMemorySize} MB){"\r\n"}";
                    foreach (var GPU in ModSecret.GPUs)
                    {
                        EnvInfo +=
                            $"显卡 {ModSecret.GPUs.IndexOf(GPU)}：{GPU.Name} ({(GPU.Memory >= 4095L ? ">= " + GPU.Memory : GPU.Memory)} MB, {GPU.DriverVersion})";
                        EnvInfo += "\r\n";
                    }

                    File.CreateText(TempFolder + @"Report\环境与启动信息.txt").Close();
                    ModBase.WriteFile(TempFolder + @"Report\环境与启动信息.txt", EnvInfo, Encoding: Encoding.UTF8);
                    // 导出报告
                    ZipFile.CreateFromDirectory(TempFolder + @"Report\", FileAddress);
                    ModBase.DeleteDirectory(TempFolder + @"Report\");
                    ModMain.Hint("错误报告已导出！", ModMain.HintType.Finish);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "导出错误报告失败", ModBase.LogLevel.Feedback);
                    return;
                }

                ModBase.OpenExplorer(FileAddress);
                break;
            }
        }
    }

    /// <summary>
    ///     获取崩溃分析的结果描述。
    /// </summary>
    private string GetAnalyzeResult(bool IsHandAnalyze)
    {
        // 没有结果的处理
        if (!CrashReasons.Any())
        {
            if (IsHandAnalyze) return "很抱歉，PCL 无法确定错误原因。";

            return $"很抱歉，你的游戏出现了一些问题……{"\r\n"}如果要寻求帮助，请把错误报告文件发给对方，而不是发送这个窗口的照片或者截图。";
        }

        // 根据不同原因判断
        var Results = new List<string>();
        const string LoaderIncompatibleResultText = @"Mod 加载器版本与 Mod 不兼容，请前往 实例设置 - 修改 更换加载器版本。\n\n详细信息：\n";
        foreach (var Reason in CrashReasons)
        {
            var Additional = Reason.Value;
            switch (Reason.Key)
            {
                case CrashReason.Mod文件被解压:
                {
                    Results.Add(
                        @"由于 Mod 文件被解压了，导致游戏无法继续运行。\n直接把整个 Mod 文件放进 Mod 文件夹中即可，若解压就会导致游戏出错。\n\n请删除 Mod 文件夹中已被解压的 Mod，然后再启动游戏。");
                    break;
                }
                case CrashReason.内存不足:
                {
                    Results.Add(
                        @"Minecraft 内存不足，导致其无法继续运行。\n这很可能是因为电脑内存不足、游戏分配的内存不足，或是配置要求过高。\n\n你可以尝试在 更多 → 百宝箱 中选择 内存优化，然后再启动游戏。\n如果还是不行，请在启动设置中增加为游戏分配的内存，并删除配置要求较高的材质、Mod、光影。\n如果依然不奏效，请在开始游戏前尽量关闭其他软件，或者……换台电脑？\h");
                    break;
                }
                case CrashReason.使用OpenJ9:
                {
                    Results.Add(@"游戏因为使用 OpenJ9 而崩溃了。\n请在启动设置的 Java 选择一项中改用非 OpenJ9 的 Java，然后再启动游戏。");
                    break;
                }
                case CrashReason.使用JDK:
                {
                    Results.Add(
                        @"游戏似乎因为使用 JDK，或 Java 版本过高而崩溃了。\n请在启动设置的 Java 选择一项中改用 JRE 8（Java 8），然后再启动游戏。\n如果你没有安装 JRE 8，你可以从网络中下载、安装一个。");
                    break;
                }
                case CrashReason.Java版本过高:
                {
                    Results.Add(
                        @"游戏似乎因为你所使用的 Java 版本过高而崩溃了。\n请在启动设置的 Java 选择一项中改用较低版本的 Java，然后再启动游戏。\n如果没有，可以从网络中下载、安装一个。");
                    break;
                }
                case CrashReason.Java版本不兼容:
                {
                    Results.Add(@"游戏不兼容你当前使用的 Java。\n如果没有合适的 Java，可以从网络中下载、安装一个。");
                    break;
                }
                case CrashReason.Mod名称包含特殊字符:
                {
                    Results.Add(@"由于有 Mod 的名称包含特殊字符，导致游戏崩溃。\n请尝试修改 Mod 文件名，让它只包含英文字母、数字、减号（-）、下划线（_）和小数点，然后再启动游戏。");
                    break;
                }
                case CrashReason.MixinBootstrap缺失:
                {
                    Results.Add(@"由于缺失 MixinBootstrap，导致游戏崩溃。\n请尝试安装 MixinBootstrap。若安装后依然崩溃，可以尝试在文件名前添加英文感叹号。");
                    break;
                }
                case CrashReason.使用32位Java导致JVM无法分配足够多的内存:
                {
                    if (Environment.Is64BitOperatingSystem)
                        Results.Add(
                            @"你似乎正在使用 32 位 Java，这会导致 Minecraft 无法使用所需的内存，进而造成崩溃。\n\n请在启动设置的 Java 选择一项中改用 64 位的 Java 再启动游戏，然后再启动游戏。\n如果你没有安装 64 位的 Java，你可以从网络中下载、安装一个。");
                    else
                        Results.Add(
                            @"你正在使用 32 位的操作系统，这会导致 Minecraft 无法使用所需的内存，进而造成崩溃。\n\n你或许只能重装 64 位的操作系统来解决此问题。\n如果你的电脑内存在 2GB 以内，那或许只能换台电脑了……\h");

                    break;
                }
                case CrashReason.Mod缺少前置或MC版本错误:
                {
                    if (Additional.Any())
                    {
                        var info = Additional.Join(@"\n - ");
                        if (info.IsMatch(RegexPatterns.IncompatibleModLoaderErrorHint))
                            Results.Add(LoaderIncompatibleResultText + info);
                        else
                            Results.Add(@"由于未安装正确的前置 Mod，导致游戏退出。\n缺失的依赖项：\n - " + info +
                                        @"\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。");
                    }
                    else
                    {
                        Results.Add(@"由于未安装正确的前置 Mod，导致游戏退出。\n请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h");
                    }

                    break;
                }
                case CrashReason.堆栈分析发现关键字:
                {
                    if (Additional.Count == 1)
                        Results.Add("你的游戏遇到了一些问题，PCL 为此找到了一个可疑的关键词：" + Additional.First() +
                                    @"。\n\n如果你知道某个关键词对应的 Mod，那么有可能就是它引起的错误，你也可以查看错误报告获取详情。\h");
                    else
                        Results.Add(@"你的游戏遇到了一些问题，PCL 为此找到了以下可疑的关键词：\n - " + Additional.Join(", ") +
                                    @"\n\n如果你知道某个关键词对应的 Mod，那么有可能就是它引起的错误，你也可以查看错误报告获取详情。\h");

                    break;
                }
                case CrashReason.堆栈分析发现Mod名称:
                case CrashReason.怀疑Mod导致游戏崩溃:
                {
                    if (Additional.Count == 1)
                        Results.Add("PCL 怀疑名为 " + Additional.First() +
                                    @" 的 Mod 导致了游戏出错，但不能完全确定。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h");
                    else
                        Results.Add(@"PCL 怀疑以下 Mod 导致了游戏出错，但不能完全确定：\n - " + Additional.Join(@"\n - ") +
                                    @"\n\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h");

                    break;
                }
                case CrashReason.确定Mod导致游戏崩溃:
                {
                    if (Additional.Count == 1)
                        Results.Add("名为 " + Additional.First() + @" 的 Mod 导致了游戏出错。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h");
                    else
                        Results.Add(@"以下 Mod 导致了游戏出错：\n - " + Additional.Join(@"\n - ") +
                                    @"\n\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h");

                    break;
                }
                case CrashReason.ModMixin失败:
                {
                    if (Additional.Count == 0)
                        Results.Add(
                            @"部分 Mod 注入失败，导致游戏出错。\n这一般代表着部分 Mod 与其他 Mod 或当前环境不兼容，或是它存在 Bug。\n你可以尝试逐步禁用 Mod，然后观察游戏是否还会崩溃，以此定位导致崩溃的 Mod。\n\e\h");
                    else if (Additional.Count == 1)
                        Results.Add("名为 " + Additional.First() +
                                    @" 的 Mod 注入失败，导致游戏出错。\n这一般代表着它与其他 Mod 或当前环境不兼容，或是它存在 Bug。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h");
                    else
                        Results.Add(@"以下 Mod 导致了游戏出错：\n - " + Additional.Join(@"\n - ") +
                                    @"\n这一般代表着它们与其他 Mod 或当前环境不兼容，或是它存在 Bug。\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h");

                    break;
                }
                case CrashReason.Mod配置文件导致游戏崩溃:
                {
                    if (Additional[1] is null)
                        Results.Add("名为 " + Additional.First() + @" 的 Mod 导致了游戏出错。\n\e\h");
                    else
                        Results.Add("名为 " + Additional.First() + @" 的 Mod 导致了游戏出错：\n其配置文件 " + Additional[1] +
                                    " 存在异常，无法读取。");

                    break;
                }
                case CrashReason.Mod初始化失败:
                {
                    if (Additional.Count == 1)
                        Results.Add("名为 " + Additional.First() +
                                    @" 的 Mod 初始化失败，导致游戏无法继续加载。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h");
                    else
                        Results.Add(@"以下 Mod 初始化失败，导致游戏出错：\n - " + Additional.Join(@"\n - ") +
                                    @"\n\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h");

                    break;
                }
                case CrashReason.特定方块导致崩溃:
                {
                    if (Additional.Count == 1)
                        Results.Add("游戏似乎因为方块 " + Additional.First() +
                                    @" 出现了问题。\n\n你可以创建一个新世界，并观察游戏的运行情况：\n - 若正常运行，则是该方块导致出错，你或许需要使用一些方式删除此方块。\n - 若仍然出错，问题就可能来自其他原因……\h");
                    else
                        Results.Add(
                            @"游戏似乎因为世界中的某些方块出现了问题。\n\n你可以创建一个新世界，并观察游戏的运行情况：\n - 若正常运行，则是某些方块导致出错，你或许需要删除该世界。\n - 若仍然出错，问题就可能来自其他原因……\h");

                    break;
                }
                case CrashReason.Mod重复安装:
                {
                    if (Additional.Count >= 2)
                        Results.Add(@"你重复安装了多个相同的 Mod：\n - " + Additional.Join(@"\n - ") +
                                    @"\n\n每个 Mod 只能出现一次，请删除重复的 Mod，然后再启动游戏。");
                    else
                        Results.Add(@"你可能重复安装了多个相同的 Mod，导致游戏出错。\n\n每个 Mod 只能出现一次，请删除重复的 Mod，然后再启动游戏。\e\h");

                    break;
                }
                case CrashReason.特定实体导致崩溃:
                {
                    if (Additional.Count == 1)
                        Results.Add("游戏似乎因为实体 " + Additional.First() +
                                    @" 出现了问题。\n\n你可以创建一个新世界，并生成一个该实体，然后观察游戏的运行情况：\n - 若正常运行，则是该实体导致出错，你或许需要使用一些方式删除此实体。\n - 若仍然出错，问题就可能来自其他原因……\h");
                    else
                        Results.Add(
                            @"游戏似乎因为世界中的某些实体出现了问题。\n\n你可以创建一个新世界，并生成各种实体，观察游戏的运行情况：\n - 若正常运行，则是某些实体导致出错，你或许需要删除该世界。\n - 若仍然出错，问题就可能来自其他原因……\h");

                    break;
                }
                case CrashReason.OptiFine与Forge不兼容:
                {
                    Results.Add(
                        @"由于 OptiFine 与当前版本的 Forge 不兼容，导致了游戏崩溃。\n\n请前往 OptiFine 官网（https://optifine.net/downloads）查看 OptiFine 所兼容的 Forge 版本，并严格按照对应版本重新安装游戏。");
                    break;
                }
                case CrashReason.ShadersMod与OptiFine同时安装:
                {
                    Results.Add(
                        @"无需同时安装 OptiFine 和 Shaders Mod，OptiFine 已经集成了 Shaders Mod 的功能。\n在删除 Shaders Mod 后，游戏即可正常运行。");
                    break;
                }
                case CrashReason.低版本Forge与高版本Java不兼容:
                {
                    Results.Add(
                        @"由于低版本 Forge 与当前 Java 不兼容，导致了游戏崩溃。\n\n请尝试以下解决方案：\n - 更新 Forge 到 36.2.26 或更高版本\n - 换用版本低于 1.8.0.320 的 Java");
                    break;
                }
                case CrashReason.实例Json中存在多个Forge:
                {
                    Results.Add(@"可能由于其他启动器修改了 Forge 版本，当前实例的文件存在异常，导致了游戏崩溃。\n请尝试重新全新安装 Forge，而非使用其他启动器修改 Forge 版本。");
                    break;
                }
                case CrashReason.玩家手动触发调试崩溃:
                {
                    Results.Add(@"* 事实上，你的游戏没有任何问题，这是你自己触发的崩溃。\n* 你难道没有更重要的事要做吗？");
                    break;
                }
                case CrashReason.Mod需要Java11:
                {
                    Results.Add(
                        @"你所安装的部分 Mod 似乎需要使用 Java 11 启动。\n请在启动设置的 Java 选择一项中改用 Java 11，然后再启动游戏。\n如果你没有安装 Java 11，你可以从网络中下载、安装一个。");
                    break;
                }
                case CrashReason.极短的程序输出:
                {
                    Results.Add($@"程序返回了以下信息：\n{Additional.First()}\n\h");
                    break;
                }
                case CrashReason.OptiFine导致无法加载世界
                    : // https://www.minecraftforum.net/forums/support/java-edition-support/3051132-exception-ticking-world
                {
                    Results.Add(@"你所使用的 OptiFine 可能导致了你的游戏出现问题。\n\n该问题只在特定 OptiFine 版本中出现，你可以尝试更换 OptiFine 的版本。\h");
                    break;
                }
                case CrashReason.显卡驱动不支持导致无法设置像素格式:
                case CrashReason.Intel驱动不兼容导致EXCEPTION_ACCESS_VIOLATION:
                case CrashReason.AMD驱动不兼容导致EXCEPTION_ACCESS_VIOLATION:
                case CrashReason.Nvidia驱动不兼容导致EXCEPTION_ACCESS_VIOLATION:
                case CrashReason.显卡不支持OpenGL:
                {
                    if (LogAll.Contains("hd graphics "))
                        Results.Add(
                            @"你的显卡驱动存在问题，或未使用独立显卡，导致游戏无法正常运行。\n\n如果你的电脑存在独立显卡，请使用独立显卡而非 Intel 核显启动 PCL 与 Minecraft。\n如果问题依然存在，请尝试升级你的显卡驱动到最新版本，或回退到出厂版本。\n如果还是不行，还可以尝试使用 8.0.51 或更低版本的 Java。\h");
                    else
                        Results.Add(
                            @"你的显卡驱动存在问题，导致游戏无法正常运行。\n\n请尝试升级你的显卡驱动到最新版本，或回退到出厂版本，然后再启动游戏。\n如果还是不行，可以尝试使用 8.0.51 或更低版本的 Java。\n如果问题依然存在，那么你可能需要换个更好的显卡……\h");

                    break;
                }
                case CrashReason.材质过大或显卡配置不足:
                {
                    Results.Add(
                        @"你所使用的材质分辨率过高，或显卡配置不足，导致游戏无法继续运行。\n\n如果你正在使用高清材质，请将它移除。\n如果你没有使用材质，那么你可能需要更新显卡驱动，或者换个更好的显卡……\h");
                    break;
                }
                case CrashReason.NightConfig的Bug:
                {
                    Results.Add(@"由于 Night Config 存在问题，导致了游戏崩溃。\n你可以尝试安装 Night Config Fixes 模组，这或许能解决此问题。\h");
                    break;
                }
                case CrashReason.光影或资源包导致OpenGL1282错误:
                {
                    Results.Add(@"你所使用的光影或材质导致游戏出现了一些问题……\n\n请尝试删除你所添加的这些额外资源。\h");
                    break;
                }
                case CrashReason.Mod过多导致超出ID限制:
                {
                    Results.Add(@"你所安装的 Mod 过多，超出了游戏的 ID 限制，导致了游戏崩溃。\n请尝试安装 JEID 等修复 Mod，或删除部分大型 Mod。");
                    break;
                }
                case CrashReason.文件或内容校验失败:
                {
                    Results.Add(@"部分文件或内容校验失败，导致游戏出现了问题。\n\n请尝试删除游戏（包括 Mod）并重新下载，或尝试在重新下载时使用 VPN。\h");
                    break;
                }
                case CrashReason.Forge安装不完整:
                {
                    Results.Add(
                        @"由于安装的 Forge 文件丢失，导致游戏无法正常运行。\n请前往实例设置重置该实例，然后再启动游戏。\n在打包游戏时删除 libraries 文件夹可能导致此错误。\h");
                    break;
                }
                case CrashReason.Fabric报错:
                {
                    if (Additional.Count == 1)
                        Results.Add(@"Fabric 提供了以下错误信息：\n" + Additional.First() +
                                    @"\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。");
                    else
                        Results.Add(@"Fabric 可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h");

                    break;
                }
                case CrashReason.Mod互不兼容:
                {
                    if (Additional.Count == 1)
                    {
                        var info = Additional.First();
                        if (info.IsMatch(RegexPatterns.IncompatibleModLoaderErrorHint))
                            Results.Add(LoaderIncompatibleResultText + info);
                        else
                            Results.Add(@"你所安装的 Mod 不兼容：\n" + info + @"\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。");
                    }
                    else
                    {
                        Results.Add(@"你所安装的 Mod 不兼容，Mod 加载器可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h");
                    }

                    break;
                }
                case CrashReason.Mod加载器报错:
                {
                    if (Additional.Count == 1)
                        Results.Add(@"Mod 加载器提供了以下错误信息：\n" + Additional.First() +
                                    @"\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。");
                    else
                        Results.Add(@"Mod 加载器可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h");

                    break;
                }
                case CrashReason.Fabric报错并给出解决方案:
                {
                    if (Additional.Count == 1)
                        Results.Add(@"Fabric 提供了以下解决方案：\n" + Additional.First() +
                                    @"\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。");
                    else
                        Results.Add(@"Fabric 可能已经提供了解决方案，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h");

                    break;
                }
                case CrashReason.Forge报错:
                {
                    if (Additional.Count == 1)
                        Results.Add(@"Forge 提供了以下错误信息：\n" + Additional.First() + @"\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。");
                    else
                        Results.Add(@"Forge 可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h");

                    break;
                }
                case CrashReason.没有可用的分析文件:
                {
                    Results.Add(@"你的游戏出现了一些问题，但 PCL 未能找到相关记录文件，因此无法进行分析。\h");
                    break;
                }

                default:
                {
                    Results.Add("PCL 获取到了没有详细信息的错误原因（" + (int)CrashReasons.First().Key + @"），请向 PCL 作者提交反馈以获取详情。\h");
                    break;
                }
            }
        }

        var isLauncherLatest = false;
        try
        {
            isLauncherLatest = ModSecret.GetVersionStatus() == ModSecret.VersionStatus.Latest;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "确认启动器更新失败", ModBase.LogLevel.Feedback);
        }

        return Results.Join(@"\n\n此外，").Replace(@"\n", "\r\n").Replace(@"\h", "")
                   .Replace(@"\e", IsHandAnalyze ? "" : "\r\n" + "你可以查看错误报告了解错误具体是如何发生的。")
                   .Replace("\r\n", "\r").Replace("\n", "\r")
                   .Replace("\r", "\r\n").Trim("\r\n".ToCharArray()) +
               (!Results.Any(r => r.EndsWithF(@"\h")) || IsHandAnalyze
                   ? ""
                   : "\r\n" + "如果要寻求帮助，请把错误报告文件发给对方，而不是发送这个窗口的照片或者截图。" + (isLauncherLatest
                       ? ""
                       : "\r\n" + "\r\n" + "此外，你正在使用老版本 PCL，更新 PCL 或许也能解决这个问题。" + "\r\n" +
                         "你可以点击 设置 → 启动器 → 检查更新 来更新 PCL。"));
    }

    // 2：确认实际用于分析的 Log 文本
    private enum AnalyzeFileType
    {
        HsErr,
        MinecraftLog,
        ExtraLogFile,
        ExtraReportFile,
        CrashReport
    }

    /// <summary>
    ///     导致崩溃的原因枚举。
    /// </summary>
    private enum CrashReason
    {
        Mod文件被解压,
        MixinBootstrap缺失,
        内存不足,
        使用JDK,
        显卡不支持OpenGL,
        使用OpenJ9,
        Java版本过高,
        Java版本不兼容,
        Mod名称包含特殊字符,
        显卡驱动不支持导致无法设置像素格式,
        极短的程序输出,
        Intel驱动不兼容导致EXCEPTION_ACCESS_VIOLATION, // https://bugs.mojang.com/browse/MC-32606
        AMD驱动不兼容导致EXCEPTION_ACCESS_VIOLATION, // https://bugs.mojang.com/browse/MC-31618
        Nvidia驱动不兼容导致EXCEPTION_ACCESS_VIOLATION,
        玩家手动触发调试崩溃,
        光影或资源包导致OpenGL1282错误,
        文件或内容校验失败,
        确定Mod导致游戏崩溃,
        怀疑Mod导致游戏崩溃,
        Mod配置文件导致游戏崩溃,
        ModMixin失败,
        Mod加载器报错,
        Mod初始化失败,
        堆栈分析发现关键字,
        堆栈分析发现Mod名称,
        OptiFine导致无法加载世界, // https://www.minecraftforum.net/forums/support/java-edition-support/3051132-exception-ticking-world
        特定方块导致崩溃,
        特定实体导致崩溃,
        材质过大或显卡配置不足,
        没有可用的分析文件,
        使用32位Java导致JVM无法分配足够多的内存,
        Mod重复安装,
        Mod互不兼容,
        OptiFine与Forge不兼容,
        Fabric报错,
        Fabric报错并给出解决方案,
        Forge报错,
        低版本Forge与高版本Java不兼容,
        实例Json中存在多个Forge,
        Mod过多导致超出ID限制,
        NightConfig的Bug,
        ShadersMod与OptiFine同时安装,
        Forge安装不完整,
        Mod需要Java11,
        Mod缺少前置或MC版本错误
    }
}