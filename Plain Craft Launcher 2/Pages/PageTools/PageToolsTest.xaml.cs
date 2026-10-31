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

namespace PCL;

public partial class PageToolsTest : IPageView
{
    private readonly PageToolsTestViewModel _vm = new();
    private bool _refreshed;
    private Bitmap currentSkinBitmap;
    private Bitmap generatedHeadBitmap;

    private int headSize = 64;
    private string skinPath = "";

    public PageToolsTest()
    {
        InitializeComponent();
        DataContext = _vm;
        CmbHeadSize.SelectionChanged += CmbHeadSize_SelectionChanged;
        Loaded += (_, _) => RefreshOnNavigate();
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
                { OnStateChanged = a => DownloadState((Loading.LoaderCombo<int>)a) };
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

    private async void BtnAchievementPreview_Click(object sender, MouseButtonEventArgs e)
    {
        var url = GetAchievementUrl();
        Common.Log("[Net] 获取网络结果" + url);
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
                        _vm.AchievementImageVisible = true;
                    });
                }
            else if (response.StatusCode == HttpStatusCode.NotFound)
                Dispatcher.Invoke(() =>
                {
                    Common.Log("获取成就图片失败（404）");
                    HintService.Show(Hints.Error(Lang.Text("Tools.Test.Achievement.FetchFailed")));
                });

            else
                Dispatcher.Invoke(() => Common.Log("获取成就图片失败（" + (int)response.StatusCode + "）"));
        }

        catch (Exception ex)
        {
            Dispatcher.Invoke(() => Common.Log(ex, "获取成就图片失败"));
        }
    }

    private async void BtnAchievementSave_Click(object sender, MouseButtonEventArgs e)
    {
        var url = GetAchievementUrl();
        await DownloadImageToLocalAsync(url);
    }

    private async Task DownloadImageToLocalAsync(string imageUrl)
    {
        var savePath = Common.pathTemp + @"Download\" + Common.GetHash(imageUrl) + ".png";
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
                    SystemDialogs.SelectSaveFile(Lang.Text("Tools.Test.Achievement.Save"), AchievementTitleTextBox.Text + ".png", Lang.Text("Tools.Test.Achievement.FileFilter"));
                if (string.IsNullOrEmpty(path))
                {
                    Common.Log("用户取消了保存操作");
                    File.Delete(savePath);
                    return;
                }

                Common.CopyFile(savePath, path);
                File.Delete(savePath);
                HintService.Show(Hints.Success(Lang.Text("Tools.Test.Achievement.Saved")));
            }
            // 下载成功，返回 True
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // 捕获 404 错误
                Common.Log("获取成就图片失败（404）");
                HintService.Show(Hints.Error(Lang.Text("Tools.Test.Achievement.FetchFailed")));
            }
            else
            {
                // 处理其他非成功状态码
                Common.Log("获取成就图片失败（" + (int)response.StatusCode + "）");
            }
        }

        catch (Exception ex)
        {
            // 捕获所有其他异常（如网络连接问题）
            Common.Log(ex, "获取成就图片失败");
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
