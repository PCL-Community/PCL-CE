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
using PCL.Core.App;
using PCL.Core.App.Tools;
using PCL.Core.IO;
using PCL.Core.IO.Net;
using PCL.Core.UI;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;
using PCL.Core.Utils.Validate;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.Core.App.Localization;
using System.Globalization;

namespace PCL;

public partial class PageToolsTest
{
    private Bitmap currentSkinBitmap;
    private Bitmap generatedHeadBitmap;

    private int headSize = 64;
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

        TextDownloadFolder.Text = States.Tool.DownloadFolder;
        TextDownloadFolder.Validate();

        if (!string.IsNullOrEmpty(TextDownloadFolder.ValidateResult) || string.IsNullOrEmpty(TextDownloadFolder.Text))
            TextDownloadFolder.Text = ModBase.exePath + @"PCL\MyDownload\";

        TextDownloadFolder.Validate();
        TextDownloadName.Validate();
        TextUserAgent.Text = States.Tool.DownloadUserAgent;
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
                    ModMain.Hint($"{Loader.name}完成！", ModMain.HintType.Finish);
                    Console.Beep();
                    break;
                }
                case ModBase.LoadState.Failed:
                {
                    ModBase.Log(Loader.Error, $"{Loader.name}失败", ModBase.LogLevel.Msgbox);
                    Console.Beep();
                    break;
                }
                case ModBase.LoadState.Aborted:
                {
                    ModMain.Hint($"{Loader.name}已取消！");
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
                if (Folder.EndsWith(FileName)) Folder = Folder[..^FileName.Length];
            }

            Folder = Folder.Replace("/", @"\").TrimEnd(new[] { '\\' }) + @"\";
            try
            {
                Directory.CreateDirectory(Folder);
                ModBase.CheckPermissionWithException(Folder);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"访问文件夹失败（{Folder}）", ModBase.LogLevel.Hint);
                return;
            }

            ModBase.Log("[Download] 自定义下载文件名：" + FileName);
            ModBase.Log("[Download] 自定义下载文件目标：" + Folder);
            var uuid = ModBase.GetUuid();
            ModLoader.LoaderBase loaderdownload;
            if (new HttpValidator().Validate(Url).IsValid)
                loaderdownload = new LoaderDownload($"自定义下载文件：{FileName} ",
                    new List<DownloadFile> { new(new[] { Url }, Folder + FileName, null, true, UserAgent) });
            else // UNC 路径
                loaderdownload = new LoaderDownloadUnc($"自定义下载文件：{FileName} ",
                    new Tuple<string, string>(Url, Folder + FileName));
            var loaderCombo = new ModLoader.LoaderCombo<int>($"自定义下载 ({uuid}) ", new[] { loaderdownload })
                { OnStateChanged = a => DownloadState((ModLoader.LoaderCombo<int>)a) };
            loaderCombo.Start();
            ModLoader.LoaderTaskbarAdd(loaderCombo);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
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
        var currentDate = Lang.Date(DateTime.Now, "d");
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
            if (ModMain.frmToolsTest is not null && ModMain.frmToolsTest.BtnClear is not null)
                ModMain.frmToolsTest.BtnClear.IsEnabled = false;
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
                if (!ModWatcher.hasRunningMinecraft && ModLaunch.mcLaunchLoader.State != ModBase.LoadState.Loading)
                {
                    if (ModNet.HasDownloadingTask())
                    {
                        ModMain.Hint("请在所有下载任务完成后再来清理吧……");
                        return;
                    }

                    if (!ModMinecraft.mcFolderList.Any()) ModMinecraft.mcFolderListLoader.Start();
                    if (States.Hint.CleanJunkFile <= 2)
                    {
                        if (ModMain.MyMsgBox(
                                """
                                即将清理游戏日志、错误报告、缓存等文件。
                                虽然应该没人往这些地方放重要文件，但还是问一下，是否确认继续？

                                在完成清理后，PCL 将自动重启。
                                """, "清理确认", Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel")) ==
                            2) return;
                        States.Hint.CleanJunkFile += 1;
                    }

                    var num = 0;
                    var cleanMcFolderList = new List<DirectoryInfo>();
                    if (!ModMinecraft.mcFolderList.Any()) ModMinecraft.mcFolderListLoader.WaitForExit();
                    foreach (var mcFolder in ModMinecraft.mcFolderList)
                    {
                        cleanMcFolderList.Add(new DirectoryInfo(mcFolder.location));
                        var dirInfo = new DirectoryInfo(mcFolder.location + "versions");
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

                    num += ModBase.DeleteDirectory(ModBase.pathTemp, true);
                    num += ModBase.DeleteDirectory(Path.Combine(SystemPaths.DriveLetter, "ProgramData", "PCL"), true);
                    if (num != 0)
                    {
                        ModMain.MyMsgBox($"""
                                           清理了 {num} 个文件！
                                           PCL 即将自动重启……
                                           """,
                            "缓存已清理", Lang.Text("Common.Action.Confirm"), "", "", false, true, true);
                        Process.Start(new ProcessStartInfo(Basics.ExecutablePath));
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
                    if (ModMain.frmToolsTest is not null && ModMain.frmToolsTest.BtnClear is not null)
                        ModMain.frmToolsTest.BtnClear.IsEnabled = true;
                });
            }
        }, "Rubbish Clear");
    }

    [DllImport("ntdll.dll", CharSet = CharSet.Ansi)]
    private static extern uint NtSetSystemInformation(int SystemInformationClass, nint SystemInformation,
        int SystemInformationLength);
    public static bool AskTrulyWantMemoryOptimize()
    {
        var memLoad = KernelInterop.GetMemoryLoadPercent();
        if (memLoad > 90) return true; // 情况不太妙啊，先别问了

        var s = ModMain.MyMsgBox(
            "内存优化功能即将被废弃。" +
            "\n\n该功能依赖未文档化的 Windows NT 内核函数调用，可能在未来版本中不可用，且存在引发未定义行为的可能。" +
            "\n\n建议使用 Mem Reduct 替代，这是一个专业的第三方内存管理工具。" +
            "\n\n是否仍然继续使用内存优化？",
            "功能即将废弃",
            Lang.Text("Common.Action.Confirm"),
            "了解 Mem Reduct",
            Lang.Text("Common.Action.Cancel"),
            IsWarn: true,
            Button2Action: () => Basics.OpenPath("https://github.com/henrypp/memreduct")
        );
        return s == 1;
    }
    public static void MemoryOptimize(bool showHint)
    {
        MemSwapService.MemorySwap(showHint);
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
        if (AskTrulyWantMemoryOptimize())
        {
            ModBase.RunInThread(() => MemoryOptimize(true));
        }
    }

    // 下载正版玩家皮肤
    private void BtnSkinSave_Click(object sender, MouseButtonEventArgs e)
    {
        var id = TextSkinID.Text;
        ModMain.Hint("正在获取皮肤...");
        ModBase.RunInNewThread(() =>
        {
            try
            {
                if (id.Length < 3)
                {
                    ModMain.Hint("这不是一个有效的 ID...");
                }
                else
                {
                    var result = (string)ModProfile.McLoginMojangUuid(id, true);
                    result = ModMinecraft.McSkinGetAddress(result, "Mojang");
                    result = ModMinecraft.McSkinDownload(result);
                    ModBase.RunInUi(() =>
                    {
                        var path = SystemDialogs.SelectSaveFile("保存皮肤", $"{id}.png", "皮肤图片文件(*.png)|*.png");
                        ModBase.CopyFile(result, path);
                        ModMain.Hint($"玩家 {id} 的皮肤已保存！", ModMain.HintType.Finish);
                    });
                }
            }
            catch (Exception ex)
            {
                if (ex.ToString().Contains("429"))
                {
                    ModMain.Hint("获取皮肤太过频繁，请 5 分钟之后再试！", ModMain.HintType.Critical);
                    ModBase.Log($"获取正版皮肤失败（{id}）：获取皮肤太过频繁，请 5 分钟后再试！");
                }
                else
                {
                    ModBase.Log(ex, $"获取正版皮肤失败（{id}）");
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
        var datePart = DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        return DJB2Hash(datePart + Identify.LauncherId);
    }

    private static int DJB2Hash(string str)
    {
        var hash = 5381L;
        var prime = 33L;
        foreach (var c in str)
        {
            long charValue = c;
            hash = (hash * prime + charValue) % 0x100000000L;
        }

        return (int)(hash & 0x7FFFFFFFL);
    }

    public static string GetRating(int luckValue)
    {
        if (luckValue == 100)
            return """
                   100！100！
                   隐藏主题 欧皇…… 不对，社区版应该没有这玩意……
                   """;

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
                $"""
                 这个快捷方式不会自动移除，在删除/移动启动器前请手动移除快捷方式。

                 {desktopName}位置: {desktop}
                 {startName}位置: {start}
                 """, "选择快捷方式位置", Lang.Text("Common.Action.Cancel"), desktopName, startName);
        if (choice == 1)
            return;
        var shortcutPath = choice == 2 ? desktop : start;
        var locationName = choice == 2 ? desktopName : startName;
        Files.CreateShortcut(shortcutPath, Basics.ExecutablePath);
        ModMain.Hint($"已在{locationName}创建快捷方式", ModMain.HintType.Finish);
    }

    // 启动计数显示
    private void BtnLaunchCount_Click(object sender, MouseButtonEventArgs e)
    {
        ModMain.MyMsgBox($"PCL 已经为你启动了 {States.System.LaunchCount} 次游戏了。", "启动次数");
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
        var savePath = ModBase.pathTemp + @"Download\" + ModBase.GetHash(imageUrl) + ".png";
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
        if (ModMain.MyMsgBoxInput("崩溃确认", "你一定是点错了，如果没错请在下方确认", Lang.Text("Common.Action.Confirm"), HintText: "\"sURe\".ToUpper()", IsWarn: true) ==
            "SURE") throw new Exception("手动崩溃");
    }

    private int GetHeadSize() => CmbHeadSize.SelectedIndex switch
    {
        0 => 64,
        1 => 96,
        2 => 128,
        _ => 64
    };

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
                currentSkinBitmap = new Bitmap(stream);
            }

            this.skinPath = skinPath;

            if (currentSkinBitmap.Width != currentSkinBitmap.Height)
            {
                ModMain.Hint("图片的大小不正确！请确认你选择了正确的文件！", ModMain.HintType.Critical);
                SkinPreviewBorder.Visibility = Visibility.Collapsed;
                return;
            }

            generatedHeadBitmap = GenerateHeadFromSkin(currentSkinBitmap);

            ImgFace.Source = BitmapToBitmapImage(generatedHeadBitmap);
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
        headSize = GetHeadSize();
        var headBitmap = new Bitmap(headSize, headSize);

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
        var faceSize = headSize - headSize / 8;
        var faceScaled = new Bitmap(faceSize, faceSize);

        using (var gFace = Graphics.FromImage(faceScaled))
        {
            gFace.InterpolationMode = InterpolationMode.NearestNeighbor;
            gFace.PixelOffsetMode = PixelOffsetMode.Half;
            gFace.DrawImage(skinBitmap, new Rectangle(0, 0, faceSize, faceSize), faceRect, GraphicsUnit.Pixel);
        }

        var offset = headSize / 16;
        g.DrawImage(faceScaled, offset, offset, faceSize, faceSize);
    }

    private void DrawHairLayer(Bitmap headBitmap, Bitmap skinBitmap, int scale)
    {
        var hairRect = new Rectangle(40 * scale, 8 * scale, 8 * scale, 8 * scale);
        var hairScaled = new Bitmap(headSize, headSize);

        using (var gHair = Graphics.FromImage(hairScaled))
        {
            gHair.InterpolationMode = InterpolationMode.NearestNeighbor;
            gHair.PixelOffsetMode = PixelOffsetMode.Half;
            gHair.DrawImage(skinBitmap, new Rectangle(0, 0, headSize, headSize), hairRect, GraphicsUnit.Pixel);
        }

        for (int x = 0, loopTo = headSize - 1; x <= loopTo; x++)
        for (int y = 0, loopTo1 = headSize - 1; y <= loopTo1; y++)
        {
            var pixel = hairScaled.GetPixel(x, y);
            if (pixel.A > 0) headBitmap.SetPixel(x, y, pixel);
        }
    }

    private void BtnSaveHead_Click(object sender, MouseButtonEventArgs e)
    {
        if (generatedHeadBitmap is null)
        {
            ModMain.Hint("请先选择皮肤！", ModMain.HintType.Critical);
            return;
        }

        var savePath = SystemDialogs.SelectSaveFile("保存头像", "Head.png");
        if (string.IsNullOrEmpty(savePath))
            return;

        generatedHeadBitmap.Save(savePath, ImageFormat.Png);
        ModMain.Hint("头像保存成功！", ModMain.HintType.Finish);
    }

    private void CmbHeadSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (currentSkinBitmap is not null && skinPath is not null) LoadAndGenerateHead(skinPath);
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

}
