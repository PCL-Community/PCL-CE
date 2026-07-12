// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PCL.Application.Instances;
using PCL.Application.Launching;
using PCL.Application.Settings;
using PCL.Desktop.Controls.Legacy;
using PCL.Desktop.Features.Launching.Views;
using PCL.Desktop.Features.Settings.Views;
using PCL.Domain.Minecraft.Java;
using PCL.Platform.Abstractions.System;
using PCL.Platform.Java;
using PCL.Platform.System;

namespace PCL.Desktop.Features.Instances.Views;

public partial class PageInstanceSetupRight : MyPageRight
{
    private readonly ISystemInfoProvider _systemInfoProvider;
    private readonly DispatcherTimer _ramRefreshTimer;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private LaunchInstanceInfo? _instance;
    private InstanceMetadata _metadata = new();
    private bool _isLoading;
    private bool _controlsWired;
    private int _globalMemorySolution;
    private int _globalCustomMemorySize = 15;
    private int _ramTextLeft = 2;
    private int _javaLoadVersion;

    public PageInstanceSetupRight()
        : this(new DefaultSystemInfoProvider())
    {
    }

    public PageInstanceSetupRight(ISystemInfoProvider systemInfoProvider)
    {
        _systemInfoProvider = systemInfoProvider ?? throw new ArgumentNullException(nameof(systemInfoProvider));
        AvaloniaXamlLoader.Load(this);
        PanScroll = this.FindControl<MyScrollViewer>("PanBack");
        _ramRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _ramRefreshTimer.Tick += RamRefreshTimer_Tick;
        AttachedToVisualTree += (_, _) =>
        {
            ReloadGlobalMemorySettings();
            RefreshRam(showAnim: false);
            _ = RefreshJavaComboBoxAsync();
            _ramRefreshTimer.Start();
            Dispatcher.UIThread.Post(() =>
            {
                if (_instance is not null)
                    ApplyMetadata();
                WireControls();
            }, DispatcherPriority.Background);
        };
        DetachedFromVisualTree += (_, _) => _ramRefreshTimer.Stop();
        if (this.FindControl<Grid>("PanRamDisplay") is { } ramDisplay)
            ramDisplay.SizeChanged += (_, _) => RefreshRamText();
        if (this.FindControl<Avalonia.Controls.Shapes.Rectangle>("RectRamUsed") is { } ramUsed)
            ramUsed.SizeChanged += (_, _) => RefreshRamText();
    }

    public event EventHandler? OpenGlobalSettingsRequested;

    public event EventHandler<SettingsMessageRequestedEventArgs>? MessageRequested;

    public event EventHandler<SettingsConfirmRequestedEventArgs>? ConfirmRequested;

    public event EventHandler<string>? CreateAuthProfileRequested;

    public async Task WaitForPendingMetadataWritesAsync()
    {
        await _saveGate.WaitAsync().ConfigureAwait(false);
        _saveGate.Release();
    }

    public void SetInstance(LaunchInstanceInfo instance)
    {
        _instance = instance;
        _metadata = InstanceMetadataStore.LoadAsync(instance.InstanceDirectory).GetAwaiter().GetResult();
        ReloadGlobalMemorySettings();
        ApplyMetadata();
        RefreshRam(showAnim: false);
        _ = RefreshJavaComboBoxAsync();
    }

