using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace PCL.Core.UI;

/// <summary>
///     Avalonia XAML 本地化标记扩展。语言切换后由 <see cref="LocalizationTextSource" /> 通知刷新，无需重新加载 XAML。
/// </summary>
// TODO: 与 WPF 端 LocExtension 语义对齐——WPF 直接绑定 LocalizationSource 索引器，这里暂用按 key 缓存的
// LocalizationTextSource 具名属性通知绕过 Avalonia 对空属性名索引器通知不刷新的限制，属于临时实现。
// 待 Core 的 LocalizationSource 增加 "Item[]"/"Item[key]" 通知后改回直接索引器绑定。
/// <example>
///     XAML 示例（<c>xmlns:loc="using:PCL.Core.UI"</c>）：
///     <code>
/// <![CDATA[
/// <TextBlock Text="{loc:Loc Common.Action.Refresh}" />
/// ]]>
///     </code>
/// </example>
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    /// <summary>本地化资源键。</summary>
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrWhiteSpace(Key))
            throw new ArgumentException("本地化资源键不能为空", nameof(Key));

        return new Binding
        {
            Mode = BindingMode.OneWay,
            Source = LocalizationTextSource.Get(Key),
            Path = nameof(LocalizationTextSource.Value)
        };
    }
}