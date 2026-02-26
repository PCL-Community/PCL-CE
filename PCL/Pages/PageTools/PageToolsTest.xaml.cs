using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using PCL.Core.App;
using PCL.Core.IO;
using PCL.Core.IO.Net;
using PCL.Core.UI;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;

namespace PCL;

public partial class PageToolsTest
{
    private static object IsMemoryOptimizing;
    private Bitmap CurrentSkinBitmap;
    private Bitmap GeneratedHeadBitmap;

    private int HeadSize = 64;
    private string skinPath = "";

    public PageToolsTest()
    {
        InitializeComponent();
        BtnSelectSkin.Click += BtnSelectSkin_Click;
        CmbHeadSize.SelectionChanged += CmbHeadSize_SelectionChanged;
        Loaded += (_, _) => MeLoaded();
    }

    private void MeLoaded()
    {
        BtnDownloadStart.IsEnabled = false;

        TextDownloadFolder.Text = Conversions.ToString(States.Tool.DownloadFolder);
        TextDownloadFolder.Validate();

        if (!string.IsNullOrEmpty(TextDownloadFolder.ValidateResult) || string.IsNullOrEmpty(TextDownloadFolder.Text))
            TextDownloadFolder.Text = ModBase.ExePath + @"PCL\MyDownload\";

        TextDownloadFolder.Validate();
        TextDownloadName.Validate();
        TextUserAgent.Text = Conversions.ToString(States.Tool.DownloadUserAgent);
    }

    private void StartButtonRefresh()
    {
        BtnDownloadStart.IsEnabled = string.IsNullOrEmpty(TextDownloadFolder.ValidateResult) &&
                                     string.IsNullOrEmpty(TextDownloadUrl.ValidateResult) &&
                                     string.IsNullOrEmpty(TextDownloadName.ValidateResult);

        BtnDownloadOpen.IsEnabled = string.IsNullOrEmpty(TextDownloadFolder.ValidateResult);

        BtnAchievementPreview.IsEnabled = string.IsNullOrEmpty(AchievementBlockTextBox.ValidateResult) &&
                                          string.IsNullOrEmpty(AchievementTitleTextBox.ValidateResult) &&
                                          string.IsNullOrEmpty(AchievementString1TextBox.ValidateResult);

        BtnAchievementSave.IsEnabled = string.IsNullOrEmpty(AchievementBlockTextBox.ValidateResult) &&
                                       string.IsNullOrEmpty(AchievementTitleTextBox.ValidateResult) &&
                                       string.IsNullOrEmpty(AchievementString1TextBox.ValidateResult);
    }

    private void SaveCacheDownloadFolder(object sender, RoutedEventArgs e)
    {
        States.Tool.DownloadFolder = TextDownloadFolder.Text;
        TextDownloadName.Validate();
    }

    private void SaveCustomUserAgent(object sender, RoutedEventArgs e)
    {
        States.Tool.DownloadUserAgent = TextUserAgent.Text;
    }

    private static void DownloadState(ModLoader.LoaderCombo<int> Loader)
    {
        try
        {
            switch (Loader.State)
            {
                case ModBase.LoadState.Finished:
                {
                    ModMain.Hint(Loader.Name + "完成！", ModMain.HintType.Finish);
                    Interaction.Beep();
                    break;
                }
                case ModBase.LoadState.Failed:
                {
                    ModBase.Log(Loader.Error, Loader.Name + "失败", ModBase.LogLevel.Msgbox);
                    Interaction.Beep();
                    break;
                }
                case ModBase.LoadState.Aborted:
                {
                    ModMain.Hint(Loader.Name + "已取消！");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
        }
    }

    public static void StartCustomDownload(string Url, string FileName, string Folder = null, string UserAgent = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Folder))
            {
                Folder = SystemDialogs.SelectSaveFile("选择文件保存位置", FileName);
                if (!Folder.Contains(@"\")) return;
                if (Folder.EndsWith(FileName)) Folder = Strings.Mid(Folder, 1, Folder.Length - FileName.Length);
            }

            Folder = Folder.Replace("/", @"\").TrimEnd(new[] { '\\' }) + @"\";
            try
            {
                Directory.CreateDirectory(Folder);
                ModBase.CheckPermissionWithException(Folder);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "访问文件夹失败（" + Folder + "）", ModBase.LogLevel.Hint);
                return;
            }

            ModBase.Log("[Download] 自定义下载文件名：" + FileName);
            ModBase.Log("[Download] 自定义下载文件目标：" + Folder);
            var uuid = ModBase.GetUuid();
            ModLoader.LoaderBase loaderdownload;
            if (string.IsNullOrEmpty(new ValidateHttp().Validate(Url)))
                loaderdownload = new ModNet.LoaderDownload("自定义下载文件：" + FileName + " ",
                    new List<ModNet.NetFile> { new(new[] { Url }, Folder + FileName, null, true, UserAgent) });
            else // UNC 路径
                loaderdownload = new ModNet.LoaderDownloadUnc("自定义下载文件：" + FileName + " ",
                    new Tuple<string, string>(Url, Folder + FileName));
            var loaderCombo = new ModLoader.LoaderCombo<int>("自定义下载 (" + uuid + ") ", new[] { loaderdownload })
                { OnStateChanged = a => DownloadState((dynamic)a) };
            loaderCombo.Start();
            ModLoader.LoaderTaskbarAdd(loaderCombo);
            ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
            ModMain.FrmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "开始自定义下载失败", ModBase.LogLevel.Feedback);
        }
    }

