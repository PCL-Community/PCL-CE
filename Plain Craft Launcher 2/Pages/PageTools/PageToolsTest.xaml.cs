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
using PCL.Core.Utils.Exts;
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
    private bool _isRenderingAchievement;

    private static readonly Media.FontFamily AchievementFontFamily = new(
        new Uri("pack://application:,,,/"),
        "./Resources/#PCL English, Microsoft YaHei UI, Segoe UI");

    /// <summary>
    ///     在线物品图标源（本地资源取不到时按顺序依次尝试）。
    /// </summary>
    private static readonly string[] OnlineIconSources =
    {
        "https://blockrender.dev/render/item/{0}.png?size=256",
        "https://mcitemgallery.com/images-v2/1.21.1/{0}.png"
    };
    private const string BarrierIconResource = "pack://application:,,,/Resources/AchievementBarrier.png";
    private const string OnlineBarrierUrl = "https://blockrender.dev/render/item/barrier.png?size=256";//在线屏障图标地址 内置资源加载失败显示图标
    private static BitmapSource? _builtInBarrier;//内置屏障图标的缓存，避免每次渲染都重新解码。

    /// <summary>
    ///     在线图标缓存（物品 ID → 位图），避免同一物品被反复请求。
    /// </summary>
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

        _InitAchievementFontCombo();
        AchievementBlockTextBox.Validate();
        AchievementTitleTextBox.Validate();
        AchievementString1TextBox.Validate();

        _StartButtonRefresh();
    }

    /// <summary>
    ///     刷新「下载」与「成就」两组按钮的可用状态。
    /// </summary>
    private void _StartButtonRefresh()
    {
        BtnDownloadStart.IsEnabled = string.IsNullOrEmpty(TextDownloadFolder.ValidateResult) &&
                                     string.IsNullOrEmpty(TextDownloadUrl.ValidateResult) &&
                                     string.IsNullOrEmpty(TextDownloadName.ValidateResult);

        BtnDownloadOpen.IsEnabled = string.IsNullOrEmpty(TextDownloadFolder.ValidateResult);

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
            _StartButtonRefresh();
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
        _StartButtonRefresh();
    }

    private void TextDownloadUrl_ValidateChanged(object sender, RoutedEventArgs e)
    {
        _StartButtonRefresh();
    }

    private void TextDownloadFolder_ValidateChanged(object sender, EventArgs e)
    {
        _StartButtonRefresh();
    }

    private void TextDownloadName_ValidateChanged(object sender, EventArgs e)
    {
        _StartButtonRefresh();
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

    private void _AchievementInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is MyTextBox box) box.Validate();

        _StartButtonRefresh();
    }

    private void _InitAchievementFontCombo()
    {
        var items = new List<MyComboBoxItem>();
        foreach (var family in Media.Fonts.SystemFontFamilies// 并让每一项用自己的字体渲染，这样下拉列表里能直接看到字体效果
                     .Where(f => !string.IsNullOrWhiteSpace(f.Source))
                     .GroupBy(f => f.Source)
                     .Select(g => g.First())
                     .OrderBy(_GetFontDisplayName, StringComparer.CurrentCulture))
        {
            var item = new MyComboBoxItem
            {
                Content = _GetFontDisplayName(family),
                Tag = family.Source // Tag 保存真正的字体族名，渲染时用它构造 FontFamily
            };
            try
            {
                item.FontFamily = family;
            }catch{// 个别字体构造失败时保持默认字体显示，不影响选择
            }items.Add(item);
        }

        ComboAchievementFont.ItemsSource = items;
        ComboAchievementFont.SelectedIndex = 0;
    }

    /// <summary>
    ///     取字体族的本地化显示名。
    /// </summary>
    /// <param name="family">字体族。</param>
    /// <returns>当前界面语言下的字体名；没有本地化名时退回字体族名（通常为英文）。</returns>
    private static string _GetFontDisplayName(Media.FontFamily family)
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

    private async void BtnAchievementPreview_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isRenderingAchievement) return;

        LoadAchievement.Visibility = Visibility.Visible;
        _isRenderingAchievement = true;
        _StartButtonRefresh(); // 立刻禁用两个按钮，避免重复触发
        try
        {
            await Task.Yield(); // 先把加载指示绘制出来
            // 先快照全部输入：等待图标期间用户仍可编辑文本框，
            // 若渲染时再读取，会出现「旧图标配新文字」的不一致结果
            var itemId = AchievementBlockTextBox.Text.Trim();
            var rawTitle = AchievementTitleTextBox.Text;
            var rawLine1 = AchievementString1TextBox.Text;
            var rawLine2 = AchievementString2TextBox.Text;

            var (icon, notFound) = await _LoadAchievementIconAsync(itemId);
            if (notFound && !string.IsNullOrEmpty(itemId))
                HintService.Hint(Lang.Text("Tools.Test.Achievement.IconNotFound", itemId), HintType.Info);

            AchievementImage.Source = _RenderAchievementImage(icon, itemId, rawTitle, rawLine1, rawLine2);
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
            _StartButtonRefresh();
        }
    }

    private async void BtnAchievementSave_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isRenderingAchievement) return;

        LoadAchievement.Visibility = Visibility.Visible;
        _isRenderingAchievement = true;
        _StartButtonRefresh(); // 立刻禁用两个按钮，避免重复触发
        try
        {
            await Task.Yield(); // 先把加载指示绘制出来
            // 与预览一致：先快照，避免等待期间用户改动导致图文不一致
            var itemId = AchievementBlockTextBox.Text.Trim();
            var rawTitle = AchievementTitleTextBox.Text;
            var rawLine1 = AchievementString1TextBox.Text;
            var rawLine2 = AchievementString2TextBox.Text;

            var (icon, notFound) = await _LoadAchievementIconAsync(itemId);

            if (notFound && !string.IsNullOrEmpty(itemId))
                HintService.Hint(Lang.Text("Tools.Test.Achievement.IconNotFound", itemId), HintType.Info);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(
                _RenderAchievementImage(icon, itemId, rawTitle, rawLine1, rawLine2)));

            using var buffer = new MemoryStream();
            encoder.Save(buffer);
            buffer.Position = 0;

            var path = SystemDialogs.SelectSaveFile(Lang.Text("Tools.Test.Achievement.Save"),
                AchievementTitleTextBox.Text + ".png",
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
            _StartButtonRefresh();
        }
    }

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

    private Media.FontFamily ResolveAchievementFont()
    {
        var name = (ComboAchievementFont.SelectedItem as MyComboBoxItem)?.Tag as string;
        if (string.IsNullOrWhiteSpace(name))
            return AchievementFontFamily;

        try
        {
            return new Media.FontFamily(name);
        }
        catch
        {
            return AchievementFontFamily;
        }
    }

    private BitmapSource _RenderAchievementImage(BitmapSource? icon, string itemId, string rawTitle,
        string rawLine1, string rawLine2)
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

        // 每个字段单独截断，避免较长的上一行把下一行整个挤掉
        var title = _TruncateText(_StripMinecraftCodes(rawTitle));
        var line1 = _TruncateText(_StripMinecraftCodes(rawLine1));
        var line2 = _TruncateText(_StripMinecraftCodes(rawLine2));
        var description = string.IsNullOrEmpty(line2) ? line1 : line1 + "\n" + line2;

        var typeface = new Media.Typeface(ResolveAchievementFont(), FontStyles.Normal, FontWeights.Normal,
            FontStretches.Normal);

        // 文字起点：左边距 + 图标宽 + 间距
        var textX = padding + iconSize + padding;
        var maxTextWidth = contentWidth - textX - padding;

        Media.FormattedText MakeText(string text, Media.Color color) => new(
            _WrapTextByWidth(text, typeface, fontSize, maxTextWidth, superSample),
            CultureInfo.CurrentUICulture, _GetFlowDirection(text), typeface, fontSize,
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

            var iconRect = new Rect(padding + borderWidth, (totalHeight - iconSize) / 2, iconSize, iconSize);
            if (icon != null)
                dc.DrawImage(icon, iconRect);
            else
                _DrawAchievementIcon(dc, iconRect.TopLeft, iconSize, itemId);

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
    ///     必须按 Unicode 标量值（Rune）遍历而非 char —— 按 char 会从中间拆开代理项对，
    ///     导致 emoji 等补充平面字符落在换行边界时显示为乱码。
    /// </remarks>
    private static string _WrapTextByWidth(string text, Media.Typeface typeface, double fontSize, double maxWidth,
        int pixelsPerDip)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var result = new System.Text.StringBuilder(text.Length + 8);
        var current = "";

        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == '\n')
            {
                result.Append(current).Append('\n');
                current = "";
                continue;
            }

            var candidate = current + rune;
            if (current.Length > 0
                && _MeasureTextWidth(candidate, typeface, fontSize, pixelsPerDip) > maxWidth)
            {
                result.Append(current).Append('\n');
                current = rune.ToString();
            }
            else
            {
                current = candidate;
            }
        }

        result.Append(current);
        return result.ToString();
    }

    /// <summary>
    ///     按文本内容判断书写方向。
    /// </summary>
    /// <param name="text">待判断的文本。</param>
    /// <returns>含 RTL 字符时返回 RightToLeft，否则 LeftToRight。</returns>
    /// <remarks>
    ///     不能固定用 LeftToRight —— 阿拉伯文、希伯来文等从右向左书写的语言会显示反掉。
    ///     这里按内容检测（而非界面语言），因为同一界面下用户可能输入任意语言的文本。
    ///     U+0590–U+08FF 覆盖希伯来文、阿拉伯文、叙利亚文等 RTL 区段。
    /// </remarks>
    private static FlowDirection _GetFlowDirection(string text)
    {
        foreach (var rune in text.EnumerateRunes())
            if (rune.Value is >= 0x0590 and <= 0x08FF)
                return FlowDirection.RightToLeft;

        return FlowDirection.LeftToRight;
    }

    /// <summary>
    ///     测量一段文本的显示宽度。
    /// </summary>
    /// <param name="text">待测量的文本。</param>
    /// <param name="typeface">字体。</param>
    /// <param name="fontSize">字号（逻辑像素）。</param>
    /// <param name="pixelsPerDip">每个 DIP 对应的像素数。</param>
    /// <returns>文本宽度（含末尾空白）。</returns>
    private static double _MeasureTextWidth(string text, Media.Typeface typeface, double fontSize, int pixelsPerDip)
    {
        var formatted = new Media.FormattedText(
            text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, fontSize,
            Media.Brushes.Black, pixelsPerDip);

        return formatted.WidthIncludingTrailingWhitespace;
    }

    /// <summary>
    ///     文本超出长度上限时截取前若干字符并追加省略号。
    /// </summary>
    /// <param name="text">原始文本。</param>
    /// <returns>截断后的文本；未超长时原样返回。</returns>
    private static string _TruncateText(string text)
    {
        const int maxLength = 67;
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;
        return text[..maxLength] + "...";
    }

    /// <summary>
    ///     移除 Minecraft 颜色代码（形如 §c、§l）。
    /// </summary>
    /// <param name="text">原始文本。</param>
    /// <returns>清理后的文本；入参为空或不含 § 时原样返回。</returns>
    private static string _StripMinecraftCodes(string text)
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
    private static BitmapSource? _TryLoadVanillaIcon(string itemId)
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

    private async Task<(BitmapSource? Icon, bool NotFound)> _LoadAchievementIconAsync(string itemId)
    {
        var local = _TryLoadVanillaIcon(itemId);
        if (local != null) return (local, false);

        var online = await Task.Run(() => _TryLoadOnlineIcon(itemId));
        if (online != null) return (online, false);

        // 仍然取不到：换成屏障图标明确表示「这个物品没有图标」，
        // 同时把 NotFound 标记传出去让调用方提示；
        // 连屏障都加载失败时返回 null，由调用方绘制内置立方体兜底
        var barrier = _TryLoadBuiltInBarrier() ?? await Task.Run(_LoadOnlineBarrier);
        return (barrier, true);
    }

    /// <summary>
    ///     加载随程序内置的屏障图标。
    /// </summary>
    /// <returns>屏障图标；资源缺失时返回 null。</returns>
    private static BitmapSource? _TryLoadBuiltInBarrier()
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
    private static BitmapSource? _LoadOnlineBarrier()
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
    ///     从在线图标源获取物品图标（本机资源不可用时的兜底）。
    /// </summary>
    /// <param name="itemId">物品 ID。</param>
    /// <returns>图标位图；所有源都失败时返回 null。</returns>
    private static BitmapSource? _TryLoadOnlineIcon(string itemId)
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
                ModBase.Log(ex, "[Achievement] 在线图标获取失败：" + url);
            }
        }

        return null;
    }

    private static void _DrawAchievementIcon(Media.DrawingContext dc, System.Windows.Point origin, double size,
        string itemId)
    {
        var baseColor = _PickAchievementIconColor(itemId);
        var halfWidth = size / 2;
        var quarterHeight = size / 4;

        var top = new Point(origin.X + halfWidth, origin.Y);
        var rightTop = new Point(origin.X + size, origin.Y + quarterHeight);
        var rightBottom = new Point(origin.X + size, origin.Y + size - quarterHeight);
        var bottom = new Point(origin.X + halfWidth, origin.Y + size);
        var leftBottom = new Point(origin.X, origin.Y + size - quarterHeight);
        var leftTop = new Point(origin.X, origin.Y + quarterHeight);
        var center = new Point(origin.X + halfWidth, origin.Y + halfWidth);

        var edgePen = new Media.Pen(new Media.SolidColorBrush(_ShadeAchievementColor(baseColor, 0.55)), 1);

        _FillAchievementPolygon(dc, new[] { top, rightTop, center, leftTop },
            _ShadeAchievementColor(baseColor, 1.25), edgePen); // 顶面
        _FillAchievementPolygon(dc, new[] { leftTop, center, bottom, leftBottom },
            _ShadeAchievementColor(baseColor, 1.0), edgePen); // 左面
        _FillAchievementPolygon(dc, new[] { center, rightTop, rightBottom, bottom },
            _ShadeAchievementColor(baseColor, 0.75), edgePen); // 右面
    }

    private static void _FillAchievementPolygon(Media.DrawingContext dc, System.Windows.Point[] points,
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

    private static Media.Color _ShadeAchievementColor(Media.Color color, double factor)
    {
        return Media.Color.FromRgb(
            (byte)Math.Clamp(color.R * factor, 0, 255),
            (byte)Math.Clamp(color.G * factor, 0, 255),
            (byte)Math.Clamp(color.B * factor, 0, 255));
    }

    private static Media.Color _PickAchievementIconColor(string itemId)
    {
        if (string.IsNullOrEmpty(itemId))
            return AchievementIconPalette[0];

        var hash = 0;
        foreach (var c in itemId)
            hash = hash * 31 + c;
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
