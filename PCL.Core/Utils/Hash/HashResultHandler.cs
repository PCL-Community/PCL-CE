using System.Text;

namespace PCL.Core.Utils.Hash;

public static class HashResultHandler
{
    public static string ConvertResultToString(byte[] hashResult, int length)
    {
        var sb = new StringBuilder(length);
        foreach (var b in hashResult)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }
}