using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.Utils.Exts;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace PCL.Core.Utils.OS;

public static class ShellUtils
{

    /// <summary>
    /// 打开网页。
    /// </summary>
    public static void OpenWebsite(string url)
    {
        try
        {
            if (!url.StartsWithF("http", true) &&
                !url.StartsWithF("minecraft://", true))
            {
                throw new ArgumentException($"{url} 不是一个有效的网址，它必须以 http 开头！");
            }

            _ReportInfo($"正在打开网页：{url}", LogLevel.Info);
            var psi = new ProcessStartInfo(url)
            {
                UseShellExecute = true,
            };
            _ = Task.Run(() => Process.Start(psi));
        }
        catch (Exception ex)
        {
            _ReportInfo($"无法打开网页 {url}", LogLevel.Error, ex);
            ClipboardUtils.SetClipboardContent(url, false);
            MsgBoxWrapper.Show(
$"""
                可能由于浏览器未正确配置，PCL 无法为你打开网页。
                网址已经复制到剪贴板，若有需要可以手动粘贴访问。
                
                网址：{url}", "无法打开网页
                """);
        }
    }

    /// <summary>
    /// Check if current code page is UTF-8. If not, some characters may be displayed as garbled.
    /// </summary>
    public static bool IsUtf8CodePage => Encoding.Default.CodePage == 65001;


    private const string ModelName = "System";

    private static void _ReportInfo(string msg, LogLevel level, Exception? ex = null)
    {
        switch (level)
        {
            case LogLevel.Trace:
                LogWrapper.Trace(ModelName, msg);
                break;
            case LogLevel.Debug:
                LogWrapper.Debug(ModelName, msg);
                break;
            case LogLevel.Info:
                LogWrapper.Info(ModelName, msg);
                break;
            case LogLevel.Warning:
                LogWrapper.Warn(ModelName, msg);
                MsgBoxWrapper.Show(msg, "警告", MsgBoxTheme.Warning);
                break;
            case LogLevel.Error:
                LogWrapper.Error(ex, ModelName, msg);
                MsgBoxWrapper.Show(msg, "错误", MsgBoxTheme.Error);
                break;
            case LogLevel.Fatal:
                LogWrapper.Fatal(ex, ModelName, msg);
                MsgBoxWrapper.Show(msg, "异常", MsgBoxTheme.Error);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(level), level, null);
        }
    }

}