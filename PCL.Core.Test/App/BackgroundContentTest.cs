using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Tools;

namespace PCL.Core.Test.App;

[TestClass]
public class BackgroundContentTest
{
    [TestMethod]
    public void RecognizesVideoExtensions()
    {
        Assert.IsTrue(BackgroundContent.IsVideo("a.mp4"));
        Assert.IsTrue(BackgroundContent.IsVideo("a.MKV"));
        Assert.IsTrue(BackgroundContent.IsVideo("a.webm"));
        Assert.IsTrue(BackgroundContent.IsVideo("a.mov"));
        Assert.IsFalse(BackgroundContent.IsVideo("a.png"));
    }

    [TestMethod]
    public void RecognizesImageExtensions()
    {
        Assert.IsTrue(BackgroundContent.IsImage("a.png"));
        Assert.IsTrue(BackgroundContent.IsImage("a.JPG"));
        Assert.IsTrue(BackgroundContent.IsImage("a.webp"));
        Assert.IsTrue(BackgroundContent.IsImage("a.avif"));
        Assert.IsFalse(BackgroundContent.IsImage("a.mp4"));
    }

    [TestMethod]
    public void RecognizesAnimatedImages()
    {
        Assert.IsTrue(BackgroundContent.IsAnimatedImage("a.gif"));
        Assert.IsTrue(BackgroundContent.IsAnimatedImage("a.webp"));
        Assert.IsFalse(BackgroundContent.IsAnimatedImage("a.png"));
    }

    [TestMethod]
    public void RejectsUnknownExtensions()
    {
        Assert.IsFalse(BackgroundContent.IsSupported("a.txt"));
        Assert.IsFalse(BackgroundContent.IsSupported("a.ini"));
    }

    [TestMethod]
    public void ReportsDirectoryErrors()
    {
        var path = Path.GetTempFileName();
        try
        {
            Assert.ThrowsExactly<IOException>(() => BackgroundContent.Collect(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}