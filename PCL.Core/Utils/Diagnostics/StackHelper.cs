using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace PCL.Core.Utils.Diagnostics;

public static class StackHelper
{
    private const string Unknown = "<unknown>";

    // 返回“直接调用者”的可读名称（例如 Namespace.Type.Method(paramTypes)）
    // includeNamespace: 是否包含命名空间
    // includeParameters: 是否包含参数类型列表
    // skipAppFrames: 额外跳过的应用层帧数量（例如你自己的日志包装器）
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string GetDirectCallerName(
        bool includeNamespace = true,
        bool includeParameters = false,
        int skipAppFrames = 0)
    {
        var st = new StackTrace(skipFrames: 1, fNeedFileInfo: false);
        var frame = st.GetFrame(skipAppFrames);
        var method = frame?.GetMethod();
        if (method is null) return Unknown;

        method = _TryMapAsyncOrIterator(method);
        return _FormatMethod(method, includeNamespace, includeParameters);
    }

    // 获取前 maxFrames 层调用栈，返回格式化后的每一帧字符串
    // needFileInfo=true 时会解析 PDB 以拿到文件名与行号（昂贵）
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IReadOnlyList<string> GetStack(
        int maxFrames = 10,
        bool includeNamespace = true,
        bool includeParameters = false,
        bool needFileInfo = false)
    {
        if (maxFrames <= 0) return [];

        var st = new StackTrace(skipFrames: 1, fNeedFileInfo: needFileInfo);
        var list = new List<string>(capacity: Math.Min(maxFrames, st.FrameCount));

        for (int i = 0, added = 0; i < st.FrameCount && added < maxFrames; i++)
        {
            var method = st.GetFrame(i)?.GetMethod();
            if (method is null) continue;

            method = _TryMapAsyncOrIterator(method);
            var sig = _FormatMethod(method, includeNamespace, includeParameters);

            if (needFileInfo)
            {
                var f = st.GetFrame(i);
                var file = f?.GetFileName();
                var line = f?.GetFileLineNumber() ?? 0;
                if (!string.IsNullOrEmpty(file) && line > 0)
                {
                    sig = $"{sig} ({System.IO.Path.GetFileName(file)}:{line})";
                }
            }

            list.Add(sig);
            added++;
        }

        return list;
    }

    // 将 async/iterator 的 MoveNext 映射回原始方法名（尽力而为的启发式）
    private static MethodBase _TryMapAsyncOrIterator(MethodBase method)
    {
        if (method.Name != "MoveNext") return method;

        var dt = method.DeclaringType;
        if (dt is null) return method;

        // 典型生成类型名：<MethodName>d__12 或 <MethodName>g__Local|12_0
        var name = dt.Name;
        var lt = name.IndexOf('<');
        var gt = name.IndexOf('>');
        if (lt >= 0 && gt > lt + 1)
        {
            var originalName = name.Substring(lt + 1, gt - lt - 1);
            // 在声明类型的外层类型里查找同名方法（可能存在重载，选第一个匹配）
            var parent = dt.DeclaringType ?? dt; // 迭代器常为嵌套到原类型里
            foreach (var m in parent.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (string.Equals(m.Name, originalName, StringComparison.Ordinal))
                    return m;
            }
        }

        return method;
    }

    private static string _FormatMethod(MethodBase method, bool includeNamespace, bool includeParameters)
    {
        var sb = new StringBuilder();
        var type = method.DeclaringType;
        if (type is not null)
        {
            sb.Append(_FormatTypeName(type, includeNamespace));
            sb.Append('.');
        }

        sb.Append(method.Name);

        if (method is MethodInfo { IsGenericMethod: true } mi)
        {
            sb.Append('[');
            var args = mi.GetGenericArguments();
            for (var i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(_FormatTypeName(args[i], false));
            }
            sb.Append(']');
        }

        if (includeParameters)
        {
            var ps = method.GetParameters();
            sb.Append('(');
            for (var i = 0; i < ps.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(_FormatTypeName(ps[i].ParameterType, includeNamespace));
                if (!string.IsNullOrEmpty(ps[i].Name))
                {
                    sb.Append(' ').Append(ps[i].Name);
                }
            }
            sb.Append(')');
        }

        return sb.ToString();
    }

    private static string _FormatTypeName(Type t, bool includeNamespace)
    {
        if (t.IsGenericType)
        {
            var def = t.GetGenericTypeDefinition();
            var name = def.Name;
            var tick = name.IndexOf('`');
            if (tick >= 0) name = name[..tick];

            var ns = includeNamespace ? (def.Namespace is null ? "" : def.Namespace + ".") : "";
            var sb = new StringBuilder(ns).Append(name).Append('[');
            var args = t.GetGenericArguments();
            for (var i = 0; i < args.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(_FormatTypeName(args[i], false));
            }
            sb.Append(']');
            return sb.ToString();
        }

        return includeNamespace && t.Namespace is not null
            ? t.Namespace + "." + t.Name
            : t.Name;
    }
}
