using System;
using System.Collections.Generic;
using PCL.Core.Logging;

namespace PCL.Core.App.Localization;

public static class WeakLanguageChanged
{
    private static readonly object _Lock = new();
    private static readonly List<(WeakReference<object> Target, Action<object> Handler)> _Handlers = [];
    private static bool _hooked;

    public static void Add<T>(T target, Action<T> handler) where T : class
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        if (handler.Target is not null)
            throw new ArgumentException(
                "handler 必须是不捕获实例的静态委托，请改用形如 static t => t.Foo() 的写法并通过 target 传入实例。",
                nameof(handler));

        lock (_Lock)
        {
            if (!_hooked)
            {
                LocalizationService.LanguageChanged += _OnLanguageChanged;
                _hooked = true;
            }

            _Handlers.Add((new WeakReference<object>(target), o => handler((T)o)));
        }
    }

    private static void _OnLanguageChanged()
    {
        (WeakReference<object> Target, Action<object> Handler)[] snapshot;
        lock (_Lock)
        {
            _Handlers.RemoveAll(h => !h.Target.TryGetTarget(out _));
            snapshot = _Handlers.ToArray();
        }

        foreach (var (target, handler) in snapshot)
        {
            if (!target.TryGetTarget(out var t))
                continue;
            try
            {
                handler(t);
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "WeakLanguageChanged", "语言变更处理器执行出错");
            }
        }
    }
}
