// Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Application.Hosting.RuntimeExtensions;
using PCL.UI.Abstractions.Navigation;
using PCL.UI.Abstractions.Pages;

namespace PCL.Desktop.Hosting;

internal sealed class DesktopHostNavigation : IHostDynamicNavigation
{
    public static DesktopHostNavigation Instance { get; } = new();

    private INavigationRegistry? _navigation;
    private Action<string>? _navigate;

    public void Initialize(INavigationRegistry navigation) =>
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));

    public void Attach(Action<string> navigate) => _navigate += navigate;

    public void Detach(Action<string> navigate) => _navigate -= navigate;

    public IHostRegistration RegisterPage(HostPageRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        INavigationRegistry navigation = _navigation
            ?? throw new InvalidOperationException("Desktop plugin navigation is not initialized.");
        NavigationRouteId route = NavigationRouteId.Parse(registration.Route);
        navigation.AddPage(new NavigationPageDescriptor
        {
            Route = route,
            Title = registration.Title,
            Icon = registration.Icon,
            Order = registration.Order,
            Provider = new DelegatePageProvider((_, _) =>
            {
                object page = registration.CreatePage();
                if (page is not Control)
                    throw new InvalidOperationException(
                        $"Plugin page factory returned an unsupported type: {page?.GetType().FullName ?? "null"}");
                return new ValueTask<object>(page);
            })
        });
        return new Registration(
            registration.OwnerId + ":" + registration.OperationId,
            () => navigation.RemovePage(route));
    }

    public Task NavigateAsync(string route, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        cancellationToken.ThrowIfCancellationRequested();
        Action invoke = () => _navigate?.Invoke(route);
        if (Dispatcher.UIThread.CheckAccess())
            invoke();
        else
            Dispatcher.UIThread.Post(invoke);
        return Task.CompletedTask;
    }

    private sealed class Registration(string id, Action release) : IHostRegistration
    {
        private Action? _release = release;

        public string Id { get; } = id;

        public bool IsActive => _release is not null;

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _release, null)?.Invoke();
            return ValueTask.CompletedTask;
        }
    }
}
