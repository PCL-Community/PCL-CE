using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Download.Scheduling;

namespace PCL.Core.Test.Downloader;

[TestClass]
public class ChunkSchedulerTests
{
    [TestMethod]
    public async Task Initialize_ShouldSplitFileIntoCorrectChunks()
    {
        // Arrange
        long fileSize = 100;
        long chunkSize = 30;
        var scheduler = new ChunkScheduler(fileSize, chunkSize);
        var ct = CancellationToken.None;

        // Act & Assert
        var chunk1 = await scheduler.GetNextChunkAsync(ct);
        Assert.IsNotNull(chunk1);
        Assert.AreEqual(0, chunk1.Value.StartOffset);
        Assert.AreEqual(30, chunk1.Value.Length);

        var chunk2 = await scheduler.GetNextChunkAsync(ct);
        Assert.AreEqual(30, chunk2.Value.StartOffset);
        Assert.AreEqual(30, chunk2.Value.Length);

        var chunk3 = await scheduler.GetNextChunkAsync(ct);
        Assert.AreEqual(60, chunk3.Value.StartOffset);
        Assert.AreEqual(30, chunk3.Value.Length);

        var chunk4 = await scheduler.GetNextChunkAsync(ct); // 最后一个小块
        Assert.AreEqual(90, chunk4.Value.StartOffset);
        Assert.AreEqual(10, chunk4.Value.Length); // 100 - 90 = 10
    }

    [TestMethod]
    public async Task ReturnIncompleteChunk_ShouldPushRemainingDataBack()
    {
        // Arrange
        var scheduler = new ChunkScheduler(100, 100); // 只有1个块
        var ct = CancellationToken.None;
        var firstChunk = await scheduler.GetNextChunkAsync(ct);

        // Act
        // 假设下载了 40 bytes 后断开，退回剩余的 60 bytes
        scheduler.ReturnIncompleteChunk(
            newStartOffset: firstChunk.Value.StartOffset + 40,
            remainingLength: firstChunk.Value.Length - 40,
            originalIndex: firstChunk.Value.ChunkIndex);

        // Assert
        var remainingChunk = await scheduler.GetNextChunkAsync(ct);
        Assert.IsNotNull(remainingChunk);
        Assert.AreEqual(40, remainingChunk.Value.StartOffset);
        Assert.AreEqual(60, remainingChunk.Value.Length);
        Assert.AreEqual(0, remainingChunk.Value.ChunkIndex); // Index 应保持不变
    }

    [TestMethod]
    public async Task MarkChunkCompleted_ShouldCloseChannelWhenAllDone()
    {
        // Arrange
        var scheduler = new ChunkScheduler(100, 50); // 共 2 块
        var ct = CancellationToken.None;

        var chunk1 = await scheduler.GetNextChunkAsync(ct);
        var chunk2 = await scheduler.GetNextChunkAsync(ct);

        // Act
        scheduler.MarkChunkCompleted(); // 完成第1块

        // 此时不应该关闭，GetNextChunkAsync 会因为没数据而阻塞（为了测试，我们不 await 它，直接断言它没关闭）

        scheduler.MarkChunkCompleted(); // 完成第2块，通道应当关闭

        // Assert
        // 通道关闭后，GetNextChunkAsync 会返回 null 而不是一直阻塞
        var emptyChunk = await scheduler.GetNextChunkAsync(ct);
        Assert.IsNull(emptyChunk, "通道应已关闭，返回 null");
    }
}