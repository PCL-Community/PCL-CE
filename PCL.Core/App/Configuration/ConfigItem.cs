using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using PCL.Core.Utils.Exts;

namespace PCL.Core.App.Configuration;

/// <summary>
/// 配置项。
/// </summary>
/// <typeparam name="TValue">值类型</typeparam>
public class ConfigItem<TValue>(
    string key,
    Func<TValue> defaultValue,
    ConfigSource source
) : IConfigScope, ConfigItem
{
    public string Key { get; } = key;

    public ConfigSource Source { get; } = source;

    public Type Type => typeof(TValue);

    private Func<TValue>? _defaultValueConstructor = defaultValue;
    private TValue? _defaultValue;
    private bool _defaultValueHasSet = false;

    #region 默认值逻辑

    private TValue _GetDefaultValue()
    {
        if (_defaultValueHasSet) return _defaultValue!;
        _defaultValue = _defaultValueConstructor!();
        _defaultValueHasSet = true;
        _defaultValueConstructor = null;
        return _defaultValue;
    }

    /// <summary>
    /// 默认值。
    /// </summary>
    public TValue DefaultValue => _GetDefaultValue();

    public object DefaultValueNoType => DefaultValue ?? default!;

    #endregion

    public ConfigItem(string key, TValue defaultValue, ConfigSource source)
        : this(key, () => defaultValue, source) { }

    public IEnumerable<string> CheckScope(IReadOnlySet<string> keys) => keys.Contains(Key) ? [Key] : [];

    #region 值获取和修改

    private IConfigProvider _Provider { get => field ??= ConfigService.GetProvider(Source); } = null!;

    private ConfigValueCache<TValue> _valueCache = new();

    /// <summary>
    /// 指定是否启用缓存。<br/>
    /// <b>NOTE</b>: 禁用缓存将造成一些功能（如自动监听内容更改）不按预期工作，请仅在真正需要的时候禁用。
    /// </summary>
    public bool EnableCache
    {
        get;
        set
        {
            if (!field) _valueCache.InvalidateAll();
            field = value;
        }
    } = true;

    /// <summary>
    /// 处理看起来是新的值，并返回是否真的是新的。<br/>
    /// 只有启用缓存时该方法才会生效，未启用缓存将始终直接返回 <see langword="true"/>。
    /// </summary>
    private bool _ProcessNewCache(TValue newCache, object? argument, bool force = false)
    {
        if (!EnableCache) return true;
        if (!force)
        {
            // 判断是否是新值
            var existsOld = _valueCache.TryRead(out var oldCache, argument);
            if (existsOld && EqualityComparer<TValue>.Default.Equals(oldCache, newCache)) return false;
        }
        // 对新缓存值执行准备工作
        if (newCache is INotifyPropertyChanged reactive)
            reactive.PropertyChanged += (_, _) => OnContentChanged();
        else if (newCache is INotifyCollectionChanged reactiveCollection)
            reactiveCollection.CollectionChanged += (_, _) => OnContentChanged();
        // 写入缓存
        _valueCache.Write(newCache, argument);
        return true;
        void OnContentChanged() => SetValue(newCache, argument, bypassCache: true);
    }

    /// <summary>
    /// 获取配置值。
    /// </summary>
    /// <param name="argument">上下文参数</param>
    /// <returns>已设置的配置值或默认值</returns>
    public TValue GetValue(object? argument = null)
    {
        TValue? value = default; // 这个初始化是多余的，但是煞笔巨硬不初始化会报错
        var exists = EnableCache && _valueCache.TryRead(out value, argument);
        var newValue = false;
        if (!exists)
        {
            newValue = true;
            exists = _Provider.GetValue(Key, out value, argument);
        }
        var e = _TriggerEvent(ConfigEvent.Get, argument, value, true);
        if (e != null)
        {
            if (e.Cancelled) return DefaultValue;
            if (e.NewValueReplacement != null) return (TValue)e.NewValueReplacement;
        }
        if (!exists) value = DefaultValue;
        if (newValue) _ProcessNewCache(value!, argument);
        return value!;
    }

    public object GetValueNoType(object? argument = null)
    {
        return GetValue(argument) ?? default!;
    }

    /// <summary>
    /// 设置配置值。
    /// </summary>
    /// <param name="value">用于设置的值</param>
    /// <param name="argument">上下文参数</param>
    /// <param name="forceNewValue">强制将传入的值视为新值，不检查缓存，仅在 <see cref="EnableCache"/> 为 <see langword="true"/> 时生效</param>
    /// <param name="bypassCache">跳过缓存检查和写入，相当于对本次操作临时将 <see cref="EnableCache"/> 设为 <see langword="false"/></param>
    /// <returns>是否成功设置值，若成功则为 <c>true</c></returns>
    public bool SetValue(TValue value, object? argument = null, bool forceNewValue = false, bool bypassCache = false)
    {
        var e = _TriggerEvent(ConfigEvent.Set, argument, value, isPreview: true);
        if (e != null)
        {
            if (e.Cancelled) return false;
            if (e.NewValueReplacement != null) value = (TValue)e.NewValueReplacement;
        }
        if (bypassCache || _ProcessNewCache(value, argument, forceNewValue))
            _Provider.SetValue(Key, value, argument);
        _TriggerEvent(ConfigEvent.Set, argument, value, e: e, isPreview: false);
        return true;
    }

    public bool SetValueNoType(object value, object? argument = null)
    {
        try
        {
            return SetValue((TValue)value, argument);
        }
        catch (InvalidCastException)
        {
            // 兼容龙猫妙妙小代码直接传入 string 值的行为
            if (value is string v) return SetValue(v.Convert<TValue>()!, argument);
            var msg = $"Value convert failed (required: {Type.FullName}, provided: {value.GetType().FullName})";
            throw new InvalidCastException(msg);
        }
    }

    public bool SetDefaultValue(object? argument = null, bool? forceNewValue = null)
    {
        return SetValue(DefaultValue, argument, forceNewValue ?? IsDefault(argument));
    }

    public bool Reset(object? argument = null)
    {
        var e = _TriggerEvent(ConfigEvent.Reset, argument, null, isPreview: true);
        if (e is { Cancelled: true }) return false;
        _Provider.Delete(Key, argument);
        if (EnableCache) _valueCache.Invalidate(argument);
        _TriggerEvent(ConfigEvent.Reset, argument, null, isPreview: false);
        return true;
    }

    public bool IsDefault(object? argument = null)
    {
        var result = !_Provider.Exists(Key, argument);
        var e = _TriggerEvent(ConfigEvent.CheckDefault, argument, result);
        if (e is { NewValueReplacement: not null }) result = (bool)e.NewValueReplacement;
        return result;
    }

    #endregion

    #region 事件处理

    private readonly HashSet<ConfigObserver> _observers = [];
    private readonly HashSet<ConfigObserver> _previewObservers = [];

    public void Observe(ConfigObserver observer)
    {
        if (observer.IsPreview) _previewObservers.Add(observer);
        else _observers.Add(observer);
    }

    public bool Unobserve(ConfigObserver observer)
        => observer.IsPreview ? _previewObservers.Remove(observer) : _observers.Remove(observer);

    // 获取值，若未设置则返回 null
    private object? _GetValueOrNull(object? argument)
    {
        var exists = _Provider.GetValue<TValue>(Key, out var value, argument);
        return exists ? value : null;
    }

    public ConfigEventArgs? TriggerEvent(
        ConfigEvent trigger, object? argument,
        bool bypassOldValue = false, bool fillNewValue = false)
    {
        return _TriggerEvent(trigger, argument, null, bypassOldValue, fillNewValue);
    }

    private ConfigEventArgs? _TriggerEvent(
        ConfigEvent trigger, object? argument, object? newValue,
        bool bypassOldValue = false, bool fillNewValue = false,
        ConfigEventArgs? e = null, bool? isPreview = null)
    {
        var replaceNewValue = false;
        foreach (var observer in (
            from observer in (isPreview is { } p ? (p ? _previewObservers : _observers) : _previewObservers.Concat(_observers))
            let logic = (int)observer.Event & (int)trigger
            where logic > 0
            select observer
        )) {
            if (e == null)
            {
                if (isPreview == false && !bypassOldValue) bypassOldValue = true;
                var currentValue = (fillNewValue || !bypassOldValue) ? _GetValueOrNull(argument) : null;
                if (newValue == null && fillNewValue) newValue = currentValue ?? DefaultValue;
                e = new ConfigEventArgs(this, trigger, argument, bypassOldValue ? null : currentValue, newValue);
            }
            observer.Handler(e);
            // 对 preview 的特殊处理
            if (observer.IsPreview)
            {
                if (e.NewValueReplacement != null) replaceNewValue = true; // 记录替换操作
                if (e.Cancelled) return e;
            }
            // 防止非 preview 事件传递替换值
            else if (!replaceNewValue && e.NewValueReplacement != null) e.NewValueReplacement = null;
        }
        // 防止非 preview 事件传递取消状态
        if (e is { Cancelled: true }) e.Cancelled = false;
        return e;
    }

    #endregion
}

