using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>
///     基于 Avalonia 资源字典的本地化后端：语言资源由 WPF 语言字典按原结构转换而来，
///     以 <see cref="ResourceInclude" /> 合并到应用资源，查找与语言切换均走 Avalonia 资源体系。
///     启动器 UI 字体由启动器层的 <c>LaunchFonts</c> 统一管理，本后端不重复设置字体。
/// </summary>
public sealed class LocalizationBackend : ILocalizationBackend
{
    private const string ModuleName = "Localization";
    private const string ResourcePathFormat = "Localization/Languages/{0}.axaml";

    private static readonly string AssemblyName =
        typeof(LocalizationBackend).Assembly.GetName().Name!;

    private readonly List<IResourceProvider> _languageProviders = [];
    private readonly Lock _stateLock = new();

    private string? _appliedBaseLanguage;
    private string? _appliedCurrentLanguage;

    public bool CheckAccess()
    {
        return Dispatcher.UIThread.CheckAccess();
    }

    public void Invoke(Action action)
    {
        if (CheckAccess()) action();
        else Dispatcher.UIThread.Invoke(action);
    }

    public string? TryFindResource(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        if (Application.Current is { } app && app.TryFindResource(key, out var value))
            return value as string;
        return Lifecycle.CurrentApplication?.TryFindResource(key) as string;
    }

    public void ApplyLanguageResources(string baseLanguageCode, string? currentLanguageCode)
    {
        Invoke(() => _ApplyLanguageResources(baseLanguageCode, currentLanguageCode));
    }

    /// <summary>
    ///     应用容器就绪后先加载基础语言。加载流程中较早的 <see cref="Lang.Text" /> 调用
    ///     早于本地化服务的 Loaded 阶段，需要基础语言先行就位。
    /// </summary>
    public void EnsureBaseLanguage()
    {
        if (!CheckAccess())
        {
            Invoke(EnsureBaseLanguage);
            return;
        }

        lock (_stateLock)
        {
            if (_appliedBaseLanguage is not null) return;
        }

        ApplyLanguageResources(LocalizationService.DefaultLanguageCode, null);
    }

    private void _ApplyLanguageResources(string baseLanguageCode, string? currentLanguageCode)
    {
        if (Application.Current is not { } app) return;
        if (string.IsNullOrWhiteSpace(baseLanguageCode))
        {
            LogWrapper.Warn(ModuleName, "基础语言代码为空，已忽略本次语言资源应用");
            return;
        }

        lock (_stateLock)
        {
            var isSameBase = string.Equals(
                _appliedBaseLanguage,
                baseLanguageCode,
                StringComparison.OrdinalIgnoreCase);
            var isSameCurrent = string.Equals(
                _appliedCurrentLanguage,
                currentLanguageCode,
                StringComparison.OrdinalIgnoreCase);
            if (isSameBase && isSameCurrent) return;

            foreach (var provider in _languageProviders)
                app.Resources.MergedDictionaries.Remove(provider);
            _languageProviders.Clear();

            // 基础语言先合并、当前语言后合并；资源字典按逆序查找，保证当前语言覆盖基础语言
            var isBaseLoaded = _TryAddLanguage(app, baseLanguageCode);
            var isCurrentLoaded = !string.IsNullOrWhiteSpace(currentLanguageCode)
                && !string.Equals(
                    baseLanguageCode,
                    currentLanguageCode,
                    StringComparison.OrdinalIgnoreCase)
                && _TryAddLanguage(app, currentLanguageCode!);

            if (!isBaseLoaded && !isCurrentLoaded)
            {
                LogWrapper.Warn(
                    ModuleName,
                    $"未能加载语言资源：base={baseLanguageCode}, current={currentLanguageCode}");
                return;
            }

            _appliedBaseLanguage = baseLanguageCode;
            _appliedCurrentLanguage = currentLanguageCode;
        }

        Trace.WriteLine(
            $"[PCL.Core.UI] 已应用语言资源：base={baseLanguageCode}, current={currentLanguageCode ?? "<null>"}");
    }

    private bool _TryAddLanguage(Application app, string languageCode)
    {
        var baseUri = new Uri($"avares://{AssemblyName}/");
        var source = new Uri(baseUri, string.Format(ResourcePathFormat, languageCode));
        try
        {
            var include = new ResourceInclude(baseUri)
            {
                Source = source
            };
            app.Resources.MergedDictionaries.Add(include);
            _languageProviders.Add(include);
            return true;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, ModuleName, $"加载语言资源字典失败：{source}");
            return false;
        }
    }
}