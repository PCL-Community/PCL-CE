// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Net;
using PCL.Core.Logging;
using PCL.Core.Serialization;

namespace PCL.Core.Portable.Test;

[TestClass]
public sealed class PortableInfrastructureTests
{
    [TestMethod]
    public void PortableJson_AllowsCommentsTrailingCommasAndStringNumbers()
    {
        var node = PortableJson.ParseObject("""
            {
                // launcher metadata can contain comments in local debug fixtures
                "count": "42",
            }
            """);

        Assert.AreEqual("42", node["COUNT"]?.ToString());
        var model = JsonSerializer.Deserialize<JsonModel>(
            """{"count":"42"}""",
            PortableJson.SerializerOptions);
        Assert.IsNotNull(model);
        Assert.AreEqual(42, model.Count);
    }

    [TestMethod]
    public void PortableLog_PublishesStructuredEntries()
    {
        var entries = new List<PortableLogEntry>();
        void Capture(PortableLogEntry entry) => entries.Add(entry);

        PortableLog.Written += Capture;
        try
        {
            PortableLog.Warn("PortableTest", "hello");
        }
        finally
        {
            PortableLog.Written -= Capture;
        }

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual(PortableLogLevel.Warn, entries[0].Level);
        Assert.AreEqual("PortableTest", entries[0].Module);
        Assert.AreEqual("hello", entries[0].Message);
        Assert.AreNotEqual(default, entries[0].Timestamp);
    }

    [TestMethod]
    public async Task PortableHttp_ReadStringAsync_UsesResponseCancellationAwareApi()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("portable", Encoding.UTF8, "text/plain")
        };

        var value = await PortableHttp.ReadStringAsync(response, TestContext.CancellationToken);

        Assert.AreEqual("portable", value);
    }

    public TestContext TestContext { get; set; } = null!;

    private sealed record JsonModel(int Count);
}
