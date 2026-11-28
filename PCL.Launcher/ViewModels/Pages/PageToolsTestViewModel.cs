using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.Logging;
using PCL.Core.Platform;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;
using PCL.Network;
using PCL.UI.Achievement;
using PCL.UI.Controls;
using PCL.Views.Pages.Tools;

namespace PCL;

/// <summary>测试工具页的视图模型：工具命令、下载输入、皮肤与成就图片状态。</summary>
public partial class PageToolsTestViewModel : ViewModelBase
{
    private bool _achievementInputValid = true;
    private static readonly Dictionary<string, FontFamily> _AchievementFontCache = new();
    private Bitmap? _currentSkin;
    private string? _skinPath;

    public PageToolsTestViewModel()
    {
#if DEBUG
        CrashButtonVisible = true;
#endif
        DownloadFolder = string.IsNullOrWhiteSpace(States.Tool.DownloadFolder)
            ? Path.Combine(Paths.Data, "MyDownload")
            : States.Tool.DownloadFolder;
        try
        {
            DownloadFolder = Path.GetFullPath(DownloadFolder);
        }
        catch
        {
            DownloadFolder = Path.Combine(Paths.Data, "MyDownload");
        }

        DownloadOpenEnabled = !string.IsNullOrWhiteSpace(DownloadFolder);
    }

    [ObservableProperty] public partial bool AchievementButtonsEnabled { get; set; } = true;

    [ObservableProperty] public partial bool AchievementBusy { get; set; }

    [ObservableProperty] public partial string AchievementFontTag { get; set; } = string.Empty;

    [ObservableProperty] public partial string AchievementLoadingText { get; set; } = string.Empty;

    [ObservableProperty] public partial string AchievementImagePath { get; set; } = string.Empty;

    [ObservableProperty] public partial bool AchievementImageVisible { get; set; }

    [ObservableProperty] public partial string AchievementItem { get; set; } = string.Empty;

    [ObservableProperty] public partial string AchievementLine1 { get; set; } = string.Empty;

    [ObservableProperty] public partial string AchievementLine2 { get; set; } = string.Empty;

    [ObservableProperty] public partial string AchievementTitle { get; set; } = string.Empty;

    [ObservableProperty] public partial bool CleanEnabled { get; set; } = true;

    [ObservableProperty] public partial bool CrashButtonVisible { get; set; }

    [ObservableProperty] public partial bool DownloadOpenEnabled { get; set; }

    [ObservableProperty] public partial string DownloadFolder { get; set; } = string.Empty;

    [ObservableProperty] public partial string DownloadName { get; set; } = string.Empty;

    [ObservableProperty] public partial bool DownloadStartEnabled { get; set; }

    [ObservableProperty] public partial string DownloadUrl { get; set; } = string.Empty;

    [ObservableProperty] public partial Bitmap? GeneratedHead { get; set; }

    [ObservableProperty] public partial int HeadSizeIndex { get; set; }

    [ObservableProperty] public partial bool LaunchCountEnabled { get; set; } = true;

    [ObservableProperty] public partial bool ShortcutEnabled { get; set; } = true;

    [ObservableProperty] public partial string SkinId { get; set; } = string.Empty;

    [ObservableProperty] public partial bool SkinPreviewVisible { get; set; }

    [ObservableProperty] public partial bool SkinSaveEnabled { get; set; } = true;

