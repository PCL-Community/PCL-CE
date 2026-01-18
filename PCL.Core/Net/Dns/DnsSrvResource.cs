using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Ae.Dns.Protocol.Records;

namespace PCL.Core.Net.Dns;

public class DnsSrvResource : IDnsResource
{
    public string Target { get; set; } = "";
    public int Weight { get; set; }
    public int Priority { get; set; }
    public int Port { get; set; }

    public void WriteBytes(Memory<byte> bytes, ref int offset)
    {
        // 6 Bytes for priority, weight, port and 2 bytes for length
        var length = 8 + Target.Length;
        var buf = new byte[length];
        var span = buf.AsSpan();
        BinaryPrimitives.WriteUInt16BigEndian(span[..2], (ushort)Priority);
        BinaryPrimitives.WriteUInt16BigEndian(span[2..4], (ushort)Weight);
        BinaryPrimitives.WriteUInt16BigEndian(span[4..6], (ushort)Port);
        BinaryPrimitives.WriteUInt16BigEndian(span[6..8], (ushort)Target.Length);
        Encoding.UTF8.GetBytes(Target).CopyTo(span[8..]);
    }

    public void ReadBytes(ReadOnlyMemory<byte> bytes, ref int offset, int length)
    {
        var buf = bytes.Slice(offset, length).Span;
        offset += length;
        var priorityBuf = buf[..2];
        Priority = BinaryPrimitives.ReadUInt16BigEndian(priorityBuf);
        var weightBuf = buf[2..4];
        Weight = BinaryPrimitives.ReadUInt16BigEndian(weightBuf);
        var portBuf = buf[4..6];
        Port = BinaryPrimitives.ReadUInt16BigEndian(portBuf);
        // left target buf. 1 Byte for length, then the string. Until we got end of string.
        var segments = new List<string>();
        var i = 6;
        while (i < length)
        {
            var segLength = buf[i];
            if (segLength == 0) break;
            i++;
            if (i + segLength > length)
            {
                throw new ArgumentException("Invalid DNS SRV resource record: segment length exceeds buffer size");
            }

            segments.Add(Encoding.UTF8.GetString(buf.Slice(i, segLength)));
            i += segLength;
        }

        Target = string.Join('.', segments);
    }
}