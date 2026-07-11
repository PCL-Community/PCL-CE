// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using PCL.Application.Settings;

namespace PCL.Desktop.Localization;

public static class AvaloniaLocalizationManager
{
    public const string Auto = "auto";
    public const string FollowLanguage = "follow-language";
    private const string DefaultLanguage = "zh-CN";
    private const string EnglishResourceName = "PCL.Desktop.Localization.en-US.xaml";
    private static readonly CultureInfo SystemCulture = CultureInfo.CurrentCulture;
    private static readonly CultureInfo SystemUiCulture = CultureInfo.CurrentUICulture;
    private static ResourceDictionary? _languageResources;

    public static string CurrentLanguageCode { get; private set; } = DefaultLanguage;

    public static CultureInfo CurrentFormatCulture { get; private set; } = CultureInfo.CurrentCulture;

    public static event EventHandler? LanguageChanged;

    public static void InitializeFromSettings(LauncherSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Apply(
            settings.GetTextOption("UiLanguage", Auto),
            settings.GetTextOption("UiFormatCulture", Auto));
    }

    public static void Apply(string? languageCode, string? formatCultureCode)
    {
        string resolvedLanguage = ResolveLanguage(languageCode);
        CultureInfo uiCulture = CultureInfo.GetCultureInfo(resolvedLanguage);
        CultureInfo formatCulture = ResolveFormatCulture(formatCultureCode, uiCulture);

        CultureInfo.CurrentUICulture = uiCulture;
        CultureInfo.DefaultThreadCurrentUICulture = uiCulture;
        Thread.CurrentThread.CurrentUICulture = uiCulture;
        CultureInfo.CurrentCulture = formatCulture;
        CultureInfo.DefaultThreadCurrentCulture = formatCulture;
        Thread.CurrentThread.CurrentCulture = formatCulture;

        if (Avalonia.Application.Current is { } application)
            ApplyResources(application, resolvedLanguage);

        bool changed = !string.Equals(CurrentLanguageCode, resolvedLanguage, StringComparison.OrdinalIgnoreCase) ||
                       !string.Equals(CurrentFormatCulture.Name, formatCulture.Name, StringComparison.OrdinalIgnoreCase);
        CurrentLanguageCode = resolvedLanguage;
        CurrentFormatCulture = formatCulture;
        if (changed)
            LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string GetText(string key, string fallback)
    {
        if (Avalonia.Application.Current?.TryGetResource(key, null, out object? value) == true && value is string text)
            return text;
        return fallback;
    }

    private static string ResolveLanguage(string? languageCode)
    {
        if (!string.IsNullOrWhiteSpace(languageCode) &&
            !string.Equals(languageCode, Auto, StringComparison.OrdinalIgnoreCase))
        {
            return languageCode.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en-US" : DefaultLanguage;
        }

        return SystemUiCulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase)
            ? "en-US"
            : DefaultLanguage;
    }

    private static CultureInfo ResolveFormatCulture(string? formatCultureCode, CultureInfo uiCulture)
    {
        if (string.IsNullOrWhiteSpace(formatCultureCode) ||
            string.Equals(formatCultureCode, Auto, StringComparison.OrdinalIgnoreCase))
        {
            return SystemCulture;
        }

        if (string.Equals(formatCultureCode, FollowLanguage, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(formatCultureCode, "ui-language", StringComparison.OrdinalIgnoreCase))
        {
            return uiCulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo(formatCultureCode);
        }
        catch (CultureNotFoundException)
        {
            return SystemCulture;
        }
    }

    private static void ApplyResources(Avalonia.Application application, string languageCode)
    {
        if (_languageResources is not null)
        {
            application.Resources.MergedDictionaries.Remove(_languageResources);
            _languageResources = null;
        }

        if (!string.Equals(languageCode, "en-US", StringComparison.OrdinalIgnoreCase))
            return;

        _languageResources = LoadEnglishResources();
        application.Resources.MergedDictionaries.Add(_languageResources);
    }

    private static ResourceDictionary LoadEnglishResources()
    {
        Assembly assembly = typeof(AvaloniaLocalizationManager).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(EnglishResourceName)
            ?? throw new InvalidOperationException("English localization resource is missing.");
        XDocument document = XDocument.Load(stream, LoadOptions.PreserveWhitespace);
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        ResourceDictionary resources = new();
        foreach (XElement element in document.Root?.Elements() ?? [])
        {
            XAttribute? key = element.Attribute(xaml + "Key");
            if (key is not null)
                resources[key.Value] = element.Value;
        }
        return resources;
    }
}
