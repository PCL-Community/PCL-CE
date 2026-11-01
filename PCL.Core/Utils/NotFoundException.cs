using System;
using PCL.Core.App.Localization;

namespace PCL.Core.Utils;

/// <summary>
///     表示请求的资源不存在（例如下载源返回 404）。
///     该类型用于让调用方在不依赖消息文本的前提下识别「未找到」，避免用当前界面语言的译文做字符串比较。
/// </summary>
public class NotFoundException : Exception
{
    /// <summary>
    ///     使用当前语言的错误消息初始化异常。
    /// </summary>
    public NotFoundException() : base(Lang.Text("Minecraft.Download.Error.NotFound"))
    {
    }

    /// <summary>
    ///     使用当前语言的错误消息与内部异常初始化异常。
    /// </summary>
    /// <param name="innerException">导致资源未找到的原始异常。</param>
    public NotFoundException(Exception? innerException)
        : base(Lang.Text("Minecraft.Download.Error.NotFound"), innerException)
    {
    }
}