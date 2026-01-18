using System;
using System.Collections.Generic;

namespace PCL.Core.UI.Animation.ValueProcessor;

public static class ValueProcessorManager
{
    private static readonly Dictionary<Type, Func<object, object>> _Filters = new();
    
    private static class Cache<T>
    {
        public static IValueProcessor<T>? Processor;
    }

    public static void Register<T>(IValueProcessor<T> processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        Cache<T>.Processor = processor;
        
        _Filters[typeof(T)] = o => processor.Filter((T)o)!;
    }

    public static T Filter<T>(T value)
    {
        var p = Cache<T>.Processor;
        return p is null ? value : p.Filter(value);
    }
    
    public static object Filter(object value)
    {
        var t = value.GetType();
        return _Filters.TryGetValue(t, out var func)
            ? func(value)
            : value;
    }

    public static T Add<T>(T value1, T value2)
    {
        var p = Cache<T>.Processor
                ?? throw new InvalidOperationException($"类型未注册：{typeof(T)}");
        return p.Add(value1, value2);
    }
    
    public static T Subtract<T>(T value1, T value2)
    {
        var p = Cache<T>.Processor
                ?? throw new InvalidOperationException($"类型未注册：{typeof(T)}");
        return p.Subtract(value1, value2);
    }
    
    public static T Scale<T>(T value, double factor)
    {
        var p = Cache<T>.Processor
                ?? throw new InvalidOperationException($"类型未注册：{typeof(T)}");
        return p.Scale(value, factor);
    }
}