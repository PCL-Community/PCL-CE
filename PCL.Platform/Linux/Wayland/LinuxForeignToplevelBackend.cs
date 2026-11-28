using NWayland;
using NWayland.Protocols.Wayland;
using NWayland.Protocols.Wlr.WlrForeignToplevelManagementUnstableV1;
using PCL.Core.Logging;

namespace PCL.Platform;

/// <summary>wlroots 系合成器的 zwlr_foreign_toplevel_management_v1 后端。</summary>
internal sealed class LinuxForeignToplevelBackend
{
    private const string ModuleName = "WindowInterop";

    private readonly Dictionary<nint, WindowRecord> _byHandle = [];
    private readonly Lock _gate = new();

    private WlDisplay? _display;
    private bool _finished;
    private ZwlrForeignToplevelManagerV1? _manager;
    private ZwlrForeignToplevelManagerV1.Listener.Relay? _managerListener;
    private long _nextHandle;
    private WlRegistry.Listener.Relay? _registryListener;
    private bool _unavailable;

    public (nint Handle, string Title)? TryFindWindow()
    {
        lock (_gate)
        {
            try
            {
                if (!_EnsureInitialized())
                    return null;
                if (!_Pump())
                    return null;

                _CleanupClosed();
                return _FindBest();
            }
            catch (Exception ex)
            {
                _MarkUnavailable($"wlroots Wayland 窗口交互失败：{ex.Message}");
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
                record.Proxy.SetMaximized();
                _display?.Flush();
            }
            catch (Exception ex)
            {
                LogWrapper.Debug(ModuleName, $"最大化 wlroots Wayland 窗口失败：{ex.Message}");
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
                    "当前合成器未提供 zwlr_foreign_toplevel_manager_v1 协议，跳过 wlroots Wayland 窗口后端");
                return false;
            }

            return _Pump();
        }
        catch (Exception ex)
        {
            _MarkUnavailable($"无法连接 wlroots Wayland 窗口协议：{ex.Message}");
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
            || @interface != "zwlr_foreign_toplevel_manager_v1"
            || version < 1)
            return;

        _managerListener = new ZwlrForeignToplevelManagerV1.Listener.Relay
        {
            OnToplevel = _OnToplevel,
            OnFinished = _ => _finished = true
        };
        _manager = ZwlrForeignToplevelManagerV1.Bind(
            registry,
            name,
            Math.Min(version, 3),
            _managerListener);
    }

    private void _OnToplevel(
        ZwlrForeignToplevelManagerV1 sender,
        NewId<ZwlrForeignToplevelHandleV1, ZwlrForeignToplevelHandleV1.Listener> toplevel)
    {
        var record = new WindowRecord
        {
            Handle = unchecked((nint)Interlocked.Increment(ref _nextHandle))
        };
        var listener = new ZwlrForeignToplevelHandleV1.Listener.Relay
        {
            OnTitle = (_, title) => record.Title = title ?? string.Empty,
            OnAppId = (_, appId) => record.AppId = appId ?? string.Empty,
            OnDone = _ => record.Ready = true,
            OnClosed = _ => record.Closed = true
        };

        record.Proxy = toplevel.GetAndConsume(listener);
        _byHandle[record.Handle] = record;
    }

    private void _CleanupClosed()
    {
        var closed = _byHandle
            .Where(pair => pair.Value.Closed)
            .Select(pair => pair.Key)
            .ToArray();

        foreach (var handle in closed)
        {
            var record = _byHandle[handle];
            _byHandle.Remove(handle);
            record.Proxy.Dispose();
        }
    }

    private (nint Handle, string Title)? _FindBest()
    {
        WindowRecord? best = null;
        var bestScore = 0;

        foreach (var record in _byHandle.Values)
        {
            if (record.Closed || !record.Ready)
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
        {
            if (!_finished)
                return true;

            _MarkUnavailable("wlroots Wayland toplevel manager 已结束");
            return false;
        }

        _MarkUnavailable($"wlroots Wayland 事件循环失败：{display.GetError()}");
        return false;
    }

    private void _MarkUnavailable(string message)
    {
        if (_unavailable)
            return;
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
        _byHandle.Clear();
    }

    private sealed class WindowRecord
    {
        public required nint Handle { get; init; }
        public ZwlrForeignToplevelHandleV1 Proxy { get; set; } = null!;
        public string Title { get; set; } = string.Empty;
        public string AppId { get; set; } = string.Empty;
        public bool Ready { get; set; }
        public bool Closed { get; set; }
    }
}