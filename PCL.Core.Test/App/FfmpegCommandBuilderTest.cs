using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Media;

namespace PCL.Core.Test.App;

[TestClass]
public class FfmpegCommandBuilderTest
{
    [TestMethod]
    public void BuildsRawVideoPlaybackArguments()
    {
        var arguments = FfmpegCommandBuilder.BuildPlaybackArguments(
            @"C:\videos\a b.mp4",
            1280,
            721,
            30);

        Assert.Contains("-stream_loop", arguments.ToList());
        Assert.Contains("-an", arguments.ToList());
        Assert.Contains("rawvideo", arguments.ToList());
        Assert.Contains("bgra", arguments.ToList());
        Assert.Contains(@"C:\videos\a b.mp4", arguments.ToList());
        Assert.Contains("file,pipe", arguments.ToList());
        Assert.Contains(
            "fps=30,scale=1280:722,setsar=1",
            arguments.ToList());
    }

    [TestMethod]
    public void KeepsEvenOutputDimensions()
    {
        var arguments = FfmpegCommandBuilder.BuildPlaybackArguments(
            "a.mp4",
            1025,
            683,
            24);
        Assert.AreEqual(
            "fps=24,scale=1026:684,setsar=1",
            arguments.Single(value => value.StartsWith("fps=", StringComparison.Ordinal)));
    }
}