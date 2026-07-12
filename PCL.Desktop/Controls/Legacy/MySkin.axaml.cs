// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace PCL.Desktop.Controls.Legacy;

public partial class MySkin : Grid
{
    private static readonly HttpClient SkinClient = CreateSkinClient();
    public static readonly StyledProperty<string> AddressProperty =
        AvaloniaProperty.Register<MySkin, string>(nameof(Address), string.Empty);

    public static readonly StyledProperty<bool> HasCapeProperty =
        AvaloniaProperty.Register<MySkin, bool>(nameof(HasCape));

    private readonly Image? _backImage;
    private readonly Image? _frontImage;
    private readonly Border? _shadow;
    private Bitmap? _faceBitmap;
    private Bitmap? _hatBitmap;
    private bool _isSkinMouseDown;
    private int _loadVersion;

    public MySkin()
    {
        AvaloniaXamlLoader.Load(this);
        _backImage = this.FindControl<Image>("ImgBack");
        _frontImage = this.FindControl<Image>("ImgFore");
        _shadow = this.FindControl<Border>("ShadowSkin");
        if (this.FindControl<MyMenuItem>("BtnSkinSave") is { } save)
        {
            save.Click += BtnSkinSaveClick;
            save.Checked += BtnSkinSaveChecked;
        }
        if (this.FindControl<MyMenuItem>("BtnSkinRefresh") is { } refresh)
            refresh.Click += RefreshClick;
        if (this.FindControl<MyMenuItem>("BtnSkinCape") is { } cape)
            cape.Click += BtnSkinCapeClick;

        PointerEntered += PanSkin_PointerEntered;
        PointerExited += PanSkin_PointerExited;
        PointerPressed += PanSkin_PointerPressed;
        PointerReleased += PanSkin_PointerReleased;
        this.GetObservable(AddressProperty).Subscribe(address =>
        {
            _ = LoadAsync();
        });
        this.GetObservable(HasCapeProperty).Subscribe(value =>
        {
            if (this.FindControl<MyMenuItem>("BtnSkinCape") is { } cape)
                cape.IsVisible = value;
        });
    }

    public event EventHandler<PointerReleasedEventArgs>? Click;

    public event EventHandler? SaveRequested;

    public event EventHandler? RefreshRequested;

    public event EventHandler? CapeRequested;

    public string Address
    {
        get => GetValue(AddressProperty);
        set => SetValue(AddressProperty, value);
    }

    public bool HasCape
    {
        get => GetValue(HasCapeProperty);
        set => SetValue(HasCapeProperty, value);
    }

    public void Load() => _ = LoadAsync();

    /// <summary>
    /// Prefer explicit skin texture URL/path.
    /// Third-party (Authlib) uses <paramref name="authServer"/> sessionserver; otherwise Mojang.
    /// Never invent a Steve CDN default.
    /// </summary>
    public static string ResolveSkinAddress(string? skinAddress, string? uuid = null, string? authServer = null)
    {
        if (!string.IsNullOrWhiteSpace(skinAddress))
            return skinAddress.Trim();

        string normalized = NormalizeUuid(uuid);
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(authServer))
        {
            string baseUrl = NormalizeAuthServerBase(authServer);
            if (!string.IsNullOrWhiteSpace(baseUrl))
                return baseUrl + "/sessionserver/session/minecraft/profile/" + normalized;
        }

