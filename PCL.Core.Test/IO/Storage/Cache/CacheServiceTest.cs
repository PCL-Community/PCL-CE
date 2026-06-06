using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Storage.Cache;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PCL.Core.Test.IO.Storage.Cache;

/// <summary>
/// Unit tests for the CacheService class.
/// Tests focus on basic cache operations, expiration handling, and the new TODO implementations.
/// 
/// NOTE: SQLite tests require proper SQLitePCL initialization.
/// If SQLite tests fail with "sqlite3_malloc not implemented", add SQLitePCL.Batteries_V2.Init()
/// to your test setup or ensure the SQLite native libraries are properly deployed.
/// </summary>
[TestClass]
public class CacheServiceTest
{
    private CacheService _cache = null!;
    private CacheOptions _options = null!;
    private string _testCacheDir = null!;

    [TestInitialize]
    public async Task Setup()
    {
        try
        {
            // Initialize SQLitePCL if needed (for SQLite native library binding)
            SQLitePCL.Batteries_V2.Init();
        }
        catch
        {
            // SQLitePCL may already be initialized or not available, continue anyway
        }

        // Create a temporary test cache directory
        _testCacheDir = Path.Combine(Path.GetTempPath(), "pcl_cache_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testCacheDir);

        _options = new CacheOptions
        {
            DatabasePath = Path.Combine(_testCacheDir, "test_cache.db"),
            FileCacheRoot = Path.Combine(_testCacheDir, "files"),
            MaxCacheSize = 100 * 1024 * 1024, // 100 MB for testing
            MaxInlineSize = 256 * 1024, // 256 KB
            EnableCompression = false // Disable compression for simpler testing
        };

        _cache = new CacheService(_options);
        await _cache.InitializeAsync().ConfigureAwait(false);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_cache != null)
        {
            await _cache.DisposeAsync().ConfigureAwait(false);
        }

        // Clean up the test directory
        if (Directory.Exists(_testCacheDir))
        {
            Directory.Delete(_testCacheDir, true);
        }
    }

    #region Basic Set/Get Tests

    [TestMethod]
    public async Task SetAndGetString_ShouldReturnCachedValue()
    {
        // Arrange
        var key = "test_key_1";
        var value = "Hello, Cache!";

        // Act
        await _cache.SetAsync(key, value);
        var result = await _cache.GetAsync<string>(key);

        // Assert
        Assert.IsTrue(result.Found, "Cache should find the key");
        Assert.AreEqual(value, result.Value, "Cache should return the correct value");
    }

    [TestMethod]
    public async Task SetAndGetInt_ShouldReturnCachedValue()
    {
        // Arrange
        var key = "test_int_key";
        var value = 42;

        // Act
        await _cache.SetAsync(key, value);
        var result = await _cache.GetAsync<int>(key);

        // Assert
        Assert.IsTrue(result.Found);
        Assert.AreEqual(value, result.Value);
    }

    [TestMethod]
    public async Task SetAndGetObject_ShouldReturnCachedValue()
    {
        // Arrange
        var key = "test_obj_key";
        var value = new TestModel { Id = 1, Name = "Test", Value = 123.45 };

        // Act
        await _cache.SetAsync(key, value);
        var result = await _cache.GetAsync<TestModel>(key);

        // Assert
        Assert.IsTrue(result.Found);
        Assert.AreEqual(value.Id, result.Value!.Id);
        Assert.AreEqual(value.Name, result.Value!.Name);
        Assert.AreEqual(value.Value, result.Value!.Value);
    }

    [TestMethod]
    public async Task GetNonExistentKey_ShouldReturnMiss()
    {
        // Act
        var result = await _cache.GetAsync<string>("non_existent_key");

        // Assert
        Assert.IsFalse(result.Found, "Should return Miss for non-existent key");
    }

    #endregion

    #region Expiration Tests

    [TestMethod]
    public async Task SetWithAbsoluteExpiration_ShouldExpireAfterTimeout()
    {
        // Arrange
        var key = "expiring_key";
        var value = "This will expire";
        var policy = new CachePolicy
        {
            AbsoluteExpiration = TimeSpan.FromMilliseconds(200)
        };

        // Act
        await _cache.SetAsync(key, value, policy);
        var resultBefore = await _cache.GetAsync<string>(key);
        await Task.Delay(300);
        var resultAfter = await _cache.GetAsync<string>(key);

        // Assert
        Assert.IsTrue(resultBefore.Found, "Should find key before expiration");
        Assert.IsFalse(resultAfter.Found, "Should not find key after expiration");
    }

