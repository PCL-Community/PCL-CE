using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Models;
using PCL.UI.Controls;
using static PCL.Animations;

namespace PCL.Views.Pages.Launch;

/// <summary>
///     当前账号视图：皮肤头像、玩家名与验证方式，并提供皮肤与披风、修改信息、选择档案三个入口。
///     皮肤来自内置贴图（离线档案）或会话服务器（在线档案），加载失败时显示空头像。
///     修改皮肤/披风与修改密码在在线档案下跳转到对应平台网页；离线档案给出不支持提示。
/// </summary>
public partial class PageLoginProfileSkin : LoginPageBase
{
    private readonly MyIconButton _btnEdit;
    private readonly MyIconButton _btnSelect;
    private readonly MyIconButton _btnSkin;
    private readonly MyCard _panButtons;
    private readonly MySkin _skin;
    private readonly TextBlock _textName;
    private readonly TextBlock _textType;
    private int _loadRevision;

    public PageLoginProfileSkin()
    {
        InitializeComponent();
        _skin = this.FindControl<MySkin>("Skin")!;
        _textName = this.FindControl<TextBlock>("TextName")!;
        _textType = this.FindControl<TextBlock>("TextType")!;
        _panButtons = this.FindControl<MyCard>("PanButtons")!;
        _btnSkin = this.FindControl<MyIconButton>("BtnSkin")!;
        _btnEdit = this.FindControl<MyIconButton>("BtnEdit")!;
        _btnSelect = this.FindControl<MyIconButton>("BtnSelect")!;

        BuildSkinMenu();
        BuildEditMenu();

        _btnSkin.Click += (_, _) => _btnSkin.ContextMenu?.Open(_btnSkin);
        _btnEdit.Click += (_, _) => _btnEdit.ContextMenu?.Open(_btnEdit);
        _btnSelect.Click += (_, _) =>
        {
            ProfileService.Select(null);
            RequestRefresh();
        };
        _skin.CapeRequested += (_, _) => ChangeCape();

        PointerEntered += (_, _) => AniStart(
            AaOpacity(_panButtons, 1d - _panButtons.Opacity, 120),
            "PageLoginProfileSkin Button");
        PointerExited += (_, _) =>
        {
            if (IsPointerOver || IsMenuOpen())
                return;
            HideButtons();
        };

        AttachedToVisualTree += (_, _) => LocalizationService.LanguageChanged += Reload;
        DetachedFromVisualTree += (_, _) => LocalizationService.LanguageChanged -= Reload;
    }

    private void HideButtons()
    {
        AniStart(
            AaOpacity(_panButtons, -_panButtons.Opacity, 120),
            "PageLoginProfileSkin Button");
    }

    private bool IsMenuOpen()
    {
        return _btnSkin.ContextMenu?.IsOpen == true || _btnEdit.ContextMenu?.IsOpen == true;
    }

    public override void Reload()
    {
        var profile = ProfileService.Current;
        if (profile is null)
        {
            _skin.Clear();
            _textName.Text = "";
            _textType.Text = "";
            _btnEdit.IsVisible = false;
            return;
        }

        _textName.Text = profile.UserName;
        _textType.Text = LaunchLoginHelper.GetProfileInfo(profile);
        _btnEdit.IsVisible = profile.ProfileType is not ProfileType.Offline;
        _ = LoadSkinAsync(profile);
    }

    #region 皮肤加载

