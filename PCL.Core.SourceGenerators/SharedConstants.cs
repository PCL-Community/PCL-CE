namespace PCL.Core.SourceGenerators;

public static class SharedConstants
{
    public const string AppNamespace = "PCL.Core.App";
    public const string IocNamespace = $"{AppNamespace}.IoC";
    public const string DependencyCollectorAttribute = $"{IocNamespace}.DependencyCollectorAttribute";
    public const string DependencyInjectionPointAttribute = $"{IocNamespace}.DependencyInjectionPointAttribute";
    public const string LifecycleScopeAttribute = $"{IocNamespace}.LifecycleScopeAttribute";
    public const string LifecycleStartAttribute = $"{IocNamespace}.LifecycleStartAttribute";
    public const string LifecycleStopAttribute = $"{IocNamespace}.LifecycleStopAttribute";
    public const string LifecycleCommandHandlerAttribute = $"{IocNamespace}.LifecycleCommandHandlerAttribute";
    public const string LifecycleDependencyInjectionAttribute = $"{IocNamespace}.LifecycleDependencyInjectionAttribute";
}
