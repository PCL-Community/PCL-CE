using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.Utils.Codecs;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;
using PCL.Core.App.Localization;
using System.Globalization;
using PCL.Core.Utils.Secret;

namespace PCL;

public class CrashAnalyzer
{
    // 1：准备用于分析的 Log 文件
    private readonly List<KeyValuePair<string, string[]>> analyzeRawFiles = new(); // 暂存的日志文件：文件完整路径 -> 文件内容

    // 可能导致崩溃的原因与附加信息
    private readonly Dictionary<CrashReason, List<string>> crashReasons = new();

    // 4：根据原因输出信息
    private readonly List<string> outputFiles = new();

    // 构造函数
    private readonly string tempFolder;

    // 暂存分析的实例供特殊用途
    // 龙猫味石山代码小记: CrashAnalyze 猛一顿分析不知道自己在分析啥实例
    private McInstance _version;
    private KeyValuePair<string, string[]>? directFile; // 在弹窗中选择直接打开的文件
    private string logAll;
    private string logCrash;
    private string logHs;

    // 3：根据文本分析崩溃原因
    private string logMc;
    private string logMcDebug;

    public CrashAnalyzer(int uUID)
    {
        // 构建文件结构
        tempFolder = ModMain.RequestTaskTempFolder();
        Directory.CreateDirectory(Path.Combine(tempFolder, "Temp"));
        Directory.CreateDirectory(Path.Combine(tempFolder, "Report"));
        ModBase.Log("[Crash] 崩溃分析暂存文件夹：" + tempFolder);
    }

    /// <summary>
    ///     将可用于分析的日志存储到 AnalyzeRawFiles。
    /// </summary>
    /// <param name="latestLog">从 PCL 捕获到的最后 200 行程序输出。</param>
    public void Collect(string versionPathIndie, IList<string> latestLog = null)
    {
        ModBase.Log("[Crash] 步骤 1：收集日志文件");

        // 简单收集可能的日志文件路径
        var possibleLogs = new List<string>();
        try
        {
            var dirInfo = new DirectoryInfo(versionPathIndie + @"crash-reports\");
            if (dirInfo.Exists)
                foreach (var File in dirInfo.EnumerateFiles())
                    possibleLogs.Add(File.FullName);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "收集 Minecraft 崩溃日志文件夹下的日志失败");
        }

        try
        {
            foreach (var File in new DirectoryInfo(versionPathIndie).Parent.Parent.EnumerateFiles())
            {
                if ((File.Extension ?? "") != ".log")
                    continue;
                possibleLogs.Add(File.FullName);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "收集 Minecraft 主文件夹下的日志失败");
        }

        try
        {
            foreach (var File in new DirectoryInfo(versionPathIndie).EnumerateFiles())
            {
                if ((File.Extension ?? "") != ".log")
                    continue;
                possibleLogs.Add(File.FullName);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "收集 Minecraft 隔离文件夹下的日志失败");
        }

        possibleLogs.Add(versionPathIndie + @"logs\latest.log"); // Minecraft 日志
        var launchScript = ModBase.ReadFile(ModBase.exePath + @"PCL\LatestLaunch.bat");
        if (launchScript.ContainsF("-Dlog4j2.formatMsgNoLookups=false"))
            possibleLogs.Add(versionPathIndie + @"logs\debug.log"); // Minecraft Debug 日志
        possibleLogs = possibleLogs.Distinct().ToList();

        // 确定最新的日志文件
        var rightLogs = new List<string>();
        foreach (var LogFile in possibleLogs)
            try
            {
                var info = new FileInfo(LogFile);
                if (!info.Exists)
                    continue;
                var time = Math.Abs((info.LastWriteTime - DateTime.Now).TotalMinutes);
                if (time < 3d && info.Length > 0L)
                {
                    rightLogs.Add(LogFile);
                    ModBase.Log("[Crash] 可能可用的日志文件：" + LogFile + "（" + Math.Round(time, 1) + " 分钟）");
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "确认崩溃日志时间失败（" + LogFile + "）");
            }

        if (!rightLogs.Any())
            ModBase.Log("[Crash] 未发现可能可用的日志文件");

        // 将可能可用的日志文件导出
        foreach (var FilePath in rightLogs)
            try
            {
                analyzeRawFiles.Add(new KeyValuePair<string, string[]>(FilePath,
                    ModBase.ReadFile(FilePath).Split("\r\n".ToCharArray())));
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "读取可能的崩溃日志文件失败（" + FilePath + "）");
            }

        if (latestLog is not null && latestLog.Any())
        {
            var rawOutput = latestLog.Join("\r\n");
            ModBase.Log("[Crash] 以下为游戏输出的最后一段内容：" + "\r\n" + rawOutput);
            ModBase.WriteFile(tempFolder + "RawOutput.log", rawOutput);
            analyzeRawFiles.Add(new KeyValuePair<string, string[]>(tempFolder + "RawOutput.log", latestLog.ToArray()));
            latestLog.Clear();
        }

