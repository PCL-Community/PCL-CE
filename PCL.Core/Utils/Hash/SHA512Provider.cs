using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using PCL.Core.Logging;
using PCL.Core.Utils.Hash;

namespace PCL.Core.Utils.Hash;

public class SHA512Provider : IHashProvider
{
    public static SHA512Provider Instance { get; } = new();
    
    public string ComputeHash(Stream input)
    {
        var originalPosition = input.Position;
        try
        {
            return HashResultHandler.ConvertResultToString(SHA512.HashData(input), Length);
        }
        catch (Exception e)
        {
            LogWrapper.Error(e, "Hash", "Compute SHA512 failed");
            throw;
        }
        finally
        {
            input.Position = originalPosition;
        }
    }

    public string ComputeHash(byte[] input) => HashResultHandler.ConvertResultToString(SHA512.HashData(input), Length);

    public string ComputeHash(ReadOnlySpan<byte> input) =>
        HashResultHandler.ConvertResultToString(SHA512.HashData(input), Length);
    public string ComputeHash(string input, Encoding? en = null) => ComputeHash(
        en == null
            ? Encoding.UTF8.GetBytes(input)
            : en.GetBytes(input));

    public int Length => 128;
}