using System.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>
///     按资源键缓存的本地化文本源，供代码侧本地化绑定辅助方法使用。
///     <para>
///         XAML 的 <see cref="LocExtension" /> 直接绑定
///         <see cref="PCL.Core.App.Localization.LocalizationSource" />
///         索引器；本类型用于需要显式绑定到具名 <see cref="Value" /> 属性的代码场景。
///     </para>
/// </summary>
public sealed class LocalizationTextSource : INotifyPropertyChanged
{
    private static readonly Dictionary<string, LocalizationTextSource> _Cache =
        new(StringComparer.Ordinal);

    private static readonly Lock _CacheLock = new();
    private static readonly Lock _HookLock = new();
    private static bool _isHooked;

    private readonly string _key;

    private LocalizationTextSource(string key)
    {
        _key = key;
    }

    /// <summary>当前语言下该资源键对应的文本。</summary>
    public string Value => Lang.Text(_key);

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>获取指定资源键的文本源；同一键始终返回同一实例以复用绑定。</summary>
    public static LocalizationTextSource Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (_CacheLock)
        {
            if (_Cache.TryGetValue(key, out var cached)) return cached;
            var source = new LocalizationTextSource(key);
            _HookLanguageChanged();
            _Cache[key] = source;
            return source;
        }
    }

    private static void _HookLanguageChanged()
    {
        lock (_HookLock)
        {
            if (_isHooked) return;
            _isHooked = true;
            LocalizationService.LanguageChanged += _OnLanguageChanged;
        }
    }

    private static void _OnLanguageChanged()
    {
        LocalizationTextSource[] snapshot;
        lock (_CacheLock)
        {
            snapshot = [.. _Cache.Values];
        }

        var dispatcher = PlatformServices.UiDispatcher;
        var isUiThread = dispatcher is null || dispatcher.CheckAccess();
        foreach (var source in snapshot)
            if (isUiThread)
                source._RaiseChanged();
            else
                dispatcher?.Post(source._RaiseChanged);
    }

    private void _RaiseChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
    }
}