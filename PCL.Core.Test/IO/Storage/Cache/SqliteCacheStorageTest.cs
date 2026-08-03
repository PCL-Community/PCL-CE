using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Storage.Cache;
using PCL.Core.IO.Storage.Cache.Model;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Test.IO.Storage.Cache;

/// <summary>
/// Unit tests for SqliteCacheStorage class.
/// Tests the database layer operations including CRUD, queries, and file hash management.
/// </summary>
[TestClass]
public class SqliteCacheStorageTest
{
    private SqliteCacheStorage _storage = null!;
    private SchemaManager _schemaManager = null!;
    private string _testDbPath = null!;

    [TestInitialize]
    public async Task Setup()
    {
        var testDir = Path.Combine(Path.GetTempPath(), "pcl_db_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(testDir);

        _testDbPath = Path.Combine(testDir, "test.db");
        _schemaManager = new SchemaManager($"Data Source={_testDbPath}");
        _storage = new SqliteCacheStorage(_testDbPath);

        // Initialize schema
        await _schemaManager.EnsureCurrentSchemaAsync();
        await _storage.CleanupStartupAsync();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _storage.Dispose();

        var testDir = Path.GetDirectoryName(_testDbPath);
        if (testDir != null && Directory.Exists(testDir))
        {
            // Wait for file locks to be released
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    Directory.Delete(testDir, true);
                    break;
                }
                catch
                {
                    await Task.Delay(50);
                }
            }
        }
    }

    #region Upsert Tests

    [TestMethod]
    public async Task UpsertAsync_ShouldInsertNewEntry()
    {
        // Arrange
        var entry = new CacheEntry
        {
            CacheKey = "test_key",
            ContentType = "String",
            ContentVersion = 1,
            DataSize = 100,
            Tags = "tag1,tag2",
            GroupName = "group1",
            Priority = 1,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            EntryType = EntryType.Inline,
            Data = [1, 2, 3, 4, 5],
            ContentHash = "abc123"
        };

        // Act
        await _storage.UpsertAsync(entry, default);
        var result = await _storage.LookupAsync("test_key", default);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("test_key", result.CacheKey);
        Assert.AreEqual("String", result.ContentType);
        Assert.AreEqual(1, result.ContentVersion);
    }

    [TestMethod]
    public async Task UpsertAsync_ShouldUpdateExistingEntry()
    {
        // Arrange
        var key = "update_key";
        var entry1 = new CacheEntry
        {
            CacheKey = key,
            ContentType = "String",
            DataSize = 50,
            EntryType = EntryType.Inline,
            Data = [1, 2, 3],
            ContentHash = "hash1"
        };

        var entry2 = new CacheEntry
        {
            CacheKey = key,
            ContentType = "String",
            DataSize = 150,
            EntryType = EntryType.Inline,
            Data = [1, 2, 3, 4, 5, 6, 7, 8, 9],
            ContentHash = "hash2"
        };

        // Act
        await _storage.UpsertAsync(entry1, default);
        await _storage.UpsertAsync(entry2, default);
        var result = await _storage.LookupAsync(key, default);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(150, result.DataSize);
        Assert.AreEqual("hash2", result.ContentHash);
    }

    #endregion

    #region Lookup Tests

    [TestMethod]
    public async Task LookupAsync_ShouldReturnEntryByKey()
    {
        // Arrange
        var entry = new CacheEntry
        {
            CacheKey = "lookup_key",
            ContentType = "Int32",
            DataSize = 4,
            EntryType = EntryType.Inline,
            Data = [42, 0, 0, 0],
            ContentHash = "hash"
        };
        await _storage.UpsertAsync(entry, default);

        // Act
        var result = await _storage.LookupAsync("lookup_key", default);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("Int32", result.ContentType);
    }

    [TestMethod]
    public async Task LookupAsync_ShouldReturnNullForNonExistentKey()
    {
        // Act
        var result = await _storage.LookupAsync("non_existent_key", default);

        // Assert
        Assert.IsNull(result);
    }

    #endregion

    #region Touch Tests

    [TestMethod]
    public async Task TouchAsync_ShouldUpdateLastAccessTime()
    {
        // Arrange
        var entry = new CacheEntry
        {
            CacheKey = "touch_key",
            ContentType = "String",
            DataSize = 10,
            EntryType = EntryType.Inline,
            Data = [1, 2, 3],
            ContentHash = "hash"
        };
        await _storage.UpsertAsync(entry, default);

        var beforeTouch = await _storage.LookupAsync("touch_key", default);
        await Task.Delay(100);

        // Act
        await _storage.TouchAsync("touch_key", default);
        var afterTouch = await _storage.LookupAsync("touch_key", default);

        // Assert
        Assert.IsNotNull(beforeTouch);
        Assert.IsNotNull(afterTouch);
        Assert.IsTrue(afterTouch.LastAccessAt >= beforeTouch.LastAccessAt);
    }

    #endregion

    #region Delete Tests

    [TestMethod]
    public async Task DeleteAsync_ShouldRemoveEntry()
    {
        // Arrange
        var entry = new CacheEntry
        {
            CacheKey = "delete_key",
            ContentType = "String",
            DataSize = 5,
            EntryType = EntryType.Inline,
            Data = [1],
            ContentHash = "hash"
        };
        await _storage.UpsertAsync(entry, default);

        // Act
        var deleted = await _storage.DeleteAsync("delete_key", default);
        var result = await _storage.LookupAsync("delete_key", default);

        // Assert
        Assert.IsTrue(deleted);
        Assert.IsNull(result);
    }

    [TestMethod]
    public async Task DeleteAsync_ShouldReturnFalseForNonExistentKey()
    {
        // Act
        var deleted = await _storage.DeleteAsync("non_existent", default);

        // Assert
        Assert.IsFalse(deleted);
    }

    #endregion

    #region Group Operations Tests

    [TestMethod]
    public async Task DeleteByGroupAsync_ShouldRemoveAllItemsInGroup()
    {
        // Arrange
        var groupName = "test_group";
        for (int i = 0; i < 3; i++)
        {
            var entry = new CacheEntry
            {
                CacheKey = $"group_key_{i}",
                ContentType = "String",
                DataSize = 10,
                EntryType = EntryType.Inline,
                Data = [1],
                ContentHash = "hash",
                GroupName = groupName
            };
            await _storage.UpsertAsync(entry, default);
        }

        // Act
        var deletedCount = await _storage.DeleteByGroupAsync(groupName, default);

        // Assert
        Assert.AreEqual(3, deletedCount);
        Assert.IsNull(await _storage.LookupAsync("group_key_0", default));
    }

    [TestMethod]
    public async Task GetFileHashesByGroupAsync_ShouldReturnOnlyFileHashes()
    {
        // Arrange
        var groupName = "file_group";

        // Add entry with file hash
        var entry1 = new CacheEntry
        {
            CacheKey = "file_key_1",
            ContentType = "Binary",
            DataSize = 1000,
            EntryType = EntryType.FileRef,
            FileHash = "hash1_abc123",
            ContentHash = "content_hash1",
            GroupName = groupName
        };
        await _storage.UpsertAsync(entry1, default);

        // Add entry with file hash
        var entry2 = new CacheEntry
        {
            CacheKey = "file_key_2",
            ContentType = "Binary",
            DataSize = 2000,
            EntryType = EntryType.FileRef,
            FileHash = "hash2_def456",
            ContentHash = "content_hash2",
            GroupName = groupName
        };
        await _storage.UpsertAsync(entry2, default);

        // Add inline entry (should not be included)
        var entry3 = new CacheEntry
        {
            CacheKey = "inline_key",
            ContentType = "String",
            DataSize = 100,
            EntryType = EntryType.Inline,
            Data = [1, 2, 3],
            ContentHash = "content_hash3",
            GroupName = groupName
        };
        await _storage.UpsertAsync(entry3, default);

        // Act
        var fileHashes = await _storage.GetFileHashesByGroupAsync(groupName, default);

        // Assert
        Assert.AreEqual(2, fileHashes.Count);
        Assert.IsTrue(fileHashes.Contains("hash1_abc123"));
        Assert.IsTrue(fileHashes.Contains("hash2_def456"));
    }

    #endregion

    #region Tag Operations Tests

    [TestMethod]
    public async Task DeleteByTagAsync_ShouldRemoveItemsWithTag()
    {
        // Arrange
        var tag = "important";

        var entry1 = new CacheEntry
        {
            CacheKey = "tagged_1",
            ContentType = "String",
            DataSize = 10,
            EntryType = EntryType.Inline,
            Data = [1],
            ContentHash = "hash1",
            Tags = tag
        };
        await _storage.UpsertAsync(entry1, default);

        var entry2 = new CacheEntry
        {
            CacheKey = "tagged_2",
            ContentType = "String",
            DataSize = 10,
            EntryType = EntryType.Inline,
            Data = [1],
            ContentHash = "hash2",
            Tags = "other_tag"
        };
        await _storage.UpsertAsync(entry2, default);

        // Act
        var deletedCount = await _storage.DeleteByTagAsync(tag, default);

        // Assert
        Assert.IsTrue(deletedCount >= 1);
        Assert.IsNull(await _storage.LookupAsync("tagged_1", default));
        Assert.IsNotNull(await _storage.LookupAsync("tagged_2", default));
    }

    #endregion

    #region Expiration Tests

    [TestMethod]
    public async Task DeleteExpriedAsync_ShouldRemoveExpiredEntries()
    {
        // Arrange
        var pastTime = DateTime.UtcNow.AddHours(-1);

        var expiredEntry = new CacheEntry
        {
            CacheKey = "expired",
            ContentType = "String",
            DataSize = 10,
            EntryType = EntryType.Inline,
            Data = [1],
            ContentHash = "hash",
            ExpiresAt = pastTime
        };
        await _storage.UpsertAsync(expiredEntry, default);

        var validEntry = new CacheEntry
        {
            CacheKey = "valid",
            ContentType = "String",
            DataSize = 10,
            EntryType = EntryType.Inline,
            Data = [1],
            ContentHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        await _storage.UpsertAsync(validEntry, default);

        // Act
        var deletedCount = await _storage.DeleteExpiredAsync(DateTime.UtcNow, default);

        // Assert
        Assert.IsTrue(deletedCount >= 1);
        Assert.IsNull(await _storage.LookupAsync("expired", default));
        Assert.IsNotNull(await _storage.LookupAsync("valid", default));
    }

    #endregion

    #region Statistics Tests

    [TestMethod]
    public async Task GetStatsAsync_ShouldReturnValidStatistics()
    {
        // Arrange
        var inline = new CacheEntry
        {
            CacheKey = "inline",
            ContentType = "String",
            DataSize = 100,
            EntryType = EntryType.Inline,
            Data = new byte[100],
            ContentHash = "hash1"
        };
        await _storage.UpsertAsync(inline, default);

        var fileRef = new CacheEntry
        {
            CacheKey = "fileref",
            ContentType = "Binary",
            DataSize = 500000,
            EntryType = EntryType.FileRef,
            FileHash = "filehash",
            ContentHash = "hash2"
        };
        await _storage.UpsertAsync(fileRef, default);

        // Act
        var stats = await _storage.GetStatsAsync(default);

        // Assert
        Assert.AreEqual(2, stats.TotalEntries);
        Assert.AreEqual(500100, stats.TotalSizeBytes);
        Assert.AreEqual(1, stats.InlineEntries);
        Assert.AreEqual(1, stats.FileEntries);
    }

    #endregion

    #region File Hash Queries Tests

    [TestMethod]
    public async Task GetAllFileHashesAsync_ShouldReturnAllFileHashes()
    {
        // Arrange
        var entry1 = new CacheEntry
        {
            CacheKey = "file1",
            ContentType = "Binary",
            DataSize = 1000,
            EntryType = EntryType.FileRef,
            FileHash = "hash_file_1",
            ContentHash = "content1"
        };
        await _storage.UpsertAsync(entry1, default);

        var entry2 = new CacheEntry
        {
            CacheKey = "file2",
            ContentType = "Binary",
            DataSize = 2000,
            EntryType = EntryType.FileRef,
            FileHash = "hash_file_2",
            ContentHash = "content2"
        };
        await _storage.UpsertAsync(entry2, default);

        // Inline entry (should not be included)
        var entry3 = new CacheEntry
        {
            CacheKey = "inline",
            ContentType = "String",
            DataSize = 100,
            EntryType = EntryType.Inline,
            Data = [1],
            ContentHash = "content3"
        };
        await _storage.UpsertAsync(entry3, default);

        // Act
        var hashes = await _storage.GetAllFileHashesAsync(default);

        // Assert
        Assert.AreEqual(2, hashes.Count);
        Assert.IsTrue(hashes.Contains("hash_file_1"));
        Assert.IsTrue(hashes.Contains("hash_file_2"));
    }

    #endregion

    #region Eviction Candidate Queries Tests

    [TestMethod]
    public async Task GetEvictionCandidatesAsync_ShouldReturnCandidatesOrderedByPriority()
    {
        // Arrange - Add entries with different priorities
        var highPriority = new CacheEntry
        {
            CacheKey = "high",
            ContentType = "String",
            DataSize = 100,
            EntryType = EntryType.Inline,
            Data = [1],
            ContentHash = "hash1",
            Priority = 2 // High
        };
        await _storage.UpsertAsync(highPriority, default);

        var lowPriority = new CacheEntry
        {
            CacheKey = "low",
            ContentType = "String",
            DataSize = 100,
            EntryType = EntryType.Inline,
            Data = [1],
            ContentHash = "hash2",
            Priority = 0 // Low
        };
        await _storage.UpsertAsync(lowPriority, default);

        // Act
        var candidates = await _storage.GetEvictionCandidatesAsync(10, default);

        // Assert
        Assert.IsTrue(candidates.Count > 0);
        // Lower priority items should come first for eviction
        var lowIndex = candidates.FindIndex(c => c.CacheKey == "low");
        var highIndex = candidates.FindIndex(c => c.CacheKey == "high");
        Assert.IsTrue(lowIndex < highIndex, "Low priority should be evicted before high priority");
    }

    #endregion

    #region Compact Tests

    [TestMethod]
    public async Task CompactAsync_ShouldExecuteWithoutError()
    {
        // Arrange
        var entry = new CacheEntry
        {
            CacheKey = "compact_test",
            ContentType = "String",
            DataSize = 50,
            EntryType = EntryType.Inline,
            Data = [1, 2, 3],
            ContentHash = "hash"
        };
        await _storage.UpsertAsync(entry, CancellationToken.None);

        // Act & Assert
        try
        {
            await _storage.CompactAsync(CancellationToken.None);
            Assert.IsTrue(true, "Compact should execute without error");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Compact failed with exception: {ex.Message}");
        }
    }

    #endregion
}
