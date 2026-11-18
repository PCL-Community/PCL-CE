using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PCL.Core.SourceGenerators;

/// <summary>为 <see cref="LocalizedPropertyAttribute" /> 标注的 partial 属性生成本地化 getter。</summary>
[Generator(LanguageNames.CSharp)]
public sealed class LocalizedPropertyGenerator : IIncrementalGenerator
{
    private const string AttributeName =
        "PCL.Core.App.Localization.LocalizedPropertyAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var properties = context
            .SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeName,
                static (node, _) => node is PropertyDeclarationSyntax,
                static (ctx, _) => (IPropertySymbol)ctx.TargetSymbol)
            .Where(static property => property.IsPartialDefinition);

        context.RegisterSourceOutput(
            properties,
            static (spc, property) =>
            {
                var attribute = property
                    .GetAttributes()
                    .FirstOrDefault(item =>
                        item.AttributeClass?.ToDisplayString() == AttributeName);
                if (attribute is null || attribute.ConstructorArguments.Length == 0)
                    return;

                var key = attribute.ConstructorArguments[0].Value as string;
                if (string.IsNullOrWhiteSpace(key)) return;

                var type = property.ContainingType;
                if (!type.IsPartial()) return;

                var source = new StringBuilder();
                var indent = type.GenerateTypeHeader(source);
                source
                    .Append(' ', indent * 4)
                    .Append("public partial ")
                    .Append(property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                    .Append(' ')
                    .Append(property.Name)
                    .AppendLine();
                source.Append(' ', indent * 4).AppendLine("{");
                source
                    .Append(' ', (indent + 1) * 4)
                    .Append("get => global::PCL.Core.App.Localization.Lang.Text(")
                    .Append(key.ToLiteral())
                    .AppendLine(");");
                source.Append(' ', indent * 4).AppendLine("}");
                for (var i = indent - 1; i >= 0; i--)
                    source.Append(' ', i * 4).AppendLine("}");

                spc.AddSource(
                    $"{type.Name}.{property.Name}.Localized.g.cs",
                    source.ToString());
            });
    }
}