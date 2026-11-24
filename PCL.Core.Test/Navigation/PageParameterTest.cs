using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Navigation;

namespace PCL.Core.Test.Navigation;

[TestClass]
public sealed class PageParameterTest
{
    [TestMethod]
    public void StringCaseRoundTripsThroughUnion()
    {
        PageParameter parameter = "saves/world";

        var text = parameter switch
        {
            string value => value,
            _ => null
        };

        Assert.AreEqual("saves/world", text);
    }

    [TestMethod]
    public void JsonObjectCaseKeepsReference()
    {
        var version = new JsonObject
        {
            ["id"] = "1.21.5"
        };
        PageParameter parameter = version;

        var routed = parameter switch
        {
            JsonObject value => value,
            _ => null
        };

        Assert.AreSame(version, routed);
    }

    [TestMethod]
    public void PageRouteComparesUnionPayload()
    {
        var a = new PageRoute(PageIds.InstanceSetup, SubPageIds.VersionOverall, "a");
        var same = new PageRoute(PageIds.InstanceSetup, SubPageIds.VersionOverall, "a");
        var different = new PageRoute(PageIds.InstanceSetup, SubPageIds.VersionOverall, "b");

        Assert.AreEqual(a, same);
        Assert.AreNotEqual(a, different);
    }
}