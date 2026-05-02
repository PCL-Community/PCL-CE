using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FluentValidation;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Core.UI;
using PCL.Core.Utils;


namespace PCL;

public static partial class ModMain
{
    #region 帮助

    public class HelpEntry
    {
        /// <summary>
        ///     显示描述。
        /// </summary>
        public string Desc;

        public string EventData;
        public string EventType;

        // 动作

        /// <summary>
        ///     是否为 “执行事件”。
        /// </summary>
        public bool IsEvent;

        // 显示（可选）

        /// <summary>
        ///     帮助项的自定义图标。可能为 Nothing。
        /// </summary>
        public string Logo;

        /// <summary>
        ///     原始信息路径。用于刷新。
        /// </summary>
        public string RawPath;

        /// <summary>
        ///     检索关键字。
        /// </summary>
        public string Search;

        /// <summary>
        ///     是否在公开版的 PCL 中显示（这会影响主页与搜索）。默认为 True。
        /// </summary>
        public bool ShowInPublic = true;

        /// <summary>
        ///     是否显示在搜索结果。默认为 True。
        /// </summary>
        public bool ShowInSearch = true;

        /// <summary>
        ///     是否在快照版的 PCL 中显示（这会影响主页与搜索）。默认为 True。
        /// </summary>
        public bool ShowInSnapshot = true;

        // 基础

        /// <summary>
        ///     显示标题。
        /// </summary>
        public string Title;

        /// <summary>
        ///     用于分类的标签列表。
        /// </summary>
        public List<string> Types;

        /// <summary>
        ///     若非执行事件，其对应的 .xaml 本地文件内容。
        /// </summary>
        public string XamlContent;

        // 转换

        /// <summary>
        ///     从文件初始化 HelpEntry 对象，失败会抛出异常。
        /// </summary>
        public HelpEntry(string FilePath)
        {
            RawPath = FilePath;
            var JsonData = (JObject)ModBase.GetJson(ModMain.ArgumentReplace(ModBase.ReadFile(FilePath)));
            if (JsonData is null)
                throw new FileNotFoundException("未找到帮助文件：" + FilePath, FilePath);
            // 加载常规信息
            if (JsonData["Title"] is not null)
                Title = (string)JsonData["Title"];
            else
                throw new ArgumentException("未找到 Title 项");
            Desc = (string)(JsonData["Description"] ?? "");
            Search = (string)(JsonData["Keywords"] ?? "");
            Logo = (string)JsonData["Logo"]; // 为保持 Nothing，不要加 If
            ShowInSearch = (bool)(JsonData["ShowInSearch"] ?? ShowInSearch);
            ShowInPublic = (bool)(JsonData["ShowInPublic"] ?? ShowInPublic);
            ShowInSnapshot = (bool)(JsonData["ShowInSnapshot"] ?? ShowInSnapshot);
            Types = new List<string>();
            foreach (var NameOfType in (IEnumerable)(JsonData["Types"] ?? ModBase.GetJson("[]")))
                Types.Add(NameOfType.ToString());
            // 加载事件信息
            if ((bool)(JsonData["IsEvent"] ?? false))
            {
                EventType = Enum.Parse(typeof(CustomEvent.EventType), JsonData["EventType"].ToString()).ToString();
                EventData = (JsonData["EventData"] ?? "").ToString();
                IsEvent = true;
            }
            else
            {
                var XamlAddress = FilePath.ToLower().Replace(".json", ".xaml");
                if (File.Exists(XamlAddress))
                {
                    XamlContent = ModBase.ReadFile(XamlAddress);
                    IsEvent = false;
                }
                else
                {
                    throw new FileNotFoundException("未找到帮助条目 .json 对应的 .xaml 文件（" + XamlAddress + "）");
                }
            }
        }

        /// <summary>
        ///     获取该 HelpEntry 对应的 MyListItem。
        /// </summary>
        public MyListItem ToListItem()
        {
            return SetToListItem(new MyListItem());
        }

