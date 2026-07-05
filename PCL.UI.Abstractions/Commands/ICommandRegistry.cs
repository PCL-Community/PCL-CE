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

    public IReadOnlyList<CommandDescriptor> Commands => _commands.ToArray();

    public void AddCommand(CommandDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (string.IsNullOrWhiteSpace(descriptor.Id.Value))
            throw new ArgumentException("命令 ID 不能为空。", nameof(descriptor));
        if (string.IsNullOrWhiteSpace(descriptor.Title))
            throw new ArgumentException("命令标题不能为空。", nameof(descriptor));
        if (_commands.Any(command => command.Id.Equals(descriptor.Id.Value)))
            throw new InvalidOperationException($"命令已注册：{descriptor.Id}");

        _commands.Add(descriptor);
    }

    public bool RemoveCommand(CommandId id)
    {
        int index = _commands.FindIndex(command => command.Id.Equals(id.Value));
        if (index < 0)
            return false;

        _commands.RemoveAt(index);
        return true;
    }

    public bool TryGetCommand(CommandId id, out CommandDescriptor descriptor)
    {
        CommandDescriptor? match = _commands.FirstOrDefault(command => command.Id.Equals(id.Value));
        descriptor = match!;
        return match is not null;
    }
}
