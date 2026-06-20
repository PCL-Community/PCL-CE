// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Windows.Input;
using PCL.UI.Abstractions;

namespace PCL.Desktop.ViewModels.Tools;

public sealed class ControlsGalleryViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;
    private readonly IHintService _hintService;
    private readonly INotificationService _notificationService;
    private readonly IThemeService _themeService;
    private double _sliderValue = 13;

    public ControlsGalleryViewModel(
        IDialogService dialogService,
        IHintService hintService,
        INotificationService notificationService,
        IThemeService themeService)
    {
        _dialogService = dialogService;
        _hintService = hintService;
        _notificationService = notificationService;
        _themeService = themeService;
        ShowInfoCommand =
            new DelegateCommand(() => _hintService.ShowInfo("设置已保留。"));
        ShowSuccessCommand =
            new DelegateCommand(() => _hintService.ShowSuccess("操作已完成。"));
        ShowWarningCommand =
            new DelegateCommand(
                () => _hintService.ShowWarning("请确认当前选择后再继续。"));
        ShowErrorCommand =
            new DelegateCommand(
                () => _hintService.ShowError("操作失败，请检查网络后重试。"));
        ShowToastCommand =
            new AsyncDelegateCommand(
                () => _notificationService.ShowToastAsync(
                    "下载完成",
                    "文件已保存到选定目录。",
                    HintSeverity.Success));
        ShowDialogCommand =
            new AsyncDelegateCommand(
                () => _dialogService.ShowMessageAsync(
                    "界面组件",
                    "此窗口由 Avalonia Dialog 服务创建，支持键盘与跨平台焦点管理。"));
        ShowConfirmCommand =
            new AsyncDelegateCommand(ShowConfirmationAsync);
        UseSystemThemeCommand =
            new DelegateCommand(
                () => _themeService.Apply(
                    ThemeMode.System,
                    AccentColor.CatBlue));
        UseLightThemeCommand =
            new DelegateCommand(
                () => _themeService.Apply(
                    ThemeMode.Light,
                    AccentColor.CatBlue));
        UseDarkThemeCommand =
            new DelegateCommand(
                () => _themeService.Apply(
                    ThemeMode.Dark,
                    AccentColor.CatBlue));
    }

    public ICommand ShowInfoCommand { get; }

    public ICommand ShowSuccessCommand { get; }

    public ICommand ShowWarningCommand { get; }

    public ICommand ShowErrorCommand { get; }

    public ICommand ShowToastCommand { get; }

    public ICommand ShowDialogCommand { get; }

    public ICommand ShowConfirmCommand { get; }

    public ICommand UseSystemThemeCommand { get; }

    public ICommand UseLightThemeCommand { get; }

    public ICommand UseDarkThemeCommand { get; }

    public double SliderValue
    {
        get => _sliderValue;
        set
        {
            if (!SetProperty(ref _sliderValue, value))
                return;
            OnPropertyChanged(nameof(SliderHint));
        }
    }

    public string SliderHint =>
        $"最多显示 {Math.Round(SliderValue):0} 组内容";

    private async Task ShowConfirmationAsync()
    {
        bool accepted = await _dialogService.ConfirmAsync(
            "确认操作",
            "是否继续执行示例操作？");
        if (accepted)
            _hintService.ShowSuccess("已确认继续。");
        else
            _hintService.ShowInfo("操作已取消。");
    }
}
