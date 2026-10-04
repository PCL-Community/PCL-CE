using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.Hash;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace PCL.Core.Test.Hash;

[TestClass]
public class HashCacheTest
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Initialize()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PCLTest", "HashCache", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private sealed record FileItem(string FilePath, string MD5, string SHA1, string SHA256, string SHA512, string MurmurHash2);

    private static async Task<FileItem> CreateRandomFile(string dir, int size)
    {
        var path = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".bin");
        var data = RandomNumberGenerator.GetBytes(size);
        await File.WriteAllBytesAsync(path, data).ConfigureAwait(false);
        return new FileItem(
            path,
            MD5Provider.Instance.ComputeHash(data).ToHexString(),
            SHA1Provider.Instance.ComputeHash(data).ToHexString(),
            SHA256Provider.Instance.ComputeHash(data).ToHexString(),
            SHA512Provider.Instance.ComputeHash(data).ToHexString(),
            MurmurHash2Provider.Instance.ComputeHash(data).ToHexString()
        );
    }

    [TestMethod]
    public async Task TestCache()
    {
        var testFiles = new Dictionary<string, string>();
        for (var i = 0; i < 10; i++)
        {
            var file = await CreateRandomFile(_tempDir, 1024).ConfigureAwait(false);
            testFiles.Add(file.FilePath, file.SHA256);
        }

        var hashCache = new HashCache(Path.Combine(_tempDir, ".hash_cache.db"));

        for (var i = 0; i < 5; i++)
        {
            foreach (var (filePath, fileHash) in testFiles)
            {
                Assert.AreEqual(await hashCache.GetSHA256Async(filePath), fileHash);
            }
        }
    }

    [TestMethod]
    public async Task TestSingleFile_SingleDB_AllAlgorithms_Concurrent()
    {
        var file = await CreateRandomFile(_tempDir, 4096).ConfigureAwait(false);
        var cache = new HashCache(Path.Combine(_tempDir, ".hash_cache.db"));

        var tasks = new Task<string>[]
        {
            cache.GetMD5Async(file.FilePath),
            cache.GetSHA1Async(file.FilePath),
            cache.GetSHA256Async(file.FilePath),
            cache.GetSHA512Async(file.FilePath),
            cache.GetMurmurHash2Async(file.FilePath),
        };

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        Assert.AreEqual(file.MD5, results[0]);
        Assert.AreEqual(file.SHA1, results[1]);
        Assert.AreEqual(file.SHA256, results[2]);
        Assert.AreEqual(file.SHA512, results[3]);
        Assert.AreEqual(file.MurmurHash2, results[4]);

        var repeat = await Task.WhenAll(
            cache.GetMD5Async(file.FilePath),
            cache.GetSHA1Async(file.FilePath),
            cache.GetSHA256Async(file.FilePath),
            cache.GetSHA512Async(file.FilePath),
            cache.GetMurmurHash2Async(file.FilePath)
        ).ConfigureAwait(false);

        Assert.AreEqual(file.MD5, repeat[0]);
        Assert.AreEqual(file.SHA1, repeat[1]);
        Assert.AreEqual(file.SHA256, repeat[2]);
        Assert.AreEqual(file.SHA512, repeat[3]);
        Assert.AreEqual(file.MurmurHash2, repeat[4]);
    }

    [TestMethod]
    public async Task TestSingleFile_SingleDB_SameAlgo_HeavyConcurrent()
    {
        var file = await CreateRandomFile(_tempDir, 1024).ConfigureAwait(false);
        var cache = new HashCache(Path.Combine(_tempDir, ".hash_cache.db"));

        var count = 100;
        var tasks = new Task<string>[count];
        for (var i = 0; i < count; i++)
            tasks[i] = cache.GetSHA256Async(file.FilePath);

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var result in results)
            Assert.AreEqual(file.SHA256, result);
    }

    [TestMethod]
    public async Task TestManyFiles_SingleDB_AllAlgorithms_Concurrent()
    {
        var files = new FileItem[50];
        for (var i = 0; i < files.Length; i++)
            files[i] = await CreateRandomFile(_tempDir, RandomNumberGenerator.GetInt32(1, 65536)).ConfigureAwait(false);

        var cache = new HashCache(Path.Combine(_tempDir, ".hash_cache.db"));

        var tasks = new List<Task>();
        var results = new ConcurrentBag<(string path, string algo, string hash)>();

        foreach (var file in files)
        {
            tasks.Add(Task.Run(async () =>
            {
                var md5 = await cache.GetMD5Async(file.FilePath).ConfigureAwait(false);
                results.Add((file.FilePath, "MD5", md5));
            }));
            tasks.Add(Task.Run(async () =>
            {
                var sha1 = await cache.GetSHA1Async(file.FilePath).ConfigureAwait(false);
                results.Add((file.FilePath, "SHA1", sha1));
            }));
            tasks.Add(Task.Run(async () =>
            {
                var sha256 = await cache.GetSHA256Async(file.FilePath).ConfigureAwait(false);
                results.Add((file.FilePath, "SHA256", sha256));
            }));
            tasks.Add(Task.Run(async () =>
            {
                var sha512 = await cache.GetSHA512Async(file.FilePath).ConfigureAwait(false);
                results.Add((file.FilePath, "SHA512", sha512));
            }));
            tasks.Add(Task.Run(async () =>
            {
                var mmh2 = await cache.GetMurmurHash2Async(file.FilePath).ConfigureAwait(false);
                results.Add((file.FilePath, "MurmurHash2", mmh2));
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var file in files)
        {
            foreach (var r in results.Where(r => r.path == file.FilePath))
            {
                var expected = r.algo switch
                {
                    "MD5" => file.MD5,
                    "SHA1" => file.SHA1,
                    "SHA256" => file.SHA256,
                    "SHA512" => file.SHA512,
                    "MurmurHash2" => file.MurmurHash2,
                    _ => throw new InvalidOperationException(),
                };
                Assert.AreEqual(expected, r.hash, $"{r.algo} mismatch for {file.FilePath}");
            }
        }
    }

    [TestMethod]
    public async Task TestManyFiles_SingleDB_SameAlgo_Concurrent()
    {
        var files = new FileItem[50];
        for (var i = 0; i < files.Length; i++)
            files[i] = await CreateRandomFile(_tempDir, RandomNumberGenerator.GetInt32(1, 16384)).ConfigureAwait(false);

        var cache = new HashCache(Path.Combine(_tempDir, ".hash_cache.db"));

        var tasks = files.Select(f => Task.Run(() => cache.GetSHA256Async(f.FilePath)));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        for (var i = 0; i < files.Length; i++)
            Assert.AreEqual(files[i].SHA256, results[i]);
    }

    [TestMethod]
    public async Task TestMultipleInstances_SharedDB_Concurrent()
    {
        var files = new FileItem[20];
        for (var i = 0; i < files.Length; i++)
            files[i] = await CreateRandomFile(_tempDir, RandomNumberGenerator.GetInt32(1, 8192)).ConfigureAwait(false);

        var cache1 = new HashCache(Path.Combine(_tempDir, ".hash_cache.db"));
        var cache2 = new HashCache(Path.Combine(_tempDir, ".hash_cache.db"));
        var cache3 = new HashCache(Path.Combine(_tempDir, ".hash_cache.db"));

        var results = new ConcurrentBag<(int fileIndex, int algoIndex, string hash)>();
        var tasks = new List<Task>();
        for (var i = 0; i < files.Length; i++)
        {
            var fi = i;
            var f = files[i];
            tasks.Add(Task.Run(async () =>
            {
                var h = await cache1.GetMD5Async(f.FilePath).ConfigureAwait(false);
                results.Add((fi, 0, h));
            }));
            tasks.Add(Task.Run(async () =>
            {
                var h = await cache2.GetSHA1Async(f.FilePath).ConfigureAwait(false);
                results.Add((fi, 1, h));
            }));
            tasks.Add(Task.Run(async () =>
            {
                var h = await cache3.GetSHA256Async(f.FilePath).ConfigureAwait(false);
                results.Add((fi, 2, h));
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var r in results)
        {
            var expected = r.algoIndex switch
            {
                0 => files[r.fileIndex].MD5,
                1 => files[r.fileIndex].SHA1,
                2 => files[r.fileIndex].SHA256,
                _ => throw new InvalidOperationException(),
            };
            Assert.AreEqual(expected, r.hash);
        }

        var repeat1 = await cache1.GetSHA512Async(files[0].FilePath).ConfigureAwait(false);
        Assert.AreEqual(files[0].SHA512, repeat1);
    }

    [TestMethod]
    public async Task TestMixedCacheHitAndCompute_Concurrent()
    {
        var existingFile = await CreateRandomFile(_tempDir, 2048).ConfigureAwait(false);
        var newFile = await CreateRandomFile(_tempDir, 2048).ConfigureAwait(false);

        var preload = new HashCache(Path.Combine(_tempDir, ".hash_cache.db"));
        await preload.GetSHA256Async(existingFile.FilePath).ConfigureAwait(false);

        var cache = new HashCache(Path.Combine(_tempDir, ".hash_cache.db"));
        var tasks = new Task<string>[]
        {
            cache.GetSHA256Async(existingFile.FilePath),
            cache.GetSHA256Async(existingFile.FilePath),
            cache.GetSHA256Async(newFile.FilePath),
            cache.GetSHA256Async(newFile.FilePath),
        };

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        Assert.AreEqual(existingFile.SHA256, results[0]);
        Assert.AreEqual(existingFile.SHA256, results[1]);
        Assert.AreEqual(newFile.SHA256, results[2]);
        Assert.AreEqual(newFile.SHA256, results[3]);
    }

    [TestMethod]
    public async Task TestConcurrentReadDuringWrite_DoesNotBlock()
    {
        var bigFile = await CreateRandomFile(_tempDir, 1024 * 1024).ConfigureAwait(false);
        var smallFile = await CreateRandomFile(_tempDir, 128).ConfigureAwait(false);
        var cache = new HashCache(Path.Combine(_tempDir, ".hash_cache.db"));

        var bigTask = cache.GetSHA512Async(bigFile.FilePath);
        var smallTasks = new Task<string>[20];
        for (var i = 0; i < smallTasks.Length; i++)
            smallTasks[i] = cache.GetSHA256Async(smallFile.FilePath);

        var bigResult = await bigTask.ConfigureAwait(false);
        var smallResults = await Task.WhenAll(smallTasks).ConfigureAwait(false);

        Assert.AreEqual(bigFile.SHA512, bigResult);
        foreach (var r in smallResults)
            Assert.AreEqual(smallFile.SHA256, r);
    }

    [TestMethod]
    public async Task TestFileModifiedDuringConcurrentAccess()
    {
        var file = await CreateRandomFile(_tempDir, 512).ConfigureAwait(false);
        var cache = new HashCache(Path.Combine(_tempDir, ".hash_cache.db"));

        var hash1 = await cache.GetSHA256Async(file.FilePath).ConfigureAwait(false);
        Assert.AreEqual(file.SHA256, hash1);

        var modifiedFile = await CreateRandomFile(_tempDir, 512).ConfigureAwait(false);
        File.Copy(modifiedFile.FilePath, file.FilePath, true);

        var tasks = new Task<string>[10];
        for (var i = 0; i < tasks.Length; i++)
            tasks[i] = cache.GetSHA256Async(file.FilePath);

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        var expectedNewHash = modifiedFile.SHA256;
        foreach (var r in results)
            Assert.AreEqual(expectedNewHash, r);
    }

    [TestMethod]
    public async Task TestConcurrentDeleteAndQuery()
    {
        var file = await CreateRandomFile(_tempDir, 256).ConfigureAwait(false);
        var cache = new HashCache(Path.Combine(_tempDir, ".hash_cache.db"));

        var hash = await cache.GetSHA256Async(file.FilePath).ConfigureAwait(false);
        Assert.AreEqual(file.SHA256, hash);

        File.Delete(file.FilePath);

        var exceptions = new ConcurrentBag<Exception>();
        var tasks = new[]
        {
            Task.Run(async () =>
            {
                try { await cache.GetSHA256Async(file.FilePath).ConfigureAwait(false); }
                catch (FileNotFoundException ex) { exceptions.Add(ex); }
            }),
            Task.Run(async () =>
            {
                try { await cache.GetSHA256Async(file.FilePath).ConfigureAwait(false); }
                catch (FileNotFoundException ex) { exceptions.Add(ex); }
            }),
        };

        await Task.WhenAll(tasks).ConfigureAwait(false);
        Assert.AreEqual(2, exceptions.Count);
    }
}
