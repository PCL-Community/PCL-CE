// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using fNbt;

namespace PCL.Application.Instances;

public sealed class MinecraftServerListService
{
    public static async Task<IReadOnlyList<MinecraftServerEntry>> LoadAsync(
        string minecraftRoot,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(minecraftRoot);

        string serversFile = Path.Combine(minecraftRoot, "servers.dat");
        if (!File.Exists(serversFile))
            return [];

        await using FileStream stream = new(
            serversFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 16 * 1024,
            useAsync: true);

        NbtFile nbtFile = new();
        await Task.Run(() => nbtFile.LoadFromStream(stream, NbtCompression.AutoDetect), cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        NbtList? servers = nbtFile.RootTag.Get<NbtList>("servers");
        if (servers is null)
            return [];

        List<MinecraftServerEntry> result = new(servers.Count);
        foreach (NbtTag tag in servers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (tag is not NbtCompound server)
                continue;

            string name = server.Get<NbtString>("name")?.Value ?? "Unknown";
            string address = server.Get<NbtString>("ip")?.Value ?? "Unknown";
            string? icon = server.Get<NbtString>("icon")?.Value;
            result.Add(new MinecraftServerEntry(name, address, icon));
        }

        return result;
    }

    public static async Task AddAsync(
        string minecraftRoot,
        MinecraftServerEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(minecraftRoot);
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.Name))
            throw new ArgumentException("服务器名称不能为空。", nameof(entry));
        if (string.IsNullOrWhiteSpace(entry.Address))
            throw new ArgumentException("服务器地址不能为空。", nameof(entry));

        string serversFile = Path.Combine(Path.GetFullPath(minecraftRoot), "servers.dat");
        Directory.CreateDirectory(Path.GetDirectoryName(serversFile)
                                  ?? throw new InvalidOperationException("服务器列表文件没有父目录。"));

        await Task.Run(
                () =>
                {
                    NbtFile nbtFile = File.Exists(serversFile) ? LoadExistingServerFile(serversFile) : CreateEmptyServerFile();
                    NbtList servers = nbtFile.RootTag.Get<NbtList>("servers") ?? new NbtList("servers", NbtTagType.Compound);
                    if (servers.Parent is null)
                        nbtFile.RootTag.Add(servers);

                    servers.Add(new NbtCompound
                    {
                        new NbtString("name", entry.Name.Trim()),
                        new NbtString("ip", entry.Address.Trim())
                    });
                    if (!string.IsNullOrWhiteSpace(entry.Icon))
                        ((NbtCompound)servers[^1]).Add(new NbtString("icon", entry.Icon));

                    using FileStream stream = new(
                        serversFile,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None);
                    nbtFile.SaveToStream(stream, NbtCompression.GZip);
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static Task<bool> UpdateAsync(
        string minecraftRoot,
        MinecraftServerEntry original,
        MinecraftServerEntry updated,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(original);
        ValidateEntry(updated, nameof(updated));
        return MutateAsync(
            minecraftRoot,
            servers =>
            {
                int index = FindServerIndex(servers, original);
                if (index < 0 || servers[index] is not NbtCompound server)
                    return false;

                server["name"] = new NbtString("name", updated.Name.Trim());
                server["ip"] = new NbtString("ip", updated.Address.Trim());
                if (!string.IsNullOrWhiteSpace(updated.Icon))
                    server["icon"] = new NbtString("icon", updated.Icon);
                return true;
            },
            cancellationToken);
    }

    public static Task<bool> RemoveAsync(
        string minecraftRoot,
        MinecraftServerEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return MutateAsync(
            minecraftRoot,
            servers =>
            {
                int index = FindServerIndex(servers, entry);
                if (index < 0)
                    return false;

                servers.RemoveAt(index);
                return true;
            },
            cancellationToken);
    }

    private static async Task<bool> MutateAsync(
        string minecraftRoot,
        Func<NbtList, bool> mutate,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(minecraftRoot);
        ArgumentNullException.ThrowIfNull(mutate);
        string serversFile = Path.Combine(Path.GetFullPath(minecraftRoot), "servers.dat");
        if (!File.Exists(serversFile))
            return false;

        return await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    NbtFile nbtFile = LoadExistingServerFile(serversFile);
                    NbtList? servers = nbtFile.RootTag.Get<NbtList>("servers");
                    if (servers is null || !mutate(servers))
                        return false;

                    using FileStream stream = new(
                        serversFile,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None);
                    nbtFile.SaveToStream(stream, NbtCompression.GZip);
                    return true;
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static int FindServerIndex(NbtList servers, MinecraftServerEntry entry)
    {
        for (int index = 0; index < servers.Count; index++)
        {
            if (servers[index] is not NbtCompound server)
                continue;

            string name = server.Get<NbtString>("name")?.Value ?? string.Empty;
            string address = server.Get<NbtString>("ip")?.Value ?? string.Empty;
            if (string.Equals(name, entry.Name, StringComparison.Ordinal) &&
                string.Equals(address, entry.Address, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static void ValidateEntry(MinecraftServerEntry entry, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(entry, parameterName);
        if (string.IsNullOrWhiteSpace(entry.Name))
            throw new ArgumentException("服务器名称不能为空。", parameterName);
        if (string.IsNullOrWhiteSpace(entry.Address))
            throw new ArgumentException("服务器地址不能为空。", parameterName);
    }

    private static NbtFile LoadExistingServerFile(string serversFile)
    {
        try
        {
            using FileStream stream = new(
                serversFile,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            NbtFile nbtFile = new();
            nbtFile.LoadFromStream(stream, NbtCompression.AutoDetect);
            return nbtFile.RootTag is null ? CreateEmptyServerFile() : nbtFile;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            return CreateEmptyServerFile();
        }
    }

    private static NbtFile CreateEmptyServerFile()
    {
        NbtCompound rootTag = new("");
        rootTag.Add(new NbtList("servers", NbtTagType.Compound));
        return new NbtFile(rootTag);
    }
}
