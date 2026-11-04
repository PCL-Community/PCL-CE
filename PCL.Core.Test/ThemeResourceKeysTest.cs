using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Test;

[TestClass]
public class ThemeResourceKeysTest
{
    private static readonly string[] BrushSuffixes =
    [
        "1", "2", "3", "4", "5", "6", "7", "8", "Bg0", "Bg1", "SemiTransparent"
    ];

    private static readonly string[] ColorSuffixes = ["1", "2", "3", "4", "5", "6", "7", "8", "Bg0", "Bg1"];

    private static readonly string[] GraySuffixes =
    [
        "1", "2", "3", "4", "5", "6", "7", "8",
        "HalfWhite", "SemiWhite", "White", "Transparent", "TransparentBackground",
        "Background", "ToolTip", "RedBack", "Memory"
    ];

    private static readonly string[] FixedKeys =
    [
        "ColorBrushRedLight", "ColorBrushRedDark", "ColorBrushFatal", "ColorBrushError",
        "ColorBrushWarn", "ColorBrushInfo", "ColorBrushInfoDark", "ColorBrushDebug",
        "ColorObjectMsgBoxShadow"
    ];

    [TestMethod]
    public void ThemeServiceKeysShouldCoverWpfPalette()
    {
        var produced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var suffix in BrushSuffixes) produced.Add("ColorBrush" + suffix);
        foreach (var suffix in ColorSuffixes) produced.Add("ColorObject" + suffix);
        foreach (var suffix in GraySuffixes) produced.Add("ColorBrushGray" + suffix);
        foreach (var suffix in GraySuffixes.Where(suffix => suffix is not ("HalfWhite" or "SemiWhite" or "White"
                     or "Transparent" or "TransparentBackground" or "Background" or "ToolTip" or "RedBack" or "Memory")))
            produced.Add("ColorObjectGray" + suffix);
        foreach (var suffix in GraySuffixes)
            if (suffix is "HalfWhite" or "SemiWhite" or "White" or "Transparent"
                or "TransparentBackground" or "Background" or "ToolTip" or "RedBack" or "Memory")
                produced.Add("ColorBrush" + suffix);
        foreach (var suffix in FixedKeys) produced.Add(suffix);

        var wpfKeys = LoadWpfColorKeys();
        Assert.IsTrue(wpfKeys.Count >= 50, $"WPF 色表键数量异常：{wpfKeys.Count}");

        var missing = wpfKeys.Where(key => !produced.Contains(key)).ToArray();
        Assert.IsEmpty(missing, $"以下 WPF 色表键没有运行时来源：{string.Join(", ", missing)}");
    }

    private static HashSet<string> LoadWpfColorKeys()
    {
        var path = Path.Combine(GetRepositoryRoot(), "Plain Craft Launcher 2", "Application.xaml");
        var document = XDocument.Load(path);
        var keyName = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");
        return document.Descendants()
            .Select(element => element.Attribute(keyName)?.Value)
            .Where(key => key is not null
                          && (key.StartsWith("ColorBrush", StringComparison.Ordinal)
                              || key.StartsWith("ColorObject", StringComparison.Ordinal)))
            .Select(key => key!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "PCL.Core"))) return directory.FullName;
            directory = directory.Parent;
        }

        Assert.Fail("无法定位仓库根目录");
        return string.Empty;
    }
}
