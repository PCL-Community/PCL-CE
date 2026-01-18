using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using PCL.Core.App.Configuration.NTraffic;
using PCL.Core.Logging;
using PCL.Core.UI;
using PCL.Core.Utils.Threading;

namespace PCL.Core.App.Configuration.Impl;

public sealed class FileTrafficCenter(IKeyValueFileProvider provider) : AsyncTrafficCenter(1)
{
    private const string LogModule = "Config";

    public IKeyValueFileProvider Provider { get; } = provider;

    private readonly AsyncDebounce _saveDebounce = new()
    {
        Delay = TimeSpan.FromSeconds(10),
        ScheduledTask = () =>
        {
            try
            {
                LogWrapper.Trace(LogModule, $"正在保存 {provider.FilePath}");
                provider.Sync();
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "Config", "配置文件保存失败");
                const string hint = "保存配置文件时出现问题，请尽快重启启动器，" +
                    "否则可能出现状态不一致的情况，严重时会导致启动器崩溃。";
                MsgBoxWrapper.Show(hint, "配置文件保存失败", MsgBoxTheme.Error);
            }
            return Task.CompletedTask;
        }
    };

    protected override bool OnAsyncCheck<TInput, TOutput>(TrafficEventArgs<TInput, TOutput> e)
        => e.Access == TrafficAccess.Write;

    protected override void OnTrafficSync<TInput, TOutput>(PreviewTrafficEventArgs<TInput, TOutput> e)
    {
        if (e.Access != TrafficAccess.Read) return;
        if (CheckKey(e, out var key)) return;
        // 获取值 / 检查存在性
        var exists = Provider.Exists(key);
        if (e is { HasInitialOutput: true, Output: bool }) e.SetOutput(exists);
        else if (exists) e.SetOutput(Provider.Get<TOutput>(key));
    }

    protected override async Task OnTrafficAsync<TInput, TOutput>(PreviewTrafficEventArgs<TInput, TOutput> e)
    {
        if (e.Access != TrafficAccess.Write) return;
        if (CheckKey(e, out var key)) return;
        // 设置值 / 删除值
        if (e.HasOutput) Provider.Set(key, e.Output);
        else Provider.Remove(key);
        // 延迟保存
        await _saveDebounce.Reset();
    }

    /// <summary>
    /// 检查事件参数所含 key 的合法性。若合法，返回 <c>false</c>，输出 key 的值，否则返回 <c>true</c>。
    /// </summary>
    public static bool CheckKey<TInput, TOutput>(TrafficEventArgs<TInput, TOutput> e, [NotNullWhen(false)] out string? key)
    {
        if (e is { HasInput: true, Input: string input })
        {
            key = input;
            return false;
        }
        key = null;
        return true;
    }

    protected override void OnStop()
    {
        // 停止延时器并保存文件
        var running = _saveDebounce.IsCurrentTaskRunning;
        _saveDebounce.Dispose();
        if (!running) Provider.Sync();
    }
}
