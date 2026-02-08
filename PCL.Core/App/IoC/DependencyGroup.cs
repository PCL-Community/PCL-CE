using System;
using System.Collections.Immutable;

namespace PCL.Core.App.IoC;

public abstract class DependencyGroup
{
}

public class DependencyGroup<TValue> : DependencyGroup
{
    public required ImmutableList<TValue> Items { get; init; }
}

public class DependencyGroup<TValue, TArguments> : DependencyGroup
{
    public required ImmutableList<(TValue value, TArguments args)> Items { get; init; }
}
