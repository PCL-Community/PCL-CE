// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App;
using PCL.Core.UI.Theme;

namespace PCL.Core.Test.UI.Theme;

[TestClass]
public sealed class ThemeServiceTest
{
    [TestMethod]
    public void WindowsOnlyNormalizesSystemAccentTheme()
    {
        Assert.IsFalse(ThemeService.IsSystemAccentThemeSupported);
        Assert.AreEqual(
            ThemeService.WindowsSystemAccentFallback,
            ThemeService.NormalizeTheme(ColorTheme.SystemAccent));

        Assert.AreEqual(ColorTheme.SkyBlue, ThemeService.NormalizeTheme(ColorTheme.SkyBlue));
        Assert.AreEqual(ColorTheme.CatBlue, ThemeService.NormalizeTheme(ColorTheme.CatBlue));
        Assert.AreEqual(ColorTheme.DeathBlue, ThemeService.NormalizeTheme(ColorTheme.DeathBlue));
        Assert.AreEqual(ColorTheme.HmclBlue, ThemeService.NormalizeTheme(ColorTheme.HmclBlue));
    }
}
