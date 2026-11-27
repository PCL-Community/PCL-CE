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
            new FfmpegPlaybackOptions(
                false,
                2,
                1,
                false,
                FfmpegPixelFormat.Bgra8888,
                30));

        Assert.Contains("-stream_loop", arguments.ToList());
        Assert.Contains("-an", arguments.ToList());
        Assert.Contains("rawvideo", arguments.ToList());
        Assert.Contains("bgra", arguments.ToList());
        Assert.Contains(@"C:\videos\a b.mp4", arguments.ToList());
        Assert.Contains("file,pipe", arguments.ToList());
        Assert.Contains(
            "fps=30,scale=1280:722:flags=bicubic,setsar=1",
            arguments.ToList());
        Assert.Contains("-filter_threads", arguments.ToList());
        Assert.Contains("-threads", arguments.ToList());
        Assert.DoesNotContain("-hwaccel", arguments);
    }

    [TestMethod]
    public void AddsHardwareAccelerationBeforeInput()
    {
        var arguments = FfmpegCommandBuilder.BuildPlaybackArguments(
            "a.mp4",
            1280,
            720,
            new FfmpegPlaybackOptions(
                true,
                2,
                1,
                false,
                FfmpegPixelFormat.Yuv420p,
                30));

        var hardwareIndex = Array.IndexOf([.. arguments], "-hwaccel");
        var inputIndex = Array.IndexOf([.. arguments], "-i");
        Assert.IsGreaterThanOrEqualTo(0, hardwareIndex);
        Assert.IsGreaterThan(hardwareIndex, inputIndex);
        Assert.AreEqual("auto", arguments[hardwareIndex + 1]);
        Assert.Contains("yuv420p", arguments.ToList());
    }

    [TestMethod]
    public void AddsLowDelayAndOmitsFpsFilterWhenSourceIsSlow()
    {
        var arguments = FfmpegCommandBuilder.BuildPlaybackArguments(
            "a.mp4",
            1280,
            720,
            new FfmpegPlaybackOptions(
                false,
                2,
                1,
                true,
                FfmpegPixelFormat.Yuv420p,
                null));

        Assert.Contains("+low_delay", arguments.ToList());
        Assert.Contains(
            "scale=1280:720:flags=bicubic,setsar=1",
            arguments.ToList());
        Assert.DoesNotContain(
            value => value.StartsWith("fps=", StringComparison.Ordinal),
            arguments);
    }

    [TestMethod]
    public void KeepsEvenOutputDimensions()
    {
        var arguments = FfmpegCommandBuilder.BuildPlaybackArguments(
            "a.mp4",
            1025,
            683,
            new FfmpegPlaybackOptions(
                false,
                2,
                1,
                false,
                FfmpegPixelFormat.Bgra8888,
                24));
        Assert.AreEqual(
            "fps=24,scale=1026:684:flags=bicubic,setsar=1",
            arguments.Single(value => value.Contains("scale=1026:684", StringComparison.Ordinal)));
    }
}