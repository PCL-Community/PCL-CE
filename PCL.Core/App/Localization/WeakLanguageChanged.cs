using System;
using System.Collections.Generic;
using PCL.Core.Logging;

namespace PCL.Core.App.Localization;

/// <summary>
///     以弱引用方式订阅 <see cref="LocalizationService.LanguageChanged" />。
///     订阅者（<paramref name="target" />）被 GC 回收后，其处理器会在下次语言变更时自动移除，
///     因此常驻缓存页面无需显式退订，即便将来不再是单例也不会造成内存泄漏。
/// </summary>
/// <remarks>
///     为避免强引用 <paramref name="target" />，<paramref name="handler" /> 必须是<b>不捕获</b>
///     目标实例的静态委托，形如 <c>static page =&gt; page.Refresh()</c>，目标通过参数传入。
/// </remarks>
public static class WeakLanguageChanged
{
    private static readonly object _Lock = new();
    private static readonly List<(WeakReference<object> Target, Action<object> Handler)> _Handlers = [];
    private static bool _hooked;

    public static void Add<T>(T target, Action<T> handler) where T : class
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        // handler 必须是不捕获实例的静态委托（Target 为 null）。否则其闭包会强引用捕获的对象，
        // 使本类的弱订阅失效、可能造成内存泄漏，因此在此显式拒绝，避免误用。
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
            // 顺带清理已被回收的订阅者
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
                // 单个订阅者抛出异常不应中断其余订阅者的语言刷新
                LogWrapper.Warn(ex, "WeakLanguageChanged", "语言变更处理器执行出错");
            }
        }
    }
}
