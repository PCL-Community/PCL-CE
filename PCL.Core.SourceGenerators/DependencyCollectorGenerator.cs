using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PCL.Core.SourceGenerators;

#pragma warning disable CS9113 // Parameter is unread.
// ReSharper disable UnusedTypeParameter

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class DependencyCollectorAttribute<TDependency>(string identifier, AttributeTargets targets) : Attribute;

// ReSharper restore UnusedTypeParameter
#pragma warning restore CS9113 // Parameter is unread.

public record CollectorInfo(ITypeSymbol DependencyType, string Identifier, AttributeTargets Targets);

public record MatchResult(ISymbol Target, AttributeTargets TargetType, INamedTypeSymbol CollectorAttr, CollectorInfo Info);

[Generator(LanguageNames.CSharp)]
public sealed class DependencyCollectorGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var collectorMarkupAttr = typeof(DependencyCollectorAttribute<>).FullName!;
        
        // 收集被标记为 collector 的注解
        var collectorAttrs = context.SyntaxProvider
            .ForAttributeWithMetadataName(collectorMarkupAttr,
                predicate: static (node, _) => node is ClassDeclarationSyntax, 
                transform: static (ctx, _) =>
                {
                    if (ctx.TargetSymbol is not INamedTypeSymbol attr || !attr.IsAttribute()) return default;
                    var infos = new List<CollectorInfo>();
                    foreach (var attrData in ctx.Attributes)
                    {
                        var attrClass = attrData.AttributeClass;
                        if (attrClass == null || attrClass.GetSimplifiedTypeName() != "PCL.Core.SourceGenerators.DependencyCollectorAttribute") continue;
                        var dependencyType = attrClass.TypeArguments.FirstOrDefault();
                        if (dependencyType == null) continue;
                        var ctorArgs = attrData.ConstructorArguments;
                        if (ctorArgs.Length < 2
                            || ctorArgs[0].Value is not string identifier
                            || ctorArgs[1].Value is not AttributeTargets targets)
                            continue;
                        infos.Add(new CollectorInfo(dependencyType, identifier, targets));
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
        
        // 收集所有带注解的 member
        var potentialTargets = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) =>
            {
                // 仅支持 class, property, method
                if (node is not (ClassDeclarationSyntax or PropertyDeclarationSyntax or MethodDeclarationSyntax)) return false;
                return node is MemberDeclarationSyntax { AttributeLists.Count: > 0 };
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
                        select new MatchResult(symbol, targetType, attr, info)
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
        // TODO
    }
}
