// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using PCL.Core.IO.Net;

namespace PCL.Desktop.Controls.Legacy;

/// <summary>
/// WPF-compatible image control used by copied PCL views.
/// </summary>
public class MyImage : Image
{
    private const string WpfImagePrefix = "pack://application:,,,/images/";
    private const string AvaloniaImagePrefix = "avares://PCL.Desktop/WpfOriginal/Images/";
    private static readonly ConcurrentDictionary<string, Task<string>> DownloadTasks = new(StringComparer.Ordinal);
    private static readonly string ImageCacheDirectory = Path.Combine(Path.GetTempPath(), "PCL-N", "Cache", "Images");

    private string? _actualSource;
    private bool _isAttached;
    private int _loadVersion;
    private object? _source = string.Empty;

    public MyImage()
    {
        AttachedToVisualTree += (_, _) =>
        {
            _isAttached = true;
            Load();
        };
        DetachedFromVisualTree += (_, _) => _isAttached = false;
        SizeChanged += (_, _) => UpdateClip();
        this.GetObservable(CornerRadiusProperty).Subscribe(_ => UpdateClip());
    }

    public static readonly StyledProperty<bool> EnableCacheProperty =
        AvaloniaProperty.Register<MyImage, bool>(nameof(EnableCache), true);

    public static readonly StyledProperty<string?> FallbackSourceProperty =
        AvaloniaProperty.Register<MyImage, string?>(nameof(FallbackSource));

    public static readonly StyledProperty<string?> LoadingSourceProperty =
        AvaloniaProperty.Register<MyImage, string?>(
            nameof(LoadingSource),
            WpfImagePrefix + "Icons/NoIcon.png");

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<MyImage, CornerRadius>(
            nameof(CornerRadius),
            new CornerRadius(-1d));

