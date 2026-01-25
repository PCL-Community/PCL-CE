using System;
using System.IO;
using System.Text;

namespace PCL.Core.Utils.Hash;

public interface IHashProvider
{
    string ComputeHash(Stream input);
    string ComputeHash(byte[] input);
    string ComputeHash(ReadOnlySpan<byte> input);
    string ComputeHash(string input, Encoding? en = null);
    int Length { get; }
}