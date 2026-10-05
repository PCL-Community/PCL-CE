using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
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
using Media = System.Windows.Media;
// 本文件同时引用了 System.Drawing 与 System.Windows，两者都有 Point；
// 用别名显式指定 WPF 的版本（别名优先级高于 using 引入的类型），避免 CS0104 歧义。
using Point = System.Windows.Point;

namespace PCL;

public partial class PageToolsTest
{
    private Bitmap currentSkinBitmap;
    private Bitmap generatedHeadBitmap;

    /// <summary>
    ///     用户指定的自定义图标文件路径；为空表示按物品 ID 自动获取图标。
    /// </summary>
    private string _customIconPath = "";

    /// <summary>
    ///     是否正在生成成就图片。
    /// </summary>
    /// <remarks>
    ///     生成期间禁用「预览」「保存」，避免用户连点导致多个任务并发请求同一张图标。
    /// </remarks>
    private bool _isRenderingAchievement;

    /// <summary>
    ///     建议的文字长度：超过后会在输入框下方以蓝字提示「可能显示效果不佳」。
    /// </summary>
    private const int MaxRecommendedChars = 20;

    /// <summary>
    ///     允许输入的最大长度，与 XAML 中三个输入框的 MaxLength 保持一致。
    /// </summary>
    /// <remarks>
    ///     达到该长度后输入框无法继续输入，并在下方显示红字警告。
    /// </remarks>
    private const int MaxAllowedChars = 55;

    /// <summary>字数超建议长度的提示色（蓝）。</summary>
    private static readonly Media.SolidColorBrush TooLongHintBrush = CreateFrozenBrush(0x4C, 0x9A, 0xFF);

    /// <summary>字数达到上限的提示色（红）。</summary>
    private static readonly Media.SolidColorBrush TooLongLimitBrush = CreateFrozenBrush(0xFF, 0x6B, 0x6B);

