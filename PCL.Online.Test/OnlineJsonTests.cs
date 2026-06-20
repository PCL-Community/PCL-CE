// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Online.Test;

[TestClass]
public sealed class OnlineJsonTests
{
    [TestMethod]
    public void CloudSyncDocument_ShouldRoundTripWithSourceGeneratedMetadata()
    {
        DateTimeOffset updatedAt = new(2026, 6, 20, 1, 2, 3, TimeSpan.Zero);
        var document = new CloudUserDocument
        {
            MsId = "account-id",
            Account = new CloudSyncSection
            {
                Data = new JsonObject { ["name"] = "PCL N" },
                UpdatedAt = updatedAt
            }
        };

        string json = OnlineJson.Serialize(document);
        CloudUserDocument? restored = OnlineJson.Deserialize<CloudUserDocument>(json);

        Assert.IsNotNull(restored);
        Assert.AreEqual("account-id", restored.MsId);
        Assert.AreEqual("PCL N", restored.Account?.Data?["name"]?.ToString());
        Assert.AreEqual(updatedAt, restored.Account?.UpdatedAt);
    }

    [TestMethod]
    public void FriendModels_ShouldRoundTripWithSourceGeneratedMetadata()
    {
        DateTimeOffset createdAt = new(2026, 6, 20, 1, 2, 3, TimeSpan.Zero);
        List<OnlineFriendRequest> requests =
        [
            new("request-id", "profile-id", "Player", "pending", createdAt)
        ];

        string json = OnlineJson.Serialize(requests);
        List<OnlineFriendRequest>? restored = OnlineJson.Deserialize<List<OnlineFriendRequest>>(json);

        Assert.IsNotNull(restored);
        Assert.AreEqual(1, restored.Count);
        Assert.AreEqual("profile-id", restored[0].TargetProfileId);
        Assert.AreEqual(createdAt, restored[0].CreatedAt);
    }

    [TestMethod]
    public void RegionPolicy_ShouldReadStringEncodedNumbersAndCaseInsensitiveProperties()
    {
        ClientRegionPolicy? policy = OnlineJson.Deserialize<ClientRegionPolicy>(
            """{"countrycode":"CN","useDomesticMirror":true,"allowDomesticMirrorSwitch":false}""");

        Assert.IsNotNull(policy);
        Assert.AreEqual("CN", policy.CountryCode);
        Assert.IsTrue(policy.UseDomesticMirror);
        Assert.IsFalse(policy.AllowDomesticMirrorSwitch);
    }
}
