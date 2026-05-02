using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json;
using PCL.Core.App;
using PCL.Core.IO;
using PCL.Core.Logging;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xaml;
using System.Xml.Linq;
using static System.Windows.Application;
using FontFamily = System.Windows.Media.FontFamily;
using Size = System.Windows.Size;

namespace PCL;

public static class ModBase
{
    #region 声明
    /// <summary>
    /// 主窗口句柄。
    /// </summary>
    public static nint FrmHandle;


    /// <summary>
    /// 设置对象。
    /// </summary>
    public static ModSetup Setup = new();

    /// <summary>
    /// 程序是否已结束。
    /// </summary>
    public static bool IsProgramEnded = false;

    #endregion

    #region 文本

    public static char VbLq = Convert.ToChar(8220);
    public static char VbRq = Convert.ToChar(8221);

    /// <summary>
    /// 获取 JSON 对象。
    /// </summary>
    [Obsolete("Need replace this in the future")]
    public static object GetJson(string data)
    {
        try
        {
            return JsonConvert.DeserializeObject(data,
                new JsonSerializerSettings { DateTimeZoneHandling = DateTimeZoneHandling.Local });
        }
        catch (Exception ex)
        {
            var length = (data ?? "").Length;
            throw new Exception("格式化 JSON 失败：" + (length > 2000
                ? data.Substring(0, 500) + $"...(全长 {length} 个字符)..." + Strings.Right(data, 500)
                : data));
        }
    }


    #endregion

    #region 系统
    /// <summary>
    /// 指示接取到这个异常的函数进行重试。
    /// </summary>
    public class RestartException : Exception
    {
    }

    /// <summary>
    /// 判断对象是否为某个泛型类型的实例。
    /// </summary>
    public static bool IsInstanceOfGenericType(this Type genericType, object? obj)
    {
        if (obj is null)
            return false;
        var t = obj.GetType();
        while (t is not null)
        {
            if (t.IsGenericType && ReferenceEquals(t.GetGenericTypeDefinition(), genericType))
                return true;
            t = t.BaseType;
        }

        return false;
    }

    private static int _uuid = 1;
    private static object _uuidLock;



