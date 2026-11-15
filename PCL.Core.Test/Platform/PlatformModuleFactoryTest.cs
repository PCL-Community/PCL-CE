using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Platform;
using PCL.Platform;

namespace PCL.Core.Test.Platform;

[TestClass]
public sealed class PlatformModuleFactoryTest
{
    [TestMethod]
    public void CreateRejectsUnknownPlatform()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            PlatformModuleFactory.Create((PlatformKind)int.MaxValue));
    }
}