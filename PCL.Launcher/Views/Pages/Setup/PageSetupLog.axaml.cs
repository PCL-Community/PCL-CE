using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using PCL.Core.App;
using PCL.Core.App.DeviceInfo;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Platform;
using PCL.Core.UI;
using PCL.Core.UI.Hints;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.OS.DeviceInfo;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Setup;

/// <summary>日志设置页：顶部展示当前设备信息，下方列出日志文件、导出当前/全部日志、打开日志目录与清理非活动日志。</summary>
public partial class PageSetupLog : MyPageRight
{
    private bool _deviceInfoLoaded;
    private bool _deviceInfoLoading;
    private bool _loaded;
    private bool _refreshed;

    public PageSetupLog()
    {
        InitializeComponent();
        SetDeviceInfoActionsEnabled(false);
        PanScroll = PanBack;
        DataContext = ViewModel;
        Grid.SetIsSharedSizeScope(PanDeviceSections, true);

        BtnOpenDir.Click += (_, _) => PageSetupLogViewModel.OpenLogDirectory();
        BtnExport.Click += (_, _) => _ = ExportCurrentAsync();
        BtnExportAll.Click += (_, _) => _ = ExportAllAsync();
        BtnClear.Click += (_, _) => _ = CleanAsync();
        BtnExportDeviceInfo.Click += (_, _) => _ = ExportDeviceInfoAsync();
        BtnCopyDeviceInfo.Click += (_, _) => _ = CopyDeviceInfoAsync();

        Loaded += (_, _) =>
        {
            if (_loaded) return;
            _loaded = true;
            Refresh();
            _ = EnsureDeviceInfoAsync();
        };
        Unloaded += (_, _) => _refreshed = false;
    }

    public PageSetupLogViewModel ViewModel { get; } = new();

    public override void OnNavigatedTo(PageParameter? parameter)
    {
        PanBack.ScrollToHome();
        Refresh();
        if (_deviceInfoLoaded)
            RefreshDeviceInfoDisplay();
        else
            _ = EnsureDeviceInfoAsync();
    }

    public override void OnNavigatedFrom()
    {
    }

    private void Refresh()
    {
        if (_refreshed) return;
        _refreshed = true;
        LoadList();
    }

    private void LoadList()
    {
        _ = LoadListAsync();
    }

