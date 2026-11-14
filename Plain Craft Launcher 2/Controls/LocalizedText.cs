using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using PCL.Core.App.Localization;

namespace PCL;

/// <summary>
///     用委托按需生成本地化文本的绑定源。<br/>
///     适用于“带参数的格式化文本”这类无法用单一资源键绑定的场景：
///     它把生成逻辑交给调用方，并在语言变更时通知绑定重新求值。
/// </summary>
public sealed class LocalizedText : INotifyPropertyChanged
{
    private static readonly ConditionalWeakTable<DependencyObject,
            Dictionary<DependencyProperty, LocalizedText>>
        Instances = new();

    private Func<string> _factory = () => string.Empty;

    private LocalizedText()
    {
        PropertyChangedEventManager.AddHandler(
            LocalizationSource.Instance,
            _OnSourceChanged,
            nameof(Value));
    }

    /// <summary>
    ///     当前语言下的文本。
    /// </summary>
    public string Value => _factory();

    /// <summary>
    ///     创建一个独立的本地化文本绑定源，用于 ViewModel 等无法通过 <see cref="Bind" /> 绑定到控件的场景。<br />
    ///     在 XAML 中绑定到 <c>{Binding Xxx.Value}</c> 即可随语言切换自动刷新。
    /// </summary>
    /// <param name="factory">文本生成委托，在每次求值时调用。</param>
    public static LocalizedText Create(Func<string> factory)
    {
        return new LocalizedText { _factory = factory };
    }

    /// <summary>
    ///     替换文本生成逻辑并立即通知绑定刷新。
    /// </summary>
    /// <param name="factory">新的文本生成委托。</param>
    public void SetFactory(Func<string> factory)
    {
        _factory = factory;
        _RaiseChanged();
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    ///     为指定对象绑定一段按需生成的本地化文本。<br/>
    ///     同一对象上的同一属性再次调用会替换原有绑定与生成逻辑，与直接赋值语义一致。
    /// </summary>
    /// <param name="target">目标对象。</param>
    /// <param name="property">目标依赖属性。</param>
    /// <param name="factory">文本生成委托，在每次求值时调用。</param>
    public static void Bind(
        DependencyObject target,
        DependencyProperty property,
        Func<string> factory)
    {
        var sources = Instances.GetOrCreateValue(target);
        if (!sources.TryGetValue(property, out var source))
        {
            source = new LocalizedText();
            sources[property] = source;
        }
        source._factory = factory;
        BindingOperations.SetBinding(
            target,
            property,
            new Binding(nameof(Value))
            {
                Source = source,
                Mode = BindingMode.OneWay
            });
        source._RaiseChanged();
    }

    /// <inheritdoc cref="Bind" />
    /// <param name="target">目标对象。</param>
    /// <param name="propertyName">依赖属性名，例如 <c>Text</c>、<c>Title</c>。</param>
    /// <param name="factory">文本生成委托。</param>
    /// <returns>成功绑定返回 <see langword="true" />；找不到对应依赖属性时返回 <see langword="false" />。</returns>
    public static bool TryBind(
        DependencyObject target,
        string propertyName,
        Func<string> factory)
    {
        var property = LocalizationBindingExtensions.FindDependencyProperty(
            target.GetType(),
            propertyName);
        if (property is null) return false;
        Bind(target, property, factory);
        return true;
    }

    private void _OnSourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        _RaiseChanged();
    }

    private void _RaiseChanged()
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nameof(Value)));
    }
}