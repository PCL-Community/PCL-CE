using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.IO.Net.Http.Client.Request;
using PCL.Core.Utils;

namespace PCL;

public class MyImage : Image
{
    private string _ActualSource;

    public MyImage()
    {
        Initialized += (_, _) => Load();
        SizeChanged += (_, _) => UpdateClip();
    }

    /// <summary>
    ///     实际被呈现的图片地址。
    /// </summary>
    public string ActualSource
    {
        get => _ActualSource;
        set
        {
            if (string.IsNullOrEmpty(value))
                value = null;
            if ((_ActualSource ?? "") == (value ?? ""))
                return;
            _ActualSource = value;
            Dispatcher.BeginInvoke(new Func<Task>(async () =>
            {
                try
                {
                    ImageSource bitmap = value is null ? null : await Task.Run(() => new MyBitmap(value));
                    base.Source = bitmap;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"加载图片失败（{value}）");
                    try
                    {
                        if (value.StartsWithF(ModBase.PathTemp) && File.Exists(value)) File.Delete(value);
                    }
                    catch
                    {
                    }
                }
            })); // 在这里先触发可能的文件读取，尽量避免在 UI 线程中读取文件
            // ignored
        }
    }

    private void Load() // 属性读取顺序修正：在完成 XAML 属性读取后再触发图片加载（#4868）
    {
        if (Source is null)
        {
            ActualSource = null;
            return;
        }

        if (!Source.StartsWithF("http"))
        {
            ActualSource = Source;
            return;
        }

        var Url = Source;
        var TempPath = GetTempPath(Url);
        var TempFile = new FileInfo(TempPath);
        var EnableCache = this.EnableCache;
        if (EnableCache && TempFile.Exists)
        {
            ActualSource = TempPath;
            if (DateTime.Now - TempFile.LastWriteTime < FileCacheExpiredTime)
                return; // 无需刷新缓存
        }

        Dispatcher.BeginInvoke(new Func<Task>(async () =>
        {
            try
            {
                // 下载
                ActualSource = LoadingSource;

                var resp = await DownloadImageAsync(Url);
                if (!string.IsNullOrEmpty(resp))
                {
                    ActualSource = resp;
                    return;
                }

                resp = await DownloadImageAsync(FallbackSource);
                if (!string.IsNullOrEmpty(resp))
                {
                    ActualSource = resp;
                    return;
                }
            }
            catch (Exception ex)
            {
                // 更换备用地址
                ModBase.Log(ex, $"Online image get fail（source = {Url}, fallback = {FallbackSource}）", ModBase.LogLevel.Developer);
                TempPath = GetTempPath(Url);
                TempFile = new FileInfo(TempPath);
                if (EnableCache && TempFile.Exists)
                {
                    ActualSource = TempPath;
                    if (DateTime.Now - TempFile.LastWriteTime < FileCacheExpiredTime)
                        return;
                }
            }
        }));
    }

    public static Task<string> DownloadImageAsync(string url)
    {
        return _downloadTasks.GetOrAdd(url, key =>
        {
            var t = DownloadImageInternalAsync(key);
            t.ContinueWith(_ => _downloadTasks.TryRemove(url, out _));
            return t;
        });
    }

    public static string GetTempPath(string Url)
    {
        return Path.Combine(ModBase.PathTemp, "Cache", "Images", $"{ModBase.GetStringMD5(Url)}.png");
    }

    private static readonly ConcurrentDictionary<string, Task<string>> _downloadTasks = new();

    private static async Task<string> DownloadImageInternalAsync(string url)
    {
        var tempPath = GetTempPath(url);
        var tempDownloadingPath = tempPath + RandomUtils.NextInt(0, 1000000);

        try
        {
            Directory.CreateDirectory(ModBase.GetPathFromFullPath(tempPath)); // 重新实现下载，以避免携带 Header（#5072）
            using (var fs = new FileStream(tempDownloadingPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
            {
                using (var response = await HttpRequest.Create(url)
                           .WithHttpVersionOption(HttpVersion.Version30)
                           .SendAsync(addMetedata: false))
                {
                    response.EnsureSuccessStatusCode();

                    using (var nfs = await response.AsStreamAsync())
                    {
                        fs.SetLength(0L);
                        await nfs.CopyToAsync(fs);
                    }
                }
            }

            File.Move(tempDownloadingPath, tempPath, true);
            return tempPath;
        }
        catch (Exception ex)
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            if (File.Exists(tempDownloadingPath)) File.Delete(tempDownloadingPath);

            ModBase.Log(ex, $"Try to get online image fail (url = {url}, dest = {tempPath})");
            return string.Empty;
        }
    }

    #region 公开属性

    public TimeSpan FileCacheExpiredTime = TimeSpan.FromDays(14d);

    public bool EnableCache
    {
        get => (bool)GetValue(EnableCacheProperty);
        set => SetValue(EnableCacheProperty, value);
    }

    public new static readonly DependencyProperty EnableCacheProperty =
        DependencyProperty.Register("EnableCache", typeof(bool), typeof(MyImage), new PropertyMetadata(true));

    /// <summary>
    ///     与 Image 的 Source 类似。
    ///     若输入以 http 开头的字符串，则会尝试下载图片然后显示，图片会保存为本地缓存。
    ///     支持 WebP 格式的图片。
    /// </summary>
    public new string Source // 覆写 Image 的 Source 属性
    {
        get => _Source;
        set
        {
            if (string.IsNullOrEmpty(value))
                value = null;
            if ((_Source ?? "") == (value ?? ""))
                return;
            _Source = value;
            if (!IsInitialized)
                return; // 属性读取顺序修正：在完成 XAML 属性读取后再触发图片加载（#4868）
            Load();
        }
    }

    private string _Source = "";

    public new static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(string),
        typeof(MyImage), new PropertyMetadata((sender, e) =>
        {
            if (sender is not null) ((MyImage)sender).Source = e.NewValue.ToString();
        }));

    /// <summary>
    ///     当 Source 首次下载失败时，会从该备用地址加载图片。
    /// </summary>
    public string FallbackSource { get; set; }

    /// <summary>
    ///     正在下载网络图片时显示的本地图片。
    /// </summary>
    public string LoadingSource { get; set; } = "pack://application:,,,/images/Icons/NoIcon.png";
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }
    private static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            "CornerRadius",
            typeof(CornerRadius),
            typeof(MyImage),
            new FrameworkPropertyMetadata(
                new CornerRadius(-1),
                OnCornerRadiusChanged)
        );

    private static void OnCornerRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MyImage)d).UpdateClip();
    }

    private void UpdateClip() // Handles Me.SizeChanged will be added separately
    {
        if (ActualWidth > 0 && ActualHeight > 0 &&
            CornerRadius.TopLeft >= 0 && CornerRadius.TopRight >= 0)
        {
            Clip = new RectangleGeometry(
                new Rect(0, 0, ActualWidth, ActualHeight),
                CornerRadius.TopLeft,
                CornerRadius.TopRight);
        }
    }
    #endregion
}