/// <summary>
/// <see cref="ConfigItem{TValue}"/> 的非泛型方法抽象层，用于手动解决巨硬
/// 2025 年仍未支持的极其先进的隐式去泛型化。
/// </summary>
// ReSharper disable once InconsistentNaming
public interface ConfigItem
{
    /// <summary>
    /// 配置键。
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 配置来源。
    /// </summary>
    public ConfigSource Source { get; }

    /// <summary>
    /// 配置的 CLR 类型。
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// 传入事件观察器以观察事件。
    /// </summary>
    public void Observe(ConfigObserver observer);

    /// <summary>
    /// 取消观察事件。
    /// </summary>
    public bool Unobserve(ConfigObserver observer);

    /// <summary>
    /// 触发配置项事件。
    /// </summary>
    /// <param name="trigger">触发事件</param>
    /// <param name="argument">上下文参数</param>
    /// <param name="bypassOldValue">若为 <c>true</c> 则向事件参数的旧值传递 <c>null</c>，否则传递当前值</param>
    /// <param name="fillNewValue">若为 <c>true</c>，当新值为 <c>null</c> 时将传递当前值或默认值</param>
    /// <returns></returns>
    public ConfigEventArgs? TriggerEvent(
        ConfigEvent trigger,
        object? argument,
        bool bypassOldValue = false,
        bool fillNewValue = false
    );

