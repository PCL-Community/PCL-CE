// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PCL.Application.Launching;
using PCL.Application.Settings;
using PCL.Desktop.Controls.Legacy;
using PCL.Platform.System;
using PCL.Platform.Abstractions.System;

#pragma warning disable CA1822, CS0067

namespace PCL.Desktop.Features.Settings.Views;

public partial class PageSetupLaunch : MyPageRight, ISettingsPageInteractionSource
{
    private const string DefaultJvmArguments =
        "-XX:+UseG1GC -XX:-UseAdaptiveSizePolicy -XX:-OmitStackTraceInFastThrow " +
        "-Djdk.lang.Process.allowAmbiguousCommands=true -Dfml.ignoreInvalidMinecraftCertificates=True " +
        "-Dfml.ignorePatchDiscrepancies=True -Dlog4j2.formatMsgNoLookups=true";

    private readonly ISystemInfoProvider _systemInfoProvider;
    private readonly DispatcherTimer _ramRefreshTimer;
    private int _ramTextLeft = 2;
    private int _ramTextRight = 1;
    private bool _hasAttached;

    public PageSetupLaunch()
        : this(new DefaultSystemInfoProvider())
    {
    }

    public PageSetupLaunch(ISystemInfoProvider systemInfoProvider)
    {
        _systemInfoProvider = systemInfoProvider ?? throw new ArgumentNullException(nameof(systemInfoProvider));
        AvaloniaXamlLoader.Load(this);
        if (this.FindControl<MyScrollViewer>("PanBack") is { } panBack)
            PanScroll = panBack;
        LauncherSettingsPageBinder.Attach(this);
        _ramRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _ramRefreshTimer.Tick += (_, _) => RefreshRam(showAnim: true);
        AttachedToVisualTree += (_, _) =>
        {
            if (!_hasAttached)
            {
                _hasAttached = true;
                this.FindControl<MyScrollViewer>("PanBack")?.ScrollToHome();
            }
            RefreshDependentVisibility();
            RefreshRam(showAnim: false);
            _ramRefreshTimer.Start();
        };
        DetachedFromVisualTree += (_, _) => _ramRefreshTimer.Stop();
        if (this.FindControl<Grid>("PanRamDisplay") is { } ramDisplay)
            ramDisplay.SizeChanged += (_, _) => RefreshRamText();
        if (this.FindControl<Avalonia.Controls.Shapes.Rectangle>("RectRamUsed") is { } ramUsed)
            ramUsed.SizeChanged += (_, _) => RefreshRamText();
        if (this.FindControl<MyRadioBox>("RadioRamType1") is { } customMode &&
            this.FindControl<MySlider>("SliderRamCustom") is { } customSlider)
        {
            customMode.GetObservable(MyRadioBox.CheckedProperty).Subscribe(isChecked => customSlider.IsEnabled = isChecked);
        }
    }

    public event EventHandler<SettingsPathRequestedEventArgs>? OpenPathRequested;

    public event EventHandler<SettingsUrlRequestedEventArgs>? OpenUrlRequested;

    public event EventHandler<SettingsMessageRequestedEventArgs>? MessageRequested;

    public event EventHandler<SettingsConfirmRequestedEventArgs>? ConfirmRequested;

    public event EventHandler? SwitchToInstanceSetupRequested;

    public void RefreshMemoryDisplay() => RefreshRam(showAnim: false);

    private void BtnAdvanceJvmReset_Click(object? sender, EventArgs e)
    {
        if (TextAdvanceJvm is not null)
            TextAdvanceJvm.Text = DefaultJvmArguments;

        LauncherSettings settings = LauncherSettingsPageBinder.LoadSettings();
        settings.SetTextOption(LauncherSettingKeys.LaunchAdvanceJvm, DefaultJvmArguments);
        LauncherSettingsPageBinder.SaveSettings(settings);
        RefreshDependentVisibility();
        MessageRequested?.Invoke(this, new SettingsMessageRequestedEventArgs("已恢复默认 JVM 参数", "JVM 参数已恢复为 PCL N 推荐的默认值。"));
    }

    private void BtnSwitch_Click(object? sender, EventArgs e)
    {
        SwitchToInstanceSetupRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CheckBoxChange(object sender, bool user)
    {
    }

    private void ComboAdvanceRenderer_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ComboChange(sender, e);
        if (_hasAttached && this.IsAttachedToVisualTree() && sender is MyComboBox { IsDropDownOpen: true, SelectedIndex: > 0 })
        {
            MessageRequested?.Invoke(
                this,
                new SettingsMessageRequestedEventArgs(
                    "渲染器设置已更改",
                    "非默认渲染器可能导致部分 Minecraft 版本无法启动。若遇到启动问题，请先切回“游戏默认”。"));
        }
    }

