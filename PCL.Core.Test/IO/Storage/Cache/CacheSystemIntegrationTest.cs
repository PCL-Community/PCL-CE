using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Storage;
using PCL.Core.IO.Storage.Cache;

namespace PCL.Core.Test.IO.Storage.Cache;

/// <summary>
/// Integration tests for the complete cache system.
/// Tests interactions between CacheService, SqliteCacheStorage, and FileCacheStorage.
/// </summary>
[TestClass]
public class CacheSystemIntegrationTest
{
    private CacheService _cache = null!;
    private CacheOptions _options = null!;
    private string _testCacheDir = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _testCacheDir = Path.Combine(Path.GetTempPath(), "pcl_integration_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testCacheDir);

        _options = new CacheOptions
        {
            DatabasePath = Path.Combine(_testCacheDir, "cache.db"),
            FileCacheRoot = Path.Combine(_testCacheDir, "files"),
            MaxCacheSize = 50 * 1024 * 1024, // 50 MB
            MaxInlineSize = 64 * 1024, // 64 KB
            EnableCompression = false
        };

        _cache = new CacheService(_options);
        await _cache.InitializeAsync();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_cache != null)
        {
            await _cache.StopAsync();
        }

        if (Directory.Exists(_testCacheDir))
        {
            // Wait for file locks to be released
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    Directory.Delete(_testCacheDir, true);
                    break;
                }
                catch
                {
                    await Task.Delay(50);
                }
            }
        }
    }

    #region Mixed Storage Mode Tests

    [TestMethod]
    public async Task MixedInlineAndFileStorage_ShouldCoexist()
    {
        // Arrange - Mix small and large items
        var smallItems = new Dictionary<string, string>();
        var largeItems = new Dictionary<string, byte[]>();

        // Store small items (inline)
        for (int i = 0; i < 5; i++)
        {
            var key = $"small_{i}";
            var value = $"Small value {i}";
            smallItems[key] = value;
            await _cache.SetAsync(key, value);
        }

        // Store large items (file ref)
        for (int i = 0; i < 3; i++)
        {
            var key = $"large_{i}";
            var value = new byte[200 * 1024]; // 200 KB
            Random.Shared.NextBytes(value);
            largeItems[key] = value;
            await _cache.SetAsync(key, value);
        }

        // Act & Assert - Verify all items
        foreach (var (key, value) in smallItems)
        {
            var result = await _cache.GetAsync<string>(key);
            Assert.IsTrue(result.Found);
            Assert.AreEqual(value, result.Value);
        }

        foreach (var (key, value) in largeItems)
        {
            var result = await _cache.GetAsync<byte[]>(key);
            Assert.IsTrue(result.Found);
            CollectionAssert.AreEqual(value, result.Value);
        }

        // Verify stats
        var stats = await _cache.GetStatsAsync();
        Assert.IsTrue(stats.InlineEntries >= 5);
        Assert.IsTrue(stats.FileEntries >= 3);
    }

    #endregion

    #region Multi-Group Management Tests

    [TestMethod]
    public async Task MultipleGroups_ShouldBeDeletedIndependently()
    {
        // Arrange
        var groups = new[] { "group1", "group2", "group3" };
        var itemsPerGroup = 4;

        foreach (var group in groups)
        {
            var policy = new CachePolicy { Group = group };
            for (int i = 0; i < itemsPerGroup; i++)
            {
                await _cache.SetAsync($"{group}_item_{i}", $"Value in {group}_{i}", policy);
            }
        }

        // Verify all were cached
        var stats1 = await _cache.GetStatsAsync();
        Assert.AreEqual(groups.Length * itemsPerGroup, stats1.TotalEntries);

        // Act - Delete first group
        var deletedCount = await _cache.DeleteByGroupAsync("group1");

        // Assert
        Assert.AreEqual(itemsPerGroup, deletedCount);

        // Verify other groups still exist
        for (int i = 0; i < itemsPerGroup; i++)
        {
            Assert.IsFalse((await _cache.GetAsync<string>($"group1_item_{i}")).Found);
            Assert.IsTrue((await _cache.GetAsync<string>($"group2_item_{i}")).Found);
            Assert.IsTrue((await _cache.GetAsync<string>($"group3_item_{i}")).Found);
        }

        var stats2 = await _cache.GetStatsAsync();
        Assert.AreEqual((groups.Length - 1) * itemsPerGroup, stats2.TotalEntries);
    }

    #endregion

    #region Complex Expiration Scenarios Tests

    [TestMethod]
    public async Task MixedExpirationDates_ShouldExpireIndependently()
    {
        // Arrange
        var shortLived = new CachePolicy { AbsoluteExpiration = TimeSpan.FromMilliseconds(100) };
        var mediumLived = new CachePolicy { AbsoluteExpiration = TimeSpan.FromMilliseconds(200) };
        var longLived = new CachePolicy { AbsoluteExpiration = TimeSpan.FromMilliseconds(400) };

        await _cache.SetAsync("short", "expires first", shortLived);
        await _cache.SetAsync("medium", "expires second", mediumLived);
        await _cache.SetAsync("long", "expires last", longLived);

        // Act & Assert
        // After 150ms, only short should be expired
        await Task.Delay(150);
        Assert.IsFalse((await _cache.GetAsync<string>("short")).Found);
        Assert.IsTrue((await _cache.GetAsync<string>("medium")).Found);
        Assert.IsTrue((await _cache.GetAsync<string>("long")).Found);

        // After another 100ms (250ms total), short and medium should be expired
        await Task.Delay(100);
        Assert.IsFalse((await _cache.GetAsync<string>("short")).Found);
        Assert.IsFalse((await _cache.GetAsync<string>("medium")).Found);
        Assert.IsTrue((await _cache.GetAsync<string>("long")).Found);

        // After another 200ms (450ms total), all should be expired
        await Task.Delay(200);
        Assert.IsFalse((await _cache.GetAsync<string>("long")).Found);
    }

    #endregion

    #region Priority-Based Retention Tests

    [TestMethod]
    public async Task DifferentPriorities_ShouldReflectInStats()
    {
        // Arrange
        var policies = new Dictionary<string, CachePolicy>
        {
            ["low"] = new CachePolicy { Priority = CachePriority.Low },
            ["normal"] = new CachePolicy { Priority = CachePriority.Normal },
            ["high"] = new CachePolicy { Priority = CachePriority.High },
            ["never"] = new CachePolicy { Priority = CachePriority.NeverEvict }
        };

        foreach (var (key, policy) in policies)
        {
            await _cache.SetAsync(key, $"Item with {key} priority", policy);
        }

        // Act
        var stats = await _cache.GetStatsAsync();

        // Assert
        Assert.AreEqual(4, stats.TotalEntries);
        Assert.IsTrue(stats.TotalSizeBytes > 0);
    }

    #endregion

    #region Cache File Operations Tests

    [TestMethod]
    public async Task CacheFileAsync_WithLargeFile_ShouldStore()
    {
        // Arrange
        var largeData = new byte[5 * 1024 * 1024]; // 5 MB
        Random.Shared.NextBytes(largeData);
        var stream = new MemoryStream(largeData);

        // Act
        var hash = await _cache.CacheFileAsync("large_file", stream);
        var filePath = await _cache.GetCachedFilePathAsync("large_file");

        // Assert
        Assert.IsNotNull(hash);
        Assert.IsNotNull(filePath);
        Assert.IsTrue(File.Exists(filePath));
    }

    [TestMethod]
    public async Task CacheFileAsync_ThenRetrieve_ShouldReturnExactContent()
    {
        // Arrange
        var fileContent = "File content for integration test"u8.ToArray();
        var stream = new MemoryStream(fileContent);

        // Act
        var hash = await _cache.CacheFileAsync("test_file", stream);
        var result = await _cache.GetAsync<byte[]>("test_file");

        // Assert
        Assert.IsTrue(result.Found);
        CollectionAssert.AreEqual(fileContent, result.Value);
    }

    #endregion

    #region Statistics Accuracy Tests

    [TestMethod]
    public async Task StatsAfterOperations_ShouldBeAccurate()
    {
        // Arrange - Perform various operations
        var initialStats = await _cache.GetStatsAsync();
        var initialCount = initialStats.TotalEntries;

        // Add items
        await _cache.SetAsync("stat_key_1", "value1");
        await _cache.SetAsync("stat_key_2", new byte[10000]);

        // Perform hits and misses
        await _cache.GetAsync<string>("stat_key_1"); // Hit
        await _cache.GetAsync<string>("stat_key_1"); // Hit
        await _cache.GetAsync<string>("non_existent"); // Miss

        // Delete one
        await _cache.DeleteAsync("stat_key_1");

        // Act
        var finalStats = await _cache.GetStatsAsync();

        // Assert
        Assert.AreEqual(initialCount + 1, finalStats.TotalEntries); // Only stat_key_2 should remain
        Assert.AreEqual(2L, finalStats.CacheHits);
        Assert.AreEqual(1L, finalStats.CacheMisses);
    }

    #endregion

    #region Clear and Cleanup Tests

    [TestMethod]
    public async Task ClearAsync_ShouldRemoveEverything()
    {
        // Arrange - Fill cache with various items
        var policy1 = new CachePolicy { Group = "group1" };
        var policy2 = new CachePolicy { Group = "group2", Tags = "tag1" };

        await _cache.SetAsync("inline_1", "Small inline data", policy1);
        await _cache.SetAsync("inline_2", "Another inline item", policy1);
        await _cache.SetAsync("file_1", new byte[100 * 1024], policy2); // 100 KB
        await _cache.SetAsync("file_2", new byte[100 * 1024], policy2); // 100 KB

        var statsBeforeClear = await _cache.GetStatsAsync();
        Assert.IsTrue(statsBeforeClear.TotalEntries >= 4);

        // Act
        await _cache.ClearAsync();

        // Assert
        var statsAfterClear = await _cache.GetStatsAsync();
        Assert.AreEqual(0, statsAfterClear.TotalEntries);

        // Verify individual lookups fail
        Assert.IsFalse((await _cache.GetAsync<string>("inline_1")).Found);
        Assert.IsFalse((await _cache.GetAsync<string>("file_1")).Found);
    }

    [TestMethod]
    public async Task SequentialClearAndCacheRefill_ShouldWork()
    {
        // Arrange initial data
        await _cache.SetAsync("key_1", "value_1");
        var statsBefore = await _cache.GetStatsAsync();

        // Act - Clear and refill
        await _cache.ClearAsync();
        var statsAfterClear = await _cache.GetStatsAsync();

        await _cache.SetAsync("key_1", "value_1_new");
        await _cache.SetAsync("key_2", "value_2");
        var statsAfterRefill = await _cache.GetStatsAsync();

        // Assert
        Assert.IsTrue(statsBefore.TotalEntries >= 1);
        Assert.AreEqual(0, statsAfterClear.TotalEntries);
        Assert.AreEqual(2, statsAfterRefill.TotalEntries);
    }

    #endregion

    #region Concurrent Multi-Operation Tests

    [TestMethod]
    public async Task ConcurrentMixedOperations_ShouldBeThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();

        // Task 1: Set and Get
        tasks.Add(Task.Run(async () =>
        {
            for (int i = 0; i < 20; i++)
            {
                await _cache.SetAsync($"set_key_{i}", $"value_{i}");
                var result = await _cache.GetAsync<string>($"set_key_{i}");
                Assert.IsTrue(result.Found);
            }
        }));

        // Task 2: Delete operations
        tasks.Add(Task.Run(async () =>
        {
            for (int i = 0; i < 20; i++)
            {
                await _cache.SetAsync($"del_key_{i}", $"value_{i}");
                await Task.Delay(10);
                await _cache.DeleteAsync($"del_key_{i}");
            }
        }));

        // Task 3: Group operations
        tasks.Add(Task.Run(async () =>
        {
            var policy = new CachePolicy { Group = "concurrent_group" };
            for (int i = 0; i < 10; i++)
            {
                await _cache.SetAsync($"group_key_{i}", $"value_{i}", policy);
            }
            await Task.Delay(100);
            await _cache.DeleteByGroupAsync("concurrent_group");
        }));

        // Task 4: Check stats
        tasks.Add(Task.Run(async () =>
        {
            for (int i = 0; i < 10; i++)
            {
                var stats = await _cache.GetStatsAsync();
                Assert.IsNotNull(stats);
                await Task.Delay(50);
            }
        }));

        // Act
        await Task.WhenAll(tasks);

        // Assert - Cache should still be in valid state
        var finalStats = await _cache.GetStatsAsync();
        Assert.IsNotNull(finalStats);
    }

    #endregion

    #region Data Type Consistency Tests

    [TestMethod]
    public async Task LargeObjectSerialization_ShouldPreserveData()
    {
        // Arrange
        var complexObject = new
        {
            Id = 12345,
            Name = "Test Object",
            Values = new[] { 1, 2, 3, 4, 5 },
            Nested = new
            {
                Description = "Nested data",
                Count = 42
            }
        };

        // Act
        await _cache.SetAsync("complex_obj", complexObject);
        var result = await _cache.GetAsync<dynamic>("complex_obj");

        // Assert
        Assert.IsTrue(result.Found);
        Assert.IsNotNull(result.Value);
    }

    #endregion
}
