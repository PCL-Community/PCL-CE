using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.UI.Controls;
using PCL.Views.Pages.Download;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例总览页：顶部展示实例版本条目，随后是实例信息、个性化、文件夹快捷方式与管理动作五张卡片。
///     个性化卡直接读写实例配置（<see cref="States.Instance" />）；管理卡接入启动、文件补全、核心修补与回收站删除等既有业务。
/// </summary>
public partial class PageInstanceOverall : MyPageRight
{
    private MyLoadingStateSimulator? _infoSimulator;
    private bool _isLoad;
    private bool _syncing;

    public PageInstanceOverall()
    {
        InitializeComponent();
        DataContext = ViewModel;
        PanScroll = PanBack;
        ItemDisplayLogoCustom.Tag = Path.Combine("PCL", "Logo.png");

        BtnDisplayRename.Click += (_, _) => _ = ViewModel.RenameAsync();
        BtnDisplayDesc.Click += (_, _) => _ = ViewModel.EditDescriptionAsync();
        BtnDisplayStar.Click += (_, _) => ViewModel.ToggleStarCommand.Execute(null);
        BtnManageRestore.Click += (_, _) => _ = ViewModel.RestoreAsync();
        BtnManageDelete.Click += (_, e) =>
            _ = ViewModel.DeleteAsync(e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        BtnManagePatch.Click += (_, _) => _ = ViewModel.PatchCoreAsync();

        ComboDisplayLogo.SelectionChanged += (_, _) => OnLogoChanged();
        ComboDisplayType.SelectionChanged += (_, _) => _ = OnCardTypeChangedAsync();

        ViewModel.InstanceChanged += Reload;
    }

    public PageInstanceOverallViewModel ViewModel { get; } = new();

    public override void OnNavigatedTo(object? parameter)
    {
        ViewModel.SetInstance(InstancePageHelper.ResolveInstance(parameter));
        RefreshOnNavigate();
    }

    protected override void RefreshLocalization()
    {
        var offset = PanScroll?.Offset.Y ?? 0d;
        Reload();
        Dispatcher.UIThread.Post(() =>
        {
            PanScroll?.Offset = new Vector(PanScroll.Offset.X, offset);
        });
    }

    private void RefreshOnNavigate()
    {
        if (ViewModel.Instance is null)
            return;
        Reload();
        if (_isLoad)
            return;
        _isLoad = true;
        PanMain.Children[0].InvalidateMeasure();
    }

    public void Reload()
    {
        var instance = ViewModel.Instance;
        if (instance is null)
            return;

        _syncing = true;
        try
        {
            ViewModel.RefreshDisplay();
            PanDisplayItem.Children.Clear();
            var item = InstanceVersionItem.Create(instance);
            item.IsHitTestVisible = false;
            PanDisplayItem.Children.Add(item);
            ComboDisplayType.SelectedIndex = ViewModel.CardTypeIndex;
            RefreshIconSelection();
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Overall.Error.OperationFailed", ex);
        }
        finally
        {
            _syncing = false;
        }

        GetInstanceInfo();
    }

    #region 实例信息

    private void GetInstanceInfo()
    {
        if (ViewModel.Instance is null)
            return;

        PanModpack.Children.Clear();
        PanModpack.IsVisible = false;
        PanInfo.Children.Clear();
        PanInfo.IsVisible = false;
        _infoSimulator = new MyLoadingStateSimulator
        {
            LoadingState = LoadingState.Run
        };
        LabInfoLoading.State = _infoSimulator;
        _ = LoadInstanceInfoAsync();
    }

    private async Task LoadInstanceInfoAsync()
    {
        try
        {
            await ViewModel.LoadInfoAsync();
            if (ViewModel.Instance is null)
                return;

            PanModpack.Children.Clear();
            PanInfo.Children.Clear();
            if (ViewModel.ModpackProject is not null)
            {
                var compItem = new MyCompItem(ViewModel.ModpackProject, false, false);
                PanModpack.Children.Add(compItem);
                PanModpack.Children.Add(new TextBlock());
                PanModpack.IsVisible = true;
            }

            foreach (var row in ViewModel.InfoRows)
                PanInfo.Children.Add(
                    new MyListItem
                    {
                        Title = row.Title,
                        Info = row.Info,
                        Logo = row.Logo,
                        Margin = new Thickness(0d, 0d, 2d, 0d)
                    });
            PanInfo.IsVisible = true;
        }
        catch (Exception ex)
        {
            InstancePageHelper.ReportError("Instance.Overall.Error.OperationFailed", ex);
        }
        finally
        {
            _infoSimulator?.LoadingState = LoadingState.Stop;
        }
    }

    #endregion

    #region 个性化

    private void RefreshIconSelection()
    {
        _syncing = true;
        try
        {
            ComboDisplayLogo.SelectedIndex = 0;
            var logo = ViewModel.LogoTag;
            var customTag = Path.Combine("PCL", "Logo.png");
            foreach (var raw in ComboDisplayLogo.Items)
            {
                if (raw is not MyComboBoxItem item)
                    continue;
                var tag = item.Tag as string ?? string.Empty;
                var matches = ReferenceEquals(item, ItemDisplayLogoCustom)
                    ? string.Equals(logo, customTag, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(tag, logo, StringComparison.OrdinalIgnoreCase);
                if (!matches)
                    continue;
                ComboDisplayLogo.SelectedItem = item;
                break;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private async Task OnCardTypeChangedAsync()
    {
        if (_syncing)
            return;
        var index = ComboDisplayType.SelectedIndex;
        if (index < 0)
            return;

        if (!await ViewModel.ApplyCardTypeAsync(index))
        {
            _syncing = true;
            ComboDisplayType.SelectedIndex = ViewModel.CardTypeIndex;
            _syncing = false;
        }
        else
        {
            Reload();
        }
    }

    private void OnLogoChanged()
    {
        if (_syncing)
            return;
        var isCustom = ReferenceEquals(ComboDisplayLogo.SelectedItem, ItemDisplayLogoCustom);
        var tag = (ComboDisplayLogo.SelectedItem as MyComboBoxItem)?.Tag as string ?? string.Empty;
        _ = ApplyLogoAsync(tag, isCustom);
    }

    private async Task ApplyLogoAsync(string tag, bool isCustom)
    {
        if (!await ViewModel.SelectLogoAsync(tag, isCustom))
            RefreshIconSelection();
    }

    #endregion
}