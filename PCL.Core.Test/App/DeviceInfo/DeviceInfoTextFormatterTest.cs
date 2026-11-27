using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.DeviceInfo;

namespace PCL.Core.Test.App.DeviceInfo;

[TestClass]
public sealed class DeviceInfoTextFormatterTest
{
    [TestMethod]
    public void FormatIncludesSummaryAndIndentsMultilineValues()
    {
        var snapshot = new DeviceInfoSnapshot(
            "Test OS",
            ["Test device", "Test summary"],
            "Test launcher",
            [
                new DeviceInfoSection(
                    DeviceInfoSectionKind.Graphics,
                    "Setup.Log.Device.Section.Graphics",
                    [
                        new DeviceInfoRow(
                            "Setup.Log.Device.Field.GpuName",
                            "Test GPU\nDriver version: 1.0",
                            [1])
                    ])
            ],
            new DateTimeOffset(2026, 11, 22, 12, 0, 0, TimeSpan.FromHours(8)));

        var text = DeviceInfoTextFormatter.Format(snapshot);

        Assert.Contains("Test OS", text);
        Assert.Contains("Test summary", text);
        Assert.Contains("Test GPU", text);
        Assert.Contains("    Driver version: 1.0", text);
    }
}