    private async Task LoadListAsync()
    {
        try
        {
            await ViewModel.LoadListAsync();
            PanList.Children.Clear();
            foreach (var entry in ViewModel.Entries)
            {
                var item = new MyListItem
                {
                    Type = MyListItem.CheckType.Clickable,
                    Info = entry.FilePath
                };
                item.Bind(
                    MyListItem.TitleProperty,
                    new Binding(nameof(SetupLogEntry.Title))
                    {
                        Source = entry
                    });
                var file = entry.FilePath;
                item.Click += (_, _) => Basics.OpenPath(file);
                PanList.Children.Add(item);
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "加载日志列表失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
    }

    private async Task EnsureDeviceInfoAsync()
    {
        if (_deviceInfoLoaded || _deviceInfoLoading)
            return;

        _deviceInfoLoading = true;
        SetDeviceInfoActionsEnabled(false);
        LabDeviceOs.Text = Lang.Text("Setup.Log.Device.Collecting");
        LabDeviceLine1.Text = string.Empty;
        LabDeviceLine2.Text = string.Empty;
        LabDeviceLine3.Text = string.Empty;
        LabDeviceLine4.Text = string.Empty;
        PanDeviceSections.Children.Clear();

        try
        {
            if (!HardwareInfo.HasCollectedHardware)
                await Task.Run(HardwareInfo.GetHardwareInfo);
            RenderDeviceInfo(BuildCurrentSnapshot());
            _deviceInfoLoaded = true;
        }
        catch (Exception ex)
        {
            LabDeviceOs.Text = Lang.Text("Common.State.Unknown");
            LauncherCommon.Log(
                ex,
                "加载设备信息失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
        finally
        {
            _deviceInfoLoading = false;
            SetDeviceInfoActionsEnabled(true);
        }
    }

    private void SetDeviceInfoActionsEnabled(bool enabled)
    {
        BtnExportDeviceInfo.IsEnabled = enabled;
        BtnCopyDeviceInfo.IsEnabled = enabled;
    }

    private void RefreshDeviceInfoDisplay()
    {
        try
        {
            RenderDeviceInfo(BuildCurrentSnapshot());
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", "刷新设备信息失败");
        }
    }

    private void RenderDeviceInfo(DeviceInfoSnapshot snapshot)
    {
        LabDeviceOs.Text = snapshot.Title;
        var lines = snapshot.SummaryLines;
        LabDeviceLine1.Text = lines.Count > 0 ? lines[0] : string.Empty;
        LabDeviceLine2.Text = lines.Count > 1 ? lines[1] : string.Empty;
        LabDeviceLine3.Text = lines.Count > 2 ? lines[2] : string.Empty;
        LabDeviceLine4.Text = lines.Count > 3 ? lines[3] : string.Empty;

        PanDeviceSections.Children.Clear();
        foreach (var section in snapshot.Sections)
        {
            var panel = new StackPanel
            {
                Spacing = 1d
            };
            panel.Children.Add(
                new TextBlock
                {
                    Text = Lang.Text(section.TitleKey),
                    FontSize = 15d,
                    FontWeight = FontWeight.Bold,
                    Margin = new Thickness(0d, 0d, 0d, 3d)
                });
            foreach (var row in section.Rows)
                panel.Children.Add(CreateRow(row));
            PanDeviceSections.Children.Add(panel);
        }
    }

    private static Grid CreateRow(DeviceInfoRow row)
    {
        var grid = new Grid
        {
            Margin = new Thickness(0d)
        };
        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto,
                SharedSizeGroup = "DeviceInfoLabel"
            });
        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(24d)
            });
        grid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(1d, GridUnitType.Star)
            });

        var label = new TextBlock
        {
            Opacity = 0.7d,
            VerticalAlignment = VerticalAlignment.Top,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 2d, 0d, 2d),
            Text = row.LabelArgs is { Length: > 0 }
                ? Lang.Text(row.LabelKey, row.LabelArgs)
                : Lang.Text(row.LabelKey)
        };
        var value = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Top,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0d, 2d, 0d, 2d),
            Text = row.Value
        };
        Grid.SetColumn(value, 2);
        grid.Children.Add(label);
        grid.Children.Add(value);
        return grid;
    }

    private DeviceInfoSection? BuildDisplaySection()
    {
        try
        {
            var kind = ApplicationRuntime.Current.Platform.Kind;
            var rows = new List<DeviceInfoRow>
            {
                new("Setup.Log.Device.Field.DisplayProtocol", GetDisplayProtocol(kind)),
                new("Setup.Log.Device.Field.Dpi", GetDpiText())
            };
            var desktop = GetDesktopEnvironment(kind);
            if (!string.IsNullOrWhiteSpace(desktop))
                rows.Add(new DeviceInfoRow("Setup.Log.Device.Field.DesktopEnvironment", desktop));

            var screens = TopLevel.GetTopLevel(this)?.Screens?.All;
            if (screens is { Count: > 0 })
            {
                rows.Add(
                    new DeviceInfoRow(
                        "Setup.Log.Device.Field.DisplayCount",
                        screens.Count.ToString(CultureInfo.CurrentCulture)));
                var index = 0;
                foreach (var screen in screens.OrderByDescending(screen => screen.IsPrimary))
                {
                    index++;
                    var displayName = string.IsNullOrWhiteSpace(screen.DisplayName)
                        ? Lang.Text("Setup.Log.Device.Field.DisplayName", index)
                        : screen.DisplayName!;
                    var primarySuffix = screen.IsPrimary
                        ? Lang.Text("Setup.Log.Device.Value.PrimarySuffix")
                        : string.Empty;
                    var resolution = $"{screen.Bounds.Width} × {screen.Bounds.Height}";
                    var workArea = $"{screen.WorkingArea.Width} × {screen.WorkingArea.Height}";
                    var scaling = $"{Lang.Number(screen.Scaling * 100d, "N0")}%";
                    var value = $"{displayName}{primarySuffix}\n"
                        + $"{Lang.Text("Setup.Log.Device.Field.DisplayResolution")}: {resolution}\n"
                        + $"{Lang.Text("Setup.Log.Device.Field.DisplayWorkArea")}: {workArea} · "
                        + $"{Lang.Text("Setup.Log.Device.Field.DisplayScaling")}: {scaling}";
                    rows.Add(
                        new DeviceInfoRow(
                            "Setup.Log.Device.Field.DisplayName",
                            value,
                            [index]));
                }
            }
            else
            {
                rows.Add(
                    new DeviceInfoRow(
                        "Setup.Log.Device.Field.DisplayCount",
                        Lang.Text("Common.State.Unknown")));
            }

            return new DeviceInfoSection(
                DeviceInfoSectionKind.Display,
                "Setup.Log.Device.Section.Display",
                rows);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", "读取显示信息失败");
            return null;
        }
    }

    private static string GetDisplayProtocol(PlatformKind kind)
    {
        return kind switch
        {
            PlatformKind.Windows => "DWM",
            PlatformKind.MacOs => "Quartz",
            PlatformKind.Linux => GetLinuxDisplayProtocol(),
            _ => Lang.Text("Common.State.Unknown")
        };
    }

    private static string GetLinuxDisplayProtocol()
    {
        var session = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        return session switch
        {
            _ when string.Equals(session, "wayland", StringComparison.OrdinalIgnoreCase)
                => "Wayland",
            _ when string.Equals(session, "x11", StringComparison.OrdinalIgnoreCase)
                => "X11",
            _ when !string.IsNullOrWhiteSpace(session)
                => session,
            _ when !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
                => "Wayland",
            _ when !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
                => "X11",
            _ => Lang.Text("Common.State.Unknown")
        };
    }

    private static string? GetDesktopEnvironment(PlatformKind kind)
    {
        return kind switch
        {
            PlatformKind.Windows => "Windows Shell",
            PlatformKind.MacOs => "Aqua",
            PlatformKind.Linux => Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP"),
            _ => null
        };
    }

    private static string GetDpiText()
    {
        var scale = LauncherCommon.Dpi / 96d;
        return $"{LauncherCommon.Dpi} ({Lang.Number(scale * 100d, "N0")}%)";
    }

    private DeviceInfoSnapshot BuildCurrentSnapshot()
    {
        var facts = DeviceInfoFactsCollector.Collect(
            Paths.Data,
            GameFolders.mcFolderSelected);
        return DeviceInfoSectionBuilder.Build(facts, BuildDisplaySection());
    }

    private async Task ExportDeviceInfoAsync()
    {
        try
        {
            var text = DeviceInfoTextFormatter.Format(BuildCurrentSnapshot());
            var fileName = "PCL_CE_DeviceInfo_"
                + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
                + ".txt";
            var path = await SystemDialogs.SelectSaveFileAsync(
                Lang.Text("Setup.Log.Device.Export.SaveTitle"),
                fileName,
                Lang.Text("Setup.Log.Device.Export.Filter"),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            if (string.IsNullOrEmpty(path))
                return;

            await File.WriteAllTextAsync(path, text);
            HintService.Show(Hints.Success(Lang.Text("Setup.Log.Device.Export.Success")));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "导出设备信息失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Setup.Log.Device.Export.Failed"));
        }
    }

    private async Task CopyDeviceInfoAsync()
    {
        try
        {
            var text = DeviceInfoTextFormatter.Format(BuildCurrentSnapshot());
            var clipboard = ApplicationRuntime.GetService<IClipboardService>()
                ?? throw new InvalidOperationException("剪贴板服务不可用");
            await clipboard.SetTextAsync(text);
            HintService.Show(Hints.Success(Lang.Text("Common.Hint.Copied")));
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", "复制设备信息失败");
            HintService.Show(Hints.Error(Lang.Text("Common.Hint.CopyFailed")));
        }
    }

    private async Task CleanAsync()
    {
        var ctl = BtnClear;
        ctl.IsEnabled = false;
        try
        {
            if (await ViewModel.CleanAsync())
                LoadList();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "清理日志文件失败",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
        finally
        {
            ctl.IsEnabled = true;
        }
    }

    private async Task ExportAllAsync()
    {
        try
        {
            await ViewModel.ExportAllAsync();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Setup.Log.ExportFailed"),
                LauncherLogLevel.Hint,
                userSummary: Lang.Text("Setup.Log.ExportFailed"));
        }
    }

    private async Task ExportCurrentAsync()
    {
        try
        {
            await ViewModel.ExportCurrentAsync();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Setup.Log.ExportFailed"),
                LauncherLogLevel.Hint,
                userSummary: Lang.Text("Setup.Log.ExportFailed"));
        }
    }
}