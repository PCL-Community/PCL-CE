using PCL.Core.IO.Download.Core;
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PCL.Core.IO.Download.Scheduling;

public class ChunkScheduler
{
    private readonly Channel<ChunkInfo> _chunkChannel;
    private int _pendingChunks;

    public bool HasPendingChunks => _pendingChunks > 0;

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

    public void MarkChunkCompleted()
    {
        if (Interlocked.Decrement(ref _pendingChunks) == 0)
        {
            _chunkChannel.Writer.Complete();
        }
    }
}