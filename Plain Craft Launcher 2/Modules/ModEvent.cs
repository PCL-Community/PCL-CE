using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;
using PCL.Network;
using PCL.Core.App.Localization;

namespace PCL
{
    /// <summary>
    /// 用于在 XAML 中初始化列表对象。
    /// </summary>
    [ContentProperty("Events")]
    public class CustomEventCollection : IEnumerable<CustomEvent>
    {
        private readonly List<CustomEvent> _events = new List<CustomEvent>();

        public List<CustomEvent> Events => _events;

        public IEnumerator<CustomEvent> GetEnumerator() => Events.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// 提供自定义事件的附加属性。
    /// </summary>
    public static class CustomEventService
    {
        public static readonly DependencyProperty EventsProperty =
            DependencyProperty.RegisterAttached(
                "Events",
                typeof(CustomEventCollection),
                typeof(CustomEventService),
                new PropertyMetadata(null));

        [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
        public static void SetEvents(DependencyObject d, CustomEventCollection value) =>
            d.SetValue(EventsProperty, value);

        [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
        public static CustomEventCollection GetEvents(DependencyObject d)
        {
            if (d.GetValue(EventsProperty) == null)
                d.SetValue(EventsProperty, new CustomEventCollection());
            return (CustomEventCollection)d.GetValue(EventsProperty);
        }

        public static readonly DependencyProperty EventTypeProperty =
            DependencyProperty.RegisterAttached(
                "EventType",
                typeof(CustomEvent.EventType),
                typeof(CustomEventService),
                new PropertyMetadata(CustomEvent.EventType.None));

        [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
        public static void SetEventType(DependencyObject d, CustomEvent.EventType value) =>
            d.SetValue(EventTypeProperty, value);

        [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
        public static CustomEvent.EventType GetEventType(DependencyObject d) =>
            (CustomEvent.EventType)d.GetValue(EventTypeProperty);

        public static readonly DependencyProperty EventDataProperty =
            DependencyProperty.RegisterAttached(
                "EventData",
                typeof(string),
                typeof(CustomEventService),
                new PropertyMetadata(null));

        [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
        public static void SetEventData(DependencyObject d, string value) =>
            d.SetValue(EventDataProperty, value);

        [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
        public static string GetEventData(DependencyObject d) =>
            (string)d.GetValue(EventDataProperty);
    }

    /// <summary>
    /// 自定义事件。
    /// </summary>
    public class CustomEvent
    {
        public enum EventType
        {
            None = 0,
            打开网页,
            打开文件,
            打开帮助,
            执行命令,
            启动游戏,
            复制文本,
            刷新主页,
            刷新主页市场,
            刷新页面,
            刷新帮助,
            今日人品,
            内存优化,
            清理垃圾,
            弹出窗口,
            弹出提示,
            切换页面,
            导入整合包,
            安装整合包,
            下载文件,
            修改设置,
            写入设置,
            修改变量,
            写入变量
        }

        public EventType Type { get; set; } = EventType.None;
        public string Data { get; set; }

        public CustomEvent() { }

        public CustomEvent(EventType type, string data)
        {
            Type = type;
            Data = data;
        }

        public void Raise() => Raise(Type, Data);

        public static void Raise(EventType type, string arg)
        {
            if (type == EventType.None) return;
            ModBase.Log($"[Control] 执行自定义事件：{type}, {arg}");

            try
            {
                var args = arg?.Split('|') ?? new[] { "" };

                switch (type)
                {
                    case EventType.打开网页:
                        arg = arg.Replace('\\', '/');
                        if (!arg.Contains("://") || arg.StartsWithF("file", true))
                        {
                            ModMain.MyMsgBox("EventData 必须为一个网址。\r\n如果想要启动程序，请将 EventType 改为 打开文件。", "事件执行失败");
                            return;
                        }
                        ModMain.Hint("正在开启中，请稍候：" + arg);
                        ModBase.RunInThread(() => ModBase.OpenWebsite(arg));
                        break;

                    case EventType.打开文件:
                    case EventType.打开帮助:
                    case EventType.执行命令:
                        ModBase.RunInThread(() =>
                        {
                            try
                            {
                                var actualPaths = GetAbsoluteUrls(args[0], type);
                                string location = actualPaths[0], workingDir = actualPaths[1];
                                ModBase.Log($"[Control] 打开类自定义事件实际路径：{location}，工作目录：{workingDir}");

                                if (type == EventType.打开帮助)
                                {
                                    PageToolsHelp.EnterHelpPage(location);
                                }
                                else
                                {
                                    if (!EventSafetyConfirm($"即将执行：{location}{(args.Length >= 2 ? " " + args[1] : "")}"))
                                        return;
                                    ProcessInterop.Start(location, args.Length >= 2 ? args[1] : "");
                                }
                            }
                            catch (Exception ex)
                            {
                                ModBase.Log(ex, "执行打开类自定义事件失败", ModBase.LogLevel.Msgbox);
                            }
                        });
                        break;

                    case EventType.启动游戏:
                        if (args[0] == "\\current")
                        {
                            if (ModMinecraft.McInstanceSelected == null)
                            {
                                ModMain.Hint("请先选择一个 Minecraft 版本！", ModMain.HintType.Critical);
                                return;
                            }
                            args[0] = ModMinecraft.McInstanceSelected.Name;
                        }
                        ModBase.RunInUi(() =>
                        {
                            var options = new ModLaunch.McLaunchOptions
                            {
                                ServerIp = args.Length >= 2 ? args[1] : null,
                                Instance = new ModMinecraft.McInstance(args[0])
                            };
                            if (ModLaunch.McLaunchStart(options))
                            {
                                ModMain.Hint($"正在启动 {args[0]}……");
                            }
                        });
                        break;

                    case EventType.复制文本:
                        ModBase.ClipboardSet(arg);
                        break;

                    case EventType.刷新主页:
                    case EventType.刷新页面:
                        if (ModMain.FrmMain?.PageRight is IRefreshable refreshable)
                        {
                            ModBase.RunInUiWait(() => refreshable.Refresh());
                            if (string.IsNullOrEmpty(arg))
                                ModMain.Hint("已刷新！", ModMain.HintType.Finish);
                        }
                        else
                        {
                            ModMain.Hint("当前页面不支持刷新操作！", ModMain.HintType.Critical);
                        }
                        break;

                    case EventType.刷新主页市场:
                        ModMain.FrmHomePageMarket?.Refresh();
                        if (args[0] == "")
                            ModMain.Hint("已刷新主页市场！", ModMain.HintType.Finish);
                        break;

                    case EventType.刷新帮助:
                        ModBase.RunInUiWait(() => PageToolsLeft.RefreshHelp());
                        if (string.IsNullOrEmpty(arg))
                            ModMain.Hint("已刷新！", ModMain.HintType.Finish);
                        break;

                    case EventType.今日人品:
                        PageToolsTest.Jrrp();
                        break;

                    case EventType.内存优化:
                        if (PageToolsTest.AskTrulyWantMemoryOptimize())
                            ModBase.RunInThread(() => PageToolsTest.MemoryOptimize(true));
                        break;

                    case EventType.清理垃圾:
                        ModBase.RunInThread(() => PageToolsTest.RubbishClear());
                        break;

                    case EventType.弹出窗口:
                        if (args.Length == 1)
                            throw new Exception($"EventType {type} 需要至少 2 个以 | 分割的参数，例如 弹窗标题|弹窗内容");
                        ModMain.MyMsgBox(
                            args[1].Replace("\\n", "\r\n"),
                            args[0].Replace("\\n", "\r\n"),
                            args.Length > 2 ? args[2] : Lang.Text("Common.Action.Confirm"));
                        break;

                    case EventType.弹出提示:
                        {
                            var hintType = args.Length == 1
                                ? ModMain.HintType.Info
                                : (ModMain.HintType)Enum.Parse(typeof(ModMain.HintType), args[1], true);
                            ModMain.Hint(args[0].Replace("\\n", "\r\n"), hintType);
                        }
                        break;

                    case EventType.切换页面:
                        ModBase.RunInUi(() =>
                        {
                            var pageType = (FormMain.PageType)Enum.Parse(typeof(FormMain.PageType), args[0], true);
                            var subType = args.Length == 1
                                ? FormMain.PageSubType.Default
                                : (FormMain.PageSubType)Enum.Parse(typeof(FormMain.PageSubType), args[1], true);
                            ModMain.FrmMain?.PageChange(pageType, subType);
                        });
                        break;

                    case EventType.导入整合包:
                    case EventType.安装整合包:
                        ModBase.RunInUi(() => ModModpack.ModpackInstall());
                        break;

                    case EventType.下载文件:
                        args[0] = args[0].Replace('\\', '/');
                        if (!args[0].StartsWithF("http://", true) && !args[0].StartsWithF("https://", true))
                        {
                            ModMain.MyMsgBox("EventData 必须为以 http:// 或 https:// 开头的网址。\r\nPCL 不支持其他乱七八糟的下载协议。", "事件执行失败");
                            return;
                        }
                        if (!EventSafetyConfirm($"即将从该网址下载文件：\r\n{args[0]}"))
                            return;
                        try
                        {
                            switch (args.Length)
                            {
                                case 1:
                                    PageToolsTest.StartCustomDownload(args[0], ModBase.GetFileNameFromPath(args[0]));
                                    break;
                                case 2:
                                    PageToolsTest.StartCustomDownload(args[0], args[1]);
                                    break;
                                default:
                                    PageToolsTest.StartCustomDownload(args[0], args[1], args[2]);
                                    break;
                            }
                        }
                        catch
                        {
                            PageToolsTest.StartCustomDownload(args[0], Lang.Text("Common.State.Unknown"));
                        }
                        break;

                    case EventType.修改设置:
                    case EventType.写入设置:
                        if (args.Length == 1)
                            throw new Exception($"EventType {type} 需要至少 2 个以 | 分割的参数，例如 UiLauncherTransparent|400");
                        if (ConfigService.TryGetConfigItemNoType(args[0], out var item) && item.Source != ConfigSource.SharedEncrypt)
                            item.SetValueNoType(args[1], ModMinecraft.McInstanceSelected?.PathInstance);
                        if (args.Length == 2)
                            ModMain.Hint($"已写入设置：{args[0]} → {args[1]}", ModMain.HintType.Finish);
                        break;

                    case EventType.修改变量:
                    case EventType.写入变量:
                        if (args.Length == 1)
                            throw new Exception($"EventType {type} 需要至少 2 个以 | 分割的参数，例如 VariableName|Value");
                        States.CustomVariables[args[0]] = args[1];
                        States.CustomVariables = States.CustomVariables; // 触发属性变更通知
                        if (args.Length == 2)
                            ModMain.Hint($"已写入变量：{args[0]} → {args[1]}", ModMain.HintType.Finish);
                        break;

                    default:
                        ModMain.MyMsgBox($"未知的事件类型：{type}\r\n请检查事件类型填写是否正确，或者 PCL 是否为最新版本。", "事件执行失败");
                        break;
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"事件执行失败（{type}, {arg}）", ModBase.LogLevel.Msgbox);
            }
        }

        public static string GetCustomVariable(string name, string defaultValue = "") =>
            States.CustomVariables.TryGetValue(name, out var value) ? value : defaultValue;

        public static string[] GetAbsoluteUrls(string relativeUrl, EventType type)
        {
            // 联网帮助页面处理
            if (relativeUrl.StartsWithF("http", true))
            {
                if (ModBase.RunInUi())
                    throw new Exception("能打开联网帮助页面的 MyListItem 必须手动设置 Title、Info 属性！");

                string rawFileName;
                try
                {
                    rawFileName = ModBase.GetFileNameFromPath(relativeUrl);
                    if (!rawFileName.EndsWithF(".json", true))
                        throw new Exception("未指向 .json 后缀的文件");
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        "联网帮助页面须指向一个帮助 JSON 文件，并在同路径下包含相应 XAML 文件！\r\n例如：\r\n - https://www.baidu.com/test.json（填写这个路径）\r\n - https://www.baidu.com/test.xaml（同时也需要包含这个文件）",
                        ex);
                }

                string localTemp = ModMain.RequestTaskTempFolder() + rawFileName;
                ModBase.Log($"[Event] 转换网络资源：{relativeUrl} -> {localTemp}");
                try
                {
                    // 修正：直接调用静态方法 NetDownloadByClient，而不是 .Download
                    FileDownloader.Download(relativeUrl, localTemp).GetAwaiter().GetResult();
                    FileDownloader.Download(relativeUrl.Replace(".json", ".xaml"), localTemp.Replace(".json", ".xaml"))
                        .GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        "下载指定的文件失败！\r\n注意，联网帮助页面须指向一个帮助 JSON 文件，并在同路径下包含相应 XAML 文件！\r\n例如：\r\n - https://www.baidu.com/test.json（填写这个路径）\r\n - https://www.baidu.com/test.xaml（同时也需要包含这个文件）",
                        ex);
                }
                relativeUrl = localTemp;
            }

            relativeUrl = relativeUrl.Replace('/', '\\').ToLower().TrimStart('\\');

            string location, workingDir = System.IO.Path.Combine(Basics.ExecutableDirectory, "PCL");
            ModMain.HelpExtract();

            if (relativeUrl.Contains(":\\"))
            {
                location = relativeUrl;
                ModBase.Log($"[Control] 自定义事件中由绝对路径{type}：{location}");
            }
            else if (File.Exists(System.IO.Path.Combine(Basics.ExecutableDirectory, "PCL", relativeUrl)))
            {
                location = System.IO.Path.Combine(Basics.ExecutableDirectory, "PCL", relativeUrl);
                ModBase.Log($"[Control] 自定义事件中由相对 PCL 文件夹的路径{type}：{location}");
            }
            else if (File.Exists(System.IO.Path.Combine(Basics.ExecutableDirectory, "PCL", "Help", relativeUrl)))
            {
                location = System.IO.Path.Combine(Basics.ExecutableDirectory, "PCL", "Help", relativeUrl);
                workingDir = System.IO.Path.Combine(Basics.ExecutableDirectory, "PCL", "Help");
                ModBase.Log($"[Control] 自定义事件中由相对 PCL 本地帮助文件夹的路径{type}：{location}");
            }
            else if (type == EventType.打开帮助 && File.Exists(System.IO.Path.Combine(ModBase.PathTemp, "CE", "Help", relativeUrl)))
            {
                location = System.IO.Path.Combine(ModBase.PathTemp, "CE", "Help", relativeUrl);
                workingDir = System.IO.Path.Combine(ModBase.PathTemp, "CE", "Help");
                ModBase.Log($"[Control] 自定义事件中由相对 PCL 自带帮助文件夹的路径{type}：{location}");
            }
            else if (type == EventType.打开文件 || type == EventType.执行命令)
            {
                location = relativeUrl;
                ModBase.Log($"[Control] 自定义事件中直接{type}：{location}");
            }
            else
            {
                throw new FileNotFoundException($"未找到 EventData 指向的本地 xaml 文件：{relativeUrl}", relativeUrl);
            }

            return new[] { location, workingDir };
        }

        private static bool EventSafetyConfirm(string message)
        {
            if (States.Hint.HomepageCommand)
                return true;

            switch (ModMain.MyMsgBox(
                message + "\r\n请在确认没有安全隐患后再继续。",
                "执行确认",
                "继续",
                "继续且今后不再要求确认",
                Lang.Text("Common.Action.Cancel")))
            {
                case 1:
                    return true;
                case 2:
                    States.Hint.HomepageCommand = true;
                    return true;
                default:
                    return false;
            }
        }
    }
}
