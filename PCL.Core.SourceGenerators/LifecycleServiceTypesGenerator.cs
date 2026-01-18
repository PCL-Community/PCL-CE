using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PCL.Core.SourceGenerators;

[Generator(LanguageNames.CSharp)]
public class LifecycleServiceTypesGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 查找 LifecycleState.cs 文件以获取有效的枚举值
        var lifecycleStateProvider = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith("LifecycleState.cs"))
            .Select(static (text, cancellationToken) => 
            {
                var content = text.GetText(cancellationToken)?.ToString();
                return _GetValidLifecycleStates(content);
            })
            .Where(static states => states?.Count > 0)
            .Collect();

        // 查找带有 LifecycleService 属性的类
        var serviceClassProvider = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (ctx, _) => _GetLifecycleServiceInfo(ctx))
            .Where(static x => x is not null);

        // 收集所有服务信息
        var servicesProvider = serviceClassProvider.Collect();

        // 合并枚举状态和服务信息
        var combinedProvider = lifecycleStateProvider.Combine(servicesProvider);

        // 生成代码
        context.RegisterSourceOutput(combinedProvider, 
            static (spc, data) => _Execute(spc, data.Left.FirstOrDefault() ?? new List<string>(), data.Right.Where(x => x != null).Select(x => x!).ToImmutableArray()));
    }

    private static List<string>? _GetValidLifecycleStates(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return null;

        var validStates = new List<string>();

        // 提取枚举定义块
        var enumPattern = @"(?s)public\s+enum\s+LifecycleState\s*\{(.*?)\}";
        var enumMatch = Regex.Match(content, enumPattern);

        if (!enumMatch.Success)
            return null;

        var enumContent = enumMatch.Groups[1].Value;

        // 按行分割并处理每一行
        var lines = enumContent.Split('\n');
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // 跳过空行、注释行和花括号
            if (string.IsNullOrEmpty(trimmedLine) ||
                trimmedLine.StartsWith("///") ||
                trimmedLine.StartsWith("//") ||
                trimmedLine.StartsWith("/*") ||
                trimmedLine.StartsWith("*") ||
                trimmedLine == "{" ||
                trimmedLine == "}")
            {
                continue;
            }

            // 匹配枚举成员（可能包含逗号）
            var memberMatch = Regex.Match(trimmedLine, @"^(\w+)\s*,?\s*$");
            if (memberMatch.Success)
            {
                var enumValue = memberMatch.Groups[1].Value;
                if (!string.IsNullOrEmpty(enumValue) && enumValue != "LifecycleState")
                {
                    validStates.Add(enumValue);
                }
            }
        }

        return validStates.Count > 0 ? validStates : null;
    }

    private static LifecycleServiceInfo? _GetLifecycleServiceInfo(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        // 查找 LifecycleService 属性
        var lifecycleAttribute = classDeclaration.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a => a.Name.ToString().Contains("LifecycleService"));

        if (lifecycleAttribute == null)
            return null;

        // 获取语义模型信息
        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
        if (classSymbol == null)
            return null;

        // 解析属性参数
        var state = "Unknown";
        var priority = 0;

        if (lifecycleAttribute.ArgumentList?.Arguments.Count > 0)
        {
            // 解析第一个参数（状态）
            var firstArg = lifecycleAttribute.ArgumentList.Arguments[0];
            if (firstArg.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                state = memberAccess.Name.Identifier.ValueText;
            }

            // 查找 Priority 参数（支持命名参数和位置参数）
            var priorityArg = lifecycleAttribute.ArgumentList.Arguments
                .FirstOrDefault(arg => arg.NameEquals?.Name.Identifier.ValueText == "Priority");

            // 如果没有找到命名的Priority参数，检查第二个位置参数
            if (priorityArg == null && lifecycleAttribute.ArgumentList.Arguments.Count > 1)
            {
                priorityArg = lifecycleAttribute.ArgumentList.Arguments[1];
            }

            if (priorityArg != null)
            {
                priority = _ParsePriorityExpression(priorityArg.Expression, semanticModel);
            }
        }

        return new LifecycleServiceInfo(
            classSymbol.ToDisplayString(),
            classSymbol.Name,
            state,
            priority);
    }

    private static int _ParsePriorityExpression(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal:
                if (int.TryParse(literal.Token.ValueText, out var literalValue))
                    return literalValue;
                break;

            case MemberAccessExpressionSyntax memberAccess:
                var memberName = memberAccess.ToString();
                if (memberName == "int.MaxValue")
                    return int.MaxValue;
                if (memberName == "int.MinValue")
                    return int.MinValue;
                break;

            case PrefixUnaryExpressionSyntax unary when unary.IsKind(SyntaxKind.UnaryMinusExpression):
                // 处理负数
                if (unary.Operand is LiteralExpressionSyntax negLiteral &&
                    int.TryParse(negLiteral.Token.ValueText, out var negValue))
                {
                    return -negValue;
                }
                break;

            case BinaryExpressionSyntax binary:
                // 简单的数学表达式支持
                var left = _ParsePriorityExpression(binary.Left, semanticModel);
                var right = _ParsePriorityExpression(binary.Right, semanticModel);
                    
                return binary.OperatorToken.Kind() switch
                {
                    SyntaxKind.PlusToken => left + right,
                    SyntaxKind.MinusToken => left - right,
                    SyntaxKind.AsteriskToken => left * right,
                    SyntaxKind.SlashToken => right != 0 ? left / right : 0,
                    _ => 0
                };
        }

        // 尝试获取常量值
        var constantValue = semanticModel.GetConstantValue(expression);
        return constantValue is { HasValue: true, Value: int intValue } ? intValue : 0;
    }

    private static void _Execute(SourceProductionContext context, List<string> validStates, ImmutableArray<LifecycleServiceInfo> services)
    {
        // 过滤服务，只保留有效状态的服务
        var filteredServices = services.Where(s => validStates.Count == 0 || validStates.Contains(s.State)).ToList();
            
        // 按状态分组并排序
        var groupedServices = filteredServices
            .GroupBy(s => s.State)
            .OrderBy(g => g.Key)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// 此文件由 Source Generator 自动生成，请勿手动修改");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine("namespace PCL.Core.App;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// 包含所有使用 LifecycleService 注解的类型，按 StartState 分类并按 Priority 降序排序");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class LifecycleServiceTypes");
        sb.AppendLine("{");

        // 为每个状态生成数组
        foreach (var group in groupedServices)
        {
            var sortedServices = group.OrderByDescending(s => s.Priority).ToList();

            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {group.Key} 状态的生命周期服务类型");
            sb.AppendLine($"    /// </summary>");
            sb.AppendLine($"    public static readonly Type[] {group.Key} = [");

            foreach (var service in sortedServices)
            {
                sb.AppendLine($"        typeof({service.FullName}), // Priority: {service.Priority}");
            }

            sb.AppendLine("    ];");
            sb.AppendLine();
        }

        // 生成 GetServiceTypes 方法
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// 获取指定生命周期状态的所有服务类型");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"state\">生命周期状态</param>");
        sb.AppendLine("    /// <returns>该状态下的所有服务类型数组</returns>");
        sb.AppendLine("    public static Type[] GetServiceTypes(LifecycleState state) => state switch");
        sb.AppendLine("    {");

        foreach (var group in groupedServices)
        {
            sb.AppendLine($"        LifecycleState.{group.Key} => {group.Key},");
        }

        sb.AppendLine("        _ => new Type[0]");
        sb.AppendLine("    };");
        sb.AppendLine();

        // 生成 GetAllServiceTypes 方法
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// 获取所有生命周期服务类型的状态映射");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <returns>状态到类型数组的字典</returns>");
        sb.AppendLine("    public static System.Collections.Generic.Dictionary<LifecycleState, Type[]> GetAllServiceTypes() => new()");
        sb.AppendLine("    {");

        foreach (var group in groupedServices)
        {
            sb.AppendLine($"        [LifecycleState.{group.Key}] = {group.Key},");
        }

        sb.AppendLine("    };");
        sb.AppendLine();

        // 生成统计信息方法
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// 获取生命周期服务的统计信息");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <returns>包含状态数量和总服务数量的统计信息</returns>");
        sb.AppendLine($"    public static (int StateCount, int TotalServices) GetStatistics() => ({groupedServices.Count}, {filteredServices.Count});");

        sb.AppendLine("}");

        context.AddSource("LifecycleServiceTypes.g.cs", sb.ToString());
    }
}

// 破烂 .NET Standard 用不了 init 修饰没法 record，先这样吧
public class LifecycleServiceInfo(string fullName, string className, string state, int priority)
{
    public string FullName { get; } = fullName;
    public string ClassName { get; } = className;
    public string State { get; } = state;
    public int Priority { get; } = priority;
}
