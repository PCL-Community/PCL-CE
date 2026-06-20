// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Online.Test;

[TestClass]
public sealed class OnlineInfrastructureTests
{
    [TestMethod]
    public void DefaultRuntimeHost_ShouldUseAnAbsoluteSharedDataDirectory()
    {
        string directory = OnlineRuntime.Host.SharedDataDirectory;

        Assert.IsFalse(string.IsNullOrWhiteSpace(directory));
        Assert.IsTrue(Path.IsPathFullyQualified(directory));
    }

    [TestMethod]
    public void FirstLaunchService_ShouldLoadEmbeddedLegalDocuments()
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(FirstLaunchService.LoadTerms()));
        Assert.IsFalse(string.IsNullOrWhiteSpace(FirstLaunchService.LoadPrivacy()));
    }
}
