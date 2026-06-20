// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Nodes;
using PCL.Online;

DateTimeOffset updatedAt = new(2026, 6, 20, 1, 2, 3, TimeSpan.Zero);
var document = new CloudUserDocument
{
    MsId = "aot-smoke",
    Account = new CloudSyncSection
    {
        Data = new JsonObject { ["name"] = "PCL N" },
        UpdatedAt = updatedAt
    }
};

string json = OnlineJson.Serialize(document);
CloudUserDocument? restored = OnlineJson.Deserialize<CloudUserDocument>(json);
string legalText = FirstLaunchService.LoadFullText();
string sharedDataDirectory = OnlineRuntime.Host.SharedDataDirectory;

bool valid =
    restored?.MsId == document.MsId &&
    restored.Account?.Data?["name"]?.ToString() == "PCL N" &&
    restored.Account?.UpdatedAt == updatedAt &&
    !string.IsNullOrWhiteSpace(legalText) &&
    Path.IsPathFullyQualified(sharedDataDirectory);

return valid ? 0 : 1;
