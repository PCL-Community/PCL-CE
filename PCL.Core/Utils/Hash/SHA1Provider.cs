using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using PCL.Core.Logging;

namespace PCL.Core.Utils.Hash;

public class SHA1Provider : IHashProvider
{
    public static SHA1Provider Instance { get; } = new();

    public string ComputeHash(Stream input)
    {
        var originalPosition = input.Position;
        try
        {
            return HashResultHandler.ConvertResultToString(SHA1.HashData(input), Length);
        }
        catch (Exception e)
        {
            LogWrapper.Error(e, "Hash", "Compute hash failed");
            throw;
        }
        finally
        {
            input.Position = originalPosition;
        }
    }
    public string ComputeHash(byte[] input) => HashResultHandler.ConvertResultToString(SHA1.HashData(input), Length);
    public string ComputeHash(ReadOnlySpan<byte> input) => HashResultHandler.ConvertResultToString(SHA1.HashData(input), Length);

    public string ComputeHash(string input, Encoding? en = null) => ComputeHash(
        en == null
            ? Encoding.UTF8.GetBytes(input)
            : en.GetBytes(input));

    public int Length => 40;
}