    partial void OnDownloadUrlChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !string.IsNullOrEmpty(DownloadName))
            return;
        try
        {
            var fileName = Files.GetFileNameFromPath(WebUtility.UrlDecode(value) ?? value);
            if (!string.IsNullOrWhiteSpace(fileName))
                DownloadName = fileName;
        }
        catch (Exception)
        {
            // 非法 URL 交给校验器提示，不在输入阶段打断用户。
        }
    }

    partial void OnHeadSizeIndexChanged(int value)
    {
        RegenerateHead();
    }

    public void RefreshDownloadButtons(bool urlValid, bool folderValid, bool nameValid)
    {
        DownloadStartEnabled = urlValid && folderValid && nameValid;
        DownloadOpenEnabled = folderValid && !string.IsNullOrWhiteSpace(DownloadFolder);
    }

    public void RefreshAchievementButtons(bool itemValid, bool titleValid, bool line1Valid)
    {
        _achievementInputValid = itemValid && titleValid && line1Valid;
        UpdateAchievementButtons();
    }

    private void UpdateAchievementButtons()
    {
        AchievementButtonsEnabled = _achievementInputValid && !AchievementBusy;
    }

    [RelayCommand]
    public static void Clean()
    {
        PageToolsTest.ClearTrash();
    }

    [RelayCommand]
    public static void ShowDailyFortune()
    {
        PageToolsTest.ShowDailyFortune();
    }

    [RelayCommand]
    public async Task ShowLaunchCountAsync()
    {
        LaunchCountEnabled = false;
        try
        {
            await Dialog.ShowAsync(
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
            LauncherCommon.Log(
                ex,
                "显示启动次数失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Tools.Test.Error.OperationFailed"));
        }
        finally
        {
            LaunchCountEnabled = true;
        }
    }

    private static string? EnsureShortcutIcon()
    {
        try
        {
            var iconPath = Path.Combine(
                Paths.SharedLocalData,
                "icons",
                "pcl-community-edition.png");
            Directory.CreateDirectory(Path.GetDirectoryName(iconPath)!);
            if (!File.Exists(iconPath))
            {
                using var source = AssetLoader.Open(
                    new Uri("avares://PCL.Launcher/Assets/Images/icon.png"));
                using var target = File.Create(iconPath);
                source.CopyTo(target);
            }

            return iconPath;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Tools", "导出快捷方式图标失败");
            return null;
        }
    }

    [RelayCommand]
    public async Task CreateShortcutAsync()
    {
        ShortcutEnabled = false;
        try
        {
            var target = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(target))
            {
                HintService.Show(Hints.Error(Lang.Text("Tools.Test.Error.OperationFailed")));
                return;
            }

            var shortcuts = ApplicationRuntime.Current.Platform.Shortcuts;
            var extension = shortcuts.GetPreferredExtension(target);
            var shortcutName = Lang.Text("Tools.Test.Shortcut.FileName", extension);
            var desktopPath = shortcuts.GetDesktopPath(shortcutName);
            var startMenuPath = shortcuts.GetStartMenuPath(shortcutName);
            var desktopName = Lang.Text("Tools.Test.Shortcut.Desktop");
            var startName = Lang.Text("Tools.Test.Shortcut.StartMenu");

            var choice = (await Dialog.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Tools.Test.Shortcut.SelectLocation"),
                    Content = Lang.Text(
                        "Tools.Test.Shortcut.ConfirmMessage",
                        desktopName,
                        desktopPath,
                        startName,
                        startMenuPath),
                    Buttons = DialogButtons.From(
                        DialogButton.Create(Lang.Text("Common.Action.Cancel")),
                        DialogButton.Create(desktopName),
                        DialogButton.Create(startName))
                })).Button;
            if (choice == 1)
                return;

            var shortcutPath = choice == 2 ? desktopPath : startMenuPath;
            var locationName = choice == 2 ? desktopName : startName;
            shortcuts.Create(
                shortcutPath,
                target,
                workingDirectory: AppContext.BaseDirectory,
                description: "PCL CE",
                icon: ApplicationRuntime.Current.Platform.Capabilities.SupportsShortcutIcon
                    ? EnsureShortcutIcon()
                    : null);
            HintService.Show(Hints.Success(Lang.Text("Tools.Test.Shortcut.Created", locationName)));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "创建快捷方式失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.Test.Error.OperationFailed")));
        }
        finally
        {
            ShortcutEnabled = true;
        }
    }

    [RelayCommand]
    public static void Crash()
    {
        throw new InvalidOperationException(Lang.Text("Tools.Test.Crash.ManualCrash"));
    }

    [RelayCommand]
    public async Task SelectDownloadFolder()
    {
        var folder = await SystemDialogs.SelectFolderAsync(
            Lang.Text("Tools.Test.CustomDownload.SaveTo"),
            DownloadFolder);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            DownloadFolder = folder;
            DownloadOpenEnabled = true;
        }
    }

    [RelayCommand]
    public void OpenDownloadFolder()
    {
        try
        {
            var folder = DownloadFolder.Trim();
            if (string.IsNullOrWhiteSpace(folder)) return;
            Directory.CreateDirectory(folder);
            Basics.OpenPath(folder);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "打开下载文件夹失败");
        }
    }

    [RelayCommand]
    public async Task StartDownload()
    {
        var url = DownloadUrl.Trim();
        var folder = DownloadFolder.Trim();
        var name = DownloadName.Trim();
        States.Tool.DownloadFolder = folder;
        await PageToolsTest.StartCustomDownloadAsync(url, name, folder);
        DownloadUrl = "";
        DownloadName = "";
        DownloadStartEnabled = false;
    }

    [RelayCommand]
    public async Task SavePlayerSkinAsync()
    {
        var name = SkinId.Trim();
        if (name.Length < 3)
        {
            HintService.Show(Hints.Info(Lang.Text("Tools.Test.Skin.InvalidId")));
            return;
        }

        SkinSaveEnabled = false;
        HintService.Show(Hints.Info(Lang.Text("Tools.Test.Skin.Fetching")));
        try
        {
            var (path, error) = await Task.Run(() => FetchPlayerSkin(name));
            if (path is null)
            {
                switch (error)
                {
                    case "429":
                        HintService.Show(Hints.Error(Lang.Text("Tools.Test.Skin.TooFrequent")));
                        break;

                    case "invalid":
                        HintService.Show(Hints.Info(Lang.Text("Tools.Test.Skin.InvalidId")));
                        break;

                    default:
                        HintService.Show(
                            Hints.Error(Lang.Text("Tools.Test.Error.OperationFailed")));
                        break;
                }

                return;
            }

            var savePath = await SystemDialogs.SelectSaveFileAsync(
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
        finally
        {
            SkinSaveEnabled = true;
        }
    }

    [RelayCommand]
    public async Task PreviewAchievementAsync()
    {
        if (!AchievementButtonsEnabled)
        {
            HintService.Show(Hints.Error(Lang.Text("Tools.Test.Achievement.InvalidId")));
            return;
        }

        AchievementButtonsEnabled = false;
        AchievementBusy = true;
        UpdateAchievementButtons();
        try
        {
            AchievementLoadingText = Lang.Text("Tools.Test.Achievement.Loading");
            AchievementImagePath = await _RenderAchievementToFileAsync(
                AchievementItem.Trim(), AchievementTitle, AchievementLine1, AchievementLine2);
            AchievementImageVisible = true;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "渲染成就图片失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.Test.Achievement.FetchFailed")));
        }
        finally
        {
            AchievementBusy = false;
            UpdateAchievementButtons();
        }
    }

    [RelayCommand]
    public async Task SaveAchievementAsync()
    {
        if (!AchievementButtonsEnabled)
        {
            HintService.Show(Hints.Error(Lang.Text("Tools.Test.Achievement.InvalidId")));
            return;
        }

        AchievementButtonsEnabled = false;
        AchievementBusy = true;
        UpdateAchievementButtons();
        try
        {
            AchievementLoadingText = Lang.Text("Tools.Test.Achievement.Saving");
            var local = await _RenderAchievementToFileAsync(
                AchievementItem.Trim(), AchievementTitle, AchievementLine1, AchievementLine2);

            var savePath = await SystemDialogs.SelectSaveFileAsync(
                Lang.Text("Tools.Test.Achievement.Save"),
                AchievementImageRenderer.SanitizeFileName(AchievementTitle) + ".png",
                Lang.Text("Tools.Test.Achievement.FileFilter"));
            if (string.IsNullOrWhiteSpace(savePath))
                return;

            File.Copy(local, savePath, true);
            HintService.Show(Hints.Success(Lang.Text("Tools.Test.Achievement.Saved")));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "保存成就图片失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.Test.Achievement.FetchFailed")));
        }
        finally
        {
            AchievementBusy = false;
            UpdateAchievementButtons();
        }
    }

    [RelayCommand]
    public async Task SelectSkinFile()
    {
        var path = await SystemDialogs.SelectFileAsync(
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
                bitmap.Dispose();
                ClearSkinPreview();
                HintService.Show(Hints.Error(Lang.Text("Tools.Test.Avatar.InvalidSize")));
                return;
            }

            _currentSkin?.Dispose();
            _currentSkin = bitmap;
            _skinPath = path;
            RegenerateHead();
            HintService.Show(Hints.Success(Lang.Text("Tools.Test.Avatar.Generated")));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "生成头像失败");
            ClearSkinPreview();
            HintService.Show(
                Hints.Error(Lang.Text("Tools.Test.Avatar.GenerateFailed", ex.Message)));
        }
    }

    [RelayCommand]
    public async Task SaveHead()
    {
        if (GeneratedHead is null)
        {
            HintService.Show(Hints.Error(Lang.Text("Tools.Test.Avatar.SelectFirst")));
            return;
        }

        var savePath = await SystemDialogs.SelectSaveFileAsync(
            Lang.Text("Tools.Test.Avatar.Save"),
            "Head.png");
        if (string.IsNullOrWhiteSpace(savePath))
            return;

        try
        {
            GeneratedHead.Save(savePath, PngBitmapEncoderOptions.Default);
            HintService.Show(Hints.Success(Lang.Text("Tools.Test.Avatar.Saved")));
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "保存头像失败");
            HintService.Show(Hints.Error(Lang.Text("Tools.Test.Error.OperationFailed")));
        }
    }

    private void RegenerateHead()
    {
        if (_currentSkin is null || string.IsNullOrEmpty(_skinPath))
            return;

        try
        {
            GeneratedHead?.Dispose();
            GeneratedHead = CreateHeadBitmap(_currentSkin, GetHeadSize());
            SkinPreviewVisible = true;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Tools", "生成头像失败");
            GeneratedHead?.Dispose();
            GeneratedHead = null;
            HintService.Show(
                Hints.Error(Lang.Text("Tools.Test.Avatar.GenerateFailed", ex.Message)));
            SkinPreviewVisible = false;
        }
    }

    private void ClearSkinPreview()
    {
        _currentSkin?.Dispose();
        _currentSkin = null;
        _skinPath = null;
        GeneratedHead?.Dispose();
        GeneratedHead = null;
        SkinPreviewVisible = false;
    }

    private int GetHeadSize()
    {
        return HeadSizeIndex switch
        {
            1 => 96,
            2 => 128,
            _ => 64
        };
    }

    /// <summary>渲染成就卡片并写入临时文件，返回可供界面显示的路径。</summary>
    private async Task<string> _RenderAchievementToFileAsync(string itemId, string title, string line1,
        string line2)
    {
        var (icon, notFound) = await AchievementImageRenderer.LoadIconAsync(itemId);
        if (notFound && !string.IsNullOrWhiteSpace(itemId))
            HintService.Show(Hints.Info(Lang.Text("Tools.Test.Achievement.IconNotFound", itemId)));

        var font = _ResolveAchievementFont();
        return await Task.Run(() =>
        {
            // 物品 ID 为空时是「无图标」而非「找不到」，画占位立方体更合适
            var iconMissing = notFound && !string.IsNullOrWhiteSpace(itemId);
            using var bitmap = AchievementImageRenderer.Render(icon, iconMissing, itemId, title, line1, line2, font);

            var folder = Path.Combine(Paths.Temp, "Achievement");
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, $"preview_{DateTime.Now:yyyyMMddHHmmssfff}.png");
            bitmap.Save(file);
            return file;
        });
    }

    /// <summary>取成就渲染字体：按族名解析，失败则回退启动器默认字体。</summary>
    private FontFamily _ResolveAchievementFont()
    {
        if (string.IsNullOrWhiteSpace(AchievementFontTag))
            return LaunchFonts.ForCurrentLanguage();
        if (_AchievementFontCache.TryGetValue(AchievementFontTag, out var cached))
            return cached;

        var family = _FindAchievementFont(AchievementFontTag, out var detail);
        if (family is null)
        {
            LogWrapper.Warn("Tools", $"成就字体回退：{AchievementFontTag}（{detail}）");
            family = LaunchFonts.ForCurrentLanguage();
        }

        _AchievementFontCache[AchievementFontTag] = family;
        return family;
    }

    /// <summary>按族名与系统字体族对象两种途径找字体，族名必须真的命中。</summary>
    private static FontFamily? _FindAchievementFont(string tag, out string detail)
    {
        // 不能串回退链：族名写错时回退字体会顶上，看起来「解析成功」其实用的是默认字体
        foreach (var candidate in new[] { tag, _ResolveFamilyName(tag) }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var family = new FontFamily(candidate);
            if (!_IsSameFamily(family, candidate)) continue;
            detail = "按名解析";
            return family;
        }

        // 系统字体族对象自带集合信息，有时比重新构造名字更能命中
        foreach (var installed in FontManager.Current.SystemFonts)
        {
            if (!string.Equals(installed.Name, tag, StringComparison.OrdinalIgnoreCase)) continue;
            if (!_IsSameFamily(installed, installed.Name)) continue;
            detail = "系统字体族对象";
            return installed;
        }

        detail = "未找到同名族";
        return null;
    }

    /// <summary>校验字体是否真的落到目标族，避免回退字体被误判成解析成功。</summary>
    /// <remarks>
    ///     不能要求族名与候选字符串完全相等：下拉里给的是全名（Minecraft AE Pixel），
    ///     而 <see cref="GlyphTypeface.FamilyName" /> 回读的是短名（Minecraft AE），两者互为前缀即为同一族；
    ///     而写法错误时回读到的会是 Segoe UI 之类的默认字体，前缀对不上，正好被排除。
    /// </remarks>
    private static bool _IsSameFamily(FontFamily family, string expected)
    {
        try
        {
            if (!FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out var glyph)) return false;

            var resolved = glyph.FamilyName.Replace(" ", "").ToLowerInvariant();
            var candidate = expected.Replace(" ", "").ToLowerInvariant();
            return resolved.Length > 0 && candidate.Length > 0
                && (resolved == candidate
                    || resolved.StartsWith(candidate, StringComparison.Ordinal)
                    || candidate.StartsWith(resolved, StringComparison.Ordinal));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     把字体下拉里的名字映射回系统字体族的 family 名。
    /// </summary>
    /// <param name="tag">字体选择器给出的名字。</param>
    /// <returns>能用于构造 FontFamily 的 family 名；找不到时原样返回。</returns>
    /// <remarks>
    ///     先按 FontFamily.Name 精确匹配，再退到 FamilyNames 里各语言的本地化名，
    ///     两者都命中不了就认为这个名字本身不可解析，交由上层回退。
    /// </remarks>
    private static string _ResolveFamilyName(string tag)
    {
        foreach (var family in FontManager.Current.SystemFonts)
        {
            if (string.Equals(family.Name, tag, StringComparison.OrdinalIgnoreCase))
                return family.FamilyNames.FirstOrDefault() ?? family.Name;

            if (family.FamilyNames.Any(name => string.Equals(name, tag, StringComparison.OrdinalIgnoreCase)))
                return family.FamilyNames.FirstOrDefault() ?? family.Name;
        }

        return tag;
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

    private static (string? Path, string? Error) FetchPlayerSkin(string name)
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
}