using System;
using System.Windows;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.Platform;

namespace PCL.Core.Wpf;

/// <summary>基于 WPF 资源字典与字体的本地化后端。</summary>
public sealed class WpfLocalizationBackend : ILocalizationBackend
{
    private const string AssemblyResourcePrefix =
        "/PCL.Core.Wpf;component/App/Localization/Languages/";

    private ResourceDictionary? _baseDictionary;
    private ResourceDictionary? _currentDictionary;

    public bool CheckAccess()
    {
        return Application.Current?.Dispatcher.CheckAccess() ?? false;
    }

    public void Invoke(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null) action();
        else dispatcher.Invoke(action);
    }

    public string? TryFindResource(string key)
    {
        return Application.Current?.TryFindResource(key) as string
            ?? Lifecycle.CurrentApplication?.TryFindResource(key) as string;
    }

    public void ApplyLanguageResources(string baseLanguageCode, string? currentLanguageCode)
    {
        var dictionaries = Application.Current?.Resources.MergedDictionaries;
        if (dictionaries is null) return;

        if (_baseDictionary is not null) dictionaries.Remove(_baseDictionary);
        if (_currentDictionary is not null) dictionaries.Remove(_currentDictionary);

        _baseDictionary = _LoadDictionary(baseLanguageCode);
        dictionaries.Add(_baseDictionary);

        _currentDictionary =
            currentLanguageCode is null ? null : _LoadDictionary(currentLanguageCode);
        if (_currentDictionary is not null) dictionaries.Add(_currentDictionary);
    }

    public void ApplyLaunchFont(string? customFontName, LocalizationLanguage? language)
    {
        LocalizationFontService.ApplyLaunchFont(customFontName, language);
    }

    private static ResourceDictionary _LoadDictionary(string languageCode)
    {
        return new ResourceDictionary
        {
            Source = new Uri($"{AssemblyResourcePrefix}{languageCode}.xaml", UriKind.Relative)
        };
    }
}