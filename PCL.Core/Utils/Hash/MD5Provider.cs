using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using PCL.Core.Logging;

namespace PCL.Core.Utils.Hash;

// ReSharper disable once InconsistentNaming
public class MD5Provider : IHashProvider {
    public static MD5Provider Instance { get; } = new();
    
    public string ComputeHash(Stream input)
    {
        var originalPos = input.Position;
        try
        {
            return HashResultHandler.ConvertResultToString(MD5.HashData(input), Length);
        }
        catch (Exception e)
        {
            LogWrapper.Error(e, "Hash", "Compute hash failed");
            throw;
        }
        finally
        {
            input.Position = originalPos;
        }
    }
    public string ComputeHash(byte[] input) => HashResultHandler.ConvertResultToString(MD5.HashData(input), Length);
    public string ComputeHash(ReadOnlySpan<byte> input) => HashResultHandler.ConvertResultToString(MD5.HashData(input), Length);
    public string ComputeHash(string input, Encoding? en = null) => ComputeHash(
        en == null
            ? Encoding.UTF8.GetBytes(input)
            : en.GetBytes(input));

    public int Length => 32;
}