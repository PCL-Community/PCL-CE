using System;
using System.Collections.Generic;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App.Localization;
using PCL.Core.Platform;

namespace PCL.Core.ViewModel;

/// <summary>支持语言变化的可观察对象。语言变化时通知所有绑定重新求值，ViewModel 只需使用计算属性。</summary>
public abstract class LocalizedObservableObject : ObservableObject
{
    private static readonly List<WeakReference<LocalizedObservableObject>> _Instances = [];
    private static readonly Lock _HookLock = new();
    private static bool _isHooked;

    protected LocalizedObservableObject()
    {
        lock (_HookLock)
        {
            _HookLanguageChanged();
            _Instances.Add(new WeakReference<LocalizedObservableObject>(this));
        }
    }

    /// <summary>语言变化时由基类调用；需要局部刷新时可重写。</summary>
    protected virtual void OnLanguageChanged()
    {
        OnPropertyChanged(string.Empty);
    }

    private static void _HookLanguageChanged()
    {
        if (_isHooked) return;
        _isHooked = true;
        LocalizationService.LanguageChanged += _OnLanguageChanged;
    }

    private static void _OnLanguageChanged()
    {
        LocalizedObservableObject[] snapshot;
        lock (_HookLock)
        {
            var alive = new List<LocalizedObservableObject>(_Instances.Count);
            for (var i = _Instances.Count - 1; i >= 0; i--)
            {
                if (!_Instances[i].TryGetTarget(out var instance))
                {
                    _Instances.RemoveAt(i);
                    continue;
                }

                alive.Add(instance);
            }

            snapshot = [.. alive];
        }

        var dispatcher = ApplicationRuntime.GetService<IUiDispatcher>();
        var isUiThread = dispatcher is null || dispatcher.CheckAccess();
        foreach (var instance in snapshot)
            if (isUiThread)
                instance.OnLanguageChanged();
            else
                dispatcher?.Post(instance.OnLanguageChanged);
    }
}