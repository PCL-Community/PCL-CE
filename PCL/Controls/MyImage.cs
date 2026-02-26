using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualBasic.CompilerServices;
using PCL.Core.IO.Net.Http.Client;
using PCL.Core.Utils;

namespace PCL;

public class MyImage : Image
{
    private string _ActualSource;

    public MyImage()
    {
        Initialized += (_, __) => Load();
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

    private async void Load() // 属性读取顺序修正：在完成 XAML 属性读取后再触发图片加载（#4868）
    {
        // 空
        if (Source is null)
        {
            ActualSource = null;
            return;
        }

        // 本地图片
        if (!Source.StartsWithF("http"))
        {
            ActualSource = Source;
            return;
        }

        // 从缓存加载网络图片
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

        string TempDownloadingPath = null;
        try
        {
            // 下载
            ActualSource = LoadingSource; // 显示加载中图片
            TempDownloadingPath = TempPath + RandomUtils.NextInt(0, 1000000);
            Directory.CreateDirectory(ModBase.GetPathFromFullPath(TempPath)); // 重新实现下载，以避免携带 Header（#5072）
            using (var fs = new FileStream(TempDownloadingPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read))
            {
                using (var response = await HttpRequestBuilder.Create(Url, HttpMethod.Get)
                           .WithHttpVersionOption(HttpVersion.Version30).WithDefaultHeaderOption(false).SendAsync())
                {
                    if (response.IsSuccess)
                    {
                        using (var nfs = await response.AsStreamAsync())
                        {
                            fs.SetLength(0L);
                            await nfs.CopyToAsync(fs);
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(FallbackSource))
                    {
                        using (var fallbackResponse = await HttpRequestBuilder.Create(FallbackSource, HttpMethod.Get)
                                   .WithHttpVersionOption(HttpVersion.Version30).WithDefaultHeaderOption(false)
                                   .SendAsync(true))
                        {
                            if (fallbackResponse.IsSuccess)
                                using (var fallbackNfs = await fallbackResponse.AsStreamAsync())
                                {
                                    fs.SetLength(0L);
                                    await fallbackNfs.CopyToAsync(fs);
                                }
                        }
                    }
                    else
                    {
                        return;
                    }
                }
            }

            if ((Url ?? "") != (Source ?? "") && (Url ?? "") != (FallbackSource ?? ""))
            {
                // 已经更换了地址
                File.Delete(TempDownloadingPath);
            }
            else if (EnableCache)
            {
                // 保存缓存并显示
                if (File.Exists(TempPath))
                    File.Delete(TempPath);
                File.Move(TempDownloadingPath, TempPath, true);
                ActualSource = TempPath;
            }
            else
            {
                // 直接显示
                ActualSource = TempDownloadingPath;
            }
        }
        catch (Exception ex)
        {
            try
            {
                if (TempPath is not null && File.Exists(TempPath))
                    File.Delete(TempPath);
                if (TempDownloadingPath is not null && File.Exists(TempDownloadingPath))
                    File.Delete(TempDownloadingPath);
            }
            catch
            {
            }

            // 更换备用地址
            ModBase.Log(ex, $"下载图片失败（Base = {Url}, Fallback = {FallbackSource}）", ModBase.LogLevel.Developer);
            // 从缓存加载网络图片
            TempPath = GetTempPath(Url);
            TempFile = new FileInfo(TempPath);
            if (EnableCache && TempFile.Exists)
            {
                ActualSource = TempPath;
                if (DateTime.Now - TempFile.LastWriteTime < FileCacheExpiredTime)
                    return; // 无需刷新缓存
            }
        }
    }

    public static string GetTempPath(string Url)
    {
        return Path.Combine(ModBase.PathTemp, "Cache", "Images", $"{ModBase.GetStringMD5(Url)}.png");
    }

    #region 公开属性

    /// <summary>
    ///     网络图片的缓存有效期。
    ///     在这个时间后，才会重新尝试下载图片。
    /// </summary>
    public TimeSpan FileCacheExpiredTime = TimeSpan.FromDays(14d);

    /// <summary>
    ///     是否允许将网络图片存储到本地用作缓存。
    /// </summary>
    public bool EnableCache
    {
        get => Conversions.ToBoolean(GetValue(EnableCacheProperty));
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

    #endregion
}