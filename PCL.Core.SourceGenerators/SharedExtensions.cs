using Microsoft.CodeAnalysis.CSharp;

namespace PCL.Core.SourceGenerators;

public static class SharedExtensions
{
    extension(string str)
    {
        public string ToLiteral() => SymbolDisplay.FormatLiteral(str, true);
    }
}