    /// <summary>
    /// 在新的工作线程中执行代码。
    /// </summary>
    public static Thread RunInNewThread(Action action,
        string name = null,
        ThreadPriority priority = ThreadPriority.Normal)
    {
        var th = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (ThreadInterruptedException ex)
                {
                    Log(name + "：线程已中止");
                }
                catch (Exception ex)
                {
                    Log(ex, name + "：线程执行失败", LogType.Feedback);
                }
            })
        { Name = name ?? "Runtime New Invoke " + GlobalUniqueId.GetUniqueId() + "#", Priority = priority };
        th.Start();
        return th;
    }

    /// <summary>
    /// 确保在 UI 线程中执行代码。
    /// 如果当前并非 UI 线程，则会阻断当前线程，直至 UI 线程执行完毕。
    /// 为防止线程互锁，请仅在开始加载动画、从 UI 获取输入时使用！
    /// </summary>
    public static Output RunInUiWait<Output>(Func<Output> action)
    {
        if (IsRunInUi()) return action();

        return Current.Dispatcher.Invoke(action);
    }

    /// <summary>
    /// 确保在 UI 线程中执行代码。
    /// 如果当前并非 UI 线程，则会阻断当前线程，直至 UI 线程执行完毕。
    /// 为防止线程互锁，请仅在开始加载动画、从 UI 获取输入时使用！
    /// </summary>
    public static void RunInUiWait(Action action)
    {
        if (Current is null)
            return;
        if (IsRunInUi())
            action();
        else
            Current.Dispatcher.Invoke(action);
    }

    /// <summary>
    /// 确保在 UI 线程中执行代码，代码按触发顺序执行。
    /// 如果当前并非 UI 线程，也不阻断当前线程的执行。
    /// </summary>
    public static void RunInUi(Action action, bool forceWaitUntilLoaded = false)
    {
        if (Current is null)
            return;
        if (IsRunInUi())
            action();
        else
            Current.Dispatcher.InvokeAsync(action,
                forceWaitUntilLoaded ? DispatcherPriority.Loaded : DispatcherPriority.Normal);
    }

    /// <summary>
    /// 确保在工作线程中执行代码。
    /// </summary>
    public static void RunInWorkerThread(Action action)
    {
        if (IsRunInUi())
            RunInNewThread(action, "Runtime Invoke " + GlobalUniqueId.GetUniqueId() + "#");
        else
            action();
    }

    /// <summary>
    /// 设置剪贴板。将在另一线程运行，且不会抛出异常。
    /// </summary>
    public static void ClipboardSet(string text, bool showSuccessHint = true)
    {
        RunInWorkerThread(() =>
        {
            var success = false;

            for (var attempt = 0; attempt <= 5; attempt++)
                try
                {
                    RunInUi(() => Clipboard.SetText(text));
                    success = true;
                    break;
                }
                catch (Exception ex) when (attempt < 5)
                {
                    Thread.Sleep(20);
                }
                catch (Exception finalEx)
                {
                    Log(finalEx, "剪贴板被占用，文本复制失败", LogType.Hint);
                }

            if (success && showSuccessHint) RunInUi(() => ModMain.Hint("已成功复制！", ModMain.HintType.Finish));
        });
    }

    /// <summary>
    /// 从剪切板粘贴文件或文件夹
    /// </summary>
    /// <param name="dest">目标文件夹</param>
    /// <param name="copyFile">是否粘贴文件</param>
    /// <param name="copyDir">是否粘贴文件夹</param>
    /// <returns>总共粘贴的数量</returns>
    public static int PasteFileFromClipboard(string dest, bool copyFile = true, bool copyDir = true)
    {
        Log("[System] 从剪贴板粘贴文件到：" + dest);
        try
        {
            var files = Clipboard.GetFileDropList();
            if (files.Count == 0)
            {
                Log("[System] 剪贴板内无文件可粘贴");
                return 0;
            }

            var copiedFiles = 0;
            var copiedFolders = 0;
            foreach (var i in files)
            {
                if (copyFile && File.Exists(i)) // 文件
                    try
                    {
                        var thisDest = dest + PathUtils.GetFileNameFromPath(i);
                        if (File.Exists(thisDest))
                        {
                            Log("[System] 已存在同名文件：" + thisDest);
                        }
                        else
                        {
                            File.Copy(i, thisDest);
                            copiedFiles += 1;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(ex, "[System] 复制文件时出错");
                        continue;
                    }

                if (copyDir && Directory.Exists(i)) // 文件夹
                    try
                    {
                        var thisDest = dest + PathUtils.GetFolderNameFromPath(i);
                        if (Directory.Exists(thisDest))
                        {
                            Log("[System] 已存在同名文件夹：" + thisDest);
                        }
                        else
                        {
                            Directories.CopyDirectoryAsync(i, thisDest).GetAwaiter().GetResult();
                            copiedFolders += 1;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log(ex, "[System] 复制文件时出错");
                    }
            }

            ModMain.Hint("[System] 已粘贴 " + copiedFiles + " 个文件和 " + copiedFolders + " 个文件夹");
        }
        catch (Exception ex)
        {
            Log(ex, "[System] 从剪切板粘贴文件失败", LogType.Hint);
        }

        return 0;
    }

    #endregion

    #region UI

    public static void SetLaunchFont(string fontName = null)
    {
        try
        {
            FontFamily targetFont;
            if (string.IsNullOrEmpty(fontName))
                targetFont = new FontFamily(new Uri("pack://application:,,,/"),
                    "./Resources/#PCL English, Segoe UI, Microsoft YaHei UI");
            else
                targetFont = new FontFamily($"{fontName}, Segoe UI, Microsoft YaHei UI");
            Current.Resources["LaunchFontFamily"] = targetFont;
        }
        catch (Exception ex)
        {
            Log(ex, "设置字体失败", LogType.Hint);
        }
    }

    // 边距改变
    /// <summary>
    /// 相对增减控件的左边距。
    /// </summary>
    public static void DeltaLeft(FrameworkElement control, double newValue)
    {
        // 安全性检查
        DebugAssert(newValue is not double.NaN);
        DebugAssert(!double.IsInfinity(newValue));

        if (control is Window)
            // 窗口改变
            ((Window)control).Left += newValue;
        else
            // 根据 HorizontalAlignment 改变数值
            switch (control.HorizontalAlignment)
            {
                case HorizontalAlignment.Left:
                case HorizontalAlignment.Stretch:
                    {
                        control.Margin = new Thickness(control.Margin.Left + newValue, control.Margin.Top,
                            control.Margin.Right, control.Margin.Bottom);
                        break;
                    }
                case HorizontalAlignment.Right:
                    {
                        // control.Margin = New Thickness(control.Margin.Left, control.Margin.Top, CType(control.Parent, Object).ActualWidth - control.ActualWidth - newValue, control.Margin.Bottom)
                        control.Margin = new Thickness(control.Margin.Left, control.Margin.Top,
                            control.Margin.Right - newValue, control.Margin.Bottom);
                        break;
                    }

                default:
                    {
                        DebugAssert(false);
                        break;
                    }
            }
    }

    /// <summary>
    /// 设置控件的左边距。（仅针对置左控件）
    /// </summary>
    public static void SetLeft(FrameworkElement control, double newValue)
    {
        DebugAssert(control.HorizontalAlignment == HorizontalAlignment.Left);
        control.Margin = new Thickness(newValue, control.Margin.Top, control.Margin.Right, control.Margin.Bottom);
    }

    /// <summary>
    /// 相对增减控件的上边距。
    /// </summary>
    public static void DeltaTop(FrameworkElement control, double newValue)
    {
        // 安全性检查
        DebugAssert(newValue is not double.NaN);
        DebugAssert(!double.IsInfinity(newValue));

        if (control is Window)
            // 窗口改变
            ((Window)control).Top += newValue;
        else
            // 根据 VerticalAlignment 改变数值
            switch (control.VerticalAlignment)
            {
                case VerticalAlignment.Top:
                    {
                        control.Margin = new Thickness(control.Margin.Left, control.Margin.Top + newValue,
                            control.Margin.Right, control.Margin.Bottom);
                        break;
                    }
                case VerticalAlignment.Bottom:
                    {
                        // control.Margin = New Thickness(control.Margin.Left, control.Margin.Top, CType(control.Parent, Object).ActualWidth - control.ActualWidth - newValue, control.Margin.Bottom)
                        control.Margin = new Thickness(control.Margin.Left, control.Margin.Top, control.Margin.Right,
                            control.Margin.Bottom - newValue);
                        break;
                    }

                default:
                    {
                        DebugAssert(false);
                        break;
                    }
            }

        // If Double.IsNaN(newValue) OrElse Double.IsInfinity(newValue) Then Return '安全性检查
        // Select Case control.VerticalAlignment
        // Case VerticalAlignment.Top, VerticalAlignment.Stretch, VerticalAlignment.Center
        // control.Margin = New Thickness(control.Margin.Left, newValue, control.Margin.Right, control.Margin.Bottom)
        // Case VerticalAlignment.Bottom
        // control.Margin = New Thickness(control.Margin.Left, control.Margin.Top, control.Margin.Right, -newValue)
        // 'control.Margin = New Thickness(control.Margin.Left, control.Margin.Top, control.Margin.Right, CType(control.Parent, Object).ActualHeight - control.ActualHeight - newValue)
        // End Select
    }

    /// <summary>
    /// 设置控件的顶边距。（仅针对置上控件）
    /// </summary>
    public static void SetTop(FrameworkElement control, double newValue)
    {
        DebugAssert(control.VerticalAlignment == VerticalAlignment.Top);
        control.Margin = new Thickness(control.Margin.Left, newValue, control.Margin.Right, control.Margin.Bottom);
    }

    // DPI 转换
    public static readonly int Dpi = (int)Math.Round(Graphics.FromHwnd(nint.Zero).DpiX);

    /// <summary>
    /// 将经过 DPI 缩放的 WPF 尺寸转化为实际的像素尺寸。
    /// </summary>
    public static double GetPixelSize(double wpfSize)
    {
        return wpfSize / 96d * Dpi;
    }

    /// <summary>
    /// 将实际的像素尺寸转化为经过 DPI 缩放的 WPF 尺寸。
    /// </summary>
    public static double GetWpfSize(double pixelSize)
    {
        return pixelSize * 96d / Dpi;
    }

    // UI 截图
    /// <summary>
    /// 将某个控件的呈现转换为图片。
    /// </summary>
    public static ImageBrush ControlBrush(FrameworkElement ui)
    {
        var width = ui.ActualWidth;
        var height = ui.ActualHeight;
        if (width < 1d || height < 1d)
            return new ImageBrush();
        var bmp = new RenderTargetBitmap((int)Math.Round(GetPixelSize(width)), (int)Math.Round(GetPixelSize(height)),
            Dpi, Dpi, PixelFormats.Pbgra32);
        bmp.Render(ui);
        return new ImageBrush(bmp);
    }

    /// <summary>
    /// 将某个控件的模拟呈现转换为图片。
    /// </summary>
    public static ImageBrush ControlBrush(FrameworkElement ui, double width, double height, double left = 0d,
        double top = 0d)
    {
        ui.Measure(new Size(width, height));
        ui.Arrange(new Rect(0d, 0d, width, height));
        var bmp = new RenderTargetBitmap((int)Math.Round(GetPixelSize(width)), (int)Math.Round(GetPixelSize(height)),
            Dpi, Dpi, PixelFormats.Default);
        bmp.Render(ui);
        if (!(left == 0d && top == 0d))
            ui.Arrange(new Rect(left, top, width, height));
        return new ImageBrush(bmp);
    }

    /// <summary>
    /// 将 UI 内容固定为图片并进行 Clear。
    /// </summary>
    public static void ControlFreeze(Panel ui)
    {
        ui.Background = ControlBrush(ui);
        ui.Children.Clear();
    }

    /// <summary>
    /// 将 UI 内容固定为图片并进行 Clear。
    /// </summary>
    public static void ControlFreeze(Border ui)
    {
        ui.Background = ControlBrush(ui);
        ui.Child = null;
    }

    /// <summary>
    /// 将 XML 转换为对应 UI 对象。
    /// </summary>
    public static object GetObjectFromXml(XElement str)
    {
        return GetObjectFromXml(str.ToString());
    }

    /// <summary>
    /// 将 XML 转换为对应 UI 对象。
    /// </summary>
    public static object GetObjectFromXml(string str)
    {
        str = str. // 兼容旧版自定义事件写法
            Replace("EventType=\"", "local:CustomEventService.EventType=\"")
            .Replace("EventData=\"", "local:CustomEventService.EventData=\"")
            .Replace("Property=\"EventType\"", "Property=\"local:CustomEventService.EventType\"")
            .Replace("Property=\"EventData\"", "Property=\"local:CustomEventService.EventData\"");
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(str)))
        {
            // 类型检查
            using (var reader = new XamlXmlReader(stream))
            {
                while (reader.Read())
                {
                    foreach (var blackListType in new[]
                             {
                                 typeof(WebBrowser), typeof(Frame), typeof(MediaElement), typeof(ObjectDataProvider),
                                 typeof(XamlReader), typeof(Window), typeof(XmlDataProvider)
                             })
                    {
                        if (reader.Type is not null && blackListType.IsAssignableFrom(reader.Type.UnderlyingType))
                            throw new UnauthorizedAccessException($"不允许使用 {blackListType.Name} 类型。");
                        if (reader.Value is not null && Conversions.ToBoolean(
                                Operators.ConditionalCompareObjectEqual(reader.Value, blackListType.Name, false)))
                            throw new UnauthorizedAccessException($"不允许使用 {blackListType.Name} 值。");
                    }

                    foreach (var blackListMember in new[] { "Code", "FactoryMethod", "Static" })
                        if (reader.Member is not null && (reader.Member.Name ?? "") == (blackListMember ?? ""))
                            throw new UnauthorizedAccessException($"不允许使用 {blackListMember} 成员。");
                }
            }

            // 实际的加载
            stream.Position = 0L;
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(str);
                writer.Flush();
                stream.Position = 0L;
                return System.Windows.Markup.XamlReader.Load(stream);
            }
        }
    }

    private static readonly int UiThreadId = Thread.CurrentThread.ManagedThreadId;

    /// <summary>
    /// 当前线程是否为主线程。
    /// </summary>
    public static bool IsRunInUi()
    {
        return Thread.CurrentThread.ManagedThreadId == UiThreadId;
    }

    #endregion

    #region Debug

    public static bool ModeDebug = false;

    // Log
    public enum LogType
    {
        /// <summary>
        /// 不提示，只记录日志。
        /// </summary>
        Normal = 0,

        /// <summary>
        /// 只提示开发者。
        /// </summary>
        Developer = 1,

        /// <summary>
        /// 只提示开发者与调试模式用户。
        /// </summary>
        Debug = 2,

        /// <summary>
        /// 弹出提示所有用户。
        /// </summary>
        Hint = 3,

        /// <summary>
        /// 弹窗，不要求反馈。
        /// </summary>
        Msgbox = 4,

        /// <summary>
        /// 弹窗，要求反馈。
        /// </summary>
        Feedback = 5,

        /// <summary>
        /// 弹出 Windows 原生弹窗，要求反馈。在无法保证 WPF 窗口能正常运行时使用此级别。
        /// 在第二次触发后会直接结束程序。
        /// </summary>
        Critical = 6
    }

    private static bool _isCriticalErrorTriggered;

    /// <summary>
    /// 输出 Log。
    /// </summary>
    /// <param name="title">如果要求弹窗，指定弹窗的标题。</param>
    public static void Log(string text, LogType type = LogType.Normal, string title = "出现错误")
    {
        // On Error Resume Next
        // 放在最后会导致无法显示极端错误下的弹窗（如无法写入日志文件）
        // 处理错误会导致再次调用 Log() 导致无限循环

        // 输出日志
        if (new[] { LogType.Msgbox, LogType.Hint }.Contains(type))
            LogWrapper.Warn(text);
        else if (LogType.Feedback == type)
            LogWrapper.Error(text);
        else if (LogType.Critical == type)
            LogWrapper.Fatal(text);
        else if (LogType.Debug == type)
            LogWrapper.Debug(text);
        else if (LogType.Developer == type)
            LogWrapper.Trace(text);
        else
            LogWrapper.Info(text);

        if (IsProgramEnded || type == LogType.Normal)
            return;

        // 去除前缀
        text = text.RegexReplace(@"\[[^\]]+?\] ", "");

        // 输出提示
        switch (type)
        {
            case LogType.Developer:
                {
                    break;
#if DEBUG
                }
            case LogType.Debug:
                {
                    if (ModeDebug)
                        ModMain.Hint("[调试模式] " + text, ModMain.HintType.Info, false);
                    break;
                }


#endif
            case LogType.Hint:
                {
                    ModMain.Hint(text, ModMain.HintType.Critical, false);
                    break;
                }
            case LogType.Msgbox:
                {
                    ModMain.MyMsgBox(text, title, IsWarn: true);
                    break;
                }
            case LogType.Feedback:
                {
                    if (CanFeedback(false))
                    {
                        if (ModMain.MyMsgBox(text + "\r\n" + "\r\n" + "是否反馈此问题？如果不反馈，这个问题可能永远无法得到解决！",
                                title, "反馈", "取消", IsWarn: true) == 1)
                            Feedback(false, true);
                    }
                    else
                    {
                        ModMain.MyMsgBox(text + "\r\n" + "\r\n" + "将 PCL 更新至最新版或许可以解决这个问题……", title,
                            IsWarn: true);
                    }

                    break;
                }
            case LogType.Critical:
                {
                    if (_isCriticalErrorTriggered)
                    {
                        FormMain.EndProgramForce(Enums.ProcessReturnValues.Exception);
                        return;
                    }

                    _isCriticalErrorTriggered = true;
                    if (CanFeedback(false))
                    {
                        if (Interaction.MsgBox(text + "\r\n" + "\r\n" + "是否反馈此问题？如果不反馈，这个问题可能永远无法得到解决！",
                                (MsgBoxStyle)((int)MsgBoxStyle.Critical + (int)MsgBoxStyle.YesNo), title) ==
                            MsgBoxResult.Yes)
                            Feedback(false, true);
                    }
                    else
                    {
                        Interaction.MsgBox(text + "\r\n" + "\r\n" + "将 PCL 更新至最新版或许可以解决这个问题……",
                            MsgBoxStyle.Critical, title);
                    }

                    break;
                }
        }
    }

    /// <summary>
    /// 输出错误信息。
    /// </summary>
    /// <param name="desc">错误描述。会在处理时在末尾加入冒号。</param>
    public static void Log(Exception ex, string desc, LogType type = LogType.Debug, string title = "出现错误")
    {
        // On Error Resume Next
        if (ex is ThreadInterruptedException)
            return;

        // 获取错误信息
        var exFull = desc + "：" + ex.Message;

        // 输出日志
        if (new[] { LogType.Msgbox, LogType.Hint }.Contains(type))
            LogWrapper.Warn(ex, desc);
        else if (LogType.Feedback == type)
            LogWrapper.Error(ex, desc);
        else if (LogType.Critical == type)
            LogWrapper.Fatal(ex, desc);
        else if (LogType.Debug == type)
            LogWrapper.Debug($"{desc}:{ex}");
        else if (LogType.Developer == type)
            LogWrapper.Trace($"{desc}:{ex}");
        else
            LogWrapper.Error(ex, desc);

        if (IsProgramEnded)
            return;

        if (ex.GetType() == typeof(Win32Exception))
            exFull += "\r\n" + "与系统底层交互失败，请尝试重新安装 .NET 8 解决此问题";

        // 输出提示
        switch (type)
        {
            case LogType.Normal:
                {
                    break;
                }
            case LogType.Developer:
                {
                    break;
                }
            case LogType.Debug:
                {
                    var exLine = desc + "：" + ex;
                    if (ModeDebug)
                        ModMain.Hint("[调试模式] " + exLine, ModMain.HintType.Info, false);
                    break;
                }
            /* TODO ERROR: Skipped EndIfDirectiveTrivia
            #End If
            */
            case LogType.Hint:
                {
                    var exLine = desc + "：" + ex;
                    ModMain.Hint(exLine, ModMain.HintType.Critical, false);
                    break;
                }
            case LogType.Msgbox:
                {
                    ModMain.MyMsgBox(exFull, title, IsWarn: true);
                    break;
                }
            case LogType.Feedback:
                {
                    if (CanFeedback(false))
                    {
                        if (ModMain.MyMsgBox(exFull + "\r\n" + "\r\n" + "是否反馈此问题？如果不反馈，这个问题可能永远无法得到解决！",
                                title, "反馈", "取消", IsWarn: true) == 1)
                            Feedback(false, true);
                    }
                    else
                    {
                        ModMain.MyMsgBox(exFull + "\r\n" + "\r\n" + "将 PCL 更新至最新版或许可以解决这个问题……", title,
                            IsWarn: true);
                    }

                    break;
                }
            case LogType.Critical:
                {
                    if (_isCriticalErrorTriggered)
                    {
                        FormMain.EndProgramForce(Enums.ProcessReturnValues.Exception);
                        return;
                    }

                    _isCriticalErrorTriggered = true;
                    if (CanFeedback(false))
                    {
                        if (Interaction.MsgBox(
                                exFull + "\r\n" + "\r\n" + "是否反馈此问题？如果不反馈，这个问题可能永远无法得到解决！",
                                (MsgBoxStyle)((int)MsgBoxStyle.Critical + (int)MsgBoxStyle.YesNo), title) ==
                            MsgBoxResult.Yes)
                            Feedback(false, true);
                    }
                    else
                    {
                        Interaction.MsgBox(exFull + "\r\n" + "\r\n" + "将 PCL 更新至最新版或许可以解决这个问题……",
                            MsgBoxStyle.Critical, title);
                    }

                    break;
                }
        }
    }

    // 反馈
    public static void Feedback(bool showMsgbox = true, bool forceOpenLog = false)
    {
        // On Error Resume Next
        FeedbackInfo();
        string currentDate;
        currentDate = Strings.Format(DateTime.Now, "yyyy-M-dd");

        if (forceOpenLog || (showMsgbox &&
                             ModMain.MyMsgBox(
                                 "若你在汇报一个 Bug，请点击 打开文件夹 按钮，并上传 Launch-" + currentDate + "-[一串数字].log 中包含错误信息的文件。" +
                                 "\r\n" + "游戏崩溃一般与启动器无关，请不要因为游戏崩溃而提交反馈。", "反馈提交提醒", "打开文件夹", "不需要") ==
                             1))
        {
            Basics.OpenPath(Path.Combine(Basics.ExecutableDirectory, "PCL", "Log"));
        }

        ShellUtils.OpenWebsite("https://github.com/PCL-Community/PCL2-CE/issues/");
    }

    public static bool CanFeedback(bool showHint)
    {
        var stat = ModSecret.GetVersionStatus();
        if (stat != ModSecret.VersionStatus.Latest)
        {
            if (showHint)
            {
                if (ModMain.MyMsgBox(
                        stat == ModSecret.VersionStatus.NotLatest
                            ? "你的 PCL 不是最新版，因此无法提交反馈。\r\n请在更新后，确认该问题在最新版中依然存在，然后再提交反馈。"
                            : "你的 PCL 检查更新失败，因此无法提交反馈。\r\n请连接到互联网，在检查更新后，确认该问题在最新版中依然存在，然后再提交反馈。",
                        "无法提交反馈", stat == ModSecret.VersionStatus.NotLatest ? "更新" : "重新检查更新", "取消") == 1)
                {
                    ModMain.FrmMain.PageChange(FormMain.PageType.Setup, FormMain.PageSubType.SetupUpdate);
                }
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// 在日志中输出系统诊断信息。
    /// </summary>
    public static void FeedbackInfo()
    {
        try
        {
            // Get system memory info
            var phyRam = KernelInterop.GetPhysicalMemoryBytes();

            // Calculate memory and DPI scale
            var availableMb = phyRam.Available / 1024 / 1024;
            var totalMb = phyRam.Total / 1024 / 1024;
            var dpiScale = Math.Round(Dpi / 96.0, 2);

            // Build diagnostic information string
            var info = $"[System] Diagnostic Information:{"\r\n"}" +
                       $"OS: {RuntimeInformation.OSDescription} (32-bit: {Basics.Is32BitSystem}){"\r\n"}" +
                       $"Memory: {availableMb} MB / {totalMb} MB{"\r\n"}" +
                       $"DPI: {Dpi} ({dpiScale * 100}%){"\r\n"}" +
                       $"MC Folder: {ModMinecraft.McFolderSelected ?? "Nothing"}{"\r\n"}" +
                       $"Executable Path: {Basics.ExecutableDirectory}";

            LogWrapper.Info(info);
        }
        catch (Exception ex)
        {
            // Basic fail-safe to replace "On Error Resume Next"
            LogWrapper.Error(ex, "Failed to collect feedback information");
        }
    }

    // 断言
    public static void DebugAssert(bool exp)
    {
        if (!exp)
            throw new Exception("断言命中");
    }

    #endregion
}