using System.Globalization;
using System.Net;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.Secret;
using PCL.Core.Utils.Validate;
using PCL.Network;
using PCL.Network.Loaders;
using PCL.UI;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Tools;

/// <summary>
///     测试工具页：常用工具（清理垃圾、今日人品、启动次数、创建快捷方式、手动崩溃）、
///     自定义下载、正版玩家皮肤下载、服务器查询、自定义成就图片与皮肤头像生成器。
///     自定义下载走统一的下载管线，皮肤与成就图片通过联网接口获取，头像在本地由皮肤贴图裁切生成。
/// </summary>
public partial class PageToolsTest : MyPageRight
{
    public PageToolsTest()
    {
        InitializeComponent();
        PanScroll = PanBack;
        Current = this;
        DataContext = ViewModel;
        RenderOptions.SetBitmapInterpolationMode(ImgHead, BitmapInterpolationMode.None);

        TextDownloadUrl.ValidateRules.Add(new HttpAndUncValidator());
        TextDownloadFolder.ValidateRules.Add(new FolderPathValidator());
        TextDownloadName.ValidateRules.Add(new FileNameValidator());
        TextAchievementItem.ValidateRules.Add(
            new RegexValidator("^[a-z0-9_]+$", Lang.Text("Tools.Test.Achievement.InvalidId")));
        TextAchievementTitle.ValidateRules.Add(new NullOrEmptyValidator());
        TextAchievementLine1.ValidateRules.Add(new NullOrEmptyValidator());

        TextDownloadUrl.TextChanged += (_, _) =>
        {
            ViewModel.DownloadUrl = TextDownloadUrl.Text ?? string.Empty;
            RefreshDownloadButtons();
        };
        TextDownloadFolder.TextChanged += (_, _) =>
        {
            ViewModel.DownloadFolder = TextDownloadFolder.Text ?? string.Empty;
            TextDownloadName.Validate();
            RefreshDownloadButtons();
        };
        TextDownloadFolder.ValidatedTextChanged += (_, _) =>
        {
            if (TextDownloadFolder.IsValidated
                && !string.IsNullOrWhiteSpace(ViewModel.DownloadFolder))
                States.Tool.DownloadFolder = ViewModel.DownloadFolder;
        };
        TextDownloadName.TextChanged += (_, _) =>
        {
            ViewModel.DownloadName = TextDownloadName.Text ?? string.Empty;
            RefreshDownloadButtons();
        };
        TextDownloadUrl.Validate();
        TextDownloadFolder.Validate();
        TextDownloadName.Validate();
        RefreshDownloadButtons();

        TextAchievementItem.TextChanged += (_, _) => RefreshAchievementButtons();
        TextAchievementTitle.TextChanged += (_, _) => RefreshAchievementButtons();
        TextAchievementLine1.TextChanged += (_, _) => RefreshAchievementButtons();
        TextAchievementLine2.TextChanged += (_, _) => RefreshAchievementButtons();
        TextAchievementItem.Validate();
        TextAchievementTitle.Validate();
        TextAchievementLine1.Validate();
        RefreshAchievementButtons();

        CmbHeadSize.ItemsSource = new List<string>
        {
            "64x64",
            "96x96",
            "128x128"
        };
        CmbHeadSize.SelectedIndex = 0;

        CollapseTestCard(CardAchievement);
        CollapseTestCard(CardAvatar);
    }

    public static PageToolsTest? Current { get; private set; }

    public PageToolsTestViewModel ViewModel { get; } = new();

    private void RefreshDownloadButtons()
    {
        ViewModel.RefreshDownloadButtons(
            TextDownloadUrl.IsValidated,
            TextDownloadFolder.IsValidated,
            TextDownloadName.IsValidated);
    }

    private void RefreshAchievementButtons()
    {
        ViewModel.RefreshAchievementButtons(
            TextAchievementItem.IsValidated,
            TextAchievementTitle.IsValidated,
            TextAchievementLine1.IsValidated);
    }

    private static void CollapseTestCard(MyCard card)
    {
        card.SwapControl = card.Children.OfType<StackPanel>().LastOrDefault();
        if (card.SwapControl is null)
            return;
        card.IsSwapped = false;
        card.IsSwapped = true;
    }

