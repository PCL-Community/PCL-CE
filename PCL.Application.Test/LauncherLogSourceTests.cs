// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Logging;
using PCL.Core.Logging;

namespace PCL.Application.Test;

[TestClass]
public sealed class LauncherLogSourceTests
{
    [TestMethod]
    public void Source_BuffersEntriesAndEnforcesCapacity()
    {
        using PortableLauncherLogSource source = new(capacity: 2);

        PortableLog.Info("Test", "first");
        PortableLog.Warn("Test", "second");
        PortableLog.Error(
            new InvalidOperationException("failure"),
            "Test",
            "third");

        IReadOnlyList<LauncherLogEntry> entries = source.GetSnapshot();

        Assert.HasCount(2, entries);
        Assert.AreEqual("second", entries[0].Message);
        Assert.AreEqual("third", entries[1].Message);
        StringAssert.Contains(entries[1].ExceptionText, "failure");
    }

    [TestMethod]
    public void Clear_RemovesBufferedEntries()
    {
        using PortableLauncherLogSource source = new();
        PortableLog.Debug("Test", "entry");

        source.Clear();

        Assert.IsEmpty(source.GetSnapshot());
    }
}
