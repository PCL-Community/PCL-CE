using System;
using PCL.Core.App.Localization;

namespace PCL.Core.Utils;

/// <summary>
///     表示读取到的 JSON 内容无效。该类型用于让调用方在不依赖消息文本的前提下
///     识别 JSON 损坏，避免用当前界面语言的译文做字符串比较。
/// </summary>
public class InvalidJsonException : Exception
{
    /// <summary>
    ///     使用当前语言的错误消息初始化异常。
    /// </summary>
    public InvalidJsonException() : base(Lang.Text("Common.Error.InvalidJson"))
    {
    }

    /// <summary>
    ///     使用当前语言的错误消息与内部异常初始化异常。
    /// </summary>
    /// <param name="innerException">导致 JSON 解析失败的原始异常。</param>
    public InvalidJsonException(Exception? innerException)
        : base(Lang.Text("Common.Error.InvalidJson"), innerException)
    {
    }
}