using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Platform;

namespace PCL.Core.Test;

[TestClass]
public sealed class LinuxDisplayProtocolTest
{
    [TestMethod]
    public void WaylandSessionPrefersNativeWaylandEvenWithXWaylandDisplay()
    {
        Assert.AreEqual(
            LinuxDisplayProtocol.Wayland,
            LinuxDisplayProtocolResolver.ResolveAuto("wayland", "wayland-0", ":0"));
        Assert.AreEqual(
            LinuxDisplayProtocol.Wayland,
            LinuxDisplayProtocolResolver.ResolveAuto("Wayland", null, ":0"));
    }

    [TestMethod]
    public void X11SessionKeepsX11()
    {
        Assert.AreEqual(
            LinuxDisplayProtocol.X11,
            LinuxDisplayProtocolResolver.ResolveAuto("x11", "wayland-0", ":0"));
    }

    [TestMethod]
    public void MissingSessionFallsBackBasedOnDisplayVariables()
    {
        Assert.AreEqual(
            LinuxDisplayProtocol.Wayland,
            LinuxDisplayProtocolResolver.ResolveAuto(null, "wayland-0", null));
        Assert.AreEqual(
            LinuxDisplayProtocol.X11,
            LinuxDisplayProtocolResolver.ResolveAuto(null, "wayland-0", ":0"));
        Assert.AreEqual(
            LinuxDisplayProtocol.X11,
            LinuxDisplayProtocolResolver.ResolveAuto(null, null, ":0"));
        Assert.AreEqual(
            LinuxDisplayProtocol.X11,
            LinuxDisplayProtocolResolver.ResolveAuto("", "", ""));
    }

    [TestMethod]
    public void ConfiguredValuesMapToProtocols()
    {
        Assert.AreEqual(
            LinuxDisplayProtocol.Auto,
            LinuxDisplayProtocolResolver.ResolveConfigured(0));
        Assert.AreEqual(
            LinuxDisplayProtocol.X11,
            LinuxDisplayProtocolResolver.ResolveConfigured(1));
        Assert.AreEqual(
            LinuxDisplayProtocol.Wayland,
            LinuxDisplayProtocolResolver.ResolveConfigured(2));
        Assert.AreEqual(
            LinuxDisplayProtocol.Auto,
            LinuxDisplayProtocolResolver.ResolveConfigured(99));
        Assert.AreEqual(
            LinuxDisplayProtocol.Auto,
            LinuxDisplayProtocolResolver.ResolveConfigured(-1));
    }
}