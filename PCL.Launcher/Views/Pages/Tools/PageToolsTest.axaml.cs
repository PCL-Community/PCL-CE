using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.Secret;
using PCL.Core.Utils.Validate;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.UI.Controls;
using PCL.UI.Seams;

namespace PCL.Views.Pages.Tools;

/// <summary>
///     测试工具页：常用工具（清理垃圾、今日人品、启动次数、创建快捷方式、手动崩溃）、
///     自定义下载、正版玩家皮肤下载、服务器查询、自定义成就图片与皮肤头像生成器。
///     自定义下载走统一的下载管线，皮肤与成就图片通过联网接口获取，头像在本地由皮肤贴图裁切生成。
/// </summary>
public partial class PageToolsTest : MyPageRight
{
    private Bitmap? _currentSkin;
    private Bitmap? _generatedHead;
    private string? _skinPath;

    public PageToolsTest()
    {
        InitializeComponent();
        PanScroll = PanBack;
        Current = this;

        BtnClean.Click += (_, _) => ClearTrash();
        BtnLuck.Click += (_, _) => ShowDailyFortune();
        BtnLaunchCount.Click += (_, _) => _ShowLaunchCount();
        BtnShortcut.Click += (_, _) => _CreateShortcut();
        BtnCrash.Click += (_, _) => _ManualCrash();
#if DEBUG
        BtnCrash.IsVisible = true;
#endif

        TextDownloadUrl.TextChanged += (_, _) => _OnDownloadUrlChanged();
        TextDownloadFolder.TextChanged += (_, _) => _RefreshDownloadButtons();
        TextDownloadName.TextChanged += (_, _) => _RefreshDownloadButtons();
        BtnDownloadSelectFolder.Click += (_, _) => _SelectDownloadFolder();
        BtnDownloadOpen.Click += (_, _) => _OpenDownloadFolder();
        BtnDownloadStart.Click += (_, _) => _StartCustomDownload();
        TextDownloadFolder.Text = States.Tool.DownloadFolder;
        if (string.IsNullOrWhiteSpace(TextDownloadFolder.Text))
            TextDownloadFolder.Text = Path.Combine(Paths.Data, "MyDownload");
        _RefreshDownloadButtons();

        BtnSkinSave.Click += (_, _) => _ = _SavePlayerSkinAsync();

        TextAchievementItem.TextChanged += (_, _) => _RefreshAchievementButtons();
        TextAchievementTitle.TextChanged += (_, _) => _RefreshAchievementButtons();
        TextAchievementLine1.TextChanged += (_, _) => _RefreshAchievementButtons();
        TextAchievementLine2.TextChanged += (_, _) => _RefreshAchievementButtons();
        BtnAchievementPreview.Click += (_, _) => _ = _PreviewAchievementAsync();
        BtnAchievementSave.Click += (_, _) => _ = _SaveAchievementAsync();
        _RefreshAchievementButtons();

        CmbHeadSize.ItemsSource = new List<string>
        {
            "64x64",
            "96x96",
            "128x128"
        };
        CmbHeadSize.SelectedIndex = 0;
        CmbHeadSize.SelectionChanged += (_, _) => _RegenerateHead();
        BtnSelectSkin.Click += (_, _) => _SelectSkinFile();
        BtnSaveHead.Click += (_, _) => _SaveHead();
    }

    public static PageToolsTest? Current { get; private set; }

