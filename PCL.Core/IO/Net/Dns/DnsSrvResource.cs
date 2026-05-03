using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Ae.Dns.Protocol.Records;

namespace PCL.Core.IO.Net.Dns;

public class DnsSrvResource : IDnsResource
{
    public string Target { get; set; } = "";
    public int Weight { get; set; }
    public int Priority { get; set; }
    public int Port { get; set; }

    public void WriteBytes(Memory<byte> bytes, ref int offset)
    {
        // 6 Bytes for priority, weight, port and 2 bytes for length
        var buf = bytes.Span[offset..];
        BinaryPrimitives.WriteUInt16BigEndian(buf[offset..(offset + 2)], (ushort)Priority);
        offset += 2;
        BinaryPrimitives.WriteUInt16BigEndian(buf[offset..(offset + 2)], (ushort)Weight);
        offset += 2;
        BinaryPrimitives.WriteUInt16BigEndian(buf[offset..(offset + 2)], (ushort)Port);
        offset += 2;
        // target string
        foreach (var seg in Target.Split('.'))
        {
            var segBuf = Encoding.UTF8.GetBytes(seg);
            var segLength = (byte)segBuf.Length;
            buf[offset] = segLength;
            offset++;
            segBuf.CopyTo(buf[offset..]);
            offset += segBuf.Length;
        }
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