    private async Task LoadSkinAsync(McProfile profile)
    {
        var revision = ++_loadRevision;
        var address = "";
        try
        {
            if (profile.ProfileType == ProfileType.Offline)
                address = LaunchLoginHelper.GetOfflineSkinAddress(profile.Uuid);
            else
                address = await Task.Run(() =>
                {
                    try
                    {
                        return LaunchLoginHelper.GetOnlineSkinAddress(profile);
                    }
                    catch (Exception ex)
                    {
                        LogWrapper.Warn(ex, "Launch", $"获取在线皮肤失败：{profile.UserName}");
                        return LaunchLoginHelper.GetOfflineSkinAddress(profile.Uuid);
                    }
                });
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Launch", $"加载皮肤失败：{profile.UserName}");
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (revision == _loadRevision)
                _skin.Address = address;
        });
    }

    #endregion

    #region 菜单

    private void BuildSkinMenu()
    {
        var items = new List<MyMenuItem>();

        AddItem("Launch.Account.ProfileSkin.ChangeSkin", "lucide/shirt", ChangeSkin);
        AddItem(
            "Launch.Account.ProfileSkin.SaveSkin",
            "lucide/download",
            () => _ = SaveSkinAsync());
        AddItem("Launch.Account.ProfileSkin.RefreshSkin", "lucide/refresh-cw", RefreshSkin);
        AddItem("Launch.Account.ProfileSkin.ChangeCape", "lucide/shirt", ChangeCape);

        _btnSkin.ContextMenu = new ContextMenu
        {
            ItemsSource = items,
            HorizontalOffset = 10d,
            VerticalOffset = 18d
        };
        _btnSkin.ContextMenu.Closed += (_, _) =>
        {
            if (!IsPointerOver) HideButtons();
        };
        return;

        void AddItem(string headerKey, string svgIcon, Action action)
        {
            var item = new MyMenuItem
            {
                Header = Lang.Text(headerKey),
                SvgIcon = svgIcon
            };
            item.Click += (_, _) =>
            {
                action();
                HideButtons();
            };
            items.Add(item);
        }
    }

    private void BuildEditMenu()
    {
        var items = new List<MyMenuItem>();

        AddItem("Launch.Account.ProfileSkin.EditPassword", "lucide/key-round", EditPassword);
        AddItem("Launch.Account.ProfileSkin.EditUsername", "lucide/user-pen", EditUserName);

        _btnEdit.ContextMenu = new ContextMenu
        {
            ItemsSource = items,
            HorizontalOffset = 10d,
            VerticalOffset = 18d
        };
        _btnEdit.ContextMenu.Closed += (_, _) =>
        {
            if (!IsPointerOver) HideButtons();
        };
        return;

        void AddItem(string headerKey, string svgIcon, Action action)
        {
            var item = new MyMenuItem
            {
                Header = Lang.Text(headerKey),
                SvgIcon = svgIcon
            };
            item.Click += (_, _) =>
            {
                action();
                HideButtons();
            };
            items.Add(item);
        }
    }

    #endregion

    #region 皮肤与披风

    private async Task SaveSkinAsync()
    {
        await _skin.SaveSkinAsync();
    }

    private void RefreshSkin()
    {
        _skin.RefreshSource();
    }

    private void ChangeSkin()
    {
        var profile = ProfileService.Current;
        if (profile is null)
            return;
        if (profile.ProfileType == ProfileType.Microsoft)
            return;

        if (profile.ProfileType is ProfileType.Authlib or ProfileType.YggdrasilConnect
            && !string.IsNullOrWhiteSpace(profile.Server))
            OpenProfilePage(profile.Server, "user/closet");
    }

    private static void ChangeCape()
    {
        var profile = ProfileService.Current;
        if (profile is null)
            return;
        if (profile.ProfileType == ProfileType.Microsoft)
            return;

        if (profile.ProfileType is ProfileType.Authlib or ProfileType.YggdrasilConnect
            && !string.IsNullOrWhiteSpace(profile.Server))
            OpenProfilePage(profile.Server, "user/closet");
    }

    #endregion

    #region 修改信息

    private static void EditPassword()
    {
        var profile = ProfileService.Current;
        if (profile is null)
            return;
        if (profile.ProfileType == ProfileType.Microsoft)
        {
            Basics.OpenPath("https://account.live.com/password/Change");
            return;
        }

        if (profile.ProfileType is ProfileType.Authlib or ProfileType.YggdrasilConnect
            && !string.IsNullOrWhiteSpace(profile.Server))
            OpenProfilePage(profile.Server, "user/profile");
    }

    private void EditUserName()
    {
        var profile = ProfileService.Current;
        if (profile is null)
            return;
        if (profile.ProfileType == ProfileType.Offline)
        {
            LaunchLoginHelper.EditOfflineUserName(profile);
            return;
        }

        if (profile.ProfileType is ProfileType.Authlib or ProfileType.YggdrasilConnect
            && !string.IsNullOrWhiteSpace(profile.Server))
            OpenProfilePage(profile.Server, "user/profile");
    }

    private static void OpenProfilePage(string server, string subPath)
    {
        try
        {
            var address = server;
            var authIndex = address.IndexOf(
                "api/yggdrasil/authserver",
                StringComparison.OrdinalIgnoreCase);
            if (authIndex >= 0)
                address = address[..authIndex];
            if (!address.EndsWith('/'))
                address += "/";
            Basics.OpenPath(address + subPath);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Launch", $"打开档案网页失败：{server}/{subPath}");
        }
    }

    #endregion
}