        ModBase.Log("[Crash] 步骤 1：收集日志文件完成，收集到 " + analyzeRawFiles.Count + " 个文件");
    }

    /// <summary>
    ///     从文件路径直接导入日志文件或崩溃报告压缩包。
    /// </summary>
    public void Import(string filePath)
    {
        ModBase.Log("[Crash] 步骤 1：自主导入日志文件");

        // 尝试视作压缩包解压
        try
        {
            var info = new FileInfo(filePath);
            if (info.Exists && info.Length > 0L && !filePath.EndsWithF(".jar", true))
            {
                ModBase.ExtractFile(filePath, Path.Combine(tempFolder, "Temp"));
                ModBase.Log("[Crash] 已解压导入的日志文件：" + filePath);
                goto Extracted;
            }
        }
        catch
        {
        }

        // 并非压缩包
        ModBase.CopyFile(filePath, Path.Combine(tempFolder, "Temp", ModBase.GetFileNameFromPath(filePath)));
        ModBase.Log("[Crash] 已复制导入的日志文件：" + filePath);
        Extracted: ;


        // 导入其中的日志文件
        foreach (var TargetFile in new DirectoryInfo(Path.Combine(tempFolder, "Temp")).EnumerateFiles().ToList())
            try
            {
                if (!TargetFile.Exists || TargetFile.Length == 0L)
                    continue;
                var ext = TargetFile.Extension.ToLower();
                if (ext == ".log" || ext == ".txt")
                    analyzeRawFiles.Add(new KeyValuePair<string, string[]>(TargetFile.FullName,
                        ModBase.ReadFile(TargetFile.FullName).Split("\r\n".ToCharArray())));
                else
                    File.Delete(TargetFile.FullName);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "导入单个日志文件失败");
            }

        ModBase.Log("[Crash] 步骤 1：自主导入日志文件，收集到 " + analyzeRawFiles.Count + " 个文件");
    }

    /// <summary>
    ///     从 AnalyzeRawFiles 中提取实际有用的文本片段存储到 AnalyzeFiles，并整理可用于生成报告的文件。
    ///     返回是否有足够信息可用于分析。
    /// </summary>
    public bool Prepare()
    {
        bool prepareRet = default;
        ModBase.Log("[Crash] 步骤 2：准备日志文本");

        // 对日志文件进行分类
        directFile = default;
        var allFiles = new List<KeyValuePair<AnalyzeFileType, KeyValuePair<string, string[]>>>();
        foreach (var LogFile in analyzeRawFiles)
        {
            var matchName = ModBase.GetFileNameFromPath(LogFile.Key).ToLower();
            AnalyzeFileType targetType;
            if (matchName.StartsWithF("hs_err"))
            {
                targetType = AnalyzeFileType.HsErr;
                directFile = LogFile;
            }
            else if (matchName.StartsWithF("crash-"))
            {
                targetType = AnalyzeFileType.CrashReport;
                directFile = LogFile;
            }
            else if (matchName == "latest.log" || matchName == "latest log.txt" || matchName == "debug.log" ||
                     matchName == "debug log.txt" || matchName == "游戏崩溃前的输出.txt" || matchName == "rawoutput.log")
            {
                targetType = AnalyzeFileType.MinecraftLog;
                if (directFile is null)
                    directFile = LogFile;
            }
            else if (matchName == "启动器日志.txt" || matchName == "PCL2 启动器日志.txt" || matchName == "PCL 启动器日志.txt" ||
                     matchName == "log1.txt" || matchName == "log-ce1.log")
            {
                if (LogFile.Value.Any(s => s.Contains("以下为游戏输出的最后一段内容")))
                {
                    targetType = AnalyzeFileType.MinecraftLog;
                    if (directFile is null)
                        directFile = LogFile;
                }
                else
                {
                    targetType = AnalyzeFileType.ExtraLogFile;
                }
            }
            else if (matchName.EndsWithF(".log", true))
            {
                targetType = AnalyzeFileType.ExtraLogFile;
            }
            else if (matchName.EndsWithF(".txt", true))
            {
                targetType = AnalyzeFileType.ExtraReportFile;
            }
            else
            {
                ModBase.Log("[Crash] " + matchName + " 分类为 Ignore");
                continue;
            }

            if (LogFile.Value.Any())
            {
                allFiles.Add(new KeyValuePair<AnalyzeFileType, KeyValuePair<string, string[]>>(targetType, LogFile));
                ModBase.Log("[Crash] " + matchName + " 分类为 " + ModBase.GetStringFromEnum(targetType));
            }
            else
            {
                ModBase.Log("[Crash] " + matchName + " 由于内容为空跳过");
            }
        }

        // 若只有额外日志，则将它们视作 Minecraft 日志
        if (allFiles.Any() && allFiles.All(p => p.Key == AnalyzeFileType.ExtraLogFile))
        {
            ModBase.Log("[Crash] 由于仅发现了额外日志，将它们视作 Minecraft 日志进行分析");
            allFiles = allFiles.Select(p =>
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
            var selectedFiles = new List<KeyValuePair<string, string[]>>();
            foreach (var File in allFiles)
                if (SelectType == File.Key)
                    selectedFiles.Add(File.Value);
            if (!selectedFiles.Any())
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
                        var datedFiles = new SortedList<DateTime, KeyValuePair<string, string[]>>();
                        foreach (var File in selectedFiles)
                            try
                            {
                                datedFiles.Add(new FileInfo(File.Key).LastWriteTime, File);
                            }
                            catch (Exception ex)
                            {
                                ModBase.Log(ex, "获取日志文件修改时间失败");
                                datedFiles.Add(new DateTime(1900, 1, 1), File);
                            }

                        // 输出最新的文件
                        var newestFile = datedFiles.Last().Value;
                        outputFiles.Add(newestFile.Key);
                        if (SelectType == AnalyzeFileType.HsErr)
                        {
                            logHs = GetHeadTailLines(newestFile.Value, 200, 100);
                            ModBase.Log("[Crash] 输出报告：" + newestFile.Key + "，作为虚拟机错误信息");
                            ModBase.Log("[Crash] 导入分析：" + newestFile.Key + "，作为虚拟机错误信息");
                        }
                        else
                        {
                            logCrash = GetHeadTailLines(newestFile.Value, 300, 700);
                            ModBase.Log("[Crash] 输出报告：" + newestFile.Key + "，作为 Minecraft 崩溃报告");
                            ModBase.Log("[Crash] 导入分析：" + newestFile.Key + "，作为 Minecraft 崩溃报告");
                        }

                        break;
                    }
                    case AnalyzeFileType.MinecraftLog:
                    {
                        logMc = "";
                        logMcDebug = "";
                        // 创建文件名词典
                        var fileNameDict = new Dictionary<string, KeyValuePair<string, string[]>>();
                        foreach (var SelectedFile in selectedFiles)
                        {
                            fileNameDict[ModBase.GetFileNameFromPath(SelectedFile.Key).ToLower()] = SelectedFile;
                            outputFiles.Add(SelectedFile.Key);
                            ModBase.Log("[Crash] 输出报告：" + SelectedFile.Key + "，作为 Minecraft 或启动器日志");
                        }

                        // 选择一份最佳的来自启动器的游戏日志
                        foreach (var FileName in new[]
                                 {
                                     "rawoutput.log", "启动器日志.txt", "log1.txt", "log-ce1.log", "游戏崩溃前的输出.txt",
                                     "PCL2 启动器日志.txt", "PCL 启动器日志.txt"
                                 })
                        {
                            if (!fileNameDict.ContainsKey(FileName))
                                continue;
                            var currentLog = fileNameDict[FileName];
                            // 截取 “以下为游戏输出的最后一段内容” 后的内容
                            var hasLauncherMark = false;
                            foreach (var Line in currentLog.Value)
                                if (hasLauncherMark)
                                {
                                    logMc += Line + "\n";
                                }
                                else if (Line.Contains("以下为游戏输出的最后一段内容"))
                                {
                                    hasLauncherMark = true;
                                    ModBase.Log("[Crash] 找到 PCL 输出的游戏实时日志头");
                                }

                            // 导入后 500 行
                            if (!hasLauncherMark)
                                logMc += GetHeadTailLines(currentLog.Value, 0, 500);
                            logMc = logMc.TrimEnd("\r\n".ToCharArray());
                            ModBase.Log("[Crash] 导入分析：" + currentLog.Key + "，作为启动器日志");
                            break;
                        }

                        // 选择一份最佳的 Minecraft Log
                        foreach (var FileName in new[] { "latest.log", "latest log.txt", "debug.log", "debug log.txt" })
                        {
                            if (!fileNameDict.ContainsKey(FileName))
                                continue;
                            var currentLog = fileNameDict[FileName];
                            logMc += GetHeadTailLines(currentLog.Value, 1500, 500);
                            ModBase.Log("[Crash] 导入分析：" + currentLog.Key + "，作为 Minecraft 日志");
                            break;
                        }

                        // 查找 Debug Log
                        foreach (var FileName in new[] { "debug.log", "debug log.txt" })
                        {
                            if (!fileNameDict.ContainsKey(FileName))
                                continue;
                            var currentLog = fileNameDict[FileName];
                            logMcDebug += GetHeadTailLines(currentLog.Value, 1000, 0);
                            ModBase.Log("[Crash] 导入分析：" + currentLog.Key + "，作为 Minecraft Debug 日志");
                            break;
                        }

                        // 兜底
                        if (string.IsNullOrEmpty(logMc))
                        {
                            if (!string.IsNullOrEmpty(logMcDebug)) // 如果没有找到 Minecraft 日志，则使用 Debug 日志作为兜底
                            {
                                logMc = logMcDebug;
                            }
                            else if (fileNameDict.Any()) // 如果都没有找到，则使用第一个文件
                            {
                                var currentLog = fileNameDict.First().Value;
                                logMc += GetHeadTailLines(currentLog.Value, 1500, 500);
                                ModBase.Log("[Crash] 导入分析：" + currentLog.Key + "，作为兜底日志");
                            }
                            else
                            {
                                logMc = null;
                                throw new Exception("无法找到匹配的 Minecraft Log");
                            }
                        }

                        if (string.IsNullOrEmpty(logMcDebug))
                            logMcDebug = null;
                        break;
                    }
                    case AnalyzeFileType.ExtraLogFile:
                    case AnalyzeFileType.ExtraReportFile:
                    {
                        // 全部丢过去
                        foreach (var SelectedFile in selectedFiles)
                        {
                            outputFiles.Add(SelectedFile.Key);
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
        prepareRet = logMc is not null || logHs is not null || logCrash is not null;
        if (prepareRet)
            ModBase.Log(("[Crash] 步骤 2：准备日志文本完成，找到" + (logMc is null ? "" : "游戏日志、") +
                         (logMcDebug is null ? "" : "游戏 Debug 日志、") + (logHs is null ? "" : "虚拟机日志、") +
                         (logCrash is null ? "" : "崩溃日志、")).TrimEnd('、') + "用作分析");
        else
            ModBase.Log("[Crash] 步骤 2：准备日志文本完成，没有任何可供分析的日志");

        return prepareRet;
    }

    /// <summary>
    ///     输出字符串的前后某些行，并统一行尾为 vbLf (正则 \n)、删除空行和重复行。
    /// </summary>
    private string GetHeadTailLines(string[] raw, int headLines, int tailLines)
    {
        if (raw.Length <= headLines + tailLines)
            return raw.Distinct().Join("\n");
        var lines = new List<string>();
        var realHeadLines = 0;
        int viewedLines;
        var loopTo = raw.Length - 1;
        for (viewedLines = 0; viewedLines <= loopTo; viewedLines++)
        {
            if (lines.Contains(raw[viewedLines]))
                continue;
            realHeadLines += 1;
            lines.Add(raw[viewedLines]);
            if (realHeadLines >= headLines)
                break;
        }

        var realTailLines = 0;
        for (int i = raw.Length - 1, loopTo1 = viewedLines; i >= loopTo1; i -= 1)
        {
            if (lines.Contains(raw[i]))
                continue;
            realTailLines += 1;
            lines.Insert(realHeadLines, raw[i]);
            if (realTailLines >= tailLines)
                break;
        }

        var result = new StringBuilder();
        foreach (var Line in lines)
        {
            if (string.IsNullOrEmpty(Line))
                continue;
            result.Append(Line);
            result.Append("\n");
        }

        return result.ToString();
    }

    /// <summary>
    ///     根据 AnalyzeLogs 与可能的实例信息分析崩溃原因。
    /// </summary>
    public void Analyze(McInstance version = null)
    {
        _version = version;
        ModBase.Log("[Crash] 步骤 3：分析崩溃原因");
        logAll = (logMc ?? logMcDebug ?? "") + (logHs ?? "") + (logCrash ?? "");

        // 处理 Quilt Mod Table 以避免错误分析 (CE #107)
        if (logAll.Contains("quilt") && logAll.Contains("Mod Table Version"))
        {
            ModBase.Log("[Crash] 处理 Quilt Mod Table 后再继续分析");
            var beforeTable = logAll.BeforeFirst("| Index");
            var afterTable = logAll.AfterFirst("Mod Table Version:");
            logAll = beforeTable + afterTable;
        }

        // 1. 精准日志匹配，中/高优先级
        AnalyzeCrit1();
        if (crashReasons.Any())
            goto Done;
        AnalyzeCrit2();
        if (crashReasons.Any())
            goto Done;

        // 2. 堆栈分析
        if (logAll.Contains("orge") || logAll.Contains("abric") || logAll.Contains("uilt") ||
            logAll.Contains("iteloader"))
        {
            var keywords = new List<string>();
            // 崩溃日志
            if (logCrash is not null)
            {
                ModBase.Log("[Crash] 开始进行崩溃日志堆栈分析");
                keywords.AddRange(AnalyzeStackKeyword(logCrash.BeforeFirst("System Details")));
            }

            // Minecraft 日志
            if (logMc is not null)
            {
                var fatals = logMc.RegexSearch(@"/FATAL] .+?(?=[\n]+\[)");
                if (logMc.Contains("Unreported exception thrown!"))
                    fatals.Add(logMc.Between("Unreported exception thrown!", "at oolloo.jlw.Wrapper"));
                ModBase.Log("[Crash] 开始进行 Minecraft 日志堆栈分析，发现 " + fatals.Count + " 个报错项");
                foreach (var Fatal in fatals)
                    keywords.AddRange(AnalyzeStackKeyword(Fatal));
            }

            // 虚拟机日志
            if (logHs is not null)
            {
                ModBase.Log("[Crash] 开始进行虚拟机堆栈分析");
                var stackLogs = logHs.Between("T H R E A D", "Registers:");
                keywords.AddRange(AnalyzeStackKeyword(stackLogs));
            }

            // Mod 名称分析
            if (keywords.Any())
            {
                var names = AnalyzeModName(keywords);
                if (names is null)
                    AppendReason(CrashReason.堆栈分析发现关键字, keywords);
                else
                    AppendReason(CrashReason.堆栈分析发现Mod名称, names);
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

        if (!crashReasons.Any())
        {
            ModBase.Log("[Crash] 步骤 3：分析崩溃原因完成，未找到可能的原因");
        }
        else
        {
            ModBase.Log("[Crash] 步骤 3：分析崩溃原因完成，找到 " + crashReasons.Count + " 条可能的原因");
            foreach (var Reason in crashReasons)
                ModBase.Log("[Crash]  - " + ModBase.GetStringFromEnum(Reason.Key) +
                            (Reason.Value.Any() ? "（" + Reason.Value.Join("；") + "）" : ""));
        }
    }

    /// <summary>
    ///     增加一个可能的崩溃原因。
    /// </summary>
    private void AppendReason(CrashReason reason, ICollection<string> additional = null)
    {
        if (crashReasons.ContainsKey(reason))
        {
            if (additional is not null)
            {
                crashReasons[reason].AddRange(additional);
                crashReasons[reason] = crashReasons[reason].Distinct().ToList();
            }
        }
        else
        {
            crashReasons.Add(reason, new List<string>(additional ?? Array.Empty<string>()));
        }

        ModBase.Log("[Crash] 可能的崩溃原因：" + ModBase.GetStringFromEnum(reason) +
                    (additional is not null && additional.Any() ? "（" + additional.Join("；") + "）" : ""));
    }

    private void AppendReason(CrashReason reason, string additional)
    {
        AppendReason(reason, string.IsNullOrEmpty(additional) ? null : new List<string> { additional });
    }

    // 具体的分析代码
    /// <summary>
    ///     进行精准日志匹配。匹配优先级高于堆栈分析的崩溃。
    /// </summary>
    private void AnalyzeCrit1()
    {
        // 空白分析
        if (logMc is null && logHs is null && logCrash is null)
        {
            AppendReason(CrashReason.没有可用的分析文件);
            return;
        }

        // 崩溃报告分析，高优先级
        if (logCrash is not null)
            if (logCrash.Contains("Unable to make protected final java.lang.Class java.lang.ClassLoader.defineClass"))
                AppendReason(CrashReason.Java版本过高);

        // 游戏日志分析
        if (logMc is not null)
        {
            if (logMc.Contains("Found multiple arguments for option fml.forgeVersion, but you asked for only one"))
                AppendReason(CrashReason.实例Json中存在多个Forge);
            if (logMc.Contains("The driver does not appear to support OpenGL"))
                AppendReason(CrashReason.显卡不支持OpenGL);
            if (logMc.Contains("java.lang.ClassCastException: java.base/jdk"))
                AppendReason(CrashReason.使用JDK);
            if (logMc.Contains("java.lang.ClassCastException: class jdk."))
                AppendReason(CrashReason.使用JDK);
            if (logMc.Contains("TRANSFORMER/net.optifine/net.optifine.reflect.Reflector.<clinit>(Reflector.java"))
                AppendReason(CrashReason.OptiFine与Forge不兼容);
            if (logMc.Contains(
                    "java.lang.NoSuchMethodError: 'void net.minecraft.client.renderer.texture.SpriteContents.<init>"))
                AppendReason(CrashReason.OptiFine与Forge不兼容);
            if (logMc.Contains(
                    "java.lang.NoSuchMethodError: 'java.lang.String com.mojang.blaze3d.systems.RenderSystem.getBackendDescription"))
                AppendReason(CrashReason.OptiFine与Forge不兼容);
            if (logMc.Contains(
                    "java.lang.NoSuchMethodError: 'void net.minecraft.client.renderer.block.model.BakedQuad.<init>"))
                AppendReason(CrashReason.OptiFine与Forge不兼容);
            if (logMc.Contains(
                    "java.lang.NoSuchMethodError: 'void net.minecraftforge.client.gui.overlay.ForgeGui.renderSelectedItemName"))
                AppendReason(CrashReason.OptiFine与Forge不兼容);
            if (logMc.Contains("java.lang.NoSuchMethodError: 'void net.minecraft.server.level.DistanceManager"))
                AppendReason(CrashReason.OptiFine与Forge不兼容);
            if (logMc.Contains(
                    "java.lang.NoSuchMethodError: 'net.minecraft.network.chat.FormattedText net.minecraft.client.gui.Font.ellipsize"))
                AppendReason(CrashReason.OptiFine与Forge不兼容);
            if (logMc.Contains("Open J9 is not supported") || logMc.Contains("OpenJ9 is incompatible") ||
                logMc.Contains(".J9VMInternals."))
                AppendReason(CrashReason.使用OpenJ9);
            if (logMc.Contains("java.lang.NoSuchFieldException: ucp"))
                AppendReason(CrashReason.Java版本过高);
            if (logMc.Contains("because module java.base does not export"))
                AppendReason(CrashReason.Java版本过高);
            if (logMc.Contains(
                    "java.lang.ClassNotFoundException: jdk.nashorn.api.scripting.NashornScriptEngineFactory"))
                AppendReason(CrashReason.Java版本过高);
            if (logMc.Contains("java.lang.ClassNotFoundException: java.lang.invoke.LambdaMetafactory"))
                AppendReason(CrashReason.Java版本过高);
            if (logMc.Contains("The directories below appear to be extracted jar files. Fix this before you continue."))
                AppendReason(CrashReason.Mod文件被解压);
            if (logMc.Contains("Extracted mod jars found, loading will NOT continue"))
                AppendReason(CrashReason.Mod文件被解压);
            if (logMc.Contains("java.lang.ClassNotFoundException: org.spongepowered.asm.launch.MixinTweaker"))
                AppendReason(CrashReason.MixinBootstrap缺失);
            if (logMc.Contains("Couldn't set pixel format"))
                AppendReason(CrashReason.显卡驱动不支持导致无法设置像素格式);
            if (logMc.Contains("java.lang.OutOfMemoryError") || logMc.Contains("an out of memory error"))
                AppendReason(CrashReason.内存不足);
            if (logMc.Contains(
                    "java.lang.RuntimeException: Shaders Mod detected. Please remove it, OptiFine has built-in support for shaders."))
                AppendReason(CrashReason.ShadersMod与OptiFine同时安装);
            if (logMc.Contains("java.lang.NoSuchMethodError: sun.security.util.ManifestEntryVerifier"))
                AppendReason(CrashReason.低版本Forge与高版本Java不兼容);
            if (logMc.Contains("1282: Invalid operation"))
                AppendReason(CrashReason.光影或资源包导致OpenGL1282错误);
            if (logMc.Contains("signer information does not match signer information of other classes in the same package"))
                AppendReason(CrashReason.文件或内容校验失败,
                    (logMc.RegexSeek("(?<=class \")[^']+(?=\"'s signer information)") ?? "").TrimEnd('\r', '\n'));
            if (logMc.Contains("Maybe try a lower resolution resourcepack?"))
                AppendReason(CrashReason.材质过大或显卡配置不足);
            if (logMc.Contains(
                    "java.lang.NoSuchMethodError: net.minecraft.world.server.ChunkManager$ProxyTicketManager.shouldForceTicks(J)Z") &&
                logMc.Contains("OptiFine"))
                AppendReason(CrashReason.OptiFine导致无法加载世界);
            if (logMc.Contains("Unsupported class file major version"))
                AppendReason(CrashReason.Java版本不兼容);
            if (logMc.Contains("com.electronwill.nightconfig.core.io.ParsingException: Not enough data available"))
                AppendReason(CrashReason.NightConfig的Bug);
            if (logMc.Contains("Cannot find launch target fmlclient, unable to launch"))
                AppendReason(CrashReason.Forge安装不完整);
            if (logMc.Contains("Invalid paths argument, contained no existing paths") &&
                logMc.Contains(@"libraries\net\minecraftforge\fmlcore"))
                AppendReason(CrashReason.Forge安装不完整);
            if (logMc.Contains("Invalid module name: '' is not a Java identifier"))
                AppendReason(CrashReason.Mod名称包含特殊字符);
            if (logMc.Contains(
                    "has been compiled by a more recent version of the Java Runtime (class file version 55.0), this version of the Java Runtime only recognizes class file versions up to"))
                AppendReason(CrashReason.Mod需要Java11);
            if (logMc.Contains(
                    "java.lang.RuntimeException: java.lang.NoSuchMethodException: no such method: sun.misc.Unsafe.defineAnonymousClass(Class,byte[],Object[])Class/invokeVirtual"))
                AppendReason(CrashReason.Mod需要Java11);
            if (logMc.Contains(
                    "java.lang.IllegalArgumentException: The requested compatibility level JAVA_11 could not be set. Level is not supported by the active JRE or ASM version"))
                AppendReason(CrashReason.Mod需要Java11);
            if (logMc.Contains("Unsupported major.minor version"))
                AppendReason(CrashReason.Java版本不兼容);
            if (logMc.Contains("Invalid maximum heap size"))
                AppendReason(CrashReason.使用32位Java导致JVM无法分配足够多的内存);
            if (logMc.Contains("Could not reserve enough space"))
            {
                if (logMc.Contains("for 1048576KB object heap"))
                    AppendReason(CrashReason.使用32位Java导致JVM无法分配足够多的内存);
                else
                    AppendReason(CrashReason.内存不足);
            }

            // 确定的 Mod 导致崩溃
            if (logMc.Contains("Caught exception from "))
                AppendReason(CrashReason.确定Mod导致游戏崩溃,
                    TryAnalyzeModName(logMc.RegexSeek(@"(?<=Caught exception from )[^\n]+?")
                        ?.TrimEnd(("\r\n" + " ").ToCharArray())));
            // Mod 重复 / 前置问题
            if (logMc.Contains("DuplicateModsFoundException"))
                AppendReason(CrashReason.Mod重复安装,
                    logMc.RegexSearch(@"(?<=\n\t[\w]+ : [A-Z]:[^\n]+(/|\\))[^/\\\n]+?.jar", RegexOptions.IgnoreCase));
            if (logMc.Contains("Found a duplicate mod"))
                AppendReason(CrashReason.Mod重复安装,
                    (logMc.RegexSeek(@"Found a duplicate mod[^\n]+") ?? "").RegexSearch(@"[^\\/]+.jar",
                        RegexOptions.IgnoreCase));
            if (logMc.Contains("Found duplicate mods"))
                AppendReason(CrashReason.Mod重复安装,
                    logMc.RegexSearch(@"(?<=Mod ID: ')\w+?(?=' from mod files:)").Distinct().ToList());
            if (logMc.Contains("ModResolutionException: Duplicate"))
                AppendReason(CrashReason.Mod重复安装,
                    (logMc.RegexSeek(@"ModResolutionException: Duplicate[^\n]+") ?? "").RegexSearch(@"[^\\/]+.jar",
                        RegexOptions.IgnoreCase));
            if (logMc.Contains("Incompatible mods found!")) // #5006
                AppendReason(CrashReason.Mod互不兼容,
                    logMc.RegexSeek(@"(?<=Incompatible mods found![\s\S]+: )[\s\S]+?(?=\tat )") ?? "");
            if (logMc.Contains("Missing or unsupported mandatory dependencies:"))
                AppendReason(CrashReason.Mod缺少前置或MC版本错误,
                    logMc.RegexSearch(@"(?<=Missing or unsupported mandatory dependencies:)([\n\r]+\t(.*))+",
                            RegexOptions.IgnoreCase)
                        .Select(s => s.Trim(("\r\n\t ").ToCharArray())).Distinct()
                        .ToList());
        }

        // 虚拟机日志分析
        if (logHs is not null)
        {
            if (logHs.Contains("The system is out of physical RAM or swap space"))
                AppendReason(CrashReason.内存不足);
            if (logHs.Contains("Out of Memory Error"))
                AppendReason(CrashReason.内存不足);
            if (logHs.Contains("EXCEPTION_ACCESS_VIOLATION"))
            {
                if (logHs.Contains("# C  [ig"))
                    AppendReason(CrashReason.Intel驱动不兼容导致EXCEPTION_ACCESS_VIOLATION);
                if (logHs.Contains("# C  [atio"))
                    AppendReason(CrashReason.AMD驱动不兼容导致EXCEPTION_ACCESS_VIOLATION);
                if (logHs.Contains("# C  [nvoglv"))
                    AppendReason(CrashReason.Nvidia驱动不兼容导致EXCEPTION_ACCESS_VIOLATION);
            }
        }

        // 崩溃报告分析
        if (logCrash is not null)
        {
            if (logCrash.Contains("maximum id range exceeded"))
                AppendReason(CrashReason.Mod过多导致超出ID限制);
            if (logCrash.Contains("java.lang.OutOfMemoryError"))
                AppendReason(CrashReason.内存不足);
            if (logCrash.Contains("Pixel format not accelerated"))
                AppendReason(CrashReason.显卡驱动不支持导致无法设置像素格式);
            if (logCrash.Contains("Manually triggered debug crash"))
                AppendReason(CrashReason.玩家手动触发调试崩溃);
            if (logCrash.Contains("has mods that were not found") &&
                logCrash.RegexCheck(@"The Mod File [^\n]+optifine\\OptiFine[^\n]+ has mods that were not found"))
                AppendReason(CrashReason.OptiFine与Forge不兼容);
            // Mod 导致的崩溃
            if (logCrash.Contains("-- MOD "))
            {
                var logCrashMod = logCrash.Between("-- MOD ", "Failure message:");
                if (logCrashMod.ContainsF(".jar", true))
                    AppendReason(CrashReason.确定Mod导致游戏崩溃,
                        (logCrashMod.RegexSeek("(?<=Mod File: ).+") ?? "").TrimEnd(
                            ("\r\n" + " ").ToCharArray()));
                else
                    AppendReason(CrashReason.Mod加载器报错,
                        (logCrash.RegexSeek(@"(?<=Failure message: )[\w\W]+?(?=\tMod)") ?? "")
                        .Replace("\t", " ").TrimEnd(("\r\n" + " ").ToCharArray()));
            }

            if (logCrash.Contains("Multiple entries with same key: "))
                AppendReason(CrashReason.确定Mod导致游戏崩溃,
                    TryAnalyzeModName(
                        (logCrash.RegexSeek("(?<=Multiple entries with same key: )[^=]+") ?? "").TrimEnd(
                            ("\r\n" + " ").ToCharArray())));
            if (logCrash.Contains("LoaderExceptionModCrash: Caught exception from "))
                AppendReason(CrashReason.确定Mod导致游戏崩溃,
                    TryAnalyzeModName(
                        (logCrash.RegexSeek(@"(?<=LoaderExceptionModCrash: Caught exception from )[^\n]+") ?? "")
                        .TrimEnd(("\r\n" + " ").ToCharArray())));
            if (logCrash.Contains("Failed loading config file "))
                AppendReason(CrashReason.Mod配置文件导致游戏崩溃,
                    new[]
                    {
                        TryAnalyzeModName(
                            (logCrash.RegexSeek(@"(?<=Failed loading config file .+ for modid )[^\n]+") ?? "").TrimEnd('\r', '\n')).First(),
                        (logCrash.RegexSeek("(?<=Failed loading config file ).+(?= of type)") ?? "").TrimEnd('\r', '\n')
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
        bool MixinAnalyze(string logText)
        {
            var isMixin = logText.Contains("Mixin prepare failed ") || logText.Contains("Mixin apply failed ") ||
                          logText.Contains("MixinApplyError") || logText.Contains("MixinTransformerError") ||
                          logText.Contains("mixin.injection.throwables.") || logText.Contains(".json] FAILED during )");
            if (!isMixin)
                return false;
            // Mod 名称匹配
            var modName = logText.RegexSeek(@"(?<=from mod )[^.\/ ]+(?=\] from)");
            if (modName is null)
                modName = logText.RegexSeek(@"(?<=for mod )[^.\/ ]+(?= failed)");
            if (modName is not null)
            {
                AppendReason(CrashReason.ModMixin失败,
                    TryAnalyzeModName(modName.TrimEnd(("\r\n" + " ").ToCharArray())));
                return true;
            }

            // JSON 名称匹配
            foreach (var JsonName in logText.RegexSearch(@"(?<=^[^\t]+[ \[{(]{1})[^ \[{(]+\.[^ ]+(?=\.json)",
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
        if (logMc is not null)
        {
            // Mixin 崩溃
            var isMixin = MixinAnalyze(logMc);
            // 常规信息
            if (logMc.Contains("An exception was thrown, the game will display an error screen and halt."))
                AppendReason(CrashReason.Forge报错,
                    (logMc.RegexSeek(@"(?<=the game will display an error screen and halt.[\n\r]+[^\n]+?Exception: )[\s\S]+?(?=\n\tat)")?.Trim('\r', '\n')) ?? "");
            if (logMc.Contains("A potential solution has been determined:"))
                AppendReason(CrashReason.Fabric报错并给出解决方案,
                    (logMc.RegexSeek(@"(?<=A potential solution has been determined:\n)(\s+ - [^\n]+\n)+") ?? "")
                    .RegexSearch(@"(?<=\s+)[^\n]+").Join("\n"));
            if (logMc.Contains("A potential solution has been determined, this may resolve your problem:"))
                AppendReason(CrashReason.Fabric报错并给出解决方案,
                    (logMc.RegexSeek(
                         @"(?<=A potential solution has been determined, this may resolve your problem:\n)(\s+ - [^\n]+\n)+") ??
                     "").RegexSearch(@"(?<=\s+)[^\n]+").Join("\n"));
            if (logMc.Contains("确定了一种可能的解决方法，这样做可能会解决你的问题："))
                AppendReason(CrashReason.Fabric报错并给出解决方案,
                    (logMc.RegexSeek(@"(?<=确定了一种可能的解决方法，这样做可能会解决你的问题：\n)(\s+ - [^\n]+\n)+") ?? "")
                    .RegexSearch(@"(?<=\s+)[^\n]+").Join("\n"));
            if (!isMixin &&
                logMc.Contains(
                    "due to errors, provided by ")) // 在 #3104 的情况下，这一句导致 OptiFabric 的 Mixin 失败错判为 Fabric Loader 加载失败
                AppendReason(CrashReason.确定Mod导致游戏崩溃,
                    TryAnalyzeModName(
                        (logMc.RegexSeek("(?<=due to errors, provided by ')[^']+") ?? "").TrimEnd(
                            ("\r\n" + " ").ToCharArray())));
        }

        // 崩溃报告分析
        if (logCrash is not null)
        {
            // Mixin 崩溃
            MixinAnalyze(logCrash);
            // 常规信息
            if (logCrash.Contains("Suspected Mod"))
            {
                var suspectsRaw = logCrash.Between("Suspected Mod", "Stacktrace");
                if (!suspectsRaw.StartsWithF("s: None")) // Suspected Mods: None
                {
                    var suspects = suspectsRaw.RegexSearch(@"(?<=\n\t[^(\t]+\()[^)\n]+");
                    if (suspects.Any())
                        AppendReason(CrashReason.怀疑Mod导致游戏崩溃, TryAnalyzeModName(suspects));
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
        if (logMc is not null)
        {
            // 极短的程序输出
            if (!(logMc.Contains("at net.") || logMc.Contains("INFO]")) && logHs is null && logCrash is null &&
                logMc.Length < 100) AppendReason(CrashReason.极短的程序输出, logMc);
            // Mod 解析错误（常见于 Fabric 前置校验失败）
            if (logMc.Contains("Mod resolution failed"))
                AppendReason(CrashReason.Mod加载器报错);
            // Mixin 失败可以导致大量 Mod 实例创建失败
            if (logMc.Contains("Failed to create mod instance."))
                AppendReason(CrashReason.Mod初始化失败,
                    TryAnalyzeModName(
                        (logMc.RegexSeek("(?<=Failed to create mod instance. ModID: )[^,]+") ??
                         logMc.RegexSeek(@"(?<=Failed to create mod instance. ModId )[^\n]+(?= for )") ?? "")
                        .TrimEnd('\r', '\n')));
            // 注意：Fabric 的 Warnings were found! 不一定是崩溃原因，它可能是单纯的警报
        }

        // 崩溃报告分析
        if (logCrash is not null)
        {
            if (logCrash.Contains("\tBlock location: World: "))
                AppendReason(CrashReason.特定方块导致崩溃,
                    (logCrash.RegexSeek(@"(?<=\tBlock: Block\{)[^\}]+") ?? "") + " " +
                    (logCrash.RegexSeek(@"(?<=\tBlock location: World: )\([^\)]+\)") ?? ""));
            if (logCrash.Contains("\tEntity's Exact location: "))
                AppendReason(CrashReason.特定实体导致崩溃,
                    (logCrash.RegexSeek(@"(?<=\tEntity Type: )[^\n]+(?= \()") ?? "") + " (" +
                    (logCrash.RegexSeek(@"(?<=\tEntity's Exact location: )[^\n]+") ?? "").TrimEnd(
                        "\r\n".ToCharArray()) + ")");
        }
    }

    /// <summary>
    ///     从堆栈中提取 Mod ID 关键字。若失败则返回空列表。
    /// </summary>
    private List<string> AnalyzeStackKeyword(string errorStack)
    {
        errorStack = "\n" + (errorStack ?? "") + "\n";

        // 进行正则匹配
        var stackSearchResults = new List<string>();
        stackSearchResults.AddRange(
            errorStack.RegexSearch(@"(?<=\n[^{]+)[a-zA-Z_]+\w+\.[a-zA-Z_]+[\w\.]+(?=\.[\w\.$]+\.)"));
        stackSearchResults.AddRange(errorStack.RegexSearch(@"(?<=at [^(]+?\.\w+\$\w+\$)[\w\$]+?(?=\$\w+\()")
            .Select(s => s.Replace("$", "."))); // Mixin 堆栈：xxx.xxx.xxxx$xxxx$xxx
        stackSearchResults = stackSearchResults.Distinct().ToList();

        // 检查堆栈开头
        var possibleStacks = new List<string>();
        foreach (var Stack in stackSearchResults)
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
            possibleStacks.Add(Stack.Trim());
            NextStack: ;
        }

        possibleStacks = possibleStacks.Distinct().ToList();

        ModBase.Log("[Crash] 找到 " + possibleStacks.Count + " 条可能的堆栈信息");
        if (!possibleStacks.Any())
            return new List<string>();
        foreach (var Stack in possibleStacks)
            ModBase.Log("[Crash]  - " + Stack);

        // 检查堆栈关键词
        var possibleWords = new List<string>();
        foreach (var Stack in possibleStacks)
        {
            var splited = Stack.Split(".");
            for (int i = 0, loopTo = Math.Min(3, splited.Count() - 1); i <= loopTo; i++) // 最多取前 4 节
            {
                var word = splited[i];
                if (word.Length <= 2 || word.StartsWithF("func_"))
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
                    }.Contains(word.ToLower()))
                    continue;
                possibleWords.Add(word.Trim());
            }
        }

        possibleWords = possibleWords.Distinct().ToList();
        ModBase.Log("[Crash] 从堆栈信息中找到 " + possibleWords.Count + " 个可能的 Mod ID 关键词");
        if (possibleWords.Any())
            ModBase.Log("[Crash]  - " + possibleWords.Join(", "));
        if (possibleWords.Count > 10)
        {
            ModBase.Log("[Crash] 关键词过多，考虑匹配出错，不纳入考虑");
            return new List<string>();
        }

        return possibleWords;
    }

    /// <summary>
    ///     根据 Mod 关键词尝试获取实际的 Mod 名称。
    ///     若失败则返回 Nothing。
    /// </summary>
    private List<string> AnalyzeModName(List<string> keywords)
    {
        var modFileNames = new List<string>();

        // 预处理关键词（分割括号）
        var realKeywords = new List<string>();
        foreach (var Keyword in keywords)
        foreach (var SubKeyword in Keyword.Split("("))
            realKeywords.Add(SubKeyword.Trim(" )".ToCharArray()));
        keywords = realKeywords;

        // 从崩溃报告获取 Mod 信息
        if (logCrash is not null && logCrash.Contains("A detailed walkthrough of the error"))
        {
            var details = logCrash.Replace("A detailed walkthrough of the error", "¨");
            var isFabricDetail = details.Contains("Fabric Mods"); // 是否为 Fabric 信息格式
            if (isFabricDetail)
            {
                details = details.Replace("Fabric Mods", "¨");
                ModBase.Log("[Crash] 崩溃报告中检测到 Fabric Mod 信息格式");
            }

            var isQuiltDetail = details.Contains("quilt-loader");
            if (isQuiltDetail)
            {
                details = details.Replace("Mod Table Version", "¨");
                ModBase.Log("[Crash] 崩溃报告中检测到 Quilt Mod 信息格式");
            }

            details = details.AfterLast("¨");

            // [Forge] 获取所有包含 .jar 的行
            // [Fabric] 获取所有包含 Mod 信息的行
            var modNameLines = new List<string>();
            foreach (var Line in details.Split("\n"))
                if ((Line.ContainsF(".jar", true) && Line.Length - Line.Replace(".jar", "").Length == 4) ||
                    (isFabricDetail && Line.StartsWithF("\t\t") &&
                     !Line.RegexCheck(@"\t\tfabric[\w-]*: Fabric"))) // 只有一个 .jar
                    modNameLines.Add(Line);
            ModBase.Log("[Crash] 崩溃报告中找到 " + modNameLines.Count + " 个可能的 Mod 项目行");

            // 获取 Mod ID 与关键词的匹配行
            var hintLines = new List<string>();
            foreach (var KeyWord in keywords)
            foreach (var ModString in modNameLines)
            {
                var realModString = ModString.ToLower().Replace("_", "");
                if (!realModString.Contains(KeyWord.ToLower().Replace("_", "")))
                    continue;
                if (realModString.Contains("minecraft.jar") || realModString.Contains(" forge-") ||
                    realModString.Contains(" mixin-"))
                    continue;
                hintLines.Add(ModString.Trim("\r\n".ToCharArray()));
                break;
            }

            hintLines = hintLines.Distinct().ToList();
            ModBase.Log("[Crash] 崩溃报告中找到 " + hintLines.Count + " 个可能的崩溃 Mod 匹配行");
            foreach (var ModLine in hintLines)
                ModBase.Log("[Crash]  - " + ModLine);

            // 从 Mod 匹配行中提取 .jar 文件的名称
            foreach (var Line in hintLines)
            {
                string name;
                if (isFabricDetail)
                    name = Line.RegexSeek(@"(?<=: )[^\n]+(?= [^\n]+)");
                else
                    name = Line.RegexSeek(@"(?<=\()[^\t]+.jar(?=\))|(?<=(\t\t)|(\| ))[^\t\|]+.jar",
                        RegexOptions.IgnoreCase);
                if (name is not null)
                    modFileNames.Add(name);
            }
        }

        // 从 debug.log 获取 Mod 信息
        if (logMcDebug is not null)
        {
            // Forge: Found valid mod file YungsBetterStrongholds-1.20-Forge-4.0.1.jar with {betterstrongholds} mods - versions {1.20-Forge-4.0.1}
            var modNameLines = logMcDebug.RegexSearch("(?<=valid mod file ).*", RegexOptions.Multiline);
            ModBase.Log("[Crash] Debug 信息中找到 " + modNameLines.Count + " 个可能的 Mod 项目行");

            // 获取 Mod ID 与关键词的匹配行
            var hintLines = new List<string>();
            foreach (var KeyWord in keywords)
            foreach (var ModString in modNameLines)
                if (ModString.Contains($"{{{KeyWord}}}"))
                    hintLines.Add(ModString);

            hintLines = hintLines.Distinct().ToList();
            ModBase.Log("[Crash] Debug 信息中找到 " + hintLines.Count + " 个可能的崩溃 Mod 匹配行");
            foreach (var ModLine in hintLines)
                ModBase.Log("[Crash]  - " + ModLine);

            // 从 Mod 匹配行中提取 .jar 文件的名称
            foreach (var Line in hintLines)
            {
                string name;
                name = Line.RegexSeek(".*(?= with)");
                if (name is not null)
                    modFileNames.Add(name);
            }
        }

        // 输出
        modFileNames = modFileNames.Distinct().ToList();
        if (!modFileNames.Any()) return null;

        ModBase.Log("[Crash] 找到 " + modFileNames.Count + " 个可能的崩溃 Mod 文件名");
        foreach (var ModFileName in modFileNames)
            ModBase.Log("[Crash]  - " + ModFileName);
        return modFileNames;
    }

    /// <summary>
    ///     尝试从关键字获取 Mod 名称，若失败则返回原关键字。
    /// </summary>
    private List<string> TryAnalyzeModName(string keyword)
    {
        var rawList = new List<string> { keyword ?? "" };
        if (string.IsNullOrEmpty(keyword))
            return rawList;
        return AnalyzeModName(rawList) ?? rawList;
    }

    /// <summary>
    ///     尝试从关键字获取 Mod 名称，若失败则返回原关键字。
    /// </summary>
    private List<string> TryAnalyzeModName(List<string> keywords)
    {
        if (!keywords.Any())
            return keywords;
        return AnalyzeModName(keywords) ?? keywords;
    }

    /// <summary>
    ///     弹出崩溃弹窗，并指导导出崩溃报告。
    /// </summary>
    public void Output(bool isHandAnalyze, List<string> extraFiles = null)
    {
        // 弹窗提示
        ModMain.frmMain.ShowWindowToTop();
        var resultText = GetAnalyzeResult(isHandAnalyze);
        // 确定是否是加载器版本不兼容问题
        var isModLoaderIncompatible = _version is not null && resultText.StartsWith("Mod 加载器版本与 Mod 不兼容");
        // 弹窗选择：查看日志
        switch (ModMain.MyMsgBox(resultText, isHandAnalyze ? "错误报告分析结果" : "Minecraft 出现错误", Lang.Text("Common.Action.Confirm"),
                    isHandAnalyze || directFile is null ? "" : isModLoaderIncompatible ? "前往修改" : "查看日志",
                    isHandAnalyze ? "" : "导出错误报告",
                    button2Action: isHandAnalyze || directFile is null || isModLoaderIncompatible
                        ? null
                        : new Action(() =>
                        {
                            if (File.Exists(directFile.Value.Key))
                            {
                                ModBase.ShellOnly(directFile.Value.Key);
                            }
                            else
                            {
                                var filePath = Path.Combine(ModBase.pathTemp, "Crash.txt");
                                ModBase.WriteFile(filePath, directFile.Value.Value.Join("\r\n"));
                                ModBase.ShellOnly(filePath);
                            }
                        })))
        {
            case 2:
            {
                // 弹窗选择：前往修改
                PageInstanceLeft.McInstance = _version;
                ModBase.RunInUi(() =>
                    ModMain.frmMain.PageChange(FormMain.PageType.InstanceSetup, FormMain.PageSubType.VersionInstall));
                break;
            }
            case 3:
            {
                // 弹窗选择：导出错误报告
                string fileAddress = null;
                try
                {
                    // 获取文件路径
                    ModBase.RunInUiWait(() => fileAddress = SystemDialogs.SelectSaveFile("选择保存位置",
                        "错误报告-" + DateTime.Now.ToString("G", CultureInfo.InvariantCulture).Replace("/", "-").Replace(":", ".").Replace(" ", "_") +
                        ".zip", "Minecraft 错误报告(*.zip)|*.zip"));
                    if (string.IsNullOrEmpty(fileAddress))
                        return;
                    Directory.CreateDirectory(ModBase.GetPathFromFullPath(fileAddress));
                    if (File.Exists(fileAddress))
                        File.Delete(fileAddress);
                    // 输出诊断信息
                    ModBase.FeedbackInfo();
                    // 复制文件
                    if (extraFiles is not null)
                        outputFiles.AddRange(extraFiles);
                    foreach (var OutputFile in outputFiles)
                    {
                        var fileName = ModBase.GetFileNameFromPath(OutputFile);
                        Encoding fileEncoding = null;
                        switch (fileName ?? "")
                        {
                            case "LatestLaunch.bat":
                            {
                                fileName = "启动脚本.bat";
                                break;
                            }
                            case "RawOutput.log":
                            {
                                fileName = "游戏崩溃前的输出.txt";
                                fileEncoding = Encoding.UTF8;
                                break;
                            }
                        }

                        if (LogWrapper.CurrentLogger.CurrentLogFiles.Last().AfterLast(@"\") == fileName)
                        {
                            fileName = "PCL 启动器日志.txt";
                            fileEncoding = Encoding.UTF8;
                        }

                        if (File.Exists(OutputFile))
                        {
                            if (fileEncoding is null)
                                fileEncoding = EncodingDetector.DetectEncoding(ModBase.ReadFileBytes(OutputFile));
                            var fileContent = ModBase.ReadFile(OutputFile, fileEncoding);
                            fileContent = McLogFilter.FilterAccessToken(fileContent,
                                fileName == "启动脚本.bat" ? 'F' : '*');
                            fileContent = McLogFilter.FilterUserName(fileContent, '*');
                            ModBase.WriteFile(Path.Combine(tempFolder, "Report", fileName), fileContent, encoding: fileEncoding);
                            ModBase.Log($"[Crash] 导出文件：{fileName}，编码：{fileEncoding.HeaderName}");
                        }
                    }

                    // 输出环境与启动信息
                    var mcLauncherLog = ModBase.ReadFile(Path.Combine(tempFolder, "Report", "PCL 启动器日志.txt"))
                        .AfterLast("[Launch] ~ 基础参数 ~").BeforeFirst("开始 Minecraft 日志监控");
                    var launchScript = ModBase.ReadFile(Path.Combine(tempFolder, "Report", "启动脚本.bat"));
                    var envInfo = new StringBuilder();
                    envInfo.Append($"PCL CE 版本：{ModBase.versionBaseName}\r\n");
                    envInfo.Append($"识别码：{Identify.LauncherId}\r\n");
                    envInfo.Append($"\r\n- 档案信息 -\r\n");
                    envInfo.Append(
                        $"档案名称：{mcLauncherLog.Between("玩家用户名：", "[").TrimEnd('[').Trim()} (验证方式：{mcLauncherLog.Between("验证方式：", "[").TrimEnd('[').Trim()})\r\n");
                    envInfo.Append($"\r\n- 实例信息 -\r\n");
                    envInfo.Append(
                        $"选定的 Java 虚拟机：{mcLauncherLog.Between("Java 信息：", "[").TrimEnd('[').Trim()}\r\n");
                    envInfo.Append(
                        $"Log4j2 NoLookups：{!launchScript.ContainsF("-Dlog4j2.formatMsgNoLookups=false")}\r\n");
                    envInfo.Append($"MC 文件夹：{mcLauncherLog.Between("MC 文件夹：", "[").TrimEnd('[').Trim()}\r\n");
                    envInfo.Append($"\r\n- 环境信息 -\r\n");
                    envInfo.Append(
                        $"操作系统：{SystemInfo.OSInfo}（64 位：{!SystemInfo.Is32BitSystem}, ARM64: {SystemInfo.IsArm64System}）\r\n");
                    envInfo.Append($"CPU：{HardwareInfo.CPUName}\r\n");
                    envInfo.Append(
                        $"内存分配 (分配的内存 / 已安装物理内存)：{mcLauncherLog.Between("分配的内存：", "[").TrimEnd('[').Trim()} / {Lang.Number(HardwareInfo.SystemMemorySize / 1024d, "N2")} GB ({Lang.Number(HardwareInfo.SystemMemorySize, "N0")} MB)\r\n");
                    for (int i = 0; i < HardwareInfo.GPUs.Count; i++)
                    {
                        var gPU = HardwareInfo.GPUs[i];
                        envInfo.Append(
                            $"显卡 {i}：{gPU.Name} ({(gPU.Memory >= 4095L ? ">= " + gPU.Memory : gPU.Memory)} MB, {gPU.DriverVersion})");
                        envInfo.Append("\r\n");
                    }

                    File.CreateText(Path.Combine(tempFolder, "Report", "环境与启动信息.txt")).Close();
                    ModBase.WriteFile(Path.Combine(tempFolder, "Report", "环境与启动信息.txt"), envInfo.ToString(), encoding: Encoding.UTF8);
                    // 导出报告
                    ZipFile.CreateFromDirectory(Path.Combine(tempFolder, "Report"), fileAddress);
                    ModBase.DeleteDirectory(Path.Combine(tempFolder, "Report"));
                    ModMain.Hint("错误报告已导出！", ModMain.HintType.Finish);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "导出错误报告失败", ModBase.LogLevel.Feedback);
                    return;
                }

                ModBase.OpenExplorer(fileAddress);
                break;
            }
        }
    }

    /// <summary>
    ///     获取崩溃分析的结果描述。
    /// </summary>
    private string GetAnalyzeResult(bool isHandAnalyze)
    {
        // 没有结果的处理
        if (!crashReasons.Any())
        {
            if (isHandAnalyze) return "很抱歉，PCL 无法确定错误原因。";

            return $"很抱歉，你的游戏出现了一些问题……{"\r\n"}如果要寻求帮助，请把错误报告文件发给对方，而不是发送这个窗口的照片或者截图。";
        }

        // 根据不同原因判断
        var results = new List<string>();
        const string loaderIncompatibleResultText = @"Mod 加载器版本与 Mod 不兼容，请前往 实例设置 - 修改 更换加载器版本。\n\n详细信息：\n";
        foreach (var Reason in crashReasons)
        {
            var additional = Reason.Value;
            switch (Reason.Key)
            {
                case CrashReason.Mod文件被解压:
                {
                    results.Add(
                        @"由于 Mod 文件被解压了，导致游戏无法继续运行。\n直接把整个 Mod 文件放进 Mod 文件夹中即可，若解压就会导致游戏出错。\n\n请删除 Mod 文件夹中已被解压的 Mod，然后再启动游戏。");
                    break;
                }
                case CrashReason.内存不足:
                {
                    results.Add(
                        @"Minecraft 内存不足，导致其无法继续运行。\n这很可能是因为电脑内存不足、游戏分配的内存不足，或是配置要求过高。\n\n请在启动设置中增加为游戏分配的内存，并删除配置要求较高的材质、Mod、光影。\n如果依然不奏效，请在开始游戏前尽量关闭其他软件，或者……换台电脑？\h");
                    break;
                }
                case CrashReason.使用OpenJ9:
                {
                    results.Add(@"游戏因为使用 OpenJ9 而崩溃了。\n请在启动设置的 Java 选择一项中改用非 OpenJ9 的 Java，然后再启动游戏。");
                    break;
                }
                case CrashReason.使用JDK:
                {
                    results.Add(
                        @"游戏似乎因为使用 JDK，或 Java 版本过高而崩溃了。\n请在启动设置的 Java 选择一项中改用 JRE 8（Java 8），然后再启动游戏。\n如果你没有安装 JRE 8，你可以从网络中下载、安装一个。");
                    break;
                }
                case CrashReason.Java版本过高:
                {
                    results.Add(
                        @"游戏似乎因为你所使用的 Java 版本过高而崩溃了。\n请在启动设置的 Java 选择一项中改用较低版本的 Java，然后再启动游戏。\n如果没有，可以从网络中下载、安装一个。");
                    break;
                }
                case CrashReason.Java版本不兼容:
                {
                    results.Add(@"游戏不兼容你当前使用的 Java。\n如果没有合适的 Java，可以从网络中下载、安装一个。");
                    break;
                }
                case CrashReason.Mod名称包含特殊字符:
                {
                    results.Add(@"由于有 Mod 的名称包含特殊字符，导致游戏崩溃。\n请尝试修改 Mod 文件名，让它只包含英文字母、数字、减号（-）、下划线（_）和小数点，然后再启动游戏。");
                    break;
                }
                case CrashReason.MixinBootstrap缺失:
                {
                    results.Add(@"由于缺失 MixinBootstrap，导致游戏崩溃。\n请尝试安装 MixinBootstrap。若安装后依然崩溃，可以尝试在文件名前添加英文感叹号。");
                    break;
                }
                case CrashReason.使用32位Java导致JVM无法分配足够多的内存:
                {
                    if (Environment.Is64BitOperatingSystem)
                        results.Add(
                            @"你似乎正在使用 32 位 Java，这会导致 Minecraft 无法使用所需的内存，进而造成崩溃。\n\n请在启动设置的 Java 选择一项中改用 64 位的 Java 再启动游戏，然后再启动游戏。\n如果你没有安装 64 位的 Java，你可以从网络中下载、安装一个。");
                    else
                        results.Add(
                            @"你正在使用 32 位的操作系统，这会导致 Minecraft 无法使用所需的内存，进而造成崩溃。\n\n你或许只能重装 64 位的操作系统来解决此问题。\n如果你的电脑内存在 2GB 以内，那或许只能换台电脑了……\h");

                    break;
                }
                case CrashReason.Mod缺少前置或MC版本错误:
                {
                    if (additional.Any())
                    {
                        var info = additional.Join(@"\n - ");
                        if (info.IsMatch(RegexPatterns.IncompatibleModLoaderErrorHint))
                            results.Add(loaderIncompatibleResultText + info);
                        else
                            results.Add(@"由于未安装正确的前置 Mod，导致游戏退出。\n缺失的依赖项：\n - " + info +
                                        @"\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。");
                    }
                    else
                    {
                        results.Add(@"由于未安装正确的前置 Mod，导致游戏退出。\n请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h");
                    }

                    break;
                }
                case CrashReason.堆栈分析发现关键字:
                {
                    if (additional.Count == 1)
                        results.Add("你的游戏遇到了一些问题，PCL 为此找到了一个可疑的关键词：" + additional.First() +
                                    @"。\n\n如果你知道某个关键词对应的 Mod，那么有可能就是它引起的错误，你也可以查看错误报告获取详情。\h");
                    else
                        results.Add(@"你的游戏遇到了一些问题，PCL 为此找到了以下可疑的关键词：\n - " + additional.Join(", ") +
                                    @"\n\n如果你知道某个关键词对应的 Mod，那么有可能就是它引起的错误，你也可以查看错误报告获取详情。\h");

                    break;
                }
                case CrashReason.堆栈分析发现Mod名称:
                case CrashReason.怀疑Mod导致游戏崩溃:
                {
                    if (additional.Count == 1)
                        results.Add("PCL 怀疑名为 " + additional.First() +
                                    @" 的 Mod 导致了游戏出错，但不能完全确定。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h");
                    else
                        results.Add(@"PCL 怀疑以下 Mod 导致了游戏出错，但不能完全确定：\n - " + additional.Join(@"\n - ") +
                                    @"\n\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h");

                    break;
                }
                case CrashReason.确定Mod导致游戏崩溃:
                {
                    if (additional.Count == 1)
                        results.Add("名为 " + additional.First() + @" 的 Mod 导致了游戏出错。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h");
                    else
                        results.Add(@"以下 Mod 导致了游戏出错：\n - " + additional.Join(@"\n - ") +
                                    @"\n\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h");

                    break;
                }
                case CrashReason.ModMixin失败:
                {
                    if (additional.Count == 0)
                        results.Add(
                            @"部分 Mod 注入失败，导致游戏出错。\n这一般代表着部分 Mod 与其他 Mod 或当前环境不兼容，或是它存在 Bug。\n你可以尝试逐步禁用 Mod，然后观察游戏是否还会崩溃，以此定位导致崩溃的 Mod。\n\e\h");
                    else if (additional.Count == 1)
                        results.Add("名为 " + additional.First() +
                                    @" 的 Mod 注入失败，导致游戏出错。\n这一般代表着它与其他 Mod 或当前环境不兼容，或是它存在 Bug。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h");
                    else
                        results.Add(@"以下 Mod 导致了游戏出错：\n - " + additional.Join(@"\n - ") +
                                    @"\n这一般代表着它们与其他 Mod 或当前环境不兼容，或是它存在 Bug。\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h");

                    break;
                }
                case CrashReason.Mod配置文件导致游戏崩溃:
                {
                    if (additional[1] is null)
                        results.Add("名为 " + additional.First() + @" 的 Mod 导致了游戏出错。\n\e\h");
                    else
                        results.Add("名为 " + additional.First() + @" 的 Mod 导致了游戏出错：\n其配置文件 " + additional[1] +
                                    " 存在异常，无法读取。");

                    break;
                }
                case CrashReason.Mod初始化失败:
                {
                    if (additional.Count == 1)
                        results.Add("名为 " + additional.First() +
                                    @" 的 Mod 初始化失败，导致游戏无法继续加载。\n你可以尝试禁用此 Mod，然后观察游戏是否还会崩溃。\n\e\h");
                    else
                        results.Add(@"以下 Mod 初始化失败，导致游戏出错：\n - " + additional.Join(@"\n - ") +
                                    @"\n\n你可以尝试依次禁用上述 Mod，然后观察游戏是否还会崩溃。\n\e\h");

                    break;
                }
                case CrashReason.特定方块导致崩溃:
                {
                    if (additional.Count == 1)
                        results.Add("游戏似乎因为方块 " + additional.First() +
                                    @" 出现了问题。\n\n你可以创建一个新世界，并观察游戏的运行情况：\n - 若正常运行，则是该方块导致出错，你或许需要使用一些方式删除此方块。\n - 若仍然出错，问题就可能来自其他原因……\h");
                    else
                        results.Add(
                            @"游戏似乎因为世界中的某些方块出现了问题。\n\n你可以创建一个新世界，并观察游戏的运行情况：\n - 若正常运行，则是某些方块导致出错，你或许需要删除该世界。\n - 若仍然出错，问题就可能来自其他原因……\h");

                    break;
                }
                case CrashReason.Mod重复安装:
                {
                    if (additional.Count >= 2)
                        results.Add(@"你重复安装了多个相同的 Mod：\n - " + additional.Join(@"\n - ") +
                                    @"\n\n每个 Mod 只能出现一次，请删除重复的 Mod，然后再启动游戏。");
                    else
                        results.Add(@"你可能重复安装了多个相同的 Mod，导致游戏出错。\n\n每个 Mod 只能出现一次，请删除重复的 Mod，然后再启动游戏。\e\h");

                    break;
                }
                case CrashReason.特定实体导致崩溃:
                {
                    if (additional.Count == 1)
                        results.Add("游戏似乎因为实体 " + additional.First() +
                                    @" 出现了问题。\n\n你可以创建一个新世界，并生成一个该实体，然后观察游戏的运行情况：\n - 若正常运行，则是该实体导致出错，你或许需要使用一些方式删除此实体。\n - 若仍然出错，问题就可能来自其他原因……\h");
                    else
                        results.Add(
                            @"游戏似乎因为世界中的某些实体出现了问题。\n\n你可以创建一个新世界，并生成各种实体，观察游戏的运行情况：\n - 若正常运行，则是某些实体导致出错，你或许需要删除该世界。\n - 若仍然出错，问题就可能来自其他原因……\h");

                    break;
                }
                case CrashReason.OptiFine与Forge不兼容:
                {
                    results.Add(
                        @"由于 OptiFine 与当前版本的 Forge 不兼容，导致了游戏崩溃。\n\n请前往 OptiFine 官网（https://optifine.net/downloads）查看 OptiFine 所兼容的 Forge 版本，并严格按照对应版本重新安装游戏。");
                    break;
                }
                case CrashReason.ShadersMod与OptiFine同时安装:
                {
                    results.Add(
                        @"无需同时安装 OptiFine 和 Shaders Mod，OptiFine 已经集成了 Shaders Mod 的功能。\n在删除 Shaders Mod 后，游戏即可正常运行。");
                    break;
                }
                case CrashReason.低版本Forge与高版本Java不兼容:
                {
                    results.Add(
                        @"由于低版本 Forge 与当前 Java 不兼容，导致了游戏崩溃。\n\n请尝试以下解决方案：\n - 更新 Forge 到 36.2.26 或更高版本\n - 换用版本低于 1.8.0.320 的 Java");
                    break;
                }
                case CrashReason.实例Json中存在多个Forge:
                {
                    results.Add(@"可能由于其他启动器修改了 Forge 版本，当前实例的文件存在异常，导致了游戏崩溃。\n请尝试重新全新安装 Forge，而非使用其他启动器修改 Forge 版本。");
                    break;
                }
                case CrashReason.玩家手动触发调试崩溃:
                {
                    results.Add(@"* 事实上，你的游戏没有任何问题，这是你自己触发的崩溃。\n* 你难道没有更重要的事要做吗？");
                    break;
                }
                case CrashReason.Mod需要Java11:
                {
                    results.Add(
                        @"你所安装的部分 Mod 似乎需要使用 Java 11 启动。\n请在启动设置的 Java 选择一项中改用 Java 11，然后再启动游戏。\n如果你没有安装 Java 11，你可以从网络中下载、安装一个。");
                    break;
                }
                case CrashReason.极短的程序输出:
                {
                    results.Add($@"程序返回了以下信息：\n{additional.First()}\n\h");
                    break;
                }
                case CrashReason.OptiFine导致无法加载世界
                    : // https://www.minecraftforum.net/forums/support/java-edition-support/3051132-exception-ticking-world
                {
                    results.Add(@"你所使用的 OptiFine 可能导致了你的游戏出现问题。\n\n该问题只在特定 OptiFine 版本中出现，你可以尝试更换 OptiFine 的版本。\h");
                    break;
                }
                case CrashReason.显卡驱动不支持导致无法设置像素格式:
                case CrashReason.Intel驱动不兼容导致EXCEPTION_ACCESS_VIOLATION:
                case CrashReason.AMD驱动不兼容导致EXCEPTION_ACCESS_VIOLATION:
                case CrashReason.Nvidia驱动不兼容导致EXCEPTION_ACCESS_VIOLATION:
                case CrashReason.显卡不支持OpenGL:
                {
                    if (logAll.Contains("hd graphics "))
                        results.Add(
                            @"你的显卡驱动存在问题，或未使用独立显卡，导致游戏无法正常运行。\n\n如果你的电脑存在独立显卡，请使用独立显卡而非 Intel 核显启动 PCL 与 Minecraft。\n如果问题依然存在，请尝试升级你的显卡驱动到最新版本，或回退到出厂版本。\n如果还是不行，还可以尝试使用 8.0.51 或更低版本的 Java。\h");
                    else
                        results.Add(
                            @"你的显卡驱动存在问题，导致游戏无法正常运行。\n\n请尝试升级你的显卡驱动到最新版本，或回退到出厂版本，然后再启动游戏。\n如果还是不行，可以尝试使用 8.0.51 或更低版本的 Java。\n如果问题依然存在，那么你可能需要换个更好的显卡……\h");

                    break;
                }
                case CrashReason.材质过大或显卡配置不足:
                {
                    results.Add(
                        @"你所使用的材质分辨率过高，或显卡配置不足，导致游戏无法继续运行。\n\n如果你正在使用高清材质，请将它移除。\n如果你没有使用材质，那么你可能需要更新显卡驱动，或者换个更好的显卡……\h");
                    break;
                }
                case CrashReason.NightConfig的Bug:
                {
                    results.Add(@"由于 Night Config 存在问题，导致了游戏崩溃。\n你可以尝试安装 Night Config Fixes 模组，这或许能解决此问题。\h");
                    break;
                }
                case CrashReason.光影或资源包导致OpenGL1282错误:
                {
                    results.Add(@"你所使用的光影或材质导致游戏出现了一些问题……\n\n请尝试删除你所添加的这些额外资源。\h");
                    break;
                }
                case CrashReason.Mod过多导致超出ID限制:
                {
                    results.Add(@"你所安装的 Mod 过多，超出了游戏的 ID 限制，导致了游戏崩溃。\n请尝试安装 JEID 等修复 Mod，或删除部分大型 Mod。");
                    break;
                }
                case CrashReason.文件或内容校验失败:
                {
                    results.Add(@"部分文件或内容校验失败，导致游戏出现了问题。\n\n请尝试删除游戏（包括 Mod）并重新下载，或尝试在重新下载时使用 VPN。\h");
                    break;
                }
                case CrashReason.Forge安装不完整:
                {
                    results.Add(
                        @"由于安装的 Forge 文件丢失，导致游戏无法正常运行。\n请前往实例设置重置该实例，然后再启动游戏。\n在打包游戏时删除 libraries 文件夹可能导致此错误。\h");
                    break;
                }
                case CrashReason.Fabric报错:
                {
                    if (additional.Count == 1)
                        results.Add(@"Fabric 提供了以下错误信息：\n" + additional.First() +
                                    @"\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。");
                    else
                        results.Add(@"Fabric 可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h");

                    break;
                }
                case CrashReason.Mod互不兼容:
                {
                    if (additional.Count == 1)
                    {
                        var info = additional.First();
                        if (info.IsMatch(RegexPatterns.IncompatibleModLoaderErrorHint))
                            results.Add(loaderIncompatibleResultText + info);
                        else
                            results.Add(@"你所安装的 Mod 不兼容：\n" + info + @"\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。");
                    }
                    else
                    {
                        results.Add(@"你所安装的 Mod 不兼容，Mod 加载器可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h");
                    }

                    break;
                }
                case CrashReason.Mod加载器报错:
                {
                    if (additional.Count == 1)
                        results.Add(@"Mod 加载器提供了以下错误信息：\n" + additional.First() +
                                    @"\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。");
                    else
                        results.Add(@"Mod 加载器可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h");

                    break;
                }
                case CrashReason.Fabric报错并给出解决方案:
                {
                    if (additional.Count == 1)
                        results.Add(@"Fabric 提供了以下解决方案：\n" + additional.First() +
                                    @"\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。");
                    else
                        results.Add(@"Fabric 可能已经提供了解决方案，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h");

                    break;
                }
                case CrashReason.Forge报错:
                {
                    if (additional.Count == 1)
                        results.Add(@"Forge 提供了以下错误信息：\n" + additional.First() + @"\n\n请根据上述信息进行对应处理，如果看不懂英文可以使用翻译软件。");
                    else
                        results.Add(@"Forge 可能已经提供了错误信息，请根据错误报告中的日志信息进行对应处理，如果看不懂英文可以使用翻译软件。\h");

                    break;
                }
                case CrashReason.没有可用的分析文件:
                {
                    results.Add(@"你的游戏出现了一些问题，但 PCL 未能找到相关记录文件，因此无法进行分析。\h");
                    break;
                }

                default:
                {
                    results.Add("PCL 获取到了没有详细信息的错误原因（" + (int)crashReasons.First().Key + @"），请向 PCL 作者提交反馈以获取详情。\h");
                    break;
                }
            }
        }

        var isLauncherLatest = false;
        try
        {
            isLauncherLatest = UpdateManager.GetVersionStatus() == UpdateEnums.VersionStatus.Latest;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "确认启动器更新失败", ModBase.LogLevel.Feedback);
        }

        return results.Join(@"\n\n此外，").Replace(@"\n", "\r\n").Replace(@"\h", "")
                   .Replace(@"\e", isHandAnalyze ? "" : "\r\n" + "你可以查看错误报告了解错误具体是如何发生的。")
                   .Replace("\r\n", "\r").Replace("\n", "\r")
                   .Replace("\r", "\r\n").Trim("\r\n".ToCharArray()) +
               (!results.Any(r => r.EndsWithF(@"\h")) || isHandAnalyze
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