    /// <summary>
    ///     WPF <c>PageToolsTest.RubbishClear</c> 的等价实现：运行中游戏、下载任务、清理确认、
    ///     完整清理范围与重启行为都与原版一致。
    /// </summary>
    public static void ClearTrash()
    {
        LauncherCommon.RunInUi(() =>
        {
            if (Current is { } page)
                page.ViewModel.CleanEnabled = false;
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
                        count += _DeleteDirectory(
                            Path.Combine(folder.FullName, "crash-reports"));
                        count += _DeleteDirectory(Path.Combine(folder.FullName, "logs"));
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
                            .Sum(nativeFolder => _DeleteDirectory(nativeFolder.FullName));
                    }

                    count += _DeleteDirectory(LauncherPaths.Temp);
                    if (OperatingSystem.IsWindows())
                        count += _DeleteDirectory(
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
                        UiInteraction.Restart();
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
                            page.ViewModel.CleanEnabled = true;
                    });
                }
            },
            "Rubbish Clear");
    }

    /// <summary>
    ///     删除目录并返回真正删除成功的文件数。与 WPF <c>Common.DeleteDirectory</c> 一样，
    ///     只在单个文件删除成功后计数，删除失败的目录不计入清理数量。
    /// </summary>
    private static int _DeleteDirectory(string path, bool ignoreIssue = true)
    {
        if (!Directory.Exists(path))
            return 0;

        var deletedCount = 0;
        string[] files;
        try
        {
            files = Directory.GetFiles(path);
        }
        catch (DirectoryNotFoundException ex)
        {
            LogWrapper.Warn(ex, "Tools", $"疑似为孤立符号链接，尝试直接删除（{path}）");
            try
            {
                Directory.Delete(path);
            }
            catch (Exception deleteEx)
            {
                LogWrapper.Warn(deleteEx, "Tools", $"清理目录失败：{path}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Tools", $"读取目录失败：{path}");
            if (!ignoreIssue)
                throw;
            return 0;
        }

        foreach (var filePath in files)
            for (var attempt = 0; attempt < 2; attempt++)
                try
                {
                    File.Delete(filePath);
                    deletedCount++;
                    break;
                }
                catch (Exception ex)
                {
                    if (attempt == 0)
                    {
                        LogWrapper.Warn(ex, "Tools", $"删除文件失败，将在 0.3s 后重试（{filePath}）");
                        Thread.Sleep(300);
                        continue;
                    }

                    if (!ignoreIssue)
                        throw;
                    LogWrapper.Warn(ex, "Tools", $"删除文件失败（{filePath}）");
                }

        string[] directories;
        try
        {
            directories = Directory.GetDirectories(path);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Tools", $"读取子目录失败：{path}");
            if (!ignoreIssue)
                throw;
            return deletedCount;
        }

        deletedCount += directories.Sum(directory => _DeleteDirectory(directory, ignoreIssue));

        for (var attempt = 0; attempt < 2; attempt++)
            try
            {
                Directory.Delete(path, true);
                break;
            }
            catch (Exception ex)
            {
                if (attempt == 0 && !LauncherCommon.RunInUi())
                {
                    LogWrapper.Warn(ex, "Tools", $"删除文件夹失败，将在 0.3s 后重试（{path}）");
                    Thread.Sleep(300);
                    continue;
                }

                if (!ignoreIssue)
                    throw;
                LogWrapper.Warn(ex, "Tools", $"删除文件夹失败（{path}）");
            }

        return deletedCount;
    }

    /// <summary>WPF 名称兼容入口。</summary>
    public static void RubbishClear()
    {
        ClearTrash();
    }

    /// <summary>WPF <c>PageToolsTest.Jrrp</c> 的等价实现。</summary>
    public static async void ShowDailyFortune()
    {
        try
        {
            var random = new Random(GenerateDailySeed());
            var luckValue = random.Next(0, 101);
            var rating = GetRating(luckValue);
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

    /// <summary>WPF 名称兼容入口。</summary>
    public static void Jrrp()
    {
        ShowDailyFortune();
    }

    /// <summary>WPF <c>GenerateDailySeed</c> 的等价实现：日期 + 启动器识别码的 DJB2 哈希。</summary>
    public static int GenerateDailySeed()
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

    /// <summary>WPF <c>GetRating</c> 的等价实现。</summary>
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

    /// <summary>WPF <c>PageToolsTest.StartCustomDownload</c> 的等价实现。</summary>
    public static void StartCustomDownload(
        string url,
        string? fileName,
        string? folder,
        string userAgent = "")
    {
        try
        {
            fileName = string.IsNullOrWhiteSpace(fileName)
                ? LauncherCommon.GetFileNameFromPath(WebUtility.UrlDecode(url) ?? url)
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
            if (new HttpValidator().Validate(url).IsValid)
                loader = new LoaderDownload(
                    Lang.Text("Tools.Test.CustomDownload.LoaderName", fileName),
                    [new DownloadFile([url], savePath, null, true, userAgent)]);
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
                switch (state)
                {
                    case LoadState.Finished:
                        HintService.Show(
                            Hints.Success(
                                Lang.Text("Tools.Test.CustomDownload.Finished", loaderBase.name)));
                        break;

                    case LoadState.Aborted:
                        HintService.Show(
                            Hints.Info(
                                Lang.Text("Tools.Test.CustomDownload.Aborted", loaderBase.name)));
                        break;

                    case LoadState.Failed:
                        LogWrapper.Error(
                            loaderBase.Error,
                            "Tools",
                            $"{loaderBase.name}失败");
                        HintService.Show(
                            Hints.Error(Lang.Text("Tools.Test.Error.OperationFailed")));
                        break;

                    case LoadState.Waiting:
                    case LoadState.Loading:
                        break;
                }
            };
            combo.Start();
            Loading.LoaderTaskbarAdd(combo);
            UiInteraction.RefreshDownloadButton(nameof(StartCustomDownload));
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
}