    /// <summary>
    ///     WPF <c>PageToolsTest.RubbishClear</c> 的等价实现：运行中游戏、下载任务、清理确认、
    ///     完整清理范围与重启行为都与原版一致。
    /// </summary>
    public static void ClearTrash()
    {
        LauncherCommon.RunInUi(() =>
        {
            if (Current is { } page)
                page.BtnClean.IsEnabled = false;
        });

        LauncherCommon.RunInNewThread(
            () =>
            {
                try
                {
                    if (GameWatcher.hasRunningMinecraft
                        || GameLaunch.mcLaunchLoader.State == LoadState.Loading)
                    {
                        HintService.Show(Hints.Info(Lang.Text("Tools.Test.Clean.CloseGameFirst")));
                        return;
                    }

                    if (NetFacade.HasDownloadingTask())
                    {
                        HintService.Show(Hints.Info(Lang.Text("Tools.Test.Clean.WaitForDownload")));
                        return;
                    }

                    if (GameFolders.mcFolderList.Count == 0)
                        GameFolders.McFolderListLoader.Start();

                    if (States.Hint.CleanJunkFile <= 2)
                    {
                        if ((DialogService.Instance.Show(
                                        new TextDialogRequest
                                        {
                                            Title = Lang.Text("Tools.Test.Clean.ConfirmTitle"),
                                            Content = Lang.Text("Tools.Test.Clean.ConfirmMessage"),
                                            Buttons = DialogButtons.OkCancel,
                                            ForceWait = true
                                        })
                                    .Result?.Button
                                ?? 0)
                            == 2)
                            return;
                        States.Hint.CleanJunkFile += 1;
                    }

                    var count = 0;
                    var cleanFolders = new List<DirectoryInfo>();
                    if (GameFolders.mcFolderList.Count == 0)
                        GameFolders.McFolderListLoader.WaitForExit();
                    foreach (var mcFolder in GameFolders.mcFolderList)
                    {
                        cleanFolders.Add(new DirectoryInfo(mcFolder.Location));
                        var versionsFolder = new DirectoryInfo(
                            Path.Combine(mcFolder.Location, "versions"));
                        if (versionsFolder.Exists)
                            cleanFolders.AddRange(versionsFolder.EnumerateDirectories());
                    }

                    foreach (var folder in cleanFolders)
                    {
                        count += _DeleteDirectoryCounted(
                            Path.Combine(folder.FullName, "crash-reports"));
                        count += _DeleteDirectoryCounted(Path.Combine(folder.FullName, "logs"));
                        foreach (var file in folder.EnumerateFiles("*"))
                            if (file.Name.StartsWith("hs_err_pid", StringComparison.Ordinal)
                                || file.Name.EndsWith(".log", StringComparison.OrdinalIgnoreCase)
                                || file.Name == "WailaErrorOutput.txt")
                            {
                                file.Delete();
                                count += 1;
                            }

                        count += folder
                            .EnumerateDirectories()
                            .Where(nativeFolder => nativeFolder.Name.EndsWith(
                                    "-natives",
                                    StringComparison.Ordinal)
                                || nativeFolder.Name == "natives-windows-x86_64")
                            .Sum(nativeFolder => _DeleteDirectoryCounted(nativeFolder.FullName));
                    }

                    count += _DeleteDirectoryCounted(LauncherPaths.Temp);
                    if (OperatingSystem.IsWindows())
                        count += _DeleteDirectoryCounted(
                            Path.Combine(
                                Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\",
                                "ProgramData",
                                "PCL"));

                    if (count != 0)
                    {
                        DialogService.Instance.Show(
                            new TextDialogRequest
                            {
                                Title = Lang.Text("Tools.Test.Clean.Cleared"),
                                Content = Lang.Text("Tools.Test.Clean.ClearedMessage", count),
                                Buttons = DialogButtons.Ok,
                                ForceWait = true
                            });
                        UiSeamInteraction.Restart();
                    }
                    else
                    {
                        HintService.Show(Hints.Info(Lang.Text("Tools.Test.Clean.NoFiles")));
                    }
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(
                        ex,
                        "清理垃圾失败",
                        LauncherLogLevel.Hint,
                        userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
                }
                finally
                {
                    LauncherCommon.RunInUi(() =>
                    {
                        if (Current is { } page)
                            page.BtnClean.IsEnabled = true;
                    });
                }
            },
            "Rubbish Clear");
    }

    private static int _DeleteDirectoryCounted(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return 0;
            var count = Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length;
            Directory.Delete(path, true);
            return Math.Max(1, count);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Tools", $"清理目录失败：{path}");
            return 0;
        }
    }

