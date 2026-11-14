using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Data;
using Avalonia.Threading;
using PCL.Core.App.Localization;

namespace PCL;

/// <summary>
///     用委托按需生成本地化文本的绑定源；语言变更时通知绑定重新求值。
/// </summary>
public sealed class LocalizedText : INotifyPropertyChanged
{
    private static readonly ConditionalWeakTable<
        AvaloniaObject,
        Dictionary<AvaloniaProperty, LocalizedText>> Instances = new();

    private static readonly List<LocalizedText> All = [];

    private Func<string> _factory = () => string.Empty;

    static LocalizedText()
    {
        LocalizationService.LanguageChanged += _OnLanguageChanged;
    }

    private LocalizedText()
    {
    }

    public string Value => _factory();

    public event PropertyChangedEventHandler? PropertyChanged;

    public static void Bind(
        AvaloniaObject target,
        AvaloniaProperty property,
        Func<string> factory)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(factory);

        var sources = Instances.GetOrCreateValue(target);
        if (!sources.TryGetValue(property, out var source))
        {
            source = new LocalizedText();
            sources[property] = source;
            All.Add(source);
        }

        source._factory = factory;
        target.Bind(
            property,
            new Binding(nameof(Value))
            {
                Source = source,
                Mode = BindingMode.OneWay
            });
        source._RaiseChanged();
    }

    public static bool TryBind(
        AvaloniaObject target,
        string propertyName,
        Func<string> factory)
    {
        var property = LocalizationBindingExtensions.FindAvaloniaProperty(
            target.GetType(),
            propertyName);
        if (property is null) return false;
        Bind(target, property, factory);
        return true;
    }

    private static void _OnLanguageChanged()
    {
        foreach (var source in All.ToArray())
            if (Dispatcher.UIThread.CheckAccess())
                source._RaiseChanged();
            else
                Dispatcher.UIThread.Post(source._RaiseChanged);
    }

    private void _RaiseChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
    }
}