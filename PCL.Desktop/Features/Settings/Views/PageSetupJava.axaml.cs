// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using PCL.Application.Settings;
using PCL.Desktop.Controls.Legacy;
using PCL.Domain.Minecraft.Java;
using PCL.Platform.Java;

#pragma warning disable CA1822, CS0067

namespace PCL.Desktop.Features.Settings.Views;

public partial class PageSetupJava : MyPageRight, IRefreshableSettingsPage, ISettingsPageInteractionSource
{
    private List<JavaRuntimeCandidate> _javaCandidates = [];
    private bool _loaderInitialized;

    public PageSetupJava()
    {
        AvaloniaXamlLoader.Load(this);
        EnsureLoaderInitialized();
        AttachedToVisualTree += (_, _) => EnsureLoaderInitialized();
    }

    private void EnsureLoaderInitialized()
    {
        if (_loaderInitialized)
            return;

        if (this.FindControl<MyScrollViewer>("PanBack") is { } panBack)
            PanScroll = panBack;

        MyLoading? panLoad = this.FindControl<MyLoading>("PanLoad");
        MyCard? cardLoad = this.FindControl<MyCard>("CardLoad");
        StackPanel? panMain = this.FindControl<StackPanel>("PanMain");
        if (panLoad is null || cardLoad is null || panMain is null)
            return;

        _loaderInitialized = true;
        PageLoaderInit(
            panLoad,
            cardLoad,
            panMain,
            null,
            LoadJavaListAsync,
            RenderJavaList);
    }

    public event EventHandler<SettingsPathRequestedEventArgs>? OpenPathRequested;

    public event EventHandler<SettingsUrlRequestedEventArgs>? OpenUrlRequested;

    public event EventHandler<SettingsMessageRequestedEventArgs>? MessageRequested;

    public event EventHandler<SettingsConfirmRequestedEventArgs>? ConfirmRequested;

    public void RefreshPage()
    {
        HideJavaContent();
        PageLoaderRestart();
    }