        /// <summary>
        ///     将属性设置入一个现有的 ListItem。
        /// </summary>
        public MyListItem SetToListItem(MyListItem Item)
        {
            string Logo;
            if (IsEvent)
            {
                if (EventType == "弹出窗口")
                    Logo = ModBase.PathImage + "Blocks/GrassPath.png";
                else
                    Logo = ModBase.PathImage + "Blocks/CommandBlock.png";
            }
            else
            {
                Logo = ModBase.PathImage + "Blocks/Grass.png";
            }

            // 设置属性
            Item.SnapsToDevicePixels = true;
            Item.Title = Title;
            Item.Info = Desc;
            Item.Logo = this.Logo ?? Logo;
            Item.Height = 42d;
            Item.Type = MyListItem.CheckType.Clickable;
            Item.Tag = this;
            CustomEventService.SetEventType(Item, CustomEvent.EventType.None); //清空自定义事件属性，它们会被下面的点击事件处理
            CustomEventService.SetEventData(Item, null);
            // 项目的点击事件
            Item.Click += (sender, e) => PageToolsHelp.OnItemClick((HelpEntry)((MyListItem)sender).Tag);
            return Item;
        }
    }


    private static readonly object HelpLoadLock = new();

    /// <summary>
    ///     初始化帮助列表对象。
    /// </summary>
    private static void HelpLoad(ModLoader.LoaderTask<int, List<HelpEntry>> Loader)
    {
        lock (HelpLoadLock) // 避免重复解压文件导致出错
        {
            try
            {
                // 解压内置文件
                HelpExtract();

                // 遍历文件
                var FileList = new List<string>();
                try
                {
                    var IgnoreList = new List<string>();
                    // 读取自定义文件
                    if (Directory.Exists(ModBase.ExePath + @"PCL\Help\"))
                        foreach (var File in ModBase.EnumerateFiles(ModBase.ExePath + @"PCL\Help\"))
                            switch (File.Extension.ToLower() ?? "")
                            {
                                case ".helpignore":
                                {
                                    // 加载忽略列表
                                    ModBase.Log("[Help] 发现 .helpignore 文件：" + File.FullName);
                                    foreach (var Line in ModBase.ReadFile(File.FullName)
                                                 .Split("\r\n".ToCharArray()))
                                    {
                                        var RealString = Line.BeforeFirst("#").Trim();
                                        if (string.IsNullOrWhiteSpace(RealString))
                                            continue;
                                        IgnoreList.Add(RealString);
                                        if (ModBase.ModeDebug)
                                            ModBase.Log("[Help]  > " + RealString);
                                    }

                                    break;
                                }
                                case ".json":
                                {
                                    FileList.Add(File.FullName);
                                    break;
                                }
                            }

                    ModBase.Log("[Help] 已扫描 PCL 文件夹下的帮助文件，目前总计 " + FileList.Count + " 条");
                    // 读取自带文件
                    foreach (var File in ModBase.EnumerateFiles(ModBase.PathHelpFolder))
                    {
                        // 跳过非 Json 文件与以 . 开头的文件夹
                        if (File.Extension.ToLower() != ".json" || File.Directory.FullName
                                .Replace(ModBase.PathHelpFolder.TrimEnd('\\'), "").Contains(@"\."))
                            continue;
                        // 检查忽略列表
                        var RealPath = File.FullName.Replace(ModBase.PathHelpFolder.TrimEnd('\\'), "");
                        foreach (var Ignore in IgnoreList)
                            if (RealPath.RegexCheck(Ignore))
                            {
                                if (ModBase.ModeDebug)
                                    ModBase.Log("[Help] 已忽略 " + RealPath + "：" + Ignore);
                                goto NextFile;
                            }

                        FileList.Add(File.FullName);
                        NextFile: ;
                    }

                    ModBase.Log("[Help] 已扫描缓存文件夹下的帮助文件，目前总计 " + FileList.Count + " 条");
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "检查帮助文件夹失败", ModBase.LogLevel.Msgbox);
                }

                if (Loader.IsAborted)
                    return;

                // 将文件实例化
                var Dict = new List<HelpEntry>();
                foreach (var FilePath in FileList)
                    try
                    {
                        var Entry = new HelpEntry(FilePath);
                        Dict.Add(Entry);
                        if (ModBase.ModeDebug)
                            ModBase.Log("[Help] 已加载的帮助条目：" + Entry.Title + " ← " + FilePath);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "初始化帮助条目失败（" + FilePath + "）", ModBase.LogLevel.Msgbox);
                    }

                // 回设
                if (!Dict.Any())
                    throw new Exception("未找到可用的帮助；若不需要帮助页面，可以在 设置 → 个性化 → 功能隐藏 中将其隐藏");
                if (Loader.IsAborted)
                    return;
                Loader.Output = Dict;
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "帮助列表初始化失败");
                throw;
            }
        }
    }

    /// <summary>
    ///     解压内置帮助文件。
    /// </summary>
    public static void HelpExtract()
    {
        ModBase.DeleteDirectory(ModBase.PathTemp + @"CE\Help");
        Directory.CreateDirectory(ModBase.PathTemp + @"CE\Help");
        ModBase.WriteFile(ModBase.PathTemp + @"CE\Cache\Help.zip", ModBase.GetResourceStream("Resources/Help.zip"));
        ModBase.ExtractFile(ModBase.PathTemp + @"CE\Cache\Help.zip", ModBase.PathTemp + @"CE\Help", Encoding.UTF8);
        ModBase.Log("[Help] 已解压内置帮助文件，目前状态：" + File.Exists(ModBase.PathTemp + @"CE\Help\启动器\备份设置.xaml"),
            ModBase.LogLevel.Debug);
    }

    /// <summary>
    ///     对帮助文件约定的替换标记进行处理，如果遇到需要转义的字符会进行转义。
    /// </summary>
    public static string HelpArgumentReplace(string Xaml)
    {
        var Result = Xaml.Replace("{path}", ModBase.EscapeXML(ModBase.ExePath));
        Result = Result.RegexReplaceEach(@"\{hint\}", _ => ModBase.EscapeXML(PageToolsTest.GetRandomHint()));
        Result = Result.RegexReplaceEach(@"\{cave\}", _ => ModBase.EscapeXML(PageToolsTest.GetRandomCave()));
        return Result;
    }

    #endregion

    #region 系统

    /// <summary>
    ///     把某个 PCL 窗口拖到最前面。
    /// </summary>
    public static void ShowWindowToTop(nint Handle)
    {
        try
        {
            PostMessage(Handle, 400 * 16 + 2, 0L, 0L);
            SetForegroundWindow(Handle); // 不在这里放不行，神秘 WinAPI，建议别动
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "设置窗口置顶失败", ModBase.LogLevel.Hint);
        }
    }

