// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace PCL.Desktop.Hosting;

internal sealed record PclMetadata(
    string Name,
    PclMetadataVersion Version,
    string Branch,
    string Commit,
    string Repository,
    string Sponsor,
    IReadOnlyList<PclMetadataLicense> Licenses)
{
    private const string ResourceName = "PCL.Desktop.metadata.json";

    public static PclMetadata Current { get; } = Load();

    public string DisplayVersion => string.IsNullOrWhiteSpace(Version.Suffix)
        ? Version.Base
        : Version.Base + " " + Version.Suffix;

    private static PclMetadata Load()
    {
        try
        {
            using Stream? stream = typeof(PclMetadata).Assembly.GetManifestResourceStream(ResourceName);
            if (stream is not null && JsonSerializer.Deserialize(stream, PclMetadataJsonContext.Default.PclMetadata) is { } metadata)
                return metadata;
        }
        catch (JsonException)
        {
        }

        return new PclMetadata(
            "Plain Craft Launcher N Edition",
            new PclMetadataVersion("dev", string.Empty, string.Empty, 0),
            "dev",
            "local",
            "https://github.com/MuXue1230-owo/PCL-N",
            "https://ifdian.net/a/pclne",
            []);
    }
}

internal sealed record PclMetadataVersion(
    [property: JsonPropertyName("base")] string Base,
    string Upstream,
    string Suffix,
    int Code);

internal sealed record PclMetadataLicense(string Name, string Info, string Website, string License);

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(PclMetadata))]
internal sealed partial class PclMetadataJsonContext : JsonSerializerContext;
