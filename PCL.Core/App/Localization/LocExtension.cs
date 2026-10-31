using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace PCL.Core.App.Localization;

/// <summary>
///     <p>XAML 本地化文本标记扩展。</p>
///     <p>
///         用于替代原写法 <c>{DynamicResource 键名}</c> 中读取本地化字符串的场景。
///         该扩展不会直接把文本写死到目标属性，而是返回一个指向
///         <see cref="LocalizationSource" /> 单例索引器的单向绑定，
///         因此在 <see cref="LocalizationService.LanguageChanged" /> 触发时，
///         所有使用该扩展的界面元素都会自动刷新，无需重新加载 XAML 或资源字典。
///     </p>
///     <p>
///         该扩展只用于语言资源。颜色、字体、样式等非文本资源仍应继续使用
///         <c>DynamicResource</c>，因为它们需要跟随资源字典与主题的变更而实时解析。
///     </p>
/// </summary>
/// <example>
///     XAML 示例：
///     <code>
/// <![CDATA[
/// <TextBlock Text="{loc:Loc Common.Action.Refresh}" />
/// ]]>
///     </code>
/// </example>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    /// <summary>
    ///     初始化一个空的本地化标记扩展。XAML 解析器会通过 <see cref="Key" /> 填充键名。
    /// </summary>
    public LocExtension()
    {
    }

    /// <summary>
    ///     使用指定的资源键初始化本地化标记扩展。
    /// </summary>
    /// <param name="key">
    ///     本地化资源键。不能为空、空字符串或空白字符串。
    /// </param>
    public LocExtension(string key)
    {
        Key = key;
    }

    /// <summary>
    ///     本地化资源键。不能为空、空字符串或空白字符串。
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    ///     构造指向当前语言文本的绑定。
    /// </summary>
    /// <param name="serviceProvider">
    ///     XAML 解析器提供的服务提供者，用于确定目标对象与目标属性。
    /// </param>
    /// <returns>
    ///     目标属性上可用的绑定表达式。在模板等延迟求值场景中，绑定会原样返回自身，
    ///     由模板在实例化时重新求值。
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     <see cref="Key" /> 为空、空字符串或空白字符串。
    /// </exception>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key))
            throw new ArgumentException("本地化资源键不能为空", nameof(Key));

        var binding = new Binding
        {
            Mode = BindingMode.OneWay,
            Source = LocalizationSource.Instance,
            Path = new PropertyPath($"[{Key}]")
        };

        return binding.ProvideValue(serviceProvider);
    }
}