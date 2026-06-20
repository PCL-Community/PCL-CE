// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace PCL.Core.App.Cli;

public class SubcommandDefinition
{
    public required string CommandText { get; init; }

    public required IEnumerable<SubcommandDefinition> Subcommands { private get; init; }

    public IReadOnlyDictionary<string, SubcommandDefinition> SubcommandMap
    {
        get
        {
            if (field is not null) return field;
            var map = new Dictionary<string, SubcommandDefinition>();
            foreach (var c in Subcommands) map[c.CommandText] = c;
            return field = map.AsReadOnly();
        }
    } = null!;

    public bool Contains(string subcommandText)
    {
        return SubcommandMap.ContainsKey(subcommandText);
    }

    public static implicit operator SubcommandDefinition((string commandText, IEnumerable<SubcommandDefinition> subcommands) tuple)
    {
        return new SubcommandDefinition
        {
            CommandText = tuple.commandText,
            Subcommands = tuple.subcommands
        };
    }

    public static implicit operator SubcommandDefinition(string commandText)
    {
        return new SubcommandDefinition
        {
            CommandText = commandText,
            Subcommands = []
        };
    }
}