    [SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "WPF compatibility surface.")]
    public TimeSpan fileCacheExpiredTime = TimeSpan.FromDays(14d);

    public bool EnableCache
    {
        get => GetValue(EnableCacheProperty);
        set => SetValue(EnableCacheProperty, value);
    }

    /// <summary>
    /// Matches the WPF control: string values may be local resources, files, or http(s) URLs.
    /// Existing Avalonia migration code may also pass an already decoded image.
    /// </summary>
    public new object? Source
    {
        get => _source;
        set
        {
            object? normalized = NormalizeSourceValue(value);
            if (Equals(_source, normalized))
                return;

            _source = normalized;
            if (normalized is IImage or null)
            {
                BeginLoad(normalized);
                return;
            }

            if (_isAttached)
                BeginLoad(normalized);
        }
    }

    /// <summary>
    /// The concrete image address currently presented after cache and fallback resolution.
    /// </summary>
    public string? ActualSource
    {
        get => _actualSource;
        private set => _actualSource = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public string? FallbackSource
    {
        get => GetValue(FallbackSourceProperty);
        set => SetValue(FallbackSourceProperty, NormalizeString(value));
    }

    public string? LoadingSource
    {
        get => GetValue(LoadingSourceProperty);
        set => SetValue(LoadingSourceProperty, NormalizeString(value));
    }

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public static Task<string> DownloadImageAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Task.FromResult(string.Empty);

        return DownloadTasks.GetOrAdd(url, static key =>
        {
            Task<string> task = DownloadImageInternalAsync(key);
            _ = task.ContinueWith(
                static (finishedTask, state) =>
                {
                    string finishedUrl = (string)state!;
                    DownloadTasks.TryRemove(finishedUrl, out _);
                    _ = finishedTask.Exception;
                },
                key,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return task;
        });
    }

    public static string GetTempPath(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return Path.Combine(ImageCacheDirectory, Convert.ToHexString(hash).ToLowerInvariant() + ".png");
    }

    private void Load() => BeginLoad(_source);

    private void BeginLoad(object? source)
    {
        int version = Interlocked.Increment(ref _loadVersion);
        _ = LoadAsync(version, source);
    }

    private async Task LoadAsync(int version, object? source)
    {
        if (source is null)
        {
            await ApplyDecodedSourceAsync(version, null, null).ConfigureAwait(false);
            return;
        }

        if (source is IImage image)
        {
            await ApplyDecodedSourceAsync(version, null, image).ConfigureAwait(false);
            return;
        }

        if (source is not string address)
        {
            await ApplyDecodedSourceAsync(version, null, null).ConfigureAwait(false);
            return;
        }

        if (!IsHttpSource(address))
        {
            await ApplyActualSourceAsync(version, address).ConfigureAwait(false);
            return;
        }

        await LoadRemoteSourceAsync(version, address).ConfigureAwait(false);
    }

    private async Task LoadRemoteSourceAsync(int version, string url)
    {
        string tempPath = GetTempPath(url);
        FileInfo tempFile = new(tempPath);
        bool enableCache = EnableCache;
        if (enableCache && tempFile.Exists)
        {
            await ApplyActualSourceAsync(version, tempPath).ConfigureAwait(false);
            if (DateTime.UtcNow - tempFile.LastWriteTimeUtc < fileCacheExpiredTime)
                return;
        }

        await ApplyActualSourceAsync(version, LoadingSource).ConfigureAwait(false);

        string downloadedPath = await DownloadImageAsync(url).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(downloadedPath))
        {
            await ApplyActualSourceAsync(version, downloadedPath).ConfigureAwait(false);
            return;
        }

        string fallbackPath = await DownloadImageAsync(FallbackSource).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(fallbackPath))
            await ApplyActualSourceAsync(version, fallbackPath).ConfigureAwait(false);
    }

    private async Task ApplyActualSourceAsync(int version, string? address)
    {
        string? normalizedAddress = NormalizeString(address);
        if (normalizedAddress is null)
        {
            await ApplyDecodedSourceAsync(version, null, null).ConfigureAwait(false);
            return;
        }

        try
        {
            IImage? image = await Task.Run(() => LoadBitmap(normalizedAddress)).ConfigureAwait(false);
            await ApplyDecodedSourceAsync(version, normalizedAddress, image).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            TryDeleteBrokenCache(normalizedAddress);
            Trace.WriteLine($"Failed to load image '{normalizedAddress}': {ex}");
        }
    }

    private Task ApplyDecodedSourceAsync(int version, string? actualSource, IImage? image)
    {
        return Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (version != Volatile.Read(ref _loadVersion))
                return;

            ActualSource = actualSource;
            base.Source = image;
            UpdateClip();
        }).GetTask();
    }

    private static Bitmap? LoadBitmap(string address)
    {
        string? normalized = NormalizeImageAddress(address);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        if (File.Exists(normalized))
        {
            using Stream fileStream = File.OpenRead(normalized);
            return new Bitmap(fileStream);
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri))
            return null;

        if (uri.IsFile)
        {
            using Stream fileStream = File.OpenRead(uri.LocalPath);
            return new Bitmap(fileStream);
        }

        if (uri.Scheme.Equals("avares", StringComparison.OrdinalIgnoreCase))
        {
            using Stream assetStream = AssetLoader.Open(uri);
            return new Bitmap(assetStream);
        }

        return null;
    }

    private static async Task<string> DownloadImageInternalAsync(string url)
    {
        string tempPath = GetTempPath(url);
        string downloadingPath = tempPath + "." + Environment.ProcessId + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
            using HttpResponseMessage response = await PortableHttp.Client.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using (Stream sourceStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            await using (FileStream destinationStream = new(
                             downloadingPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.Read,
                             bufferSize: 81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await sourceStream.CopyToAsync(destinationStream).ConfigureAwait(false);
                await destinationStream.FlushAsync().ConfigureAwait(false);
            }

            File.Move(downloadingPath, tempPath, overwrite: true);
            return tempPath;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            TryDeleteFile(tempPath);
            TryDeleteFile(downloadingPath);
            Trace.WriteLine($"Try to get online image fail (url = {url}, dest = {tempPath}): {ex}");
            return string.Empty;
        }
    }

    private static object? NormalizeSourceValue(object? value) =>
        value switch
        {
            null => null,
            string text => NormalizeString(text),
            Uri uri => NormalizeString(uri.ToString()),
            _ => value
        };

    private static string? NormalizeString(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeImageAddress(string address)
    {
        string? normalized = NormalizeString(address);
        if (normalized is null)
            return null;

        normalized = normalized.Replace('\\', '/');
        if (normalized.StartsWith(WpfImagePrefix, StringComparison.OrdinalIgnoreCase))
            return AvaloniaImagePrefix + normalized[WpfImagePrefix.Length..];

        const string rootedImagePrefix = "/images/";
        if (normalized.StartsWith(rootedImagePrefix, StringComparison.OrdinalIgnoreCase))
            return AvaloniaImagePrefix + normalized[rootedImagePrefix.Length..];

        const string relativeImagePrefix = "images/";
        if (normalized.StartsWith(relativeImagePrefix, StringComparison.OrdinalIgnoreCase))
            return AvaloniaImagePrefix + normalized[relativeImagePrefix.Length..];

        return normalized;
    }

    private static bool IsHttpSource(string address) =>
        Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
         uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));

    private static void TryDeleteBrokenCache(string address)
    {
        if (address.StartsWith(ImageCacheDirectory, StringComparison.OrdinalIgnoreCase))
            TryDeleteFile(address);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"Failed to delete image cache '{path}': {ex}");
        }
    }

    private void UpdateClip()
    {
        if (Bounds.Width <= 0d || Bounds.Height <= 0d || CornerRadius.TopLeft < 0d || CornerRadius.TopRight < 0d)
        {
            Clip = null;
            return;
        }

        Clip = new RectangleGeometry
        {
            Rect = new Rect(Bounds.Size),
            RadiusX = CornerRadius.TopLeft,
            RadiusY = CornerRadius.TopRight
        };
    }
}
