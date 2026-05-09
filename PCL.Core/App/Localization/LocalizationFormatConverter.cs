using System;
using System.Globalization;
using System.Windows.Data;

namespace PCL.Core.App.Localization;

/// <summary>
///     <p>将 XAML 绑定值按照当前展示区域性格式化为字符串。</p>
///     <p>
///         该转换器用于界面绑定中的数字、日期、时间等 <see cref="IFormattable" /> 值展示。
///         格式化时使用 <see cref="CultureInfo.CurrentCulture" />，
///         因此会跟随 <see cref="LocalizationService" /> 当前设置的展示格式区域性。
///     </p>
///     <p>
///         该转换器只处理“值的显示格式”，例如数字分隔符、日期顺序、时间格式等。
///         它不负责翻译文本、不负责切换语言资源，也不应参与配置、日志、协议、缓存键等
///         需要稳定格式的内容生成。
///     </p>
///     <p>
///         使用时需要通过 <c>ConverterParameter</c> 提供标准 .NET 格式字符串。
///         如果未提供格式字符串，则返回原始绑定值。
///     </p>
/// </summary>
/// <example>
///     XAML 示例：
///     <code>
/// <![CDATA[
/// <TextBlock
///     Text="{Binding UpdateTime,
///            Converter={StaticResource LocalizationFormatConverter},
///            ConverterParameter=G}" />
/// 
/// <TextBlock
///     Text="{Binding DownloadSpeed,
///            Converter={StaticResource LocalizationFormatConverter},
///            ConverterParameter=N2}" />
/// ]]>
///         </code>
/// </example>
public sealed class LocalizationFormatConverter : IValueConverter
{
    /// <summary>
    ///     将绑定值按照当前展示区域性格式化。
    /// </summary>
    /// <param name="value">
    ///     需要格式化的绑定值。通常是数字、日期、时间等实现了 <see cref="IFormattable" /> 的类型。
    /// </param>
    /// <param name="targetType">
    ///     绑定目标属性的类型。本转换器不依赖该参数。
    /// </param>
    /// <param name="parameter">
    ///     标准 .NET 格式字符串，例如 <c>N2</c>、<c>G</c>、<c>d</c>。
    ///     如果该参数不是有效字符串，或为空白字符串，则直接返回原始值。
    /// </param>
    /// <param name="culture">
    ///     WPF 绑定传入的区域性。本转换器不直接使用该参数，
    ///     而是统一使用 <see cref="CultureInfo.CurrentCulture" />，
    ///     以确保格式化结果跟随应用当前展示格式区域性。
    /// </param>
    /// <returns>
    ///     如果 <paramref name="value" /> 为 <see langword="null" />，返回 <see langword="null" />；
    ///     如果未提供格式字符串，返回原始值；
    ///     否则返回使用当前展示区域性格式化后的字符串。
    /// </returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return null;
        if (parameter is not string format || string.IsNullOrWhiteSpace(format)) return value;
        return value switch
        {
            IFormattable formattable => formattable.ToString(format, CultureInfo.CurrentCulture),
            _ => string.Format(CultureInfo.CurrentCulture, "{0}", value)
        };
    }

    /// <summary>
    ///     不支持从格式化后的显示文本反向转换回原始值。
    /// </summary>
    /// <param name="value">
    ///     绑定目标传回的值。
    /// </param>
    /// <param name="targetType">
    ///     绑定源目标类型。
    /// </param>
    /// <param name="parameter">
    ///     转换参数。
    /// </param>
    /// <param name="culture">
    ///     WPF 绑定传入的区域性。
    /// </param>
    /// <returns>
    ///     该方法不会返回值。
    /// </returns>
    /// <exception cref="NotSupportedException">
    ///     始终抛出。该转换器仅用于单向显示格式化。
    /// </exception>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}