namespace PCL;

public class PageCrashRightBase : MyPageRight, IRefreshable
{
    private string? _lastSessionId;

    public PageCrashRightBase()
    {
        PageEnter += () => RefreshIfNeeded(false);
        Loaded += (_, _) => RefreshIfNeeded(false);
        Unloaded += (_, _) => MinecraftCrashSessionStore.SessionChanged -= _OnSessionChanged;
        MinecraftCrashSessionStore.SessionChanged += _OnSessionChanged;
    }

    public void Refresh()
    {
        RefreshIfNeeded(true);
    }

    protected void RefreshIfNeeded(bool force)
    {
        var session = MinecraftCrashSessionStore.TryGetCurrent();
        var sessionId = session?.Id;
        if (!force && _lastSessionId == sessionId) return;
        _lastSessionId = sessionId;
        Render(session);
    }

    protected virtual void Render(MinecraftCrashSession? session)
    {
    }

    private void _OnSessionChanged()
    {
        ModBase.RunInUi(() => RefreshIfNeeded(true));
    }
}