using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace PCL.Core.App.IoC;

public abstract class DependencyGroup
{
    public abstract void InvokeInjection(Delegate injection);
}

public class DependencyGroup<TValue> : DependencyGroup
{
    public required ImmutableList<TValue> Items { get; init; }
    
    public override void InvokeInjection(Delegate injection)
    {
        if (injection is Action<ImmutableList<TValue>> action) action(Items);
        else if (injection is Func<ImmutableList<TValue>, Task> awaitableAction) awaitableAction(Items).Wait(); 
        else throw new InvalidCastException($"Injection point signature mismatch, must be: void/Task (ImmutableList<{typeof(TValue).Name}>)");
    }
}

public class DependencyGroup<TValue, TArguments> : DependencyGroup
{
    public required ImmutableList<(TValue value, TArguments args)> Items { get; init; }
    
    public override void InvokeInjection(Delegate injection)
    {
        if (injection is Action<ImmutableList<(TValue, TArguments)>> action) action(Items);
        else if (injection is Func<ImmutableList<(TValue, TArguments)>, Task> awaitableAction) awaitableAction(Items).Wait(); 
        else throw new InvalidCastException($"Injection point signature mismatch, must be: void/Task (ImmutableList<({typeof(TValue).Name}, {typeof(TArguments).Name})>)");
    }
}
