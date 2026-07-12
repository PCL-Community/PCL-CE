// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Features.Launching.Views;

namespace PCL.Desktop.Features.Instances.Views;

public partial class PageInstanceScreenshotRight : MyPageRight
{
    private static readonly HashSet<string> AllowedSuffix = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".bmp",
        ".webp",
        ".tiff"
    };

    private bool _appendLock;
    private int _offset;
    private readonly List<string> _fileList = [];
    private bool _isLoad;
    private string _screenshotPath = string.Empty;
    private LaunchInstanceInfo? _instance;

    public PageInstanceScreenshotRight()
    {
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");
        if (PanScroll is not null)
            PanScroll.ScrollChanged += RequireAppend;
        if (this.FindControl<MyButton>("BtnOpenFolder") is { } btnOpenFolder)
            btnOpenFolder.Click += BtnOpenFolder_Click;
        if (this.FindControl<MyButton>("BtnOpenFolderTop") is { } btnOpenFolderTop)
            btnOpenFolderTop.Click += BtnOpenFolder_Click;
    }

    public event EventHandler<string>? OpenFolderRequested;

    public event EventHandler<string>? OpenFileRequested;

    public event EventHandler<string>? StatusMessage;

    public void SetInstance(LaunchInstanceInfo instance)
    {
        _instance = instance ?? throw new ArgumentNullException(nameof(instance));
        _ = SetInstanceAsync(instance);
    }

    private async Task SetInstanceAsync(LaunchInstanceInfo instance)
    {
        string gameDir = await InstanceGameDirectory.ResolveAsync(instance).ConfigureAwait(true);
        if (_instance is null ||
            !string.Equals(_instance.InstanceDirectory, instance.InstanceDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _screenshotPath = Path.Combine(gameDir, "screenshots");
        if (!Directory.Exists(_screenshotPath))
            Directory.CreateDirectory(_screenshotPath);

        if (PanScroll is not null)
            PanScroll.ScrollToHome();
        if (_isLoad)
        {
            await Reload().ConfigureAwait(true);
            return;
        }

        _isLoad = true;
        await Reload().ConfigureAwait(true);
    }

    public async Task Reload()
    {
        ModAnimation.AniControlEnabled += 1;
        try
        {
            if (PanScroll is not null)
                PanScroll.ScrollToHome();
            await LoadFileList().ConfigureAwait(true);
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    private void RefreshTip()
    {
        bool isEmpty = _fileList.Count == 0;
        if (this.FindControl<Control>("PanNoPic") is { } noPic)
            noPic.IsVisible = isEmpty;
        if (this.FindControl<Control>("PanContent") is { } content)
            content.IsVisible = !isEmpty;
    }

    private async Task LoadFileList()
    {
        _fileList.Clear();
        _offset = 0;
        if (Directory.Exists(_screenshotPath))
        {
            _fileList.AddRange(
                Directory.EnumerateFiles(_screenshotPath, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => AllowedSuffix.Contains(Path.GetExtension(path)))
                    .OrderByDescending(File.GetCreationTime));
        }

        if (this.FindControl<WrapPanel>("PanList") is { } list)
            list.Children.Clear();
        RefreshTip();
        if (_fileList.Count == 0)
            return;

        await ListAppend(20, 0).ConfigureAwait(true);
    }

    private void RequireAppend(object? sender, ScrollChangedEventArgs e)
    {
        if (_fileList.Count == 0 || _appendLock || PanScroll is null)
            return;

        if (PanScroll.Offset.Y + PanScroll.Viewport.Height >= PanScroll.Extent.Height - 1d)
        {
            Dispatcher.UIThread.Post(async () => await ListAppend().ConfigureAwait(true));
        }
    }

    private async Task ListAppend(int count = 20, int offset = -1)
    {
        if (this.FindControl<WrapPanel>("PanList") is not { } list)
            return;

        _appendLock = true;
        try
        {
            if (offset == -1)
            {
                if (_offset * count > _fileList.Count)
                    return;
                offset = _offset + 1;
                _offset += 1;
            }
            else
            {
                _offset = offset;
            }

            if (count * offset > _fileList.Count)
                return;

            for (int j = count * offset, loopTo = count * (offset + 1) - 1; j <= loopTo; j++)
            {
                if (j >= _fileList.Count)
                    break;

                string path = _fileList[j];
                try
                {
                    if (!File.Exists(path))
                        continue;
                    if (File.GetAttributes(path).HasFlag(FileAttributes.Hidden))
                        continue;
                    if (new FileInfo(path).Length == 0L)
                        continue;

                    MyCard myCard = await CreateScreenshotCardAsync(path).ConfigureAwait(true);
                    list.Children.Add(myCard);
                    myCard.Opacity = 0d;
                    ModAnimation.AniStart(ModAnimation.AaOpacity(myCard, 1d, 200));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    StatusMessage?.Invoke(this, "有一张截图无法显示，可能已损坏或正在被其他程序占用。");
                }
            }
        }
        finally
        {
            _appendLock = false;
        }
    }

    private async Task<MyCard> CreateScreenshotCardAsync(string imagePath)
    {
        MyCard myCard = new()
        {
            Margin = new Thickness(7d),
            Tag = imagePath
        };
        ToolTip.SetTip(myCard, GetRelativeScreenshotPath(imagePath));

        Grid grid = new();
        myCard.Children.Add(grid);
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(9d) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(120d) });
        grid.RowDefinitions.Add(new RowDefinition());

        Bitmap bitmap = await Task.Run(() => LoadPreviewBitmap(imagePath)).ConfigureAwait(true);
        Image image = new()
        {
            Source = bitmap,
            Stretch = Stretch.Uniform,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        image.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(image).Properties.IsLeftButtonPressed)
                OpenFileRequested?.Invoke(this, imagePath);
        };
        Grid.SetRow(image, 1);
        grid.Children.Add(image);

        StackPanel stackPanel = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(3d, 5d, 3d, 5d)
        };
        Grid.SetRow(stackPanel, 2);
        grid.Children.Add(stackPanel);

        MyIconTextButton btnOpen = new()
        {
            Name = "BtnOpen",
            Text = GetResourceText("Common.Action.Open", "打开"),
            LogoScale = 0.8d,
            SvgIcon = "lucide/folder-open",
            Tag = imagePath
        };
        btnOpen.Click += (s, ev) => BtnOpen_Click((MyIconTextButton)s, ev);
        stackPanel.Children.Add(btnOpen);

        MyIconTextButton btnDelete = new()
        {
            Name = "BtnDelete",
            Text = GetResourceText("Common.Action.Delete", "删除"),
            LogoScale = 0.8d,
            SvgIcon = "lucide/trash-2",
            Tag = imagePath
        };
        btnDelete.Click += (s, ev) => BtnDelete_Click((MyIconTextButton)s, ev);
        stackPanel.Children.Add(btnDelete);

        MyIconTextButton btnCopy = new()
        {
            Name = "BtnCopy",
            Text = GetResourceText("Common.Action.Copy", "复制"),
            LogoScale = 0.8d,
            SvgIcon = "lucide/copy",
            Tag = imagePath
        };
        btnCopy.Click += (s, ev) => _ = BtnCopy_ClickAsync((MyIconTextButton)s);
        stackPanel.Children.Add(btnCopy);

        return myCard;
    }

    private static Bitmap LoadPreviewBitmap(string imagePath)
    {
        using FileStream stream = File.OpenRead(imagePath);
        return Bitmap.DecodeToWidth(stream, 400, BitmapInterpolationMode.MediumQuality);
    }

    private void RemoveItem(string path)
    {
        try
        {
            if (this.FindControl<WrapPanel>("PanList") is { } list)
            {
                foreach (Control child in list.Children.OfType<Control>().ToArray())
                {
                    if (child is MyCard card && Equals(card.Tag, path))
                    {
                        list.Children.Remove(child);
                        break;
                    }
                }
            }

            _fileList.Remove(path);
        }
        catch (InvalidOperationException)
        {
            StatusMessage?.Invoke(this, "未能刷新截图列表，请稍后再试。");
        }
    }

    private static string GetPathFromSender(MyIconTextButton sender) =>
        sender.Tag as string ?? string.Empty;

    private void BtnOpen_Click(MyIconTextButton sender, EventArgs e)
    {
        string path = GetPathFromSender(sender);
        if (!string.IsNullOrWhiteSpace(path))
            OpenFileRequested?.Invoke(this, path);
    }

    private void BtnDelete_Click(MyIconTextButton sender, EventArgs e)
    {
        string path = GetPathFromSender(sender);
        try
        {
            if (File.Exists(path))
                File.Delete(path);
            RemoveItem(path);
            RefreshTip();
            StatusMessage?.Invoke(this, "截图已删除。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage?.Invoke(this, "未能删除截图，请确认文件没有被其他程序占用。");
        }
    }

    private async Task BtnCopy_ClickAsync(MyIconTextButton sender)
    {
        string imagePath = GetPathFromSender(sender);
        if (!File.Exists(imagePath))
        {
            StatusMessage?.Invoke(this, "这张截图已经不存在。");
            return;
        }

        for (int tryTime = 0; tryTime <= 5; tryTime++)
        {
            try
            {
                IClipboard? clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard is null)
                    throw new InvalidOperationException("无法访问系统剪贴板。");

                using Bitmap bitmap = new(imagePath);
                await clipboard.SetBitmapAsync(bitmap).ConfigureAwait(true);
                StatusMessage?.Invoke(this, "已复制截图。");
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                if (tryTime == 5)
                {
                    StatusMessage?.Invoke(this, "未能复制截图，请稍后再试。");
                    return;
                }
            }
        }
    }

    private void BtnOpenFolder_Click(object? sender, EventArgs e)
    {
        if (!Directory.Exists(_screenshotPath))
            Directory.CreateDirectory(_screenshotPath);
        OpenFolderRequested?.Invoke(this, _screenshotPath);
    }

    private string GetRelativeScreenshotPath(string imagePath)
    {
        try
        {
            return Path.GetRelativePath(_screenshotPath, imagePath);
        }
        catch (ArgumentException)
        {
            return Path.GetFileName(imagePath);
        }
    }

    private string GetResourceText(string key, string fallback)
    {
        if (TryGetResource(key, null, out object? value) && value is string text)
            return text;

        return fallback;
    }

    private static string GetMinecraftRootFromInstance(LaunchInstanceInfo instance)
    {
        DirectoryInfo versionDirectory = new(instance.InstanceDirectory);
        DirectoryInfo? versionsDirectory = versionDirectory.Parent;
        if (versionsDirectory?.Parent is not null &&
            string.Equals(versionsDirectory.Name, "versions", StringComparison.OrdinalIgnoreCase))
        {
            return versionsDirectory.Parent.FullName;
        }

        return instance.InstanceDirectory;
    }
}