    public override void Dispose()
    {
        _ramRefreshTimer.Stop();
        _ramRefreshTimer.Tick -= RamRefreshTimer_Tick;
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    private void WireControls()
    {
        if (_controlsWired)
            return;
        _controlsWired = true;

        foreach (string name in new[]
                 {
                     "TextArgumentInfo", "TextServerAuthServer", "TextServerAuthRegister", "TextServerAuthName",
                     "TextAdvanceJvm", "TextAdvanceGame", "TextAdvanceClasspathHead", "TextAdvanceRun"
                 })
        {
            if (this.FindControl<MyTextBox>(name) is { } textBox)
                textBox.TextChanged += TextBox_TextChanged;
        }
        if (this.FindControl<MyTextBox>("TextServerEnter") is { } serverToEnter)
        {
            serverToEnter.GetObservable(TextBox.TextProperty)
                .Subscribe(text => ServerToEnterTextChanged(serverToEnter, text));
        }

        foreach (string name in new[]
                 {
                     "ComboArgumentIndieV2", "TextArgumentTitle", "ComboArgumentJava",
                     "ComboServerLoginRequire", "ComboAdvanceRenderer"
                 })
        {
            if (this.FindControl<MyComboBox>(name) is { } comboBox)
                comboBox.SelectionChanged += ComboBox_SelectionChanged;
        }

        if (this.FindControl<MyComboBox>("TextArgumentTitle") is { } title)
            title.TextChanged += EditableTitle_TextChanged;
        if (this.FindControl<MyComboBox>("ComboArgumentJava") is { } javaCombo)
            javaCombo.DropDownOpened += ComboArgumentJava_DropDownOpened;

        foreach (string name in new[]
                 {
                     "CheckArgumentTitleEmpty", "CheckAdvanceRunWait", "CheckAdvanceJava", "CheckAdvanceAssetsV2",
                     "CheckAdvanceUseProxyV2", "CheckAdvanceDisableJLW", "CheckAdvanceDisableRW",
                     "CheckUseDebugLog4j2Config", "CheckAdvanceDisableLwjglUnsafeAgent"
                 })
        {
            if (this.FindControl<MyCheckBox>(name) is { } checkBox)
                checkBox.Change += CheckBox_Change;
        }

        foreach (string name in new[] { "RadioRamType0", "RadioRamType1", "RadioRamType2" })
        {
            if (this.FindControl<MyRadioBox>(name) is { } radioBox)
                radioBox.Check += RadioBox_Check;
        }

        if (this.FindControl<MySlider>("SliderRamCustom") is { } slider)
            slider.Change += Slider_Change;

        if (this.FindControl<MyExtraTextButton>("BtnSwitch") is { } switchButton)
            switchButton.Click += (_, _) => OpenGlobalSettingsRequested?.Invoke(this, EventArgs.Empty);
        if (this.FindControl<MyTextBox>("TextServerAuthServer") is { } authServer)
            authServer.LostFocus += TextServerAuthServer_LostFocus;
        if (this.FindControl<MyButton>("BtnServerAuthLittle") is { } littleSkin)
            littleSkin.Click += (_, _) => ApplyLittleSkinPreset();
        if (this.FindControl<MyButton>("BtnServerAuthLock") is { } lockButton)
            lockButton.Click += (_, _) => LockAuthSettings();
        if (this.FindControl<MyButton>("BtnServerNewProfile") is { } newProfile)
            newProfile.Click += (_, _) => CreateAuthProfileRequested?.Invoke(this, _metadata.AuthServerAddress);
    }

    private void ApplyMetadata()
    {
        _isLoading = true;
        try
        {
            SetComboIndex("ComboArgumentIndieV2", _metadata.InstanceIsolation ? 0 : 1);
            SetEditableComboText("TextArgumentTitle", _metadata.WindowTitle);
            SetChecked("CheckArgumentTitleEmpty", _metadata.UseGlobalWindowTitle);
            SetText("TextArgumentInfo", _metadata.CustomInfo);
            SetRadio("RadioRamType" + _metadata.MemorySolution);
            SetSliderValue("SliderRamCustom", _metadata.CustomMemorySize);
            SetComboIndex("ComboServerLoginRequire", _metadata.ServerLoginRequirement);
            SetText("TextServerAuthServer", _metadata.AuthServerAddress);
            SetText("TextServerAuthRegister", _metadata.AuthRegisterAddress);
            SetText("TextServerAuthName", _metadata.AuthServerDisplayName);
            SetText("TextServerEnter", _metadata.ServerToEnter);
            SetComboIndex("ComboAdvanceRenderer", _metadata.Renderer);
            SetText("TextAdvanceJvm", _metadata.JvmArguments);
            SetText("TextAdvanceGame", _metadata.GameArguments);
            SetText("TextAdvanceClasspathHead", _metadata.ClasspathHead);
            SetText("TextAdvanceRun", _metadata.PreLaunchCommand);
            SetChecked("CheckAdvanceRunWait", _metadata.WaitForPreLaunchCommand);
            SetChecked("CheckAdvanceJava", _metadata.IgnoreJavaCompatibility);
            SetChecked("CheckAdvanceAssetsV2", _metadata.DisableAssetVerification);
            SetChecked("CheckAdvanceUseProxyV2", _metadata.UseProxy);
            SetChecked("CheckAdvanceDisableJLW", _metadata.DisableJlw);
            SetChecked("CheckAdvanceDisableRW", _metadata.DisableRw);
            SetChecked("CheckUseDebugLog4j2Config", _metadata.UseDebugLog4j2Config);
            SetChecked("CheckAdvanceDisableLwjglUnsafeAgent", _metadata.DisableLwjglUnsafeAgent);
            ApplyWindowTitleMode();
            ApplyRamMode();
            ApplyServerLoginMode();
            ApplyPreLaunchCommandVisibility();
            RefreshRam(showAnim: false);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void TextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading || sender is not MyTextBox textBox)
            return;

        if (textBox.Tag?.ToString() == "VersionServerEnter" && textBox.Text?.Contains('：') == true)
        {
            textBox.Text = textBox.Text.Replace('：', ':');
            return;
        }

        UpdateMetadata(metadata => (textBox.Tag?.ToString()) switch
        {
            "VersionArgumentInfo" => metadata with { CustomInfo = textBox.Text ?? string.Empty },
            "VersionServerAuthServer" => metadata with { AuthServerAddress = textBox.Text ?? string.Empty },
            "VersionServerAuthRegister" => metadata with { AuthRegisterAddress = textBox.Text ?? string.Empty },
            "VersionServerAuthName" => metadata with { AuthServerDisplayName = textBox.Text ?? string.Empty },
            "VersionServerEnter" => metadata with { ServerToEnter = textBox.Text ?? string.Empty },
            "VersionAdvanceJvm" => metadata with { JvmArguments = textBox.Text ?? string.Empty },
            "VersionAdvanceGame" => metadata with { GameArguments = textBox.Text ?? string.Empty },
            "VersionAdvanceClasspathHead" => metadata with { ClasspathHead = textBox.Text ?? string.Empty },
            "VersionAdvanceRun" => metadata with { PreLaunchCommand = textBox.Text ?? string.Empty },
            _ => metadata
        });
        ApplyPreLaunchCommandVisibility();
    }

    private void ServerToEnterTextChanged(MyTextBox textBox, string? text)
    {
        if (_isLoading)
            return;

        string normalized = (text ?? string.Empty).Replace('：', ':');
        if (!string.Equals(text, normalized, StringComparison.Ordinal))
        {
            textBox.SetCurrentValue(TextBox.TextProperty, normalized);
            return;
        }

        UpdateMetadata(metadata => metadata with { ServerToEnter = normalized });
    }

    private void ComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || sender is not MyComboBox comboBox)
            return;

