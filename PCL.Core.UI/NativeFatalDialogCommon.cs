using System.Diagnostics;
using System.Text;

namespace PCL.Core.UI;

/// <summary>原生致命错误对话框真正共享的进程执行与临时文件逻辑。</summary>
internal static class NativeFatalDialogCommon
{
    private const int MaxInlineTextLength = 8192;

    internal static bool NeedsTextFile(string message)
    {
        return message.Length > MaxInlineTextLength;
    }

    internal static bool WithTempFile(string message, Func<string, bool> action)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"pcl-ce-crash-{Environment.ProcessId}-{Guid.NewGuid():N}.txt");

        try
        {
            File.WriteAllText(path, message, new UTF8Encoding(false));
            return action(path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FatalDialog] 写入临时崩溃文本失败：{ex}");
            return false;
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }

    internal static bool RunAndWait(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            if (environment is not null)
                foreach (var pair in environment)
                    startInfo.Environment[pair.Key] = pair.Value;

            using var process = Process.Start(startInfo);
            if (process is null) return false;

            process.WaitForExit();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}