    [DllImport("user32", EntryPoint = "FindWindowA")]
    public static extern nint FindWindow(string ClassName, string WindowName);

    [DllImport("user32")]
    public static extern int SetForegroundWindow(nint hWnd);

    [DllImport("user32", EntryPoint = "PostMessageA")]
    private static extern bool PostMessage(nint hWnd, uint msg, long wParam, long lParam);

    /// <summary>
    ///     将特定程序设置为使用高性能显卡启动。
    ///     如果失败，则抛出异常。
    /// </summary>
    public static void SetGPUPreference(string Executeable, bool WantHighPerformance = true)
    {
        const string GPU_PERFERENCE_REG_KEY = @"Software\Microsoft\DirectX\UserGpuPreferences";
        const string GPU_PERFERENCE_REG_VALUE_HIGH = "GpuPreference=2;";
        const string GPU_PERFERENCE_REG_VALUE_DEFAULT = "GpuPreference=0;";
        // Const GPU_PERFERENCE_REG_VALUE_POWER_SAVING As String = "GpuPreference=1;"

        var IsCurrentHighPerformance = false;
        // 查看现有设置
        // 就知道 My.Computer，改个注册表 Microsoft.Win32.Registry 几年前的 API 了不用，还在这 My.Computer 都 5202 年了 My 你大爷
        using (var ReadOnlyKey = Registry.CurrentUser.OpenSubKey(GPU_PERFERENCE_REG_KEY, false))
        {
            if (ReadOnlyKey is not null)
            {
                var CurrentValue = ReadOnlyKey.GetValue(Executeable);
                if (GPU_PERFERENCE_REG_VALUE_HIGH == (CurrentValue?.ToString() ?? "")) IsCurrentHighPerformance = true;
            }
            else
            {
                // 创建父级键
                ModBase.Log("[System] 需要创建显卡设置的父级键");
                Registry.CurrentUser.CreateSubKey(GPU_PERFERENCE_REG_KEY);
            }
        }

        ModBase.Log($"[System] 当前程序 ({Executeable}) 的显卡设置为高性能: {IsCurrentHighPerformance}");
        if (IsCurrentHighPerformance ^ WantHighPerformance)
            // 写入新设置
            using (var WriteKey = Registry.CurrentUser.OpenSubKey(GPU_PERFERENCE_REG_KEY, true))
            {
                WriteKey.SetValue(Executeable,
                    WantHighPerformance ? GPU_PERFERENCE_REG_VALUE_HIGH : GPU_PERFERENCE_REG_VALUE_DEFAULT);
                ModBase.Log($"[System] 已调整程序 ({Executeable}) 显卡设置: {WantHighPerformance}");
            }
    }