        string? tag = comboBox.Tag?.ToString();
        if (ReferenceEquals(comboBox, this.FindControl<MyComboBox>("TextArgumentTitle")))
        {
            UpdateMetadata(metadata => metadata with { WindowTitle = comboBox.Text ?? string.Empty });
            ApplyWindowTitleMode();
            return;
        }

        if (ReferenceEquals(comboBox, this.FindControl<MyComboBox>("ComboArgumentJava")))
        {
            JavaSelectionOption? option = comboBox.SelectedItem switch
            {
                JavaSelectionOption direct => direct,
                MyComboBoxItem { Tag: JavaSelectionOption tagged } => tagged,
                _ => null
            };
            if (option is not null)
            {
                UpdateMetadata(metadata => metadata with
                {
                    JavaSelectionMode = option.Mode,
                    SelectedJavaPath = option.JavaExecutablePath
                });
                RefreshRam(showAnim: true);
            }
            return;
        }

        UpdateMetadata(metadata => tag switch
        {
            "VersionArgumentIndieV2" => metadata with { InstanceIsolation = comboBox.SelectedIndex == 0 },
            "VersionServerLoginRequire" => metadata with { ServerLoginRequirement = Math.Max(0, comboBox.SelectedIndex) },
            "VersionAdvanceRenderer" => metadata with { Renderer = Math.Max(0, comboBox.SelectedIndex) },
            _ => metadata
        });
        ApplyServerLoginMode();
    }

    private void CheckBox_Change(object sender, bool user)
    {
        if (_isLoading || sender is not MyCheckBox checkBox)
            return;

        bool value = checkBox.Checked == true;
        UpdateMetadata(metadata => (checkBox.Tag?.ToString()) switch
        {
            "VersionArgumentTitleEmpty" => metadata with { UseGlobalWindowTitle = value },
            "VersionAdvanceRunWait" => metadata with { WaitForPreLaunchCommand = value },
            "VersionAdvanceJava" => metadata with { IgnoreJavaCompatibility = value },
            "VersionAdvanceAssetsV2" => metadata with { DisableAssetVerification = value },
            "VersionAdvanceUseProxyV2" => metadata with { UseProxy = value },
            "VersionAdvanceDisableJLW" => metadata with { DisableJlw = value },
            "VersionAdvanceDisableRW" => metadata with { DisableRw = value },
            "VersionUseDebugLog4j2Config" => metadata with { UseDebugLog4j2Config = value },
            "VersionAdvanceDisableLwjglUnsafeAgent" => metadata with { DisableLwjglUnsafeAgent = value },
            _ => metadata
        });
        if (checkBox.Tag?.ToString() == "VersionArgumentTitleEmpty")
            ApplyWindowTitleMode();
    }

    private void EditableTitle_TextChanged(object sender, TextChangedEventArgs? e)
    {
        if (_isLoading || sender is not MyComboBox comboBox)
            return;

        UpdateMetadata(metadata => metadata with { WindowTitle = comboBox.Text ?? string.Empty });
        ApplyWindowTitleMode();
    }

    private void RadioBox_Check(object sender, RouteEventArgs e)
    {
        if (_isLoading || sender is not MyRadioBox radioBox)
            return;

        if (radioBox.Name is not { } name || !name.StartsWith("RadioRamType", StringComparison.Ordinal))
            return;

        if (int.TryParse(name["RadioRamType".Length..], out int value))
            UpdateMetadata(metadata => metadata with { MemorySolution = value });
        ApplyRamMode();
        RefreshRam(showAnim: true);
    }

    private void Slider_Change(object sender, bool user)
    {
        if (_isLoading || sender is not MySlider slider)
            return;

        UpdateMetadata(metadata => metadata with { CustomMemorySize = slider.Value });
        RefreshRam(showAnim: true);
    }

    private void UpdateMetadata(Func<InstanceMetadata, InstanceMetadata> update)
    {
        if (_instance is null)
            return;

        _metadata = update(_metadata);
        _ = SaveLatestMetadataAsync(_instance.InstanceDirectory);
    }

    private async Task SaveLatestMetadataAsync(string instanceDirectory)
    {
        await _saveGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await InstanceMetadataStore.SaveAsync(instanceDirectory, _metadata).ConfigureAwait(false);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private void ApplyRamMode()
    {
        if (this.FindControl<MySlider>("SliderRamCustom") is { } slider)
            slider.IsEnabled = _metadata.MemorySolution == 1;
    }

    private void RamRefreshTimer_Tick(object? sender, EventArgs e) => RefreshRam(showAnim: true);

    private void RefreshRam(bool showAnim)
    {
        if (_instance is null ||
            this.FindControl<MySlider>("SliderRamCustom") is not { } sliderRamCustom ||
            this.FindControl<TextBlock>("LabRamGame") is not { } labRamGame ||
            this.FindControl<TextBlock>("LabRamUsed") is not { } labRamUsed ||
            this.FindControl<TextBlock>("LabRamTotal") is not { } labRamTotal ||
            this.FindControl<Grid>("PanRamDisplay") is not { } panRamDisplay)
        {
            return;
        }

        MemoryInfo memory = _systemInfoProvider.GetMemoryInfo();
        double ramTotal = Math.Round(Math.Max(memory.TotalBytes, 4L * 1024 * 1024 * 1024) / 1024d / 1024d / 1024d, 1);
        double ramAvailable = memory.AvailableBytes > 0
            ? Math.Round(memory.AvailableBytes / 1024d / 1024d / 1024d, 1)
            : Math.Round(ramTotal * 0.65d, 1);
        ramAvailable = Math.Clamp(ramAvailable, 0.1d, ramTotal);

        int memorySolution = _metadata.MemorySolution;
        int customMemorySize = _metadata.CustomMemorySize;
        if (memorySolution == 2)
        {
            memorySolution = _globalMemorySolution;
            customMemorySize = _globalCustomMemorySize;
        }

        (LaunchMemoryProfile profile, int modCount) = GetMemoryProfile();
        double ramGame = LaunchMemoryCalculator.ResolveMemoryMegabytes(
            new LaunchMemoryRequest
            {
                MemorySolution = memorySolution,
                CustomMemorySize = customMemorySize,
                MemoryInfo = memory with
                {
                    AvailableBytes = memory.AvailableBytes > 0
                        ? memory.AvailableBytes
                        : (long)(ramAvailable * 1024d * 1024d * 1024d)
                },
                Profile = profile,
                ModCount = modCount
            }) / 1024d;

        double ramGameActual = Math.Round(Math.Min(ramGame, ramAvailable), 5);
        double ramUsed = Math.Round(Math.Max(0d, ramTotal - ramAvailable), 5);
        double ramEmpty = Math.Round(Math.Clamp(ramTotal - ramUsed - ramGame, 0d, 1000d), 1);

        sliderRamCustom.MaxValue = GetRamSliderMaxValue(ramTotal);
        labRamGame.Text = Math.Abs(ramGame - ramGameActual) > 0.001d
            ? $"{ramGame:N1} GB (可用 {ramGameActual:N1} GB)"
            : $"{ramGame:N1} GB";
        labRamUsed.Text = $"{ramUsed:N1} GB";
        labRamTotal.Text = $" / {ramTotal:N1} GB";
        if (this.FindControl<MyHint>("LabRamWarn") is { } labRamWarn)
            labRamWarn.IsVisible = false;
        if (this.FindControl<MyHint>("HintRamTooHigh") is { } hintRamTooHigh)
            hintRamTooHigh.IsVisible = ramTotal > 0d && ramGame / ramTotal > 0.75d;

        if (panRamDisplay.ColumnDefinitions.Count >= 3)
        {
            if (showAnim)
            {
                ModAnimation.AniStart(
                    new[]
                    {
                        ModAnimation.AaGridLengthWidth(
                            panRamDisplay.ColumnDefinitions[0],
                            ramUsed - panRamDisplay.ColumnDefinitions[0].Width.Value,
                            800,
                            ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
                        ModAnimation.AaGridLengthWidth(
                            panRamDisplay.ColumnDefinitions[1],
                            ramGameActual - panRamDisplay.ColumnDefinitions[1].Width.Value,
                            800,
                            ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong)),
                        ModAnimation.AaGridLengthWidth(
                            panRamDisplay.ColumnDefinitions[2],
                            ramEmpty - panRamDisplay.ColumnDefinitions[2].Width.Value,
                            800,
                            ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Strong))
                    },
                    "VersionSetup Ram Grid");
            }
            else
            {
                SetRamColumn(panRamDisplay.ColumnDefinitions[0], ramUsed);
                SetRamColumn(panRamDisplay.ColumnDefinitions[1], ramGameActual);
                SetRamColumn(panRamDisplay.ColumnDefinitions[2], ramEmpty);
            }
        }

        Dispatcher.UIThread.Post(RefreshRamText, DispatcherPriority.Loaded);
    }

    private (LaunchMemoryProfile Profile, int ModCount) GetMemoryProfile()
    {
        if (_instance is null)
            return (LaunchMemoryProfile.Vanilla, 0);

        HashSet<string> modFiles = new(StringComparer.OrdinalIgnoreCase);
        AddModFiles(modFiles, Path.Combine(_instance.InstanceDirectory, "mods"));
        if (!_metadata.InstanceIsolation)
        {
            DirectoryInfo? versionsDirectory = Directory.GetParent(_instance.InstanceDirectory);
            if (versionsDirectory?.Parent is { } minecraftRoot)
                AddModFiles(modFiles, Path.Combine(minecraftRoot.FullName, "mods"));
        }

        string versionJson = ReadVersionJson(_instance.VersionJsonPath);
        if (modFiles.Count > 0 || ContainsAny(versionJson, "fabric-loader", "forge", "neoforge", "quilt"))
            return (LaunchMemoryProfile.Modded, modFiles.Count);
        return ContainsAny(versionJson, "optifine")
            ? (LaunchMemoryProfile.OptiFine, 0)
            : (LaunchMemoryProfile.Vanilla, 0);
    }

    private static void AddModFiles(HashSet<string> files, string directory)
    {
        if (!Directory.Exists(directory))
            return;

        try
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*.jar", SearchOption.TopDirectoryOnly))
                files.Add(file);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string ReadVersionJson(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    private static bool ContainsAny(string text, params string[] values) =>
        values.Any(value => text.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static int GetRamSliderMaxValue(double ramTotal)
    {
        if (ramTotal <= 1.5d)
            return (int)Math.Round(Math.Max(Math.Floor((ramTotal - 0.3d) / 0.1d), 1d));
        if (ramTotal <= 8d)
            return (int)Math.Round(Math.Floor((ramTotal - 1.5d) / 0.5d) + 12d);
        if (ramTotal <= 16d)
            return (int)Math.Round(Math.Floor((ramTotal - 8d) / 1d) + 25d);
        return (int)Math.Round(Math.Floor((ramTotal - 16d) / 2d) + 33d);
    }

    private static void SetRamColumn(ColumnDefinition column, double value)
    {
        column.Width = new GridLength(Math.Max(0d, value), GridUnitType.Star);
    }

    private void RefreshRamText()
    {
        if (this.FindControl<Grid>("PanRamDisplay") is not { } panRamDisplay ||
            this.FindControl<Avalonia.Controls.Shapes.Rectangle>("RectRamUsed") is not { } rectRamUsed ||
            this.FindControl<TextBlock>("LabRamGame") is not { } labRamGame ||
            this.FindControl<TextBlock>("LabRamUsed") is not { } labRamUsed ||
            this.FindControl<TextBlock>("LabRamTotal") is not { } labRamTotal ||
            this.FindControl<TextBlock>("LabRamGameTitle") is not { } labRamGameTitle ||
            this.FindControl<TextBlock>("LabRamUsedTitle") is not { } labRamUsedTitle)
        {
            return;
        }

        double rectUsedWidth = rectRamUsed.Bounds.Width;
        double totalWidth = panRamDisplay.Bounds.Width;
        if (totalWidth <= 0d)
            return;

        double labGameWidth = GetTextWidth(labRamGame);
        double labUsedWidth = GetTextWidth(labRamUsed);
        double labTotalWidth = GetTextWidth(labRamTotal);
        double labGameTitleWidth = GetTextWidth(labRamGameTitle);
        double labUsedTitleWidth = GetTextWidth(labRamUsedTitle);

        int left = rectUsedWidth - 30d < labUsedWidth || rectUsedWidth - 30d < labUsedTitleWidth
            ? 0
            : rectUsedWidth - 25d < labUsedWidth + labTotalWidth ? 1 : 2;
        if (_ramTextLeft != left)
        {
            _ramTextLeft = left;
            labRamUsed.Opacity = left == 0 ? 0d : 1d;
            labRamTotal.Opacity = left == 2 ? 1d : 0d;
            labRamUsedTitle.Opacity = left == 0 ? 0d : 0.7d;
        }

        int right = totalWidth < labGameWidth + 2d + rectUsedWidth ||
                    totalWidth < labGameTitleWidth + 2d + rectUsedWidth
            ? 0
            : 1;
        if (right == 0)
        {
            labRamGame.Margin = new Thickness(Math.Max(2d, totalWidth - labGameWidth), 3d, 0d, 0d);
            labRamGameTitle.Margin = new Thickness(Math.Max(2d, totalWidth - labGameTitleWidth), 0d, 0d, 5d);
        }
        else
        {
            labRamGame.Margin = new Thickness(2d + rectUsedWidth, 3d, 0d, 0d);
            labRamGameTitle.Margin = new Thickness(2d + rectUsedWidth, 0d, 0d, 5d);
        }

    }

    private void ReloadGlobalMemorySettings()
    {
        LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
        _globalMemorySolution = settings.GetIntegerOption(LauncherSettingKeys.LaunchRamType, 0);
        _globalCustomMemorySize = settings.GetIntegerOption(LauncherSettingKeys.LaunchRamCustom, 15);
    }

    private static double GetTextWidth(TextBlock textBlock)
    {
        textBlock.Measure(Size.Infinity);
        return Math.Max(textBlock.Bounds.Width, textBlock.DesiredSize.Width);
    }

    private async Task RefreshJavaComboBoxAsync()
    {
        if (_instance is null || this.FindControl<MyComboBox>("ComboArgumentJava") is not { } comboBox)
            return;

        int loadVersion = Interlocked.Increment(ref _javaLoadVersion);
        string instanceDirectory = _instance.InstanceDirectory;
        comboBox.IsEnabled = false;
        _isLoading = true;
        try
        {
            comboBox.Items.Clear();
            comboBox.Items.Add(new MyComboBoxItem { Content = "正在扫描 Java…", IsEnabled = false });
            comboBox.SelectedIndex = 0;
        }
        finally
        {
            _isLoading = false;
        }

        IReadOnlyList<JavaRuntimeCandidate> candidates;
        try
        {
            candidates = await Task.Run(() => new FileSystemJavaLocator()
                .FindAllAsync(CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult()).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            candidates = [];
        }

        if (loadVersion != _javaLoadVersion ||
            _instance is null ||
            !string.Equals(_instance.InstanceDirectory, instanceDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        List<JavaSelectionOption> options =
        [
            new JavaSelectionOption(0, string.Empty, "跟随全局设置"),
            new JavaSelectionOption(1, string.Empty, "自动选择")
        ];
        options.AddRange(candidates
            .Where(static candidate => candidate.IsAvailable && candidate.IsEnabled)
            .Select(static candidate => new JavaSelectionOption(
                2,
                candidate.Installation.JavaExecutablePath,
                $"Java {candidate.Installation.MajorVersion} · {candidate.Installation.Brand} · {candidate.Installation.JavaHome}"))
            .DistinctBy(static option => option.JavaExecutablePath, OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal));
        if (_metadata.JavaSelectionMode == 2 &&
            !string.IsNullOrWhiteSpace(_metadata.SelectedJavaPath) &&
            options.All(option => !string.Equals(
                option.JavaExecutablePath,
                _metadata.SelectedJavaPath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
        {
            options.Add(new JavaSelectionOption(2, _metadata.SelectedJavaPath, "已选择（当前不可用）· " + _metadata.SelectedJavaPath));
        }

        _isLoading = true;
        try
        {
            comboBox.Items.Clear();
            MyComboBoxItem? selected = null;
            foreach (JavaSelectionOption option in options)
            {
                MyComboBoxItem item = new() { Content = option.DisplayText, Tag = option };
                comboBox.Items.Add(item);
                if (option.Mode == _metadata.JavaSelectionMode &&
                    (option.Mode != 2 || string.Equals(
                        option.JavaExecutablePath,
                        _metadata.SelectedJavaPath,
                        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
                {
                    selected = item;
                }
            }
            comboBox.SelectedItem = selected ?? comboBox.Items[0];
            comboBox.IsEnabled = true;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ComboArgumentJava_DropDownOpened(object? sender, EventArgs e)
    {
        if (sender is MyComboBox { IsEnabled: false } comboBox)
            comboBox.IsDropDownOpen = false;
    }

    private void ApplyWindowTitleMode()
    {
        if (this.FindControl<MyComboBox>("TextArgumentTitle") is not { } title ||
            this.FindControl<MyCheckBox>("CheckArgumentTitleEmpty") is not { } useGlobal)
        {
            return;
        }

        useGlobal.IsVisible = string.IsNullOrEmpty(title.Text);
        title.HintText = useGlobal.Checked == true ? "默认" : "跟随全局设置";
    }

    private void TextServerAuthServer_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not MyTextBox textBox || string.IsNullOrWhiteSpace(textBox.Text))
            return;

        string value = textBox.Text.Trim().TrimEnd('/');
        if (!value.EndsWith("/api/yggdrasil", StringComparison.OrdinalIgnoreCase))
            value += "/api/yggdrasil";
        if (!string.Equals(textBox.Text, value, StringComparison.Ordinal))
        {
            textBox.Text = value;
            MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("已格式化认证服务器", "认证服务器地址已补全为 Yggdrasil API 地址。"));
        }
        ApplyServerLoginMode();
    }

    private void ApplyLittleSkinPreset()
    {
        void Apply()
        {
            SetText("TextServerAuthServer", "https://littleskin.cn/api/yggdrasil");
            SetText("TextServerAuthRegister", "https://littleskin.cn/auth/register");
            SetText("TextServerAuthName", "LittleSkin");
            ApplyServerLoginMode();
        }

        if (!string.IsNullOrWhiteSpace(_metadata.AuthServerAddress) &&
            !string.Equals(_metadata.AuthServerAddress, "https://littleskin.cn/api/yggdrasil", StringComparison.OrdinalIgnoreCase))
        {
            ConfirmRequested?.Invoke(this, new SettingsConfirmRequestedEventArgs(
                "覆盖认证服务器",
                "当前已经填写了其他认证服务器，是否替换为 LittleSkin？",
                confirmed => { if (confirmed) Apply(); },
                primaryButton: "替换"));
            return;
        }
        Apply();
    }

    private void LockAuthSettings()
    {
        if (_metadata.AuthSettingsLocked)
            return;

        ConfirmRequested?.Invoke(this, new SettingsConfirmRequestedEventArgs(
            "锁定登录方式",
            "锁定后只能通过初始化该版本的独立设置解除。确定继续吗？",
            confirmed =>
            {
                if (!confirmed)
                    return;
                UpdateMetadata(metadata => metadata with { AuthSettingsLocked = true });
                ApplyServerLoginMode();
            },
            primaryButton: "锁定",
            isWarn: true));
    }

    private void ApplyServerLoginMode()
    {
        bool showAuth = _metadata.ServerLoginRequirement is 2 or 3;
        SetVisible("LabServerAuthServer", showAuth);
        SetVisible("TextServerAuthServer", showAuth);
        SetVisible("LabServerAuthRegister", showAuth);
        SetVisible("TextServerAuthRegister", showAuth);
        SetVisible("LabServerAuthName", showAuth);
        SetVisible("TextServerAuthName", showAuth);
        SetVisible("BtnServerAuthLittle", showAuth);
        SetVisible("BtnServerAuthLock", showAuth);
        SetVisible("BtnServerNewProfile", showAuth);

        bool enabled = !_metadata.AuthSettingsLocked;
        if (this.FindControl<MyComboBox>("ComboServerLoginRequire") is { } requirement)
            requirement.IsEnabled = enabled;
        foreach (string name in new[] { "TextServerAuthServer", "TextServerAuthRegister", "TextServerAuthName" })
        {
            if (this.FindControl<Control>(name) is { } control)
                control.IsEnabled = enabled;
        }
        if (this.FindControl<MyButton>("BtnServerAuthLittle") is { } littleSkin)
            littleSkin.IsEnabled = enabled;
        if (this.FindControl<MyButton>("BtnServerAuthLock") is { } lockButton)
            lockButton.IsEnabled = enabled && Uri.TryCreate(_metadata.AuthServerAddress, UriKind.Absolute, out _);
        SetVisible("HintServerLoginLock", showAuth && _metadata.AuthSettingsLocked);

        bool secure = showAuth && _metadata.AuthServerAddress.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        bool insecure = showAuth && _metadata.AuthServerAddress.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
        SetVisible("LabServerAuthServerSecurityVerify", secure);
        SetVisible("LabServerAuthServerSecurity", insecure);
        SetVisible("LabServerAuthServerSecurityCL", secure || insecure);
        this.FindControl<MyCard>("CardServer")?.TriggerForceResize();
    }

    private void ApplyPreLaunchCommandVisibility()
    {
        SetVisible(
            "CheckAdvanceRunWait",
            !string.IsNullOrWhiteSpace(this.FindControl<MyTextBox>("TextAdvanceRun")?.Text));
    }

    private void SetText(string name, string value)
    {
        if (this.FindControl<MyTextBox>(name) is { } textBox)
            textBox.Text = value;
    }

    private void SetEditableComboText(string name, string value)
    {
        if (this.FindControl<MyComboBox>(name) is { } comboBox)
            comboBox.Text = value;
    }

    private void SetChecked(string name, bool value)
    {
        if (this.FindControl<MyCheckBox>(name) is { } checkBox)
            checkBox.Checked = value;
    }

    private void SetRadio(string name)
    {
        if (this.FindControl<MyRadioBox>(name) is { } radioBox)
            radioBox.Checked = true;
    }

    private void SetSliderValue(string name, int value)
    {
        if (this.FindControl<MySlider>(name) is { } slider)
            slider.Value = value;
    }

    private void SetComboIndex(string name, int index)
    {
        if (this.FindControl<MyComboBox>(name) is { } comboBox && comboBox.ItemCount > 0)
            comboBox.SelectedIndex = Math.Clamp(index, 0, comboBox.ItemCount - 1);
    }

    private sealed record JavaSelectionOption(int Mode, string JavaExecutablePath, string DisplayText)
    {
        public override string ToString() => DisplayText;
    }

    private void SetVisible(string name, bool visible)
    {
        if (this.FindControl<Control>(name) is { } control)
            control.IsVisible = visible;
    }
}
