using System;
using System.Collections.Generic;

namespace PCL.Core.UI.Animation.ValueProcessor;

public static class ValueProcessorManager
{
    private static readonly Dictionary<Type, Func<object, object>> Filters = new();
    private static readonly Dictionary<Type, Func<object, object, object>> Adders = new();
    private static readonly Dictionary<Type, Func<object, double, object>> Scalers = new();
    
    private static class Cache<T>
    {
        public static IValueProcessor<T>? Processor;
    }

    public static void Register<T>(IValueProcessor<T> processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        Cache<T>.Processor = processor;
        
        Filters[typeof(T)] = o => processor.Filter((T)o)!;
        Adders[typeof(T)] = (o1, o2) => processor.Add((T)o1, (T)o2)!;
        Scalers[typeof(T)] = (o, f) => processor.Scale((T)o, f)!;
    }

    public static T Filter<T>(T value)
    {
        var p = Cache<T>.Processor;
        return p is null ? value : p.Filter(value);
    }
    
    public static object Filter(object value)
    {
        var t = value.GetType();
        return Filters.TryGetValue(t, out var func)
            ? func(value)
            : value;
    }

    public static T Add<T>(T value1, T value2)
    {
        var p = Cache<T>.Processor
                ?? throw new InvalidOperationException($"类型未注册：{typeof(T)}");
        return p.Add(value1, value2);
    }
    
    public static object Add(object value1, object value2)
    {
        var t = value1.GetType();
        if (t != value2.GetType())
            throw new InvalidOperationException($"类型不一致：{t} vs {value2.GetType()}");

        return Adders.TryGetValue(t, out var func) ? func(value1, value2) : value2;
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
    
    public static object Scale(object value, double factor)
    {
        var t = value.GetType();
        return Scalers.TryGetValue(t, out var func) ? func(value, factor) : value;
    }
    
    public static T DefaultValue<T>()
    {
        var p = Cache<T>.Processor
                ?? throw new InvalidOperationException($"类型未注册：{typeof(T)}");
        return p.DefaultValue();
    }
    
    public static bool Equal<T>(T value1, T value2)
    {
        var p = Cache<T>.Processor
                ?? throw new InvalidOperationException($"类型未注册：{typeof(T)}");
        return p.Equal(value1, value2);
    }
}