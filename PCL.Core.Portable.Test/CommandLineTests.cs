// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Cli;
using PCL.Core.Serialization;

namespace PCL.Core.Portable.Test;

[TestClass]
public sealed class CommandLineTests
{
    private readonly CommandLine _model;
    private readonly string _correct = """
        bar []
        -> foo [
             --f: true
             --bar: foo
           ]
           -> bar [
                --foo: true
              ]
              -> foo [
                   --1234: 5678
                 ]
        """.Trim();

    public CommandLineTests()
    {
        IEnumerable<SubcommandDefinition> subcommands = [
            ("foo", [("bar", [("foo")])]),
            ("bar", [("foo")]),
        ];
        string[] testArgs = ["bar", "foo", "--f", "--bar", "foo", "bar", "--foo", "foo", "1234", "5678"];
        _model = CommandLine.Parse(testArgs, subcommands);
    }

    [TestMethod]
    public void Parse()
    {
        var modelStr = _model.ToString();
        Console.WriteLine(modelStr);
        Assert.AreEqual(_correct, modelStr);
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(PortableJson.SerializerOptions)
    {
        WriteIndented = true,
    };

    [TestMethod]
    public void JsonSerialization()
    {
        Console.WriteLine("Serialized JSON string:");
        var json = JsonSerializer.Serialize(_model, JsonSerializerOptions);
        Console.WriteLine(json);
        Console.WriteLine("Deserialization result:");
        var modelStr = JsonSerializer.Deserialize<CommandLine>(json, JsonSerializerOptions)?.ToString();
        Console.WriteLine(modelStr);
        Assert.AreEqual(_correct, modelStr);
    }
}
