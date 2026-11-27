using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.OS.DeviceInfo;

namespace PCL.Core.Test.Utils.OS;

[TestClass]
public sealed class DeviceInfoFormatTest
{
    [TestMethod]
    public void FormatTimeZoneUsesRawId()
    {
        Assert.AreEqual(
            "ROC (UTC+08:00)",
            DeviceInfoFormat.FormatTimeZone("ROC", TimeSpan.FromHours(8)));
    }

    [TestMethod]
    public void FormatUptimeClampsNegativeValues()
    {
        Assert.AreEqual("00:00:00", DeviceInfoFormat.FormatUptime(TimeSpan.FromSeconds(-1)));
        Assert.AreEqual(
            "1.02:03:04",
            DeviceInfoFormat.FormatUptime(
                new TimeSpan(1, 2, 3, 4)));
    }

    [TestMethod]
    public void FormatUtcOffsetHandlesNegativeOffsets()
    {
        Assert.AreEqual("UTC-05:30", DeviceInfoFormat.FormatUtcOffset(TimeSpan.FromHours(-5.5)));
    }
}