using Avalonia.Data;
using Avalonia.Markup.Xaml;
using PCL.Core.App.Localization;

namespace PCL.Core.UI;

/// <summary>
///     Avalonia XAML 本地化标记扩展。直接绑定 <see cref="LocalizationSource" /> 的索引器，
///     语言切换后由本地化源通知刷新，无需重新加载 XAML。
/// </summary>
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
            Source = LocalizationSource.Instance,
            Path = $"[{Key}]"
        };
    }
}