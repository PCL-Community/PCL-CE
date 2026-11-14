using Avalonia;
using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Profile.Models;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Launch;

/// <summary>
///     登录档案列表：选择已有档案、删除或复制 UUID，并通过“新建档案”选择验证方式。
///     档案集合与业务命令由 <see cref="PageLoginProfileViewModel" /> 负责，页面只构建列表项。
/// </summary>
public partial class PageLoginProfile : LoginPageBase
{
    private readonly StackPanel _panProfiles;

    public PageLoginProfile()
    {
        InitializeComponent();
        DataContext = ViewModel;
        _panProfiles = this.FindControl<StackPanel>("PanProfiles")!;

        ViewModel.ProfilesUpdated += RebuildList;
        ViewModel.RefreshRequested += RequestRefresh;
        ViewModel.LoginTypeRequested += RequestLoginType;

        AttachedToVisualTree += (_, _) => LocalizationService.LanguageChanged += Reload;
        DetachedFromVisualTree += (_, _) => LocalizationService.LanguageChanged -= Reload;
    }

    public PageLoginProfileViewModel ViewModel { get; } = new();

    public override void Reload()
    {
        _ = ViewModel.ReloadAsync();
    }

    private void RebuildList()
    {
        _panProfiles.Children.Clear();
        foreach (var item in ViewModel.Profiles)
            _panProfiles.Children.Add(CreateProfileItem(item));
    }

    private Control CreateProfileItem(ProfileItem item)
    {
        var profile = item.Profile;
        var listItem = new MyListItem
        {
            Title = item.Username,
            Info = item.Info,
            Type = MyListItem.CheckType.Clickable,
            Tag = profile,
            Margin = new Thickness(8, 2, 10, 2),
            Logo = item.Logo,
            SvgIcon = item.SvgIcon
        };

        var buttons = new List<MyIconButton>();

        if (profile.ProfileType == ProfileType.Offline)
            AddButton(
                "lucide/pencil",
                "Launch.Account.Profile.ChangeUuid",
                () => ViewModel.EditUuidCommand.Execute(item));
        else
            AddButton(
                "lucide/copy",
                "Launch.Account.Profile.CopyUuid",
                () => ViewModel.CopyUuidCommand.Execute(item));

        AddButton(
            "lucide/trash-2",
            "Launch.Account.Profile.Delete",
            () => ViewModel.DeleteProfileCommand.Execute(item));

        listItem.Buttons = buttons;
        listItem.Click += (_, _) => ViewModel.SelectCommand.Execute(item);
        return listItem;

        void AddButton(string svgIcon, string toolTipKey, Action action)
        {
            var button = new MyIconButton
            {
                Height = 22,
                Width = 22,
                SvgIcon = svgIcon,
                Margin = new Thickness(4, 0, 0, 0)
            };
            ToolTip.SetTip(button, Lang.Text(toolTipKey));
            button.Click += (_, _) => action();
            buttons.Add(button);
        }
    }
}