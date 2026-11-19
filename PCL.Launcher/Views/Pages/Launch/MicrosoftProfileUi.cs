using System.Net;
using Avalonia.Media.Imaging;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Launch;

/// <summary>Microsoft 档案的皮肤、披风与玩家名操作界面流程。</summary>
public static class MicrosoftProfileUi
{
    private static readonly SemaphoreSlim SkinGate = new(1, 1);
    private static readonly SemaphoreSlim CapeGate = new(1, 1);
    private static readonly SemaphoreSlim NameGate = new(1, 1);

    /// <summary>更换 Microsoft 档案皮肤。</summary>
    public static async Task ChangeSkinAsync(McProfile profile)
    {
        if (!await SkinGate.WaitAsync(0).ConfigureAwait(true))
        {
            HintService.Show(Hints.Info(Lang.Text("Launch.Skin.Change.Busy")));
            return;
        }

        try
        {
            var current = FindCurrent(profile);
            if (current is null) return;

            var file = await SystemDialogs
                .SelectFileAsync(
                    Lang.Text("Launch.Skin.FileDialog.Filter"),
                    Lang.Text("Launch.Skin.FileDialog.Title"))
                .ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(file)) return;

            var skin = await ReadSkinAsync(file).ConfigureAwait(true);
            if (skin is null) return;

            var model = await SelectSkinModelAsync().ConfigureAwait(true);
            if (model is null) return;

            HintService.Show(Hints.Info(Lang.Text("Launch.Skin.Change.Starting")));
            var result = await MicrosoftAccountService
                .UploadSkinAsync(
                    current,
                    skin.Value.Content,
                    Path.GetFileName(file),
                    model.Value)
                .ConfigureAwait(true);

            if (!string.IsNullOrWhiteSpace(result.ActiveSkinUrl))
                await RefreshSkinCacheAsync(current, result.ActiveSkinUrl).ConfigureAwait(true);

            HintService.Show(Hints.Success(Lang.Text("Launch.Skin.ChangeSuccess")));
            ProfileChanged?.Invoke();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            HintService.Show(Hints.Error(Lang.Text("Launch.Skin.Change.LoginFailed")));
            LogWrapper.Warn(ex, "Launch", "Microsoft 皮肤上传登录失效");
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Launch", "Microsoft 皮肤上传失败");
            HintService.Show(Hints.Error(Lang.Text("Launch.Account.Profile.Error.ChangeSkin")));
        }
        finally
        {
            SkinGate.Release();
        }
    }

    /// <summary>选择并应用 Microsoft 披风。</summary>
    public static async Task ChangeCapeAsync(McProfile profile)
    {
        if (!await CapeGate.WaitAsync(0).ConfigureAwait(true))
        {
            HintService.Show(Hints.Info(Lang.Text("Launch.Skin.Cape.Changing")));
            return;
        }

        try
        {
            var current = FindCurrent(profile);
            if (current is null) return;

            HintService.Show(Hints.Info(Lang.Text("Launch.Skin.Cape.FetchingList")));
            var capes = await MicrosoftAccountService.GetCapesAsync(current).ConfigureAwait(true);

            List<DialogOption> options =
            [
                new()
                {
                    Text = Lang.Text("Launch.Skin.Cape.None"),
                    IsChecked = capes.All(cape => !cape.IsActive),
                    Tag = null
                },
                .. capes.Select(cape => new DialogOption
                {
                    Text = MySkinViewModel.GetCapeDisplayName(cape.Alias),
                    Image = cape.Url,
                    ImageScale = 0.8d,
                    IsChecked = cape.IsActive,
                    Tag = cape.Id
                })
            ];

            var result = await DialogService
                .Instance.ShowAsync(
                    new SelectDialogRequest
                    {
                        Title = Lang.Text("Launch.Skin.Cape.SelectTitle"),
                        Options = options,
                        Buttons = DialogButtons.OkCancel,
                        HighLight = false
                    })
                .ConfigureAwait(true);
            if (!result.IsConfirmed
                || result.SelectedIndex is not { } selectedIndex
                || selectedIndex < 0)
                return;

            var capeId = selectedIndex == 0
                ? null
                : options[selectedIndex].Tag?.ToString();
            await MicrosoftAccountService.SetCapeAsync(current, capeId).ConfigureAwait(true);
            HintService.Show(Hints.Success(Lang.Text("Launch.Skin.Cape.ChangeSuccess")));
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            HintService.Show(Hints.Error(Lang.Text("Launch.Skin.Cape.LoginFailed")));
            LogWrapper.Warn(ex, "Launch", "Microsoft 披风操作登录失效");
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Launch", "Microsoft 披风操作失败");
            HintService.Show(Hints.Error(Lang.Text("Launch.Skin.Cape.ChangeFailed")));
        }
        finally
        {
            CapeGate.Release();
        }
    }

    /// <summary>修改 Microsoft 档案玩家名。</summary>
    public static async Task EditNameAsync(McProfile profile)
    {
        if (!await NameGate.WaitAsync(0).ConfigureAwait(true))
            return;

        try
        {
            var current = FindCurrent(profile);
            if (current is null) return;

            var input = await DialogService
                .Instance.ShowAsync(
                    new InputDialogRequest
                    {
                        Title = Lang.Text("Launch.Account.Profile.EditPlayerId.Title"),
                        Content = Lang.Text("Launch.Account.Profile.EditPlayerId.MicrosoftWarning"),
                        DefaultValue = current.UserName,
                        HintText = Lang.Text("Launch.Account.Profile.EditPlayerId.Hint"),
                        Buttons = DialogButtons.OkCancel,
                        HighLight = false
                    })
                .ConfigureAwait(true);
            if (!input.IsConfirmed) return;

            var name = (input.InputText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                HintService.Show(
                    Hints.Error(Lang.Text("Launch.Account.Profile.EditPlayerId.Empty")));
                return;
            }

            if (name.Length is < 3 or > 16
                || !name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            {
                HintService.Show(
                    Hints.Error(Lang.Text("Launch.Account.Profile.EditPlayerId.NotAllowed")));
                return;
            }

            var confirm = await DialogService
                .Instance.ShowAsync(
                    new TextDialogRequest
                    {
                        Title = Lang.Text("Launch.Account.Profile.EditPlayerId.Confirm.Title"),
                        Content = Lang.Text("Launch.Account.Profile.EditPlayerId.Confirm.Message"),
                        Buttons = DialogButtons.ContinueCancel,
                        IsWarn = true
                    })
                .ConfigureAwait(true);
            if (!confirm.IsConfirmed) return;

            try
            {
                var status = await MicrosoftAccountService
                    .CheckNameAvailableAsync(current, name)
                    .ConfigureAwait(true);
                if (status is "DUPLICATE" or "NOT_ALLOWED")
                {
                    HintService.Show(
                        Hints.Error(
                            Lang.Text(
                                status == "DUPLICATE"
                                    ? "Launch.Account.Profile.EditPlayerId.Duplicate"
                                    : "Launch.Account.Profile.EditPlayerId.NotAllowed")));
                    return;
                }

                var updatedName = await MicrosoftAccountService
                    .RenameAsync(current, name)
                    .ConfigureAwait(true);
                var updated = current.Clone();
                updated.UserName = updatedName;
                ProfileService.Update(current, updated);
                ProfileService.Select(updated);
                HintService.Show(
                    Hints.Success(
                        Lang.Text("Launch.Account.Profile.EditPlayerId.Success", updatedName)));
                ProfileChanged?.Invoke();
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
                HintService.Show(
                    Hints.Error(Lang.Text("Launch.Account.Profile.EditPlayerId.Cooldown")));
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Launch", "Microsoft 玩家名修改失败");
            HintService.Show(Hints.Error(Lang.Text("Launch.Account.Profile.Error.ChangeId")));
        }
        finally
        {
            NameGate.Release();
        }
    }

    /// <summary>档案信息已变化，页面可据此刷新。</summary>
    public static event Action? ProfileChanged;

    private static McProfile? FindCurrent(McProfile profile)
    {
        return ProfileService.Profiles.FirstOrDefault(item => item.ProfileId == profile.ProfileId)
            ?? ProfileService.Current;
    }

    private static async Task<(byte[] Content, string? Name)?> ReadSkinAsync(string file)
    {
        try
        {
            using var bitmap = new Bitmap(file);
            var size = bitmap.PixelSize;
            if (size.Width != 64 || size.Height is not (32 or 64))
            {
                HintService.Show(Hints.Error(Lang.Text("Launch.Skin.InvalidSize")));
                return null;
            }

            var raw = await File.ReadAllBytesAsync(file).ConfigureAwait(true);
            if (raw.Length > 24 * 1024)
            {
                HintService.Show(
                    Hints.Error(
                        Lang.Text(
                            "Launch.Skin.FileTooLarge",
                            (raw.Length / 1024d).ToString("N2"))));
                return null;
            }

            // Minecraft Services only accepts PNG; normalize other supported input formats.
            if (Path.GetExtension(file).Equals(".png", StringComparison.OrdinalIgnoreCase))
                return (raw, Path.GetFileName(file));

            using var stream = new MemoryStream();
            bitmap.Save(stream);
            return (stream.ToArray(), "skin.png");
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Launch", "读取皮肤文件失败");
            HintService.Show(Hints.Error(Lang.Text("Launch.Skin.File.Error")));
            return null;
        }
    }

    private static async Task<bool?> SelectSkinModelAsync()
    {
        var result = await DialogService
            .Instance.ShowAsync(
                new SelectDialogRequest
                {
                    Title = Lang.Text("Launch.Skin.Model.SelectTitle"),
                    Options =
                    [
                        new DialogOption
                        {
                            Text = Lang.Text("Launch.Skin.Model.Steve")
                        },
                        new DialogOption
                        {
                            Text = Lang.Text("Launch.Skin.Model.Alex")
                        },
                        new DialogOption
                        {
                            Text = Lang.Text("Common.Option.IDontKnow")
                        }
                    ],
                    Buttons = DialogButtons.OkCancel,
                    HighLight = false
                })
            .ConfigureAwait(true);

        if (!result.IsConfirmed) return null;
        return result.SelectedIndex switch
        {
            0 => false,
            1 => true,
            _ => null
        };
    }

    private static async Task RefreshSkinCacheAsync(McProfile profile, string activeSkinUrl)
    {
        try
        {
            var cacheFolder = Path.Combine(LauncherPaths.Temp, "Cache", "Skin");
            Directory.CreateDirectory(cacheFolder);
            var cacheFile = Path.Combine(
                cacheFolder,
                LauncherCommon.GetHash(profile.Uuid + profile.ProfileType) + ".png");
            if (File.Exists(cacheFile)) File.Delete(cacheFile);

            var downloaded = await MyImage.DownloadImageAsync(activeSkinUrl).ConfigureAwait(true);
            if (!string.IsNullOrWhiteSpace(downloaded) && File.Exists(downloaded))
                File.Copy(downloaded, cacheFile, true);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Launch", "刷新 Microsoft 皮肤缓存失败");
        }
    }
}