using System.Buffers;

namespace PCL.Core.UI.Media;

/// <summary>一帧原始视频数据，由显示视图租借并在消费后归还。</summary>
public sealed class VideoFrameBuffer : IDisposable
{
    private Action<VideoFrameBuffer>? _release;

    private VideoFrameBuffer(
        byte[] data,
        int length,
        Action<VideoFrameBuffer> release)
    {
        Data = data;
        Length = length;
        _release = release;
    }

    public byte[] Data { get; }

    public int Length { get; }

    public void Dispose()
    {
        Interlocked.Exchange(ref _release, null)?.Invoke(this);
    }

    public static VideoFrameBuffer Rent(int length)
    {
        var data = ArrayPool<byte>.Shared.Rent(length);
        return new VideoFrameBuffer(
            data,
            length,
            buffer => ArrayPool<byte>.Shared.Return(buffer.Data));
    }
}