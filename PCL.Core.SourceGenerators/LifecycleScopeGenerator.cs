using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PCL.Core.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public class LifecycleScopeGenerator : IIncrementalGenerator
{
    private const string AppNamespace = "PCL.Core.App";
    private const string IocNamespace = $"{AppNamespace}.IoC";
    private const string ScopeAttributeType = $"{IocNamespace}.LifecycleScopeAttribute";

    private const string StartMethodAttributeType = $"{IocNamespace}.LifecycleStartAttribute";
    private const string StopMethodAttributeType = $"{IocNamespace}.LifecycleStopAttribute";
    private const string CommandHandlerMethodAttributeType = $"{IocNamespace}.LifecycleCommandHandlerAttribute";
    private const string DependencyInjectionMethodAttributeType = $"{IocNamespace}.LifecycleDependencyInjectionAttribute";

    private static readonly HashSet<string> _MethodAttributeTypes = [
        StartMethodAttributeType, StopMethodAttributeType,
        CommandHandlerMethodAttributeType, DependencyInjectionMethodAttributeType
    ];

    private record ScopeMethodModel
    {
        public string MethodName { get; init; } = null!;
        public bool Awaitable { get; init; }
    }

    private record StartMethodModel : ScopeMethodModel;

    private record StopMethodModel : ScopeMethodModel;

    private record CommandHandlerMethodModel(
        string Command,
        bool HasCommandModelArg,
        bool HasIsCallbackArg,
        (string Name, string TypeName, bool hasDefaultValue, object? DefaultValue)[] SplitArgs
    ) : ScopeMethodModel;

    private record DependencyInjectionMethodModel(
        string Identifier,
        int Targets,
        string ParameterType
    ) : ScopeMethodModel;

    private class ScopeModel
    {
        public string Namespace { get; init; } = null!;
        public string TypeName { get; init; } = null!;
        public string QualifiedTypeName => $"{Namespace}.{TypeName}";
        public string Identifier { get; init; } = null!;
        public string Name { get; init; } = null!;
        public bool SupportAsync { get; init; }
        public List<ScopeMethodModel> Methods { get; } = [];
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Debugger.Launch();
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
                    attrTypeName = data.AttributeClass?.GetSimplifiedTypeName();
                    return attrTypeName != null && _MethodAttributeTypes.Contains(attrTypeName);
                });
                if (attr == null) continue;
                var methodName = method.Name;
                var awaitable = method.ReturnType.GetSimplifiedTypeName() == "System.Threading.Tasks.Task";
                ScopeMethodModel? methodModel = attrTypeName switch
                {
                    StartMethodAttributeType => new StartMethodModel { MethodName = methodName, Awaitable = awaitable },
                    StopMethodAttributeType => new StopMethodModel { MethodName = methodName, Awaitable = awaitable },
                    CommandHandlerMethodAttributeType => GetCommandHandlerMethodModel(),
                    DependencyInjectionMethodAttributeType => GetDependencyInjectionMethodModel(),
                    _ => null
                };
                if (methodModel != null) model.Methods.Add(methodModel);
                continue;
                CommandHandlerMethodModel? GetCommandHandlerMethodModel()
                {
                    if (awaitable) return null;
                    var command = attr.ConstructorArguments[0].Value!.ToString();
                    var paraArray = method.Parameters;
                    var skip = 0;
                    var hasCommandModelArg = paraArray.Length > 0
                        && paraArray[0].Type.GetSimplifiedTypeName() == "PCL.Core.App.Cli.CommandLine";
                    if (hasCommandModelArg) skip++;
                    var hasIsCallbackArgIndex = hasCommandModelArg ? 1 : 0;
                    var hasIsCallbackArg = paraArray.Length > hasIsCallbackArgIndex
                        && paraArray[hasIsCallbackArgIndex].Type.SpecialType == SpecialType.System_Boolean
                        && paraArray[hasIsCallbackArgIndex].Name == "isCallback";
                    if (hasIsCallbackArg) skip++;
                    var splitArgs = (
                        from para in paraArray.Skip(skip)
                        let name = para.Name
                        let typeName = para.Type.GetFullyQualifiedName()
                        let hasDefaultValue = para.HasExplicitDefaultValue
                        select (name, typeName, hasDefaultValue, hasDefaultValue ? para.ExplicitDefaultValue : null)
                    ).ToArray();
                    return new CommandHandlerMethodModel(command, hasCommandModelArg, hasIsCallbackArg, splitArgs)
                    {
                        MethodName = methodName,
                        Awaitable = false
                    };
                }
                DependencyInjectionMethodModel? GetDependencyInjectionMethodModel()
                {
                    var args = attr.ConstructorArguments;
                    var identifier = args[0].Value!.ToString();
                    var targets = (int)args[1].Value!;
                    if (method.Parameters.FirstOrDefault() is not { } param) return null;
                    var paramType = param.Type.GetFullyQualifiedName();
                    return new DependencyInjectionMethodModel(identifier, targets, paramType)
                    {
                        MethodName = methodName,
                        Awaitable = awaitable
                    };
                }
            }
            spc.AddSource($"{model.QualifiedTypeName}.g.cs", _GenerateScopeSource(model));
        }
    }

    private static readonly HashSet<Type> _TypesIncludingInStartMethod = [
        typeof(StartMethodModel),
        typeof(CommandHandlerMethodModel),
        typeof(DependencyInjectionMethodModel),
        typeof(CommandHandlerMethodModel),
    ];

    private static string _GenerateScopeSource(ScopeModel model)
    {
        var sb = new StringBuilder();

        // head
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// 此文件由 Source Generator 自动生成，请勿手动修改");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine($"using {AppNamespace};");
        sb.AppendLine($"using {IocNamespace};");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {model.Namespace};");
        sb.AppendLine();

        // basic structure
        sb.AppendLine($"partial class {model.TypeName} : ILifecycleService");
        sb.AppendLine("{");
        sb.AppendLine($"    public string Identifier => {model.Identifier.ToLiteral()};");
        sb.AppendLine($"    public string Name => {model.Name.ToLiteral()};");
        sb.AppendLine($"    public bool SupportAsync => {(model.SupportAsync ? "true" : "false")};");
        sb.AppendLine();
        sb.AppendLine("    private static LifecycleContext Context { get => field ?? throw new InvalidOperationException(\"Not initialized\"); set; } = null!;");
        sb.AppendLine("    private static ILifecycleService Service => Context.ServiceInstance;");
        sb.AppendLine($"    public {model.TypeName}() {{ Context = Lifecycle.GetContext(this); }}");
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
        else if (model is CommandHandlerMethodModel argModel)
        {
            var actionParamModel = argModel.HasCommandModelArg ? "model" : "_";
            var actionParamIsCallback = argModel.HasIsCallbackArg ? "isCallback" : "_";
            yield return $"Essentials.StartupService.TryHandleCommand(" +
                $"{argModel.Command.ToLiteral()}, ({actionParamModel}, {actionParamIsCallback}) => {{";
            var argTexts = new List<string>();
            if (argModel.HasCommandModelArg) argTexts.Add(actionParamModel);
            if (argModel.HasIsCallbackArg) argTexts.Add(actionParamIsCallback);
            foreach (var (name, typeName, hasDefaultValue, defaultValue) in argModel.SplitArgs)
            {
                var existsText = "exists_" + name;
                var isTypeMatchText = "isTypeMatch_" + name;
                var valueText = "value_" + name;
                yield return $"    var ({(hasDefaultValue ? existsText : "_")}, {isTypeMatchText}) = model.TryGetArgumentValue<{typeName}>(\"{name}\", out var {valueText});";
                yield return $"    if (!{isTypeMatchText}) throw new InvalidCastException(\"Argument type mismatch\");";
                argTexts.Add(hasDefaultValue ? $"{existsText} ? {valueText} : {defaultValue.ToPrimitive() ?? "default"}" : valueText);
            }
            yield return MethodInvoke("    ", argTexts);
            yield return "}, true);";
        }
        else if (model is DependencyInjectionMethodModel diModel)
        {
            var awaitable = diModel.Awaitable;
            if (awaitable) yield return "await Task.Run(() => {";
            var indentStr = awaitable ? "    " : "";
            if (awaitable) yield return $"{indentStr}Func<{diModel.ParameterType}, Task>";
            else yield return $"{indentStr}Action<{diModel.ParameterType}>";
            yield return $"{indentStr}    action = {diModel.MethodName};";
            yield return $"{indentStr}var result = DependencyGroups.InvokeInjection(action, " +
                         $"{diModel.Identifier.ToLiteral()}, " +
                         $"(AttributeTargets){diModel.Targets});";
            var logStr = diModel.Identifier + "@" + diModel.Targets;
            yield return $"{indentStr}if (result) Context.Trace(\"Dependency injection success: {logStr}\");";
            yield return $"{indentStr}else Context.Warn(\"Dependency injection failed: {logStr}\");";
            if (awaitable) yield return "});";
        }
        yield break;
        string MethodInvoke(string prefix = "", params IEnumerable<string> args)
            => $"{prefix}{(model.Awaitable ? "await " : "")}{model.MethodName}({string.Join(", ", args)});";
    }
}
