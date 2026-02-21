using System;

namespace PCL.Core.App.IoC;

/// <summary>
/// 注册生命周期服务项，将由生命周期管理统一创建实例，然后在指定生命周期自动启动或加入等待手动启动列表。<br/>
/// 使用此注解的类型必须直接或间接实现 <see cref="ILifecycleService"/> 接口，否则将被忽略。
/// </summary>
/// <param name="startState">详见 <see cref="StartState"/></param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class LifecycleServiceAttribute(LifecycleState startState) : Attribute
{
    /// <summary>
    /// 指定该服务项应于何种生命周期状态启动。生命周期管理将在指定的状态按照 <see cref="Priority"/> 自动启动服务项。
    /// </summary>
    public LifecycleState StartState { get; } = startState;

    /// <summary>
    /// 启动优先级。同一个生命周期状态有多个服务项需要启动时，将会按优先级数值<b>降序</b>启动，即数值越大越优先。<br/>
    /// 虽然这个值可以为任意 32 位整数，但是<b>非核心服务请勿使用较为极端的值，尤其是
    /// <c>int.MaxValue</c> <c>int.MinValue</c></b>，这可能导致一些核心服务的启动时机出现问题。
    /// </summary>
    public int Priority { get; init; } = 0;
}

/// <summary>
/// 标记一个注解，使其成为依赖收集器。<br/>
/// 使用被标记的注解标记的 <see langword="public"/> 类 与 <see langword="public"/> <see langword="static"/>
/// 方法/属性 会被作为依赖收集到一个统一的存储位置，并用于运行时的依赖注入。
/// </summary>
/// <param name="identifier">依赖标记</param>
/// <param name="targets">目标，仅支持类、方法、属性，可以使用 <c>|</c> 分隔以添加多个目标</param>
/// <typeparam name="TDependency">依赖类型</typeparam>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependencyCollectorAttribute<TDependency>(string identifier, AttributeTargets targets) : Attribute;

/// <summary>
/// 标记一个 partial 类，自动实现 <see cref="ILifecycleService"/> 接口，并基于其他有标记的方法生成
/// <see cref="ILifecycleService.StartAsync"/> 和 <see cref="ILifecycleService.StopAsync"/> 方法
/// </summary>
/// <param name="identifier">See <see cref="ILifecycleService.Identifier"/></param>
/// <param name="name">See <see cref="ILifecycleService.Name"/></param>
/// <param name="asyncStart">See <see cref="ILifecycleService.SupportAsync"/></param>
[AttributeUsage(AttributeTargets.Class)]
public sealed class LifecycleScopeAttribute(string identifier, string name, bool asyncStart = true) : Attribute;

/// <summary>
/// 标记一个 Start 方法，可以标记多个
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LifecycleStartAttribute : Attribute;

/// <summary>
/// 标记一个 Stop 方法，可以标记多个
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LifecycleStopAttribute : Attribute;

/// <summary>
/// 标记一个命令处理器，可以标记多个，将自动识别和处理方法参数。<br/>
/// 若方法的第一个参数类型为 <see cref="PCL.Core.App.Cli.CommandLine"/> 则会传入指定指令的命令行模型。<br/>
/// 除命令行模型以外，任何参数在未显式声明默认值时均默认传入 <see langword="default"/> 值，请尽可能为所有参数提供显式默认值。<br/>
/// <b>NOTE</b>: 请勿使用异步实现，返回 Task 的方法将被直接忽略。<br/>
/// <b>NOTE</b>: 该方法可能在任意线程被调用，请注意线程上下文和同步问题。
/// <p/>示例：
/// <code>
/// [LifecycleCommandHandler("foo")]
/// private static void _FooHandler(CommandLine model, string bar, bool flag) {
///     // process logic...
///     // argument example: foo --flag --bar blablabla
/// }
/// </code>
/// </summary>
/// <param name="command">命令名</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LifecycleCommandHandlerAttribute(string command) : Attribute;

/// <summary>
/// 标记一个依赖注入入口，可以标记多个。
/// <p/>示例：
/// <code>
/// [LifecycleDependencyInjection("some-property", AttributeTargets.Property)]
/// private static void _LoadProperties(ImmutableList&lt;(PropertyAccessor&lt;string&gt; prop, string name)&gt; items)
/// {
///     // process logic...
/// }
/// [LifecycleDependencyInjection("some-method", AttributeTargets.Method)]
/// private static void _LoadMethods(ImmutableList&lt;(Action method, string name)&gt; items)
/// {
///     // process logic...
/// }
/// </code>
/// </summary>
/// <param name="identifier">依赖标识符</param>
/// <param name="targets">依赖类型，可以使用 <c>|</c> 连接多个</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LifecycleDependencyInjectionAttribute(string identifier, AttributeTargets targets) : Attribute;
