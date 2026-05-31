using System.Windows;

namespace PCL;

public partial class PageOtherHelpDetail : IRefreshable
{
    public ModMain.HelpEntry entry;

    public PageOtherHelpDetail()
    {
        InitializeComponent();
        Loaded += PageOtherHelpDetail_Loaded;
    }

    public void Refresh()
    {
        Init(new ModMain.HelpEntry(entry.RawPath));
    }

    private void PageOtherHelpDetail_Loaded(object sender, RoutedEventArgs e)
    {
        PanBack.ScrollToTop();
    }

    /// <summary>
    ///     根据特定帮助项初始化页面 UI，返回是否成功加载。
    /// </summary>
    public bool Init(ModMain.HelpEntry entry)
    {
        var content = entry.XamlContent ?? "";
        if (string.IsNullOrEmpty(content))
            throw new Exception("帮助 xaml 文件为空");
        try
        {
            // 修改时应同时修改 PageLaunchRight.LoadContent
            content = ModMain.ArgumentReplace(content);
            if (content.Contains("xmlns"))
                content = content.RegexReplace("xmlns[^\"']*(\"|')[^\"']*(\"|')", "").Replace("xmlns", ""); // 禁止声明命名空间
            content =
                $"<StackPanel xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:sys=\"clr-namespace:System;assembly=System.Runtime\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:local=\"clr-namespace:PCL;assembly=Plain Craft Launcher 2\">{content}</StackPanel>";
            this.entry = entry;
            PanCustom.Children.Clear();
            PanCustom.Children.Add((UIElement)ModBase.GetObjectFromXML(content));
            return true;
        }
        catch (Exception ex)
        {
            ModBase.Log($"[System] 自定义信息内容：\r\n{content}");
            ModBase.Log(ex, "加载帮助 XAML 文件失败", ModBase.LogLevel.Msgbox);
            return false;
        }
    }
}