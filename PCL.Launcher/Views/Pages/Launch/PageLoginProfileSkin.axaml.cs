using System.ComponentModel;
using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.UI.Controls;
using static PCL.Animations;

namespace PCL.Views.Pages.Launch;

/// <summary>
///     当前账号视图：皮肤头像、玩家名与验证方式，并提供皮肤与披风、修改信息、选择档案三个入口。
///     档案信息与菜单命令由 <see cref="PageLoginProfileSkinViewModel" /> 负责，页面保留菜单、
///     悬停动画与皮肤控件交互。
/// </summary>
public partial class PageLoginProfileSkin : LoginPageBase
{
    private readonly MyIconButton _btnEdit;
    private readonly MyIconButton _btnSelect;
    private readonly MyIconButton _btnSkin;
    private readonly MyCard _panButtons;
    private readonly MySkin _skin;
    private MyMenuItem? _menuSave;

    public PageLoginProfileSkin()
    {
        InitializeComponent();
        DataContext = ViewModel;
        _skin = this.FindControl<MySkin>("Skin")!;
        _panButtons = this.FindControl<MyCard>("PanButtons")!;
        _btnSkin = this.FindControl<MyIconButton>("BtnSkin")!;
        _btnEdit = this.FindControl<MyIconButton>("BtnEdit")!;
        _btnSelect = this.FindControl<MyIconButton>("BtnSelect")!;

        BuildSkinMenu();
        BuildEditMenu();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.SaveSkinRequested += () => _ = _skin.SaveSkinAsync();
        ViewModel.RefreshSkinRequested += () => _skin.RefreshSource();
        ViewModel.RefreshRequested += RequestRefresh;

        _btnSkin.Click += (_, _) => _btnSkin.ContextMenu?.Open(_btnSkin);
        _btnEdit.Click += (_, _) => _btnEdit.ContextMenu?.Open(_btnEdit);
        _btnSelect.Click += (_, _) => ViewModel.SelectProfileCommand.Execute(null);
        _skin.CapeRequested += (_, _) => ViewModel.ChangeCapeCommand.Execute(null);

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

    public PageLoginProfileSkinViewModel ViewModel { get; } = new();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PageLoginProfileSkinViewModel.SkinAddress):
                if (string.IsNullOrEmpty(ViewModel.SkinAddress))
                    _skin.Clear();
                else
                    _skin.Address = ViewModel.SkinAddress;
                break;
            case nameof(PageLoginProfileSkinViewModel.CanSave):
                _menuSave?.IsEnabled = ViewModel.CanSave;
                break;
        }
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
        _ = ViewModel.ReloadAsync();
    }

    #region 菜单

    private void BuildSkinMenu()
    {
        var items = new List<MyMenuItem>();

        AddItem(
            "Launch.Account.ProfileSkin.ChangeSkin",
            "lucide/shirt",
            () => ViewModel.ChangeSkinCommand.Execute(null));
        _menuSave = AddItem(
            "Launch.Account.ProfileSkin.SaveSkin",
            "lucide/download",
            () => ViewModel.SaveSkinCommand.Execute(null));
        _menuSave.IsEnabled = ViewModel.CanSave;
        AddItem(
            "Launch.Account.ProfileSkin.RefreshSkin",
            "lucide/refresh-cw",
            () => ViewModel.RefreshSkinCommand.Execute(null));
        AddItem(
            "Launch.Account.ProfileSkin.ChangeCape",
            "lucide/shirt",
            () => ViewModel.ChangeCapeCommand.Execute(null));

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

        MyMenuItem AddItem(string headerKey, string svgIcon, Action action)
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
            return item;
        }
    }

    private void BuildEditMenu()
    {
        var items = new List<MyMenuItem>();

        AddItem(
            "Launch.Account.ProfileSkin.EditPassword",
            "lucide/key-round",
            () => ViewModel.EditPasswordCommand.Execute(null));
        AddItem(
            "Launch.Account.ProfileSkin.EditUsername",
            "lucide/user-pen",
            () => ViewModel.EditUserNameCommand.Execute(null));

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
}