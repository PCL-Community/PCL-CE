using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Media;
using PCL.Core.Platform;

namespace PCL.Core.UI.Background;

/// <summary>FFmpeg 状态提示使用的颜色。</summary>
public enum BackgroundFfmpegStatusTheme
{
    Blue,
    Yellow,
    Red
}

/// <summary>设置页的 FFmpeg 路径、检测状态与视频自动暂停选项。</summary>
public sealed class BackgroundFfmpegSettings : ObservableObject, IDisposable
{
    private readonly ConfigObserver _autoPauseObserver;
    private readonly ConfigObserver _ffmpegPathObserver;

    private bool _autoPauseVideo;
    private string _ffmpegPath;
    private string _ffmpegStatusText = string.Empty;
    private BackgroundFfmpegStatusTheme _ffmpegStatusTheme;
    private string _ffmpegToolTip;
    private int _statusGeneration;
    private bool _syncing;

    public BackgroundFfmpegSettings()
    {
        _ffmpegPath = Config.Preference.Background.FfmpegPath;
        _autoPauseVideo = Config.Preference.Background.AutoPauseVideo;
        _ffmpegToolTip = Lang.Text("Setup.Ui.Background.Ffmpeg.ToolTip");

        BrowseCommand = new AsyncRelayCommand(BrowseAsync);
        DetectCommand = new AsyncRelayCommand(DetectAsync);

        _autoPauseObserver = new ConfigObserver(ConfigEvent.Update, _ => SyncAutoPauseVideo());
        _ffmpegPathObserver = new ConfigObserver(ConfigEvent.Update, _ => SyncFfmpegPath());
        Config.Preference.Background.AutoPauseVideoConfig.Observe(_autoPauseObserver);
        Config.Preference.Background.FfmpegPathConfig.Observe(_ffmpegPathObserver);
        BackgroundHost.StateChanged += OnBackgroundStateChanged;

        UpdatePauseVisibility();
        _ = RefreshStatusAsync(false);
    }

    public IAsyncRelayCommand BrowseCommand { get; }

    public IAsyncRelayCommand DetectCommand { get; }

    public string FfmpegPath
    {
        get => _ffmpegPath;
        set
        {
            var normalized = value ?? string.Empty;
            if (!SetProperty(ref _ffmpegPath, normalized) || _syncing) return;
            Config.Preference.Background.FfmpegPath = normalized;
        }
    }

    public string FfmpegStatusText
    {
        get => _ffmpegStatusText;
        private set => SetProperty(ref _ffmpegStatusText, value);
    }

    public bool FfmpegStatusVisible => !string.IsNullOrWhiteSpace(FfmpegStatusText);

    public BackgroundFfmpegStatusTheme FfmpegStatusTheme
    {
        get => _ffmpegStatusTheme;
        private set => SetProperty(ref _ffmpegStatusTheme, value);
    }

    public string FfmpegToolTip
    {
        get => _ffmpegToolTip;
        private set => SetProperty(ref _ffmpegToolTip, value);
    }

    public bool AutoPauseVideo
    {
        get => _autoPauseVideo;
        set
        {
            if (!SetProperty(ref _autoPauseVideo, value) || _syncing) return;
            Config.Preference.Background.AutoPauseVideo = value;
            BackgroundHost.Current?.ApplyAutoPauseSetting();
        }
    }

    public bool IsPauseVisible => BackgroundHost.Current?.HasContent == true;

    public void Dispose()
    {
        Config.Preference.Background.AutoPauseVideoConfig.Unobserve(_autoPauseObserver);
        Config.Preference.Background.FfmpegPathConfig.Unobserve(_ffmpegPathObserver);
        BackgroundHost.StateChanged -= OnBackgroundStateChanged;
    }

    public event Action<BackgroundFfmpegStatusTheme>? StatusThemeChanged;

    public async Task RefreshStatusAsync(bool force)
    {
        var generation = Interlocked.Increment(ref _statusGeneration);
        try
        {
            if (BackgroundHost.Current is not { } background)
            {
                if (generation != Volatile.Read(ref _statusGeneration)) return;
                ApplyStatus(null);
                return;
            }

            var result = await background.ProbeFfmpegAsync(force);
            if (generation != Volatile.Read(ref _statusGeneration)) return;
            ApplyStatus(result);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", "检测 FFmpeg 失败");
            if (generation != Volatile.Read(ref _statusGeneration)) return;
            ApplyStatus(null);
        }
    }

    private async Task BrowseAsync()
    {
        try
        {
            var dialog = ApplicationRuntime.GetService<IFileDialogService>()
                ?? new FileDialogService();
            var path = await dialog.SelectFileAsync(
                Lang.Text("Setup.Ui.Background.Ffmpeg.FileFilter"),
                Lang.Text("Setup.Ui.Background.Ffmpeg.SelectTitle"));
            if (string.IsNullOrEmpty(path)) return;

            FfmpegPath = path;
            await RefreshStatusAsync(true);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", "选择 FFmpeg 路径失败");
        }
    }

    private Task DetectAsync()
    {
        return RefreshStatusAsync(true);
    }

    private void ApplyStatus(FfmpegProbeResult? result)
    {
        if (result is null)
        {
            FfmpegStatusText = string.Empty;
            FfmpegStatusTheme = BackgroundFfmpegStatusTheme.Yellow;
            FfmpegToolTip = Lang.Text("Setup.Ui.Background.Ffmpeg.ToolTip");
            OnPropertyChanged(nameof(FfmpegStatusVisible));
            StatusThemeChanged?.Invoke(FfmpegStatusTheme);
            return;
        }

        FfmpegStatusTheme = result.Availability switch
        {
            FfmpegAvailability.Available => BackgroundFfmpegStatusTheme.Blue,
            FfmpegAvailability.Invalid => BackgroundFfmpegStatusTheme.Red,
            _ => BackgroundFfmpegStatusTheme.Yellow
        };
        FfmpegStatusText = result.Availability switch
        {
            FfmpegAvailability.Available => Lang.Text(
                "Setup.Ui.Background.Ffmpeg.Available",
                result.Version ?? result.ExecutablePath ?? string.Empty),
            FfmpegAvailability.Invalid => Lang.Text(
                "Setup.Ui.Background.Ffmpeg.Invalid",
                result.Error ?? result.ExecutablePath ?? string.Empty),
            _ => Lang.Text("Setup.Ui.Background.Ffmpeg.NotFound")
        };

        var details = string.Join(
            " · ",
            new[]
                {
                    result.Version,
                    result.ExecutablePath
                }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        FfmpegToolTip = result.IsAvailable && !string.IsNullOrWhiteSpace(details)
            ? details
            : Lang.Text("Setup.Ui.Background.Ffmpeg.ToolTip");
        OnPropertyChanged(nameof(FfmpegStatusVisible));
        StatusThemeChanged?.Invoke(FfmpegStatusTheme);
    }

    private void SyncAutoPauseVideo()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(SyncAutoPauseVideo);
            return;
        }

        _syncing = true;
        try
        {
            AutoPauseVideo = Config.Preference.Background.AutoPauseVideo;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void SyncFfmpegPath()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(SyncFfmpegPath);
            return;
        }

        _syncing = true;
        try
        {
            FfmpegPath = Config.Preference.Background.FfmpegPath;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnBackgroundStateChanged()
    {
        UpdatePauseVisibility();
    }

    private void UpdatePauseVisibility()
    {
        OnPropertyChanged(nameof(IsPauseVisible));
    }
}