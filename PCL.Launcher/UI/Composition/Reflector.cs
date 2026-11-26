using System.Reflection;
using System.Reflection.Emit;

namespace PCL.UI.Composition;

internal static class Reflector
{
    public static Func<object, T> Accessor<T>(PropertyInfo property)
    {
        var getter = property.GetMethod ?? throw new MissingMethodException(property.DeclaringType?.FullName, property.Name);
        var method = new DynamicMethod($"pcl_mat_get_{property.DeclaringType?.Name}_{property.Name}", typeof(T), [typeof(object)],
            typeof(Reflector).Module, true);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, property.DeclaringType!);
        il.Emit(getter.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, getter);
        EmitConvert(il, property.PropertyType, typeof(T));
        il.Emit(OpCodes.Ret);
        return method.CreateDelegate<Func<object, T>>();
    }

    public static Action<object, T> Call<T>(MethodInfo methodInfo)
    {
        var method = new DynamicMethod($"pcl_mat_call_{methodInfo.Name}", typeof(void), [typeof(object), typeof(T)],
            typeof(Reflector).Module, true);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, methodInfo.DeclaringType!);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Callvirt, methodInfo);
        il.Emit(OpCodes.Ret);
        return method.CreateDelegate<Action<object, T>>();
    }

    public static Action<object, Action, bool> Job(MethodInfo methodInfo)
    {
        var method = new DynamicMethod("pcl_mat_server_job", typeof(void), [typeof(object), typeof(Action), typeof(bool)],
            typeof(Reflector).Module, true);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, methodInfo.DeclaringType!);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldarg_2);
        il.Emit(OpCodes.Callvirt, methodInfo);
        il.Emit(OpCodes.Ret);
        return method.CreateDelegate<Action<object, Action, bool>>();
    }

    private static void EmitConvert(ILGenerator il, Type source, Type target)
    {
        if (source == target)
            return;
        if (target == typeof(object))
        {
            if (source.IsValueType)
                il.Emit(OpCodes.Box, source);
            return;
        }
        if (source == typeof(object) && target.IsValueType)
            il.Emit(OpCodes.Unbox_Any, target);
        else
            il.Emit(OpCodes.Castclass, target);
    }
}
