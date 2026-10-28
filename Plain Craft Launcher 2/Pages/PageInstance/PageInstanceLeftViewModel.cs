using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>实例左侧栏页的视图模型。</summary>
public partial class PageInstanceLeftViewModel : ViewModelBase
{
    [ObservableProperty] private bool _itemModVisible = true;

    [ObservableProperty] private bool _itemModDisabledVisible = true;

    [ObservableProperty] private bool _textResourceVisible = true;

    [ObservableProperty] private bool _itemInstallVisible = true;

    [ObservableProperty] private bool _itemExportVisible = true;

    [ObservableProperty] private bool _itemWorldVisible = true;

    [ObservableProperty] private bool _itemScreenshotVisible = true;

    [ObservableProperty] private bool _itemResourcePackVisible = true;

    [ObservableProperty] private bool _itemShaderVisible = true;

    [ObservableProperty] private bool _itemSchematicVisible = true;

    [ObservableProperty] private bool _itemServerVisible = true;

    public void RefreshModDisabled(McInstance instance)
    {
        var hide = Config.Preference.Hide;

        if (instance is not null && instance.Modable)
        {
            ItemModVisible = PageSetupUI.HiddenForceShow || !hide.InstanceMod;
            ItemModDisabledVisible = false;
        }
        else
        {
            ItemModVisible = false;
            ItemModDisabledVisible = PageSetupUI.HiddenForceShow || !hide.InstanceMod;
        }

        if (PageSetupUI.HiddenForceShow)
        {
            TextResourceVisible = true;
        }
        else
        {
            var disableCount = 0;
            if (hide.InstanceSave)
                disableCount += 1;
            if (hide.InstanceScreenshot)
                disableCount += 1;
            if (hide.InstanceMod)
                disableCount += 1;
            if (hide.InstanceResourcePack)
                disableCount += 1;
            if (hide.InstanceShader)
                disableCount += 1;
            if (hide.InstanceSchematic)
                disableCount += 1;
            if (hide.InstanceServer)
                disableCount += 1;
            TextResourceVisible = disableCount != 7;
        }

        ItemInstallVisible = PageSetupUI.HiddenForceShow || !hide.InstanceEdit;
        ItemExportVisible = PageSetupUI.HiddenForceShow || !hide.InstanceExport;
        ItemWorldVisible = PageSetupUI.HiddenForceShow || !hide.InstanceSave;
        ItemScreenshotVisible = PageSetupUI.HiddenForceShow || !hide.InstanceScreenshot;
        ItemResourcePackVisible = PageSetupUI.HiddenForceShow || !hide.InstanceResourcePack;
        ItemShaderVisible = PageSetupUI.HiddenForceShow || !hide.InstanceShader;
        ItemSchematicVisible = PageSetupUI.HiddenForceShow || !hide.InstanceSchematic;
        ItemServerVisible = PageSetupUI.HiddenForceShow || !hide.InstanceServer;
    }

    public static string GetUnknownSubPageMessage(FormMain.PageSubType id)
    {
        return "未知的实例设置子页面种类：" + (int)id;
    }

    public static string GetSwitchFailedMessage(FormMain.PageSubType id)
    {
        return "切换分页面失败（ID " + (int)id + "）";
    }

    public static string GetResetConfirmMessage()
    {
        return Lang.Text("Instance.Left.InitializeSettings.ConfirmMessage");
    }

    public static string GetResetConfirmTitle()
    {
        return Lang.Text("Instance.Left.InitializeSettings.ConfirmTitle");
    }

    public static string GetCancelText()
    {
        return Lang.Text("Common.Action.Cancel");
    }
}