    /// <summary>
    /// 对替换标记进行处理。会对替换内容使用 EscapeHandler 进行转义。
    /// /// </summary>
    public static string ArgumentReplace(string text, Func<string, string> escapeHandler = null, bool replaceTime = true) 
    {
    // 预处理
    if (text == null) return null;
    
    Func<string, string> replacer = (s) =>
    {
        if (s == null) return "";
        if (escapeHandler == null) return s;
        if (s.Contains(":\\")) s = ModBase.ShortenPath(s);
        return escapeHandler(s);
    };
    
    // 基础
    text = text.Replace("{pcl_version}", replacer(ModBase.VersionBaseName));
    text = text.Replace("{pcl_version_code}", replacer(ModBase.VersionCode.ToString()));
    text = text.Replace("{pcl_version_branch}", replacer(ModBase.VersionBranchName));
    text = text.Replace("{pcl_branch}", replacer(ModBase.VersionBranchName));
    text = text.Replace("{identify}", replacer(ModBase.UniqueAddress));
    text = text.Replace("{path}", replacer(Basics.ExecutableDirectory));
    text = text.Replace("{path_with_name}", replacer(Basics.ExecutableName));
    text = text.Replace("{path_temp}", replacer(ModBase.PathTemp));
    
    // 时间
    if (replaceTime) // 在窗口标题中，时间会被后续动态替换，所以此时不应该替换
    {
        text = text.Replace("{date}", replacer(DateTime.Now.ToString("yyyy/M/d")));
        text = text.Replace("{time}", replacer(DateTime.Now.ToString("HH:mm:ss")));
    }
    
    // Minecraft
    text = text.Replace("{java}", replacer(ModLaunch.McLaunchJavaSelected?.Installation.JavaFolder));
    text = text.Replace("{minecraft}", replacer(ModMinecraft.McFolderSelected));
    
    if (ModMinecraft.McInstanceSelected != null)
    {
        text = text.Replace("{version_path}", replacer(ModMinecraft.McInstanceSelected.PathInstance));
        text = text.Replace("{verpath}", replacer(ModMinecraft.McInstanceSelected.PathInstance));
        text = text.Replace("{version_indie}", replacer(ModMinecraft.McInstanceSelected.PathIndie));
        text = text.Replace("{verindie}", replacer(ModMinecraft.McInstanceSelected.PathIndie));
        text = text.Replace("{name}", replacer(ModMinecraft.McInstanceSelected.Name));
        
        if (new[] { "unknown", "old", "pending" }.Contains(ModMinecraft.McInstanceSelected.Info.VanillaName))
        {
            text = text.Replace("{version}", replacer(ModMinecraft.McInstanceSelected.Name));
        }
        else
        {
            text = text.Replace("{version}", replacer(ModMinecraft.McInstanceSelected.Info.VanillaName));
        }
    }
    else
    {
        text = text.Replace("{version_path}", replacer(null));
        text = text.Replace("{verpath}", replacer(null));
        text = text.Replace("{version_indie}", replacer(null));
        text = text.Replace("{verindie}", replacer(null));
        text = text.Replace("{name}", replacer(null));
        text = text.Replace("{version}", replacer(null));
    }
    
    // 验证信息
    if (ModLaunch.McLoginLoader.State == LoadState.Finished)
    {
        text = text.Replace("{user}", replacer(ModLaunch.McLoginLoader.Output.Name));
        text = text.Replace("{uuid}", replacer(ModLaunch.McLoginLoader.Output.Uuid.ToLower()));
        
        switch (ModLaunch.McLoginLoader.Input.Type)
        {
            case ModLaunch.McLoginType.Legacy:
                text = text.Replace("{login}", replacer("离线"));
                break;
            case ModLaunch.McLoginType.Ms:
                text = text.Replace("{login}", replacer("正版"));
                break;
            case ModLaunch.McLoginType.Auth:
                text = text.Replace("{login}", replacer("Authlib-Injector"));
                break;
        }
    }
    else
    {
        text = text.Replace("{user}", replacer(null));
        text = text.Replace("{uuid}", replacer(null));
        text = text.Replace("{login}", replacer(null));
    }
    
    // 高级
    text = text.RegexReplaceEach(@"\{hint\}", m => replacer(PageToolsTest.GetRandomHint()));
    text = text.RegexReplaceEach(@"\{cave\}", m => replacer(PageToolsTest.GetRandomCave()));
    text = text.RegexReplaceEach(@"\{setup:([a-zA-Z0-9]+)\}", m => replacer(ModBase.Setup.GetSafe(m.Groups[1].Value, ModMinecraft.McInstanceSelected)?.ToString() ?? ""));
    text = text.RegexReplaceEach(@"\{varible:([^\}]+)\}", m => replacer(CustomEvent.GetCustomVariable(m.Groups[1].Value)));
    text = text.RegexReplaceEach(@"\{variable:([^\}]+)\}", m => replacer(CustomEvent.GetCustomVariable(m.Groups[1].Value)));
    
    return text;
}
    #endregion

