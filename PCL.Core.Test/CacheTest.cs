using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Cache;
using System;
using System.Collections.Generic;

namespace PCL.Core.Test;

public class CustomProvider<TKey, TEntity> : DefaultCacheProvider<TKey, TEntity> where TKey : notnull
{
    public bool IsInitialized { get; set; } = true;
}

public partial class TestModel
{
    [CachedProperty] private string _userName;
    [CachedProperty(keyType: typeof(int))] private List<string> _orders;

    [CachedProperty(typeof(CustomProvider<,>), keyType: typeof(Guid))]
    private double _balance;
}

[TestClass]
public class CacheTest
{
    [TestInitialize]
    public void Setup()
    {
        CacheManager.DefauleProviderType = typeof(DefaultCacheProvider<,>);
    }

    [TestMethod]
    public void Test_Default_Property_Generation()
    {
        // Arrange
        var vm = new TestModel();

        // Act & Assert
        // 1. 验证属性是否存在 (如果编译通过，说明生成成功)
        Assert.IsNotNull(vm.UserName, "生成的属性 UserName 应该存在且不为 null");

        // 2. 验证类型正确性
        // 默认 Key 应该是 string，Value 应该是 string (字段类型)
        Assert.IsInstanceOfType(vm.UserName, typeof(ICacheProvider<string, string>));

        // 3. 验证功能 (存取)
        string key = "user_001";
        string value = "Alice";

        vm.UserName.AddOrUpdate(key, value, TimeSpan.FromMinutes(1));

        bool found = vm.UserName.TryGet(key, out var retrieved);
        Assert.IsTrue(found);
        Assert.AreEqual(value, retrieved);
    }

    [TestMethod]
    public void Test_Custom_Key_Type()
    {
        // Arrange
        var vm = new TestModel();

        // Act
        // 验证生成的属性是否使用了 int 作为 Key
        // Orders 对应字段 List<string>
        var provider = vm.Orders;

        // Assert
        Assert.IsInstanceOfType(provider, typeof(ICacheProvider<int, List<string>>));

        // 验证存取
        int orderId = 1024;
        var data = new List<string> { "Apple", "Banana" };

        provider.AddOrUpdate(orderId, data, TimeSpan.FromMinutes(5));

        Assert.IsTrue(provider.TryGet(orderId, out var result));
        Assert.AreEqual(2, result.Count);
    }

    //[TestMethod]
    //public void Test_Custom_Provider_Injection()
    //{
    //    // Arrange
    //    var vm = new TestModel();

    //    // Act
    //    // Balance 属性使用了 CustomTestProvider
    //    var provider = vm.Balance;

    //    // Assert
    //    // 验证运行时类型是否为我们自定义的 Provider
    //    Assert.IsInstanceOfType(provider, typeof(CustomProvider<Guid, double>));

    //    // 验证单例性 (Manager 应该对同一个类型返回同一个实例)
    //    var provider2 = CacheManager.GetProvider<Guid, double>(typeof(CustomProvider<,>));
    //    Assert.AreSame(provider, provider2, "Provider 应该是单例的，由 Manager 管理");
    //}

    [TestMethod]
    public void Test_Manager_Singleton_Behavior()
    {
        // 验证不同实例访问同一个 Provider
        var vm1 = new TestModel();
        var vm2 = new TestModel();

        vm1.UserName.AddOrUpdate("shared_key", "shared_value", TimeSpan.FromMinutes(1));

        // vm2 应该能获取到 vm1 设置的值，因为 Provider 是单例
        bool found = vm2.UserName.TryGet("shared_key", out var val);

        Assert.IsTrue(found);
        Assert.AreEqual("shared_value", val);
    }
}