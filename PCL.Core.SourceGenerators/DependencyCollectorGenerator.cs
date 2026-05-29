using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PCL.Core.SourceGenerators;

public readonly record struct CollectorInfo(
    INamedTypeSymbol CollectorAttrSymbol,
    ITypeSymbol DependencyType,
    string Identifier,
    AttributeTargets Targets
);

public readonly record struct DependencyMatchResult(
    ISymbol Target,
    AttributeTargets TargetType,
    AttributeData CollectorAttr,
    CollectorInfo Info
);

public readonly record struct InjectionPointInfo(
    IMethodSymbol Target,
    string Identifier
);

public readonly record struct InjectionPointMatchResult(
    InjectionPointInfo Info,
    ImmutableArray<DependencyMatchResult> Dependencies
);

[Generator(LanguageNames.CSharp)]
public sealed class DependencyCollectorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        const string collectorMarkupAttr = SharedConstants.DependencyCollectorAttribute;
        const string collectorMarkupAttrFull = $"{collectorMarkupAttr}`1";
        const string injectionPointAttr = SharedConstants.DependencyInjectionPointAttribute;
        
        // 收集被标记为 collector 的注解
        var collectorAttrs = context.SyntaxProvider
            .ForAttributeWithMetadataName(collectorMarkupAttrFull,
                predicate: static (node, _) => node is ClassDeclarationSyntax, 
                transform: static (ctx, _) =>
                {
                    if (ctx.TargetSymbol is not INamedTypeSymbol attr || !attr.IsAttribute()) return default;
                    var infos = new List<CollectorInfo>();
                    foreach (var attrData in ctx.Attributes)
                    {
                        var attrClass = attrData.AttributeClass;
                        if (attrClass is null || attrClass.GetSimplifiedTypeName() != collectorMarkupAttr) continue;
                        // 收集注解信息
                        var dependencyType = attrClass.TypeArguments.FirstOrDefault();
                        if (dependencyType is null) continue;
                        var ctorArgs = attrData.ConstructorArguments;
                        if (ctorArgs.Length < 2
                            || ctorArgs[0].Value is not string identifier
                            || ctorArgs[1].Value is not int targets)
                            continue;
                        infos.Add(new CollectorInfo(attr, dependencyType, identifier, (AttributeTargets)targets));
                    }
                    return new KeyValuePair<INamedTypeSymbol, List<CollectorInfo>>(attr, infos);
                })
            .Where(x => x.Key is not null)
            .Collect()
            // 此处合并到 dictionary 以优化后续查找性能
            .Select(static (pairs, _) =>
            {
                var dict = new Dictionary<INamedTypeSymbol, List<CollectorInfo>>(SymbolEqualityComparer.Default);
                foreach (var pair in pairs)
                {
                    if (dict.TryGetValue(pair.Key, out var list)) list.AddRange(pair.Value);
                    else dict[pair.Key] = pair.Value;
                }
                return dict.ToImmutableDictionary(SymbolEqualityComparer.Default);
            });
        
        // 收集所有带注解的 static member
        var potentialTargets = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) =>
            {
                // 仅支持 class, property, method
                if (node is not MemberDeclarationSyntax { AttributeLists.Count: > 0 } member) return false;
                if (node is ClassDeclarationSyntax) return true;
                if (node is PropertyDeclarationSyntax or MethodDeclarationSyntax
                    && member.Modifiers.Any(x => x.IsKind(SyntaxKind.StaticKeyword))) return true;
                return false;
            },
            transform: static (ctx, _) => ctx);
        
        // 筛选出被 collector 标记的 member
        var matches = potentialTargets.Combine(collectorAttrs)
            .SelectMany(static (pair, cancelToken) =>
            {
                var (ctx, validAttrs) = pair;
                // 从 syntax node 获取对应语义 symbol
                var symbol = ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, cancelToken);
                if (symbol is null) return [];
                // 确定目标类型
                AttributeTargets targetType = default;
                if (symbol is INamedTypeSymbol) targetType = AttributeTargets.Class;
                else if (symbol is IPropertySymbol) targetType = AttributeTargets.Property;
                else if (symbol is IMethodSymbol) targetType = AttributeTargets.Method;
                // 筛选目标所有符合条件的注解
                var results = new List<DependencyMatchResult>();
                foreach (var attrData in symbol.GetAttributes())
                {
                    var attr = attrData.AttributeClass;
                    if (attr is null) continue;
                    if (!validAttrs.TryGetValue(attr, out var infos)) continue;
                    results.AddRange(
                        from info in infos
                        where info.Targets.HasFlag(targetType)
                        select new DependencyMatchResult(symbol, targetType, attrData, info)
                    );
                }
                return results;
            })
            .Collect();

        // 收集被标记为注入点的方法
        var injectionPoints = context.SyntaxProvider
            .ForAttributeWithMetadataName(injectionPointAttr,
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: static (ctx, _) =>
                {
                    var method = (IMethodSymbol)ctx.TargetSymbol;
                    var attr = ctx.Attributes.First(x => x.AttributeClass?.GetSimplifiedTypeName() == injectionPointAttr);
                    var attrArgs = attr.ConstructorArguments;
                    var identifier = attrArgs[0].Value?.ToString();
                    return identifier is null ? default : new InjectionPointInfo(method, identifier);
                })
            .Where(x => x != default);

        // 将注入点与对应标记的依赖项关联
        var injectionPointMatches = injectionPoints.Combine(matches)
            .Select((item, _) =>
            {
                var point = item.Left;
                var deps = item.Right
                    .Where(x => x.Info.Identifier == point.Identifier)
                    .ToImmutableArray();
                return new InjectionPointMatchResult(point, deps);
            })
            .Collect();
        
        // 生成注入实现
        context.RegisterSourceOutput(injectionPointMatches, _GenerateDependencyInjectionMethods);

        // 保留旧生成模式以供旧组件兼容
        context.RegisterSourceOutput(matches, _GenerateDependencyGroup);
    }

    private static void _GenerateDependencyGroup(SourceProductionContext spc, ImmutableArray<DependencyMatchResult> matches)
    {
        var dependencyMap = new Dictionary<CollectorInfo, Dictionary<AttributeTargets, List<DependencyMatchResult>>>();
        foreach (var dep in matches)
        {
            var info = dep.Info;
            if (!dependencyMap.TryGetValue(info, out var map))
            {
                map = new Dictionary<AttributeTargets, List<DependencyMatchResult>>
                {
                    [AttributeTargets.Class] = [],
                    [AttributeTargets.Method] = [],
                    [AttributeTargets.Property] = []
                };
                dependencyMap[info] = map;
            }
            map[dep.TargetType].Add(dep);
        }

        var sb = new StringBuilder(1024);
        
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// 此文件由 Source Generator 自动生成，请勿手动修改");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Collections.Immutable;");
        sb.AppendLine();
        sb.AppendLine("namespace PCL.Core.App.IoC;");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("public static partial class DependencyGroups");
        sb.AppendLine("{");
        
        sb.AppendLine("    private static readonly Dictionary<string, Dictionary<AttributeTargets, DependencyGroup>> _GroupMap = new()");
        sb.AppendLine("    {");
        
        foreach (var (info, map) in dependencyMap
            .Select(x => (x.Key, x.Value)))
        {
            sb.Append("        [").Append(info.Identifier.ToLiteral()).AppendLine("] = new()");
            sb.AppendLine("        {");
            string? typeStr = null;
            string? argTypeList = null;
            foreach (var (target, deps) in map
                .Where(x => x.Value.Count > 0)
                .Select(x => (x.Key, x.Value)))
            {
                sb.Append("            [AttributeTargets.").Append(target).Append("] = new DependencyGroup<");
                typeStr ??= info.DependencyType.GetFullyQualifiedName();
                switch (target)
                {
                    case AttributeTargets.Class:
                        sb.Append("Action<").Append(typeStr).Append(">");
                        break;
                    case AttributeTargets.Method:
                        sb.Append(typeStr);
                        break;
                    case AttributeTargets.Property:
                        sb.Append("PropertyAccessor<").Append(typeStr).Append(">");
                        break;
                }
                argTypeList ??= ((Func<string>)(() =>
                {
                    var ctor = info.CollectorAttrSymbol.InstanceConstructors.FirstOrDefault();
                    if (ctor is null) return string.Empty;
                    var args = ctor.Parameters.Select(para => para.Type.GetFullyQualifiedName()).ToList();
                    var cnt = args.Count;
                    if (cnt == 0) return string.Empty;
                    if (cnt == 1) return args[0];
                    return "(" + string.Join(", ", args) + ")";
                }))();
                if (argTypeList != string.Empty) sb.Append(", ").Append(argTypeList);
                sb.AppendLine("> { Items = [");
                foreach (var dep in deps)
                {
                    sb.Append("                (");
                    var depRef = dep.Target.GetQualifiedSymbolName();
                    switch (target)
                    {
                        case AttributeTargets.Class:
                            sb.Append("static () => new ")
                              .Append(depRef).Append("()");
                            break;
                        case AttributeTargets.Method:
                            sb.Append(depRef);
                            break;
                        case AttributeTargets.Property:
                            sb.Append("new(getter: ");
                            var prop = (IPropertySymbol)dep.Target;
                            if (prop.IsWriteOnly) sb.Append("null");
                            else sb.Append("static () => ").Append(depRef);
                            sb.Append(", setter: ");
                            if (prop.IsReadOnly) sb.Append("null");
                            else sb.Append("static value => ").Append(depRef).Append(" = value");
                            sb.Append(")");
                            break;
                    }
                    if (argTypeList != string.Empty)
                    {
                        sb.Append(", ");
                        var args = dep.CollectorAttr.ConstructorArguments.Select(arg => arg.ToCSharpString()).ToList();
                        if (args.Count == 1) sb.Append(args[0]);
                        else sb.Append("(").Append(string.Join(", ", args)).Append(")");
                    }
                    sb.AppendLine("),");
                }
                sb.AppendLine("            ] },");
            }
            sb.AppendLine("        },");
        }
        
        sb.AppendLine("    };");
        sb.AppendLine("}");
        
        spc.AddSource("DependencyGroups.g.cs", sb.ToString());
    }

    private static void _GenerateDependencyInjectionMethods(SourceProductionContext spc, ImmutableArray<InjectionPointMatchResult> matches)
    {
        foreach (var match in matches)
        {
            var sb = new StringBuilder(1024);

            // file header
            sb.AppendLine("// 此文件由 Source Generator 自动生成，请勿手动修改");
            sb.AppendLine("// <auto-generated />");
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine($"using {SharedConstants.IocNamespace};");
            sb.AppendLine();
            sb.AppendLine("#nullable enable");
            sb.AppendLine();

            // type header
            var targetMethod = match.Info.Target;
            var indent = targetMethod.ContainingType.GenerateTypeHeader(sb);

            // method
            var indentStr = new string(' ', indent * 4);
            var targetMethodName = targetMethod.Name;
            var isStatic = targetMethod.IsStatic;
            var isAwaitable = targetMethod.IsAwaitable();
            sb.Append(indentStr).AppendLine("[global::System.CodeDom.Compiler.GeneratedCode(\"PCL.Core.SourceGenerators.DependencyCollectorGenerator\", \"1.0.0.0\")]");
            sb.Append(indentStr).AppendLine("[global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]");
            var idCode = match.Info.Identifier.SnakeIdToPascal();
            sb.Append(indentStr).Append("private ");
            if (isStatic) sb.Append("static ");
            sb.Append(isAwaitable ? "async Task " : "void ");
            sb.Append(targetMethodName).Append("_InvokeInjection_").Append(idCode).AppendLine("()");
            sb.Append(indentStr).AppendLine("{");
            foreach (var dep in match.Dependencies)
            {
                sb.Append(indentStr).Append("    ");
                if (isAwaitable) sb.Append("await ");
                sb.Append(targetMethodName).Append("(");
                var depRef = dep.Target.GetQualifiedSymbolName();
                switch (dep.TargetType)
                {
                    case AttributeTargets.Class:
                        sb.Append("static () => new ").Append(depRef).Append("()");
                        break;
                    case AttributeTargets.Method:
                        sb.Append(depRef);
                        break;
                    case AttributeTargets.Property:
                        sb.Append("new PropertyAccessor(getter: ");
                        var prop = (IPropertySymbol)dep.Target;
                        if (prop.IsWriteOnly) sb.Append("null");
                        else sb.Append("static () => ").Append(depRef);
                        sb.Append(", setter: ");
                        if (prop.IsReadOnly) sb.Append("null");
                        else sb.Append("static value => ").Append(depRef).Append(" = value");
                        sb.Append(")");
                        break;
                }
                foreach (var arg in dep.CollectorAttr.ConstructorArguments)
                    sb.Append(", ").Append(arg.ToCSharpString());
                sb.AppendLine(");");
            }
            sb.Append(indentStr).AppendLine("}");

            // type footer
            while (indent-- > 0) sb.Append(' ', indent * 4).AppendLine("}");

            // register source code
            spc.AddSource($"{targetMethod.GetQualifiedSymbolName()}.g.cs", sb.ToString());
        }
    }
}
