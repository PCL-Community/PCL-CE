using PCL.Core.Platform;

namespace PCL;

/// <summary><c>--gpu &lt;javaPath&gt; [0|1]</c> 的无界面处理入口。 提权子进程只完成 GPU 偏好写入后退出，不创建窗口，也不参与单例检测。</summary>
public static class GpuPreferenceCommand
{
    public const string Option = "--gpu";

    /// <summary>当前命令行是否请求处理 GPU 偏好。</summary>
    public static bool IsRequested(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return Array.IndexOf(args, Option) >= 0;
    }

    /// <summary>尝试解析并执行 GPU 偏好命令。</summary>
    /// <returns>未包含该命令时返回 <see langword="null" />；否则返回进程退出码。</returns>
    public static int? TryExecute(string[] args, IGpuPreferenceService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        return IsRequested(args) ? Execute(args, service) : null;
    }

    /// <summary>执行 GPU 偏好命令并返回进程退出码。</summary>
    public static int Execute(string[] args, IGpuPreferenceService service)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(service);

        var index = Array.IndexOf(args, Option);
        if (index < 0)
            return (int)LauncherCommon.ProcessReturnValues.Fail;

        if (index + 1 >= args.Length
            || !TryParsePreference(args, index + 2, out var preferHighPerformance))
        {
            Console.Error.WriteLine("--gpu 参数无效：需要 --gpu <javaPath> [0|1]");
            return (int)LauncherCommon.ProcessReturnValues.Fail;
        }

        var javaPath = args[index + 1].Trim('"');
        if (!Path.IsPathFullyQualified(javaPath)
            || !File.Exists(javaPath))
        {
            Console.Error.WriteLine($"--gpu 指定的 Java 路径无效：{javaPath}");
            return (int)LauncherCommon.ProcessReturnValues.Fail;
        }

        try
        {
            if (!service.IsSupported)
            {
                Console.Error.WriteLine("当前平台不支持 GPU 偏好设置");
                return (int)LauncherCommon.ProcessReturnValues.Fail;
            }

            service.Set(javaPath, preferHighPerformance);
            return (int)LauncherCommon.ProcessReturnValues.TaskDone;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"设置 GPU 偏好失败：{ex.Message}");
            return (int)LauncherCommon.ProcessReturnValues.Fail;
        }
    }

    private static bool TryParsePreference(string[] args, int index, out bool preferHighPerformance)
    {
        preferHighPerformance = true;
        if (index >= args.Length)
            return true;

        switch (args[index].Trim().ToLowerInvariant())
        {
            case "1":
            case "true":
                return true;
            case "0":
            case "false":
                preferHighPerformance = false;
                return true;
            default:
                return false;
        }
    }
}
