using PCL.Core.IO.Download.Core;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PCL.Core.IO.Download.Scheduling;

/// <summary>
/// 分块管理器
/// </summary>
public class ChunkScheduler
{
    private readonly Channel<ChunkInfo> _chunkChannel;
    private int _pendingChunks;

    /// <summary>
    /// 是否仍有分块未完成
    /// </summary>
    public bool HasPendingChunks => _pendingChunks > 0;

    /// <summary>
    /// 分块管理器
    /// </summary>
    /// <param name="totalFileSize">总文件大小（字节）</param>
    /// <param name="chunkSize">分块大小</param>
    public ChunkScheduler(long totalFileSize, long chunkSize)
    {
        _chunkChannel = Channel.CreateUnbounded<ChunkInfo>();
        _InititalizeChunks(totalFileSize, chunkSize);
    }

    private void _InititalizeChunks(long totalFileSize, long chunkSize)
    {
        var offset = 0L;
        var index = 0;

        while (offset < totalFileSize)
        {
            var length = Math.Min(chunkSize, totalFileSize - offset);
            _chunkChannel.Writer.TryWrite(new ChunkInfo(offset, length, index++));

            Interlocked.Increment(ref _pendingChunks);
            offset += length;
        }
    }

    /// <summary>
    /// 获取下一个分块
    /// </summary>
    /// <param name="token">取消令牌</param>
    /// <returns>下一个分块（如果没有，则为null）</returns>
    public async ValueTask<ChunkInfo?> GetNextChunkAsync(CancellationToken token = default)
    {
        try
        {
            return await _chunkChannel.Reader.ReadAsync(token).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// 将完成部分的分块的未完成部分添加到队列中
    /// </summary>
    /// <param name="newStartOffset">偏移</param>
    /// <param name="remainingLength">剩余长度</param>
    /// <param name="originalIndex">原始索引</param>
    public void ReturnIncompleteChunk(long newStartOffset, long remainingLength, int originalIndex)
    {
        if (remainingLength > 0)
        {
            var remainingChunk = new ChunkInfo(newStartOffset, remainingLength, originalIndex);
            _chunkChannel.Writer.TryWrite(remainingChunk);
        }
        else
        {
            MarkChunkCompleted();
        }
    }

    /// <summary>
    /// 标记完成一个区块
    /// </summary>
    public void MarkChunkCompleted()
    {
        if (Interlocked.Decrement(ref _pendingChunks) == 0)
        {
            _chunkChannel.Writer.Complete();
        }
    }
}