    public static void Jrrp()
    {
        var random = new Random(GenerateDailySeed());
        var luckValue = random.Next(0, 101);
        var rating = GetRating(luckValue);
        var currentDate = DateTime.Now.ToString("yyyy/MM/dd");
        var title = $"今日人品 - {currentDate}";

        if (luckValue >= 60)
            ModMain.MyMsgBox($"你今天的人品值是：{luckValue}！{rating}", title);
        else
            ModMain.MyMsgBox($"你今天的人品值是：{luckValue}... {rating}", title, IsWarn: luckValue <= 30);
    }

    public static void RubbishClear()
    {
        ModBase.RunInUi(() =>
        {
            if (!(ModMain.FrmToolsTest == null) && !(ModMain.FrmToolsTest.BtnClear == null))
                ModMain.FrmToolsTest.BtnClear.IsEnabled = false;
        });
        // 只有当没有运行中的Minecraft游戏且启动器不在加载状态时才能清理

        // 清理的文件数量
        // 所有 Minecraft 文件夹


        // 寻找所有 Minecraft 文件夹

        // 删除 Minecraft 的缓存
        // 删除日志和崩溃报告并计数

        // 删除 Natives 文件

        // 删除 PCL 的缓存

        ModBase.RunInNewThread(() =>
        {
            try
            {
                if (!ModWatcher.HasRunningMinecraft && ModLaunch.McLaunchLoader.State != ModBase.LoadState.Loading)
                {
                    if (ModNet.HasDownloadingTask())
                    {
                        ModMain.Hint("请在所有下载任务完成后再来清理吧……");
                        return;
                    }

                    if (!ModMinecraft.McFolderList.Any()) ModMinecraft.McFolderListLoader.Start();
                    if (Conversions.ToBoolean(
                            Operators.ConditionalCompareObjectLessEqual(States.Hint.CleanJunkFile, 2,
                                false)))
                    {
                        if (ModMain.MyMsgBox(
                                "即将清理游戏日志、错误报告、缓存等文件。" + Constants.vbCrLf + "虽然应该没人往这些地方放重要文件，但还是问一下，是否确认继续？" +
                                Constants.vbCrLf + Constants.vbCrLf + "在完成清理后，PCL 将自动重启。", "清理确认", "确定", "取消") ==
                            2) return;
                        States.Hint.CleanJunkFile += 1;
                    }

                    var num = 0;
                    var cleanMcFolderList = new List<DirectoryInfo>();
                    if (!ModMinecraft.McFolderList.Any()) ModMinecraft.McFolderListLoader.WaitForExit();
                    foreach (var mcFolder in ModMinecraft.McFolderList)
                    {
                        cleanMcFolderList.Add(new DirectoryInfo(mcFolder.Location));
                        var dirInfo = new DirectoryInfo(mcFolder.Location + "versions");
                        if (dirInfo.Exists)
                            foreach (var item in dirInfo.EnumerateDirectories())
                                cleanMcFolderList.Add(item);
                    }

                    foreach (var dirInfo in cleanMcFolderList)
                    {
                        num += ModBase.DeleteDirectory(
                            dirInfo.FullName + (dirInfo.FullName.EndsWith(@"\") ? "" : @"\") + @"crash-reports\", true);
                        num += ModBase.DeleteDirectory(
                            dirInfo.FullName + (dirInfo.FullName.EndsWith(@"\") ? "" : @"\") + @"logs\", true);
                        foreach (var fileInfo in dirInfo.EnumerateFiles("*"))
                            if (fileInfo.Name.StartsWith("hs_err_pid") || fileInfo.Name.EndsWith(".log") ||
                                fileInfo.Name == "WailaErrorOutput.txt")
                            {
                                fileInfo.Delete();
                                num += 1;
                            }

                        foreach (var dirInfo2 in dirInfo.EnumerateDirectories())
                            if ((dirInfo2.Name ?? "") == (dirInfo2.Name + "-natives" ?? "") ||
                                dirInfo2.Name == "natives-windows-x86_64")
                                num += ModBase.DeleteDirectory(dirInfo2.FullName, true);
                    }

                    num += ModBase.DeleteDirectory(ModBase.PathTemp, true);
                    num += ModBase.DeleteDirectory(ModBase.OsDrive + @"ProgramData\PCL\", true);
                    if (num != 0)
                    {
                        ModMain.MyMsgBox(string.Format("清理了 {0} 个文件！", num) + Constants.vbCrLf + "PCL 即将自动重启……",
                            "缓存已清理", "确定", "", "", false, true, true);
                        Process.Start(new ProcessStartInfo(ModBase.ExePathWithName));
                        FormMain.EndProgramForce();
                    }
                    else
                    {
                        ModMain.Hint("没有找到任何可以清理的文件！");
                    }
                }
                else
                {
                    ModMain.Hint("请先关闭所有运行中的游戏……");
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "清理垃圾失败", ModBase.LogLevel.Hint);
            }
            finally
            {
                ModBase.RunInUiWait(() =>
                {
                    if (!(ModMain.FrmToolsTest == null) && !(ModMain.FrmToolsTest.BtnClear == null))
                        ModMain.FrmToolsTest.BtnClear.IsEnabled = true;
                });
            }
        }, "Rubbish Clear");
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern nint GetCurrentProcess();

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern bool CloseHandle(nint handle);

    [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
    private static extern bool OpenProcessToken(HandleRef ProcessHandle, int DesiredAccess, out nint TokenHandle);

    [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
    private static extern bool LookupPrivilegeValue([MarshalAs(UnmanagedType.LPTStr)] string lpSystemName,
        [MarshalAs(UnmanagedType.LPTStr)] string lpName, out LUID lpLuid);

    [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
    private static extern bool AdjustTokenPrivileges(HandleRef TokenHandle, bool DisableAllPrivileges,
        TokenPrivileges NewState, int BufferLength, nint PreviousState, nint ReturnLength);

    [DllImport("ntdll.dll", CharSet = CharSet.Ansi)]
    private static extern uint NtSetSystemInformation(int SystemInformationClass, nint SystemInformation,
        int SystemInformationLength);

    public static void MemoryOptimize(bool ShowHint)
    {
        if (Conversions.ToBoolean(IsMemoryOptimizing))
        {
            if (ShowHint) ModMain.Hint("内存优化尚未结束，请稍等！");
        }
        else
        {
            IsMemoryOptimizing = true;
            long num;
            if (ProcessInterop.IsAdmin())
            {
                num = (long)KernelInterop.GetAvailablePhysicalMemoryBytes();
                try
                {
                    MemoryOptimizeInternal(ShowHint);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "内存优化失败", ShowHint ? ModBase.LogLevel.Hint : ModBase.LogLevel.Debug);
                    return;
                }
                finally
                {
                    IsMemoryOptimizing = false;
                }

                num = Convert.ToInt64(decimal.Subtract(new decimal(KernelInterop.GetAvailablePhysicalMemoryBytes()),
                    new decimal(num)));
            }
            else
            {
                ModBase.Log("[Test] 没有管理员权限，将以命令行方式进行内存优化");
                try
                {
                    num = ProcessInterop.StartAsAdmin("--memory").ExitCode * 1024L;
                }
                catch (Exception ex2)
                {
                    ModBase.Log(ex2, "命令行形式内存优化失败");
                    if (ShowHint)
                        ModMain.Hint(
                            string.Concat("获取管理员权限失败，请尝试右键 PCL，选择 ", Conversions.ToString(ModBase.vbLQ), "以管理员身份运行",
                                Conversions.ToString(ModBase.vbRQ), "！"), ModMain.HintType.Critical);
                    return;
                }
                finally
                {
                    IsMemoryOptimizing = false;
                }

                if (num < 0L) return;
            }

            var MemAfter = ModBase.GetString((long)KernelInterop.GetAvailablePhysicalMemoryBytes());
            ModBase.Log(string.Format("[Test] 内存优化完成，可用内存改变量：{0}，大致剩余内存：{1}", ModBase.GetString(num), MemAfter));
            if (num > 0L)
            {
                if (ShowHint)
                    ModMain.Hint(
                        string.Format("内存优化完成，可用内存增加了 {0}，目前剩余内存 {1}！",
                            ModBase.GetString((long)Math.Round(Math.Round(num * 0.8d))), MemAfter),
                        ModMain.HintType.Finish);
            }
            else if (ShowHint)
            {
                ModMain.Hint(string.Format("内存优化完成，已经优化到了最佳状态，目前剩余内存 {0}！", MemAfter));
            }
        }
    }

    public static void MemoryOptimizeInternal(bool ShowHint)
    {
        if (!ProcessInterop.IsAdmin())
            throw new Exception("内存优化功能需要管理员权限！" + Constants.vbCrLf +
                                "如果需要自动以管理员身份启动 PCL，可以右键 PCL，打开 属性 → 兼容性 → 以管理员身份运行此程序。");
        ModBase.Log("[Test] 获取内存优化权限");

        // 提权部分
        try
        {
            var processId = GetCurrentProcess();
            LUID luid1 = default;
            LUID luid2 = default;
            nint hToken = 0;
            if (OpenProcessToken(new HandleRef(null, processId), 32, out hToken))
            {
                string arglpSystemName = null;
                var arglpName = "SeProfileSingleProcessPrivilege";
                LookupPrivilegeValue(arglpSystemName, arglpName, out luid1);
                string arglpSystemName1 = null;
                var arglpName1 = "SeIncreaseQuotaPrivilege";
                LookupPrivilegeValue(arglpSystemName1, arglpName1, out luid2);

                var tokenPrivileges1 = new TokenPrivileges();
                tokenPrivileges1.Luid = luid1;
                tokenPrivileges1.Attributes = 2;
                var tokenPrivileges2 = new TokenPrivileges();
                tokenPrivileges2.Luid = luid2;
                tokenPrivileges2.Attributes = 2;

                AdjustTokenPrivileges(new HandleRef(null, hToken), false, tokenPrivileges1, 0, nint.Zero, nint.Zero);
                AdjustTokenPrivileges(new HandleRef(null, hToken), false, tokenPrivileges2, 0, nint.Zero, nint.Zero);

                CloseHandle(hToken);
            }
        }
        catch (Exception)
        {
            throw new Exception(string.Format("获取内存优化权限失败（错误代码：{0}）", Marshal.GetLastWin32Error()));
        }

        if (ShowHint) ModMain.Hint("正在进行内存优化……");

        // 内存优化部分
        var NowType = "None";
        try
        {
            int info;
            var scfi = default(SYSTEM_FILECACHE_INFORMATION);
            var combineInfoEx = default(MEMORY_COMBINE_INFORMATION_EX);
            GCHandle _gcHandle;

            NowType = "MemoryEmptyWorkingSets";
            info = 2;
            _gcHandle = GCHandle.Alloc(info, GCHandleType.Pinned);
            NtSetSystemInformation(80, _gcHandle.AddrOfPinnedObject(), Marshal.SizeOf(info));
            _gcHandle.Free();
            NowType = "SystemFileCacheInformation";
            scfi.MaximumWorkingSet = uint.MaxValue;
            scfi.MinimumWorkingSet = uint.MaxValue;
            _gcHandle = GCHandle.Alloc(scfi, GCHandleType.Pinned);
            NtSetSystemInformation(81, _gcHandle.AddrOfPinnedObject(), Marshal.SizeOf(scfi));
            _gcHandle.Free();
            NowType = "MemoryFlushModifiedList";
            info = 3;
            _gcHandle = GCHandle.Alloc(info, GCHandleType.Pinned);
            NtSetSystemInformation(80, _gcHandle.AddrOfPinnedObject(), Marshal.SizeOf(info));
            _gcHandle.Free();
            NowType = "MemoryPurgeStandbyList";
            info = 4;
            _gcHandle = GCHandle.Alloc(info, GCHandleType.Pinned);
            NtSetSystemInformation(80, _gcHandle.AddrOfPinnedObject(), Marshal.SizeOf(info));
            _gcHandle.Free();
            NowType = "MemoryPurgeLowPriorityStandbyList";
            info = 5;
            _gcHandle = GCHandle.Alloc(info, GCHandleType.Pinned);
            NtSetSystemInformation(80, _gcHandle.AddrOfPinnedObject(), Marshal.SizeOf(info));
            _gcHandle.Free();
            NowType = "SystemRegistryReconciliationInformation";
            NtSetSystemInformation(155, new nint(default(int)), 0);
            NowType = "SystemCombinePhysicalMemoryInformation";
            _gcHandle = GCHandle.Alloc(combineInfoEx, GCHandleType.Pinned);
            NtSetSystemInformation(130, _gcHandle.AddrOfPinnedObject(), Marshal.SizeOf(combineInfoEx));
            _gcHandle.Free();
        }
        catch (Exception)
        {
            throw new Exception(string.Format("内存优化操作 {0} 失败（错误代码：{1}）", NowType));
        }
    }

    public static string GetRandomCave()
    {
        return "为便于维护，社区版中不包含百宝箱功能……";
    }

    public static string GetRandomHint()
    {
        return "为便于维护，社区版中不包含百宝箱功能……";
    }

    public static string GetRandomPresetHint()
    {
        return "为便于维护，社区版中不包含百宝箱功能……";
    }

    private void TextDownloadUrl_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(TextDownloadName.Text) || string.IsNullOrEmpty(TextDownloadUrl.Text)) return;
            TextDownloadName.Text = ModBase.GetFileNameFromPath(WebUtility.UrlDecode(TextDownloadUrl.Text));
        }
        catch
        {
        }
    }

    private void MyTextButton_Click(object sender, EventArgs e)
    {
        var text = SystemDialogs.SelectFolder();
        if (!string.IsNullOrEmpty(text)) TextDownloadFolder.Text = text;
    }

    private void BtnDownloadOpen_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            var text = TextDownloadFolder.Text;
            Directory.CreateDirectory(text);
            Basics.OpenPath(text);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "打开下载文件夹失败");
        }
    }

    private void BtnDownloadStart_Click(object sender, MouseButtonEventArgs e)
    {
        StartCustomDownload(TextDownloadUrl.Text, TextDownloadName.Text, TextDownloadFolder.Text, TextUserAgent.Text);
        TextDownloadUrl.Text = "";
        TextDownloadUrl.Validate();
        TextDownloadUrl.ForceShowAsSuccess();
        TextDownloadName.Text = "";
        TextDownloadName.Validate();
        TextDownloadName.ForceShowAsSuccess();
        StartButtonRefresh();
    }

    private void TextDownloadUrl_ValidateChanged(object sender, RoutedEventArgs e)
    {
        StartButtonRefresh();
    }

    private void TextDownloadFolder_ValidateChanged(object sender, EventArgs e)
    {
        StartButtonRefresh();
    }

    private void TextDownloadName_ValidateChanged(object sender, EventArgs e)
    {
        StartButtonRefresh();
    }

    private void BtnClear_Click(object sender, MouseButtonEventArgs e)
    {
        RubbishClear();
    }

    private void BtnMemory_Click(object sender, MouseButtonEventArgs e)
    {
        ModBase.RunInThread(() => MemoryOptimize(true));
    }

    // 下载正版玩家皮肤
    private void BtnSkinSave_Click(object sender, MouseButtonEventArgs e)
    {
        var ID = TextSkinID.Text;
        ModMain.Hint("正在获取皮肤...");
        ModBase.RunInNewThread(() =>
        {
            try
            {
                if (ID.Length < 3)
                {
                    ModMain.Hint("这不是一个有效的 ID...");
                }
                else
                {
                    var Result = Conversions.ToString(ModProfile.McLoginMojangUuid(ID, true));
                    Result = ModMinecraft.McSkinGetAddress(Result, "Mojang");
                    Result = ModMinecraft.McSkinDownload(Result);
                    ModBase.RunInUi(() =>
                    {
                        var Path = SystemDialogs.SelectSaveFile("保存皮肤", ID + ".png", "皮肤图片文件(*.png)|*.png");
                        ModBase.CopyFile(Result, Path);
                        ModMain.Hint($"玩家 {ID} 的皮肤已保存！", ModMain.HintType.Finish);
                    });
                }
            }
            catch (Exception ex)
            {
                if (ex.ToString().Contains("429"))
                {
                    ModMain.Hint("获取皮肤太过频繁，请 5 分钟之后再试！", ModMain.HintType.Critical);
                    ModBase.Log("获取正版皮肤失败（" + ID + "）：获取皮肤太过频繁，请 5 分钟后再试！");
                }
                else
                {
                    ModBase.Log(ex, "获取正版皮肤失败（" + ID + "）");
                }
            }
        });
    }

    // 今日人品
    private void BtnLuck_Click(object sender, MouseButtonEventArgs e)
    {
        Jrrp();
    }

    public static int GenerateDailySeed()
    {
        var datePart = DateTime.Today.ToString("yyyyMMdd");

        return DJB2Hash(datePart + Identify.LauncherId);
    }

    private static int DJB2Hash(string str)
    {
        var hash = 5381L;
        var prime = 33L;
        foreach (var c in str)
        {
            long charValue = Strings.AscW(c);
            hash = (hash * prime + charValue) % 0x100000000L;
        }

        return (int)(hash & 0x7FFFFFFFL);
    }

    public static string GetRating(int luckValue)
    {
        if (luckValue == 100) return "100！100！" + Constants.vbCrLf + "隐藏主题 欧皇…… 不对，社区版应该没有这玩意……";

        return luckValue >= 95 ? "差一点就到100了呢..." :
            luckValue >= 90 ? "好评如潮！" :
            luckValue >= 60 ? "还行啦，还行啦" :
            luckValue >= 40 ? "勉强还行吧..." :
            luckValue >= 30 ? "呜..." :
            luckValue >= 10 ? "不会吧！" : "（是百分制哦）";
    }

    private void BtnCreateShortcut_Click(object sender, MouseButtonEventArgs e)
    {
        const string shortcutName = "PCL 社区版.lnk";
        const string desktopName = "桌面";
        const string startName = "开始菜单";
        var desktop = Paths.GetSpecialPath(Environment.SpecialFolder.Desktop, shortcutName);
        var start = Paths.GetSpecialPath(Environment.SpecialFolder.StartMenu, @"Programs\" + shortcutName);
        var choice =
            ModMain.MyMsgBox(
                "这个快捷方式不会自动移除，在删除/移动启动器前请手动移除快捷方式。" + Constants.vbCrLf + Constants.vbCrLf + desktopName + "位置: " +
                desktop + Constants.vbCrLf + startName + "位置: " + start, "选择快捷方式位置", "取消", desktopName, startName);
        if (choice == 1)
            return;
        var shortcutPath = choice == 2 ? desktop : start;
        var locationName = choice == 2 ? desktopName : startName;
        Files.CreateShortcut(shortcutPath, Basics.ExecutablePath);
        ModMain.Hint("已在" + locationName + "创建快捷方式", ModMain.HintType.Finish);
    }

    // 启动计数显示
    private void BtnLaunchCount_Click(object sender, MouseButtonEventArgs e)
    {
        var launchCount = Conversions.ToInteger(States.System.LaunchCount);
        ModMain.MyMsgBox($"PCL 已经为你启动了 {launchCount} 次游戏了。", "启动次数");
    }

    private async void BtnAchievementPreview_Click(object sender, MouseButtonEventArgs e)
    {
        var url = GetAchievementUrl();
        ModBase.Log("[Net] 获取网络结果" + url);
        await LoadImageAsync(url);
    }

    private async Task LoadImageAsync(string imageUrl)
    {
        var client = NetworkService.GetClient();
        try
        {
            var response = await client.GetAsync(imageUrl);
            if (response.IsSuccessStatusCode)
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.StreamSource = stream;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();

                    Dispatcher.Invoke(() =>
                    {
                        AchievementImage.Source = bitmapImage;
                        AchievementImage.Visibility = Visibility.Visible;
                    });
                }
            else if (response.StatusCode == HttpStatusCode.NotFound)
                Dispatcher.Invoke(() =>
                {
                    ModBase.Log("获取成就图片失败（404）");
                    ModMain.Hint("获取成就图片失败，请检查文字是否包含特殊字符", ModMain.HintType.Critical);
                });
            else
                Dispatcher.Invoke(() => ModBase.Log("获取成就图片失败（" + (int)response.StatusCode + "）"));
        }

        catch (Exception ex)
        {
            Dispatcher.Invoke(() => ModBase.Log(ex, "获取成就图片失败"));
        }
    }

    private async void BtnAchievementSave_Click(object sender, MouseButtonEventArgs e)
    {
        var url = GetAchievementUrl();
        await DownloadImageToLocalAsync(url);
    }

    private async Task DownloadImageToLocalAsync(string imageUrl)
    {
        var savePath = ModBase.PathTemp + @"Download\" + ModBase.GetHash(imageUrl) + ".png";
        var client = NetworkService.GetClient();
        try
        {
            // 异步发送 GET 请求
            var response = await client.GetAsync(imageUrl);

            // 如果响应状态码是成功的，则继续
            if (response.IsSuccessStatusCode)
            {
                // 异步读取响应内容为字节流
                var imageBytes = await response.Content.ReadAsByteArrayAsync();

                // 将字节写入本地文件
                File.WriteAllBytes(savePath, imageBytes);

                var path =
                    SystemDialogs.SelectSaveFile("保存皮肤", AchievementTitleTextBox.Text + ".png", "PNG 图片|*.png");
                if (string.IsNullOrEmpty(path))
                {
                    ModBase.Log("用户取消了保存操作");
                    File.Delete(savePath);
                    return;
                }

                ModBase.CopyFile(savePath, path);
                File.Delete(savePath);
                ModMain.Hint("自定义成就图片已保存！", ModMain.HintType.Finish);
            }
            // 下载成功，返回 True
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // 捕获 404 错误
                ModBase.Log("获取成就图片失败（404）");
                ModMain.Hint("获取成就图片失败，请检查文字是否包含特殊字符", ModMain.HintType.Critical);
            }
            else
            {
                // 处理其他非成功状态码
                ModBase.Log("获取成就图片失败（" + (int)response.StatusCode + "）");
            }
        }

        catch (Exception ex)
        {
            // 捕获所有其他异常（如网络连接问题）
            ModBase.Log(ex, "获取成就图片失败");
        }
    }

    private string GetAchievementUrl()
    {
        var block = AchievementBlockTextBox.Text.Trim();
        var title = AchievementTitleTextBox.Text.Replace(" ", "..");
        var str1 = AchievementString1TextBox.Text.Replace(" ", "..");
        var str2 = AchievementString2TextBox.Text.Replace(" ", "..");
        var url = $"https://minecraft-api.com/api/achivements/{block}/{title}/{str1}";
        if (!string.IsNullOrEmpty(str2)) url += $"/{str2}";
        return url;
    }

    private void BtnCrash_Click(object sender, MouseButtonEventArgs e)
    {
        if (ModMain.MyMsgBoxInput("崩溃确认", "你一定是点错了，如果没错请在下方确认", "确认", HintText: "\"sURe\".ToUpper()", IsWarn: true) ==
            "SURE") throw new Exception("手动崩溃");
    }

    private int GetHeadSize()
    {
        switch (CmbHeadSize.SelectedIndex)
        {
            case 0:
            {
                return 64;
            }
            case 1:
            {
                return 96;
            }
            case 2:
            {
                return 128;
            }

            default:
            {
                return 64;
            }
        }
    }

    private void BtnSelectSkin_Click(object sender, RoutedEventArgs e)
    {
        var filePath = SystemDialogs.SelectFile("图像文件(*.png)|*.png", "选择皮肤文件");
        if (!string.IsNullOrEmpty(filePath)) LoadAndGenerateHead(filePath);
    }

    private void LoadAndGenerateHead(string skinPath)
    {
        try
        {
            using (var stream = new FileStream(skinPath, FileMode.Open, FileAccess.Read))
            {
                CurrentSkinBitmap = new Bitmap(stream);
            }

            this.skinPath = skinPath;

            if (CurrentSkinBitmap.Width != CurrentSkinBitmap.Height)
            {
                ModMain.Hint("图片的大小不正确！请确认你选择了正确的文件！", ModMain.HintType.Critical);
                SkinPreviewBorder.Visibility = Visibility.Collapsed;
                return;
            }

            GeneratedHeadBitmap = GenerateHeadFromSkin(CurrentSkinBitmap);

            ImgFace.Source = BitmapToBitmapImage(GeneratedHeadBitmap);
            ImgHair.Source = null;

            SkinPreviewBorder.Visibility = Visibility.Visible;
            ModMain.Hint("头像生成成功！", ModMain.HintType.Finish);
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "生成头像失败");
            ModMain.Hint("生成头像失败：" + ex.Message, ModMain.HintType.Critical);
            SkinPreviewBorder.Visibility = Visibility.Collapsed;
        }
    }

    private Bitmap GenerateHeadFromSkin(Bitmap skinBitmap)
    {
        var scale = skinBitmap.Width / 64;
        HeadSize = GetHeadSize();
        var headBitmap = new Bitmap(HeadSize, HeadSize);

        using (var g = Graphics.FromImage(headBitmap))
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            DrawFaceLayer(g, skinBitmap, scale);
            if (skinBitmap.Width >= 64) DrawHairLayer(headBitmap, skinBitmap, scale);
        }

        return headBitmap;
    }

    private void DrawFaceLayer(Graphics g, Bitmap skinBitmap, int scale)
    {
        var faceRect = new Rectangle(8 * scale, 8 * scale, 8 * scale, 8 * scale);
        var faceSize = HeadSize - HeadSize / 8;
        var faceScaled = new Bitmap(faceSize, faceSize);

        using (var gFace = Graphics.FromImage(faceScaled))
        {
            gFace.InterpolationMode = InterpolationMode.NearestNeighbor;
            gFace.PixelOffsetMode = PixelOffsetMode.Half;
            gFace.DrawImage(skinBitmap, new Rectangle(0, 0, faceSize, faceSize), faceRect, GraphicsUnit.Pixel);
        }

        var offset = HeadSize / 16;
        g.DrawImage(faceScaled, offset, offset, faceSize, faceSize);
    }

    private void DrawHairLayer(Bitmap headBitmap, Bitmap skinBitmap, int scale)
    {
        var hairRect = new Rectangle(40 * scale, 8 * scale, 8 * scale, 8 * scale);
        var hairScaled = new Bitmap(HeadSize, HeadSize);

        using (var gHair = Graphics.FromImage(hairScaled))
        {
            gHair.InterpolationMode = InterpolationMode.NearestNeighbor;
            gHair.PixelOffsetMode = PixelOffsetMode.Half;
            gHair.DrawImage(skinBitmap, new Rectangle(0, 0, HeadSize, HeadSize), hairRect, GraphicsUnit.Pixel);
        }

        for (int x = 0, loopTo = HeadSize - 1; x <= loopTo; x++)
        for (int y = 0, loopTo1 = HeadSize - 1; y <= loopTo1; y++)
        {
            var pixel = hairScaled.GetPixel(x, y);
            if (pixel.A > 0) headBitmap.SetPixel(x, y, pixel);
        }
    }

    private void BtnSaveHead_Click(object sender, MouseButtonEventArgs e)
    {
        if (GeneratedHeadBitmap is null)
        {
            ModMain.Hint("请先选择皮肤！", ModMain.HintType.Critical);
            return;
        }

        var savePath = SystemDialogs.SelectSaveFile("保存头像", "Head.png");
        if (string.IsNullOrEmpty(savePath))
            return;

        GeneratedHeadBitmap.Save(savePath, ImageFormat.Png);
        ModMain.Hint("头像保存成功！", ModMain.HintType.Finish);
    }

    private void CmbHeadSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CurrentSkinBitmap is not null && skinPath is not null) LoadAndGenerateHead(skinPath);
    }

    private BitmapImage BitmapToBitmapImage(Bitmap bitmap)
    {
        using (var memoryStream = new MemoryStream())
        {
            bitmap.Save(memoryStream, ImageFormat.Png);
            memoryStream.Position = 0L;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = memoryStream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            return bitmapImage;
        }
    }

    private void TextDownloadFolder_OnValidatedTextChanged(object sender, RoutedEventArgs e)
    {
        SaveCacheDownloadFolder(sender, e);
        TextDownloadName_ValidateChanged(sender, e);
    }

    private void TextUserAgent_OnValidatedTextChanged(object sender, RoutedEventArgs e)
    {
        SaveCustomUserAgent(sender, e);
        TextDownloadFolder_ValidateChanged(sender, e);
    }

    [StructLayout(LayoutKind.Sequential)]
    private class TokenPrivileges
    {
        public int PrivilegeCount = 1;
        public LUID Luid;
        public int Attributes;
    }

    private struct LUID
    {
        public int LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_FILECACHE_INFORMATION
    {
        public nuint CurrentSize;
        public nuint PeakSize;
        public uint PageFaultCount;
        public nuint MinimumWorkingSet;
        public nuint MaximumWorkingSet;
        public nuint CurrentSizeIncludingTransitionInPages;
        public nuint PeakSizeIncludingTransitionInPages;
        public uint TransitionRePurposeCount;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_COMBINE_INFORMATION_EX
    {
        public nint Handle;
        public nuint PagesCombined;
        public uint Flags;
    }
}
