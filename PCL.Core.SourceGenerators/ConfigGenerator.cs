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
public sealed class ConfigGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 收集所有可能的属性与类
        var propertyCandidates = context.SyntaxProvider.CreateSyntaxProvider(
            static (s, _) => s is PropertyDeclarationSyntax { AttributeLists.Count: > 0 },
            static (ctx, _) => _GetItemCandidate(ctx)
        ).Where(static m => m is not null);

        var groupCandidates = context.SyntaxProvider.CreateSyntaxProvider(
            static (s, _) => s is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
            static (ctx, _) => _GetGroupCandidate(ctx)
        ).Where(static m => m is not null);

        var configClassCandidates = context.SyntaxProvider.CreateSyntaxProvider(
            static (s, _) => s is ClassDeclarationSyntax,
            static (ctx, _) => _GetConfigClass(ctx)
        ).Where(static m => m is not null);

        // 新增：收集 [RegisterConfigEvent] 的 public static 属性
        var eventCandidates = context.SyntaxProvider.CreateSyntaxProvider(
            static (s, _) => s is PropertyDeclarationSyntax { AttributeLists.Count: > 0 },
            static (ctx, _) => _GetRegisterConfigEventCandidate(ctx)
        ).Where(static m => m is not null);

        var collected = propertyCandidates.Collect()
            .Combine(groupCandidates.Collect())
            .Combine(configClassCandidates.Collect());

        context.RegisterSourceOutput(collected, static (spc, triple) =>
        {
            var items = triple.Left.Left;
            var groups = triple.Left.Right;
            var configs = triple.Right;

            if (configs.Length == 0) return;

            // 建立快速查找
            var itemList = items.Cast<ItemModel>().ToImmutableArray();
            var groupList = groups.Cast<GroupModel>().ToImmutableArray();
            var configList = configs.Cast<ConfigModel>().ToImmutableArray();

            foreach (var config in configList)
            {
                try
                {
                    var tree = _BuildConfigTree(config, itemList, groupList);

                    // 跳过无用生成（无顶层项和顶层组声明的类型）
                    if (tree.TopItems.Count == 0 && tree.TopGroups.Count == 0) continue;

                    var source = _GenerateAdditionalSource(tree);
                    var hint = _MakeHintName(config);
                    spc.AddSource(hint, source);
                }
                catch
                {
                    // 可添加诊断，此处直接忽略以免打断编译
                }
            }
        });

        var serviceInputs = propertyCandidates.Collect().Combine(eventCandidates.Collect());
        context.RegisterSourceOutput(serviceInputs, static (spc, tuple) =>
        {
            var items  = tuple.Left.Cast<ItemModel>().OrderBy(i => i.DeclOrder).ToList();
            var events = tuple.Right.Cast<EventRegisterModel>().OrderBy(e => e.DeclOrder).ToList();

            // 两者都为空则不生成
            if (items.Count == 0 && events.Count == 0) return;

            var src = _GenerateServiceInitSource(items, events);
            spc.AddSource("ConfigService.g.cs", src);
        });
    }

    private static object? _GetItemCandidate(GeneratorSyntaxContext ctx)
    {
        var propSyntax = (PropertyDeclarationSyntax)ctx.Node;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(propSyntax);
        if (symbol == null) return null;
    
        var compilation = ctx.SemanticModel.Compilation;
        var attrDefItem = compilation.GetTypeByMetadataName("PCL.Core.App.Configuration.ConfigItemAttribute`1");
        var attrDefAny  = compilation.GetTypeByMetadataName("PCL.Core.App.Configuration.AnyConfigItemAttribute`1");
        if (attrDefItem == null && attrDefAny == null) return null;
    
        AttributeData? picked = null;
        var isAny = false;
    
        foreach (var a in symbol.GetAttributes())
        {
            var ac = a.AttributeClass;
            if (ac == null) continue;
            if (attrDefItem != null && SymbolEqualityComparer.Default.Equals(ac.ConstructedFrom, attrDefItem))
            {
                picked = a; isAny = false; break;
            }
            if (attrDefAny != null && SymbolEqualityComparer.Default.Equals(ac.ConstructedFrom, attrDefAny))
            {
                picked = a; isAny = true; break;
            }
        }
        if (picked == null) return null;
    
        if (picked.ConstructorArguments.Length < 1) return null;
        var key = picked.ConstructorArguments[0].Value as string;
        if (string.IsNullOrEmpty(key)) return null;
    
        // 默认值与来源解析
        var defaultCode = "default";
        var sourceCode = "ConfigSource.Shared";
    
        var attrSyntax = (AttributeSyntax?)picked.ApplicationSyntaxReference?.GetSyntax();
        if (attrSyntax != null)
        {
            var args = attrSyntax.ArgumentList?.Arguments;
    
            if (isAny)
            {
                // AnyConfigItem：没有“默认值”参数: 替换为无参构造函数
                var tQualified = _BuildQualifiedTypeName(symbol.Type);
                defaultCode = "() => new " + tQualified + "()";
    
                // 来源参数若存在，是第 2 个实参
                if (args is { Count: >= 2 })
                {
                    sourceCode = _RenderSourceCode(ctx.SemanticModel, args.Value[1].Expression);
                }
            }
            else
            {
                // ConfigItem：参数2为默认值，参数3为来源（可省略）
                if (args is { Count: >= 2 })
                {
                    defaultCode = _RenderDefaultValueCode(ctx.SemanticModel, args.Value[1].Expression);
                }
                if (args is { Count: >= 3 })
                {
                    sourceCode = _RenderSourceCode(ctx.SemanticModel, args.Value[2].Expression);
                }
            }
        }
    
        return new ItemModel
        {
            Property = symbol,
            Key = key!,
            Type = symbol.Type,
            IsStatic = symbol.IsStatic,
            DeclOrder = _GetDeclarationOrder(symbol),
            DefaultValueCode = defaultCode,
            SourceCode = sourceCode
        };
    }

    private static object? _GetGroupCandidate(GeneratorSyntaxContext ctx)
    {
        var classSyntax = (ClassDeclarationSyntax)ctx.Node;
        var symbol = ModelExtensions.GetDeclaredSymbol(ctx.SemanticModel, classSyntax) as INamedTypeSymbol;
        if (symbol == null) return null;

        var compilation = ctx.SemanticModel.Compilation;
        var attrDef = compilation.GetTypeByMetadataName("PCL.Core.App.Configuration.ConfigGroupAttribute");
        if (attrDef == null) return null;

        var attr = symbol.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass != null &&
            SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrDef));

        if (attr == null) return null;

        if (attr.ConstructorArguments.Length < 1) return null;
        var name = attr.ConstructorArguments[0].Value as string;
        if (string.IsNullOrEmpty(name)) return null;

        return new GroupModel
        {
            GroupType = symbol,
            GroupName = name!,
            DeclOrder = _GetDeclarationOrder(symbol)
        };
    }
    
    private static object? _GetRegisterConfigEventCandidate(GeneratorSyntaxContext ctx)
    {
        var propSyntax = (PropertyDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(propSyntax) is not { } symbol) return null;

        // 仅 public static
        if (symbol.DeclaredAccessibility != Accessibility.Public || !symbol.IsStatic) return null;

        // 精确匹配 [RegisterConfigEvent]
        var compilation = ctx.SemanticModel.Compilation;
        var attrDef = compilation.GetTypeByMetadataName("PCL.Core.App.Configuration.RegisterConfigEventAttribute");
        if (attrDef is null) return null;
        var hasAttr = symbol.GetAttributes().Any(a =>
            a.AttributeClass is not null &&
            SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrDef));
        if (!hasAttr) return null;

        return new EventRegisterModel
        {
            Property = symbol,
            DeclOrder = _GetDeclarationOrder(symbol)
        };
    }

    private static object? _GetConfigClass(GeneratorSyntaxContext ctx)
    {
        var classSyntax = (ClassDeclarationSyntax)ctx.Node;
        if (ModelExtensions.GetDeclaredSymbol(ctx.SemanticModel, classSyntax) is not INamedTypeSymbol symbol) return null;

        // 限定为 partial
        if (!_IsPartial(symbol)) return null;

        // 绕过 [ConfigGroup]
        var compilation = ctx.SemanticModel.Compilation;
        var attrDef = compilation.GetTypeByMetadataName("PCL.Core.App.Configuration.ConfigGroupAttribute");
        if (attrDef is not null)
        {
            var hasAttr = symbol.GetAttributes().Any(a =>
                a.AttributeClass is not null &&
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrDef));
            if (hasAttr) return null;
        }

        return new ConfigModel
        {
            ConfigType = symbol,
            DeclOrder = _GetDeclarationOrder(symbol)
        };
    }

    private static int _GetDeclarationOrder(ISymbol symbol)
    {
        var loc = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax().GetLocation();
        return loc?.SourceSpan.Start ?? int.MaxValue;
    }

    private static bool _IsPartial(INamedTypeSymbol type)
    {
        foreach (var decl in type.DeclaringSyntaxReferences)
        {
            var syn = decl.GetSyntax() as ClassDeclarationSyntax;
            if (syn == null) continue;
            if (syn.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                return true;
        }
        return false;
    }

    private static ConfigTree _BuildConfigTree(ConfigModel config,
        ImmutableArray<ItemModel> items,
        ImmutableArray<GroupModel> groups)
    {
        var configType = config.ConfigType;

        // 过滤归属于该 Config 的顶层项与组
        var topItems = items.Where(i => SymbolEqualityComparer.Default.Equals(i.Property.ContainingType, configType))
                            .OrderBy(i => i.DeclOrder)
                            .ToList();

        var allGroupsForConfig = groups
            .Where(g => _IsUnderConfig(g.GroupType, configType))
            .OrderBy(g => g.DeclOrder)
            .ToList();

        // 构建组索引
        var groupMap = allGroupsForConfig.ToDictionary(g => g.GroupType, g => new GroupNode(g), SymbolEqualityComparer.Default);

        // 组装层级
        foreach (var node in groupMap.Values)
        {
            var parentType = node.Model.GroupType.ContainingType;
            if (parentType != null && !SymbolEqualityComparer.Default.Equals(parentType, configType))
            {
                if (groupMap.TryGetValue(parentType, out var parentNode))
                {
                    parentNode.Children.Add(node);
                }
            }
        }

        // 顶层组
        var topGroups = groupMap.Values
            .Where(n => SymbolEqualityComparer.Default.Equals(n.Model.GroupType.ContainingType, configType))
            .OrderBy(n => n.Model.DeclOrder)
            .ToList();

        // 将 Item 分配到各自的组
        foreach (var item in items.Except(topItems))
        {
            var container = item.Property.ContainingType;
            if (container == null) continue;
            if (groupMap.TryGetValue(container, out var groupNode))
            {
                groupNode.Items.Add(item);
            }
        }

        return new ConfigTree
        {
            Namespace = configType.ContainingNamespace?.ToDisplayString() ?? "",
            ConfigType = configType,
            TopItems = topItems,
            TopGroups = topGroups
        };
    }

    private static bool _IsUnderConfig(INamedTypeSymbol group, INamedTypeSymbol configType)
    {
        var t = group.ContainingType;
        while (t != null)
        {
            if (SymbolEqualityComparer.Default.Equals(t, configType))
                return true;
            t = t.ContainingType;
        }
        return false;
    }

    private static string _MakeHintName(ConfigModel config)
    {
        var ns = config.ConfigType.ContainingNamespace?.ToDisplayString() ?? "Global";
        return $"{ns}.{config.ConfigType.Name}.g.cs";
    }

    private static string _GenerateAdditionalSource(ConfigTree tree)
    {
        var sb = new StringBuilder(4096);

        var ns = string.IsNullOrEmpty(tree.Namespace) ? null : tree.Namespace;
        var configType = tree.ConfigType;
        var configName = configType.Name;

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// 此文件由 Source Generator 自动生成，请勿手动修改");
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using PCL.Core.App.Configuration;");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(ns))
        {
            sb.Append("namespace ").Append(ns).AppendLine(";");
            sb.AppendLine();
        }

        sb.Append("partial class ").Append(configName).AppendLine();
        sb.AppendLine("{");

        // === Config Items ===
        sb.AppendLine("    // === Config Items ===");
        sb.AppendLine();
        if (tree.TopItems.Count == 0)
        {
            sb.AppendLine();
        }
        else
        {
            foreach (var item in tree.TopItems)
            {
                _EmitItem(sb, item, indent: 1, isTopLevel: true);
                sb.AppendLine();
            }
        }

        // === Config Groups ===
        sb.AppendLine("    // === Config Groups ===");
        if (tree.TopGroups.Count > 0)
        {
            foreach (var grp in tree.TopGroups)
            {
                _EmitGroupInto(sb, grp, indent: 1, isTopLevel: true);
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string _GenerateServiceInitSource(IReadOnlyList<ItemModel> items, IReadOnlyList<EventRegisterModel> events)
    {
        var sb = new StringBuilder(1024);
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// 此文件由 Source Generator 自动生成，请勿手动修改");
        sb.AppendLine();
        sb.AppendLine("namespace PCL.Core.App.Configuration;");
        sb.AppendLine();
        sb.AppendLine("public sealed partial class ConfigService");
        sb.AppendLine("{");

        // 配置项初始化
        sb.AppendLine("    private static void _InitializeConfigItems()");
        sb.AppendLine("    {");
        sb.AppendLine("        (string, ConfigItem)[] items = [");

        HashSet<string> keysAdded = [];
        for (var i = 0; i < items.Count; i++)
        {
            var it = items[i];
            if (!keysAdded.Add(it.Key)) continue;
            var keyLiteral = it.Key.ToLiteral();
            var typeName = _CorrectTypeName(_BuildQualifiedTypeName(it.Type), out _);
            sb.Append("            (")
                .Append(keyLiteral)
                .Append(", new ConfigItem<").Append(typeName).Append(">(")
                .Append(keyLiteral).Append(", ")
                .Append(it.DefaultValueCode).Append(", ").Append(it.SourceCode)
              .Append("))");
            if (i != items.Count - 1) sb.Append(',');
            sb.AppendLine();
        }

        sb.AppendLine("        ];");
        sb.AppendLine("        foreach (var (key, value) in items) {");
        sb.AppendLine("            _KeySet.Add(key);");
        sb.AppendLine("            _Items[key] = value;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();

        // 事件观察器初始化
        sb.AppendLine("    private static void _InitializeObservers()");
        sb.AppendLine("    {");
        sb.AppendLine("        ConfigEventRegistry[] registers = [");

        for (var i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            sb.Append("            ")
              .Append(_BuildQualifiedPropertyAccess(ev.Property));
            if (i != events.Count - 1) sb.Append(',');
            sb.AppendLine();
        }

        sb.AppendLine("        ];");
        sb.AppendLine("        foreach (var r in registers) foreach (var scope in r.Scopes) {");
        sb.AppendLine("            RegisterObserver(scope, r.ToObserver());");
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static Action<StringBuilder>? _EmitItem(StringBuilder sb, ItemModel item, int indent, bool isTopLevel)
    {
        Action<StringBuilder>? accessorInitializer = null;
        var typeName = _CorrectTypeName(_BuildQualifiedTypeName(item.Type), out var fullTypeName);
        var propName = item.Property.Name;
        var configItemName = propName + "Config";
        var staticKeyword = item.IsStatic || isTopLevel ? "static " : string.Empty;
        var indentStr = new string(' ', indent * 4);

        // 注释
        sb.Append(indentStr).Append("// Item: ").Append(propName).Append(" [").Append(item.Key).AppendLine("]");

        // 访问器
        sb.Append(indentStr)
          .Append("public ").Append(staticKeyword).Append("partial ")
          .Append(fullTypeName ?? typeName).Append(' ').Append(propName);

        if (fullTypeName != null)
        {
            var accessorName = "ACCESSOR_" + propName;
            sb.Append(" => ").Append(accessorName).AppendLine(";");
            // 初始化带参数访问器
            sb.Append(indentStr)
              .Append("private ").Append(staticKeyword).Append("readonly ")
              .Append(fullTypeName).Append(' ');
            // ReSharper disable once VariableHidesOuterVariable
            void AccessorInitializer(StringBuilder sb)
            {
                // 向构造函数传递生成访问器初始化代码的 StringBuilder 链式调用，以解决巨硬 2025 年
                // 仍然没支持隔壁 JVM 在 1.0 就支持的极其先进的 非静态字段初始化访问非静态上下文 的问题
                sb.Append(accessorName)
                  .Append(" = new((arg) => ")
                  .Append(configItemName)
                  .Append(".GetValue(arg), (arg, value) => ")
                  .Append(configItemName)
                  .AppendLine(".SetValue(value, arg));");
            }
            if (item.IsStatic) AccessorInitializer(sb);
            else {
                accessorInitializer = AccessorInitializer;
                sb.Append(accessorName).AppendLine(";");
            }
        }
        else
        {
            sb.Append(" { get => ")
              .Append(configItemName)
              .Append(".GetValue(); set => ")
              .Append(configItemName)
              .AppendLine(".SetValue(value); }");
        }

        // 配置项
        sb.Append(indentStr)
          .Append("public ").Append(staticKeyword)
          .Append("ConfigItem<").Append(typeName).Append("> ")
          .Append(configItemName).Append(" => ConfigService.GetConfigItem<")
            .Append(typeName)
          .Append(">(")
            .Append(item.Key.ToLiteral())
          .AppendLine(");");
        
        return accessorInitializer;
    }

    private static void _EmitGroupInto(StringBuilder sb, GroupNode node, int indent, bool isTopLevel = false)
    {
        var indentStr = new string(' ', indent * 4);
        var type = node.Model.GroupType;
        var typeName = type.Name;
        var staticKeyword = isTopLevel ? "static " : string.Empty;

        // 组实例字段（在其父作用域中）
        sb.AppendLine();
        sb.Append(indentStr).Append("// Group: ").AppendLine(node.Model.GroupName);
        sb.Append(indentStr)
          .Append("public ")
          .Append(staticKeyword)
          .Append("readonly ")
          .Append(typeName)
          .Append(' ')
          .Append(node.Model.GroupName)
          .Append(" = ")
          .Append(typeName)
          .AppendLine(".SINGLE_INSTANCE;");

        // 嵌套类型定义
        sb.Append(indentStr)
          .Append("public sealed partial class ")
          .Append(typeName)
          .AppendLine(" : IConfigScope");
        sb.Append(indentStr).AppendLine("{");

        // === Config Items ===
        sb.Append(indentStr).AppendLine("    // === Config Items ===");
        sb.Append(indentStr).AppendLine();
        List<Action<StringBuilder>> accessorInitializers = [];
        foreach (var item in node.Items.OrderBy(i => i.DeclOrder))
        {
            var result = _EmitItem(sb, item, indent + 1, isTopLevel: false);
            if (result != null) accessorInitializers.Add(result);
            sb.AppendLine();
        }

        // === Config Groups ===
        sb.Append(indentStr).AppendLine("    // === Config Groups ===");
        foreach (var child in node.Children.OrderBy(c => c.Model.DeclOrder))
        {
            _EmitGroupInto(sb, child, indent + 1);
        }

        // === Group Scope Implementation ===
        sb.AppendLine();
        sb.Append(indentStr).AppendLine("    // === Group Scope Implementation ===");
        sb.Append(indentStr).AppendLine();
        sb.Append(indentStr).AppendLine("    public static readonly " + typeName + " SINGLE_INSTANCE = new();");
        sb.Append(indentStr).AppendLine("    private readonly IConfigScope[] _InnerScopes;");
        sb.Append(indentStr).AppendLine("    private " + typeName + "()");
        sb.Append(indentStr).AppendLine("    {");
        sb.Append(indentStr).AppendLine("        _InnerScopes = [");

        // InnerScopes: 先项再子组
        var first = true;
        foreach (var item in node.Items.SkipWhile(i => i.IsStatic).OrderBy(i => i.DeclOrder))
        {
            if (!first) sb.AppendLine(",");
            sb.Append(indentStr).Append("            ").Append(item.Property.Name).Append("Config");
            first = false;
        }
        foreach (var child in node.Children.OrderBy(c => c.Model.DeclOrder))
        {
            if (!first) sb.AppendLine(",");
            sb.Append(indentStr).Append("            ").Append(child.Model.GroupName);
            first = false;
        }
        if (!first) sb.AppendLine();
        sb.Append(indentStr).AppendLine("        ];");
        foreach (var initializer in accessorInitializers)
        {
            sb.Append(indentStr).Append("        ");
            initializer.Invoke(sb);
        }
        sb.Append(indentStr).AppendLine("    }");

        // CheckScope
        sb.Append(indentStr).AppendLine("    public IEnumerable<string> CheckScope(IReadOnlySet<string> keys)");
        sb.Append(indentStr).AppendLine("    {");
        sb.Append(indentStr).AppendLine("        IEnumerable<string> result = [];");
        sb.Append(indentStr).AppendLine("        foreach (var scope in _InnerScopes)");
        sb.Append(indentStr).AppendLine("        {");
        sb.Append(indentStr).AppendLine("            var next = scope.CheckScope(keys);");
        sb.Append(indentStr).AppendLine("            if (next.Any()) result = result.Concat(next);");
        sb.Append(indentStr).AppendLine("        }");
        sb.Append(indentStr).AppendLine("        return result;");
        sb.Append(indentStr).AppendLine("    }");

        // Reset
        sb.Append(indentStr).AppendLine("    public bool Reset(object? argument = null)");
        sb.Append(indentStr).AppendLine("    {");
        sb.Append(indentStr).AppendLine("        var result = true;");
        sb.Append(indentStr).AppendLine("        foreach (var scope in _InnerScopes)");
        sb.Append(indentStr).AppendLine("        {");
        sb.Append(indentStr).AppendLine("            var next = scope.Reset(argument);");
        sb.Append(indentStr).AppendLine("            if (!next) result = false;");
        sb.Append(indentStr).AppendLine("        }");
        sb.Append(indentStr).AppendLine("        return result;");
        sb.Append(indentStr).AppendLine("    }");

        // IsDefault
        sb.Append(indentStr).AppendLine("    public bool IsDefault(object? argument = null)");
        sb.Append(indentStr).AppendLine("    {");
        sb.Append(indentStr).AppendLine("        var result = true;");
        sb.Append(indentStr).AppendLine("        foreach (var scope in _InnerScopes)");
        sb.Append(indentStr).AppendLine("        {");
        sb.Append(indentStr).AppendLine("            var next = scope.IsDefault(argument);");
        sb.Append(indentStr).AppendLine("            if (!next) result = false;");
        sb.Append(indentStr).AppendLine("        }");
        sb.Append(indentStr).AppendLine("        return result;");
        sb.Append(indentStr).AppendLine("    }");

        sb.Append(indentStr).AppendLine("}");
    }

    // ===== Models/Trees =====

    private sealed class ItemModel
    {
        public IPropertySymbol Property { get; set; } = null!;
        public string Key { get; set; } = "";
        public ITypeSymbol Type { get; set; } = null!;
        public bool IsStatic { get; set; }
        public int DeclOrder { get; set; }
        public string DefaultValueCode { get; set; } = "";
        public string SourceCode { get; set; } = "ConfigSource.Shared";
    }

    private sealed class GroupModel
    {
        public INamedTypeSymbol GroupType { get; set; } = null!;
        public string GroupName { get; set; } = "";
        public int DeclOrder { get; set; }
    }

    private sealed class GroupNode(GroupModel model)
    {
        public GroupModel Model { get; } = model;
        public List<GroupNode> Children { get; } = [];
        public List<ItemModel> Items { get; } = [];
    }

    private sealed class ConfigModel
    {
        public INamedTypeSymbol ConfigType { get; set; } = null!;
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public int DeclOrder { get; set; }
    }

    private sealed class ConfigTree
    {
        public string Namespace { get; set; } = "";
        public INamedTypeSymbol ConfigType { get; set; } = null!;
        public List<ItemModel> TopItems { get; set; } = [];
        public List<GroupNode> TopGroups { get; set; } = [];
    }

    private sealed class EventRegisterModel
    {
        public IPropertySymbol Property { get; set; } = null!;
        public int DeclOrder { get; set; }
    }

    private static string _RenderDefaultValueCode(SemanticModel sm, ExpressionSyntax expr)
    {
        if (expr is LiteralExpressionSyntax || _IsNegativeNumeric(expr))
            return expr.ToString();

        if (expr is TypeOfExpressionSyntax toe)
        {
            var type = sm.GetTypeInfo(toe.Type).Type;
            if (type != null)
                return "typeof(" + _BuildQualifiedTypeName(type) + ")";
            return expr.ToString();
        }

        if (expr is InvocationExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" },
                ArgumentList.Arguments.Count: 1
            } inv)
        {
            var targetExpr = inv.ArgumentList.Arguments[0].Expression;
            var sym = sm.GetSymbolInfo(targetExpr).Symbol;
            if (sym != null)
            {
                var qualified = _BuildQualifiedSymbolName(sym);
                return "nameof(" + qualified + ")";
            }
            return expr.ToString();
        }

        // 其它常见引用：枚举成员/const 字段/静态字段等
        var s = sm.GetSymbolInfo(expr).Symbol;
        if (s is IFieldSymbol fs)
        {
            var qualified = _BuildQualifiedSymbolName(fs);
            return qualified;
        }
     
        // 兜底：保留源码（如数组/集合等复杂表达式）
        return expr.ToString();
    }

    private static bool _IsNegativeNumeric(ExpressionSyntax expr)
    {
        return expr is PrefixUnaryExpressionSyntax p
               && p.IsKind(SyntaxKind.UnaryMinusExpression)
               && p.Operand is LiteralExpressionSyntax l
               && l.IsKind(SyntaxKind.NumericLiteralExpression);
    }

    // 来源渲染（缺省为 ConfigSource.Shared）
    private static string _RenderSourceCode(SemanticModel sm, ExpressionSyntax expr)
    {
        // 允许直接写枚举成员或带限定名
        var sym = sm.GetSymbolInfo(expr).Symbol;
        if (sym is IFieldSymbol fs && fs.ContainingType?.ToDisplayString() == "PCL.Core.App.Configuration.ConfigSource")
        {
            // 输出 ConfigSource.Xxx 形式（与示例一致）
            return "ConfigSource." + fs.Name;
        }
        // 兜底：使用源码文本
        return expr.ToString();
    }

    // 构建“命名空间.类型链.成员”形式完整名称
    private static string _BuildQualifiedSymbolName(ISymbol symbol)
    {
        // 枚举成员/const 字段
        var parts = new Stack<string>();
        if (symbol is IFieldSymbol { ContainingType: not null } f)
        {
            parts.Push(f.Name);
            var t = f.ContainingType;
            while (t != null)
            {
                parts.Push(t.Name);
                t = t.ContainingType;
            }
            var ns = f.ContainingNamespace?.ToDisplayString();
            if (!string.IsNullOrEmpty(ns)) parts.Push(ns!);
            return string.Join(".", parts);
        }
     
        // 其它符号（如类型）回退到类型名
        if (symbol is ITypeSymbol ts) return _BuildQualifiedTypeName(ts);
        var n = symbol.ContainingNamespace?.ToDisplayString();
        return string.IsNullOrEmpty(n) ? symbol.ToDisplayString() : n + "." + symbol.ToDisplayString();
    }
    
    private static bool _TryGetSpecialTypeKeyword(ITypeSymbol t, out string keyword)
    {
        switch (t.SpecialType)
        {
            case SpecialType.System_Boolean: keyword = "bool"; return true;
            case SpecialType.System_Byte: keyword = "byte"; return true;
            case SpecialType.System_SByte: keyword = "sbyte"; return true;
            case SpecialType.System_Int16: keyword = "short"; return true;
            case SpecialType.System_UInt16: keyword = "ushort"; return true;
            case SpecialType.System_Int32: keyword = "int"; return true;
            case SpecialType.System_UInt32: keyword = "uint"; return true;
            case SpecialType.System_Int64: keyword = "long"; return true;
            case SpecialType.System_UInt64: keyword = "ulong"; return true;
            case SpecialType.System_IntPtr: keyword = "nint"; return true;
            case SpecialType.System_UIntPtr: keyword = "nuint"; return true;
            case SpecialType.System_Char: keyword = "char"; return true;
            case SpecialType.System_String: keyword = "string"; return true;
            case SpecialType.System_Object: keyword = "object"; return true;
            case SpecialType.System_Single: keyword = "float"; return true;
            case SpecialType.System_Double: keyword = "double"; return true;
            case SpecialType.System_Decimal: keyword = "decimal"; return true;
            default: keyword = ""; return false;
        }
    }

    // 类型完整名（命名空间 + 外层类型 + 内层类型）
    private static string _BuildQualifiedTypeName(ITypeSymbol type)
    {
        // Nullable<T> 处理：T? / 呈现内层类型后加 ?
        if (type is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                TypeArguments.Length: 1
            } nt)
        {
            var inner = nt.TypeArguments[0];
            return _BuildQualifiedTypeName(inner) + "?";
        }
 
        // 基本类型关键字
        if (_TryGetSpecialTypeKeyword(type, out var keyword))
            return keyword;
 
        // 非基本类型：完整命名空间 + 外层类型 + 内层类型 + 泛型参数
        var format = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes
        );
        return type.ToDisplayString(format);
    }

    private static string _BuildQualifiedPropertyAccess(IPropertySymbol prop)
    {
        var owner = _BuildQualifiedTypeName(prop.ContainingType);
        return owner + "." + prop.Name;
    }

    // 传入 qualified name，返回真实的 type name
    // 若传入不是真实 type name，则 fullTypeName 将被赋予传入值，否则为 null
    private static string _CorrectTypeName(string typeName, out string? fullTypeName)
    {
        var isArgConfig = typeName.StartsWith("PCL.Core.App.Configuration.ArgConfig<");
        if (isArgConfig)
        {
            fullTypeName = typeName;
            typeName = typeName.Substring(37, typeName.Length - 38);
        }
        else fullTypeName = null;
        return typeName;
    }
}
