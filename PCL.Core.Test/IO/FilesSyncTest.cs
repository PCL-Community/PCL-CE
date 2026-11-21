using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO;

namespace PCL.Core.Test.IO;

[TestClass]
public sealed class FilesSyncTest
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "pcl-files-sync-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    [TestMethod]
    public void ReadAndWriteTextCreatesParentDirectory()
    {
        var path = Path.Combine(_root, "nested", "sample.txt");

        Files.WriteAllText(path, "hello", new UTF8Encoding(false));

        Assert.AreEqual("hello", Files.ReadAllTextOrEmpty(path));
        CollectionAssert.AreEqual("hello"u8.ToArray(), Files.ReadAllBytesOrEmpty(path));
    }

    [TestMethod]
    public void ReadTextSkipsUtf8Bom()
    {
        var path = Path.Combine(_root, "bom.json");
        Files.WriteAllText(path, "{\"Key\":1}", new UTF8Encoding(true));

        var text = Files.ReadAllTextOrEmpty(path);

        Assert.AreEqual('{', text[0]);
        using var document = JsonDocument.Parse(text);
        Assert.AreEqual(1, document.RootElement.GetProperty("Key").GetInt32());
    }

    [TestMethod]
    public void ReadTextSkipsUtf8BomWhenEncodingHasNoBom()
    {
        var path = Path.Combine(_root, "bom-explicit.json");
        Files.WriteAllText(path, "{\"Key\":2}", new UTF8Encoding(true));

        var text = Files.ReadAllTextOrEmpty(path, new UTF8Encoding(false));

        Assert.AreEqual('{', text[0]);
        using var document = JsonDocument.Parse(text);
        Assert.AreEqual(2, document.RootElement.GetProperty("Key").GetInt32());
    }

    [TestMethod]
    public void ReadTextSkipsUtf16LeBom()
    {
        var path = Path.Combine(_root, "utf16.txt");
        byte[] bytes = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("hello")];
        Files.WriteAllBytes(path, bytes);

        Assert.AreEqual("hello", Files.ReadAllTextOrEmpty(path, new UnicodeEncoding(false, false)));
    }

    [TestMethod]
    public void ReadMissingFileReturnsEmptyValues()
    {
        var path = Path.Combine(_root, "missing.txt");

        Assert.AreEqual("", Files.ReadAllTextOrEmpty(path));
        Assert.IsEmpty(Files.ReadAllBytesOrEmpty(path));
    }

    [TestMethod]
    public void FileHashUsesLowercaseHex()
    {
        var path = Path.Combine(_root, "hash.txt");
        Files.WriteAllBytes(path, "abc"u8.ToArray());

        Assert.AreEqual(
            "900150983cd24fb0d6963f7d28e17f72",
            Files.GetFileMd5(path));
        Assert.AreEqual(
            "a9993e364706816aba3e25717850c26c9cd0d89d",
            Files.GetFileSha1(path));
    }


    [TestMethod]
    public void IniRoundTripAndRemoveKey()
    {
        var path = Path.Combine(_root, "config.ini");
        Ini.Write(path, "Version", "1.0");
        Ini.Write(path, "Name", "PCL");

        Assert.AreEqual("1.0", Ini.Read(path, "Version"));
        Assert.AreEqual("PCL", Ini.Read(path, "Name"));

        Ini.Write(path, "Version", null);
        Assert.AreEqual("default", Ini.Read(path, "Version", "default"));
    }

    [TestMethod]
    public void CopyFileCreatesTargetDirectory()
    {
        var source = Path.Combine(_root, "source.bin");
        var target = Path.Combine(_root, "nested", "target.bin");
        Files.WriteAllBytes(source, [1, 2, 3]);

        Files.CopyFile(source, target);

        Assert.AreSequenceEqual(
            new byte[]
            {
                1,
                2,
                3
            },
            File.ReadAllBytes(target));
    }
}