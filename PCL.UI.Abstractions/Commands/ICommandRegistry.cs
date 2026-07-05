// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.UI.Abstractions.Commands;

public interface ICommandContext
{
    IServiceProvider Services { get; }

    object? Parameter { get; }
}

public sealed record CommandDescriptor(
    CommandId Id,
    string Title,
    Func<ICommandContext, CancellationToken, ValueTask> ExecuteAsync,
    string? Description = null);

public interface ICommandRegistry
{
    IReadOnlyList<CommandDescriptor> Commands { get; }

    void AddCommand(CommandDescriptor descriptor);

    bool RemoveCommand(CommandId id);

    bool TryGetCommand(CommandId id, out CommandDescriptor descriptor);
}

public sealed class CommandRegistry : ICommandRegistry
{
    private readonly List<CommandDescriptor> _commands = [];
    private readonly Dictionary<string, CommandDescriptor> _commandMap = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<CommandDescriptor> _snapshot = Array.Empty<CommandDescriptor>();

    public IReadOnlyList<CommandDescriptor> Commands => _snapshot;

    public void AddCommand(CommandDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.IsNullOrWhiteSpace(descriptor.Id.Value))
            throw new ArgumentException("命令 ID 不能为空。", nameof(descriptor));
        if (string.IsNullOrWhiteSpace(descriptor.Title))
            throw new ArgumentException("命令标题不能为空。", nameof(descriptor));
        if (!_commandMap.TryAdd(descriptor.Id.Value, descriptor))
            throw new InvalidOperationException($"命令已注册：{descriptor.Id}");

        _commands.Add(descriptor);
        RefreshSnapshot();
    }

    public bool RemoveCommand(CommandId id)
    {
        if (string.IsNullOrWhiteSpace(id.Value) || !_commandMap.Remove(id.Value))
            return false;

        int index = _commands.FindIndex(command => command.Id.Equals(id.Value));
        if (index < 0)
            return false;

        _commands.RemoveAt(index);
        RefreshSnapshot();
        return true;
    }

    public bool TryGetCommand(CommandId id, out CommandDescriptor descriptor)
    {
        if (!string.IsNullOrWhiteSpace(id.Value) && _commandMap.TryGetValue(id.Value, out descriptor!))
            return true;

        descriptor = null!;
        return false;
    }

    private void RefreshSnapshot() =>
        _snapshot = _commands.ToArray();
}
