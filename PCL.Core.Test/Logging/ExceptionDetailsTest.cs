using System;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Logging;

namespace PCL.Core.Test.Logging;

[TestClass]
public sealed class ExceptionDetailsTest
{
    [TestMethod]
    public void GetDialogDetailsLimitsStackFrames()
    {
        Exception exception;
        try
        {
            ThrowRecursive(100);
            throw new InvalidOperationException("unreachable");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        var details = ExceptionDetails.GetDialogDetails(exception);

        Assert.Contains(nameof(InvalidOperationException), details);
        Assert.Contains(nameof(ThrowRecursive), details);

        var frameCount = CountStackFrames(details);
        Assert.IsLessThanOrEqualTo(
            20,
            frameCount,
            $"弹窗堆栈应限制在 20 帧以内，实际 {frameCount} 帧");
    }

    [TestMethod]
    public void GetDialogDetailsTruncatesLongMessage()
    {
        var exception = new InvalidOperationException(new string('x', 20000));

        var details = ExceptionDetails.GetDialogDetails(exception);

        Assert.IsLessThanOrEqualTo(
            8192 + 8,
            details.Length,
            $"详情长度应受限，实际 {details.Length}");
        Assert.Contains("…", details);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowRecursive(int depth)
    {
        while (true)
        {
            if (depth == 0) throw new InvalidOperationException("deep stack");
            depth -= 1;
        }
    }

    private static int CountStackFrames(string details)
    {
        var count = 0;
        var index = 0;
        while ((index = details.IndexOf("   at ", index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += 6;
        }

        return count;
    }
}