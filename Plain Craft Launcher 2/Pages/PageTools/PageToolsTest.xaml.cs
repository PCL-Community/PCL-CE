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
using PCL.Core.UI.Dialogs;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;
using PCL.Core.Utils.Validate;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using System.Globalization;
using Media = System.Windows.Media;
using Point = System.Windows.Point;

namespace PCL;

public partial class PageToolsTest : IPageView
{
    private readonly PageToolsTestViewModel _vm = new();
    private bool _refreshed;
    private Bitmap currentSkinBitmap;
    private Bitmap generatedHeadBitmap;
    private bool _isRenderingAchievement;
    private bool _achievementFontInitialized; 

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

    private const string OnlineBarrierUrl =
        "https://blockrender.dev/render/item/barrier.png?size=256"; //在线屏障图标地址 内置资源加载失败显示图标

    private static BitmapSource? _builtInBarrier; //内置屏障图标的缓存，避免每次渲染都重新解码。
    private static bool _builtInBarrierLoadFailed; //内置屏障图标已确认加载失败，不再重复尝试。

    /// <summary>
    ///     在线图标缓存（物品 ID → 位图），避免同一物品被反复请求。
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BitmapSource>
        OnlineIconCache = new();

    private int headSize = 64;
    private string skinPath = "";

    public PageToolsTest()
    {
        InitializeComponent();
        DataContext = _vm;
        CmbHeadSize.SelectionChanged += CmbHeadSize_SelectionChanged;
        // 成就卡片默认是折叠的，字体列表等它展开再枚举：一次要遍历本机几百个字体，进页面就做会拖慢导航
        ComboAchievementFont.IsVisibleChanged += (_, _) =>
        {
            if (ComboAchievementFont.IsVisible) _ = _InitAchievementFontComboAsync();
        };
        Loaded += (_, _) =>
        {
            RefreshOnNavigate();
            // 卡片若本来就是展开状态，可见性不会再变化，这里补一次初始化
            if (ComboAchievementFont.IsVisible) _ = _InitAchievementFontComboAsync();
        };
        Unloaded += (_, _) => _refreshed = false;
    }

    public PageToolsTestViewModel ViewModel => _vm;

    public void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    public void OnNavigatedFrom()
    {
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        MeLoaded();
    }

    private void MeLoaded()
    {
        _vm.DownloadStartEnabled = false;

        TextDownloadFolder.Text = States.Tool.DownloadFolder;
        TextDownloadFolder.Validate();

        if (!string.IsNullOrEmpty(TextDownloadFolder.ValidateResult) || string.IsNullOrEmpty(TextDownloadFolder.Text))
            TextDownloadFolder.Text = Common.ExePath + @"PCL\MyDownload\";

        TextDownloadFolder.Validate();
        TextDownloadName.Validate();
        TextDownloadUrl.Validate();
        StartButtonRefresh();
    }

    private void StartButtonRefresh()
    {
        _vm.RefreshDownloadButtons(
            string.IsNullOrEmpty(TextDownloadFolder.ValidateResult),
            string.IsNullOrEmpty(TextDownloadUrl.ValidateResult),
            string.IsNullOrEmpty(TextDownloadName.ValidateResult));

        _vm.RefreshAchievementButtons(
            string.IsNullOrEmpty(AchievementBlockTextBox.ValidateResult),
            string.IsNullOrEmpty(AchievementTitleTextBox.ValidateResult),
            string.IsNullOrEmpty(AchievementString1TextBox.ValidateResult));
    }

    private void SaveCacheDownloadFolder(object sender, RoutedEventArgs e)
    {
        States.Tool.DownloadFolder = TextDownloadFolder.Text;
        TextDownloadName.Validate();
    }

