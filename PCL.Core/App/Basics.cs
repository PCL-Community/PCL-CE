using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Windows;
using PCL.Core.Logging;
using PCL.Core.Utils;

namespace PCL.Core.App;

/// <summary>
/// 基础工具集。
/// </summary>
public static class Basics
{
    #region 基本信息

    /// <summary>
    /// 启动器元数据。
    /// </summary>
    public static MetadataModel Metadata { get; } = JsonSerializer.Deserialize<MetadataModel>(
        Assembly.GetEntryAssembly()!.GetManifestResourceStream("PCL.metadata.json")!)!;

    /// <summary>
    /// 版本名称。
    /// </summary>
    public static string VersionName => Metadata.Version.BaseName;

    /// <summary>
    /// 版本内部代号。
    /// </summary>
    public static int VersionCode => Metadata.Version.Code;

    /// <summary>
    /// 版本分支名。
    /// </summary>
    public static string VersionBranch => Metadata.Version.BranchName;

    #endregion

    #region 程序路径信息

    /// <summary>
    /// 当前进程实例。
    /// </summary>
    public static Process CurrentProcess { get; } = Process.GetCurrentProcess();

    /// <summary>
    /// 当前进程 ID。
    /// </summary>
    public static int CurrentProcessId { get; } = CurrentProcess.Id;

    /// <summary>
    /// 当前进程可执行文件的绝对路径。
    /// </summary>
    public static string ExecutablePath { get; } = Environment.ProcessPath!;

    /// <summary>
    /// 当前进程可执行文件所在的目录。若有需求，请使用 <see cref="Path.Combine(string[])"/> 而不是自行拼接路径。
    /// </summary>
    public static string ExecutableDirectory { get; } = GetParentPath(ExecutablePath) ?? CurrentDirectory;

    /// <summary>
    /// 当前进程可执行文件的名称，含扩展名。
    /// </summary>
    public static string ExecutableName { get; } = Path.GetFileName(ExecutablePath);

    /// <summary>
    /// 当前进程可执行文件的名称，不含扩展名。
    /// </summary>
    public static string ExecutableNameWithoutExtension { get; } = Path.GetFileNameWithoutExtension(ExecutablePath);

    /// <summary>
    /// 当前进程不包括第一个参数（文件名）的命令行参数。
    /// </summary>
    public static string[] CommandLineArguments { get; } = Environment.GetCommandLineArgs().Skip(1).ToArray();

    /// <summary>
    /// 实时获取的当前目录。若要在可执行文件目录中存放文件等内容，请使用更准确的 <see cref="ExecutableDirectory"/> 而不是这个目录。
    /// </summary>
    public static string CurrentDirectory => Environment.CurrentDirectory;

    #endregion

    #region 线程操作

    /// <summary>
    /// 在新的工作线程运行指定委托。
    /// </summary>
    /// <param name="action">要运行的委托</param>
    /// <param name="name">线程名，默认为 <c>WorkerThread@[ThreadId]</c></param>
    /// <param name="priority">线程优先级</param>
    /// <returns>新创建的线程实例</returns>
    public static Thread RunInNewThread(Action action, string? name = null, ThreadPriority priority = ThreadPriority.Normal)
    {
        var threadName = new AtomicVariable<string>(name);
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (ThreadInterruptedException) { LogWrapper.Trace("Thread", $"{threadName.Value}: 已中止"); }
            catch (Exception ex) { LogWrapper.Error(ex, "Thread", $"{threadName.Value}: 抛出异常"); }
        }) { Priority = priority };
        threadName.Value ??= $"Worker#{thread.ManagedThreadId}";
        thread.Name = threadName.Value;
        thread.Start();
        return thread;
    }

    #endregion

    #region 路径操作

    /// <summary>
    /// 获取某个路径的父路径/目录。
    /// </summary>
    /// <param name="path">路径文本</param>
    /// <returns>父路径文本，可能为 <c>null</c></returns>
    public static string? GetParentPath(string path) => Path.GetDirectoryName(path) ?? Path.GetPathRoot(path);

    /// <summary>
    /// 获取某个路径的父路径/目录。
    /// </summary>
    /// <param name="path">路径文本</param>
    /// <returns>父路径文本，或空白</returns>
    public static string GetParentPathOrEmpty(string path) => GetParentPath(path) ?? string.Empty;

    /// <summary>
    /// 获取某个路径的父路径/目录。
    /// </summary>
    /// <param name="path">路径文本</param>
    /// <returns>父路径文本，或默认 (<see cref="CurrentDirectory"/>)</returns>
    public static string GetParentPathOrDefault(string path) => GetParentPath(path) ?? CurrentDirectory;

    /// <summary>
    /// 以默认方式打开一个路径 (文件或目录)
    /// </summary>
    /// <param name="path">路径文本</param>
    /// <param name="workingDirectory">执行工作目录</param>
    public static void OpenPath(string path, string? workingDirectory = null)
    {
        var psi = new ProcessStartInfo(path)
        {
            WorkingDirectory = workingDirectory ?? CurrentDirectory,
            UseShellExecute = true,
            CreateNoWindow = true
        };
        Process.Start(psi);
    }
    #endregion

    #region 应用程序操作

    /// <summary>
    /// 获取程序打包资源的输入流。该资源必须声明为 <c>Resource</c> 类型，否则将会报错，<c>Images</c>
    /// 和 <c>Resources</c> 目录已默认声明该类型。
    /// </summary>
    /// <param name="path">资源路径，例如 "Resources/java-wrapper.jar"</param>
    /// <returns>资源输入流，若资源不存在则为 <c>null</c></returns>
    public static Stream? GetResourceStream(string path) {
        var resourceInfo = Application.GetResourceStream(new Uri($"pack://application:,,,/{path}", UriKind.Absolute));
        return resourceInfo?.Stream;
    }

    private const string AssemblyImagePath = "pack://application:,,,/Plain Craft Launcher 2;component/Images/";
    public static string GetAppImagePath(string imageName) => AssemblyImagePath + imageName;

    #endregion
}
