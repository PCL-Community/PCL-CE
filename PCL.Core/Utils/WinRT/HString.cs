using System;

namespace PCL.Core.Utils.WinRT;

/// <summary>
/// 表示一个 WinRT HSTRING 句柄的只读包装。
/// </summary>
/// <param name="Value">HSTRING 的底层指针句柄（<see cref="nint"/>）。</param>
public readonly record struct HString(IntPtr Value)
{
    public readonly IntPtr Value = Value;
    public bool IsNull => Value == IntPtr.Zero;
    public static implicit operator IntPtr(HString h) => h.Value;
    public static explicit operator HString(IntPtr ptr) => new(ptr);
}