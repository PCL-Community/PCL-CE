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
using PCL.Core.UI;

namespace PCL;

public partial class PageInstanceScreenshot : IRefreshable
{
    private bool _AppendLock;
    private int _Offset;

    private List<string> fileList = new();

    private bool isLoad;
    private string screenshotPath;

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
        if (ModMain.frmInstanceScreenshot is not null)
            await ModMain.frmInstanceScreenshot.Reload();
        ModMain.frmInstanceLeft.ItemScreenshot.Checked = true;
        ModMain.Hint(Lang.Text("Instance.Saves.Status.Refreshing"), log: false);
    }

    private void PageSetupLaunch_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();
        screenshotPath = PageInstanceLeft.instance.PathIndie + @"screenshots\";
        if (!Directory.Exists(screenshotPath))
            Directory.CreateDirectory(screenshotPath);
        Dispatcher.BeginInvoke(new Func<Task>(Reload));

        // 非重复加载部分
        if (isLoad)
            return;
        isLoad = true;
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
        if (fileList.Count.Equals(0))
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
    
    private static string[] allowedSuffix = { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp", "*.tiff" };
    
    private async Task LoadFileList()
    {
        ModBase.Log("[Screenshot] 刷新截图文件");
        fileList.Clear();
        if (Directory.Exists(screenshotPath))
        {
            fileList = allowedSuffix
                .SelectMany(suffix => Directory.EnumerateFiles(screenshotPath, suffix, SearchOption.TopDirectoryOnly))
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToList();
        }
        PanList.Children.Clear();
        RefreshTip();
        //FileList = FileList.Where(e => !e.ContainsF(@"\debug\")).ToList(); // 排除资源包调试输出
        //FileList.Sort((a, b) => new FileInfo(a).CreationTime > new FileInfo(b).CreationTime);
        ModBase.Log("[Screenshot] 共发现 " + fileList.Count + " 个截图文件");
        if (fileList.Count == 0)
            return;
        await ListAppend(20, 0);
    }

    private void RequireAppend(object sender, ScrollChangedEventArgs e)
    {
        if (fileList.Count != 0 && !_AppendLock && PanBack.VerticalOffset + PanBack.ViewportHeight >= PanBack.ExtentHeight)
        {
            Dispatcher.BeginInvoke(new Func<Task>(async () => await ListAppend()));
        }
    }

    private async Task ListAppend(int count = 20, int offset = -1)
    {
        _AppendLock = true;
        if (offset == -1)
        {
            if (_Offset * count > fileList.Count)
                return;
            offset = _Offset + 1;
            _Offset += 1;
        }
        else
        {
            _Offset = offset;
        }

        if (count * offset > fileList.Count)
            return;
        for (int j = count * offset, loopTo = count * (offset + 1) - 1; j <= loopTo; j++)
        {
            if (j >= fileList.Count)
                break;
            var i = fileList.ElementAt(j);
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
                    ToolTip = i.Replace(screenshotPath, "") // 适配高清截图模组
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
                        ModBase.Log(ex, Lang.Text("Instance.Screenshot.OpenFailed"), ModBase.LogLevel.Hint);
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
                    Logo = Icon.IconButtonOpen,
                    Tag = i
                };
                btnOpen.Click += (s, ev) => BtnOpen_Click((MyIconTextButton)s, ev);
                stackPanel.Children.Add(btnOpen);
                var btnDelete = new MyIconTextButton
                {
                    Name = "BtnDelete",
                    Text = Lang.Text("Common.Action.Delete"),
                    LogoScale = 0.8d,
                    Logo = Icon.IconButtonDelete,
                    Tag = i
                };
                btnDelete.Click += (s, ev) => BtnDelete_Click((MyIconTextButton)s, ev);
                stackPanel.Children.Add(btnDelete);
                var btnCopy = new MyIconTextButton
                {
                    Name = "BtnCopy",
                    Text = Lang.Text("Common.Action.Copy"),
                    LogoScale = 0.8d,
                    Logo = Icon.IconButtonCopy,
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

    private void RemoveItem(string path)
    {
        try
        {
            foreach (var i in PanList.Children)
                if (((MyCard)i).Tag.Equals(path))
                {
                    PanList.Children.Remove((UIElement)i);
                    break;
                }

            fileList.Remove(path);
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
            ModMain.Hint(Lang.Text("Instance.Screenshot.Deleted"));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Instance.Screenshot.DeleteFailed"), ModBase.LogLevel.Hint);
        }
    }

    private void BtnCopy_Click(MyIconTextButton sender, EventArgs e)
    {
        var imagePath = GetPathFromSender(sender);
        if (File.Exists(imagePath))
        {
            var tryTime = 0;
            while (tryTime <= 5)
                try
                {
                    ModBase.Log("[Screenshot] 尝试复制" + imagePath + "到剪贴板");
                    Clipboard.SetImage(new BitmapImage(new Uri(imagePath)));
                    ModMain.Hint(Lang.Text("Instance.Screenshot.CopiedToClipboard"));
                    tryTime = 6;
                    return;
                }
                catch (Exception ex)
                {
                    tryTime += 1;
                    ModBase.Log(ex, $"[Screenshot]第 {tryTime} 次复制尝试失败");
                }

            ModMain.Hint(Lang.Text("Instance.Screenshot.CopyFailed"), ModMain.HintType.Critical);
        }
        else
        {
            ModMain.Hint(Lang.Text("Instance.Screenshot.FileNotFound"));
        }
    }

    private void BtnOpenFolder_Click(object sender, MouseButtonEventArgs e)
    {
        if (!Directory.Exists(screenshotPath))
            Directory.CreateDirectory(screenshotPath);
        ModBase.OpenExplorer(screenshotPath);
    }
}