    /// <summary>
    ///     创建一个已冻结的纯色画刷，供静态共享使用。
    /// </summary>
    /// <param name="r">红色分量。</param>
    /// <param name="g">绿色分量。</param>
    /// <param name="b">蓝色分量。</param>
    /// <returns>已冻结的画笔（可安全跨线程只读访问）。</returns>
    private static Media.SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
    {
        var brush = new Media.SolidColorBrush(Media.Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    /// <summary>
    ///     成就图片使用的字体族。
    /// </summary>
    /// <remarks>
    ///     优先使用内置的像素中文字体，再逐级回退到 PCL 界面字体与系统字体，
    ///     保证任意语言都有可用字形（原先依赖的第三方接口仅支持 ASCII）。
    /// </remarks>
    private static readonly Media.FontFamily AchievementFontFamily = new(
        new Uri("pack://application:,,,/"),
        "./Resources/#Minecraft AE Pixel, ./Resources/#PCL English, Microsoft YaHei UI, Segoe UI");

    /// <summary>
    ///     在线物品图标源（本地资源取不到时按顺序依次尝试）。
    /// </summary>
    /// <remarks>
    ///     都是按物品 ID 直接返回 PNG 的公开接口，不要求本机装过 Minecraft。
    ///     路径中的 <c>{0}</c> 为物品 ID 占位符；
    ///     mcitemgallery 的版本号取 <c>base</c> 版本（其 versions.json 标注为 1.21.1，
    ///     物品覆盖最全，路径写 /images-v2/ 才是重渲染的高清版）。
    /// </remarks>
    private static readonly string[] OnlineIconSources =
    {
        "https://blockrender.dev/render/item/{0}.png?size=256",
        "https://mcitemgallery.com/images-v2/1.21.1/{0}.png"
    };

    /// <summary>
    ///     内置的「图标缺失」占位图（MC 屏障方块）在程序集里的资源路径。
    /// </summary>
    /// <remarks>
    ///     文件放在 Resources 下，由 csproj 的 &lt;Resource Include="Resources\**" /&gt; 自动嵌入。
    /// </remarks>
    private const string BarrierIconResource = "pack://application:,,,/Resources/AchievementBarrier.png";

    /// <summary>
    ///     在线屏障图标地址（内置资源加载失败时的兜底）。
    /// </summary>
    private const string OnlineBarrierUrl = "https://blockrender.dev/render/item/barrier.png?size=256";

    /// <summary>
    ///     内置屏障图标的缓存，避免每次渲染都重新解码。
    /// </summary>
    private static BitmapSource? _builtInBarrier;

    /// <summary>
    ///     在线图标缓存（物品 ID → 位图），避免同一物品被反复请求。
    /// </summary>
    /// <remarks>
    ///     必须用并发字典：在线加载是在 <c>Task.Run</c> 的线程池里执行的，
    ///     而「预览」「保存」两��按钮此前不会在渲染期间禁用，用户连续点击就会让
    ///     两个任务同时读写这个缓存。普通 Dictionary 在并发写入时会破坏其内部的
    ///     桶数组结构，可能抛出异常，甚至让查找陷入死循环。
    /// </remarks>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BitmapSource> OnlineIconCache = new();

    private int headSize = 64;
    private string skinPath = "";

    public PageToolsTest()
    {
        InitializeComponent();
        BtnSelectSkin.Click += BtnSelectSkin_Click;
        CmbHeadSize.SelectionChanged += CmbHeadSize_SelectionChanged;
        Loaded += (_, _) => MeLoaded();
        #if DEBUG
        BtnCrash.Visibility = Visibility.Visible;
        #endif
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
        TextDownloadUrl.Validate();

        // 初始化成就卡片的字体下拉框，并先校验一遍，让按钮初始状态就是正确的
        InitAchievementFontCombo();
        AchievementBlockTextBox.Validate();
        AchievementTitleTextBox.Validate();
        AchievementString1TextBox.Validate();

        StartButtonRefresh();
    }

    private void StartButtonRefresh()
    {
        BtnDownloadStart.IsEnabled = string.IsNullOrEmpty(TextDownloadFolder.ValidateResult) &&
                                     string.IsNullOrEmpty(TextDownloadUrl.ValidateResult) &&
                                     string.IsNullOrEmpty(TextDownloadName.ValidateResult);

        BtnDownloadOpen.IsEnabled = string.IsNullOrEmpty(TextDownloadFolder.ValidateResult);

        // 正在生成时一律禁用，防止用户连点导致并发起图标请求
        BtnAchievementPreview.IsEnabled = !_isRenderingAchievement &&
                                          string.IsNullOrEmpty(AchievementBlockTextBox.ValidateResult) &&
                                          string.IsNullOrEmpty(AchievementTitleTextBox.ValidateResult) &&
                                          string.IsNullOrEmpty(AchievementString1TextBox.ValidateResult);

        BtnAchievementSave.IsEnabled = !_isRenderingAchievement &&
                                       string.IsNullOrEmpty(AchievementBlockTextBox.ValidateResult) &&
                                       string.IsNullOrEmpty(AchievementTitleTextBox.ValidateResult) &&
                                       string.IsNullOrEmpty(AchievementString1TextBox.ValidateResult);
    }

    private void SaveCacheDownloadFolder(object sender, RoutedEventArgs e)
    {
        States.Tool.DownloadFolder = TextDownloadFolder.Text;
        TextDownloadName.Validate();
    }

    private static void DownloadState(ModLoader.LoaderCombo<int> loader)
    {
        try
        {
            switch (loader.State)
            {
                case ModBase.LoadState.Finished:
                {
                    HintService.Hint(Lang.Text("Tools.Test.CustomDownload.Finished", loader.name), HintType.Success);
                    Console.Beep();
                    break;
                }
                case ModBase.LoadState.Failed:
                {
                    ModBase.Log(
                        loader.Error,
                        $"{loader.name}失败",
                        ModBase.LogLevel.Msgbox,
                        userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
                    Console.Beep();
                    break;
                }
                case ModBase.LoadState.Aborted:
                {
                    HintService.Hint(Lang.Text("Tools.Test.CustomDownload.Aborted", loader.name));
                    break;
                }
            }
        }
        catch (Exception ex)
        {
        }
    }

    public static void StartCustomDownload(string url, string fileName, string folder = null, string userAgent = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = SystemDialogs.SelectSaveFile(Lang.Text("Tools.Test.CustomDownload.SelectLocation"), fileName);
                if (!folder.Contains(@"\")) return;
                if (folder.EndsWith(fileName)) folder = folder[..^fileName.Length];
            }

            folder = folder.Replace("/", @"\").TrimEnd(new[] { '\\' }) + @"\";
            try
            {
                Directory.CreateDirectory(folder);
                ModBase.CheckPermissionWithException(folder);
            }
            catch (Exception ex)
            {
                ModBase.Log(
                    ex,
                    $"访问文件夹失败（{folder}）",
                    ModBase.LogLevel.Hint,
                    userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
                return;
            }

            ModBase.Log("[Download] 自定义下载文件名：" + fileName);
            ModBase.Log("[Download] 自定义下载文件目标：" + folder);
            var uuid = ModBase.GetUuid();
            ModLoader.LoaderBase loaderdownload;
            if (new HttpValidator().Validate(url).IsValid)
                loaderdownload = new LoaderDownload(Lang.Text("Tools.Test.CustomDownload.LoaderName", fileName),
                    new List<DownloadFile> { new(new[] { url }, folder + fileName, null, true, userAgent) });
            else // UNC 路径
                loaderdownload = new LoaderDownloadUnc(Lang.Text("Tools.Test.CustomDownload.LoaderName", fileName),
                    new Tuple<string, string>(url, folder + fileName));
            var loaderCombo = new ModLoader.LoaderCombo<int>(Lang.Text("Tools.Test.CustomDownload.LoaderTitle", uuid), new[] { loaderdownload })
                { OnStateChanged = a => DownloadState((ModLoader.LoaderCombo<int>)a) };
            loaderCombo.Start();
            ModLoader.LoaderTaskbarAdd(loaderCombo);
            ModMain.frmMain.BtnExtraDownload.ShowRefresh();
            ModMain.frmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "开始自定义下载失败",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
        }
    }

    public static void Jrrp()
    {
        var random = new Random(GenerateDailySeed());
        var luckValue = random.Next(0, 101);
        var rating = GetRating(luckValue);
        var title = Lang.Text("Tools.Test.Luck.MsgboxTitle", Lang.Date(DateTime.Now, "d"));

        if (luckValue >= 60)
            ModMain.MyMsgBox(Lang.Text("Tools.Test.Luck.MessageGood", luckValue, rating), title);
        else
            ModMain.MyMsgBox(Lang.Text("Tools.Test.Luck.MessageBad", luckValue, rating), title, isWarn: luckValue <= 30);
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
                        HintService.Hint(Lang.Text("Tools.Test.Clean.WaitForDownload"));
                        return;
                    }

                    if (!ModFolder.mcFolderList.Any()) ModFolder.mcFolderListLoader.Start();
                    if (States.Hint.CleanJunkFile <= 2)
                    {
                        if (ModMain.MyMsgBox(
                                Lang.Text("Tools.Test.Clean.ConfirmMessage"),
                                Lang.Text("Tools.Test.Clean.ConfirmTitle"),
                                Lang.Text("Common.Action.Confirm"),
                                Lang.Text("Common.Action.Cancel")
                            ) == 2) return;
                        States.Hint.CleanJunkFile += 1;
                    }

                    var num = 0;
                    var cleanMcFolderList = new List<DirectoryInfo>();
                    if (!ModFolder.mcFolderList.Any()) ModFolder.mcFolderListLoader.WaitForExit();
                    foreach (var mcFolder in ModFolder.mcFolderList)
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
                            if (dirInfo2.Name.EndsWith("-natives", StringComparison.OrdinalIgnoreCase) ||
                                dirInfo2.Name == "natives-windows-x86_64")
                                num += ModBase.DeleteDirectory(dirInfo2.FullName, true);
                    }

                    num += ModBase.DeleteDirectory(ModBase.pathTemp, true);
                    num += ModBase.DeleteDirectory(Path.Combine(SystemPaths.DriveLetter, "ProgramData", "PCL"), true);
                    if (num != 0)
                    {
                        ModMain.MyMsgBox(Lang.Text("Tools.Test.Clean.ClearedMessage", num),
                            Lang.Text("Tools.Test.Clean.Cleared"), Lang.Text("Common.Action.Confirm"), "", "", false, true, true);
                        Process.Start(new ProcessStartInfo(Basics.ExecutablePath));
                        FormMain.EndProgramForce();
                    }
                    else
                    {
                        HintService.Hint(Lang.Text("Tools.Test.Clean.NoFiles"));
                    }
                }
                else
                {
                    HintService.Hint(Lang.Text("Tools.Test.Clean.CloseGameFirst"));
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(
                    ex,
                    "清理垃圾失败",
                    ModBase.LogLevel.Hint,
                    userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
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

    public static string GetRandomCave()
    {
        return Lang.Text("Tools.Test.CeNotice");
    }

    public static string GetRandomHint()
    {
        return Lang.Text("Tools.Test.CeNotice");
    }

    public static string GetRandomPresetHint()
    {
        return Lang.Text("Tools.Test.CeNotice");
    }

    private void TextDownloadUrl_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(TextDownloadName.Text) || string.IsNullOrEmpty(TextDownloadUrl.Text)) return;
            TextDownloadName.Text = ModBase.GetFileNameFromPath(WebUtility.UrlDecode(TextDownloadUrl.Text));
            StartButtonRefresh();
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
        StartCustomDownload(TextDownloadUrl.Text, TextDownloadName.Text, TextDownloadFolder.Text);
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

    // 下载正版玩家皮肤
    private void BtnSkinSave_Click(object sender, MouseButtonEventArgs e)
    {
        var id = TextSkinID.Text;
        HintService.Hint(Lang.Text("Tools.Test.Skin.Fetching"));
        ModBase.RunInNewThread(() =>
        {
            try
            {
                if (id.Length < 3)
                {
                    HintService.Hint(Lang.Text("Tools.Test.Skin.InvalidId"));
                }
                else
                {
                    var result = (string)ProfileUi.McLoginMojangUuid(id, true);
                    result = ModSkin.McSkinGetAddress(result, "Mojang");
                    result = ModSkin.McSkinDownload(result);
                    ModBase.RunInUi(() =>
                    {
                        var path = SystemDialogs.SelectSaveFile(Lang.Text("Tools.Test.Skin.Save"), $"{id}.png", Lang.Text("Tools.Test.Skin.FileFilter"));
                        ModBase.CopyFile(result, path);
                        HintService.Hint(Lang.Text("Tools.Test.Skin.Saved", id), HintType.Success);
                    });
                }
            }
            catch (Exception ex)
            {
                if (ex.ToString().Contains("429"))
                {
                    HintService.Hint(Lang.Text("Tools.Test.Skin.TooFrequent"), HintType.Error);
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
        var key = luckValue switch
        {
            100 => "Tools.Test.Luck.Rating100",
            >= 95 => "Tools.Test.Luck.Rating95",
            >= 90 => "Tools.Test.Luck.Rating90",
            >= 60 => "Tools.Test.Luck.Rating60",
            >= 40 => "Tools.Test.Luck.Rating40",
            >= 30 => "Tools.Test.Luck.Rating30",
            >= 10 => "Tools.Test.Luck.Rating10",
            _ => "Tools.Test.Luck.Rating0"
        };

        return Lang.Text(key);
    }

    private void BtnCreateShortcut_Click(object sender, MouseButtonEventArgs e)
    {
        var shortcutName = Lang.Text("Tools.Test.Shortcut.FileName", ".lnk");
        var desktopName = Lang.Text("Tools.Test.Shortcut.Desktop");
        var startName = Lang.Text("Tools.Test.Shortcut.StartMenu");
        var desktop = Paths.GetSpecialPath(Environment.SpecialFolder.Desktop, shortcutName);
        var start = Paths.GetSpecialPath(Environment.SpecialFolder.StartMenu, @"Programs\" + shortcutName);
        var choice =
            ModMain.MyMsgBox(
                Lang.Text("Tools.Test.Shortcut.ConfirmMessage", desktopName, desktop, startName, start),
                Lang.Text("Tools.Test.Shortcut.SelectLocation"), Lang.Text("Common.Action.Cancel"), desktopName, startName);
        if (choice == 1)
            return;
        var shortcutPath = choice == 2 ? desktop : start;
        var locationName = choice == 2 ? desktopName : startName;
        Files.CreateShortcut(shortcutPath, Basics.ExecutablePath);
        HintService.Hint(Lang.Text("Tools.Test.Shortcut.Created", locationName), HintType.Success);
    }

    // 启动计数显示
    private void BtnLaunchCount_Click(object sender, MouseButtonEventArgs e)
    {
        ModMain.MyMsgBox(Lang.Text("Tools.Test.LaunchCount.Message", States.System.LaunchCount), Lang.Text("Tools.Test.LaunchCount.Title"));
    }

    /// <summary>
    ///     成就卡片输入框内容变化：先重新校验，再刷新「预览」「保存」按钮的可用状态。
    /// </summary>
    /// <param name="sender">触发事件的输入框。</param>
    /// <param name="e">文本变化事件参数。</param>
    /// <remarks>
    ///     这里必须用 TextChanged 而不是 ValidatedTextChanged —— 后者只在「校验通过」时触发，
    ///     把内容删空导致校验失败时反而不会触发，按钮就会停留在上一次的状态（仍可点击）。
    ///     同时要手动调一次 Validate()，否则 ValidateResult 还是上一次的旧值。
    /// </remarks>
    private void AchievementInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is MyTextBox box)
        {
            // MaxLength 只拦键盘逐字输入，粘贴一大段文本仍能突破上限，这里强制截断
            if (box.Text.Length > MaxAllowedChars)
            {
                var caret = box.CaretIndex;
                box.Text = box.Text[..MaxAllowedChars];
                box.CaretIndex = Math.Min(caret, MaxAllowedChars);
            }

            box.Validate();
            UpdateLengthWarning(box);
        }

        StartButtonRefresh();
    }

    /// <summary>
    ///     根据文本长度刷新输入框下方的字数警告。
    /// </summary>
    /// <param name="box">内容发生变化的输入框。</param>
    /// <remarks>
    ///     达到最大长度时显示红字（此时输入框已因 MaxLength 无法继续输入），
    ///     超过建议长度时显示蓝字，都未命中则隐藏提示行。
    ///     提示直接贴在输入框下方，而不是弹右下角横幅，免得干扰视线。
    /// </remarks>
    private void UpdateLengthWarning(MyTextBox box)
    {
        var hint = box == AchievementTitleTextBox ? HintAchievementTitle
            : box == AchievementString1TextBox ? HintAchievementString1
            : box == AchievementString2TextBox ? HintAchievementString2
            : null;
        if (hint is null) return;

        var length = box.Text.Length;
        if (length >= MaxAllowedChars)
        {
            hint.Text = Lang.Text("Tools.Test.Achievement.TooLongLimit", MaxAllowedChars);
            hint.Foreground = TooLongLimitBrush;
            hint.Visibility = Visibility.Visible;
        }
        else if (length > MaxRecommendedChars)
        {
            hint.Text = Lang.Text("Tools.Test.Achievement.TooLong", MaxRecommendedChars);
            hint.Foreground = TooLongHintBrush;
            hint.Visibility = Visibility.Visible;
        }
        else
        {
            hint.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    ///     填充成就图片的字体下拉框：首项为内置像素字体，其余为本机已安装的字体。
    /// </summary>
    /// <remarks>
    ///     改为直接罗列系统字体，而不是让用户挑选字体文件 ——
    ///     Windows 的 C:\Windows\Fonts 是虚拟文件夹，文件选择器里往往显示为空，
    ///     用户在那里根本选不中任何字体。
    /// </remarks>
    private void InitAchievementFontCombo()
    {
        var items = new List<MyComboBoxItem>
        {
            new() { Content = Lang.Text("Tools.Test.Achievement.FontDefault"), Tag = "" }
        };

        // 显示本地化字体名（「微软雅黑」而不是 Microsoft YaHei），
        // 并让每一项用自己的字体渲染，这样下拉列表里能直接看到字体效果
        foreach (var family in Media.Fonts.SystemFontFamilies
                     .Where(f => !string.IsNullOrWhiteSpace(f.Source))
                     .GroupBy(f => f.Source)
                     .Select(g => g.First())
                     .OrderBy(GetFontDisplayName, StringComparer.CurrentCulture))
        {
            var item = new MyComboBoxItem
            {
                Content = GetFontDisplayName(family),
                Tag = family.Source // Tag 保存真正的字体族名，渲染时用它构造 FontFamily
            };
            try
            {
                item.FontFamily = family;
            }
            catch
            {
                // 个别字体构造失败时保持默认字体显示，不影响选择
            }

            items.Add(item);
        }

        ComboAchievementFont.ItemsSource = items;
        ComboAchievementFont.SelectedIndex = 0;
    }

    /// <summary>
    ///     取字体族的本地化显示名。
    /// </summary>
    /// <param name="family">字体族。</param>
    /// <returns>当前界面语言下的字体名；没有本地化名时退回字体族名（通常为英文）。</returns>
    /// <remarks>
    ///     FontFamily.Source 给的是字体族名（如 Microsoft YaHei），直接显示会让用户
    ///     误以为系统里没有装中文字体，所以优先取 FamilyNames 中的本地化名称（微软雅黑）。
    /// </remarks>
    private static string GetFontDisplayName(Media.FontFamily family)
    {
        try
        {
            // 注意：FamilyNames 的键是 XmlLanguage 而不是 CultureInfo
            var names = family.FamilyNames;
            var current = System.Windows.Markup.XmlLanguage.GetLanguage(
                CultureInfo.CurrentUICulture.IetfLanguageTag);

            if (names.TryGetValue(current, out var localized) && !string.IsNullOrWhiteSpace(localized))
                return localized;

            // 当前语言没有对应条目时，退一步找一个中文名
            foreach (var pair in names)
                if (pair.Key.IetfLanguageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(pair.Value))
                    return pair.Value;
        }
        catch
        {
            // 忽略，退回 Source
        }

        return family.Source;
    }

    /// <summary>
    ///     点击「浏览」：选择一张本地图片作为成就图的图标。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">鼠标事件参数。</param>
    /// <remarks>
    ///     选定后该图片会优先于自动获取的图标使用，方便展示自定义素材。
    /// </remarks>
    private void BtnAchievementIcon_Click(object sender, MouseButtonEventArgs e)
    {
        var initialDirectory = !string.IsNullOrEmpty(_customIconPath) && File.Exists(_customIconPath)
            ? Path.GetDirectoryName(_customIconPath)
            : null;

        var path = SystemDialogs.SelectFile(
            Lang.Text("Tools.Test.Achievement.IconFilter"),
            Lang.Text("Tools.Test.Achievement.Icon"),
            initialDirectory);
        if (string.IsNullOrEmpty(path)) return;

        _customIconPath = path;
        // 按钮文字换成所选文件名，用户一眼就能看出当前用的是哪张图
        BtnAchievementIcon.Text = Path.GetFileName(path);
    }

    /// <summary>
    ///     点击「预览」：本地渲染成就图片并显示。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">鼠标事件参数。</param>
    /// <remarks>
    ///     图标可能需要联网获取，耗时不确定，所以先显示加载指示并让出一次渲染时机
    ///     （<c>await Task.Yield()</c>），否则界面会一直卡在旧画面上直到渲染完成。
    /// </remarks>
    private async void BtnAchievementPreview_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isRenderingAchievement) return;

        LoadAchievement.Visibility = Visibility.Visible;
        _isRenderingAchievement = true;
        StartButtonRefresh(); // 立刻禁用两个按钮，避免重复触发
        try
        {
            await Task.Yield(); // 先把加载指示绘制出来
            var itemId = AchievementBlockTextBox.Text.Trim();
            var (icon, notFound) = await LoadAchievementIconAsync(itemId);

            // 图标没取到时明确告知用户，而不是只默默画个屏障占位图
            if (notFound && !string.IsNullOrEmpty(itemId))
                HintService.Hint(Lang.Text("Tools.Test.Achievement.IconNotFound", itemId), HintType.Info);

            AchievementImage.Source = RenderAchievementImage(icon);
            AchievementImage.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "渲染成就图片失败");
            HintService.Hint(Lang.Text("Tools.Test.Achievement.FetchFailed"), HintType.Error);
        }
        finally
        {
            LoadAchievement.Visibility = Visibility.Collapsed;
            _isRenderingAchievement = false;
            StartButtonRefresh();
        }
    }

    /// <summary>
    ///     点击「保存」：本地渲染成就图片并另存为 PNG 文件。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">鼠标事件参数。</param>
    private async void BtnAchievementSave_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isRenderingAchievement) return;

        LoadAchievement.Visibility = Visibility.Visible;
        _isRenderingAchievement = true;
        StartButtonRefresh(); // 立刻禁用两个按钮，避免重复触发
        try
        {
            await Task.Yield(); // 先把加载指示绘制出来
            var itemId = AchievementBlockTextBox.Text.Trim();
            var (icon, notFound) = await LoadAchievementIconAsync(itemId);

            if (notFound && !string.IsNullOrEmpty(itemId))
                HintService.Hint(Lang.Text("Tools.Test.Achievement.IconNotFound", itemId), HintType.Info);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(RenderAchievementImage(icon)));

            using var buffer = new MemoryStream();
            encoder.Save(buffer);
            buffer.Position = 0;

            var path = SystemDialogs.SelectSaveFile(Lang.Text("Tools.Test.Achievement.Save"),
                SanitizeFileName(AchievementTitleTextBox.Text) + ".png",
                Lang.Text("Tools.Test.Achievement.FileFilter"));
            if (string.IsNullOrEmpty(path))
            {
                ModBase.Log("用户取消了保存操作");
                return;
            }

            using (var file = File.Create(path))
            {
                buffer.CopyTo(file);
            }

            HintService.Hint(Lang.Text("Tools.Test.Achievement.Saved"), HintType.Success);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "保存成就图片失败");
            HintService.Hint(Lang.Text("Tools.Test.Achievement.FetchFailed"), HintType.Error);
        }
        finally
        {
            LoadAchievement.Visibility = Visibility.Collapsed;
            _isRenderingAchievement = false;
            StartButtonRefresh();
        }
    }

    /// <summary>
    ///     把任意文本清理成合法的 Windows 文件名（不含扩展名）。
    /// </summary>
    /// <param name="text">原始文本，通常是成就标题。</param>
    /// <returns>可安全用作文件名的字符串；清理后为空时返回「achievement」。</returns>
    /// <remarks>
    ///     Windows 禁止文件名含有 &lt; &gt; : " / \ | ? * 以及控制字符，
    ///     也不允许以空格或点结尾、还禁用 CON / PRN / AUX 等设备名。
    ///     这里只清理**默认文件名**，图片里渲染的标题仍保持原样。
    /// </remarks>
    private static string SanitizeFileName(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "achievement";

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(text.Length);
        foreach (var ch in text)
            builder.Append(Array.IndexOf(invalid, ch) >= 0 || char.IsControl(ch) ? '_' : ch);

        var name = builder.ToString().TrimEnd(' ', '.');
        if (string.IsNullOrEmpty(name)) return "achievement";

        // 设备名即使带扩展名也无法创建（CON.txt 同样会失败）
        var stem = Path.GetFileNameWithoutExtension(name);
        if (IsReservedDeviceName(stem))
            name = "_" + name;

        return name;
    }

    /// <summary>
    ///     判断某个名称是否是 Windows 保留的设备名。
    /// </summary>
    /// <param name="name">不含扩展名的名称。</param>
    /// <returns>是保留设备名时返回 true。</returns>
    private static bool IsReservedDeviceName(string name)
    {
        var stem = name.TrimEnd(' ');
        if (stem.Length == 0) return false;

        var dot = stem.IndexOf('.');
        if (dot >= 0) stem = stem[..dot];

        return stem.Equals("CON", StringComparison.OrdinalIgnoreCase)
               || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
               || stem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
               || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
               || (stem.Length == 4
                   && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                       || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                   && char.IsDigit(stem[3])
                   && stem[3] != '0');
    }

    /// <summary>
    ///     物品图标的候选配色（取自 MC 原版常见材质的主色调）。
    /// </summary>
    private static readonly Media.Color[] AchievementIconPalette =
    {
        Media.Color.FromRgb(0x8B, 0x8B, 0x8B), // 石头灰
        Media.Color.FromRgb(0x8B, 0x5A, 0x2B), // 木头棕
        Media.Color.FromRgb(0x4C, 0x8B, 0x3C), // 草绿
        Media.Color.FromRgb(0x4A, 0x6B, 0x9C), // 水蓝
        Media.Color.FromRgb(0xA0, 0x3C, 0x3C), // 红石红
        Media.Color.FromRgb(0xC8, 0xA8, 0x3C), // 金
        Media.Color.FromRgb(0x6B, 0x4A, 0x8B), // 紫水晶
        Media.Color.FromRgb(0x3C, 0x3C, 0x3C), // 煤炭黑
        Media.Color.FromRgb(0xDC, 0xDC, 0xDC), // 铁白
        Media.Color.FromRgb(0x8B, 0x3C, 0x6B), // 品红
        Media.Color.FromRgb(0x2E, 0x7A, 0x7A), // 青绿
        Media.Color.FromRgb(0x9C, 0x6B, 0x3C) // 铜
    };

    /// <summary>
    ///     解析当前应当使用的字体族。
    /// </summary>
    /// <returns>下拉框选中的系统字体；选中内置项时返回内置像素字体。</returns>
    /// <remarks>
    ///     下拉框首项（Tag 为空字符串）代表内置像素字体，其余项的 Tag 是系统字体族名，
    ///     可以直接交给 <see cref="Media.FontFamily" /> 构造。
    /// </remarks>
    private Media.FontFamily ResolveAchievementFont()
    {
        var name = (ComboAchievementFont.SelectedItem as MyComboBoxItem)?.Tag as string;
        if (string.IsNullOrWhiteSpace(name))
            return AchievementFontFamily; // 首项 = 内置像素字体

        try
        {
            return new Media.FontFamily(name);
        }
        catch
        {
            // 字体名异常时静默回退到内置字体
            return AchievementFontFamily;
        }
    }

    /// <summary>
    ///     本地渲染成就图片。
    /// </summary>
    /// <returns>渲染好的位图。宽度固定（内容 320 + 边框），高度随内容自适应。</returns>
    /// <remarks>
    ///     原实现调用第三方接口 minecraft-api.com 生成图片，而该接口不支持非 ASCII 字符，
    ///     输入中文会直接返回 404（见 #1566）；改为完全本地绘制后任意语言均可渲染，
    ///     同时不再依赖外部服务。
    ///     <para>
    ///         版式参考 MIT 许可的 Python 库 timetetng/minecraft_achievement_enerator：
    ///         · 描述按像素宽度自动换行（中日韩逐字断行、西文按词断行），卡片高度随之自适应；
    ///         · 边框由深、浅两层圆角矩形叠出，内容底再叠一层半透明深色；
    ///         · 文字带 1px 右下偏移的阴影，更贴近游戏内观感。
    ///     </para>
    ///     左侧物品图标以等轴立方体代替，配色由物品 ID 决定，因此无需附带任何贴图资源。
    ///     所有几何量按逻辑像素计算，最终以 3 倍分辨率栅格化，缩回后在高分屏上依然锐利。
    /// </remarks>
    private BitmapSource RenderAchievementImage(BitmapSource? icon)
    {
        const double contentWidth = 320;
        const double minContentHeight = 64;
        const double borderWidth = 2;
        const double cornerRadius = 5;
        const double padding = 12;
        const double iconSize = 32;
        const double textSpacing = 4;
        const double titleYAdjust = -3; // 修正像素字体的基线偏移
        const double fontSize = 16;
        const int superSample = 3;

        var backgroundColor = Media.Color.FromArgb(220, 42, 42, 42);
        var borderDarkColor = Media.Color.FromArgb(255, 25, 25, 25);
        var borderLightColor = Media.Color.FromArgb(255, 85, 85, 85);
        var shadowColor = Media.Color.FromArgb(255, 10, 10, 10);
        var titleColor = Media.Color.FromArgb(255, 0xFF, 0xFF, 0x55); // MC 成就名标准黄
        var descColor = Media.Color.FromArgb(255, 255, 255, 255);

        var title = StripMinecraftCodes(AchievementTitleTextBox.Text);
        var description = StripMinecraftCodes(
            string.IsNullOrEmpty(AchievementString2TextBox.Text)
                ? AchievementString1TextBox.Text
                : AchievementString1TextBox.Text + "\n" + AchievementString2TextBox.Text);

        var typeface = new Media.Typeface(ResolveAchievementFont(), FontStyles.Normal, FontWeights.Normal,
            FontStretches.Normal);

        // 文字起点：左边距 + 图标宽 + 间距
        var textX = padding + iconSize + padding;
        var maxTextWidth = contentWidth - textX - padding;

        // 先按像素宽度手动断行，再交给 FormattedText 绘制。
        // 不能只靠 MaxTextWidth：WPF 会把 "1234567890..." 这类连续 ASCII 串
        // 当作一个不可断开的单词，放不下时直接截断并加省略号，而不是换行。
        Media.FormattedText MakeText(string text, Media.Color color) => new(
            WrapTextByWidth(text, typeface, fontSize, maxTextWidth, superSample),
            CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, fontSize,
            new Media.SolidColorBrush(color), superSample)
        {
            MaxTextWidth = maxTextWidth,
            Trimming = TextTrimming.None // 已经手动断好行，不需要再省略
        };

        var titleText = MakeText(title, titleColor);
        var titleShadow = MakeText(title, shadowColor);
        Media.FormattedText? descText = null;
        Media.FormattedText? descShadow = null;
        if (!string.IsNullOrEmpty(description))
        {
            descText = MakeText(description, descColor);
            descShadow = MakeText(description, shadowColor);
        }

        // 卡片高度：最小高度与「文字总高 + 上下内边距」取较大者
        var titleHeight = titleText.Height;
        var descHeight = descText?.Height ?? 0;
        var totalTextHeight = titleHeight + (descText != null ? textSpacing + descHeight : 0);
        var contentHeight = Math.Max(minContentHeight, totalTextHeight + padding * 2);

        var totalWidth = contentWidth + borderWidth * 2;
        var totalHeight = contentHeight + borderWidth * 2;

        var visual = new Media.DrawingVisual();
        // 物品贴图是 16×16 的像素图，放大时必须用最近邻采样，否则会糊成一团
        Media.RenderOptions.SetBitmapScalingMode(visual, Media.BitmapScalingMode.NearestNeighbor);

        using (Media.DrawingContext dc = visual.RenderOpen())
        {
            // ---- 1. 三层圆角边框：深色外框 → 浅色次层 → 半透明内容底 ----
            var outerRadius = cornerRadius + borderWidth;
            dc.DrawRoundedRectangle(new Media.SolidColorBrush(borderDarkColor), null,
                new Rect(0, 0, totalWidth, totalHeight), outerRadius, outerRadius);
            dc.DrawRoundedRectangle(new Media.SolidColorBrush(borderLightColor), null,
                new Rect(1, 1, totalWidth - 2, totalHeight - 2), outerRadius - 1, outerRadius - 1);
            dc.DrawRoundedRectangle(new Media.SolidColorBrush(backgroundColor), null,
                new Rect(borderWidth, borderWidth, contentWidth, contentHeight), cornerRadius, cornerRadius);

            // ---- 2. 左侧物品图标（垂直居中）----
            // 优先取当前实例资源包里的真实物品贴图，取不到才退回内置的等轴立方体占位图
            var itemId = AchievementBlockTextBox.Text.Trim();
            var iconRect = new Rect(padding + borderWidth, (totalHeight - iconSize) / 2, iconSize, iconSize);
            // 图标由调用方预先解析好传进来 —— 解析可能涉及网络请求，
            // 不能在绘制过程里同步等待，否则会卡死界面
            if (icon != null)
                dc.DrawImage(icon, iconRect);
            else
                DrawAchievementIcon(dc, iconRect.TopLeft, iconSize, itemId);

            // ---- 3. 文字（整体垂直居中，带 1px 右下偏移的阴影）----
            var textStartY = (totalHeight - totalTextHeight) / 2;
            dc.DrawText(titleShadow, new Point(textX + borderWidth + 1, textStartY + titleYAdjust + 1));
            dc.DrawText(titleText, new Point(textX + borderWidth, textStartY + titleYAdjust));
            if (descText != null && descShadow != null)
            {
                var descY = textStartY + titleHeight + textSpacing;
                dc.DrawText(descShadow, new Point(textX + borderWidth + 1, descY + 1));
                dc.DrawText(descText, new Point(textX + borderWidth, descY));
            }
        }

        var target = new RenderTargetBitmap(
            (int)Math.Ceiling(totalWidth * superSample), (int)Math.Ceiling(totalHeight * superSample),
            96 * superSample, 96 * superSample, Media.PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }

    /// <summary>
    ///     按像素宽度把文本手动断行（逐字符累加测量）。
    /// </summary>
    /// <param name="text">原始文本，可含 \n 手动换行。</param>
    /// <param name="typeface">字体。</param>
    /// <param name="fontSize">字号（逻辑像素）。</param>
    /// <param name="maxWidth">单行允许的最大宽度。</param>
    /// <param name="pixelsPerDip">每个 DIP 对应的像素数，与栅格化倍率保持一致。</param>
    /// <returns>在超宽处插入换行符后的文本。</returns>
    /// <remarks>
    ///     WPF 自带的换行对连续 ASCII 串不生效 —— 像 "12345678901234567890" 或
    ///     超长英文单词，会被当成一个不可断开的单词，超宽时直接截断并加上省略号。
    ///     所以这里按参考实现的思路自己断行：逐字符累加测宽，超了就换行。
    ///     文本最多几十个字符，逐字符测量的开销可以忽略。
    /// </remarks>
    private static string WrapTextByWidth(string text, Media.Typeface typeface, double fontSize, double maxWidth,
        int pixelsPerDip)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var result = new System.Text.StringBuilder(text.Length + 8);
        var line = new System.Text.StringBuilder(text.Length);

        foreach (var ch in text)
        {
            if (ch == '\n')
            {
                result.Append(line).Append('\n');
                line.Clear();
                continue;
            }

            line.Append(ch);

            // 当前行超宽且不止一个字符时，把最后一个字符挪到下一行
            if (line.Length > 1 && MeasureTextWidth(line.ToString(), typeface, fontSize, pixelsPerDip) > maxWidth)
            {
                line.Length -= 1;
                result.Append(line).Append('\n');
                line.Clear();
                line.Append(ch);
            }
        }

        result.Append(line);
        return result.ToString();
    }

    /// <summary>
    ///     测量一段文本的显示宽度。
    /// </summary>
    /// <param name="text">待测量的文本。</param>
    /// <param name="typeface">字体。</param>
    /// <param name="fontSize">字号（逻辑像素）。</param>
    /// <param name="pixelsPerDip">每个 DIP 对应的像素数。</param>
    /// <returns>文本宽度（含末尾空白）。</returns>
    private static double MeasureTextWidth(string text, Media.Typeface typeface, double fontSize, int pixelsPerDip)
    {
        var formatted = new Media.FormattedText(
            text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, fontSize,
            Media.Brushes.Black, pixelsPerDip);

        return formatted.WidthIncludingTrailingWhitespace;
    }

    /// <summary>
    ///     移除 Minecraft 颜色代码（形如 §c、§l）。
    /// </summary>
    /// <param name="text">原始文本。</param>
    /// <returns>清理后的文本；入参为空或不含 § 时原样返回。</returns>
    private static string StripMinecraftCodes(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains('§'))
            return text;

        var builder = new System.Text.StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '§')
            {
                i++; // 连同后一个字符一起跳过
                continue;
            }

            builder.Append(text[i]);
        }