    private async Task LoadJavaListAsync(CancellationToken cancellationToken)
    {
        LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
        string[] customRoots = ReadCustomJavaRoots(settings);
        List<JavaRuntimeCandidate> candidates = [];

        IReadOnlyList<JavaRuntimeCandidate> autoCandidates =
            await Task.Run(
                    () => new FileSystemJavaLocator()
                        .FindAllAsync(cancellationToken)
                        .AsTask()
                        .GetAwaiter()
                        .GetResult(),
                    cancellationToken)
                .ConfigureAwait(true);
        candidates.AddRange(autoCandidates);

        if (customRoots.Length > 0)
        {
            IReadOnlyList<JavaRuntimeCandidate> manualCandidates =
                await Task.Run(
                        () => new FileSystemJavaLocator(customRoots)
                            .FindAllAsync(cancellationToken)
                            .AsTask()
                            .GetAwaiter()
                            .GetResult(),
                        cancellationToken)
                    .ConfigureAwait(true);

            candidates.AddRange(manualCandidates.Select(static candidate => candidate with
            {
                Source = JavaSource.ManualAdded
            }));
        }

        Dictionary<string, JavaRuntimeCandidate> merged = new(GetPathComparer());
        foreach (JavaRuntimeCandidate candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string key = candidate.Installation.JavaExecutablePath;
            if (!merged.TryGetValue(key, out JavaRuntimeCandidate? existing) ||
                candidate.Source == JavaSource.ManualAdded ||
                existing.Source != JavaSource.ManualAdded)
            {
                merged[key] = ApplySavedState(candidate, settings);
            }
        }

        _javaCandidates = merged.Values
            .OrderByDescending(static candidate => candidate.IsEnabled)
            .ThenByDescending(static candidate => candidate.Installation.MajorVersion)
            .ThenBy(static candidate => candidate.Installation.Brand.ToString(), StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(static candidate => candidate.Installation.JavaHome, GetPathComparer())
            .ToList();
    }

    private void RenderJavaList()
    {
        StackPanel? contentPanel = this.FindControl<StackPanel>("PanContent");
        if (contentPanel is null)
            return;

        LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
        string selectedJava = settings.GetTextOption(LauncherSettingKeys.LaunchSelectedJava);

        contentPanel.Children.Clear();
        MyListItem automaticItem = new()
        {
            Title = "自动选择合适的 Java",
            Info = "启动游戏时按版本需求自动匹配本机 Java。",
            Height = 45d,
            Type = MyListItem.CheckType.RadioBox,
            SvgIcon = "lucide/sparkles",
            LogoScale = 0.92d,
            Checked = string.IsNullOrWhiteSpace(selectedJava)
        };
        automaticItem.Check += (_, _) => SaveSelectedJava(string.Empty);
        contentPanel.Children.Add(automaticItem);

        if (_javaCandidates.Count == 0)
        {
            contentPanel.Children.Add(new MyHint
            {
                Text = "暂未找到可用 Java。你可以点击“添加”并选择 java 或 java.exe 文件。",
                Theme = MyHint.Themes.Yellow,
                Margin = new Avalonia.Thickness(0d, 12d, 0d, 0d)
            });
            ShowJavaContent();
            return;
        }

        foreach (JavaRuntimeCandidate candidate in _javaCandidates)
            contentPanel.Children.Add(BuildJavaItem(candidate, selectedJava));

        ShowJavaContent();
    }

    private void HideJavaContent()
    {
        if (this.FindControl<MyCard>("CardContent") is { } card)
            card.IsVisible = false;
    }

    private void ShowJavaContent()
    {
        if (this.FindControl<MyCard>("CardContent") is { } card)
            card.IsVisible = true;
    }

    private MyListItem BuildJavaItem(JavaRuntimeCandidate candidate, string selectedJava)
    {
        JavaInstallation java = candidate.Installation;
        bool isSelected = string.Equals(selectedJava, java.JavaExecutablePath, GetPathComparison());
        string state = candidate switch
        {
            { IsAvailable: false } => "不可用",
            { IsEnabled: false } => "已禁用",
            { Source: JavaSource.ManualAdded } => "手动添加",
            _ => "自动扫描"
        };

        MyIconButton openButton = CreateIconButton("lucide/folder-open", "打开 Java 文件夹");
        openButton.Click += (_, _) => OpenPathRequested?.Invoke(this, new SettingsPathRequestedEventArgs(java.JavaHome));

        MyIconButton infoButton = CreateIconButton("lucide/info", "查看 Java 详情");
        infoButton.Click += (_, _) => MessageRequested?.Invoke(
            this,
            new SettingsMessageRequestedEventArgs(
                "Java 详情",
                $"{FormatJavaTitle(java)}\n\n路径：{java.JavaHome}\n可执行文件：{java.JavaExecutablePath}\n来源：{FormatSource(candidate.Source)}\n状态：{state}"));

        MyIconButton stateButton = CreateIconButton(
            candidate.Source == JavaSource.ManualAdded ? "lucide/trash-2" :
            candidate.IsEnabled ? "lucide/ban" : "lucide/check",
            candidate.Source == JavaSource.ManualAdded ? "移除此 Java" :
            candidate.IsEnabled ? "禁用此 Java" : "启用此 Java");
        stateButton.Theme = candidate.Source == JavaSource.ManualAdded ? MyIconButton.Themes.Red : MyIconButton.Themes.Color;
        stateButton.Click += (_, _) => ToggleJavaState(candidate);

        MyListItem item = new()
        {
            Title = FormatJavaTitle(java),
            Info = java.JavaHome,
            Height = 45d,
            MinPaddingRight = 88d,
            Type = candidate.IsEnabled && candidate.IsAvailable
                ? MyListItem.CheckType.RadioBox
                : MyListItem.CheckType.Clickable,
            SvgIcon = "lucide/coffee",
            LogoScale = 0.9d,
            Checked = isSelected,
            Tags = state,
            Buttons = [openButton, infoButton, stateButton]
        };
        item.Check += (_, _) =>
        {
            if (!candidate.IsEnabled || !candidate.IsAvailable)
            {
                MessageRequested?.Invoke(
                    this,
                    new SettingsMessageRequestedEventArgs("无法选择 Java", "这个 Java 当前不可用或已被禁用，请先启用后再选择。"));
                RenderJavaList();
                return;
            }

            SaveSelectedJava(java.JavaExecutablePath);
        };
        return item;
    }

    private async void BtnAdd_Click(object sender, IconTextButtonClickEventArgs e)
    {
        IStorageProvider? storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("无法添加 Java", "当前窗口无法打开文件选择器。"));
            return;
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = OperatingSystem.IsWindows() ? "选择 java.exe 或 javaw.exe" : "选择 java 可执行文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Java 可执行文件")
                {
                    Patterns = OperatingSystem.IsWindows()
                        ? ["java.exe", "javaw.exe", "*.exe"]
                        : ["java", "java*"]
                }
            ]
        }).ConfigureAwait(true);
        if (files.Count == 0)
            return;

        string selectedPath = files[0].Path.LocalPath;
        IReadOnlyList<JavaRuntimeCandidate> candidates = await new FileSystemJavaLocator([selectedPath])
            .FindAllAsync(CancellationToken.None)
            .ConfigureAwait(true);
        JavaRuntimeCandidate? candidate = candidates.Count > 0 ? candidates[0] : null;
        if (candidate is null)
        {
            MessageRequested?.Invoke(
                this,
                new SettingsMessageRequestedEventArgs(
                    "未找到 Java",
                    "请选择 Java 安装目录中的 java 或 java.exe 文件。"));
            return;
        }

        LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
        List<string> customRoots = ReadCustomJavaRoots(settings).ToList();
        if (!customRoots.Contains(candidate.Installation.JavaHome, GetPathComparer()))
            customRoots.Add(candidate.Installation.JavaHome);

        settings.SetTextOption(LauncherSettingKeys.JavaCustomRoots, string.Join(Path.PathSeparator, customRoots));
        settings.SetBooleanOption(LauncherSettingKeys.JavaDisabled(candidate.Installation.JavaExecutablePath), false);
        LauncherSettingsPageBinder.SaveSettings(settings);

        MessageRequested?.Invoke(
            this,
            new SettingsMessageRequestedEventArgs("已添加 Java", "已添加：" + candidate.Installation.JavaHome));
        RefreshPage();
    }

    private void ToggleJavaState(JavaRuntimeCandidate candidate)
    {
        if (candidate.Source == JavaSource.ManualAdded)
        {
            ConfirmRequested?.Invoke(
                this,
                new SettingsConfirmRequestedEventArgs(
                    "移除此 Java",
                    $"确定要从 PCL N 的 Java 列表中移除这个手动添加的 Java 吗？\n\n{candidate.Installation.JavaHome}",
                    confirmed =>
                    {
                        if (!confirmed)
                            return;

                        RemoveManualJava(candidate.Installation.JavaHome);
                        RefreshPage();
                    },
                    primaryButton: "移除",
                    isWarn: true));
            return;
        }

        LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
        SettingKey key = LauncherSettingKeys.JavaDisabled(candidate.Installation.JavaExecutablePath);
        bool disabled = settings.GetBooleanOption(key);
        settings.SetBooleanOption(key, !disabled);
        if (!disabled &&
            settings.TryGetTextOption(LauncherSettingKeys.LaunchSelectedJava, out string? selected) &&
            string.Equals(selected, candidate.Installation.JavaExecutablePath, GetPathComparison()))
        {
            settings.SetTextOption(LauncherSettingKeys.LaunchSelectedJava, string.Empty);
        }

        LauncherSettingsPageBinder.SaveSettings(settings);
        RefreshPage();
    }

    private static JavaRuntimeCandidate ApplySavedState(JavaRuntimeCandidate candidate, LauncherSettings settings)
    {
        bool disabled = settings.GetBooleanOption(LauncherSettingKeys.JavaDisabled(candidate.Installation.JavaExecutablePath));
        return candidate with { IsEnabled = !disabled };
    }

    private static void SaveSelectedJava(string javaExecutablePath)
    {
        LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
        settings.SetTextOption(LauncherSettingKeys.LaunchSelectedJava, javaExecutablePath);
        LauncherSettingsPageBinder.SaveSettings(settings);
    }

    private static void RemoveManualJava(string javaHome)
    {
        LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
        List<string> customRoots = ReadCustomJavaRoots(settings)
            .Where(root => !string.Equals(root, javaHome, GetPathComparison()))
            .ToList();
        settings.SetTextOption(LauncherSettingKeys.JavaCustomRoots, string.Join(Path.PathSeparator, customRoots));
        LauncherSettingsPageBinder.SaveSettings(settings);
    }

    private static string[] ReadCustomJavaRoots(LauncherSettings settings)
    {
        if (!settings.TryGetTextOption(LauncherSettingKeys.JavaCustomRoots, out string? raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Path.GetFullPath)
            .Distinct(GetPathComparer())
            .ToArray();
    }

    private static MyIconButton CreateIconButton(string icon, string tooltip) =>
        new()
        {
            SvgIcon = icon,
            LogoScale = 0.88d,
            ToolTip = tooltip,
            Theme = MyIconButton.Themes.Color
        };

    private static string FormatJavaTitle(JavaInstallation java)
    {
        string kind = java.IsJre ? "JRE" : "JDK";
        return $"{kind} {java.MajorVersion} ({java.Version}) - {FormatBrand(java.Brand)}";
    }

    private static string FormatBrand(JavaBrand brand) => brand switch
    {
        JavaBrand.EclipseTemurin => "Eclipse Temurin",
        JavaBrand.IBMSemeru => "IBM Semeru",
        JavaBrand.GraalVmCommunity => "GraalVM Community",
        JavaBrand.OpenJDK => "OpenJDK",
        JavaBrand.TencentKona => "Tencent Kona",
        _ => brand.ToString()
    };

    private static string FormatSource(JavaSource source) => source switch
    {
        JavaSource.AutoInstalled => "自动安装",
        JavaSource.ManualAdded => "手动添加",
        _ => "自动扫描"
    };

    private static StringComparer GetPathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static StringComparison GetPathComparison() =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
