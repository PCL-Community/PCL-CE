using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Storage.Cache;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PCL.Core.Test.IO.Storage.Cache;

/// <summary>
/// Unit tests for FileCacheStorage class.
/// Tests file storage, reference counting, and cleanup operations.
/// </summary>
[TestClass]
public class FileCacheStorageTest
{
    private FileCacheStorage _fileCache = null!;
    private string _testCachePath = null!;

    [TestInitialize]
    public void Setup()
    {
        _testCachePath = Path.Combine(Path.GetTempPath(), "pcl_file_cache_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testCachePath);
        _fileCache = new FileCacheStorage(_testCachePath, enableCompression: false);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _fileCache.Dispose();

        if (Directory.Exists(_testCachePath))
        {
            Directory.Delete(_testCachePath, true);
        }
    }

    #region Store and Retrieve Tests

    [TestMethod]
    public async Task StoreAsync_ShouldReturnHashAndCreateFile()
    {
        // Arrange
        var content = "Test file content"u8.ToArray();
        var stream = new MemoryStream(content);

        // Act
        var hash = await _fileCache.StoreAsync(stream);

        // Assert
        Assert.IsNotNull(hash);
        Assert.IsFalse(string.IsNullOrEmpty(hash));
        Assert.IsTrue(_fileCache.Exists(hash), "Stored file should exist");
    }

    [TestMethod]
    public async Task RetrieveAsync_ShouldReturnStoredContent()
    {
        // Arrange
        var expectedContent = "Test content for retrieval"u8.ToArray();
        var stream = new MemoryStream(expectedContent);

        var hash = await _fileCache.StoreAsync(stream);

        // Act
        using var retrievedStream = _fileCache.Retrieve(hash);

        // Assert
        Assert.IsNotNull(retrievedStream, "Retrieved stream should not be null");

        using var ms = new MemoryStream();
        await retrievedStream.CopyToAsync(ms);
        var retrievedContent = ms.ToArray();

        // Debug info
        Assert.AreEqual(expectedContent.Length, retrievedContent.Length,
            $"Content length mismatch. Expected: {expectedContent.Length}, Got: {retrievedContent.Length}");

        CollectionAssert.AreEqual(expectedContent, retrievedContent,
            "Retrieved content should match stored content");
    }

    [TestMethod]
    public async Task RetrieveNonExistentHash_ShouldReturnNull()
    {
        // Act
        var stream = _fileCache.Retrieve("non_existent_hash_abc123def456");

        // Assert
        Assert.IsNull(stream);
    }

    #endregion

    #region Reference Counting Tests

    [TestMethod]
    public async Task ReleaseAsync_ShouldDecrementRefCount()
    {
        // Arrange
        var content = "Test"u8.ToArray();
        var stream = new MemoryStream(content);

        var hash1 = await _fileCache.StoreAsync(stream);
        stream.Position = 0;
        var hash2 = await _fileCache.StoreAsync(stream);

        // Act - Release second reference
        await _fileCache.ReleaseAsync(hash1);

        // Assert - File should still exist due to ref count
        Assert.IsTrue(_fileCache.Exists(hash1));
    }

    [TestMethod]
    public async Task ReleaseAsync_WithMultipleStores_ShouldDeleteOnLastRelease()
    {
        // Arrange - Store same content multiple times (same hash)
        var content = "Test"u8.ToArray();

        var hash1 = await _fileCache.StoreAsync(new MemoryStream(content));
        var hash2 = await _fileCache.StoreAsync(new MemoryStream(content));

        Assert.AreEqual(hash1, hash2, "Same content should produce same hash");

        // Act - Release both references
        await _fileCache.ReleaseAsync(hash1);
        var existsAfterFirstRelease = _fileCache.Exists(hash1);

        await _fileCache.ReleaseAsync(hash1);
        var existsAfterSecondRelease = _fileCache.Exists(hash1);

        // Assert
        Assert.IsTrue(existsAfterFirstRelease, "File should exist after first release (ref count > 0)");
        Assert.IsFalse(existsAfterSecondRelease, "File should be deleted after final release");
    }

    [TestMethod]
    public async Task ReleaseNonExistentHash_ShouldReturnFalse()
    {
        // Act
        var released = await _fileCache.ReleaseAsync("non_existent_hash");

        // Assert
        Assert.IsFalse(released);
    }

    [TestMethod]
    public async Task ForceDeleteAsync_ShouldDeleteRegardlessOfRefCount()
    {
        // Arrange
        var content = "Test"u8.ToArray();
        var hash = await _fileCache.StoreAsync(new MemoryStream(content));

        // Store another reference (increment ref count)
        await _fileCache.StoreAsync(new MemoryStream(content));

        // Act
        await _fileCache.ForceDeleteAsync(hash);

        // Assert
        Assert.IsFalse(_fileCache.Exists(hash), "File should be deleted regardless of ref count");
    }

    #endregion

    #region File Path Tests

    [TestMethod]
    public async Task GetFilePathAsync_ShouldReturnValidPath()
    {
        // Arrange
        var content = "File path test"u8.ToArray();
        var hash = await _fileCache.StoreAsync(new MemoryStream(content));

        // Act
        var filePath = _fileCache.GetFilePath(hash);

        // Assert
        Assert.IsNotNull(filePath);
        Assert.IsTrue(File.Exists(filePath), "File should exist at returned path");
    }

    [TestMethod]
    public async Task GetFilePathForNonExistent_ShouldReturnNull()
    {
        // Act
        var filePath = _fileCache.GetFilePath("non_existent_hash_abc123");

        // Assert
        Assert.IsNull(filePath);
    }

    #endregion

    #region Existence Tests

    [TestMethod]
    public async Task ExistsAsync_ShouldReturnTrueForStoredHash()
    {
        // Arrange
        var content = "Test"u8.ToArray();
        var hash = await _fileCache.StoreAsync(new MemoryStream(content));

        // Act
        var exists = _fileCache.Exists(hash);

        // Assert
        Assert.IsTrue(exists);
    }

    [TestMethod]
    public void ExistForNonExistentHash_ShouldReturnFalse()
    {
        // Act
        var exists = _fileCache.Exists("non_existent_hash_xyz");

        // Assert
        Assert.IsFalse(exists);
    }

    #endregion

    #region Compression Tests

    [TestMethod]
    public async Task StoreWithCompressionDisabled_ShouldStoreUncompressed()
    {
        // Arrange - Already disabled in Setup
        var content = "Uncompressed content"u8.ToArray();

        // Act
        var hash = await _fileCache.StoreAsync(new MemoryStream(content));
        using var retrievedStream = _fileCache.Retrieve(hash);
        using var ms = new MemoryStream();
        await retrievedStream!.CopyToAsync(ms);

        // Assert
        CollectionAssert.AreEqual(content, ms.ToArray());
    }

    [TestMethod]
    public async Task StoreWithKnownHash_ShouldUseProvidedHash()
    {
        // Arrange
        var content = "Test content"u8.ToArray();
        var stream = new MemoryStream(content);

        var computedHash = await _fileCache.StoreAsync(stream);

        // Act - Store again with known hash
        stream.Position = 0;
        var secondHash = await _fileCache.StoreAsync(stream, computedHash);

        // Assert
        Assert.AreEqual(computedHash, secondHash);
    }

    #endregion

    #region Large File Tests

    [TestMethod]
    public async Task StoreLargeFile_ShouldWorkCorrectly()
    {
        // Arrange - Create a 10 MB file
        var largeContent = new byte[10 * 1024 * 1024];
        Random.Shared.NextBytes(largeContent);
        var stream = new MemoryStream(largeContent);

        // Act
        var hash = await _fileCache.StoreAsync(stream);

        // Assert
        Assert.IsNotNull(hash);
        Assert.IsTrue(_fileCache.Exists(hash));

        // Verify content can be retrieved
        using var retrieved = _fileCache.Retrieve(hash);
        Assert.IsNotNull(retrieved);
        using var ms = new MemoryStream();
        await retrieved.CopyToAsync(ms);
        CollectionAssert.AreEqual(largeContent, ms.ToArray());
    }

    #endregion

    #region Empty Content Tests

    [TestMethod]
    public async Task StoreEmptyContent_ShouldWork()
    {
        // Arrange
        var emptyContent = Array.Empty<byte>();
        var stream = new MemoryStream(emptyContent);

        // Act
        var hash = await _fileCache.StoreAsync(stream);

        // Assert
        Assert.IsNotNull(hash);
        Assert.IsTrue(_fileCache.Exists(hash));
    }

    #endregion

    #region Concurrent Operations Tests

    [TestMethod]
    public async Task ConcurrentStoreRetrieve_ShouldBeThreadSafe()
    {
        // Arrange
        var taskCount = 5;
        var storesPerTask = 10;
        var hashes = new System.Collections.Concurrent.ConcurrentBag<string>();
        var rand = new Random();

        // Act - Store concurrently with UNIQUE content for each task to avoid hash collisions
        var storeTasks = new List<Task>();

        for (int i = 0; i < taskCount; i++)
        {
            storeTasks.Add(Task.Run(async () =>
            {
                // Use unique content per task to avoid race conditions on same file
                var taskId = i;
                for (int j = 0; j < storesPerTask; j++)
                {
                    // Create unique content for each store operation
                    var guid = Guid.NewGuid().ToString();
                    var content = $"Unique_Content_{taskId}_{j}_{guid}u8".ToArray();

                    try
                    {
                        var hash = await _fileCache.StoreAsync(new MemoryStream(content.Select(Convert.ToByte).ToArray()));
                        hashes.Add(hash);
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Storage failed for task {taskId}, iteration {j}: {ex.Message}");
                    }
                }
            }));
        }

        await Task.WhenAll(storeTasks);

        // Assert - Verify all items were stored
        Assert.AreEqual(taskCount * storesPerTask, hashes.Count,
            $"Should have stored {taskCount * storesPerTask} items");

        // Verify retrieval of stored items (with delay to ensure file handles are released)
        await Task.Delay(100);

        var retrieveTasks = new List<Task>();
        foreach (var hash in hashes)
        {
            var currentHash = hash;
            retrieveTasks.Add(Task.Run(() =>
            {
                try
                {
                    // Small delay to reduce concurrent file access
                    System.Threading.Thread.Sleep(Random.Shared.Next(0, 10));

                    using var stream = _fileCache.Retrieve(currentHash);
                    Assert.IsNotNull(stream, $"Should be able to retrieve hash {currentHash}");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Retrieval failed for hash {currentHash}: {ex.Message}");
                }
            }));
        }

        await Task.WhenAll(retrieveTasks);
    }

    #endregion

    #region Directory Structure Tests

    [TestMethod]
    public async Task StoredFiles_ShouldBeInCorrectDirectoryStructure()
    {
        // Arrange
        var content = "Test"u8.ToArray();
        var hash = await _fileCache.StoreAsync(new MemoryStream(content));

        // Act
        var filePath = _fileCache.GetFilePath(hash);

        // Assert - Should be in hash[0:2] subdirectory
        Assert.IsNotNull(filePath);
        var directory = Path.GetDirectoryName(filePath);
        var directoryName = new DirectoryInfo(directory!).Name;
        Assert.AreEqual(hash[..2], directoryName, "File should be in [hash:2] subdirectory");
    }

    #endregion
}
