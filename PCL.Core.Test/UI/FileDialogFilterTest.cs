using System.Collections.Generic;
using Avalonia.Platform.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.UI;

namespace PCL.Core.Test.UI;

[TestClass]
public sealed class FileDialogFilterTest
{
    [TestMethod]
    public void ParsesMultipleFilterGroups()
    {
        var result = TestFileDialogService.Parse("Images|*.png;*.jpg|All|*.*");

        Assert.IsNotNull(result);
        Assert.HasCount(2, result);
        Assert.AreSequenceEqual(
            [
                "*.png",
                "*.jpg"
            ],
            [.. result[0].Patterns!]);
        Assert.AreSequenceEqual(
            [
                "*.*"
            ],
            [.. result[1].Patterns!]);
    }

    [TestMethod]
    public void EmptyFilterReturnsNull()
    {
        Assert.IsNull(TestFileDialogService.Parse(null));
        Assert.IsNull(TestFileDialogService.Parse(""));
        Assert.IsNull(TestFileDialogService.Parse("OnlyName|"));
    }

    private sealed class TestFileDialogService : FileDialogService
    {
        public static IReadOnlyList<FilePickerFileType>? Parse(string? filter)
        {
            return ParseFilter(filter);
        }
    }
}