using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PCL.Core.SourceGenerators;

public static class SharedExtensions
{
    public static string ToLiteral(this string str) => SymbolDisplay.FormatLiteral(str, true);

    public static string? ToPrimitive(this object? obj) => SymbolDisplay.FormatPrimitive(obj, true, false);

    public static int GetDeclarationOrder(this ISymbol symbol)
    {
        var loc = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax().GetLocation();
        return loc?.SourceSpan.Start ?? int.MaxValue;
    }

    extension(INamedTypeSymbol type)
    {
        public bool IsPartial()
        {
            foreach (var decl in type.DeclaringSyntaxReferences)
            {
                if (decl.GetSyntax() is ClassDeclarationSyntax { Modifiers: { } modifiers } &&
                    modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                    return true;
            }
            return false;
        }

        public bool IsNestedWithin(INamedTypeSymbol potentialContainer)
        {
            var t = type.ContainingType;
            while (t != null)
            {
                if (SymbolEqualityComparer.Default.Equals(t, potentialContainer))
                    return true;
                t = t.ContainingType;
            }
            return false;
        }

        public bool IsAttribute()
        {
            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (baseType.ToDisplayString() == "System.Attribute") return true;
                baseType = baseType.BaseType;
            }
            return false;
        }
    }

    public static string RenderDefaultValueCode(this SemanticModel sm, ExpressionSyntax expr)
    {
        if (expr is LiteralExpressionSyntax || expr.IsNegativeNumeric())
            return expr.ToString();

        if (expr is TypeOfExpressionSyntax toe)
        {
            var type = sm.GetTypeInfo(toe.Type).Type;
            if (type != null)
                return "typeof(" + type.GetFullyQualifiedName() + ")";
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
                return "nameof(" + sym.GetQualifiedSymbolName() + ")";
            }
            return expr.ToString();
        }

        var s = sm.GetSymbolInfo(expr).Symbol;
        if (s is IFieldSymbol fs)
        {
            return fs.GetQualifiedSymbolName();
        }

        return expr.ToString();
    }

    public static bool IsNegativeNumeric(this ExpressionSyntax expr)
    {
        return expr is PrefixUnaryExpressionSyntax p
               && p.IsKind(SyntaxKind.UnaryMinusExpression)
               && p.Operand is LiteralExpressionSyntax l
               && l.IsKind(SyntaxKind.NumericLiteralExpression);
    }

    extension(ISymbol symbol)
    {
        public string GetQualifiedSymbolName()
        {
            if (symbol is ITypeSymbol ts) return ts.GetFullyQualifiedName();
            
            var parts = new Stack<string>();
            parts.Push(symbol.Name);
            var t = symbol.ContainingType;
            while (t != null)
            {
                parts.Push(t.Name);
                t = t.ContainingType;
            }
            var ns = symbol.ContainingNamespace?.ToDisplayString();
            if (!string.IsNullOrEmpty(ns)) parts.Push(ns!);
            return string.Join(".", parts);
        }
    }

    private static readonly SymbolDisplayFormat _SimplifiedTypeNameFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.OmittedAsContaining,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        miscellaneousOptions:
        SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
        SymbolDisplayMiscellaneousOptions.CollapseTupleTypes |
        SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
        SymbolDisplayMiscellaneousOptions.UseSpecialTypes,
        genericsOptions: SymbolDisplayGenericsOptions.None
    );

    private static readonly SymbolDisplayFormat _FullQualifiedNameFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions:
        SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
        SymbolDisplayMiscellaneousOptions.UseSpecialTypes
    );

    extension(ITypeSymbol type)
    {
        public string GetSimplifiedTypeName()
        {
            return type.ToDisplayString(_SimplifiedTypeNameFormat);
        }

        public string GetFullyQualifiedName()
        {
            if (type is INamedTypeSymbol {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                TypeArguments.Length: 1 } nt)
            {
                var inner = nt.TypeArguments[0];
                return inner.GetFullyQualifiedName() + "?";
            }
            if (type.TryGetSpecialTypeKeyword(out var keyword)) return keyword;
            return type.ToDisplayString(_FullQualifiedNameFormat);
        }

        public bool TryGetSpecialTypeKeyword(out string keyword)
        {
            switch (type.SpecialType)
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
    }

    public static string GetQualifiedPropertyAccess(this IPropertySymbol prop)
    {
        var owner = prop.ContainingType.GetFullyQualifiedName();
        return owner + "." + prop.Name;
    }

    public static string CorrectConfigTypeName(this string typeName, out string? fullTypeName)
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
