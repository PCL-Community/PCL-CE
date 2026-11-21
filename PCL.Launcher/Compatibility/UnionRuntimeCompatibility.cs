// C# 15 union types require runtime support that was introduced in .NET 11.
// Keep this shim while the launcher targets .NET 10 so the same source can still use unions.

#if !NET11_0_OR_GREATER
namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class UnionAttribute : Attribute;

public interface IUnion
{
    object? Value { get; }
}
#endif