using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Utils.Hash;

public interface IHashProvider
{
    ValueTask<byte[]> ComputeHashAsync(Stream input, CancellationToken cancellationToken = default);
    byte[] ComputeHash(Stream input);
    byte[] ComputeHash(ReadOnlySpan<byte> input);
    byte[] ComputeHash(string input, Encoding? en = null);
    int Length { get; }
}