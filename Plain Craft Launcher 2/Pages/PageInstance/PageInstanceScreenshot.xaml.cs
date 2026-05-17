using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualBasic.FileIO;
using PCL.Core.App;
using SearchOption = System.IO.SearchOption;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageInstanceScreenshot : IRefreshable
{
    private bool _AppendLock;
    private int _Offset;

    private List<string> FileList = new();

    private bool IsLoad;
    private string ScreenshotPath;

    public PageInstanceScreenshot()
    {
        InitializeComponent();
        Loaded += PageSetupLaunch_Loaded;
        PanBack.ScrollChanged += RequireAppend;
        BtnOpenFolder.Click += BtnOpenFolder_Click;
        BtnOpenFolderTop.Click += BtnOpenFolder_Click;
    }

    void IRefreshable.Refresh()
    {
        RefreshSelf();
    }

    private void RefreshSelf()
    {
        var ignore = Refresh();
    }

    public static async Task Refresh()
    {
        if (ModMain.FrmInstanceScreenshot is not null)
            await ModMain.FrmInstanceScreenshot.Reload();
        ModMain.FrmInstanceLeft.ItemScreenshot.Checked = true;
        ModMain.Hint("正在刷新……", Log: false);
    }

    private void PageSetupLaunch_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();
        ScreenshotPath = PageInstanceLeft.Instance.PathIndie + @"screenshots\";
        if (!Directory.Exists(ScreenshotPath))
            Directory.CreateDirectory(ScreenshotPath);
        Dispatcher.BeginInvoke(new Func<Task>(Reload));

        // 非重复加载部分
        if (IsLoad)
            return;
        IsLoad = true;
    }

    /// <summary>
    ///     确保当前页面上的信息已正确显示。
    /// </summary>
    public async Task Reload()
    {
        ModAnimation.AniControlEnabled += 1;
        PanBack.ScrollToHome();
        await LoadFileList();
        ModAnimation.AniControlEnabled -= 1;
    }

    private void RefreshTip()
    {
        if (FileList.Count.Equals(0))
        {
            PanNoPic.Visibility = Visibility.Visible;
            PanContent.Visibility = Visibility.Collapsed;
        }
        else
        {
            PanNoPic.Visibility = Visibility.Collapsed;
            PanContent.Visibility = Visibility.Visible;
        }
    }
    
    private static string[] AllowedSuffix = { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp", "*.tiff" };
    
    private async Task LoadFileList()
    {
        ModBase.Log("[Screenshot] 刷新截图文件");
        FileList.Clear();
        if (Directory.Exists(ScreenshotPath))
        {
            FileList = AllowedSuffix
                .SelectMany(suffix => Directory.EnumerateFiles(ScreenshotPath, suffix, SearchOption.TopDirectoryOnly))
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToList();
        }
        PanList.Children.Clear();
        RefreshTip();
        //FileList = FileList.Where(e => !e.ContainsF(@"\debug\")).ToList(); // 排除资源包调试输出
        //FileList.Sort((a, b) => new FileInfo(a).CreationTime > new FileInfo(b).CreationTime);
        ModBase.Log("[Screenshot] 共发现 " + FileList.Count + " 个截图文件");
        if (FileList.Count == 0)
            return;
        await ListAppend(20, 0);
    }

    private void RequireAppend(object sender, ScrollChangedEventArgs e)
    {
        if (FileList.Count != 0 && !_AppendLock && PanBack.VerticalOffset + PanBack.ViewportHeight >= PanBack.ExtentHeight)
        {
            Dispatcher.BeginInvoke(new Func<Task>(async () => await ListAppend()));
        }
    }

    private async Task ListAppend(int Count = 20, int Offset = -1)
    {
        _AppendLock = true;
        if (Offset == -1)
        {
            if (_Offset * Count > FileList.Count)
                return;
            Offset = _Offset + 1;
            _Offset += 1;
        }
        else
        {
            _Offset = Offset;
        }

        if (Count * Offset > FileList.Count)
            return;
        for (int j = Count * Offset, loopTo = Count * (Offset + 1) - 1; j <= loopTo; j++)
        {
            if (j >= FileList.Count)
                break;
            var i = FileList.ElementAt(j);
            try
            {
                if (!File.Exists(i))
                    continue; // 文件在加载途中消失了
                if (File.GetAttributes(i).HasFlag(FileAttributes.Hidden))
                    continue; // 隐藏文件
                if (new FileInfo(i).Length == 0L)
                    continue; // 空文件
                var myCard = new MyCard
                {
                    Margin = new Thickness(7),
                    Tag = i,
                    ToolTip = i.Replace(ScreenshotPath, "") // 适配高清截图模组
                };
                var grid = new Grid();
                myCard.Children.Add(grid);

                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(9d) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(120d) });
                grid.RowDefinitions.Add(new RowDefinition());

                // 图片
                var image = new Image();
                image.Source = await Task.Run(() =>
                {
                    var bitmapImage = new BitmapImage();
                    var loadSource = i;
                    using (var fs = new FileStream(loadSource, FileMode.Open, FileAccess.Read))
                    {
                        bitmapImage.BeginInit();
                        bitmapImage.DecodePixelHeight = 200;
                        bitmapImage.DecodePixelWidth = 400;
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                        bitmapImage.StreamSource = fs;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze();
                    }

                    return bitmapImage;
                });
                image.Stretch = Stretch.Uniform; // 使图片自适应控件大小
                image.Cursor = Cursors.Hand;
                image.MouseLeftButtonDown += (sender, e) =>
                {
                    try
                    {
                        Basics.OpenPath(i);
                    }
                    catch (Exception ex)
                    {
                        ModBase.Log(ex, "打开截图失败！", ModBase.LogLevel.Hint);
                    }
                }; // 使用系统默认程序打开
                Grid.SetRow(image, 1);
                grid.Children.Add(image);

                // 按钮
                var stackPanel = new StackPanel();
                stackPanel.Orientation = Orientation.Horizontal;
                stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
                stackPanel.Margin = new Thickness(3d, 5d, 3d, 5d);
                Grid.SetRow(stackPanel, 2);
                grid.Children.Add(stackPanel);

                var btnOpen = new MyIconTextButton
                {
                    Name = "BtnOpen",
                    Text = Lang.Text("Common.Action.Open"),
                    LogoScale = 0.8d,
                    Logo = ModBase.Logo.IconButtonOpen,
                    Tag = i
                };
                btnOpen.Click += (s, ev) => BtnOpen_Click((MyIconTextButton)s, ev);
                stackPanel.Children.Add(btnOpen);
                var btnDelete = new MyIconTextButton
                {
                    Name = "BtnDelete",
                    Text = Lang.Text("Common.Action.Delete"),
                    LogoScale = 0.8d,
                    Logo = ModBase.Logo.IconButtonDelete,
                    Tag = i
                };
                btnDelete.Click += (s, ev) => BtnDelete_Click((MyIconTextButton)s, ev);
                stackPanel.Children.Add(btnDelete);
                var btnCopy = new MyIconTextButton
                {
                    Name = "BtnCopy",
                    Text = Lang.Text("Common.Action.Copy"),
                    LogoScale = 0.8d,
                    Logo = ModBase.Logo.IconButtonCopy,
                    Tag = i
                };
                btnCopy.Click += (s, ev) => BtnCopy_Click((MyIconTextButton)s, ev);
                stackPanel.Children.Add(btnCopy);
                PanList.Children.Add(myCard);
                myCard.Opacity = 0d;
                ModAnimation.AniStart(new[] { ModAnimation.AaOpacity(myCard, 1d, 200) });
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"[Screenshot] 创建 {i} 截图预览失败，图像可能损坏");
            }
        }

        _AppendLock = false;
    }

    private void RemoveItem(string Path)
    {
        try
        {
            foreach (var i in PanList.Children)
                if (((MyCard)i).Tag.Equals(Path))
                {
                    PanList.Children.Remove((UIElement)i);
                    break;
                }

            FileList.Remove(Path);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "未能找到对应 UI");
        }
    }

    private string GetPathFromSender(MyIconTextButton sender)
    {
        return (string)sender.Tag;
    }

    private void BtnOpen_Click(MyIconTextButton sender, EventArgs e)
    {
        ModBase.OpenExplorer(GetPathFromSender(sender));
    }

    private void BtnDelete_Click(MyIconTextButton sender, EventArgs e)
    {
        var path = GetPathFromSender(sender);
        try
        {
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            RemoveItem(path);
            RefreshTip();
            ModMain.Hint("已将截图移至回收站！");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "删除截图失败！", ModBase.LogLevel.Hint);
        }
    }

    private void BtnCopy_Click(MyIconTextButton sender, EventArgs e)
    {
        var imagePath = GetPathFromSender(sender);
        if (File.Exists(imagePath))
        {
            var TryTime = 0;
            while (TryTime <= 5)
                try
                {
                    ModBase.Log("[Screenshot] 尝试复制" + imagePath + "到剪贴板");
                    Clipboard.SetImage(new BitmapImage(new Uri(imagePath)));
                    ModMain.Hint("已复制截图到剪贴板！");
                    TryTime = 6;
                    return;
                }
                catch (Exception ex)
                {
                    TryTime += 1;
                    ModBase.Log(ex, $"[Screenshot]第 {TryTime} 次复制尝试失败");
                }

            ModMain.Hint("截图复制失败！", ModMain.HintType.Critical);
        }
        else
        {
            ModMain.Hint("截图文件不存在！");
        }
    }

    private void BtnOpenFolder_Click(object sender, MouseButtonEventArgs e)
    {
        if (!Directory.Exists(ScreenshotPath))
            Directory.CreateDirectory(ScreenshotPath);
        ModBase.OpenExplorer(ScreenshotPath);
    }
}
