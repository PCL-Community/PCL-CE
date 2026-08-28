using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Skin;
using SkinRecord = PCL.Core.Minecraft.Skin.Skin;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public class SkinTest
{
    /// <summary>
    /// 构造纯色位图。
    /// </summary>
    private static Bitmap CreateBitmap(int width, int height, int argb)
    {
        var color = Color.FromArgb(argb);
        var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                bmp.SetPixel(x, y, color);
        return bmp;
    }

    // ---- SkinTexture.ComputeHash ----

    [TestMethod]
    public void ComputeHash_MatchesReference()
    {
        // 1x1 纯色位图:alpha=0xFF, r=0x12, g=0x34, b=0x56
        var bmp = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        bmp.SetPixel(0, 0, Color.FromArgb(0xFF, 0x12, 0x34, 0x56));

        // 参考实现:宽(4B BE) + 高(4B BE) + a,r,g,b 各 1B
        var reference = new byte[12];
        reference[0] = 0; reference[1] = 0; reference[2] = 0; reference[3] = 1; // 宽=1
        reference[4] = 0; reference[5] = 0; reference[6] = 0; reference[7] = 1; // 高=1
        reference[8] = 0xFF; reference[9] = 0x12; reference[10] = 0x34; reference[11] = 0x56;
        using var sha = System.Security.Cryptography.SHA256.Create();
        var expected = Convert.ToHexString(sha.ComputeHash(reference)).ToLowerInvariant();

        Assert.AreEqual(expected, SkinTexture.ComputeHash(bmp));
    }

    [TestMethod]
    public void ComputeHash_TransparentPixel_RgbNormalizedToZero()
    {
        // 两个透明像素(RGB 不同)的哈希必须一致:alpha==0 时 RGB 清零
        var bmpA = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        bmpA.SetPixel(0, 0, Color.FromArgb(0, 0xFF, 0, 0)); // 透明红色
        var bmpB = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        bmpB.SetPixel(0, 0, Color.FromArgb(0, 0, 0, 0xFF)); // 透明蓝色

        Assert.AreEqual(SkinTexture.ComputeHash(bmpA), SkinTexture.ComputeHash(bmpB));
    }

    [TestMethod]
    public void Load_CachesByHash()
    {
        var bmp = CreateBitmap(64, 64, unchecked((int)0xFF123456));
        var tex1 = SkinTexture.Load(bmp);
        var tex2 = SkinTexture.Load(CreateBitmap(64, 64, unchecked((int)0xFF123456)));

        Assert.AreSame(tex1, tex2); // 相同内容命中缓存,返回同一实例
        Assert.AreEqual(tex1.Hash, tex2.Hash);
        Assert.IsNotNull(SkinTexture.Get(tex1.Hash));
        Assert.IsNull(SkinTexture.Get("ffff")); // 未命中返回 null
    }

    // ---- NormalizedSkin ----

    [TestMethod]
    public void NormalizedSkin_InvalidSize_Throws()
    {
        Assert.ThrowsExactly<InvalidSkinException>(() => new NormalizedSkin(CreateBitmap(32, 32, unchecked((int)0xFF000000))));
        Assert.ThrowsExactly<InvalidSkinException>(() => new NormalizedSkin(CreateBitmap(64, 48, unchecked((int)0xFF000000))));
    }

    [TestMethod]
    public void NormalizedSkin_NewFormat_64x64_NotOld()
    {
        var skin = new NormalizedSkin(CreateBitmap(64, 64, unchecked((int)0xFF000000)));
        Assert.IsFalse(skin.IsOldFormat);
        Assert.AreEqual(1, skin.Scale);
        Assert.AreEqual(64, skin.NormalizedTexture.Width);
        Assert.AreEqual(64, skin.NormalizedTexture.Height);
    }

    [TestMethod]
    public void NormalizedSkin_OldFormat_64x32_ConvertedTo64x64()
    {
        var skin = new NormalizedSkin(CreateBitmap(64, 32, unchecked((int)0xFF000000)));
        Assert.IsTrue(skin.IsOldFormat);
        Assert.AreEqual(1, skin.Scale);
        // 旧格式转换后目标应为 64x64
        Assert.AreEqual(64, skin.NormalizedTexture.Width);
        Assert.AreEqual(64, skin.NormalizedTexture.Height);
    }

    [TestMethod]
    public void NormalizedSkin_IsSlim_TransparentRightArm_ReturnsTrue()
    {
        // 64x64 皮肤,右臂区域 (50,16)2x4 放一个透明像素 → slim
        var bmp = CreateBitmap(64, 64, unchecked((int)0xFF888888));
        for (var y = 16; y < 20; y++)
            for (var x = 50; x < 52; x++)
                bmp.SetPixel(x, y, Color.FromArgb(0, 0, 0, 0));

        Assert.IsTrue(new NormalizedSkin(bmp).IsSlim());
    }

    [TestMethod]
    public void NormalizedSkin_IsSlim_SolidSkin_ReturnsFalse()
    {
        // 全不透明且非黑的皮肤 → 非 slim
        var bmp = CreateBitmap(64, 64, unchecked((int)0xFF888888));
        Assert.IsFalse(new NormalizedSkin(bmp).IsSlim());
    }

    [TestMethod]
    public void NormalizedSkin_IsSlim_AllBlackRightArm_ReturnsTrue()
    {
        // 右臂四区域全黑 → slim(HMCL 的 isAreaBlack 分支)
        var bmp = CreateBitmap(64, 64, unchecked((int)0xFF888888));
        for (var y = 16; y < 20; y++)
            for (var x = 50; x < 52; x++)
                bmp.SetPixel(x, y, Color.FromArgb(0xFF, 0, 0, 0));
        for (var y = 20; y < 32; y++)
            for (var x = 54; x < 56; x++)
                bmp.SetPixel(x, y, Color.FromArgb(0xFF, 0, 0, 0));
        for (var y = 48; y < 52; y++)
            for (var x = 42; x < 44; x++)
                bmp.SetPixel(x, y, Color.FromArgb(0xFF, 0, 0, 0));
        for (var y = 52; y < 64; y++)
            for (var x = 46; x < 48; x++)
                bmp.SetPixel(x, y, Color.FromArgb(0xFF, 0, 0, 0));

        Assert.IsTrue(new NormalizedSkin(bmp).IsSlim());
    }

    // ---- Skin 序列化 ----

    [TestMethod]
    public void Skin_WriteRead_RoundTrip()
    {
        var skin = new SkinRecord(SkinType.LocalFile, null, TextureModel.Slim, @"C:\skin.png", @"C:\cape.png");
        var storage = new JsonObject();
        skin.WriteStorage(storage);

        var restored = SkinRecord.FromStorage(storage);
        Assert.IsNotNull(restored);
        Assert.AreEqual(SkinType.LocalFile, restored!.Type);
        Assert.AreEqual(TextureModel.Slim, restored.Model);
        Assert.AreEqual(@"C:\skin.png", restored.LocalSkinPath);
        Assert.AreEqual(@"C:\cape.png", restored.LocalCapePath);
        Assert.AreEqual("local_file", (string?)storage["type"]);
        Assert.AreEqual("slim", (string?)storage["textureModel"]);
    }

    [TestMethod]
    public void Skin_FromStorage_UnknownType_ReturnsNull()
    {
        var storage = new JsonObject { ["type"] = "unknown_type" };
        Assert.IsNull(SkinRecord.FromStorage(storage));
    }

    [TestMethod]
    public void Skin_FromStorage_MissingSkin_ReturnsNull()
    {
        Assert.IsNull(SkinRecord.FromStorage(new JsonObject()));
    }

    [TestMethod]
    public void Skin_FromStorage_NonSlimModelDefaultsToWide()
    {
        var storage = new JsonObject
        {
            ["type"] = "local_file",
            ["textureModel"] = "wide",
            ["localSkinPath"] = @"C:\skin.png"
        };
        var skin = SkinRecord.FromStorage(storage);
        Assert.IsNotNull(skin);
        Assert.AreEqual(TextureModel.Wide, skin!.Model);
    }

    [TestMethod]
    public void Skin_FromStorage_SnakeCaseTypes()
    {
        // 验证所有枚举名的 snake_case 往返
        foreach (var type in Enum.GetValues<SkinType>())
        {
            var skin = new SkinRecord(type, null, TextureModel.Wide, null, null);
            var storage = new JsonObject();
            skin.WriteStorage(storage);
            Assert.AreEqual(type, SkinRecord.FromStorage(storage)!.Type, $"往返失败:{type}");
        }
    }
}
