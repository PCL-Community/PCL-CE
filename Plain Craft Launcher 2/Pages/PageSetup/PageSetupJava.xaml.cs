using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.App;
using PCL.Core.Minecraft;
using PCL.Core.UI;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageSetupJava
{
    private bool IsLoad = false;

    public ModLoader.LoaderTask<bool, List<JavaEntry>> Loader;

    public PageSetupJava()
    {
        InitializeComponent();
        Loader = new ModLoader.LoaderTask<bool, List<JavaEntry>>("JavaPageLoader", Load_GetJavaList);
        Loaded += PageSetupLaunch_Loaded;
    }

    private void PageSetupLaunch_Loaded(object sender, RoutedEventArgs e)
    {
        PageLoaderInit(PanLoad, CardLoad, PanMain, null, Loader, _ => OnLoadFinished(), Load_Input);
    }

    private object Load_Input()
    {
        return false;
    }

    private void Load_GetJavaList(ModLoader.LoaderTask<bool, List<JavaEntry>> loader)
    {
        if (loader.Input) JavaService.JavaManager.ScanJavaAsync().GetAwaiter().GetResult();
        loader.Output = ModJava.Javas.GetSortedJavaList();
    }

    private void OnLoadFinished()
    {
        PanContent.Children.Clear();
        var itemAuto = new MyListItem
        {
            Type = MyListItem.CheckType.RadioBox,
            Title = "自动选择",
            Info = "Java 选择自动挡，依据游戏需要自动选择合适的 Java"
        };
        itemAuto.Check += (sender, e) => Config.Launch.SelectedJava = "";
        PanContent.Children.Add(itemAuto);
        var currentSetJava = Config.Launch.SelectedJava;
        foreach (var entry in ModJava.Javas.GetSortedJavaList())
        {
            var item = ItemBuild(entry);
            PanContent.Children.Add(item);
            if (entry.Installation.JavaExePath == currentSetJava)
                item.SetChecked(true, false, false);
        }

        if (string.IsNullOrEmpty(currentSetJava))
            itemAuto.SetChecked(true, false, false);
    }
    
    private MyListItem ItemBuild(JavaEntry J)
    {
        var item = new MyListItem();
        var versionTypeDesc = J.Installation.IsJre ? "JRE" : "JDK";
        var versionNameDesc = J.Installation.MajorVersion.ToString();
        item.Title = $"{versionTypeDesc} {versionNameDesc}";

        item.Info = J.Installation.JavaFolder;
        var displayTags = new List<string>();
        var displayBits = J.Installation.Is64Bit ? "64 Bit" : "32 Bit";
        displayTags.Add(displayBits);
        var DisplayBrand = J.Installation.Brand.ToString();
        displayTags.Add(DisplayBrand);
        item.Tags = displayTags;

        item.Type = MyListItem.CheckType.RadioBox;
        item.Check += (sender, e) =>
        {
            if (!J.Installation.IsStillAvailable)
            {
                ModMain.Hint("此 Java 不可用，请刷新列表");
                return;
            }

            if (J.IsEnabled)
                Config.Launch.SelectedJava = J.Installation.JavaExePath;
            else
            {
                ModMain.Hint("请先启用此 Java 后再选择其作为默认 Java");
                e.Handled = true;
            }
        };
        var btnOpenFolder = new MyIconButton();
        btnOpenFolder.Logo = ModBase.Logo.IconButtonOpen;
        btnOpenFolder.ToolTip = Lang.Text("Common.Action.Open");
        btnOpenFolder.Click += (sender, e) =>
        {
            if (!J.Installation.IsStillAvailable)
            {
                ModMain.Hint("此 Java 不可用，请刷新列表");
                return;
            }

            ModBase.OpenExplorer(J.Installation.JavaFolder);
        };
        var btnInfo = new MyIconButton();
        btnInfo.Logo = ModBase.Logo.IconButtonInfo;
        btnInfo.ToolTip = "详细信息";
        btnInfo.Click += (sender, e) =>
        {
            if (!J.Installation.IsStillAvailable)
            {
                ModMain.Hint("此 Java 不可用，请刷新列表");
                return;
            }

            ModMain.MyMsgBox(
                $"""
                 类型: {versionTypeDesc}
                 版本: {J.Installation.Version.ToString()}
                 架构: {J.Installation.Architecture.ToString()} ({displayBits})
                 品牌: {DisplayBrand}
                 位置: {J.Installation.JavaFolder}
                 """,
                "Java 信息");
        };
        var btnEnableSwitch = new MyIconButton();
        
        item.Buttons = [btnOpenFolder, btnInfo, btnEnableSwitch];

        void UpdateEnableStyle(bool isCurEnable)
        {
            if (!J.Installation.IsStillAvailable)
            {
                ModMain.Hint("此 Java 不可用，请刷新列表");
                return;
            }

            if (isCurEnable)
            {
                item.LabTitle.TextDecorations = null;
                item.LabTitle.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush1");
                btnEnableSwitch.Logo = ModBase.Logo.IconButtonDisable;
                btnEnableSwitch.ToolTip = "禁用此 Java";
            }
            else
            {
                item.LabTitle.TextDecorations = TextDecorations.Strikethrough;
                item.LabTitle.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray4");
                btnEnableSwitch.Logo = ModBase.Logo.IconButtonEnable;
                btnEnableSwitch.ToolTip = "启用此 Java";
            }
        }
        
        btnEnableSwitch.Click += (_, _) =>
        {
            try
            {
                var target = ModJava.Javas.AddOrGet(J.Installation.JavaExePath);
                if (target == null)
                {
                    ModMain.Hint("此 Java 不可用，请刷新列表");
                    return;
                }

                if (target.IsEnabled && Config.Launch.SelectedJava == target.Installation.JavaExePath)
                {
                    ModMain.Hint("请先取消选择此 Java 作为默认 Java 后再禁用");
                    return;
                }

                target.IsEnabled = !target.IsEnabled;
                UpdateEnableStyle(target.IsEnabled);
                ModJava.Javas.SaveConfig();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "调整 Java 启用状态失败", ModBase.LogLevel.Hint);
            }
        };
        UpdateEnableStyle(J.IsEnabled);

        return item;
    }

    private void BtnAdd_Click(object sender, ModBase.RouteEventArgs e)
    {
        var ret = SystemDialogs.SelectFile("Java 程序(java.exe)|java.exe", "选择 Java 程序");
        if (string.IsNullOrEmpty(ret) || !File.Exists(ret))
            return;
        if (ModJava.Javas.Exist(ret))
            ModMain.Hint("Java 已经存在，不用再次添加……");
        else
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                await Task.Run(() =>
                {
                    ModJava.Javas.AddOrGet(ret);
                    ModJava.Javas.SaveConfig();
                });
                if (ModJava.Javas.Exist(ret))
                {
                    ModMain.Hint("已添加 Java！", ModMain.HintType.Finish);
                    Loader.Start(true, true);
                }
                else
                {
                    ModMain.Hint("未能成功将 Java 加入列表中", ModMain.HintType.Critical);
                }
            }));
    }
}
