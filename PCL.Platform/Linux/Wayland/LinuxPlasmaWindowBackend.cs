using NWayland;
using NWayland.Protocols.Plasma.PlasmaWindowManagement;
using NWayland.Protocols.Wayland;
using PCL.Core.Logging;

namespace PCL.Platform;

/// <summary>KWin 的 org_kde_plasma_window_management 后端，可按 PID 精确匹配窗口。</summary>
internal sealed class LinuxPlasmaWindowBackend
{
    private const string ModuleName = "WindowInterop";
    
    private const uint RequiredVersion = 17;
    private const uint StateMaximized = 0x4;

    private readonly Dictionary<nint, WindowRecord> _byHandle = [];
    private readonly Lock _gate = new();
    private readonly HashSet<string> _knownUuids = [];
    private readonly List<string> _pendingUuids = [];
    private readonly Dictionary<string, WindowRecord> _windows = [];

    private WlDisplay? _display;
    private OrgKdePlasmaWindowManagement? _manager;
    private OrgKdePlasmaWindowManagement.Listener.Relay? _managerListener;
    private long _nextHandle;
    private WlRegistry.Listener.Relay? _registryListener;
    private bool _unavailable;

    public (nint Handle, string Title)? TryFindWindow(int processId)
    {
        lock (_gate)
        {
            try
            {
                if (!_EnsureInitialized())
                    return null;
                if (!_Pump())
                    return null;

                if (_CreatePendingWindows() && !_Pump())
                    return null;

                _CleanupClosed();
                return _FindBest(processId);
            }
            catch (Exception ex)
            {
                _MarkUnavailable($"KDE Wayland 窗口交互失败：{ex.Message}");
                return null;
            }
        }
    }

    public void Maximize(nint handle)
    {
        lock (_gate)
        {
            if (_unavailable
                || !_byHandle.TryGetValue(handle, out var record)
                || record.Closed)
                return;

            try
            {
                record.Window.SetState(StateMaximized, StateMaximized);
                _display?.Flush();
            }
            catch (Exception ex)
            {
                LogWrapper.Debug(ModuleName, $"最大化 KDE Wayland 窗口失败：{ex.Message}");
            }
        }
    }

    private bool _EnsureInitialized()
    {
        if (_unavailable)
            return false;
        if (_manager is not null)
            return true;

        try
        {
            _display = WlDisplay.Connect(null, new WlDisplayOptions());
            _registryListener = new WlRegistry.Listener.Relay
            {
                OnGlobal = _OnGlobal
            };
            _display.GetRegistry(_registryListener);

            if (!_Pump())
                return false;

            if (_manager is null)
            {
                _MarkUnavailable(
                    "当前合成器未提供 org_kde_plasma_window_management 协议，跳过 KWin Wayland 窗口后端");
                return false;
            }

            return _Pump();
        }
        catch (Exception ex)
        {
            _MarkUnavailable($"无法连接 KDE Wayland 窗口协议：{ex.Message}");
            return false;
        }
    }

    private void _OnGlobal(
        WlRegistry registry,
        uint name,
        string @interface,
        uint version)
    {
        if (_manager is not null
            || @interface != "org_kde_plasma_window_management"
            || version < RequiredVersion)
            return;

        _managerListener = new OrgKdePlasmaWindowManagement.Listener.Relay
        {
            OnWindowWithUuid = _OnWindowWithUuid
        };
        _manager = OrgKdePlasmaWindowManagement.Bind(
            registry,
            name,
            Math.Min(version, RequiredVersion),
            _managerListener);
    }

    private void _OnWindowWithUuid(
        OrgKdePlasmaWindowManagement sender,
        uint id,
        string uuid)
    {
        if (string.IsNullOrEmpty(uuid) || !_knownUuids.Add(uuid))
            return;
        _pendingUuids.Add(uuid);
    }

    private bool _CreatePendingWindows()
    {
        if (_manager is null || _pendingUuids.Count == 0)
            return false;

        foreach (var uuid in _pendingUuids)
        {
            if (_windows.ContainsKey(uuid))
                continue;

            var record = new WindowRecord
            {
                Handle = unchecked((nint)Interlocked.Increment(ref _nextHandle))
            };
            var listener = new OrgKdePlasmaWindow.Listener.Relay
            {
                OnTitleChanged = (_, title) => record.Title = title ?? string.Empty,
                OnAppIdChanged = (_, appId) => record.AppId = appId ?? string.Empty,
                OnPidChanged = (_, pid) => record.Pid = pid,
                OnInitialState = _ => record.Ready = true,
                OnUnmapped = _ => record.Closed = true
            };
            record.Window = _manager.GetWindowByUuid(uuid, listener);

            _windows[uuid] = record;
            _byHandle[record.Handle] = record;
        }

        _pendingUuids.Clear();
        return true;
    }

    private void _CleanupClosed()
    {
        var closed = _windows
            .Where(pair => pair.Value.Closed)
            .Select(pair => pair.Key)
            .ToArray();

        foreach (var uuid in closed)
        {
            var record = _windows[uuid];
            _windows.Remove(uuid);
            _byHandle.Remove(record.Handle);
            _knownUuids.Remove(uuid);
            record.Window.Dispose();
        }
    }

    private (nint Handle, string Title)? _FindBest(int processId)
    {
        WindowRecord? best = null;
        var bestScore = 0;

        foreach (var record in _byHandle.Values)
        {
            if (record.Closed
                || !record.Ready
                || record.Pid != (uint)processId)
                continue;

            var score = LinuxGameWindowMatcher.Score(record.AppId, record.Title);
            if (score < LinuxGameWindowMatcher.GameWindowThreshold)
                continue;

            if (best is not null)
            {
                if (score < bestScore)
                    continue;
                if (score == bestScore && record.Handle <= best.Handle)
                    continue;
            }

            best = record;
            bestScore = score;
        }

        return best is null
            ? null
            : (best.Handle, best.Title);
    }

    private bool _Pump()
    {
        var display = _display;
        if (display is null)
            return false;

        var result = display.Roundtrip();
        if (result >= 0)
            return true;

        _MarkUnavailable($"KDE Wayland 事件循环失败：{display.GetError()}");
        return false;
    }

    private void _MarkUnavailable(string message)
    {
        if (_unavailable) return;
        _unavailable = true;
        LogWrapper.Debug(ModuleName, message);

        try
        {
            _display?.Dispose();
        }
        catch (Exception)
        {
            // 连接已经损坏时回收失败没有补救意义。
        }

        _display = null;
        _manager = null;
        _managerListener = null;
        _registryListener = null;
        _windows.Clear();
        _byHandle.Clear();
        _knownUuids.Clear();
        _pendingUuids.Clear();
    }

    private sealed class WindowRecord
    {
        public required nint Handle { get; init; }
        public OrgKdePlasmaWindow Window { get; set; } = null!;
        public string Title { get; set; } = string.Empty;
        public string AppId { get; set; } = string.Empty;
        public uint Pid { get; set; }
        public bool Ready { get; set; }
        public bool Closed { get; set; }
    }
}