    private static void DownloadState(Loading.LoaderCombo<int> loader)
    {
        try
        {
            switch (loader.State)
            {
                case Common.LoadState.Finished:
                {
                    HintService.Show(Hints.Success(Lang.Text("Tools.Test.CustomDownload.Finished", loader.name)));
                    Console.Beep();
                    break;
                }
                case Common.LoadState.Failed:
                {
                    Common.Log(
                        loader.Error,
                        $"{loader.name}失败",
                        Common.LogLevel.Msgbox,
                        userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
                    Console.Beep();
                    break;
                }
                case Common.LoadState.Aborted:
                {
                    HintService.Show(Hints.Info(Lang.Text("Tools.Test.CustomDownload.Aborted", loader.name)));
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
                Common.CheckPermissionWithException(folder);
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    $"访问文件夹失败（{folder}）",
                    Common.LogLevel.Hint,
                    userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
                return;
            }

            Common.Log("[Download] 自定义下载文件名：" + fileName);
            Common.Log("[Download] 自定义下载文件目标：" + folder);
            var uuid = Common.GetUuid();
            Loading.LoaderBase loaderdownload;
            if (new HttpValidator().Validate(url).IsValid)
                loaderdownload = new LoaderDownload(Lang.Text("Tools.Test.CustomDownload.LoaderName", fileName),
                    new List<DownloadFile> { new(new[] { url }, folder + fileName, null, true, userAgent) });
            else // UNC 路径
                loaderdownload = new LoaderDownloadUnc(Lang.Text("Tools.Test.CustomDownload.LoaderName", fileName),
                    new Tuple<string, string>(url, folder + fileName));
            var loaderCombo = new Loading.LoaderCombo<int>(Lang.Text("Tools.Test.CustomDownload.LoaderTitle", uuid), new[] { loaderdownload })
            {
                OnStateChanged = a => DownloadState((Loading.LoaderCombo<int>)a),
                Identifier = "CustomDownload:" + uuid
            };
            loaderCombo.Start();
            Loading.LoaderTaskbarAdd(loaderCombo);
            Launcher.frmMain.BtnExtraDownload.ShowRefresh();
            Launcher.frmMain.BtnExtraDownload.Ribble();
        }

        catch (Exception ex)
        {
            Common.Log(
                ex,
                "开始自定义下载失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
        }
    }

    public static async void Jrrp()
    {
        try
        {
            var random = new Random(GenerateDailySeed());
            var luckValue = random.Next(0, 101);
            var rating = GetRating(luckValue);
            var title = Lang.Text("Tools.Test.Luck.MsgboxTitle", Lang.Date(DateTime.Now, "d"));

            if (luckValue >= 60)
                await DialogService.Instance.ShowAsync(new TextDialogRequest
                {
                    Title = title,
                    Content = Lang.Text("Tools.Test.Luck.MessageGood", luckValue, rating),
                    Buttons = DialogButtons.Ok,
                    ForceWait = false
                });
            else
                await DialogService.Instance.ShowAsync(new TextDialogRequest
                {
                    Title = title,
                    Content = Lang.Text("Tools.Test.Luck.MessageBad", luckValue, rating),
                    Buttons = DialogButtons.Ok,
                    IsWarn = luckValue <= 30,
                    ForceWait = false
                });
        }
        catch (Exception ex)
        {
            Common.Log(ex, "显示今日人品弹窗失败", Common.LogLevel.Feedback,
                userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
        }
    }

    public static void RubbishClear()
    {
        Common.RunInUi(() =>
        {
            if (Launcher.frmToolsTest is not null && Launcher.frmToolsTest.BtnClear is not null)
                Launcher.frmToolsTest.BtnClear.IsEnabled = false;
        });
        // 只有当没有运行中的Minecraft游戏且启动器不在加载状态时才能清理

        // 清理的文件数量
        // 所有 Minecraft 文件夹


        // 寻找所有 Minecraft 文件夹

        // 删除 Minecraft 的缓存
        // 删除日志和崩溃报告并计数

        // 删除 Natives 文件

        // 删除 PCL 的缓存

        Common.RunInNewThread(() =>
        {
            try
            {
                if (!GameWatcher.hasRunningMinecraft && GameLaunch.mcLaunchLoader.State != Common.LoadState.Loading)
                {
                    if (NetFacade.HasDownloadingTask())
                    {
                        HintService.Show(Hints.Info(Lang.Text("Tools.Test.Clean.WaitForDownload")));
                        return;
                    }

                    if (!GameFolders.mcFolderList.Any()) GameFolders.McFolderListLoader.Start();
                    if (States.Hint.CleanJunkFile <= 2)
                    {
                        if ((DialogService.Instance.Show(new TextDialogRequest
                        {
                            Title = Lang.Text("Tools.Test.Clean.ConfirmTitle"),
                            Content = Lang.Text("Tools.Test.Clean.ConfirmMessage"),
                            Buttons = DialogButtons.OkCancel,
                            ForceWait = true
                        }).Result?.Button ?? 0) == 2) return;
                        States.Hint.CleanJunkFile += 1;
                    }

                    var num = 0;
                    var cleanMcFolderList = new List<DirectoryInfo>();
                    if (!GameFolders.mcFolderList.Any()) GameFolders.McFolderListLoader.WaitForExit();
                    foreach (var mcFolder in GameFolders.mcFolderList)
                    {
                        cleanMcFolderList.Add(new DirectoryInfo(mcFolder.Location));
                        var dirInfo = new DirectoryInfo(mcFolder.Location + "versions");
                        if (dirInfo.Exists)
                            foreach (var item in dirInfo.EnumerateDirectories())
                                cleanMcFolderList.Add(item);
                    }

                    foreach (var dirInfo in cleanMcFolderList)
                    {
                        num += Common.DeleteDirectory(
                            dirInfo.FullName + (dirInfo.FullName.EndsWith(@"\") ? "" : @"\") + @"crash-reports\", true);
                        num += Common.DeleteDirectory(
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
                                num += Common.DeleteDirectory(dirInfo2.FullName, true);
                    }

                    num += Common.DeleteDirectory(Common.pathTemp, true);
                    num += Common.DeleteDirectory(Path.Combine(SystemPaths.DriveLetter, "ProgramData", "PCL"), true);
                    if (num != 0)
                    {
                        DialogService.Instance.Show(new TextDialogRequest
                        {
                            Title = Lang.Text("Tools.Test.Clean.Cleared"),
                            Content = Lang.Text("Tools.Test.Clean.ClearedMessage", num),
                            Buttons = DialogButtons.Ok,
                            ForceWait = true
                        });
                        Process.Start(new ProcessStartInfo(Basics.ExecutablePath));
                        FormMain.EndProgramForce();
                    }
                    else
                    {
                        HintService.Show(Hints.Info(Lang.Text("Tools.Test.Clean.NoFiles")));
                    }
                }
                else
                {
                    HintService.Show(Hints.Info(Lang.Text("Tools.Test.Clean.CloseGameFirst")));
                }
            }
            catch (Exception ex)
            {
                Common.Log(
                    ex,
                    "清理垃圾失败",
                    Common.LogLevel.Hint,
                    userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
            }
            finally
            {
                Common.RunInUiWait(() =>
                {
                    if (Launcher.frmToolsTest is not null && Launcher.frmToolsTest.BtnClear is not null)
                        Launcher.frmToolsTest.BtnClear.IsEnabled = true;
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
            TextDownloadName.Text = Common.GetFileNameFromPath(WebUtility.UrlDecode(TextDownloadUrl.Text));
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
            Common.Log(ex, "打开下载文件夹失败");
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
        HintService.Show(Hints.Info(Lang.Text("Tools.Test.Skin.Fetching")));
        Common.RunInNewThread(() =>
        {
            try
            {
                if (id.Length < 3)
                {
                    HintService.Show(Hints.Info(Lang.Text("Tools.Test.Skin.InvalidId")));
                }
                else
                {
                    var result = (string)ProfileUi.McLoginMojangUuid(id, true);
                    result = Skin.McSkinGetAddress(result, "Mojang");
                    result = Skin.McSkinDownload(result);
                    Common.RunInUi(() =>
                    {
                        var path = SystemDialogs.SelectSaveFile(Lang.Text("Tools.Test.Skin.Save"), $"{id}.png", Lang.Text("Tools.Test.Skin.FileFilter"));
                        Common.CopyFile(result, path);
                        HintService.Show(Hints.Success(Lang.Text("Tools.Test.Skin.Saved", id)));
                    });
                }
            }
            catch (Exception ex)
            {
                if (ex.ToString().Contains("429"))
                {
                    HintService.Show(Hints.Error(Lang.Text("Tools.Test.Skin.TooFrequent")));
                    Common.Log($"获取正版皮肤失败（{id}）：获取皮肤太过频繁，请 5 分钟后再试！");
                }
                else
                {
                    Common.Log(ex, $"获取正版皮肤失败（{id}）");
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

    private void _AchievementInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is MyTextBox box) box.Validate();

        StartButtonRefresh();
    }

    /// <summary>
    ///     异步枚举系统字体并填充字体下拉框。
    /// </summary>
    /// <remarks>
    ///     枚举要遍历本机全部字体族，机器上装了几百个字体时在 UI 线程直接跑会明显卡住页面切换，
    ///     所以枚举放到后台线程，只有创建控件项那一步回到 UI 线程；整个列表只初始化一次。
    /// </remarks>
    private async Task _InitAchievementFontComboAsync()
    {
        if (_achievementFontInitialized) return;
        _achievementFontInitialized = true; // 先占位，避免展开动画期间重复触发

        ComboAchievementFont.IsEnabled = false;
        try
        {
            var families = await Task.Run(() => Media.Fonts.SystemFontFamilies
                .Where(f => !string.IsNullOrWhiteSpace(f.Source))
                .GroupBy(f => f.Source)
                .Select(g => g.First())
                .Select(f => (Source: f.Source, Display: _GetFontDisplayName(f)))
                .OrderBy(f => f.Display, StringComparer.CurrentCulture)
                .ToList());

            var items = new List<MyComboBoxItem>(families.Count + 1)
            {
                // 首项固定是内置字体：Tag 留空时 ResolveAchievementFont 会退回 AchievementFontFamily，
                // 这样用户没主动选字体时，渲染结果与这台机器装了哪些字体无关
                new MyComboBoxItem
                {
                    Content = Lang.Text("Common.Option.Default"),
                    FontFamily = AchievementFontFamily
                }
            };
            foreach (var (source, display) in families)
            {
                var item = new MyComboBoxItem
                {
                    Content = display,
                    Tag = source // Tag 保存真正的字体族名，渲染时用它构造 FontFamily
                };
                try
                {
                    // 让每一项用自己的字体渲染，下拉列表里能直接看到字体效果
                    item.FontFamily = new Media.FontFamily(source);
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
        catch (Exception ex)
        {
            Common.Log(ex, "枚举系统字体失败");
        }
        finally
        {
            ComboAchievementFont.IsEnabled = true;
        }
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

        LoadAchievement.State.LoadingState = MyLoading.MyLoadingState.Run;
        LoadAchievement.Visibility = Visibility.Visible;
        _isRenderingAchievement = true;
        StartButtonRefresh(); // 立刻禁用两个按钮，避免重复触发
        try
        {
            await Task.Yield();
            var itemId = AchievementBlockTextBox.Text.Trim();
            var rawTitle = AchievementTitleTextBox.Text;
            var rawLine1 = AchievementString1TextBox.Text;
            var rawLine2 = AchievementString2TextBox.Text;

            var (icon, notFound) = await _LoadAchievementIconAsync(itemId);
            if (notFound && !string.IsNullOrEmpty(itemId))
                HintService.Show(Hints.Info(Lang.Text("Tools.Test.Achievement.IconNotFound", itemId)));

            AchievementImage.Source = _RenderAchievementImage(icon, itemId, rawTitle, rawLine1, rawLine2);
            _vm.AchievementImageVisible = true;
        }
        catch (Exception ex)
        {
            Common.Log(ex, "渲染成就图片失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.Test.Achievement.FetchFailed")));
        }
        finally
        {
            LoadAchievement.State.LoadingState = MyLoading.MyLoadingState.Stop;
            LoadAchievement.Visibility = Visibility.Collapsed;
            _isRenderingAchievement = false;
            StartButtonRefresh();
        }
    }

    private async void BtnAchievementSave_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isRenderingAchievement) return;

        LoadAchievement.State.LoadingState = MyLoading.MyLoadingState.Run;
        LoadAchievement.Visibility = Visibility.Visible;
        _isRenderingAchievement = true;
        StartButtonRefresh(); // 立刻禁用两个按钮，避免重复触发
        try
        {
            await Task.Yield(); // 先把加载指示绘制出来
            var itemId = AchievementBlockTextBox.Text.Trim();
            var rawTitle = AchievementTitleTextBox.Text;
            var rawLine1 = AchievementString1TextBox.Text;
            var rawLine2 = AchievementString2TextBox.Text;

            var (icon, notFound) = await _LoadAchievementIconAsync(itemId);

            if (notFound && !string.IsNullOrEmpty(itemId))
                HintService.Show(Hints.Info(Lang.Text("Tools.Test.Achievement.IconNotFound", itemId)));

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(
                _RenderAchievementImage(icon, itemId, rawTitle, rawLine1, rawLine2)));

            using var buffer = new MemoryStream();
            encoder.Save(buffer);
            buffer.Position = 0;

            var path = SystemDialogs.SelectSaveFile(Lang.Text("Tools.Test.Achievement.Save"),
                _SanitizeFileName(_StripMinecraftCodes(rawTitle)) + ".png",
                Lang.Text("Tools.Test.Achievement.FileFilter"));
            if (string.IsNullOrEmpty(path))
            {
                Common.Log("用户取消了保存操作");
                return;
            }

            using (var file = File.Create(path))
            {
                buffer.CopyTo(file);
            }

            HintService.Show(Hints.Success(Lang.Text("Tools.Test.Achievement.Saved")));
        }
        catch (Exception ex)
        {
            Common.Log(ex, "保存成就图片失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.Test.Achievement.FetchFailed")));
        }
        finally
        {
            LoadAchievement.State.LoadingState = MyLoading.MyLoadingState.Stop;
            LoadAchievement.Visibility = Visibility.Collapsed;
            _isRenderingAchievement = false;
            StartButtonRefresh();
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
        var titleColor = Media.Color.FromArgb(255, 0xFF, 0xFF, 0x55);
        var descColor = Media.Color.FromArgb(255, 255, 255, 255);

        var title = _TruncateText(_StripMinecraftCodes(rawTitle));
        var line1 = _TruncateText(_StripMinecraftCodes(rawLine1));
        var line2 = _TruncateText(_StripMinecraftCodes(rawLine2));

        var description = line1.Length == 0 ? line2
            : line2.Length == 0 ? line1
            : line1 + "\n" + line2;

        var typeface = new Media.Typeface(ResolveAchievementFont(), FontStyles.Normal, FontWeights.Normal,
            FontStretches.Normal);

        var textX = padding + iconSize + padding;
        var maxTextWidth = contentWidth - textX - padding;

        List<Media.FormattedText> MakeLines(string text, Media.Color color)
        {
            var lines = new List<Media.FormattedText>();
            foreach (var line in _WrapTextByWidth(text, typeface, fontSize, maxTextWidth, superSample).Split('\n'))
            {
                lines.Add(new Media.FormattedText(line, CultureInfo.CurrentUICulture, _GetFlowDirection(line),
                    typeface, fontSize, new Media.SolidColorBrush(color), superSample)
                {
                    MaxTextWidth = maxTextWidth,
                    Trimming = TextTrimming.None // 已经手动断好行，不需要再省略
                });
            }

            return lines;
        }

        var titleLines = MakeLines(title, titleColor);
        var titleShadowLines = MakeLines(title, shadowColor);
        var descLines = string.IsNullOrEmpty(description) ? null : MakeLines(description, descColor);
        var descShadowLines = descLines is null ? null : MakeLines(description, shadowColor);

        // 卡片高度：最小高度与「文字总高 + 上下内边距」取较大者
        var titleHeight = titleLines.Sum(line => line.Height);
        var descHeight = descLines?.Sum(line => line.Height) ?? 0;
        var totalTextHeight = titleHeight + (descLines != null ? textSpacing + descHeight : 0);
        var contentHeight = Math.Max(minContentHeight, totalTextHeight + padding * 2);

        var totalWidth = contentWidth + borderWidth * 2;
        var totalHeight = contentHeight + borderWidth * 2;

        var visual = new Media.DrawingVisual();
        // 本地贴图是 16×16 的像素图，放大时用最近邻才不会糊；在线源是 256×256 的渲染图，
        // 缩到 32×32 若仍用最近邻会丢掉 7/8 的像素，边缘全是锯齿，需要高质量插值。
        Media.RenderOptions.SetBitmapScalingMode(visual,
            icon is not null && icon.PixelWidth > iconSize
                ? Media.BitmapScalingMode.HighQuality
                : Media.BitmapScalingMode.NearestNeighbor);

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

            // ---- 2. 图标：优先用真实贴图，取不到时画一个由物品 ID 决定颜色的占位方块 ----
            var iconRect = new Rect(padding + borderWidth, (totalHeight - iconSize) / 2, iconSize, iconSize);
            if (icon != null)
                dc.DrawImage(icon, iconRect);
            else
                _DrawAchievementIcon(dc, iconRect.TopLeft, iconSize, itemId);

            // ---- 3. 文字（整体垂直居中，每行带 1px 右下偏移的阴影）----
            // 每行都从同一个 x 开始绘制；段落自己的对齐（LTR 靠左、RTL 靠右）由 FormattedText 负责。
            void DrawLines(List<Media.FormattedText> lines, List<Media.FormattedText> shadows, double startY)
            {
                var y = startY;
                for (var i = 0; i < lines.Count; i++)
                {
                    dc.DrawText(shadows[i], new Point(textX + borderWidth + 1, y + 1));
                    dc.DrawText(lines[i], new Point(textX + borderWidth, y));
                    y += lines[i].Height;
                }
            }

            var textStartY = (totalHeight - totalTextHeight) / 2;
            DrawLines(titleLines, titleShadowLines, textStartY + titleYAdjust);
            if (descLines != null && descShadowLines != null)
                DrawLines(descLines, descShadowLines, textStartY + titleHeight + textSpacing);
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

            var candidate = current + rune.ToString();
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
    private static FlowDirection _GetFlowDirection(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value is >= 0x0590 and <= 0x08FF
                or >= 0xFB1D and <= 0xFDFD
                or >= 0xFE70 and <= 0xFEFF)
                return FlowDirection.RightToLeft;
            if (System.Text.Rune.IsLetter(rune))
                return FlowDirection.LeftToRight;
        }

        return FlowDirection.LeftToRight;
    }

    /// <summary>
    ///     测量一段文本的显示宽度。
    /// </summary>
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
    private static string _TruncateText(string text)
    {
        const int maxLength = 67;
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;

        var builder = new System.Text.StringBuilder(maxLength + 3);
        foreach (var rune in text.EnumerateRunes())
        {
            if (builder.Length + rune.Utf16SequenceLength > maxLength) break;
            builder.Append(rune.ToString());
        }

        return builder.Append("...").ToString();
    }

    /// <summary>
    ///     移除 Minecraft 颜色代码（形如 §c、§l）。
    /// </summary>
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
    ///     把文本转换成可用作文件名的形式。
    /// </summary>
    private static string _SanitizeFileName(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "achievement";

        var builder = new System.Text.StringBuilder(text.Length);
        foreach (var c in text)
            if (Array.IndexOf(Path.GetInvalidFileNameChars(), c) < 0 && c is not ('\r' or '\n'))
                builder.Append(c);

        var name = builder.ToString().Trim().TrimEnd('.');
        if (name.Length > 50) name = name[..50].TrimEnd();
        return string.IsNullOrEmpty(name) ? "achievement" : name;
    }

    /// <summary>
    ///     定位实例实际启用的客户端 JAR，先跟 jar 覆盖字段，再沿继承链向上查找。
    /// </summary>
    private static string? _ResolveClientJarPath(McInstance instance)
    {
        // 父实例要靠游戏文件夹拼路径，没选文件夹时直接放弃，交给在线源兜底
        var folder = GameFolders.mcFolderSelected;
        if (string.IsNullOrEmpty(folder)) return null;

        var current = instance;
        // 继承链可能是自引用或成环（inheritsFrom 指向自己、或两个实例互指），按「实例目录 + 名字」去重：
        // 同一个实例再次出现说明已经绕回来了，必须停下，否则这里会无限 new McInstance 卡死 UI 线程
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (current is not null)
        {
            if (!visited.Add(current.PathInstance + current.Name)) break;

            var jarName = current.JsonObject["jar"]?.ToString();
            var target = current;
            if (!string.IsNullOrEmpty(jarName) && jarName != current.Name)
            {
                try
                {
                    target = new McInstance(jarName);
                }
                catch
                {
                    target = current; // jar 字段指向的实例缺失时退回自身
                }
            }

            var jar = target.PathInstance + target.Name + ".jar";
            if (File.Exists(jar)) return jar;

            if (string.IsNullOrEmpty(current.InheritInstanceName)) break;
            try
            {
                current = new McInstance(Path.Combine(folder, "versions", current.InheritInstanceName));
            }
            catch
            {
                break; // 父实例信息异常时停止向上查找，交给在线源兜底
            }
        }

        return null;
    }

    /// <summary>
    ///     从客户端 JAR 中读出指定物品贴图的原始字节。
    /// </summary>
    private static byte[]? _ReadJarEntryBytes(string jarPath, string itemId)
    {
        try
        {
            using var jar = new System.IO.Compression.ZipArchive(
                new FileStream(jarPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            foreach (var folder in new[] { "item", "block", "items", "blocks" })
            {
                var entry = jar.GetEntry($"assets/minecraft/textures/{folder}/{itemId}.png");
                if (entry is null) continue;

                using var entryStream = entry.Open();
                using var buffer = new MemoryStream();
                entryStream.CopyTo(buffer);
                return buffer.ToArray();
            }
        }
        catch
        {
            // 任何异常都静默退回内置图标，不能因为找不到贴图就让整张图生成失败
        }

        return null;
    }

    /// <summary>
    ///     把图片字节解码为冻结的位图。
    /// </summary>
    private static BitmapSource? _DecodeIcon(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // 一次性读入内存，避免文件句柄被占用
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///     在限定时间内下载指定地址的字节内容。
    /// </summary>
    private static byte[]? _DownloadBytes(string url)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var bytes = NetworkService.GetClient()
                .GetByteArrayAsync(url, cts.Token)
                .GetAwaiter().GetResult();
            return bytes is { Length: > 0 } ? bytes : null;
        }
        catch (OperationCanceledException)
        {
            Common.Log("在线图标获取超时：" + url);
            return null;
        }
        catch (Exception ex)
        {
            Common.Log(ex, "在线图标获取失败：" + url);
            return null;
        }
    }

    private async Task<(BitmapSource? Icon, bool NotFound)> _LoadAchievementIconAsync(string itemId)
    {
        // 实例解析与 JAR 定位留在 UI 线程（要访问实例列表），
        // 打开 JAR、拷贝字节放到后台，避免实例所在磁盘较慢时卡住界面和加载动画
        string? jarPath = null;
        if (!string.IsNullOrWhiteSpace(itemId) && !string.IsNullOrEmpty(GameFolders.mcFolderSelected))
        {
            var instance = InstanceList.McMcInstanceSelected;
            if (instance is not null) jarPath = _ResolveClientJarPath(instance);
        }

        if (jarPath is not null)
        {
            var path = jarPath;
            var localBytes = await Task.Run(() => _ReadJarEntryBytes(path, itemId));
            var local = localBytes is null ? null : _DecodeIcon(localBytes);
            if (local != null) return (local, false);
        }

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
        if (_builtInBarrierLoadFailed) return null;

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
            Common.Log(ex, "内置屏障图标加载失败");
            _builtInBarrierLoadFailed = true;
            return null;
        }
    }

    /// <summary>
    ///     从在线源加载屏障图标。
    /// </summary>
    /// <returns>屏障图标；请求失败时返回 null。</returns>
    private static BitmapSource? _LoadOnlineBarrier()
    {
        var bytes = _DownloadBytes(OnlineBarrierUrl);
        return bytes is null ? null : _DecodeIcon(bytes);
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
            var bytes = _DownloadBytes(url);
            if (bytes is null) continue;

            var bitmap = _DecodeIcon(bytes);
            if (bitmap is null) continue;

            OnlineIconCache[itemId] = bitmap;
            return bitmap;
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

    private int GetHeadSize() => CmbHeadSize.SelectedIndex switch
    {
        0 => 64,
        1 => 96,
        2 => 128,
        _ => 64
    };

    private void BtnSelectSkin_Click(object sender, MouseButtonEventArgs e)
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
                HintService.Show(Hints.Error(Lang.Text("Tools.Test.Avatar.InvalidSize")));
                _vm.SkinPreviewVisible = false;
                return;
            }

            generatedHeadBitmap = GenerateHeadFromSkin(currentSkinBitmap);

            ImgFace.Source = BitmapToBitmapImage(generatedHeadBitmap);
            ImgHair.Source = null;

            _vm.SkinPreviewVisible = true;
            HintService.Show(Hints.Success(Lang.Text("Tools.Test.Avatar.Generated")));
        }

        catch (Exception ex)
        {
            Common.Log(ex, "生成头像失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.Test.Avatar.GenerateFailed", ex.Message)));
            _vm.SkinPreviewVisible = false;
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
            HintService.Show(Hints.Error(Lang.Text("Tools.Test.Avatar.SelectFirst")));
            return;
        }

        var savePath = SystemDialogs.SelectSaveFile(Lang.Text("Tools.Test.Avatar.Save"), "Head.png");

        if (string.IsNullOrEmpty(savePath))
            return;

        generatedHeadBitmap.Save(savePath, ImageFormat.Png);
        HintService.Show(Hints.Success(Lang.Text("Tools.Test.Avatar.Saved")));
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
}
