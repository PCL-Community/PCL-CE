using System;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using PCL.Core.App.Localization;

namespace PCL;

/// <summary>
///     把依赖属性绑定到本地化文本的辅助方法。<br/>
///     与 XAML 中的 <c>{loc:Loc 键名}</c> 等价，用于代码侧动态创建的界面元素。
/// </summary>
public static class LocalizationBindingExtensions
{
    extension(DependencyObject target)
    {
        /// <summary>
        ///     将依赖属性绑定到指定本地化资源键，语言变更后自动刷新。
        /// </summary>
        /// <param name="property">目标依赖属性。</param>
        /// <param name="key">本地化资源键。</param>
        public void SetLocalizedValue(DependencyProperty property, string key)
        {
            BindingOperations.SetBinding(target, property, CreateLocalizationBinding(key));
        }

        /// <summary>
        ///     按属性名解析依赖属性并绑定到指定本地化资源键。<br/>
        ///     属性名对应 <c>{属性名}Property</c> 静态字段，会沿类型继承链查找。
        /// </summary>
        /// <param name="propertyName">依赖属性名，例如 <c>Text</c>、<c>ToolTip</c>。</param>
        /// <param name="key">本地化资源键。</param>
        /// <returns>成功绑定返回 <see langword="true" />；找不到对应依赖属性时返回 <see langword="false" />。</returns>
        public bool TrySetLocalizedValue(string propertyName, string key)
        {
            var property = FindDependencyProperty(target.GetType(), propertyName);
            if (property is null) return false;
            target.SetLocalizedValue(property, key);
            return true;
        }
    }

    /// <summary>
    ///     沿类型继承链查找名为 <c>{propertyName}Property</c> 的静态依赖属性字段。
    /// </summary>
    /// <param name="type">目标类型。</param>
    /// <param name="propertyName">依赖属性名。</param>
    /// <returns>找到的依赖属性；找不到时返回 <see langword="null" />。</returns>
    internal static DependencyProperty? FindDependencyProperty(Type type, string propertyName)
    {
        var fieldName = propertyName + "Property";
        for (var current = type; current is not null; current = current.BaseType)
        {
            var field = current.GetField(fieldName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (field?.GetValue(null) is DependencyProperty property)
                return property;
        }

        return null;
    }

    /// <summary>
    ///     创建指向指定本地化资源键的单向绑定。
    /// </summary>
    /// <param name="key">本地化资源键。</param>
    /// <returns>可用于 <see cref="BindingOperations.SetBinding" /> 的绑定。</returns>
    public static Binding CreateLocalizationBinding(string key)
    {
        return new Binding
        {
            Source = LocalizationSource.Instance,
            Path = new PropertyPath($"[{key}]"),
            Mode = BindingMode.OneWay
        };
    }
}