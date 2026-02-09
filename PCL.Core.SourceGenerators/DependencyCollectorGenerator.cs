using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PCL.Core.SourceGenerators;

public record CollectorInfo(INamedTypeSymbol CollectorAttrSymbol, ITypeSymbol DependencyType, string Identifier, AttributeTargets Targets);

public record MatchResult(ISymbol Target, AttributeTargets TargetType, AttributeData CollectorAttr, CollectorInfo Info);

[Generator(LanguageNames.CSharp)]
public sealed class DependencyCollectorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        const string collectorMarkupAttr = "PCL.Core.App.IoC.DependencyCollectorAttribute";
        const string collectorMarkupAttrFull = $"{collectorMarkupAttr}`1";
        
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
                        if (attrClass == null || attrClass.GetSimplifiedTypeName() != collectorMarkupAttr) continue;
                        // 收集注解信息
                        var dependencyType = attrClass.TypeArguments.FirstOrDefault();
                        if (dependencyType == null) continue;
                        var ctorArgs = attrData.ConstructorArguments;
                        if (ctorArgs.Length < 2
                            || ctorArgs[0].Value is not string identifier
                            || ctorArgs[1].Value is not int targets)
                            continue;
                        infos.Add(new CollectorInfo(attr, dependencyType, identifier, (AttributeTargets)targets));
                    }
                    return new KeyValuePair<INamedTypeSymbol, List<CollectorInfo>>(attr, infos);
                })
            .Where(x => x.Key != null)
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
                if (symbol == null) return [];
                // 确定目标类型
                AttributeTargets targetType = default;
                if (symbol is INamedTypeSymbol) targetType = AttributeTargets.Class;
                else if (symbol is IPropertySymbol) targetType = AttributeTargets.Property;
                else if (symbol is IMethodSymbol) targetType = AttributeTargets.Method;
                // 筛选目标所有符合条件的注解
                var results = new List<MatchResult>();
                foreach (var attrData in symbol.GetAttributes())
                {
                    var attr = attrData.AttributeClass;
                    if (attr == null) continue;
                    if (!validAttrs.TryGetValue(attr, out var infos)) continue;
                    results.AddRange(
                        from info in infos
                        where info.Targets.HasFlag(targetType)
                        select new MatchResult(symbol, targetType, attrData, info)
                    );
                }
                return results;
            })
            .Collect();
        
        // 生成代码
        context.RegisterSourceOutput(matches, _GenerateDependencyGroup);
    }

    private static void _GenerateDependencyGroup(SourceProductionContext spc, ImmutableArray<MatchResult> matches)
    {
        var dependencyMap = new Dictionary<CollectorInfo, Dictionary<AttributeTargets, List<MatchResult>>>();
        foreach (var dep in matches)
        {
            var info = dep.Info;
            if (!dependencyMap.TryGetValue(info, out var map))
            {
                map = new Dictionary<AttributeTargets, List<MatchResult>>
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
                    if (ctor == null) return string.Empty;
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
}
