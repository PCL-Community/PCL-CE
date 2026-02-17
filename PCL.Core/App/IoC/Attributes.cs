using System;

namespace PCL.Core.App.IoC;

#pragma warning disable CS9113 // Parameter is unread.
// ReSharper disable UnusedTypeParameter

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependencyCollectorAttribute<TDependency>(string identifier, AttributeTargets targets) : Attribute;

// ReSharper restore UnusedTypeParameter
#pragma warning restore CS9113 // Parameter is unread.
