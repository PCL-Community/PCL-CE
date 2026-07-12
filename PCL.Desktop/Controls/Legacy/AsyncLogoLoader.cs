// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace PCL.Desktop.Controls.Legacy;

/// <summary>
/// Async logo/icon loader with WPF-style NoIcon placeholder and disk cache.
/// Never blocks the UI thread on network I/O.
/// </summary>
internal static class AsyncLogoLoader
{
    public const string PlaceholderAvares = "avares://PCL.Desktop/Assets/Legacy/Icons/NoIcon.png";

    private static readonly HttpClient Client = CreateClient();
    private static readonly object CacheGate = new();
    private static Bitmap? _placeholder;
    private static readonly Dictionary<string, Bitmap> MemoryCache = new(StringComparer.OrdinalIgnoreCase);

    public static Bitmap GetPlaceholder()
    {
        if (_placeholder is not null)
            return _placeholder;

        try
        {
            using Stream stream = AssetLoader.Open(new Uri(PlaceholderAvares));
            _placeholder = new Bitmap(stream);
        }
        catch
        {
            _placeholder = new WriteableBitmap(new PixelSize(1, 1), new Vector(96, 96));
        }

        return _placeholder;
    }

    public static Bitmap? TryLoadLocal(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        try
        {
            if (File.Exists(address))
            {
                using Stream fs = File.OpenRead(address);
                return MaybeCropSkinHead(new Bitmap(fs), address);
            }

            if (address.StartsWith("avares://", StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(address, UriKind.Absolute, out Uri? avares))
            {
                using Stream stream = AssetLoader.Open(avares);
                return MaybeCropSkinHead(new Bitmap(stream), address);
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    public static bool IsRemote(string? address) =>
        !string.IsNullOrWhiteSpace(address) &&
        (address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
         address.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    public static bool IsUuidSkin(string? address) =>
        !string.IsNullOrWhiteSpace(address) &&
        address.StartsWith("uuid:", StringComparison.OrdinalIgnoreCase);

    public static bool IsLoadableLogo(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;
        if (IsRemote(address))
            return true;
        if (IsUuidSkin(address))
            return true;
        if (address.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            return true;
        if (File.Exists(address))
            return true;

        string path = address.Split('?', 2)[0];
        return path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Loads remote/local logo asynchronously. Invokes <paramref name="onLoaded"/> on the UI thread.
    /// Returns a generation token so callers can ignore stale results.
    /// </summary>
    public static int BeginLoad(string address, int generation, Action<int, Bitmap?> onLoaded)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            onLoaded(generation, null);
            return generation;
        }

        Bitmap? local = TryLoadLocal(address);
        if (local is not null)
        {
            onLoaded(generation, local);
            return generation;
        }

        // uuid: / remote / session profile — resolve off UI thread.
        if (!IsRemote(address) && !IsUuidSkin(address))
        {
            onLoaded(generation, null);
            return generation;
        }

        lock (CacheGate)
        {
            if (MemoryCache.TryGetValue(address, out Bitmap? cached))
            {
                onLoaded(generation, cached);
                return generation;
            }
        }

        _ = Task.Run(async () =>
        {
            try
            {
                string resolved = address;
                if (IsUuidSkin(address))
                {
                    string uuid = NormalizeUuid(address["uuid:".Length..]);
                    string? textureUrl = await ResolveMojangTextureUrlAsync(uuid).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(textureUrl))
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => onLoaded(generation, null));
                        return;
                    }

                    resolved = textureUrl;
                }

                string cachePath = await EnsureCachedFileAsync(resolved).ConfigureAwait(false);
                Bitmap bitmap;
                await using (FileStream fs = File.OpenRead(cachePath))
                {
                    Bitmap raw = new(fs);
                    bitmap = MaybeCropSkinHead(raw, resolved);
                }

                lock (CacheGate)
                    MemoryCache[address] = bitmap;

                await Dispatcher.UIThread.InvokeAsync(() => onLoaded(generation, bitmap));
            }
            catch
            {
                await Dispatcher.UIThread.InvokeAsync(() => onLoaded(generation, null));
            }
        });

        return generation;
    }

    private static string NormalizeUuid(string? uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid))
            return string.Empty;
        return new string(uuid.Where(static ch => ch is not ('-' or ' ')).ToArray()).ToLowerInvariant();
    }