    private void ComboArgumentIndie_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ComboChange(sender, e);
        if (_hasAttached && this.IsAttachedToVisualTree() && sender is MyComboBox { IsDropDownOpen: true, SelectedIndex: > 0 })
        {
            MessageRequested?.Invoke(
                this,
                new SettingsMessageRequestedEventArgs(
                    "实例隔离说明",
                    "修改实例隔离策略只影响之后读取的游戏目录结构。已经存在的文件不会被自动移动。"));
        }
    }

    private void ComboArgumentVisibie_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ComboChange(sender, e);
        if (_hasAttached && this.IsAttachedToVisualTree() && sender is MyComboBox { IsDropDownOpen: true, SelectedIndex: 0 })
        {
            MessageRequested?.Invoke(
                this,
                new SettingsMessageRequestedEventArgs(
                    "启动器会在游戏启动后关闭",
                    "如果游戏启动失败，你可能需要重新打开启动器查看日志。"));
        }
    }

    private void ComboChange(object? sender, SelectionChangedEventArgs e)
    {
        RefreshDependentVisibility();
    }

    private void RadioBoxChange(object sender, RouteEventArgs e)
    {
        RefreshDependentVisibility();
        RefreshRam(showAnim: true);
    }

    private void SliderChange(object sender, bool user)
    {
        RefreshRam(showAnim: true);
    }

    private void TextAdvanceJvm_TextChanged(object? sender, TextChangedEventArgs e)
    {
        RefreshDependentVisibility();
    }

    private void TextAdvanceRun_TextChanged(object? sender, TextChangedEventArgs e)
    {
        RefreshDependentVisibility();
    }

    private void TextArgumentTitle_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is MyComboBox { Text: { Length: > 100 } text } combo)
            combo.Text = text[..100];
    }

    private void TextBoxChange(object? sender, TextChangedEventArgs e)
    {
        RefreshDependentVisibility();
    }

    private void RefreshDependentVisibility()
    {
        if (ComboArgumentWindowType is not null)
        {
            bool customWindowSize = ComboArgumentWindowType.SelectedIndex == 3;
            if (TextArgumentWindowWidth is not null)
                TextArgumentWindowWidth.IsVisible = customWindowSize;
            if (TextArgumentWindowHeight is not null)
                TextArgumentWindowHeight.IsVisible = customWindowSize;
            if (LabArgumentWindowMiddle is not null)
                LabArgumentWindowMiddle.IsVisible = customWindowSize;
        }

        if (CheckAdvanceRunWait is not null && TextAdvanceRun is not null)
            CheckAdvanceRunWait.IsVisible = !string.IsNullOrWhiteSpace(TextAdvanceRun.Text);

        if (BtnAdvanceJvmReset is not null && TextAdvanceJvm is not null)
            BtnAdvanceJvmReset.IsVisible = !string.Equals(
                TextAdvanceJvm.Text ?? string.Empty,
                DefaultJvmArguments,
                StringComparison.Ordinal);

        if (SliderRamCustom is not null && RadioRamType1 is not null)
            SliderRamCustom.IsEnabled = RadioRamType1.Checked;
    }

    private void RefreshRam(bool showAnim)
    {
        MySlider? sliderRamCustom = this.FindControl<MySlider>("SliderRamCustom");
        TextBlock? labRamGame = this.FindControl<TextBlock>("LabRamGame");
        TextBlock? labRamUsed = this.FindControl<TextBlock>("LabRamUsed");
        TextBlock? labRamTotal = this.FindControl<TextBlock>("LabRamTotal");
        MyHint? labRamWarn = this.FindControl<MyHint>("LabRamWarn");
        MyHint? hintRamTooHigh = this.FindControl<MyHint>("HintRamTooHigh");
        Grid? panRamDisplay = this.FindControl<Grid>("PanRamDisplay");
        if (labRamGame is null || labRamUsed is null || labRamTotal is null || sliderRamCustom is null || panRamDisplay is null)
            return;

        PCL.Platform.Abstractions.System.MemoryInfo memory = _systemInfoProvider.GetMemoryInfo();
        double ramTotal = Math.Round(Math.Max(memory.TotalBytes, 4L * 1024 * 1024 * 1024) / 1024d / 1024d / 1024d, 1);
        double ramAvailable = memory.AvailableBytes > 0
            ? Math.Round(memory.AvailableBytes / 1024d / 1024d / 1024d, 1)
            : Math.Round(ramTotal * 0.65d, 1);
        ramAvailable = Math.Clamp(ramAvailable, 0.1d, ramTotal);

        int memoryMode = this.FindControl<MyRadioBox>("RadioRamType1")?.Checked == true ? 1 : 0;
        double ramGame = memoryMode == 1
            ? LaunchMemoryCalculator.SliderValueToGigabytes(sliderRamCustom.Value)
            : LaunchMemoryCalculator.ResolveMemoryMegabytes(
                new LaunchMemoryRequest
                {
                    MemorySolution = 0,
                    CustomMemorySize = sliderRamCustom.Value,
                    MemoryInfo = memory with { AvailableBytes = memory.AvailableBytes > 0 ? memory.AvailableBytes : (long)(ramAvailable * 1024d * 1024d * 1024d) },
                    Profile = LaunchMemoryProfile.Vanilla
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
        if (labRamWarn is not null)
            labRamWarn.IsVisible = false;
        if (hintRamTooHigh is not null)
            hintRamTooHigh.IsVisible = ramTotal > 0d && ramGame / ramTotal > 0.75d;

        if (panRamDisplay.ColumnDefinitions.Count >= 3)
        {
            SetRamColumn(panRamDisplay.ColumnDefinitions[0], ramUsed);
            SetRamColumn(panRamDisplay.ColumnDefinitions[1], ramGameActual);
            SetRamColumn(panRamDisplay.ColumnDefinitions[2], ramEmpty);
        }
        // Text changes take effect before the deferred layout pass. Reposition immediately so a
        // freshly widened label can never spend one frame on the previous (too far left) margin.
        RefreshRamText();
        Dispatcher.UIThread.Post(RefreshRamText, DispatcherPriority.Loaded);
    }

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
        Grid? panRamDisplay = this.FindControl<Grid>("PanRamDisplay");
        Avalonia.Controls.Shapes.Rectangle? rectRamUsed = this.FindControl<Avalonia.Controls.Shapes.Rectangle>("RectRamUsed");
        TextBlock? labRamGame = this.FindControl<TextBlock>("LabRamGame");
        TextBlock? labRamUsed = this.FindControl<TextBlock>("LabRamUsed");
        TextBlock? labRamTotal = this.FindControl<TextBlock>("LabRamTotal");
        TextBlock? labRamGameTitle = this.FindControl<TextBlock>("LabRamGameTitle");
        TextBlock? labRamUsedTitle = this.FindControl<TextBlock>("LabRamUsedTitle");
        if (panRamDisplay is null || rectRamUsed is null || labRamGame is null || labRamUsed is null ||
            labRamTotal is null || labRamGameTitle is null || labRamUsedTitle is null)
            return;

        double rectUsedWidth = rectRamUsed.Bounds.Width;
        double totalWidth = panRamDisplay.Bounds.Width;
        if (totalWidth <= 0d)
            return;

        labRamGame.MaxWidth = double.PositiveInfinity;
        labRamGameTitle.MaxWidth = double.PositiveInfinity;
        double labGameWidth = GetTextWidth(labRamGame);
        double labUsedWidth = GetTextWidth(labRamUsed);
        double labTotalWidth = GetTextWidth(labRamTotal);
        double labGameTitleWidth = GetTextWidth(labRamGameTitle);
        double labUsedTitleWidth = GetTextWidth(labRamUsedTitle);
        double gameAvailableWidth = Math.Max(0d, totalWidth - rectUsedWidth - 2d);
        labRamGame.MaxWidth = gameAvailableWidth;
        labRamGameTitle.MaxWidth = gameAvailableWidth;
        labRamGame.TextTrimming = TextTrimming.CharacterEllipsis;
        labRamGameTitle.TextTrimming = TextTrimming.CharacterEllipsis;

        int left;
        if (rectUsedWidth - 30d < labUsedWidth || rectUsedWidth - 30d < labUsedTitleWidth)
            left = 0;
        else if (rectUsedWidth - 25d < labUsedWidth + labTotalWidth)
            left = 1;
        else
            left = 2;

        if (left > _ramTextLeft && rectUsedWidth < Math.Max(labUsedWidth, labUsedTitleWidth) + 46d)
            left = _ramTextLeft;
        if (left == 2 && _ramTextLeft < 2 && rectUsedWidth < labUsedWidth + labTotalWidth + 41d)
            left = _ramTextLeft;

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
        double rightRequiredWidth = Math.Max(labGameWidth, labGameTitleWidth) + 2d + rectUsedWidth;
        if (_ramTextRight == 0 && right == 1 && totalWidth < rightRequiredWidth + 16d)
            right = 0;

        if (right == 0)
        {
            labRamGame.Margin = new Thickness(Math.Max(rectUsedWidth + 2d, totalWidth - labGameWidth), 3d, 0d, 0d);
            labRamGameTitle.Margin = new Thickness(Math.Max(rectUsedWidth + 2d, totalWidth - labGameTitleWidth), 0d, 0d, 5d);
        }
        else
        {
            labRamGame.Margin = new Thickness(2d + rectUsedWidth, 3d, 0d, 0d);
            labRamGameTitle.Margin = new Thickness(2d + rectUsedWidth, 0d, 0d, 5d);
        }

        _ramTextRight = right;
    }

    private static double GetTextWidth(TextBlock textBlock)
    {
        textBlock.Measure(Size.Infinity);
        return Math.Max(textBlock.Bounds.Width, textBlock.DesiredSize.Width);
    }
}