        return builder.ToString();
    }

    /// <summary>
    ///     尝试从当前 Minecraft 实例的资源中取出物品贴图。
    /// </summary>
    /// <param name="itemId">物品 ID（如 stone、diamond_sword、oak_log）。</param>
    /// <returns>物品贴图；未选择实例、资源缺失或数据异常时返回 null。</returns>
    /// <remarks>
    ///     MC 自 1.6 起把资源改为散列存储，取一张贴图要两步：
    ///     ① 在 assets/indexes/&lt;版本&gt;.json 的 objects 里按
    ///     "minecraft/textures/item/xxx.png" 查出 hash；
    ///     ② 读 assets/objects/&lt;hash 前两位&gt;/&lt;hash&gt; 这个文件。
    ///     方块贴图放在 textures/block/ 下，所以 item 与 block 两个目录都要试 ——
    ///     玩家习惯填 "stone" 这种方块 ID，只试 item 会一个都找不到。
    /// </remarks>
    private static BitmapSource? TryLoadVanillaIcon(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId) || string.IsNullOrEmpty(ModFolder.mcFolderSelected))
            return null;

        try
        {
            var instance = ModInstanceList.McMcInstanceSelected;
            if (instance is null) return null;

            var objects = ModAssets.McAssetsGetIndex(instance)?["objects"];
            if (objects is null) return null;

            foreach (var folder in new[] { "item", "block" })
            {
                var hash = objects[$"minecraft/textures/{folder}/{itemId}.png"]?["hash"]?.ToString();
                if (string.IsNullOrEmpty(hash)) continue;

                var file = Path.Combine(ModFolder.mcFolderSelected, "assets", "objects",
                    ModAssets.McAssetsHashPrefix(hash), hash);
                if (!File.Exists(file)) continue;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(file);
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // 一次性读入内存，避免文件句柄被占用
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
        }
        catch
        {
            // 任何异常都静默退回内置图标，不能因为找不到贴图就让整张图生成失败
        }

        return null;
    }

    /// <summary>
    ///     解析成就图要用的图标：自定义图片 → 实例资源贴图 → 在线接口 → null。
    /// </summary>
    /// <param name="itemId">物品 ID。</param>
    /// <returns>
    ///     图标位图，以及「是否属于自动获取失败」的标记 ——
    ///     失败时图标是屏障占位图，调用方据此给出文字提示。
    /// </returns>
    /// <remarks>
    ///     本地部分（自定义图片、实例资源）读取很快，直接在当前线程完成；
    ///     在线请求可能耗时数秒，放到线程池执行 ——
    ///     之前是在绘制过程里同步等待，两个源都超时的话会把界面卡住十秒。
    /// </remarks>
    private async Task<(BitmapSource? Icon, bool NotFound)> LoadAchievementIconAsync(string itemId)
    {
        var local = TryLoadCustomIcon() ?? TryLoadVanillaIcon(itemId);
        if (local != null) return (local, false);

        var online = await Task.Run(() => TryLoadOnlineIcon(itemId));
        if (online != null) return (online, false);

        // 仍然取不到：换成屏障图标明确表示「这个物品没有图标」，
        // 同时把 NotFound 标记传出去让调用方提示；
        // 连屏障都加载失败时返回 null，由调用方绘制内置立方体兜底
        var barrier = TryLoadBuiltInBarrier() ?? await Task.Run(LoadOnlineBarrier);
        return (barrier, true);
    }

    /// <summary>
    ///     加载随程序内置的屏障图标。
    /// </summary>
    /// <returns>屏障图标；资源缺失时返回 null。</returns>
    private static BitmapSource? TryLoadBuiltInBarrier()
    {
        if (_builtInBarrier != null) return _builtInBarrier;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(BarrierIconResource);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            _builtInBarrier = bitmap;
            return bitmap;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Achievement] 内置屏障图标加载失败");
            return null;
        }
    }

    /// <summary>
    ///     从在线源加载屏障图标。
    /// </summary>
    /// <returns>屏障图标；请求失败时返回 null。</returns>
    private static BitmapSource? LoadOnlineBarrier()
    {
        try
        {
            var bytes = NetworkService.GetClient()
                .GetByteArrayAsync(OnlineBarrierUrl)
                .WaitAsync(TimeSpan.FromSeconds(5))
                .GetAwaiter().GetResult();

            if (bytes is null || bytes.Length == 0) return null;

            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Achievement] 在线屏障图标获取失败：" + OnlineBarrierUrl);
            return null;
        }
    }

    /// <summary>
    ///     读取用户在「图标」输入框里指定的本地图片。
    /// </summary>
    /// <returns>图片位图；未指定、文件不存在或解码失败时返回 null。</returns>
    /// <remarks>
    ///     指定图片时会优先于一切自动获取的图标，方便使用自定义素材。
    /// </remarks>
    private BitmapSource? TryLoadCustomIcon()
    {
        var path = _customIconPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            // 图片损坏或格式不支持时静默回退到自动获取的图标
            return null;
        }
    }

    /// <summary>
    ///     从在线图标源获取物品图标（本机资源不可用时的兜底）。
    /// </summary>
    /// <param name="itemId">物品 ID。</param>
    /// <returns>图标位图；所有源都失败时返回 null。</returns>
    /// <remarks>
    ///     按 <see cref="OnlineIconSources" /> 的顺序逐个尝试，任一成功即返回。
    ///     网络调用是同步阻塞的，因此每个源限制 5 秒超时；
    ///     成功结果会写入内存缓存，同一个物品之后不再发起请求。
    /// </remarks>
    private static BitmapSource? TryLoadOnlineIcon(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        if (OnlineIconCache.TryGetValue(itemId, out var cached)) return cached;

        foreach (var template in OnlineIconSources)
        {
            var url = string.Format(template, Uri.EscapeDataString(itemId));
            try
            {
                var bytes = NetworkService.GetClient()
                    .GetByteArrayAsync(url)
                    .WaitAsync(TimeSpan.FromSeconds(5))
                    .GetAwaiter().GetResult();

                if (bytes is null || bytes.Length == 0) continue;

                using var stream = new MemoryStream(bytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();

                OnlineIconCache[itemId] = bitmap;
                return bitmap;
            }
            catch (Exception ex)
            {
                // 该源不可用（网络异常 / 超时 / 返回的不是图片），记日志便于排查，再换下一个源
                ModBase.Log(ex, "[Achievement] 在线图标获取失败：" + url);
            }
        }

        return null;
    }

    /// <summary>
    ///     绘制成就图左侧的物品图标（等轴立方体）。
    /// </summary>
    /// <param name="dc">绘制上下文。</param>
    /// <param name="origin">图标包围盒左上角坐标。</param>
    /// <param name="size">包围盒边长（像素）。</param>
    /// <param name="itemId">物品 ID，用于决定配色。</param>
    /// <remarks>
    ///     顶面最亮、左面居中、右面最暗，并统一描一圈深色边，
    ///     在只有纯色块的前提下也能看出方块的立体感。
    ///     参数类型写全限定名，因为本文件同时引用了 System.Drawing，
    ///     其中的 Point 会与 System.Windows.Point 冲突。
    /// </remarks>
    private static void DrawAchievementIcon(Media.DrawingContext dc, System.Windows.Point origin, double size,
        string itemId)
    {
        var baseColor = PickAchievementIconColor(itemId);
        var halfWidth = size / 2;
        var quarterHeight = size / 4;

        var top = new Point(origin.X + halfWidth, origin.Y);
        var rightTop = new Point(origin.X + size, origin.Y + quarterHeight);
        var rightBottom = new Point(origin.X + size, origin.Y + size - quarterHeight);
        var bottom = new Point(origin.X + halfWidth, origin.Y + size);
        var leftBottom = new Point(origin.X, origin.Y + size - quarterHeight);
        var leftTop = new Point(origin.X, origin.Y + quarterHeight);
        var center = new Point(origin.X + halfWidth, origin.Y + halfWidth);

        var edgePen = new Media.Pen(new Media.SolidColorBrush(ShadeAchievementColor(baseColor, 0.55)), 1);

        FillAchievementPolygon(dc, new[] { top, rightTop, center, leftTop },
            ShadeAchievementColor(baseColor, 1.25), edgePen); // 顶面
        FillAchievementPolygon(dc, new[] { leftTop, center, bottom, leftBottom },
            ShadeAchievementColor(baseColor, 1.0), edgePen); // 左面
        FillAchievementPolygon(dc, new[] { center, rightTop, rightBottom, bottom },
            ShadeAchievementColor(baseColor, 0.75), edgePen); // 右面
    }

    /// <summary>
    ///     用指定颜色填充一个多边形。
    /// </summary>
    /// <param name="dc">绘制上下文。</param>
    /// <param name="points">多边形顶点（按顺序给出）。</param>
    /// <param name="color">填充色。</param>
    /// <param name="pen">描边画笔，可为 null。</param>
    private static void FillAchievementPolygon(Media.DrawingContext dc, System.Windows.Point[] points,
        Media.Color color, Media.Pen pen)
    {
        var geometry = new Media.StreamGeometry();
        using (Media.StreamGeometryContext ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], true, true);
            for (var i = 1; i < points.Length; i++)
                ctx.LineTo(points[i], true, false);
        }

        geometry.Freeze();
        dc.DrawGeometry(new Media.SolidColorBrush(color), pen, geometry);
    }

    /// <summary>
    ///     按固定倍率调整颜色的明度。
    /// </summary>
    /// <param name="color">基准颜色。</param>
    /// <param name="factor">明度倍率，大于 1 变亮，小于 1 变暗。</param>
    /// <returns>调整后的颜色。</returns>
    private static Media.Color ShadeAchievementColor(Media.Color color, double factor)
    {
        return Media.Color.FromRgb(
            (byte)Math.Clamp(color.R * factor, 0, 255),
            (byte)Math.Clamp(color.G * factor, 0, 255),
            (byte)Math.Clamp(color.B * factor, 0, 255));
    }

    /// <summary>
    ///     根据物品 ID 稳定地挑一个图标配色。
    /// </summary>
    /// <param name="itemId">物品 ID（如 stone、diamond_sword）。</param>
    /// <returns>调色板中的某个颜色；同一个 ID 永远得到同一个颜色。</returns>
    private static Media.Color PickAchievementIconColor(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return AchievementIconPalette[0];

        var hash = 0;
        foreach (var c in itemId)
            hash = hash * 31 + c;
        // 用位与取绝对值：hash 溢出为 int.MinValue 时 Math.Abs 会直接抛 OverflowException
        return AchievementIconPalette[(hash & 0x7FFFFFFF) % AchievementIconPalette.Length];
    }

    private void BtnCrash_Click(object sender, MouseButtonEventArgs e)
    {
        throw new Exception(Lang.Text("Tools.Test.Crash.ManualCrash"));
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
        var filePath = SystemDialogs.SelectFile(Lang.Text("Tools.Test.Avatar.FileFilter"),
            Lang.Text("Tools.Test.Avatar.SelectSkinFile"));
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
                HintService.Hint(Lang.Text("Tools.Test.Avatar.InvalidSize"), HintType.Error);
                SkinPreviewBorder.Visibility = Visibility.Collapsed;
                return;
            }

            generatedHeadBitmap = GenerateHeadFromSkin(currentSkinBitmap);

            ImgFace.Source = BitmapToBitmapImage(generatedHeadBitmap);
            ImgHair.Source = null;

            SkinPreviewBorder.Visibility = Visibility.Visible;
            HintService.Hint(Lang.Text("Tools.Test.Avatar.Generated"), HintType.Success);
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "生成头像失败");
            HintService.Hint(Lang.Text("Tools.Test.Avatar.GenerateFailed", ex.Message), HintType.Error);
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
            HintService.Hint(Lang.Text("Tools.Test.Avatar.SelectFirst"), HintType.Error);
            return;
        }

        var savePath = SystemDialogs.SelectSaveFile(Lang.Text("Tools.Test.Avatar.Save"), "Head.png");

        if (string.IsNullOrEmpty(savePath))
            return;

        generatedHeadBitmap.Save(savePath, ImageFormat.Png);
        HintService.Hint(Lang.Text("Tools.Test.Avatar.Saved"), HintType.Success);
    }

    private void CmbHeadSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // skinPath 的初始值是空字符串而不是 null，用 "is not null" 判断等于没判断
        if (currentSkinBitmap is not null && !string.IsNullOrEmpty(skinPath)) LoadAndGenerateHead(skinPath);
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
}
