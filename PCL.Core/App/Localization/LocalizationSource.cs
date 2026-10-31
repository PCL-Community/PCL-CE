using System.ComponentModel;
using System.Windows;

namespace PCL.Core.App.Localization;

/// <summary>
///     <p>供 XAML 绑定读取本地化文本的单例数据源。</p>
///     <p>
///         该类型以索引器暴露 <see cref="Lang.Text(string)" />，并通过
///         <see cref="INotifyPropertyChanged" /> 在语言变更时通知全部绑定刷新。
///         它本身不持有任何界面元素引用，绑定与通知均由 WPF 以弱事件方式管理，
///         因此不会造成内存泄漏。
///     </p>
/// </summary>
public sealed class LocalizationSource : INotifyPropertyChanged
{
    /// <summary>
    ///     全局唯一的本地化数据源实例。
    /// </summary>
    public static LocalizationSource Instance { get; } = new();

    private LocalizationSource()
    {
        LocalizationService.LanguageChanged += _OnLanguageChanged;
    }

    /// <summary>
    ///     按资源键读取当前语言的本地化文本。
    /// </summary>
    /// <param name="key">
    ///     本地化资源键。不能为空、空字符串或空白字符串。
    /// </param>
    /// <returns>
    ///     与 <see cref="Lang.Text(string)" /> 完全一致的文本。
    /// </returns>
    public string this[string key] => Lang.Text(key);

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void _OnLanguageChanged()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
            _RaisePropertyChanged();
        else
            dispatcher.BeginInvoke(_RaisePropertyChanged);
    }

    /// <summary>
    ///     使用空属性名通知所有绑定重新求值。
    /// </summary>
    private void _RaisePropertyChanged()
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(string.Empty));
    }
}