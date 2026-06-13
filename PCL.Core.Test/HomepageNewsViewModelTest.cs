using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.ViewModel.Homepage;

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.Test;

[TestClass]
public class HomepageNewsViewModelTest
{
    [TestMethod]
    [DataRow("https://www.minecraft.net/zh-hans/article/example")]
    [DataRow("http://minecraft.net/news")]
    [DataRow("https://net-secondary.web.minecraft-services.net/api/v1.0/zh-cn/search")]
    [DataRow("https://learn.microsoft.com/gaming/")]
    public void IsSafeNewsLink_AllowsExpectedWebHosts(string url)
    {
        Assert.IsTrue(NewsViewModel.IsSafeNewsLink(url));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("calc.exe")]
    [DataRow("C:\\Windows\\System32\\calc.exe")]
    [DataRow("file:///C:/Windows/System32/calc.exe")]
    [DataRow("minecraft://open")]
    [DataRow("\\\\evil.example\\share\\payload.exe")]
    [DataRow("https://minecraft.net.evil.example/news")]
    [DataRow("https://evil.example/news")]
    public void IsSafeNewsLink_BlocksShellAndUnexpectedTargets(string? url)
    {
        Assert.IsFalse(NewsViewModel.IsSafeNewsLink(url));
    }
}
