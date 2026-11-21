using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Java.Parser;
using PCL.Core.Utils;

namespace PCL.Core.Test;

[TestClass]
public class MachOHeaderReaderTest
{
    [TestMethod]
    public void TestSingleArm64()
    {
        var result = _ReadBytes([
            0xCF, 0xFA, 0xED, 0xFE, 0x0C, 0x00, 0x00, 0x01,
            0x02, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00
        ]);
        Assert.AreEqual((MachineType.ARM64, true), result);
    }

    [TestMethod]
    public void TestSingleX8664()
    {
        var result = _ReadBytes([
            0xCF, 0xFA, 0xED, 0xFE, 0x07, 0x00, 0x00, 0x01,
            0x03, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00
        ]);
        Assert.AreEqual((MachineType.AMD64, true), result);
    }

    [TestMethod]
    public void TestFatBinary()
    {
        var x8664Entry = new byte[]
        {
            0x01, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x03,
            0x00, 0x00, 0x40, 0x00, 0x00, 0x01, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x0E
        };
        var arm64Entry = new byte[]
        {
            0x01, 0x00, 0x00, 0x0C, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x0E
        };
        var expectedX8664First = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? (MachineType.ARM64, true)
            : (MachineType.AMD64, true);
        var expectedArm64First = RuntimeInformation.ProcessArchitecture == Architecture.X64
            ? (MachineType.AMD64, true)
            : (MachineType.ARM64, true);

        Assert.AreEqual(expectedX8664First, _ReadBytes([
            0xCA, 0xFE, 0xBA, 0xBE, 0x00, 0x00, 0x00, 0x02,
            .. x8664Entry, .. arm64Entry
        ]));
        Assert.AreEqual(expectedArm64First, _ReadBytes([
            0xCA, 0xFE, 0xBA, 0xBE, 0x00, 0x00, 0x00, 0x02,
            .. arm64Entry, .. x8664Entry
        ]));
    }

    [TestMethod]
    public void TestFatBinaryWithUnknownArchitecture()
    {
        var ppcEntry = new byte[]
        {
            0x00, 0x00, 0x00, 0x12, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x40, 0x00, 0x00, 0x01, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x0E
        };
        var arm64Entry = new byte[]
        {
            0x01, 0x00, 0x00, 0x0C, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x0E
        };
        var result = _ReadBytes([
            0xCA, 0xFE, 0xBA, 0xBE, 0x00, 0x00, 0x00, 0x02,
            .. ppcEntry, .. arm64Entry
        ]);
        Assert.AreEqual((MachineType.ARM64, true), result);
    }

    [TestMethod]
    public void TestClassFile()
    {
        var result = _ReadBytes([
            0xCA, 0xFE, 0xBA, 0xBE, 0x00, 0x00, 0x00, 0x41,
            0x00, 0x3D, 0x0A, 0x00, 0x02, 0x00, 0x03, 0x07
        ]);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void TestElfFile()
    {
        var result = _ReadBytes([
            0x7F, 0x45, 0x4C, 0x46, 0x02, 0x01, 0x01, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x03, 0x00, 0x3E, 0x00, 0x01, 0x00, 0x00, 0x00
        ]);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void TestEmptyOrTruncatedFile()
    {
        Assert.IsNull(_ReadBytes([]));
        Assert.IsNull(_ReadBytes([0xCF, 0xFA, 0xED]));
        Assert.IsNull(_ReadBytes([0xCA, 0xFE, 0xBA, 0xBE, 0x00, 0x00]));
        Assert.IsNull(_ReadBytes([
            0xCA, 0xFE, 0xBA, 0xBE, 0x00, 0x00, 0x00, 0x02,
            0x01, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x03
        ]));
    }

    [TestMethod]
    public void TestUnknownFormatOrMissingFile()
    {
        Assert.IsNull(_ReadBytes([0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00]));
        Assert.IsNull(_ReadBytes([
            0xCF, 0xFA, 0xED, 0xFE, 0x07, 0x00, 0x00, 0x00
        ]));
        Assert.IsNull(MachOHeaderReader.TryRead(
            Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())));
    }

    private static (MachineType Architecture, bool Is64Bit)? _ReadBytes(byte[] bytes)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, bytes);
            return MachOHeaderReader.TryRead(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}