    /// <summary>WPF <c>PageToolsTest.Jrrp</c> 的等价实现。</summary>
    public static async void ShowDailyFortune()
    {
        try
        {
            var random = new Random(_GenerateDailySeed());
            var luckValue = random.Next(0, 101);
            var rating = _GetLuckRating(luckValue);
            var title = Lang.Text("Tools.Test.Luck.MsgboxTitle", Lang.Date(DateTime.Now, "d"));

            if (luckValue >= 60)
                await DialogService.Instance.ShowAsync(
                    new TextDialogRequest
                    {
                        Title = title,
                        Content = Lang.Text("Tools.Test.Luck.MessageGood", luckValue, rating),
                        Buttons = DialogButtons.Ok,
                        ForceWait = false
                    });
            else
                await DialogService.Instance.ShowAsync(
                    new TextDialogRequest
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
            LauncherCommon.Log(
                ex,
                "显示今日人品弹窗失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
        }
    }

    private static int _GenerateDailySeed()
    {
        var datePart = DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return _Djb2Hash(datePart + Identify.LauncherId);
    }

    private static int _Djb2Hash(string text)
    {
        const long prime = 33L;
        var hash = text.Aggregate(
            5381L,
            (current, character) => (current * prime + character) % 0x100000000L);

        return (int)(hash & 0x7FFFFFFFL);
    }

    private static string _GetLuckRating(int luckValue)
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

    private async void _ShowLaunchCount()
    {
        try
        {
            await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Tools.Test.LaunchCount.Title"),
                    Content =
                        Lang.Text("Tools.Test.LaunchCount.Message", States.System.LaunchCount),
                    Buttons = DialogButtons.Ok
                });
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "显示启动次数失败");
        }
    }

    private void _CreateShortcut()
    {
        try
        {
            var target = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(target)) return;
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var fileName = Lang.Text("Tools.Test.Shortcut.FileName", string.Empty);
            var extension = OperatingSystem.IsWindows() ? ".lnk" : ".desktop";
            Files.CreateShortcut(
                Path.Combine(desktop, fileName + extension),
                target,
                workingDirectory: AppContext.BaseDirectory,
                description: "PCL CE");
            HintService.Show(
                Hints.Success(
                    Lang.Text(
                        "Tools.Test.Shortcut.Created",
                        Lang.Text("Tools.Test.Shortcut.Desktop"))));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "创建快捷方式失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.Test.Error.OperationFailed")));
        }
    }

    private void _ManualCrash()
    {
        throw new InvalidOperationException(Lang.Text("Tools.Test.Crash.ManualCrash"));
    }

    #region 自定义下载

    private void _OnDownloadUrlChanged()
    {
        var url = TextDownloadUrl.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(url) && string.IsNullOrEmpty(TextDownloadName.Text))
            try
            {
                var fileName = LauncherCommon.GetFileNameFromPath(WebUtility.UrlDecode(url));
                if (!string.IsNullOrWhiteSpace(fileName))
                    TextDownloadName.Text = fileName;
            }
            catch (Exception)
            {
            }

        _RefreshDownloadButtons();
    }

    private void _RefreshDownloadButtons()
    {
        var url = TextDownloadUrl.Text?.Trim() ?? "";
        var folder = TextDownloadFolder.Text?.Trim() ?? "";
        var name = TextDownloadName.Text?.Trim() ?? "";
        var urlValid = new HttpAndUncValidator().Validate(url).IsValid;
        var folderValid = new FolderPathValidator().Validate(folder).IsValid;
        var nameValid = new FileNameValidator().Validate(name).IsValid;
        BtnDownloadStart.IsEnabled = urlValid && folderValid && nameValid;
        BtnDownloadOpen.IsEnabled = !string.IsNullOrWhiteSpace(folder);
    }

    private void _SelectDownloadFolder()
    {
        var folder = SystemDialogs.SelectFolder(
            Lang.Text("Tools.Test.CustomDownload.SaveTo"),
            TextDownloadFolder.Text);
        if (!string.IsNullOrWhiteSpace(folder))
            TextDownloadFolder.Text = folder;
    }

    private void _OpenDownloadFolder()
    {
        try
        {
            var folder = TextDownloadFolder.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(folder)) return;
            Directory.CreateDirectory(folder);
            Basics.OpenPath(folder);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "打开下载文件夹失败");
        }
    }

    private void _StartCustomDownload()
    {
        var url = TextDownloadUrl.Text?.Trim() ?? "";
        var folder = TextDownloadFolder.Text?.Trim() ?? "";
        var name = TextDownloadName.Text?.Trim() ?? "";
        States.Tool.DownloadFolder = folder;
        StartCustomDownload(url, name, folder);
        TextDownloadUrl.Text = "";
        TextDownloadName.Text = "";
        _RefreshDownloadButtons();
    }

    /// <summary>WPF <c>PageToolsTest.StartCustomDownload</c> 的等价实现。</summary>
    public static void StartCustomDownload(string url, string? fileName, string? folder)
    {
        try
        {
            fileName = string.IsNullOrWhiteSpace(fileName)
                ? LauncherCommon.GetFileNameFromPath(url)
                : fileName;
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = SystemDialogs.SelectSaveFile(
                    Lang.Text("Tools.Test.CustomDownload.SelectLocation"),
                    fileName);
                if (string.IsNullOrEmpty(folder)) return;
                var folderPath = Path.GetDirectoryName(folder);
                if (string.IsNullOrEmpty(folderPath)) return;
                folder = folderPath;
            }

            folder = Path.GetFullPath(folder);
            Directory.CreateDirectory(folder);
            LauncherCommon.CheckPermissionWithException(folder);

            LauncherCommon.Log($"[Download] 自定义下载文件名：{fileName}");
            LauncherCommon.Log($"[Download] 自定义下载文件目标：{folder}");
            var uuid = LauncherCommon.GetUuid();
            var savePath = Path.Combine(folder, fileName);
            Loading.LoaderBase loader;
            if (new HttpValidator(true).Validate(url).IsValid)
                loader = new LoaderDownload(
                    Lang.Text("Tools.Test.CustomDownload.LoaderName", fileName),
                    [new DownloadFile([url], savePath, null, true)]);
            else
                loader = new LoaderDownloadUnc(
                    Lang.Text("Tools.Test.CustomDownload.LoaderName", fileName),
                    new Tuple<string, string>(url, savePath));

            var combo = new Loading.LoaderCombo<int>(
                Lang.Text("Tools.Test.CustomDownload.LoaderTitle", uuid),
                [loader])
            {
                Identifier = "CustomDownload:" + uuid
            };
            combo.OnStateChangedUi += (loaderBase, state, _) =>
            {
                if (state == LoadState.Finished)
                    HintService.Show(
                        Hints.Success(
                            Lang.Text("Tools.Test.CustomDownload.Finished", loaderBase.name)));
                else if (state == LoadState.Aborted)
                    HintService.Show(
                        Hints.Info(
                            Lang.Text("Tools.Test.CustomDownload.Aborted", loaderBase.name)));
                else if (state == LoadState.Failed)
                    HintService.Show(Hints.Error(Lang.Text("Tools.Test.Error.OperationFailed")));
            };
            combo.Start();
            Loading.LoaderTaskbarAdd(combo);
            UiSeamInteraction.RefreshDownloadButton(nameof(StartCustomDownload));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "开始自定义下载失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
        }
    }

    #endregion

    #region 正版玩家皮肤

    private async Task _SavePlayerSkinAsync()
    {
        var name = TextSkinID.Text?.Trim() ?? "";
        if (name.Length < 3)
        {
            HintService.Show(Hints.Info(Lang.Text("Tools.Test.Skin.InvalidId")));
            return;
        }

        HintService.Show(Hints.Info(Lang.Text("Tools.Test.Skin.Fetching")));
        var (path, error) = await Task.Run(() => _FetchPlayerSkin(name));
        if (path is null)
        {
            if (error == "429")
                HintService.Show(Hints.Error(Lang.Text("Tools.Test.Skin.TooFrequent")));
            else if (error == "invalid")
                HintService.Show(Hints.Info(Lang.Text("Tools.Test.Skin.InvalidId")));
            else
                HintService.Show(Hints.Error(Lang.Text("Tools.Test.Error.OperationFailed")));
            return;
        }

        var savePath = SystemDialogs.SelectSaveFile(
            Lang.Text("Tools.Test.Skin.Save"),
            $"{name}.png",
            Lang.Text("Tools.Test.Skin.FileFilter"));
        if (string.IsNullOrWhiteSpace(savePath))
            return;

        try
        {
            File.Copy(path, savePath, true);
            HintService.Show(Hints.Success(Lang.Text("Tools.Test.Skin.Saved", name)));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "保存玩家皮肤失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.Test.Error.OperationFailed")));
        }
    }

    private static (string? Path, string? Error) _FetchPlayerSkin(string name)
    {
        try
        {
            var uuid = JsonNode.Parse(
                    Requester.FetchString(
                        $"https://api.mojang.com/users/profiles/minecraft/{Uri.EscapeDataString(name)}"))
                ?["id"]
                ?.ToString();
            if (string.IsNullOrWhiteSpace(uuid)) return (null, "invalid");

            var profile = JsonNode.Parse(
                Requester.FetchString(
                    $"https://sessionserver.mojang.com/session/minecraft/profile/{uuid}"));
            var textures = profile?["properties"]
                ?.AsArray()
                .FirstOrDefault(property => property?["name"]?.ToString() == "textures")?["value"]
                ?.ToString();
            if (string.IsNullOrWhiteSpace(textures)) return (null, "invalid");

            var decoded =
                JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(textures)));
            var skinUrl = decoded?["textures"]?["SKIN"]?["url"]?.ToString();
            if (string.IsNullOrWhiteSpace(skinUrl)) return (null, "invalid");

            var local = MyImage.DownloadImageAsync(skinUrl).GetAwaiter().GetResult();
            return string.IsNullOrEmpty(local) ? (null, "invalid") : (local, null);
        }
        catch (Exception ex)
        {
            if (ex.ToString().Contains("429")) return (null, "429");
            LogWrapper.Error(ex, "Tools", $"获取正版皮肤失败（{name}）");
            return (null, "failed");
        }
    }

    #endregion

    #region 自定义成就图片

    private void _RefreshAchievementButtons()
    {
        var valid = _ValidateAchievementInput();
        BtnAchievementPreview.IsEnabled = valid;
        BtnAchievementSave.IsEnabled = valid;
    }

    private bool _ValidateAchievementInput()
    {
        var item = TextAchievementItem.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(item)
            || !item.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            return false;
        return !string.IsNullOrWhiteSpace(TextAchievementTitle.Text)
            && !string.IsNullOrWhiteSpace(TextAchievementLine1.Text);
    }

    private string _BuildAchievementUrl()
    {
        static string Escape(string? text)
        {
            return (text ?? "").Trim().Replace(" ", "..");
        }

        var url = "https://minecraft-api.com/api/achivements/"
            + Escape(TextAchievementItem.Text)
            + "/"
            + Escape(TextAchievementTitle.Text)
            + "/"
            + Escape(TextAchievementLine1.Text);
        var line2 = Escape(TextAchievementLine2.Text);
        if (!string.IsNullOrEmpty(line2))
            url += "/" + line2;
        return url;
    }

    private async Task _PreviewAchievementAsync()
    {
        if (!_ValidateAchievementInput())
        {
            HintService.Show(Hints.Error(Lang.Text("Tools.Test.Achievement.InvalidId")));
            return;
        }

        BtnAchievementPreview.IsEnabled = false;
        try
        {
            var url = _BuildAchievementUrl();
            var local = await Task.Run(() => MyImage.DownloadImageAsync(url));
            if (string.IsNullOrEmpty(local))
                throw new InvalidDataException("成就图片下载失败");
            ImgAchievement.Source = local;
            ImgAchievement.IsVisible = true;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "获取成就图片失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.Test.Achievement.FetchFailed")));
        }
        finally
        {
            _RefreshAchievementButtons();
        }
    }

    private async Task _SaveAchievementAsync()
    {
        if (!_ValidateAchievementInput())
        {
            HintService.Show(Hints.Error(Lang.Text("Tools.Test.Achievement.InvalidId")));
            return;
        }

        BtnAchievementSave.IsEnabled = false;
        try
        {
            var url = _BuildAchievementUrl();
            var local = await Task.Run(() => MyImage.DownloadImageAsync(url));
            if (string.IsNullOrEmpty(local))
                throw new InvalidDataException("成就图片下载失败");

            var savePath = SystemDialogs.SelectSaveFile(
                Lang.Text("Tools.Test.Achievement.Save"),
                (TextAchievementTitle.Text?.Trim() ?? "Achievement") + ".png",
                Lang.Text("Tools.Test.Achievement.FileFilter"));
            if (string.IsNullOrWhiteSpace(savePath))
                return;

            File.Copy(local, savePath, true);
            HintService.Show(Hints.Success(Lang.Text("Tools.Test.Achievement.Saved")));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "获取成就图片失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.Test.Achievement.FetchFailed")));
        }
        finally
        {
            _RefreshAchievementButtons();
        }
    }

    #endregion

    #region 皮肤头像生成器

    private void _SelectSkinFile()
    {
        var path = SystemDialogs.SelectFile(
            Lang.Text("Tools.Test.Avatar.FileFilter"),
            Lang.Text("Tools.Test.Avatar.SelectSkinFile"));
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            using var stream = File.OpenRead(path);
            var bitmap = new Bitmap(stream);
            if (bitmap.PixelSize.Width != bitmap.PixelSize.Height)
            {
                HintService.Show(Hints.Error(Lang.Text("Tools.Test.Avatar.InvalidSize")));
                BorderSkinPreview.IsVisible = false;
                return;
            }

            _currentSkin = bitmap;
            _skinPath = path;
            _RegenerateHead();
            HintService.Show(Hints.Success(Lang.Text("Tools.Test.Avatar.Generated")));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "生成头像失败");
            HintService.Show(
                Hints.Error(Lang.Text("Tools.Test.Avatar.GenerateFailed", ex.Message)));
            BorderSkinPreview.IsVisible = false;
        }
    }

    private int _GetHeadSize()
    {
        return CmbHeadSize.SelectedIndex switch
        {
            1 => 96,
            2 => 128,
            _ => 64
        };
    }

    private void _RegenerateHead()
    {
        if (_currentSkin is null || string.IsNullOrEmpty(_skinPath))
            return;

        try
        {
            _generatedHead = CreateHeadBitmap(_currentSkin, _GetHeadSize());
            ImgHead.Source = _generatedHead;
            BorderSkinPreview.IsVisible = true;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "生成头像失败");
            HintService.Show(
                Hints.Error(Lang.Text("Tools.Test.Avatar.GenerateFailed", ex.Message)));
            BorderSkinPreview.IsVisible = false;
        }
    }

    private static RenderTargetBitmap CreateHeadBitmap(Bitmap skin, int headSize)
    {
        var scale = Math.Max(1, skin.PixelSize.Width / 64);
        var faceRect = new Rect(8 * scale, 8 * scale, 8 * scale, 8 * scale);
        var hasHat = skin.PixelSize.Width >= 64 && skin.PixelSize.Height >= 32;
        var hatRect = new Rect(40 * scale, 8 * scale, 8 * scale, 8 * scale);

        var target = new RenderTargetBitmap(new PixelSize(headSize, headSize));
        using var context = target.CreateDrawingContext();
        using (context.PushRenderOptions(
                   new RenderOptions
                   {
                       BitmapInterpolationMode = BitmapInterpolationMode.None
                   }))
        {
            var inner = headSize - headSize / 8;
            var offset = headSize / 16;
            context.DrawImage(skin, faceRect, new Rect(offset, offset, inner, inner));
            if (hasHat)
                context.DrawImage(skin, hatRect, new Rect(0, 0, headSize, headSize));
        }

        return target;
    }

    private void _SaveHead()
    {
        if (_generatedHead is null)
        {
            HintService.Show(Hints.Error(Lang.Text("Tools.Test.Avatar.SelectFirst")));
            return;
        }

        var savePath = SystemDialogs.SelectSaveFile(
            Lang.Text("Tools.Test.Avatar.Save"),
            "Head.png");
        if (string.IsNullOrWhiteSpace(savePath))
            return;

        try
        {
            _generatedHead.Save(savePath, 100);
            HintService.Show(Hints.Success(Lang.Text("Tools.Test.Avatar.Saved")));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "保存头像失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.Test.Error.OperationFailed")));
        }
    }

    #endregion
}