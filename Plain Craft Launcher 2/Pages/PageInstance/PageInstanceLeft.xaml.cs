using System.Windows;
using System.Windows.Controls;
using PCL.Core.App;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageInstanceLeft : IRefreshable
{
    /// <summary>
    ///     当前显示设置的 MC 实例。
    /// </summary>
    public static ModMinecraft.McInstance instance = null;

    public PageInstanceLeft()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshModDisabled();
    }

    public void Refresh()
    {
        Refresh(ModMain.frmMain.PageCurrentSub);
    }

    public void RefreshModDisabled()
    {
        var hide = Config.Preference.Hide;

        if (instance is not null && instance.Modable)
        {
            ItemMod.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceMod
                ? Visibility.Collapsed
                : Visibility.Visible;
            ItemModDisabled.Visibility = Visibility.Collapsed;
        }
        else
        {
            ItemMod.Visibility = Visibility.Collapsed;
            ItemModDisabled.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceMod
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        // 功能隐藏
        if (!PageSetupUI.HiddenForceShow)
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
            if (disableCount == 7)
                TextResource.Visibility = Visibility.Collapsed;
            else
                TextResource.Visibility = Visibility.Visible;
        }
        else
        {
            TextResource.Visibility = Visibility.Visible;
        }

        ItemInstall.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceEdit
            ? Visibility.Collapsed
            : Visibility.Visible;
        ItemExport.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceExport
            ? Visibility.Collapsed
            : Visibility.Visible;
        ItemWorld.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceSave
            ? Visibility.Collapsed
            : Visibility.Visible;
        ItemScreenshot.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceScreenshot
            ? Visibility.Collapsed
            : Visibility.Visible;
        ItemResourcePack.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceResourcePack
            ? Visibility.Collapsed
            : Visibility.Visible;
        ItemShader.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceShader
            ? Visibility.Collapsed
            : Visibility.Visible;
        ItemSchematic.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceSchematic
            ? Visibility.Collapsed
            : Visibility.Visible;
        ItemServer.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceServer
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void RefreshButton_Click(object sender, EventArgs e) // 由边栏按钮匿名调用
    {
        Refresh((FormMain.PageSubType)ModBase.Val(((MyIconButton)sender).Tag));
    }

    public void Refresh(FormMain.PageSubType SubType)
    {
        switch (SubType)
        {
            case FormMain.PageSubType.VersionMod:
            {
                PageInstanceCompResource.Refresh(ModComp.CompType.Mod);
                break;
            }
            case FormMain.PageSubType.VersionScreenshot:
            {
                var ignore= PageInstanceScreenshot.Refresh();
                break;
            }
            case FormMain.PageSubType.VersionWorld:
            {
                PageInstanceSaves.Refresh();
                break;
            }
            case FormMain.PageSubType.VersionResourcePack:
            {
                PageInstanceCompResource.Refresh(ModComp.CompType.ResourcePack);
                break;
            }
            case FormMain.PageSubType.VersionShader:
            {
                PageInstanceCompResource.Refresh(ModComp.CompType.Shader);
                break;
            }
            case FormMain.PageSubType.VersionSchematic:
            {
                PageInstanceCompResource.Refresh(ModComp.CompType.Schematic);
                break;
            }
            case FormMain.PageSubType.VersionInstall:
            {
                ModDownload.dlClientListLoader.Start(IsForceRestart: true);
                ModDownload.dlOptiFineListLoader.Start(IsForceRestart: true);
                ModDownload.dlForgeListLoader.Start(IsForceRestart: true);
                ModDownload.dlNeoForgeListLoader.Start(IsForceRestart: true);
                ModDownload.dlLiteLoaderListLoader.Start(IsForceRestart: true);
                ModDownload.dlFabricListLoader.Start(IsForceRestart: true);
                ModDownload.dlFabricApiLoader.Start(IsForceRestart: true);
                ModDownload.dlQuiltListLoader.Start(IsForceRestart: true);
                ModDownload.dlQSLLoader.Start(IsForceRestart: true);
                ModDownload.dlOptiFabricLoader.Start(IsForceRestart: true);
                ModDownload.dlLabyModListLoader.Start(IsForceRestart: true);
                ItemInstall.Checked = true;
                ModMain.frmInstanceInstall.GetCurrentInfo();
                break;
            }
            case FormMain.PageSubType.VersionExport:
            {
                if (ModMain.frmInstanceExport is not null)
                    ModMain.frmInstanceExport.RefreshAll();
                ItemExport.Checked = true;
                break;
            }
            case FormMain.PageSubType.VersionServer:
            {
                if (ModMain.frmInstanceServer is not null)
                    ModMain.frmInstanceServer.RefreshServers();
                ItemServer.Checked = true;
                break;
            }
        }
    }

    public void Reset(object sender, EventArgs e)
    {
        if (ModMain.MyMsgBox(Lang.Text("Instance.Left.InitializeSettings.ConfirmMessage"),
                Lang.Text("Instance.Left.InitializeSettings.ConfirmTitle"),
                Button2: Lang.Text("Common.Action.Cancel"),
                IsWarn: true)
            == 1)
        {
            if (ModMain.frmInstanceSetup is null)
                ModMain.frmInstanceSetup = new PageInstanceSetup();
            ModMain.frmInstanceSetup.Reset();
            ItemSetup.Checked = true;
        }
    }

    #region 页面切换

    /// <summary>
    ///     当前页面的编号。从 0 开始计算。
    /// </summary>
    public FormMain.PageSubType pageID = FormMain.PageSubType.Default;

    /// <summary>
    ///     勾选事件改变页面。
    /// </summary>
    private void PageCheck(object sender, ModBase.RouteEventArgs e)
    {
        if (sender is MyListItem item && item.Tag is not null)
            PageChange((FormMain.PageSubType)ModBase.Val(item.Tag));
    }

    public object PageGet(FormMain.PageSubType ID)
    {
        if ((int)ID == -1)
            ID = pageID;
        switch (ID)
        {
            case FormMain.PageSubType.VersionOverall:
            {
                if (ModMain.frmInstanceOverall is null)
                    ModMain.frmInstanceOverall = new PageInstanceOverall();
                return ModMain.frmInstanceOverall;
            }
            case FormMain.PageSubType.VersionMod:
            {
                if (ModMain.frmInstanceMod is null)
                    ModMain.frmInstanceMod = new PageInstanceCompResource(ModComp.CompType.Mod);
                return ModMain.frmInstanceMod;
            }
            case FormMain.PageSubType.VersionModDisabled:
            {
                if (ModMain.frmInstanceModDisabled is null)
                    ModMain.frmInstanceModDisabled = new PageInstanceModDisabled();
                return ModMain.frmInstanceModDisabled;
            }
            case FormMain.PageSubType.VersionSetup:
            {
                if (ModMain.frmInstanceSetup is null)
                    ModMain.frmInstanceSetup = new PageInstanceSetup();
                return ModMain.frmInstanceSetup;
            }
            case FormMain.PageSubType.VersionWorld:
            {
                if (ModMain.frmInstanceSaves is null)
                    ModMain.frmInstanceSaves = new PageInstanceSaves();
                return ModMain.frmInstanceSaves;
            }
            case FormMain.PageSubType.VersionScreenshot:
            {
                if (ModMain.frmInstanceScreenshot is null)
                    ModMain.frmInstanceScreenshot = new PageInstanceScreenshot();
                return ModMain.frmInstanceScreenshot;
            }
            case FormMain.PageSubType.VersionResourcePack:
            {
                if (ModMain.frmInstanceResourcePack is null)
                    ModMain.frmInstanceResourcePack = new PageInstanceCompResource(ModComp.CompType.ResourcePack);
                return ModMain.frmInstanceResourcePack;
            }
            case FormMain.PageSubType.VersionShader:
            {
                if (ModMain.frmInstanceShader is null)
                    ModMain.frmInstanceShader = new PageInstanceCompResource(ModComp.CompType.Shader);
                return ModMain.frmInstanceShader;
            }
            case FormMain.PageSubType.VersionSchematic:
            {
                if (ModMain.frmInstanceSchematic is null)
                    ModMain.frmInstanceSchematic = new PageInstanceCompResource(ModComp.CompType.Schematic);
                return ModMain.frmInstanceSchematic;
            }
            case FormMain.PageSubType.VersionInstall:
            {
                if (ModMain.frmInstanceInstall is null)
                    ModMain.frmInstanceInstall = new PageInstanceInstall();
                return ModMain.frmInstanceInstall;
            }
            case FormMain.PageSubType.VersionExport:
            {
                if (ModMain.frmInstanceExport is null)
                    ModMain.frmInstanceExport = new PageInstanceExport();
                return ModMain.frmInstanceExport;
            }
            case FormMain.PageSubType.VersionServer:
            {
                if (ModMain.frmInstanceServer is null)
                    ModMain.frmInstanceServer = new PageInstanceServer();
                return ModMain.frmInstanceServer;
            }

            default:
            {
                throw new Exception("未知的实例设置子页面种类：" + (int)ID);
            }
        }
    }

    /// <summary>
    ///     切换现有页面。
    /// </summary>
    public void PageChange(FormMain.PageSubType ID)
    {
        if (pageID == ID)
            return;
        ModAnimation.AniControlEnabled += 1;
        try
        {
            PageChangeRun((MyPageRight)PageGet(ID));
            pageID = ID;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "切换分页面失败（ID " + (int)ID + "）", ModBase.LogLevel.Feedback);
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    private static void PageChangeRun(MyPageRight Target)
    {
        ModAnimation.AniStop("FrmMain PageChangeRight"); // 停止主页面的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
        if (Target.Parent is not null)
            Target.SetValue(ContentPresenter.ContentProperty, null);
        ModMain.frmMain.pageRight = Target;
        ((MyPageRight)ModMain.frmMain.PanMainRight.Child).PageOnExit();
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() =>
            {
                ((MyPageRight)ModMain.frmMain.PanMainRight.Child).PageOnForceExit();
                ModMain.frmMain.PanMainRight.Child = ModMain.frmMain.pageRight;
                ModMain.frmMain.pageRight.Opacity = 0d;
            }, 130),
            ModAnimation.AaCode(() =>
            {
                // 延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                ModMain.frmMain.pageRight.Opacity = 1d;
                ModMain.frmMain.pageRight.PageOnEnter();
            }, 30, true)
        }, "PageLeft PageChange");
    }

    #endregion
}