    /// <summary>
    /// 传入事件类型与处理委托以观察事件。
    /// </summary>
    public ConfigObserver Observe(ConfigEvent trigger, ConfigEventHandler handler, bool isPreview = false)
    {
        var observer = new ConfigObserver(trigger, handler, isPreview);
        Observe(observer);
        return observer;
    }

    /// <summary>
    /// 传统的用于兼容的值改变事件。<br/>
    /// 请尽可能避免使用，而是使用 <see cref="RegisterConfigEventAttribute"/>
    /// 来声明事件观察，或使用 <see cref="Observe(ConfigObserver)"/> 和
    /// <see cref="Unobserve(ConfigObserver)"/> 来灵活管理事件。
    /// </summary>
    public event ConfigEventHandler Changed
    {
        add => Observe(ConfigEvent.Changed, value);
        remove => throw new NotSupportedException("Please use Observe() and Unobserve() to access advanced event management");
    }

    /// <summary>
    /// 重置配置值，使其变为未设置状态。
    /// </summary>
    /// <param name="argument">上下文参数</param>
    /// <returns>是否成功重置值，若成功则为 <c>true</c></returns>
    public bool Reset(object? argument = null);

    /// <summary>
    /// 检查配置值是否为默认值 (未设置状态)
    /// </summary>
    /// <param name="argument">上下文参数</param>
    public bool IsDefault(object? argument = null);

    /// <summary>
    /// 将配置项的值设置为默认值，设置后 <see cref="IsDefault"/> 将返回 <c>false</c>。
    /// </summary>
    /// <param name="argument">上下文参数</param>
    /// <param name="forceNewValue">强制视为新值，不检查缓存，仅在 <see cref="EnableCache"/> 为 <see langword="true"/> 时生效</param>
    /// <returns>是否成功设置值，若成功则为 <c>true</c></returns>
    public bool SetDefaultValue(object? argument = null, bool? forceNewValue = null);

    /// <summary>
    /// 没有泛型的 <see cref="ConfigItem{T}.GetValue"/>。<br/>
    /// 我们都不想给非引用类型装箱，但是龙猫想。
    /// </summary>
    public object GetValueNoType(object? argument = null);

    /// <summary>
    /// 没有泛型的 <see cref="ConfigItem{T}.SetValue"/>。<br/>
    /// 我们都不想给非引用类型装箱，但是龙猫想。
    /// </summary>
    public bool SetValueNoType(object value, object? argument = null);

    /// <summary>
    /// 没有泛型的 <see cref="ConfigItem{T}.DefaultValue"/>。<br/>
    /// 我们都不想给非引用类型装箱，但是龙猫想。
    /// </summary>
    public object DefaultValueNoType { get; }

    /// <summary>
    /// 是否启用值缓存，默认为 <c>true</c>。设为 <c>false</c> 将清除已存在的缓存。
    /// </summary>
    public bool EnableCache { get; set; }
}
