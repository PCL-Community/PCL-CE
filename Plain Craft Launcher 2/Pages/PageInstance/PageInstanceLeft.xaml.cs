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
    public static ModMinecraft.McInstance Instance = null;

    public PageInstanceLeft()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshModDisabled();
    }

    public void Refresh()
    {
        Refresh(ModMain.FrmMain.PageCurrentSub);
    }

    public void RefreshModDisabled()
    {
        var hide = Config.Preference.Hide;

        if (Instance is not null && Instance.Modable)
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
            var DisableCount = 0;
            if (hide.InstanceSave)
                DisableCount += 1;
            if (hide.InstanceScreenshot)
                DisableCount += 1;
            if (hide.InstanceMod)
                DisableCount += 1;
            if (hide.InstanceResourcePack)
                DisableCount += 1;
            if (hide.InstanceShader)
                DisableCount += 1;
            if (hide.InstanceSchematic)
                DisableCount += 1;
            if (hide.InstanceServer)
                DisableCount += 1;
            if (DisableCount == 7)
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
                ModDownload.DlClientListLoader.Start(IsForceRestart: true);
                ModDownload.DlOptiFineListLoader.Start(IsForceRestart: true);
                ModDownload.DlForgeListLoader.Start(IsForceRestart: true);
                ModDownload.DlNeoForgeListLoader.Start(IsForceRestart: true);
                ModDownload.DlLiteLoaderListLoader.Start(IsForceRestart: true);
                ModDownload.DlFabricListLoader.Start(IsForceRestart: true);
                ModDownload.DlFabricApiLoader.Start(IsForceRestart: true);
                ModDownload.DlQuiltListLoader.Start(IsForceRestart: true);
                ModDownload.DlQSLLoader.Start(IsForceRestart: true);
                ModDownload.DlOptiFabricLoader.Start(IsForceRestart: true);
                ModDownload.DlLabyModListLoader.Start(IsForceRestart: true);
                ItemInstall.Checked = true;
                ModMain.FrmInstanceInstall.GetCurrentInfo();
                break;
            }
            case FormMain.PageSubType.VersionExport:
            {
                if (ModMain.FrmInstanceExport is not null)
                    ModMain.FrmInstanceExport.RefreshAll();
                ItemExport.Checked = true;
                break;
            }
            case FormMain.PageSubType.VersionServer:
            {
                if (ModMain.FrmInstanceServer is not null)
                    ModMain.FrmInstanceServer.RefreshServers();
                ItemServer.Checked = true;
                break;
            }
        }
    }

    public void Reset(object sender, EventArgs e)
    {
        if (ModMain.MyMsgBox("是否要初始化该实例的实例独立设置？该操作不可撤销。", "初始化确认", Button2: Lang.Text("Common.Action.Cancel"), IsWarn: true) == 1)
        {
            if (ModMain.FrmInstanceSetup == null)
                ModMain.FrmInstanceSetup = new PageInstanceSetup();
            ModMain.FrmInstanceSetup.Reset();
            ItemSetup.Checked = true;
        }
    }

    #region 页面切换

    /// <summary>
    ///     当前页面的编号。从 0 开始计算。
    /// </summary>
    public FormMain.PageSubType PageID = FormMain.PageSubType.Default;

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
            ID = PageID;
        switch (ID)
        {
            case FormMain.PageSubType.VersionOverall:
            {
                if (ModMain.FrmInstanceOverall is null)
                    ModMain.FrmInstanceOverall = new PageInstanceOverall();
                return ModMain.FrmInstanceOverall;
            }
            case FormMain.PageSubType.VersionMod:
            {
                if (ModMain.FrmInstanceMod is null)
                    ModMain.FrmInstanceMod = new PageInstanceCompResource(ModComp.CompType.Mod);
                return ModMain.FrmInstanceMod;
            }
            case FormMain.PageSubType.VersionModDisabled:
            {
                if (ModMain.FrmInstanceModDisabled is null)
                    ModMain.FrmInstanceModDisabled = new PageInstanceModDisabled();
                return ModMain.FrmInstanceModDisabled;
            }
            case FormMain.PageSubType.VersionSetup:
            {
                if (ModMain.FrmInstanceSetup == null)
                    ModMain.FrmInstanceSetup = new PageInstanceSetup();
                return ModMain.FrmInstanceSetup;
            }
            case FormMain.PageSubType.VersionWorld:
            {
                if (ModMain.FrmInstanceSaves is null)
                    ModMain.FrmInstanceSaves = new PageInstanceSaves();
                return ModMain.FrmInstanceSaves;
            }
            case FormMain.PageSubType.VersionScreenshot:
            {
                if (ModMain.FrmInstanceScreenshot is null)
                    ModMain.FrmInstanceScreenshot = new PageInstanceScreenshot();
                return ModMain.FrmInstanceScreenshot;
            }
            case FormMain.PageSubType.VersionResourcePack:
            {
                if (ModMain.FrmInstanceResourcePack is null)
                    ModMain.FrmInstanceResourcePack = new PageInstanceCompResource(ModComp.CompType.ResourcePack);
                return ModMain.FrmInstanceResourcePack;
            }
            case FormMain.PageSubType.VersionShader:
            {
                if (ModMain.FrmInstanceShader is null)
                    ModMain.FrmInstanceShader = new PageInstanceCompResource(ModComp.CompType.Shader);
                return ModMain.FrmInstanceShader;
            }
            case FormMain.PageSubType.VersionSchematic:
            {
                if (ModMain.FrmInstanceSchematic is null)
                    ModMain.FrmInstanceSchematic = new PageInstanceCompResource(ModComp.CompType.Schematic);
                return ModMain.FrmInstanceSchematic;
            }
            case FormMain.PageSubType.VersionInstall:
            {
                if (ModMain.FrmInstanceInstall is null)
                    ModMain.FrmInstanceInstall = new PageInstanceInstall();
                return ModMain.FrmInstanceInstall;
            }
            case FormMain.PageSubType.VersionExport:
            {
                if (ModMain.FrmInstanceExport is null)
                    ModMain.FrmInstanceExport = new PageInstanceExport();
                return ModMain.FrmInstanceExport;
            }
            case FormMain.PageSubType.VersionServer:
            {
                if (ModMain.FrmInstanceServer is null)
                    ModMain.FrmInstanceServer = new PageInstanceServer();
                return ModMain.FrmInstanceServer;
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
        if (PageID == ID)
            return;
        ModAnimation.AniControlEnabled += 1;
        try
        {
            PageChangeRun((MyPageRight)PageGet(ID));
            PageID = ID;
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
        ModMain.FrmMain.PageRight = Target;
        ((MyPageRight)ModMain.FrmMain.PanMainRight.Child).PageOnExit();
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() =>
            {
                ((MyPageRight)ModMain.FrmMain.PanMainRight.Child).PageOnForceExit();
                ModMain.FrmMain.PanMainRight.Child = ModMain.FrmMain.PageRight;
                ModMain.FrmMain.PageRight.Opacity = 0d;
            }, 130),
            ModAnimation.AaCode(() =>
            {
                // 延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                ModMain.FrmMain.PageRight.Opacity = 1d;
                ModMain.FrmMain.PageRight.PageOnEnter();
            }, 30, true)
        }, "PageLeft PageChange");
    }

    #endregion
}
