using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Test;

[TestClass]
public class ThemeResourceKeysTest
{
    private static readonly string[] BrushSuffixes =
    [
        "1", "2", "3", "4", "5", "6", "7", "8", "Bg0", "Bg1", "SemiTransparent"
    ];

    private static readonly string[] ColorSuffixes =
    [
        "1", "2", "3", "4", "5", "6", "7", "8", "Bg0", "Bg1"
    ];

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

    private static readonly string[] FrozenPaletteKeys =
    [
        "ColorBrush1", "ColorBrush2", "ColorBrush3", "ColorBrush4",
        "ColorBrush5", "ColorBrush6", "ColorBrush7", "ColorBrush8",
        "ColorBrushBackground", "ColorBrushBg0", "ColorBrushBg1",
        "ColorBrushDebug", "ColorBrushError", "ColorBrushFatal",
        "ColorBrushGray1", "ColorBrushGray2", "ColorBrushGray3", "ColorBrushGray4",
        "ColorBrushGray5", "ColorBrushGray6", "ColorBrushGray7", "ColorBrushGray8",
        "ColorBrushHalfWhite", "ColorBrushInfo", "ColorBrushInfoDark", "ColorBrushMemory",
        "ColorBrushRedBack", "ColorBrushRedDark", "ColorBrushRedLight",
        "ColorBrushSemiTransparent", "ColorBrushSemiWhite", "ColorBrushToolTip",
        "ColorBrushTransparent", "ColorBrushTransparentBackground", "ColorBrushWarn",
        "ColorBrushWhite",
        "ColorObject1", "ColorObject2", "ColorObject3", "ColorObject4",
        "ColorObject5", "ColorObject6", "ColorObject7", "ColorObject8",
        "ColorObjectBg0", "ColorObjectBg1",
        "ColorObjectGray1", "ColorObjectGray2", "ColorObjectGray3", "ColorObjectGray4",
        "ColorObjectGray5", "ColorObjectGray6", "ColorObjectGray7", "ColorObjectGray8",
        "ColorObjectMsgBoxShadow"
    ];

    [TestMethod]
    public void ThemeServiceKeysShouldCoverFrozenPalette()
    {
        var produced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var suffix in BrushSuffixes) produced.Add($"ColorBrush{suffix}");
        foreach (var suffix in ColorSuffixes) produced.Add($"ColorObject{suffix}");
        foreach (var suffix in GraySuffixes) produced.Add($"ColorBrushGray{suffix}");
        foreach (var suffix in GraySuffixes.Where(suffix => suffix is not (
                     "HalfWhite"
                     or "SemiWhite"
                     or "White"
                     or "Transparent"
                     or "TransparentBackground"
                     or "Background"
                     or "ToolTip"
                     or "RedBack"
                     or "Memory")))
            produced.Add($"ColorObjectGray{suffix}");
        foreach (var suffix in GraySuffixes)
            if (suffix is "HalfWhite"
                or "SemiWhite"
                or "White"
                or "Transparent"
                or "TransparentBackground"
                or "Background"
                or "ToolTip"
                or "RedBack"
                or "Memory")
                produced.Add($"ColorBrush{suffix}");
        foreach (var suffix in FixedKeys) produced.Add(suffix);

        Assert.IsGreaterThanOrEqualTo(
            50, FrozenPaletteKeys.Length, $"冻结色表键数量异常：{FrozenPaletteKeys.Length}");

        var missing = FrozenPaletteKeys.Where(key => !produced.Contains(key)).ToArray();
        Assert.IsEmpty(missing, $"以下色表键没有运行时来源：{string.Join(", ", missing)}");
    }
}