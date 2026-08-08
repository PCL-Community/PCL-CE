using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Skin;
using PCL.Core.UI;

namespace PCL;

/// <summary>
///     离线账户的皮肤设置对话框，参照 HMCL 的 OfflineAccountSkinPane 实现。
/// </summary>
public partial class OfflineSkinDialog
{
    private string _skinPath = "";
    private string _capePath = "";
    private TextureModel _model = TextureModel.Wide;

    public OfflineSkinDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadCurrentSkin();
    }

    /// <summary>
    ///     当前选中的皮肤类型，取自 ComboType 选中项的 Tag（XAML 中已与 SkinType 枚举名对应）。
    /// </summary>
    private SkinType CurrentType
    {
        get
        {
            if (ComboType.SelectedItem is MyComboBoxItem { Tag: string tag } &&
                Enum.TryParse(tag, out SkinType type))
                return type;
            return SkinType.Default;
        }
    }

    /// <summary>
    ///     按 Tag 查找下拉框中的项序号，未找到返回 0。
    /// </summary>
    private static int FindComboIndex(ComboBox combo, string tag)
    {
        for (var i = 0; i < combo.Items.Count; i++)
            if (combo.Items[i] is MyComboBoxItem { Tag: string itemTag } && itemTag == tag)
                return i;
        return 0;
    }

    /// <summary>
    ///     打开时回显当前档案已保存的皮肤配置。
    /// </summary>
    private void LoadCurrentSkin()
    {
        var skin = ModProfile.selectedProfile?.Skin;
        _model = skin?.Model ?? TextureModel.Wide;
        _skinPath = skin?.LocalSkinPath ?? "";
        _capePath = skin?.LocalCapePath ?? "";

        ComboType.SelectedIndex = skin is null ? 0 : FindComboIndex(ComboType, skin.Type.ToString());
        ComboModel.SelectedIndex = _model == TextureModel.Slim ? 1 : 0;
        TextSkinPath.Text = _skinPath;
        TextCapePath.Text = _capePath;
        TextCslApi.Text = skin?.CslApi ?? "";

        UpdatePanels();
        UpdateConfirmEnabled();
    }

    /// <summary>
    ///     根据当前皮肤类型显示或隐藏对应的选项区域。
    /// </summary>
    private void UpdatePanels()
    {
        PanLocalFile.Visibility = CurrentType == SkinType.LocalFile ? Visibility.Visible : Visibility.Collapsed;
        PanCslApi.Visibility = CurrentType == SkinType.CustomSkinLoaderApi ? Visibility.Visible : Visibility.Collapsed;
        PanLittleSkin.Visibility = CurrentType == SkinType.LittleSkin ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    ///     CSL API 模式下需要填写有效的接口地址才能确定。
    /// </summary>
    private void UpdateConfirmEnabled()
    {
        BtnConfirm.IsEnabled = CurrentType != SkinType.CustomSkinLoaderApi ||
                               Uri.TryCreate(TextCslApi.Text.Trim(), UriKind.Absolute, out _);
    }

    private void BtnSelectSkin_Click(object sender, MouseButtonEventArgs e)
    {
        var fileName = SystemDialogs.SelectFile(Lang.Text("Launch.Skin.FileDialog.Filter"),
            Lang.Text("Launch.Skin.FileDialog.Title"));
        if (string.IsNullOrEmpty(fileName))
            return;
        try
        {
            var image = new MyBitmap(fileName);
            // 允许高分辨率皮肤：宽度需为 64 的倍数，高度为宽度的一半或与宽度相同
            if (image.pic.Width % 64 != 0 ||
                !(image.pic.Height == image.pic.Width / 2 || image.pic.Height == image.pic.Width))
            {
                HintService.Hint(Lang.Text("Launch.Skin.InvalidSize"), HintType.Error);
                return;
            }

            // 依据手臂区域自动判定模型（Steve 宽臂 / Alex 细臂），用户之后仍可手动调整
            try
            {
                var isSlim = new NormalizedSkin(image.pic).IsSlim();
                _model = isSlim ? TextureModel.Slim : TextureModel.Wide;
                ComboModel.SelectedIndex = isSlim ? 1 : 0;
            }
            catch (InvalidSkinException)
            {
                // 尺寸已通过上方校验，此处仅作兜底，忽略即可
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Launch.Skin.File.Error"), ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Launch.Skin.File.Error"));
            return;
        }

        _skinPath = fileName;
        TextSkinPath.Text = fileName;
    }

    private void BtnSelectCape_Click(object sender, MouseButtonEventArgs e)
    {
        var fileName = SystemDialogs.SelectFile(Lang.Text("Launch.Skin.FileDialog.Filter"),
            Lang.Text("Launch.Skin.FileDialog.Title"));
        if (string.IsNullOrEmpty(fileName))
            return;
        try
        {
            _ = new MyBitmap(fileName); // 校验文件可以正常读取
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Launch.Skin.File.Error"), ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Launch.Skin.File.Error"));
            return;
        }

        _capePath = fileName;
        TextCapePath.Text = fileName;
    }

    private void BtnOpenLittleSkin_Click(object sender, MouseButtonEventArgs e)
    {
        ModBase.OpenWebsite("https://littleskin.cn");
    }

    private void BtnConfirm_Click(object sender, MouseButtonEventArgs e)
    {
        if (CurrentType == SkinType.CustomSkinLoaderApi &&
            !Uri.TryCreate(TextCslApi.Text.Trim(), UriKind.Absolute, out _))
            return;

        var cslApi = CurrentType == SkinType.CustomSkinLoaderApi ? TextCslApi.Text.Trim() : null;
        var skin = new Skin(
            CurrentType,
            cslApi,
            _model,
            string.IsNullOrEmpty(_skinPath) ? null : _skinPath,
            string.IsNullOrEmpty(_capePath) ? null : _capePath);

        ModProfile.selectedProfile.Skin = skin;
        ModProfile.SaveProfile();
        HintService.Hint(Lang.Text("Launch.OfflineSkin.Saved"), HintType.Success);
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, MouseButtonEventArgs e)
    {
        DialogResult = false;
    }

    private void ComboType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePanels();
        UpdateConfirmEnabled();
    }

    private void ComboModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _model = ComboModel.SelectedIndex == 1 ? TextureModel.Slim : TextureModel.Wide;
    }

    private void TextCslApi_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateConfirmEnabled();
    }
}
