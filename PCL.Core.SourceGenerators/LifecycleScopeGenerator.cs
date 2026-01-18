using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PCL.Core.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public class LifecycleScopeGenerator : IIncrementalGenerator
{
    private const string ScopeAttributeType = "PCL.Core.App.LifecycleScopeAttribute";

    private const string StartMethodAttributeType = "PCL.Core.App.LifecycleStartAttribute";
    private const string StopMethodAttributeType = "PCL.Core.App.LifecycleStopAttribute";
    private const string ArgumentHandlerMethodAttributeType = "PCL.Core.App.LifecycleArgumentHandlerAttribute";

    private static readonly HashSet<string> _MethodAttributeTypes = [
        StartMethodAttributeType, StopMethodAttributeType,
        ArgumentHandlerMethodAttributeType
    ];

    private class ScopeMethodModel
    {
        public string MethodName { get; set; } = null!;
        public bool Awaitable { get; set; }
    }

    private class StartMethodModel : ScopeMethodModel;

    private class StopMethodModel : ScopeMethodModel;

    private class ArgumentHandlerMethodModel : ScopeMethodModel
    {
        public string ArgumentName { get; set; } = null!;
        public string ArgumentQualifiedTypeName { get; set; } = null!;
        public string ArgumentDefaultValue { get; set; } = null!;
    }

    private class ScopeModel
    {
        public string Namespace { get; set; } = null!;
        public string TypeName { get; set; } = null!;
        public string QualifiedTypeName => $"{Namespace}.{TypeName}";
        public string Identifier { get; set; } = null!;
        public string Name { get; set; } = null!;
        public bool SupportAsync { get; set; }
        public List<ScopeMethodModel> Methods { get; } = [];
    }

    private static readonly SymbolDisplayFormat _QualifiedTypeNameFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.CollapseTupleTypes |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes,
        genericsOptions: SymbolDisplayGenericsOptions.None
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var candidates = context.SyntaxProvider.ForAttributeWithMetadataName(
            ScopeAttributeType,
            static (node, _) => node is ClassDeclarationSyntax syntax && syntax.Modifiers.Any(m => m.ValueText == "partial"),
            static (INamedTypeSymbol, ScopeModel)? (ctx, _) =>
            {
                if (ctx.TargetSymbol is not INamedTypeSymbol typeSymbol) return null;
                var attr = ctx.Attributes[0];
                var args = attr.ConstructorArguments;
                var scopeIdentifier = args[0].Value!.ToString();
                var scopeName = args[1].Value!.ToString();
                var scopeAsyncStart = true;
                if (args.Length > 2 && args[2].Value is bool v) scopeAsyncStart = v;
                var ns = typeSymbol.ContainingNamespace.ToDisplayString();
                var typeName = typeSymbol.Name;
                return (typeSymbol, new ScopeModel
                {
                    Namespace = ns,
                    TypeName = typeName,
                    Identifier = scopeIdentifier,
                    Name = scopeName,
                    SupportAsync = scopeAsyncStart
                });
            }
        ).Where(static i => i != null).Select(static (i, _) => i.GetValueOrDefault());
        var collected = candidates.Collect();
        context.RegisterSourceOutput(collected, _CollectSources);
    }

    private static void _CollectSources(SourceProductionContext spc, ImmutableArray<(INamedTypeSymbol TypeSymbol, ScopeModel Model)> models)
    {
        foreach (var (symbol, model) in models)
        {
            model.Methods.Clear();
            foreach (var member in symbol.GetMembers())
            {
                if (member is not IMethodSymbol method) continue;
                var attrTypeName = string.Empty;
                var attr = method.GetAttributes().FirstOrDefault(data =>
                {
                    attrTypeName = data.AttributeClass?.ToDisplayString(_QualifiedTypeNameFormat);
                    return attrTypeName != null && _MethodAttributeTypes.Contains(attrTypeName);
                });
                if (attr == null) continue;
                var methodName = method.Name;
                var awaitable = method.ReturnType.ToDisplayString(_QualifiedTypeNameFormat) == "System.Threading.Tasks.Task";
                model.Methods.Add(attrTypeName switch
                {
                    StartMethodAttributeType => new StartMethodModel { MethodName = methodName, Awaitable = awaitable },
                    StopMethodAttributeType => new StopMethodModel { MethodName = methodName, Awaitable = awaitable },
                    ArgumentHandlerMethodAttributeType => GetArgumentHandlerMethodModel(),
                    _ => throw new ArgumentOutOfRangeException()
                });
                continue;
                ArgumentHandlerMethodModel GetArgumentHandlerMethodModel()
                {
                    var args = attr.ConstructorArguments;
                    var argumentName = args[0].Value!.ToString();
                    var argumentTypeName = attr.AttributeClass!.TypeArguments.First().ToDisplayString(_QualifiedTypeNameFormat);
                    var argumentDefaultValue = args.Length > 1 ? args[1].ToCSharpString() : $"new {argumentTypeName}()";
                    return new ArgumentHandlerMethodModel
                    {
                        MethodName = methodName,
                        Awaitable = awaitable,
                        ArgumentName = argumentName,
                        ArgumentDefaultValue = argumentDefaultValue,
                        ArgumentQualifiedTypeName = argumentTypeName
                    };
                }
            }
            spc.AddSource($"{model.QualifiedTypeName}.g.cs", _GenerateScopeSource(model));
        }
    }

    private static readonly HashSet<Type> _TypesIncludingInStartMethod = [
        typeof(StartMethodModel),
        typeof(ArgumentHandlerMethodModel)
    ];

    private static string _GenerateScopeSource(ScopeModel model)
    {
        var sb = new StringBuilder();

        // head
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// 此文件由 Source Generator 自动生成，请勿手动修改");
        sb.AppendLine();
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using PCL.Core.App;");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {model.Namespace};");
        sb.AppendLine();

        // basic structure
        sb.AppendLine($"partial class {model.TypeName} : ILifecycleService");
        sb.AppendLine("{");
        sb.AppendLine("    private static LifecycleContext? _context;");
        sb.AppendLine($"    private {model.TypeName}() {{ _context = Lifecycle.GetContext(this); }}");
        sb.AppendLine();
        sb.AppendLine($"    public string Identifier => {model.Identifier.ToLiteral()};");
        sb.AppendLine($"    public string Name => {model.Name.ToLiteral()};");
        sb.AppendLine($"    public bool SupportAsync => {(model.SupportAsync ? "true" : "false")};");
        sb.AppendLine("    private static LifecycleContext Context => _context!;");
        sb.AppendLine("    private static ILifecycleService Service => Context.ServiceInstance;");
        sb.AppendLine();

        // StopAsync() implementation
        sb.AppendLine("    public async Task StopAsync()");
        sb.AppendLine("    {");
        var stopCount = AppendMethodInvokes(2, model.Methods.Where(x => x is StopMethodModel));
        sb.AppendLine("    }");
        sb.AppendLine();

        // StartAsync() implementation
        sb.AppendLine("    public async Task StartAsync()");
        sb.AppendLine("    {");
        AppendMethodInvokes(2, model.Methods.Where(x => _TypesIncludingInStartMethod.Contains(x.GetType())));
        if (stopCount == 0) sb.AppendLine("        Context.DeclareStopped();");
        sb.AppendLine("    }");

        // structure tail
        sb.AppendLine("}");

        return sb.ToString();

        // method invokes implementation
        int AppendMethodInvokes(int indent, IEnumerable<ScopeMethodModel> models)
        {
            var count = 0;
            var indentStr = new string(' ', indent * 4);
            foreach (var methodModel in models)
            {
                count++;
                sb.Append(indentStr).AppendLine("{");
                foreach (var line in _EmitMethod(methodModel)) sb.Append(indentStr).Append("    ").AppendLine(line);
                sb.Append(indentStr).AppendLine("}");
            }
            return count;
        }
    }

    private static IEnumerable<string> _EmitMethod(ScopeMethodModel model)
    {
        if (model is StartMethodModel or StopMethodModel)
        {
            yield return MethodInvoke();
        }
        else if (model is ArgumentHandlerMethodModel argModel)
        {
            // TODO argument handler implementation
        }
        yield break;
        string MethodInvoke(params string[] args) => $"{(model.Awaitable ? "await " : "")}{model.MethodName}({string.Join(", ", args)});";
    }
}
