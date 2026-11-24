using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Platform;
using PCL.Core.UI;

namespace PCL.Core.Test.Platform;

[TestClass]
public sealed class UiModuleFactoryTest
{
    [TestMethod]
    public void CreateRejectsUnknownPlatform()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            UiModuleFactory.Create((PlatformKind)int.MaxValue));
    }
}