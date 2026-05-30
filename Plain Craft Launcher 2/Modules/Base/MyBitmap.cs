using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCL.Core.UI.Media;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

// 一个万能的自动图片类型转换工具类

namespace PCL;

public class MyBitmap
{
    // 使用缓存
    private readonly ConcurrentDictionary<string, MyBitmap> _Cache = new();

    /// <summary>
    ///     存储的图片
    /// </summary>
    public Bitmap pic;

    // 构造函数
    public MyBitmap()
    {
    }

    public MyBitmap(string FilePathOrResourceName)
    {
        do
        {
            try
            {
                FilePathOrResourceName =
                    FilePathOrResourceName.Replace("pack://application:,,,/images/", ModBase.pathImage);
                if (FilePathOrResourceName.StartsWithF(ModBase.pathImage))
                {
                    if (_Cache.ContainsKey(FilePathOrResourceName))
                    {
                        pic = _Cache[FilePathOrResourceName].pic;
                    }
                    else
                    {
                        pic = new MyBitmap(
                            (ImageSource)new ImageSourceConverter().ConvertFromString(FilePathOrResourceName));
                        _Cache.TryAdd(FilePathOrResourceName, pic);
                    }
                }
                else
                {
                    // 使用这种自己接管 FileStream 的方法加载才能解除文件占用
                    using (var picStream = new FileStream(FilePathOrResourceName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        if (picStream.Length > 2L && picStream.ReadByte() == 82 && picStream.ReadByte() == 73)
                        {
                            picStream.Seek(0L, SeekOrigin.Begin);
                            // 调用 WIC 转换，需要系统内置 WebP 组件，专治各种精简系统
                            using (var ms = picStream.FromWebpToPng())
                            {
                                pic = new Bitmap(ms);
                            }
                        }
                        else
                        {
                            pic = new Bitmap(picStream);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                pic = (Bitmap)System.Windows.Application.Current.TryFindResource(FilePathOrResourceName);
                if (pic is null)
                {
                    pic = new Bitmap(1, 1);
                    if (ex is ArgumentException) throw new Exception($"图片格式不支持，或图片文件损坏（{FilePathOrResourceName}）", ex);

                    throw new Exception($"加载 MyBitmap 意外失败（{FilePathOrResourceName}）", ex);
                }

                ModBase.Log(ex, $"指定类型有误的 MyBitmap 加载（{FilePathOrResourceName}）", ModBase.LogLevel.Developer);
                break;
            }
        } while (false);
    }

    public MyBitmap(ImageSource Image)
    {
        using (var mS = new MemoryStream())
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create((BitmapSource)Image));
            encoder.Save(mS);
            pic = new Bitmap(mS);
        }
    }

    public MyBitmap(Image Image)
    {
        pic = (Bitmap)Image;
    }

    public MyBitmap(Bitmap Image)
    {
        pic = Image;
    }

    public MyBitmap(ImageBrush Image)
    {
        using (var mS = new MemoryStream())
        {
            var encoder = new BmpBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create((BitmapSource)Image.ImageSource));
            encoder.Save(mS);
            pic = new Bitmap(mS);
        }
    }

    // 自动类型转换
    // 支持的类：Image，ImageSource，Bitmap，ImageBrush，BitmapSource
    public static implicit operator MyBitmap(Image Image)
    {
        if (Image is null)
            return null;
        return new MyBitmap(Image);
    }

    public static implicit operator Image(MyBitmap Image)
    {
        if (Image is null)
            return null;
        return Image.pic;
    }

    public static implicit operator MyBitmap(ImageSource Image)
    {
        if (Image is null)
            return null;
        return new MyBitmap(Image);
    }

    public static implicit operator ImageSource(MyBitmap Image)
    {
        if (Image is null)
            return null;
        var bitmapPic = Image.pic;
        var rect = new Rectangle(0, 0, bitmapPic.Width, bitmapPic.Height);
        var bitmapData = bitmapPic.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var result = BitmapSource.Create(bitmapPic.Width, bitmapPic.Height, bitmapPic.HorizontalResolution,
                bitmapPic.VerticalResolution, PixelFormats.Bgra32, null, bitmapData.Scan0, rect.Width * rect.Height * 4,
                bitmapData.Stride);
            result.Freeze();
            return result;
        }
        finally
        {
            bitmapPic.UnlockBits(bitmapData);
        }
    }

    public static implicit operator MyBitmap(Bitmap Image)
    {
        if (Image is null)
            return null;
        return new MyBitmap(Image);
    }

    public static implicit operator Bitmap(MyBitmap Image)
    {
        if (Image is null)
            return null;
        return Image.pic;
    }

    public static implicit operator MyBitmap(ImageBrush Image)
    {
        if (Image is null)
            return null;
        return new MyBitmap(Image);
    }

    public static implicit operator ImageBrush(MyBitmap Image)
    {
        if (Image is null)
            return null;
        return new ImageBrush(new MyBitmap(Image.pic));
    }

    /// <summary>
    ///     获取裁切的图片，这个方法不会导致原对象改变且会返回一个新的对象。
    /// </summary>
    public MyBitmap Clip(int X, int Y, int Width, int Height)
    {
        var bmp = new Bitmap(Width, Height, pic.PixelFormat);
        bmp.SetResolution(pic.HorizontalResolution, pic.VerticalResolution);
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.TranslateTransform(-X, -Y);
            g.DrawImage(pic, new Rectangle(0, 0, pic.Width, pic.Height));
        }

        return bmp;
    }

    /// <summary>
    ///     获取旋转或翻转后的图片，这个方法不会导致原对象改变且会返回一个新的对象。
    /// </summary>
    public MyBitmap RotateFlip(RotateFlipType Type)
    {
        var bmp = new Bitmap(pic);
        bmp.SetResolution(pic.HorizontalResolution, pic.VerticalResolution);
        bmp.RotateFlip(Type);
        return bmp;
    }

    /// <summary>
    ///     将图像保存到文件。
    /// </summary>
    public void Save(string FilePath)
    {
        BitmapEncoder encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create((BitmapSource)this));
        using (var fileStream = new FileStream(FilePath, FileMode.Create))
        {
            encoder.Save(fileStream);
        }
    }
}