    #region 任务缓存

    private static bool IsTaskTempCleared;
    private static bool IsTaskTempClearing;

    /// <summary>
    ///     尝试清理任务缓存文件夹。
    ///     在整次运行中只会实际清理一次。
    /// </summary>
    public static void TryClearTaskTemp()
    {
        if (!IsTaskTempCleared)
        {
            IsTaskTempCleared = true;
            IsTaskTempClearing = true;
            try
            {
                ModBase.Log("[System] 开始清理任务缓存文件夹");
                ModBase.DeleteDirectory($@"{ModBase.OsDrive}ProgramData\PCL\TaskTemp\");
                ModBase.DeleteDirectory($@"{ModBase.PathTemp}TaskTemp\");
                ModBase.Log("[System] 已清理任务缓存文件夹");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "清理任务缓存文件夹失败");
            }
            finally
            {
                IsTaskTempClearing = false;
            }
        }
        else if (IsTaskTempClearing)
        {
            // 等待另一个清理步骤完成
            while (IsTaskTempClearing)
                Thread.Sleep(1);
        }
    }

    /// <summary>
    ///     申请一个可用于任务缓存的临时文件夹，以 \ 结尾。这些文件夹无需进行后续清理。
    ///     若所有缓存位置均没有权限，会抛出异常。
    /// </summary>
    /// <param name="RequireNonSpace">是否要求路径不包含空格。</param>
    public static string RequestTaskTempFolder(bool RequireNonSpace = false)
    {
        TryClearTaskTemp();
        string ResultFolder;
        do
        {
            try
            {
                ResultFolder = $@"{ModBase.PathTemp}TaskTemp\{ModBase.GetUuid()}-{RandomUtils.NextInt(0, 1000000)}\";
                if (RequireNonSpace && ResultFolder.Contains(" "))
                    break; // 带空格
                Directory.CreateDirectory(ResultFolder);
                ModBase.CheckPermissionWithException(ResultFolder);
                return ResultFolder;
            }
            catch
            {
            }
        } while (false);

        // 使用备用路径
        ResultFolder =
            $@"{ModBase.OsDrive}ProgramData\PCL\TaskTemp\{ModBase.GetUuid()}-{RandomUtils.NextInt(0, 1000000)}\";
        Directory.CreateDirectory(ResultFolder);
        ModBase.CheckPermission(ResultFolder);
        return ResultFolder;
    }

    #endregion

    public static void RaiseCustomEvent(DependencyObject control)
    {
        // 收集事件列表
        var events = CustomEventService.GetEvents(control).ToList();
        var eventType = CustomEventService.GetEventType(control);
        if (eventType != CustomEvent.EventType.None)
            events.Add(new CustomEvent(eventType, CustomEventService.GetEventData(control)));

        if (!events.Any()) return;

        ModBase.RunInNewThread(() =>
            {
                foreach (var e in events)
                    e.Raise();
            }, $"执行自定义事件 {ModBase.GetUuid()}");
    }
}
