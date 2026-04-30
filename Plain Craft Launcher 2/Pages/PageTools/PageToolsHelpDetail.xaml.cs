using System.Windows;

namespace PCL;

public partial class PageOtherHelpDetail : IRefreshable
{
    public ModMain.HelpEntry Entry;

    public PageOtherHelpDetail()
    {
        InitializeComponent();
        Loaded += PageOtherHelpDetail_Loaded;
    }

    public void Refresh()
    {
        Init(new ModMain.HelpEntry(Entry.RawPath));
    }

    private void PageOtherHelpDetail_Loaded(object sender, RoutedEventArgs e)
    {
        PanBack.ScrollToTop();
    }

    /// <summary>
    ///     根据特定帮助项初始化页面 UI，返回是否成功加载。
    /// </summary>
    public bool Init(ModMain.HelpEntry Entry)
    {
        var Content = Entry.XamlContent ?? "";
        if (string.IsNullOrEmpty(Content))
            throw new Exception("帮助 xaml 文件为空");
        try
        {
            // 修改时应同时修改 PageLaunchRight.LoadContent
            Content = ModMain.ArgumentReplace(Content);
            if (Content.Contains("xmlns"))
                Content = Content.RegexReplace("xmlns[^\"']*(\"|')[^\"']*(\"|')", "").Replace("xmlns", ""); // 禁止声明命名空间
            Content =
                $"<StackPanel xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:sys=\"clr-namespace:System;assembly=System.Runtime\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:local=\"clr-namespace:PCL;assembly=Plain Craft Launcher 2\">{Content}</StackPanel>";
            this.Entry = Entry;
            PanCustom.Children.Clear();
            PanCustom.Children.Add((UIElement)ModBase.GetObjectFromXML(Content));
            return true;
        }
        catch (Exception ex)
        {
            ModBase.Log($"[System] 自定义信息内容：\r\n{Content}");
            ModBase.Log(ex, "加载帮助 XAML 文件失败", ModBase.LogLevel.Msgbox);
            return false;
        }
    }
}