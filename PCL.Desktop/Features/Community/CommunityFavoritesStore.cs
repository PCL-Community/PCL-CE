// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using System.Text.Json.Serialization;
using PCL.Platform.Paths;

namespace PCL.Desktop.Features.Community;

public sealed record CommunityFavoriteEntry(
    CommunityResourceEntry Entry,
    CommunityResourceCategory Category,
    DateTimeOffset AddedAt);

public sealed class CommunityFavoritesStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private List<CommunityFavoriteEntry> _items;

    public CommunityFavoritesStore()
        : this(CreateDefaultPath())
    {
    }

    public CommunityFavoritesStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
        _items = Load(_path);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<CommunityFavoriteEntry> Items
    {
        get
        {
            lock (_gate)
                return _items.OrderByDescending(static item => item.AddedAt).ToArray();
        }
    }

    public bool Contains(CommunityResourceEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
            return _items.Any(item => IsSameProject(item.Entry, entry));
    }

    public bool Toggle(CommunityResourceEntry entry, CommunityResourceCategory category)
    {
        ArgumentNullException.ThrowIfNull(entry);
        bool added;
        lock (_gate)
        {
            List<CommunityFavoriteEntry> previous = [.. _items];
            int index = _items.FindIndex(item => IsSameProject(item.Entry, entry));
            if (index >= 0)
            {
                _items.RemoveAt(index);
                added = false;
            }
            else
            {
                _items.Add(new CommunityFavoriteEntry(entry, category, DateTimeOffset.UtcNow));
                added = true;
            }

            try
            {
                Save(_path, _items);
            }
            catch
            {
                _items = previous;
                throw;
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return added;
    }

    private static bool IsSameProject(CommunityResourceEntry left, CommunityResourceEntry right) =>
        left.Source == right.Source &&
        string.Equals(left.ProjectId, right.ProjectId, StringComparison.OrdinalIgnoreCase);

    private static List<CommunityFavoriteEntry> Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return [];
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, CommunityFavoritesJsonContext.Default.ListCommunityFavoriteEntry) ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    private static void Save(string path, IReadOnlyList<CommunityFavoriteEntry> items)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        string temporary = path + ".tmp";
        List<CommunityFavoriteEntry> serializableItems = [.. items];
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(serializableItems, CommunityFavoritesJsonContext.Default.ListCommunityFavoriteEntry));
        File.Move(temporary, path, overwrite: true);
    }

    private static string CreateDefaultPath()
    {
        DefaultPlatformPathProvider paths = new();
        return Path.Combine(paths.ApplicationDataDirectory, "PCL-N", "community-favorites.json");
    }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true)]
[JsonSerializable(typeof(List<CommunityFavoriteEntry>))]
internal sealed partial class CommunityFavoritesJsonContext : JsonSerializerContext;
