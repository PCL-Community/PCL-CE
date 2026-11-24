using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Storage.Cache;

namespace PCL.Core.Test.IO.Storage.Cache;

/// <summary>
/// Integration tests for the complete cache system. Tests interactions between
/// CacheService, SqliteCacheStorage, and FileCacheStorage.
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
        _testCacheDir = Path.Combine(
            Path.GetTempPath(),
            "pcl_integration_test_" + Guid.NewGuid().ToString("N")[..8]);
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
        if (_cache != null) await _cache.DisposeAsync();

        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_testCacheDir))
            // Wait for file locks to be released
            for (var i = 0; i < 10; i++)
                try
                {
                    Directory.Delete(_testCacheDir, true);
                    break;
                }
                catch
                {
                    await Task.Delay(50, TestContext.CancellationToken);
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
        for (var i = 0; i < 5; i++)
        {
            var key = $"small_{i}";
            var value = $"Small value {i}";
            smallItems[key] = value;
            await _cache.SetAsync(key, value, ct: TestContext.CancellationToken);
        }

        // Store large items (file ref)
        for (var i = 0; i < 3; i++)
        {
            var key = $"large_{i}";
            var value = new byte[200 * 1024]; // 200 KB
            Random.Shared.NextBytes(value);
            largeItems[key] = value;
            await _cache.SetAsync(key, value, ct: TestContext.CancellationToken);
        }

        // Act & Assert - Verify all items
        foreach (var (key, value) in smallItems)
        {
            var result = await _cache.GetAsync<string>(key, TestContext.CancellationToken);
            Assert.IsTrue(result.Found);
            Assert.AreEqual(value, result.Value);
        }

        foreach (var (key, value) in largeItems)
        {
            var result = await _cache.GetAsync<byte[]>(key, TestContext.CancellationToken);
            Assert.IsTrue(result.Found);
            Assert.AreSequenceEqual(value, result.Value);
        }

        // Verify stats
        var stats = await _cache.GetStatsAsync();
        Assert.IsGreaterThanOrEqualTo(5, stats.InlineEntries);
        Assert.IsGreaterThanOrEqualTo(3, stats.FileEntries);
    }

    #endregion

    #region Multi-Group Management Tests

    [TestMethod]
    public async Task MultipleGroups_ShouldBeDeletedIndependently()
    {
        // Arrange
        var groups = new[]
        {
            "group1",
            "group2",
            "group3"
        };
        const int itemsPerGroup = 4;

        foreach (var group in groups)
        {
            var policy = new CachePolicy
            {
                Group = group
            };
            for (var i = 0; i < itemsPerGroup; i++)
                await _cache.SetAsync(
                    $"{group}_item_{i}",
                    $"Value in {group}_{i}",
                    policy,
                    TestContext.CancellationToken);
        }

        // Verify all were cached
        var stats1 = await _cache.GetStatsAsync();
        Assert.AreEqual(groups.Length * itemsPerGroup, stats1.TotalEntries);

        // Act - Delete first group
        var deletedCount = await _cache.DeleteByGroupAsync("group1");

        // Assert
        Assert.AreEqual(itemsPerGroup, deletedCount);

        // Verify other groups still exist
        for (var i = 0; i < itemsPerGroup; i++)
        {
            Assert.IsFalse(
                (await _cache.GetAsync<string>($"group1_item_{i}", TestContext.CancellationToken))
                .Found);
            Assert.IsTrue(
                (await _cache.GetAsync<string>($"group2_item_{i}", TestContext.CancellationToken))
                .Found);
            Assert.IsTrue(
                (await _cache.GetAsync<string>($"group3_item_{i}", TestContext.CancellationToken))
                .Found);
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
        var shortLived = new CachePolicy
        {
            AbsoluteExpiration = TimeSpan.FromSeconds(2)
        };
        var mediumLived = new CachePolicy
        {
            AbsoluteExpiration = TimeSpan.FromSeconds(4)
        };
        var longLived = new CachePolicy
        {
            AbsoluteExpiration = TimeSpan.FromSeconds(6)
        };

        await _cache.SetAsync("short", "expires first", shortLived, TestContext.CancellationToken);
        await _cache.SetAsync(
            "medium",
            "expires second",
            mediumLived,
            TestContext.CancellationToken);
        await _cache.SetAsync("long", "expires last", longLived, TestContext.CancellationToken);

        // Act & Assert
        await Task.Delay(TimeSpan.FromSeconds(2.2), TestContext.CancellationToken);
        Assert.IsFalse(
            (await _cache.GetAsync<string>("short", TestContext.CancellationToken)).Found);
        Assert.IsTrue(
            (await _cache.GetAsync<string>("medium", TestContext.CancellationToken)).Found);
        Assert.IsTrue((await _cache.GetAsync<string>("long", TestContext.CancellationToken)).Found);

        await Task.Delay(TimeSpan.FromSeconds(2), TestContext.CancellationToken);
        Assert.IsFalse(
            (await _cache.GetAsync<string>("short", TestContext.CancellationToken)).Found);
        Assert.IsFalse(
            (await _cache.GetAsync<string>("medium", TestContext.CancellationToken)).Found);
        Assert.IsTrue((await _cache.GetAsync<string>("long", TestContext.CancellationToken)).Found);

        await Task.Delay(TimeSpan.FromSeconds(2.2), TestContext.CancellationToken);
        Assert.IsFalse(
            (await _cache.GetAsync<string>("long", TestContext.CancellationToken)).Found);
    }

    #endregion

    #region Priority-Based Retention Tests

    [TestMethod]
    public async Task DifferentPriorities_ShouldReflectInStats()
    {
        // Arrange
        var policies = new Dictionary<string, CachePolicy>
        {
            ["low"] = new()
            {
                Priority = CachePriority.Low
            },
            ["normal"] = new()
            {
                Priority = CachePriority.Normal
            },
            ["high"] = new()
            {
                Priority = CachePriority.High
            },
            ["never"] = new()
            {
                Priority = CachePriority.NeverEvict
            }
        };

        foreach (var (key, policy) in policies)
            await _cache.SetAsync(
                key,
                $"Item with {key} priority",
                policy,
                TestContext.CancellationToken);

        // Act
        var stats = await _cache.GetStatsAsync();

        // Assert
        Assert.AreEqual(4, stats.TotalEntries);
        Assert.IsGreaterThan(0, stats.TotalSizeBytes);
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
        await _cache.SetAsync("stat_key_1", "value1", ct: TestContext.CancellationToken);
        await _cache.SetAsync("stat_key_2", new byte[10000], ct: TestContext.CancellationToken);

        // Perform hits and misses
        await _cache.GetAsync<string>("stat_key_1", TestContext.CancellationToken); // Hit
        await _cache.GetAsync<string>("stat_key_1", TestContext.CancellationToken); // Hit
        await _cache.GetAsync<string>("non_existent", TestContext.CancellationToken); // Miss

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

    #region Concurrent Multi-Operation Tests

    [TestMethod]
    public async Task ConcurrentMixedOperations_ShouldBeThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>
        {
            // Task 1: Set and Get
            Task.Run(
                async () =>
                {
                    for (var i = 0; i < 20; i++)
                    {
                        await _cache.SetAsync(
                            $"set_key_{i}",
                            $"value_{i}",
                            ct: TestContext.CancellationToken);
                        var result = await _cache.GetAsync<string>(
                            $"set_key_{i}",
                            TestContext.CancellationToken);
                        Assert.IsTrue(result.Found);
                    }
                },
                TestContext.CancellationToken),
            // Task 2: Delete operations
            Task.Run(
                async () =>
                {
                    for (var i = 0; i < 20; i++)
                    {
                        await _cache.SetAsync(
                            $"del_key_{i}",
                            $"value_{i}",
                            ct: TestContext.CancellationToken);
                        await Task.Delay(10, TestContext.CancellationToken);
                        await _cache.DeleteAsync($"del_key_{i}");
                    }
                },
                TestContext.CancellationToken),
            // Task 3: Group operations
            Task.Run(
                async () =>
                {
                    var policy = new CachePolicy
                    {
                        Group = "concurrent_group"
                    };
                    for (var i = 0; i < 10; i++)
                        await _cache.SetAsync(
                            $"group_key_{i}",
                            $"value_{i}",
                            policy,
                            TestContext.CancellationToken);
                    await Task.Delay(100, TestContext.CancellationToken);
                    await _cache.DeleteByGroupAsync("concurrent_group");
                },
                TestContext.CancellationToken),
            // Task 4: Check stats
            Task.Run(
                async () =>
                {
                    for (var i = 0; i < 10; i++)
                    {
                        var stats = await _cache.GetStatsAsync();
                        Assert.IsNotNull(stats);
                        await Task.Delay(50, TestContext.CancellationToken);
                    }
                },
                TestContext.CancellationToken)
        };

        // Act
        await Task.WhenAll(tasks);

        // Assert - Cache should still be in valid state
        var finalStats = await _cache.GetStatsAsync();
        Assert.IsNotNull(finalStats);
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
        var hash = await _cache.CacheFileAsync(
            "large_file",
            stream,
            ct: TestContext.CancellationToken);
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
        var hash = await _cache.CacheFileAsync(
            "test_file",
            stream,
            ct: TestContext.CancellationToken);
        var result = await _cache.GetAsync<byte[]>("test_file", TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(result.Found);
        Assert.AreSequenceEqual(fileContent, result.Value);
    }

    #endregion

    #region Clear and Cleanup Tests

    [TestMethod]
    public async Task ClearAsync_ShouldRemoveEverything()
    {
        // Arrange - Fill cache with various items
        var policy1 = new CachePolicy
        {
            Group = "group1"
        };
        var policy2 = new CachePolicy
        {
            Group = "group2",
            Tags = "tag1"
        };

        await _cache.SetAsync(
            "inline_1",
            "Small inline data",
            policy1,
            TestContext.CancellationToken);
        await _cache.SetAsync(
            "inline_2",
            "Another inline item",
            policy1,
            TestContext.CancellationToken);
        await _cache.SetAsync(
            "file_1",
            new byte[100 * 1024],
            policy2,
            TestContext.CancellationToken); // 100 KB
        await _cache.SetAsync(
            "file_2",
            new byte[100 * 1024],
            policy2,
            TestContext.CancellationToken); // 100 KB

        var statsBeforeClear = await _cache.GetStatsAsync();
        Assert.IsGreaterThanOrEqualTo(4, statsBeforeClear.TotalEntries);

        // Act
        await _cache.ClearAsync();

        // Assert
        var statsAfterClear = await _cache.GetStatsAsync();
        Assert.AreEqual(0, statsAfterClear.TotalEntries);

        // Verify individual lookups fail
        Assert.IsFalse(
            (await _cache.GetAsync<string>("inline_1", TestContext.CancellationToken)).Found);
        Assert.IsFalse(
            (await _cache.GetAsync<string>("file_1", TestContext.CancellationToken)).Found);
    }

    [TestMethod]
    public async Task SequentialClearAndCacheRefill_ShouldWork()
    {
        // Arrange initial data
        await _cache.SetAsync("key_1", "value_1", ct: TestContext.CancellationToken);
        var statsBefore = await _cache.GetStatsAsync();

        // Act - Clear and refill
        await _cache.ClearAsync();
        var statsAfterClear = await _cache.GetStatsAsync();

        await _cache.SetAsync("key_1", "value_1_new", ct: TestContext.CancellationToken);
        await _cache.SetAsync("key_2", "value_2", ct: TestContext.CancellationToken);
        var statsAfterRefill = await _cache.GetStatsAsync();

        // Assert
        Assert.IsGreaterThanOrEqualTo(1, statsBefore.TotalEntries);
        Assert.AreEqual(0, statsAfterClear.TotalEntries);
        Assert.AreEqual(2, statsAfterRefill.TotalEntries);
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
            Values = new[]
            {
                1,
                2,
                3,
                4,
                5
            },
            Nested = new
            {
                Description = "Nested data",
                Count = 42
            }
        };

        // Act
        await _cache.SetAsync("complex_obj", complexObject, ct: TestContext.CancellationToken);
        var result = await _cache.GetAsync<dynamic>("complex_obj", TestContext.CancellationToken);

        // Assert
        Assert.IsTrue(result.Found);
        Assert.IsNotNull(result.Value);
    }

    public TestContext TestContext { get; set; }

    #endregion
}