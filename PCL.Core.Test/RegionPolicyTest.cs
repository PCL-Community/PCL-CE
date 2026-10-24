using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Region;

namespace PCL.Core.Test;

[TestClass]
public class RegionPolicyTest
{
    [TestMethod]
    public void ChinaGeoRestrictsRegardlessOfLanguage()
    {
        var result = RegionPolicy.Evaluate(Signals(geo: "CN", languages: ["en-US"], locale: "en-US"));
        Assert.AreEqual(RegionClassification.Mainland, result.Classification);
    }

    [TestMethod]
    public void ChinaTimeZoneAndExplicitChinaLocaleRestrictEvenWithOtherGeo()
    {
        var result = RegionPolicy.Evaluate(Signals(geo: "US", languages: ["zh-CN", "en-US"], locale: "en-US",
            timeZone: "China Standard Time"));
        Assert.AreEqual(RegionClassification.Mainland, result.Classification);
    }

    [DataTestMethod]
    [DataRow("HK", "zh-HK", "Asia/Hong_Kong")]
    [DataRow("MO", "zh-MO", "Asia/Macau")]
    [DataRow("TW", "zh-TW", "Asia/Taipei")]
    [DataRow("SG", "zh-SG", "Asia/Singapore")]
    public void OtherChineseLocalesDoNotRestrict(string geo, string locale, string timeZone)
    {
        var result = RegionPolicy.Evaluate(Signals(geo, [locale], locale, timeZone));
        Assert.AreEqual(RegionClassification.Other, result.Classification);
        Assert.IsFalse(result.IsRestricted);
    }

    [TestMethod]
    public void MissingSignalsRestrictAsUnknown()
    {
        var result = RegionPolicy.Evaluate(new RegionSignals(null, false, [], false, null, false, null, false));
        Assert.AreEqual(RegionClassification.Unknown, result.Classification);
        Assert.IsTrue(result.IsRestricted);
        Assert.AreEqual("signals-unavailable", result.Reason);
    }

    [TestMethod]
    public void AvailableButInsufficientSignalsUseInsufficientReason()
    {
        var result = RegionPolicy.Evaluate(new RegionSignals(null, false, ["en"], true,
            null, false, "China Standard Time", true));
        Assert.AreEqual(RegionClassification.Unknown, result.Classification);
        Assert.AreEqual("insufficient-signals", result.Reason);
    }

    [TestMethod]
    public void ChinaTimeZoneAloneIsInsufficient()
    {
        var result = RegionPolicy.Evaluate(Signals(geo: "US", languages: ["en-US"], locale: "en-US",
            timeZone: "China Standard Time"));
        Assert.AreEqual(RegionClassification.Other, result.Classification);
    }

    [TestMethod]
    public void ChinaDeviceRegionRestrictsRegardlessOfOtherSignals()
    {
        var result = RegionPolicy.Evaluate(Signals(geo: "JP", locale: "ja-JP", timeZone: "Tokyo Standard Time",
            deviceRegion: 45, deviceRegionAvailable: true));
        Assert.AreEqual(RegionClassification.Mainland, result.Classification);
        Assert.AreEqual("device-region-cn", result.Reason);
        Assert.IsTrue(result.IsRestricted);
    }

    [TestMethod]
    public void ChinaDeviceRegionAloneRestricts()
    {
        var result = RegionPolicy.Evaluate(
            new RegionSignals(null, false, [], false, null, false, null, false, 45, true));
        Assert.AreEqual(RegionClassification.Mainland, result.Classification);
        Assert.IsTrue(result.IsRestricted);
    }

    [TestMethod]
    public void NonChinaDeviceRegionDoesNotOverrideOtherSignals()
    {
        var result = RegionPolicy.Evaluate(Signals(geo: "JP", locale: "ja-JP", timeZone: "Tokyo Standard Time",
            deviceRegion: 242, deviceRegionAvailable: true));
        Assert.AreEqual(RegionClassification.Other, result.Classification);
        Assert.IsFalse(result.IsRestricted);
    }

    [TestMethod]
    public void NonChinaDeviceRegionAloneIsInsufficient()
    {
        var result = RegionPolicy.Evaluate(
            new RegionSignals(null, false, [], false, null, false, null, false, 242, true));
        Assert.AreEqual(RegionClassification.Unknown, result.Classification);
        Assert.IsTrue(result.IsRestricted);
    }

    [TestMethod]
    public void DeviceRegionUsesWindowsGeoIdNotIsoNumericCode()
    {
        // GeoID 156 是马拉维，不是中国大陆（ISO 数字码 156）
        var result = RegionPolicy.Evaluate(
            new RegionSignals(null, false, [], false, null, false, null, false, 156, true));
        Assert.AreEqual(RegionClassification.Unknown, result.Classification);
        Assert.AreNotEqual("device-region-cn", result.Reason);
    }

    [TestMethod]
    public void OneWeakLocaleSignalWithoutGeoOrTimeZoneIsUnknown()
    {
        var result = RegionPolicy.Evaluate(new RegionSignals(null, false, ["zh-CN"], true,
            null, false, null, false));
        Assert.AreEqual(RegionClassification.Unknown, result.Classification);
        Assert.IsTrue(result.IsRestricted);
    }

    [TestMethod]
    public void SecondaryPreferredLanguageDoesNotCountAsTheSystemDisplayLanguage()
    {
        var result = RegionPolicy.Evaluate(Signals(geo: "US", languages: ["en-US", "zh-CN"],
            locale: "en-US", timeZone: "China Standard Time"));
        Assert.AreEqual(RegionClassification.Other, result.Classification);
        Assert.IsFalse(result.IsRestricted);
    }

    [TestMethod]
    public void ExplicitOtherLocaleAndOtherTimeZoneWithoutGeoAreAllowed()
    {
        var result = RegionPolicy.Evaluate(new RegionSignals(null, false, ["en-US"], true,
            "en-US", true, "America/Los_Angeles", true));
        Assert.AreEqual(RegionClassification.Other, result.Classification);
        Assert.IsFalse(result.IsRestricted);
    }

    private static RegionSignals Signals(string? geo = "US", IReadOnlyList<string>? languages = null,
        string? locale = "en-US", string? timeZone = "UTC", int? deviceRegion = null,
        bool deviceRegionAvailable = false) =>
        new(geo, true, languages ?? ["en-US"], true, locale, true, timeZone, true,
            deviceRegion, deviceRegionAvailable);
}
