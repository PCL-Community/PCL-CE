using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Net;
using PCL.Core.Net.Nat;
using PCL.Core.Net.Nat.Stun;

namespace PCL.Test.Net;

[TestClass]
public class TestNatTest
{
    [TestMethod]
    public async Task Test1()
    {
        var instance = new StunClient();
        var response = await StunRequestBuilder
            .Create(instance)
            .WithMessageType(StunMessageType.BindingRequest)
            .GetResponseAsync(TestContext.CancellationTokenSource.Token);
        Assert.IsNotNull(response);
        Console.WriteLine(response.ToString());
        var test = new NatTest(instance);
        Console.WriteLine((await test.GetPublicEndPointAsync(TestContext.CancellationTokenSource.Token))?.ToString());
    }

    public TestContext TestContext { get; set; }
}