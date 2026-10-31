using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using PCL.Core.Logging;
using PCL.Core.UI.Threading;

namespace PCL.Core.UI.Hints;

/// <summary>提示调度：负责去重、排队、容量控制与派发，界面层只实现 <see cref="IHintPresenter" />。</summary>
public sealed class HintService
{
    public static HintService Instance { get; } = new();

    /// <summary>同时显示的最大条数，超出时关闭最早的一条。</summary>
    private const int MaxActive = 5;

    private readonly List<HintViewModel> _pending = [];
    private readonly List<HintViewModel> _active = [];
    private readonly Lock _lock = new();

    /// <summary>界面层注册的呈现器。</summary>
    public IHintPresenter? Presenter { get; set; }

    public int ActiveCount
    {
        get
        {
            lock (_lock) return _active.Count;
        }
    }

    public int PendingCount
    {
        get
        {
            lock (_lock) return _pending.Count;
        }
    }

    /// <summary>显示一条提示。</summary>
    public static void Show(HintRequest request) => Instance.Push(request);

    /// <summary>关闭全部提示。</summary>
    public static void HideAll() => Instance.ClearAll();

    /// <summary>排队一条提示。重复（正文与类型相同）的待显示提示会被忽略，已显示的会被重新强调。</summary>
    public void Push(HintRequest request)
    {
        var message = Normalize(request.Message);
        if (message.Length == 0) return;

        lock (_lock)
        {
            if (_pending.Any(hint => hint.Message == message && hint.Kind == request.Kind)) return;
            _pending.Add(new HintViewModel(request with { Message = message }));
        }

        UiDispatcher.Post(Pump);
    }

    /// <summary>关闭全部提示。</summary>
    public void ClearAll()
    {
        List<HintViewModel> active;
        lock (_lock)
        {
            _pending.Clear();
            active = [.. _active];
        }

        UiDispatcher.Post(() =>
        {
            foreach (var hint in active) Presenter?.Dismiss(hint);
            Presenter?.DismissAll();
        });
    }

    /// <summary>界面层在提示关闭后回调。</summary>
    internal void OnHintClosed(HintViewModel hint)
    {
        lock (_lock) _active.Remove(hint);
    }

    /// <summary>把待显示的提示交给呈现器。</summary>
    public void Pump()
    {
        while (true)
        {
            HintViewModel next;
            int activeCount;

            lock (_lock)
            {
                if (_pending.Count == 0) return;

                var duplicate = _active.FirstOrDefault(hint =>
                    !hint.IsDismissing && hint.Message == _pending[0].Message && hint.Kind == _pending[0].Kind);
                if (duplicate is not null)
                {
                    _pending.RemoveAt(0);
                    UiDispatcher.Post(duplicate.RequestEmphasize);
                    continue;
                }

                activeCount = _active.Count(hint => !hint.IsDismissing);
                if (activeCount >= MaxActive)
                {
                    var oldest = _active.FirstOrDefault(hint => !hint.IsDismissing);
                    _pending.RemoveAt(0);
                    if (oldest is not null) UiDispatcher.Post(oldest.Dismiss);
                    return;
                }

                next = _pending[0];
                _pending.RemoveAt(0);
                _active.Add(next);
            }

            var presenter = Presenter;
            if (presenter is null)
            {
                LogWrapper.Warn((Exception?)null, "未注册提示呈现器，提示被丢弃：" + next.Message);
                lock (_lock) _active.Remove(next);
                next.Close();
                continue;
            }

            if (!presenter.IsAvailable)
            {
                LogWrapper.Warn((Exception?)null, "界面暂不可用，提示被丢弃：" + next.Message);
                lock (_lock) _active.Remove(next);
                next.Close();
                continue;
            }

            if (next.Log) LogWrapper.Info("[UI] 弹出提示：" + next.Message);

            try
            {
                presenter.Present(next);
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "显示提示失败：" + next.Message);
                lock (_lock) _active.Remove(next);
                next.Close();
            }
        }
    }

    private static string Normalize(string? message) =>
        (message ?? "").Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ").Trim();
}
