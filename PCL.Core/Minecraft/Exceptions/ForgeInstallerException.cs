using System;
using System.Text;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.Exceptions;

public class ForgeInstallerException : Exception
{
    public IEnumerable<string>? Logs { get; init; }
    
    public ForgeInstallerException(){}

    public ForgeInstallerException(string message) : base(message){}

    public ForgeInstallerException(string message,Exception? inner):base(message,inner){}
    
    public ForgeInstallerException(IEnumerable<string> logs):this("An error throws when execute install.")
    {
        Logs = logs;
    }
    
    public override string ToString()
    {
        var details = base.ToString();
        var builder = new StringBuilder(details);
        builder.Append($"{Environment.NewLine}--- Installer Logs --- {Environment.NewLine}Output:");
        builder.AppendJoin(Environment.NewLine, Logs ?? ["Nothing"]);
        return builder.ToString();
    }
}