        return "uuid:" + normalized;
    }

    private static string NormalizeAuthServerBase(string authServer)
    {
        string baseUrl = authServer.Trim().TrimEnd('/');
        if (baseUrl.EndsWith("/authserver", StringComparison.OrdinalIgnoreCase))
            baseUrl = baseUrl[..^"/authserver".Length].TrimEnd('/');
        return baseUrl;
    }

    private async Task LoadAsync()
    {
        string address = Address.Trim();
        int loadVersion = Interlocked.Increment(ref _loadVersion);
        try
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                await ClearIfCurrentAsync(loadVersion).ConfigureAwait(false);
                return;
            }

            byte[]? bytes = await LoadSkinBytesAsync(address).ConfigureAwait(false);
            if (bytes is null || bytes.Length < 64)
            {
                await ClearIfCurrentAsync(loadVersion).ConfigureAwait(false);
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (loadVersion != _loadVersion)
                    return;

                try
                {
                    using MemoryStream stream = new(bytes, writable: false);
                    using Bitmap fullSkin = new(stream);
                    PixelSize size = fullSkin.PixelSize;
                    if (size.Width < 32 || size.Height < 32)
                    {
                        ClearImages();
                        return;
                    }

                    int scale = Math.Max(1, (int)Math.Round(size.Width / 64d));
                    // WPF: face at (8,8) 8x8; hat overlay at (40,8) 8x8 — pixel-scaled layered head.
                    Bitmap? face = CropToBitmap(fullSkin, scale * 8, scale * 8, scale * 8, scale * 8);
                    Bitmap? hat = size.Width >= 64 && size.Height >= 32
                        ? CropToBitmap(fullSkin, scale * 40, scale * 8, scale * 8, scale * 8)
                        : null;

                    ClearImages();
                    _faceBitmap = face;
                    _hatBitmap = hat;
                    if (_backImage is not null)
                        _backImage.Source = face;
                    if (_frontImage is not null)
                        _frontImage.Source = hat;
                }
                catch (Exception)
                {
                    ClearImages();
                }
            });
        }
        catch (Exception)
        {
            await ClearIfCurrentAsync(loadVersion).ConfigureAwait(false);
        }
    }

    private static async Task<byte[]?> LoadSkinBytesAsync(string address)
    {
        if (File.Exists(address))
            return await File.ReadAllBytesAsync(address).ConfigureAwait(false);

        // Built-in offline defaults: avares://PCL.Desktop/Assets/Legacy/Skins/Steve.png
        if (address.StartsWith("avares://", StringComparison.OrdinalIgnoreCase) &&
            Uri.TryCreate(address, UriKind.Absolute, out Uri? avaresUri))
        {
            await using Stream stream = AssetLoader.Open(avaresUri);
            using MemoryStream ms = new();
            await stream.CopyToAsync(ms).ConfigureAwait(false);
            return ms.ToArray();
        }

        if (address.StartsWith("uuid:", StringComparison.OrdinalIgnoreCase))
        {
            string uuid = NormalizeUuid(address["uuid:".Length..]);
            string? textureUrl = await ResolveTextureUrlFromUuidAsync(uuid).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(textureUrl))
                return null;
            address = textureUrl;
        }

        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        uri = NormalizeSkinUri(uri);

        // Session profile JSON (Mojang or Authlib sessionserver) masquerading as skin address.
        if (IsSessionProfileUri(uri))
        {
            string? textureUrl = await ResolveTextureUrlFromSessionProfileAsync(uri).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(textureUrl))
                return null;
            uri = NormalizeSkinUri(new Uri(textureUrl));
        }

        string localPath = await EnsureCachedSkinAsync(uri, CancellationToken.None).ConfigureAwait(false);
        return await File.ReadAllBytesAsync(localPath).ConfigureAwait(false);
    }

    private static bool IsSessionProfileUri(Uri uri)
    {
        string path = uri.AbsolutePath;
        return path.Contains("/session/minecraft/profile/", StringComparison.OrdinalIgnoreCase) ||
               uri.Host.Contains("sessionserver.mojang.com", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string?> ResolveTextureUrlFromUuidAsync(string uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid) || uuid.Length != 32)
            return null;

        Uri profileUri = new("https://sessionserver.mojang.com/session/minecraft/profile/" + uuid);
        return await ResolveTextureUrlFromSessionProfileAsync(profileUri).ConfigureAwait(false);
    }

    private static async Task<string?> ResolveTextureUrlFromSessionProfileAsync(Uri profileUri)
    {
        using HttpResponseMessage response = await SkinClient.GetAsync(profileUri, CancellationToken.None).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return null;

        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("properties", out JsonElement properties) ||
            properties.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (JsonElement property in properties.EnumerateArray())
        {
            if (property.ValueKind != JsonValueKind.Object)
                continue;
            string name = property.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() ?? "" : "";
            if (!string.Equals(name, "textures", StringComparison.OrdinalIgnoreCase))
                continue;
            string? encoded = property.TryGetProperty("value", out JsonElement valueEl) ? valueEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(encoded))
                continue;

            byte[] decoded = Convert.FromBase64String(encoded);
            using JsonDocument texturesDoc = JsonDocument.Parse(decoded);
            if (texturesDoc.RootElement.TryGetProperty("textures", out JsonElement textures) &&
                textures.TryGetProperty("SKIN", out JsonElement skin) &&
                skin.TryGetProperty("url", out JsonElement urlEl) &&
                urlEl.ValueKind == JsonValueKind.String)
            {
                return urlEl.GetString();
            }
        }

        return null;
    }

    private Task ClearIfCurrentAsync(int loadVersion)
    {
        return Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (loadVersion == _loadVersion)
                ClearImages();
        }).GetTask();
    }

    public void Clear()
    {
        Interlocked.Increment(ref _loadVersion);
        ClearImages();
    }

    private void ClearImages()
    {
        if (_frontImage is not null)
            _frontImage.Source = null;
        if (_backImage is not null)
            _backImage.Source = null;
        _faceBitmap?.Dispose();
        _hatBitmap?.Dispose();
        _faceBitmap = null;
        _hatBitmap = null;
    }

    private static HttpClient CreateSkinClient()
    {
        HttpClient client = new() { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PCL-N/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("image/png,image/*;q=0.8,*/*;q=0.5");
        return client;
    }

    private static Uri NormalizeSkinUri(Uri uri)
    {
        if (uri.Scheme == Uri.UriSchemeHttp &&
            (string.Equals(uri.Host, "textures.minecraft.net", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Host, "crafatar.com", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Host, "mc-heads.net", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.Contains("littleskin", StringComparison.OrdinalIgnoreCase)))
        {
            return new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
        }

        return uri;
    }

    private static async Task<string> EnsureCachedSkinAsync(Uri uri, CancellationToken cancellationToken)
    {
        string cacheRoot = Path.Combine(Path.GetTempPath(), "PCL-N", "Cache", "Skin");
        Directory.CreateDirectory(cacheRoot);
        string fileName = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(uri.AbsoluteUri)))
            .ToLowerInvariant() + ".png";
        string cachePath = Path.Combine(cacheRoot, fileName);
        if (File.Exists(cachePath) && new FileInfo(cachePath).Length > 32)
            return cachePath;

        using HttpResponseMessage response = await SkinClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        string temp = cachePath + ".download";
        await File.WriteAllBytesAsync(temp, bytes, cancellationToken).ConfigureAwait(false);
        if (File.Exists(cachePath))
            File.Delete(cachePath);
        File.Move(temp, cachePath);
        return cachePath;
    }

    private static string NormalizeUuid(string? uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return string.Empty;

        return new string(uuid.Where(static ch => ch is not ('-' or ' ')).ToArray()).ToLowerInvariant();
    }

    private static string? TryExtractUuid(string address)
    {
        // crafatar.com/skins/{uuid} or .../avatars/{uuid}
        string[] parts = address.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int i = parts.Length - 1; i >= 0; i--)
        {
            string token = parts[i].Split('?', 2)[0];
            string normalized = NormalizeUuid(token);
            if (normalized.Length == 32)
                return normalized;
        }

        return null;
    }

    public void BtnSkinSaveClick(object? sender, RoutedEventArgs e) => SaveRequested?.Invoke(this, EventArgs.Empty);

    public void RefreshClick(object? sender, RoutedEventArgs e) => RefreshRequested?.Invoke(this, EventArgs.Empty);

    public void BtnSkinCapeClick(object? sender, RoutedEventArgs e) => CapeRequested?.Invoke(this, EventArgs.Empty);

    private void BtnSkinSaveChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is MyMenuItem item)
            item.IsEnabled = !string.IsNullOrWhiteSpace(Address);
    }

    private void PanSkin_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (_shadow is not null)
            _shadow.Opacity = 0.8d;
    }

    private void PanSkin_PointerExited(object? sender, PointerEventArgs e)
    {
        if (_shadow is not null)
            _shadow.Opacity = 0.2d;
        _isSkinMouseDown = false;
        ControlVisualHelpers.SetCenterScale(this, 1d);
    }

    private void PanSkin_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isSkinMouseDown = true;
        ControlVisualHelpers.SetCenterScale(this, 0.9d);
        e.Handled = true;
    }

    private void PanSkin_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        ControlVisualHelpers.SetCenterScale(this, 1d);
        if (!_isSkinMouseDown)
            return;

        _isSkinMouseDown = false;
        Click?.Invoke(this, e);
        e.Handled = true;
    }

    private static RenderTargetBitmap? CropToBitmap(Bitmap source, int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
            return null;

        PixelSize size = source.PixelSize;
        if (x < 0 || y < 0 || x + width > size.Width || y + height > size.Height)
            return null;

        // Draw a cropped region into a new bitmap so we do not depend on CroppedBitmap quirks.
        RenderTargetBitmap target = new(new PixelSize(width, height), new Vector(96, 96));
        using (DrawingContext context = target.CreateDrawingContext())
        {
            context.DrawImage(
                source,
                new Rect(x, y, width, height),
                new Rect(0, 0, width, height));
        }

        return target;
    }
}
