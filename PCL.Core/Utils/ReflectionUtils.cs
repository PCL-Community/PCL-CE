using System;

namespace PCL.Core.Utils;

/// <summary>
///     反射辅助工具。
/// </summary>
public static class ReflectionUtils
{
    public static bool IsInstanceOfGenericType(Type genericTypeDefinition, object? instance)
    {
        ArgumentNullException.ThrowIfNull(genericTypeDefinition);
        if (instance is null) return false;
        if (!genericTypeDefinition.IsGenericTypeDefinition) return false;

        for (var type = instance.GetType(); type is not null; type = type.BaseType)
            if (type.IsGenericType && type.GetGenericTypeDefinition() == genericTypeDefinition)
                return true;

        return false;
    }
}