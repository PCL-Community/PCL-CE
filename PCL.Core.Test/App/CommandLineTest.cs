using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.App.Cli;

namespace PCL.Core.Test.App;

[TestClass]
public sealed class CommandLineTest
{
    private readonly CommandLine _model;

    public CommandLineTest()
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
        var correct = """
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
        Assert.AreEqual(correct, modelStr);
    }

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        IndentSize = 4,
    };

    [TestMethod]
    public void JsonSerialization()
    {
        var json = JsonSerializer.Serialize(_model, _jsonSerializerOptions);
        Console.WriteLine(json);
    }
}