    private static async Task<string?> ResolveMojangTextureUrlAsync(string uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid) || uuid.Length != 32)
            return null;

        string profileUrl = "https://sessionserver.mojang.com/session/minecraft/profile/" + uuid;
        byte[] bytes = await DownloadBytesAsync(profileUrl).ConfigureAwait(false);
        return TryParseSkinTextureUrl(bytes);
    }

    private static async Task<string> EnsureCachedFileAsync(string url)
    {
        string root = Path.Combine(Path.GetTempPath(), "PCL-N", "Cache", "Logos");
        Directory.CreateDirectory(root);
        string name = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(url)))
            .ToLowerInvariant() + ".img";
        string path = Path.Combine(root, name);
        if (File.Exists(path) && new FileInfo(path).Length > 16)
            return path;

        byte[] bytes = await DownloadBytesAsync(url).ConfigureAwait(false);

        // Authlib / Mojang session profile JSON → real texture PNG URL.
        if (LooksLikeJson(bytes) && TryParseSkinTextureUrl(bytes) is { } textureUrl)
            bytes = await DownloadBytesAsync(textureUrl).ConfigureAwait(false);

        string temp = path + ".download";
        await File.WriteAllBytesAsync(temp, bytes).ConfigureAwait(false);
        if (File.Exists(path))
            File.Delete(path);
        File.Move(temp, path);
        return path;
    }

    private static async Task<byte[]> DownloadBytesAsync(string url)
    {
        using HttpResponseMessage response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
    }

    private static bool LooksLikeJson(byte[] bytes) =>
        bytes.Length > 2 && bytes[0] is (byte)'{' or (byte)'[';

    private static string? TryParseSkinTextureUrl(byte[] jsonBytes)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(jsonBytes);
            if (!document.RootElement.TryGetProperty("properties", out JsonElement properties) ||
                properties.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (JsonElement property in properties.EnumerateArray())
            {
                string name = property.TryGetProperty("name", out JsonElement n) ? n.GetString() ?? "" : "";
                if (!string.Equals(name, "textures", StringComparison.OrdinalIgnoreCase))
                    continue;
                string? encoded = property.TryGetProperty("value", out JsonElement v) ? v.GetString() : null;
                if (string.IsNullOrWhiteSpace(encoded))
                    continue;

                byte[] decoded = Convert.FromBase64String(encoded);
                using JsonDocument texturesDoc = JsonDocument.Parse(decoded);
                if (texturesDoc.RootElement.TryGetProperty("textures", out JsonElement textures) &&
                    textures.TryGetProperty("SKIN", out JsonElement skin) &&
                    skin.TryGetProperty("url", out JsonElement urlEl))
                {
                    return urlEl.GetString();
                }
            }
        }
        catch
        {
            // not a profile payload
        }

        return null;
    }

    /// <summary>
    /// Crop dual-layer head from full Minecraft skin atlases only (never crop mod icons).
    /// WPF MySkin: face at (8,8) 8×8 + hat overlay at (40,8) 8×8.
    /// </summary>
    private static Bitmap MaybeCropSkinHead(Bitmap source, string? address)
    {
        if (!LooksLikeMinecraftSkinAddress(address))
            return source;

        try
        {
            PixelSize size = source.PixelSize;
            if (size.Width is not (64 or 128) || size.Height is not (32 or 64 or 128))
                return source;

            int scale = Math.Max(1, size.Width / 64);
            int w = scale * 8;
            int h = scale * 8;
            // Face (layer 1) + hat (layer 2) composited like MySkin dual-layer head.
            RenderTargetBitmap target = new(new PixelSize(w, h), new Vector(96, 96));
            using (DrawingContext context = target.CreateDrawingContext())
            {
                Rect dest = new(0, 0, w, h);
                context.DrawImage(source, new Rect(scale * 8, scale * 8, w, h), dest);
                // Hat layer only when the skin atlas has the outer layer strip.
                if (size.Width >= scale * 48 && size.Height >= scale * 16)
                    context.DrawImage(source, new Rect(scale * 40, scale * 8, w, h), dest);
            }

            source.Dispose();
            return target;
        }
        catch
        {
            return source;
        }
    }

    private static bool LooksLikeMinecraftSkinAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;

        return IsUuidSkin(address) ||
               address.Contains("Legacy/Skins", StringComparison.OrdinalIgnoreCase) ||
               address.Contains("/textures/", StringComparison.OrdinalIgnoreCase) ||
               address.Contains("textures.minecraft.net", StringComparison.OrdinalIgnoreCase) ||
               address.Contains("session/minecraft/profile", StringComparison.OrdinalIgnoreCase) ||
               address.Contains("sessionserver", StringComparison.OrdinalIgnoreCase) ||
               address.EndsWith("Steve.png", StringComparison.OrdinalIgnoreCase) ||
               address.EndsWith("Alex.png", StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateClient()
    {
        HttpClient client = new() { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PCL-N/1.0");
        return client;
    }
}
