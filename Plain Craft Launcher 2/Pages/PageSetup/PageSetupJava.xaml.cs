using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using PCL.Core.Minecraft;
using PCL.Core.UI;

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
        MyListItem ItemBuilder(JavaEntry J)
        {
            var Item = new MyListItem();
            var VersionTypeDesc = J.Installation.IsJre ? "JRE" : "JDK";
            var VersionNameDesc = J.Installation.MajorVersion.ToString();
            Item.Title = $"{VersionTypeDesc} {VersionNameDesc}";

            Item.Info = J.Installation.JavaFolder;
            var displayTags = new List<string>();
            var DisplayBits = J.Installation.Is64Bit ? "64 Bit" : "32 Bit";
            displayTags.Add(DisplayBits);
            var DisplayBrand = J.Installation.Brand.ToString();
            displayTags.Add(DisplayBrand);
            Item.Tags = displayTags;

            Item.Type = MyListItem.CheckType.RadioBox;
            Item.Check += (sender, e) =>
            {
                if (J.IsEnabled)
                {
                    ModBase.Setup.Set("LaunchArgumentJavaSelect", J.Installation.JavaExePath);
                }
                else
                {
                    ModMain.Hint("请先启用此 Java 后再选择其作为默认 Java");
                    e.Handled = true;
                }
            };
            var BtnOpenFolder = new MyIconButton();
            BtnOpenFolder.Logo = ModBase.Logo.IconButtonOpen;
            BtnOpenFolder.ToolTip = "打开";
            BtnOpenFolder.Click += (sender, e) => ModBase.OpenExplorer(J.Installation.JavaFolder);
            var BtnInfo = new MyIconButton();
            BtnInfo.Logo = ModBase.Logo.IconButtonInfo;
            BtnInfo.ToolTip = "详细信息";
            BtnInfo.Click += (sender, e) =>
                ModMain.MyMsgBox(
                    $"类型: {VersionTypeDesc}" + Constants.vbCrLf + $"版本: {J.Installation.Version.ToString()}" +
                    Constants.vbCrLf + $"架构: {J.Installation.Architecture.ToString()} ({DisplayBits})" +
                    Constants.vbCrLf + $"品牌: {DisplayBrand}" + Constants.vbCrLf + $"位置: {J.Installation.JavaFolder}",
                    "Java 信息");
            var BtnEnableSwitch = new MyIconButton();


            Item.Buttons = new[] { BtnOpenFolder, BtnInfo, BtnEnableSwitch };

            void UpdateEnableStyle(bool IsCurEnable)
            {
                if (IsCurEnable)
                {
                    Item.LabTitle.TextDecorations = null;
                    Item.LabTitle.Foreground = (Brush)ModSecret.AppResources["ColorBrush1"];
                    BtnEnableSwitch.Logo = ModBase.Logo.IconButtonDisable;
                    BtnEnableSwitch.ToolTip = "禁用此 Java";
                }
                else
                {
                    Item.LabTitle.TextDecorations = TextDecorations.Strikethrough;
                    Item.LabTitle.Foreground = (Brush)ModSecret.AppResources["ColorBrushGray4"];
                    BtnEnableSwitch.Logo = ModBase.Logo.IconButtonEnable;
                    BtnEnableSwitch.ToolTip = "启用此 Java";
                }
            }

            ;
            BtnInfo.Click += (sender, e) =>
            {
                try
                {
                    var target = ModJava.Javas.AddOrGet(J.Installation.JavaExePath);
                    if (target.IsEnabled && Operators.ConditionalCompareObjectEqual(
                            ModBase.Setup.Get("LaunchArgumentJavaSelect"), target.Installation.JavaExePath, false))
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

            return Item;
        }

        ;
        PanContent.Children.Clear();
        var ItemAuto = new MyListItem
        {
            Type = MyListItem.CheckType.RadioBox,
            Title = "自动选择",
            Info = "Java 选择自动挡，依据游戏需要自动选择合适的 Java"
        };
        ItemAuto.Check += (sender, e) => ModBase.Setup.Set("LaunchArgumentJavaSelect", "");
        PanContent.Children.Add(ItemAuto);
        var CurrentSetJava = ModBase.Setup.Get("LaunchArgumentJavaSelect");
        foreach (var J in ModJava.Javas.GetSortedJavaList())
        {
            var item = ItemBuilder(J);
            PanContent.Children.Add(item);
            if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(((dynamic)J).Installation.JavaExePath,
                    CurrentSetJava, false)))
                item.SetChecked(true, false, false);
        }

        if (string.IsNullOrEmpty(Conversions.ToString(CurrentSetJava)))
            ItemAuto.SetChecked(true, false, false);
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