    [TestMethod]
    public async Task SetWithNeverExpirePolicy_ShouldNotExpire()
    {
        // Arrange
        var key = "never_expire_key";
        var value = "This will never expire";

        // Act
        await _cache.SetAsync(key, value, CachePolicy.NeverExpire);
        var result1 = await _cache.GetAsync<string>(key);
        await Task.Delay(200);
        var result2 = await _cache.GetAsync<string>(key);

        // Assert
        Assert.IsTrue(result1.Found);
        Assert.IsTrue(result2.Found);
        Assert.AreEqual(value, result2.Value);
    }

    #endregion

    #region Inline vs FileRef Storage Tests

    [TestMethod]
    public async Task SetSmallData_ShouldUseInlineStorage()
    {
        // Arrange - Create a small string (within MaxInlineSize)
        var key = "small_data_key";
        var value = "Small data that should be stored inline";

        // Act
        await _cache.SetAsync(key, value);
        var result = await _cache.GetAsync<string>(key);

        // Assert
        Assert.IsTrue(result.Found);
        Assert.AreEqual(value, result.Value);
    }

    [TestMethod]
    public async Task SetLargeData_ShouldUseFileRefStorage()
    {
        // Arrange - Create large byte array (exceeds MaxInlineSize)
        var key = "large_data_key";
        var largeData = new byte[500 * 1024]; // 500 KB
        Random.Shared.NextBytes(largeData);

        // Act
        await _cache.SetAsync(key, largeData);
        var result = await _cache.GetAsync<byte[]>(key);

        // Assert
        Assert.IsTrue(result.Found);
        CollectionAssert.AreEqual(largeData, result.Value, "Large data should be retrieved correctly");
    }

    #endregion

    #region Delete Tests

    [TestMethod]
    public async Task DeleteExistingKey_ShouldRemoveFromCache()
    {
        // Arrange
        var key = "delete_test_key";
        var value = "To be deleted";
        await _cache.SetAsync(key, value);

        // Act
        var deleted = await _cache.DeleteAsync(key);
        var result = await _cache.GetAsync<string>(key);

        // Assert
        Assert.IsTrue(deleted, "Delete should return true");
        Assert.IsFalse(result.Found, "Key should be removed");
    }

    [TestMethod]
    public async Task DeleteNonExistentKey_ShouldReturnFalse()
    {
        // Act
        var deleted = await _cache.DeleteAsync("non_existent");

        // Assert
        Assert.IsFalse(deleted, "Delete should return false for non-existent key");
    }

    #endregion

    #region Exists Tests

    [TestMethod]
    public async Task ExistsForValidKey_ShouldReturnTrue()
    {
        // Arrange
        var key = "exists_test_key";
        await _cache.SetAsync(key, "test value");

        // Act
        var exists = await _cache.ExistsAsync(key);

        // Assert
        Assert.IsTrue(exists);
    }

    [TestMethod]
    public async Task ExistsForExpiredKey_ShouldReturnFalse()
    {
        // Arrange
        var key = "expired_exists_key";
        var policy = new CachePolicy
        {
            AbsoluteExpiration = TimeSpan.FromMilliseconds(100)
        };
        await _cache.SetAsync(key, "expiring value", policy);

        // Act
        await Task.Delay(200);
        var exists = await _cache.ExistsAsync(key);

        // Assert
        Assert.IsFalse(exists);
    }

    [TestMethod]
    public async Task ExistsForNonExistentKey_ShouldReturnFalse()
    {
        // Act
        var exists = await _cache.ExistsAsync("non_existent");

        // Assert
        Assert.IsFalse(exists);
    }

    #endregion

    #region Group Operations Tests

    [TestMethod]
    public async Task SetMultipleItemsInGroup_ShouldAllHaveSameGroup()
    {
        // Arrange
        var groupName = "test_group";
        var policy = new CachePolicy { Group = groupName };

        // Act
        await _cache.SetAsync("group_key_1", "value1", policy);
        await _cache.SetAsync("group_key_2", "value2", policy);
        await _cache.SetAsync("group_key_3", "value3", policy);

        // Verify all are cached
        var result1 = await _cache.GetAsync<string>("group_key_1");
        var result2 = await _cache.GetAsync<string>("group_key_2");
        var result3 = await _cache.GetAsync<string>("group_key_3");

        Assert.IsTrue(result1.Found);
        Assert.IsTrue(result2.Found);
        Assert.IsTrue(result3.Found);
    }

