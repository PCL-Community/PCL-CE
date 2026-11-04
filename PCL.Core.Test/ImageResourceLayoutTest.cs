using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Test;

[TestClass]
public class ImageResourceLayoutTest
{
    private static readonly string[] ExpectedImages =
    [
        "Blocks/Grass.png",
        "Blocks/CommandBlock.png",
        "Blocks/CobbleStone.png",
        "Blocks/Anvil.png",
        "Blocks/NeoForge.png",
        "Blocks/Cleanroom.png",
        "Blocks/Fabric.png",
        "Blocks/Quilt.png",
        "Blocks/GrassPath.png",
        "Blocks/Egg.png",
        "Blocks/GoldBlock.png",
        "Blocks/LabyMod.png",
        "Blocks/RedstoneBlock.png",
        "Icons/Folder.png",
        "Icons/NoIcon.png",
        "Icons/DefaultServer.png",
        "icon.ico",
        "icon.png",
        "Backgrounds/server_bg.png"
    ];

    [TestMethod]
    public void BusinessImagePathsShouldExistInAvaloniaAssets()
    {
        var imageRoot = Path.Combine(GetRepositoryRoot(), "PCL.Launcher", "Assets", "Images");
        Assert.IsTrue(Directory.Exists(imageRoot), $"图片资源目录不存在：{imageRoot}");

        var missing = ExpectedImages
            .Where(relative => !File.Exists(Path.Combine(imageRoot, relative.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();
        Assert.IsEmpty(missing, $"以下业务图片路径在 Assets/Images 中不存在：{string.Join(", ", missing)}");
    }

    [TestMethod]
    public void ImageAssetCountShouldMatchWpfReference()
    {
        var avaloniaRoot = Path.Combine(GetRepositoryRoot(), "PCL.Launcher", "Assets", "Images");
        var wpfRoot = Path.Combine(GetRepositoryRoot(), "Plain Craft Launcher 2", "Images");
        if (!Directory.Exists(wpfRoot)) Assert.Inconclusive("WPF 参考图片目录不存在");

        var avaloniaCount = Directory.EnumerateFiles(avaloniaRoot, "*", SearchOption.AllDirectories).Count();
        var wpfCount = Directory.EnumerateFiles(wpfRoot, "*", SearchOption.AllDirectories).Count();
        Assert.AreEqual(wpfCount, avaloniaCount, "图片资源数量与 WPF 参考不一致");
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
