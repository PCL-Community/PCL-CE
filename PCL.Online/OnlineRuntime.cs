// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace PCL.Online;

public interface IOnlineRuntimeHost
{
    string SharedDataDirectory { get; }

    string? GetSecret(string key);

    string Text(string key, params object?[] args);

    string GetString(string key);

    void SetString(string key, string value);

    bool GetBoolean(string key);

    void SetBoolean(string key, bool value);

    void Flush();

    ICloudSyncDataProvider CloudSync { get; }

    IRegionalDownloadPolicySink RegionalDownloadPolicy { get; }
}

public interface ICloudSyncDataProvider
{
    bool IsEnabled { get; }

    bool HasAnySectionEnabled { get; }

    Dictionary<string, JsonObject> BuildSnapshot();

    Task ApplySectionsAsync(IReadOnlyDictionary<string, JsonObject?> sections, bool overwriteAccount);
}

public interface IRegionalDownloadPolicySink
{
    bool Apply(ClientRegionPolicy policy);
}

public static class OnlineRuntime
{
    private static IOnlineRuntimeHost _host = new DefaultOnlineRuntimeHost();

    public static IOnlineRuntimeHost Host => _host;

    public static void Configure(IOnlineRuntimeHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    private sealed class DefaultOnlineRuntimeHost : IOnlineRuntimeHost
    {
        private readonly Dictionary<string, string> _strings = new(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _booleans = new(StringComparer.Ordinal);
        private readonly EmptyCloudSyncDataProvider _cloudSync = new();
        private readonly NoopRegionalDownloadPolicySink _regionalDownloadPolicy = new();

        public string SharedDataDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PCL_N");

        public ICloudSyncDataProvider CloudSync => _cloudSync;

        public IRegionalDownloadPolicySink RegionalDownloadPolicy => _regionalDownloadPolicy;

        public string? GetSecret(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return Environment.GetEnvironmentVariable($"PCL_{key}");
        }

        public string Text(string key, params object?[] args)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            return args.Length == 0 ? key : string.Format(key, args);
        }

        public string GetString(string key) => _strings.GetValueOrDefault(key, "");

        public void SetString(string key, string value) => _strings[key] = value;

        public bool GetBoolean(string key) => _booleans.GetValueOrDefault(key);

        public void SetBoolean(string key, bool value) => _booleans[key] = value;

        public void Flush()
        {
        }
    }

    private sealed class EmptyCloudSyncDataProvider : ICloudSyncDataProvider
    {
        public bool IsEnabled => false;

        public bool HasAnySectionEnabled => false;

        public Dictionary<string, JsonObject> BuildSnapshot() => new(StringComparer.Ordinal);

        public Task ApplySectionsAsync(IReadOnlyDictionary<string, JsonObject?> sections, bool overwriteAccount)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NoopRegionalDownloadPolicySink : IRegionalDownloadPolicySink
    {
        public bool Apply(ClientRegionPolicy policy) => false;
    }
}