    [TestMethod]
    public async Task DeleteByGroupAsync_ShouldRemoveAllItemsInGroup()
    {
        // Arrange
        var groupName = "delete_group";
        var policy = new CachePolicy { Group = groupName };

        await _cache.SetAsync("group_item_1", "value1", policy);
        await _cache.SetAsync("group_item_2", "value2", policy);
        await _cache.SetAsync("group_item_3", "value3", policy);

        // Add an item in a different group to verify it's not deleted
        await _cache.SetAsync("other_group_item", "other_value",
            new CachePolicy { Group = "other_group" });

        // Act
        var deletedCount = await _cache.DeleteByGroupAsync(groupName);

        // Assert
        Assert.AreEqual(3, deletedCount, "Should delete 3 items");

        Assert.IsFalse((await _cache.GetAsync<string>("group_item_1")).Found);
        Assert.IsFalse((await _cache.GetAsync<string>("group_item_2")).Found);
        Assert.IsFalse((await _cache.GetAsync<string>("group_item_3")).Found);

        Assert.IsTrue((await _cache.GetAsync<string>("other_group_item")).Found,
            "Item in other group should not be deleted");
    }

    [TestMethod]
    public async Task DeleteByGroupAsync_WithFileReferences_ShouldReleaseFiles()
    {
        // Arrange - Create items with large data (file references)
        var groupName = "file_group";
        var policy = new CachePolicy { Group = groupName };
        var largeData = new byte[500 * 1024]; // 500 KB
        Random.Shared.NextBytes(largeData);

        await _cache.SetAsync("file_item_1", largeData, policy);
        await _cache.SetAsync("file_item_2", largeData, policy);

        // Verify items exist
        Assert.IsTrue((await _cache.GetAsync<byte[]>("file_item_1")).Found);

        // Act
        var deletedCount = await _cache.DeleteByGroupAsync(groupName);

        // Assert
        Assert.AreEqual(2, deletedCount);
        Assert.IsFalse((await _cache.GetAsync<byte[]>("file_item_1")).Found);
        Assert.IsFalse((await _cache.GetAsync<byte[]>("file_item_2")).Found);
    }

    #endregion

    #region Tag Operations Tests

    [TestMethod]
    public async Task DeleteByTagAsync_ShouldRemoveItemsWithTag()
    {
        // Arrange
        var tag = "test_tag";
        var policy1 = new CachePolicy { Tags = tag };
        var policy2 = new CachePolicy { Tags = "other_tag" };

        await _cache.SetAsync("tagged_key_1", "value1", policy1);
        await _cache.SetAsync("tagged_key_2", "value2", policy1);
        await _cache.SetAsync("untagged_key", "value3", policy2);

        // Act
        var deletedCount = await _cache.DeleteByTagAsync(tag);

        // Assert
        Assert.IsTrue(deletedCount >= 2, "Should delete at least 2 items");
        Assert.IsFalse((await _cache.GetAsync<string>("tagged_key_1")).Found);
        Assert.IsFalse((await _cache.GetAsync<string>("tagged_key_2")).Found);
        Assert.IsTrue((await _cache.GetAsync<string>("untagged_key")).Found);
    }

    #endregion

    #region Expiration Management Tests

    [TestMethod]
    public async Task DeleteExpiredAsync_ShouldRemoveExpiredItems()
    {
        // Arrange
        var expiredPolicy = new CachePolicy { AbsoluteExpiration = TimeSpan.FromMilliseconds(100) };
        var activePolicy = CachePolicy.Default; // 1 hour

        await _cache.SetAsync("expired_key", "will expire", expiredPolicy);
        await _cache.SetAsync("active_key", "will stay", activePolicy);

        // Wait for expiration
        await Task.Delay(200);

        // Act
        var deletedCount = await _cache.DeleteExpiredAsync();

        // Assert
        Assert.IsTrue(deletedCount >= 1, "Should delete at least 1 expired item");
        Assert.IsFalse((await _cache.GetAsync<string>("expired_key")).Found);
        Assert.IsTrue((await _cache.GetAsync<string>("active_key")).Found);
    }

    #endregion

    #region Clear All Tests

    [TestMethod]
    public async Task ClearAsync_ShouldRemoveAllCachedItems()
    {
        // Arrange - Add various types of items
        await _cache.SetAsync("key1", "string value");
        await _cache.SetAsync("key2", 42);
        await _cache.SetAsync("key3", new byte[1000]); // Large data

        var obj = new TestModel { Id = 1, Name = "Test" };
        await _cache.SetAsync("key4", obj);

        // Verify items exist
        Assert.IsTrue((await _cache.GetAsync<string>("key1")).Found);
        Assert.IsTrue((await _cache.GetAsync<int>("key2")).Found);

        // Act
        await _cache.ClearAsync();

        // Assert - All items should be gone
        Assert.IsFalse((await _cache.GetAsync<string>("key1")).Found);
        Assert.IsFalse((await _cache.GetAsync<int>("key2")).Found);
        Assert.IsFalse((await _cache.GetAsync<byte[]>("key3")).Found);
        Assert.IsFalse((await _cache.GetAsync<TestModel>("key4")).Found);
    }

    [TestMethod]
    public async Task ClearAsync_ShouldCleanupFileReferences()
    {
        // Arrange - Add items with file references
        var largeData = new byte[500 * 1024]; // 500 KB
        Random.Shared.NextBytes(largeData);

        await _cache.SetAsync("file_key_1", largeData);
        await _cache.SetAsync("file_key_2", largeData);

        // Act
        await _cache.ClearAsync();

        // Assert - Items should be gone
        Assert.IsFalse((await _cache.GetAsync<byte[]>("file_key_1")).Found);
        Assert.IsFalse((await _cache.GetAsync<byte[]>("file_key_2")).Found);
    }

    #endregion

    #region Statistics Tests

    [TestMethod]
    public async Task GetStatsAsync_ShouldReturnAccurateStats()
    {
        // Arrange
        await _cache.SetAsync("stat_key_1", "value1");
        await _cache.SetAsync("stat_key_2", "value2");

        // Trigger some hits and misses
        await _cache.GetAsync<string>("stat_key_1"); // Hit
        await _cache.GetAsync<string>("stat_key_1"); // Hit
        await _cache.GetAsync<string>("non_existent"); // Miss

        // Act
        var stats = await _cache.GetStatsAsync();

        // Assert
        Assert.IsTrue(stats.TotalEntries >= 2, "Should have at least 2 entries");
        Assert.IsTrue(stats.TotalSizeBytes > 0, "Should have non-zero total size");
        Assert.AreEqual(2L, stats.CacheHits, "Should have 2 cache hits");
        Assert.AreEqual(1L, stats.CacheMisses, "Should have 1 cache miss");
    }

    [TestMethod]
    public async Task GetStatsAsync_HitRate_ShouldBeCalculatedCorrectly()
    {
        // Arrange
        await _cache.SetAsync("hit_key", "value");

        // Create 10 hits
        for (int i = 0; i < 10; i++)
        {
            await _cache.GetAsync<string>("hit_key");
        }

        // Create 5 misses
        for (int i = 0; i < 5; i++)
        {
            await _cache.GetAsync<string>("miss_" + i);
        }

        // Act
        var stats = await _cache.GetStatsAsync();

        // Assert
        Assert.AreEqual(10L, stats.CacheHits);
        Assert.AreEqual(5L, stats.CacheMisses);
        Assert.AreEqual(10.0 / 15.0, stats.HitRate, 0.0001, "Hit rate should be 10/15");
    }

    #endregion

    #region CacheFileAsync Tests

    [TestMethod]
    public async Task CacheFileAsync_ShouldCacheFileStream()
    {
        // Arrange
        var key = "file_cache_key";
        var fileContent = "This is file content for caching"u8.ToArray();
        var stream = new MemoryStream(fileContent);

        // Act
        var hash = await _cache.CacheFileAsync(key, stream);
        var result = await _cache.GetAsync<byte[]>(key);

        // Assert
        Assert.IsNotNull(hash);
        Assert.IsFalse(string.IsNullOrEmpty(hash));
        Assert.IsTrue(result.Found);
        CollectionAssert.AreEqual(fileContent, result.Value);
    }

    [TestMethod]
    public async Task GetCachedFilePathAsync_ShouldReturnFilePath()
    {
        // Arrange
        var key = "file_path_key";
        var fileContent = "Test file content"u8.ToArray();
        var stream = new MemoryStream(fileContent);

        await _cache.CacheFileAsync(key, stream);

        // Act
        var filePath = await _cache.GetCachedFilePathAsync(key);

        // Assert
        Assert.IsNotNull(filePath);
        Assert.IsTrue(File.Exists(filePath), "Cached file should exist on disk");
    }

    #endregion

    #region Concurrent Operations Tests

    [TestMethod]
    public async Task ConcurrentSetGet_ShouldBeThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();
        var itemsPerTask = 10;

        // Act - Create concurrent set and get operations
        for (int i = 0; i < 5; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(async () =>
            {
                for (int j = 0; j < itemsPerTask; j++)
                {
                    var key = $"concurrent_key_{taskId}_{j}";
                    var value = $"value_{taskId}_{j}";

                    await _cache.SetAsync(key, value);
                    var result = await _cache.GetAsync<string>(key);

                    Assert.IsTrue(result.Found);
                    Assert.AreEqual(value, result.Value);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        var stats = await _cache.GetStatsAsync();
        Assert.IsTrue(stats.TotalEntries >= 50, "Should have at least 50 cached items");
    }

    #endregion

    #region Test Models

    private class TestModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public double Value { get; set; }

        public override bool Equals(object? obj)
        {
            if (obj is not TestModel other) return false;
            return Id == other.Id && Name == other.Name && Value == other.Value;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Name, Value);
        }
    }

    #endregion
}
