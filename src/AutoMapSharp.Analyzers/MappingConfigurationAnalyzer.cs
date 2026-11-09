using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AutoMapSharp.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapSharp.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MappingConfigurationAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.UnmappedDestinationProperty,
            DiagnosticDescriptors.IncompatiblePropertyTypes,
            DiagnosticDescriptors.PotentialCircularReference);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        var methodSymbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (methodSymbol == null || methodSymbol.Name != "DefineMap" || methodSymbol.TypeArguments.Length != 2)
        {
            return;
        }

        var sourceType = methodSymbol.TypeArguments[0] as INamedTypeSymbol;
        var destinationType = methodSymbol.TypeArguments[1] as INamedTypeSymbol;
        if (sourceType == null || destinationType == null)
        {
            return;
        }

        var topInvocation = invocation;
        while (topInvocation.Parent is MemberAccessExpressionSyntax mae && mae.Parent is InvocationExpressionSyntax parentInv)
        {
            topInvocation = parentInv;
        }

        var chainedCalls = CollectChainedCalls(topInvocation, invocation);

        if (chainedCalls.Any(c => c.MethodName == "ConvertUsing"))
        {
            return;
        }

        bool hasMaxDepth = chainedCalls.Any(c => c.MethodName == "MaxDepth");
        bool hasPreserveReferences = chainedCalls.Any(c => c.MethodName == "PreserveReferences");

        if (!hasMaxDepth && !hasPreserveReferences)
        {
            if (HasSelfReference(sourceType) || HasSelfReference(destinationType))
            {
                var diag = Diagnostic.Create(
                    DiagnosticDescriptors.PotentialCircularReference,
                    invocation.GetLocation(),
                    sourceType.Name,
                    destinationType.Name);
                context.ReportDiagnostic(diag);
            }
        }

        var configuredProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var call in chainedCalls.Where(c => c.MethodName is "ForProperty" or "ForCtorParam" or "ForPath"))
        {
            if (call.DestinationPropertyName != null)
            {
                configuredProperties.Add(call.DestinationPropertyName);
            }
        }

        // Dictionary keys are only known at run time.
        if (IsStringObjectDictionary(sourceType) || IsStringObjectDictionary(destinationType))
        {
            return;
        }

        var conventions = ReadModuleConventions(invocation, context.SemanticModel);

        var destProps = GetWritableProperties(destinationType);
        var sourceProps = GetReadableProperties(sourceType);

        foreach (var destProp in destProps)
        {
            if (configuredProperties.Contains(destProp.Name) ||
                destProp.GetAttributes().Any(a => a.AttributeClass?.Name is "SkipMapAttribute" or "SourceMemberAttribute"))
            {
                continue; // Property is explicitly handled, in the chain or with an attribute
            }

            // Same matching rules as the runtime engine.
            var matchingSourceType = FindMatchingSourceMemberType(sourceType, sourceProps, destProp.Name, conventions);

            if (matchingSourceType == null)
            {
                if (CanFlatten(sourceProps, destProp.Name))
                {
                    continue;
                }

                var properties = ImmutableDictionary.CreateRange(new[]
                {
                    new KeyValuePair<string, string?>("PropertyName", destProp.Name)
                });

                var diag = Diagnostic.Create(
                    DiagnosticDescriptors.UnmappedDestinationProperty,
                    invocation.GetLocation(),
                    properties,
                    destProp.Name,
                    destinationType.Name,
                    sourceType.Name);

                context.ReportDiagnostic(diag);
            }
            else
            {
                if (!AreTypesCompatible(matchingSourceType, destProp.Type))
                {
                    var diag = Diagnostic.Create(
                        DiagnosticDescriptors.IncompatiblePropertyTypes,
                        invocation.GetLocation(),
                        destProp.Name,
                        matchingSourceType.ToDisplayString(),
                        sourceType.Name,
                        destProp.Type.ToDisplayString(),
                        destinationType.Name);

                    context.ReportDiagnostic(diag);
                }
            }
        }
    }

    /// <summary>
    /// The module-level naming settings the runtime applies, read from the module's source code.
    /// </summary>
    private sealed class ModuleConventions
    {
        public List<string> Prefixes { get; } = new() { "Get" };
        public List<string> Suffixes { get; } = new();
        public List<string> DestinationPrefixes { get; } = new();
        public List<string> DestinationSuffixes { get; } = new();
        public List<(string Original, string Replacement)> Replacements { get; } = new();
        public bool SnakeCaseSource { get; set; }
        public List<INamedTypeSymbol> ExtensionTypes { get; } = new();
    }

    private static bool IsStringObjectDictionary(INamedTypeSymbol type) =>
        type.AllInterfaces.Concat(type.TypeKind == TypeKind.Interface ? new[] { type } : Array.Empty<INamedTypeSymbol>())
            .Any(i => (i.Name == "IDictionary" || i.Name == "IReadOnlyDictionary") &&
                      i.TypeArguments.Length == 2 &&
                      i.TypeArguments[0].SpecialType == SpecialType.System_String &&
                      i.TypeArguments[1].SpecialType == SpecialType.System_Object);

    private static ModuleConventions ReadModuleConventions(SyntaxNode node, SemanticModel model)
    {
        var conventions = new ModuleConventions();
        var module = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (module == null) return conventions;

        foreach (var call in module.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var name = call.Expression switch
            {
                IdentifierNameSyntax id => id.Identifier.Text,
                MemberAccessExpressionSyntax member => member.Name.Identifier.Text,
                _ => null
            };

            var literals = call.ArgumentList.Arguments
                .Select(a => a.Expression)
                .OfType<LiteralExpressionSyntax>()
                .Where(l => l.IsKind(SyntaxKind.StringLiteralExpression))
                .Select(l => l.Token.ValueText);

            if (name == "IncludeSourceExtensionMethods" &&
                call.ArgumentList.Arguments.FirstOrDefault()?.Expression is TypeOfExpressionSyntax typeOf &&
                model.SyntaxTree == call.SyntaxTree &&
                model.GetTypeInfo(typeOf.Type).Type is INamedTypeSymbol extensionType)
            {
                conventions.ExtensionTypes.Add(extensionType);
            }

            if (name == "ClearPrefixes") conventions.Prefixes.Clear();
            else if (name == "RecognizePrefixes") conventions.Prefixes.AddRange(literals);
            else if (name == "RecognizeSuffixes") conventions.Suffixes.AddRange(literals);
            else if (name == "RecognizeDestinationPrefixes") conventions.DestinationPrefixes.AddRange(literals);
            else if (name == "RecognizeDestinationSuffixes") conventions.DestinationSuffixes.AddRange(literals);
            else if (name == "ReplaceMemberName")
            {
                var pair = literals.ToList();
                if (pair.Count == 2) conventions.Replacements.Add((pair[0], pair[1]));
            }
        }

        foreach (var assignment in module.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left.ToString().EndsWith("SourceNamingConvention", StringComparison.Ordinal) &&
                assignment.Right.ToString().Contains("SnakeCase"))
            {
                conventions.SnakeCaseSource = true;
            }
        }

        return conventions;
    }

    private static ITypeSymbol? FindMatchingSourceMemberType(INamedTypeSymbol sourceType, List<IPropertySymbol> sourceProps, string destPropName, ModuleConventions conventions)
    {
        var sourceMembers = sourceProps.Select(p => (p.Name, Type: (ITypeSymbol)p.Type))
            .Concat(GetPublicFields(sourceType).Select(f => (f.Name, Type: f.Type)))
            .ToList();

        string Replace(string name)
        {
            foreach (var (original, replacement) in conventions.Replacements) name = name.Replace(original, replacement);
            return name;
        }

        foreach (var member in sourceMembers)
        {
            if (string.Equals(Replace(member.Name), destPropName, StringComparison.OrdinalIgnoreCase)) return member.Type;
        }

        var destinationWords = MemberNameMatching.SplitPascal(
            MemberNameMatching.StripPrefixesAndSuffixes(destPropName, conventions.DestinationPrefixes, conventions.DestinationSuffixes));
        foreach (var member in sourceMembers)
        {
            var stripped = MemberNameMatching.StripPrefixesAndSuffixes(Replace(member.Name), conventions.Prefixes, conventions.Suffixes);
            var words = conventions.SnakeCaseSource ? MemberNameMatching.SplitSnake(stripped) : MemberNameMatching.SplitPascal(stripped);
            if (MemberNameMatching.WordsMatch(words, destinationWords)) return member.Type;
        }

        foreach (var extensionType in conventions.ExtensionTypes)
        {
            foreach (var method in extensionType.GetMembers().OfType<IMethodSymbol>())
            {
                if (!method.IsExtensionMethod || method.Parameters.Length != 1 || method.ReturnsVoid ||
                    !IsSameOrBase(method.Parameters[0].Type, sourceType))
                {
                    continue;
                }

                if (string.Equals(method.Name, destPropName, StringComparison.OrdinalIgnoreCase) ||
                    conventions.Prefixes.Any(prefix => string.Equals(method.Name, prefix + destPropName, StringComparison.OrdinalIgnoreCase)))
                {
                    return method.ReturnType;
                }
            }
        }

        for (var current = sourceType; current != null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var method in current.GetMembers().OfType<IMethodSymbol>())
            {
                if (method.IsStatic || method.Parameters.Length != 0 || method.ReturnsVoid || method.MethodKind != MethodKind.Ordinary)
                {
                    continue;
                }

                if (string.Equals(method.Name, destPropName, StringComparison.OrdinalIgnoreCase) ||
                    conventions.Prefixes.Any(prefix => string.Equals(method.Name, prefix + destPropName, StringComparison.OrdinalIgnoreCase)))
                {
                    return method.ReturnType;
                }
            }
        }

        return null;
    }

    private static bool IsSameOrBase(ITypeSymbol candidate, INamedTypeSymbol type)
    {
        for (ITypeSymbol? current = type; current != null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(candidate, current)) return true;
        }

        return type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(candidate, i));
    }

    private static IEnumerable<IFieldSymbol> GetPublicFields(INamedTypeSymbol type)
    {
        for (var current = type; current != null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var field in current.GetMembers().OfType<IFieldSymbol>())
            {
                if (field.DeclaredAccessibility == Accessibility.Public && !field.IsStatic && !field.IsImplicitlyDeclared)
                {
                    yield return field;
                }
            }
        }
    }

    private static bool HasSelfReference(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            if (member is IPropertySymbol prop &&
                SymbolEqualityComparer.Default.Equals(prop.Type, type))
            {
                return true;
            }
        }
        return false;
    }

    private static bool CanFlatten(List<IPropertySymbol> sourceProps, string destPropName)
    {
        foreach (var sp in sourceProps)
        {
            if (destPropName.StartsWith(sp.Name, StringComparison.OrdinalIgnoreCase) &&
                destPropName.Length > sp.Name.Length &&
                sp.Type is INamedTypeSymbol childNamedType)
            {
                var remaining = destPropName.Substring(sp.Name.Length);
                var childProps = GetReadableProperties(childNamedType);
                if (childProps.Any(cp => string.Equals(cp.Name, remaining, StringComparison.OrdinalIgnoreCase)) ||
                    CanFlatten(childProps, remaining))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool IsUserComplexType(ITypeSymbol type) =>
        (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct) &&
        type.SpecialType == SpecialType.None &&
        type.ContainingNamespace?.ToDisplayString() != "System";

    private static bool AreTypesCompatible(ITypeSymbol sourceType, ITypeSymbol destType)
    {
        if (SymbolEqualityComparer.Default.Equals(sourceType, destType))
        {
            return true;
        }

        var unwrappedSource = (sourceType as INamedTypeSymbol)?.IsGenericType == true &&
                              sourceType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            ? ((INamedTypeSymbol)sourceType).TypeArguments[0]
            : sourceType;

        var unwrappedDest = (destType as INamedTypeSymbol)?.IsGenericType == true &&
                            destType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            ? ((INamedTypeSymbol)destType).TypeArguments[0]
            : destType;

        if (SymbolEqualityComparer.Default.Equals(unwrappedSource, unwrappedDest))
        {
            return true;
        }

        if (unwrappedDest.SpecialType == SpecialType.System_String)
        {
            return true; // Any type can call ToString()
        }

        bool isSourceNumeric = IsNumericType(unwrappedSource);
        bool isDestNumeric = IsNumericType(unwrappedDest);
        if (isSourceNumeric && isDestNumeric)
        {
            return true;
        }

        if ((unwrappedSource.Name == "DateTime" || unwrappedSource.Name == "DateTimeOffset") &&
            (unwrappedDest.Name == "DateTime" || unwrappedDest.Name == "DateTimeOffset"))
        {
            return true;
        }

        if (unwrappedSource.SpecialType == SpecialType.System_String)
        {
            if (unwrappedDest.TypeKind == TypeKind.Enum ||
                unwrappedDest.Name == "Guid" ||
                unwrappedDest.Name == "DateTime" ||
                unwrappedDest.Name == "DateTimeOffset" ||
                unwrappedDest.Name == "DateOnly" ||
                unwrappedDest.Name == "TimeOnly")
            {
                return true;
            }
        }

        if (unwrappedDest.SpecialType == SpecialType.System_String)
        {
            if (unwrappedSource.TypeKind == TypeKind.Enum ||
                unwrappedSource.Name == "Guid" ||
                unwrappedSource.Name == "DateTime" ||
                unwrappedSource.Name == "DateTimeOffset" ||
                unwrappedSource.Name == "DateOnly" ||
                unwrappedSource.Name == "TimeOnly")
            {
                return true;
            }
        }

        // Enum to enum maps by name, with a numeric fallback.
        if (unwrappedSource.TypeKind == TypeKind.Enum && isDestNumeric) return true;
        if (unwrappedDest.TypeKind == TypeKind.Enum && isSourceNumeric) return true;
        if (unwrappedSource.TypeKind == TypeKind.Enum && unwrappedDest.TypeKind == TypeKind.Enum) return true;

        if (IsUserComplexType(unwrappedSource) && IsUserComplexType(unwrappedDest)) return true;

        if (unwrappedSource is INamedTypeSymbol && unwrappedDest is INamedTypeSymbol)
        {
            if (unwrappedSource.TypeKind == TypeKind.Class && unwrappedDest.TypeKind == TypeKind.Class)
            {
                return true;
            }
            if (unwrappedSource.TypeKind == TypeKind.Interface || unwrappedDest.TypeKind == TypeKind.Interface)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNumericType(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
                return true;
            default:
                return false;
        }
    }

    private static List<IPropertySymbol> GetWritableProperties(INamedTypeSymbol type)
    {
        var list = new List<IPropertySymbol>();
        var current = type;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol prop &&
                    !prop.IsStatic &&
                    prop.DeclaredAccessibility == Accessibility.Public &&
                    (prop.SetMethod != null))
                {
                    if (list.All(p => p.Name != prop.Name))
                    {
                        list.Add(prop);
                    }
                }
            }
            current = current.BaseType;
        }
        return list;
    }

    private static List<IPropertySymbol> GetReadableProperties(INamedTypeSymbol type)
    {
        var list = new List<IPropertySymbol>();
        var current = type;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in current.GetMembers())
            {
                if (member is IPropertySymbol prop &&
                    !prop.IsStatic &&
                    prop.DeclaredAccessibility == Accessibility.Public &&
                    prop.GetMethod != null)
                {
                    if (list.All(p => p.Name != prop.Name))
                    {
                        list.Add(prop);
                    }
                }
            }
            current = current.BaseType;
        }
        return list;
    }

    private static List<ChainedCall> CollectChainedCalls(InvocationExpressionSyntax top, InvocationExpressionSyntax root)
    {
        var calls = new List<ChainedCall>();
        var current = top;

        while (current != null)
        {
            if (current.Expression is MemberAccessExpressionSyntax mae)
            {
                string methodName = mae.Name.Identifier.ValueText;
                string? destProp = null;

                if (methodName == "ForProperty" && current.ArgumentList.Arguments.Count > 0)
                {
                    var firstArg = current.ArgumentList.Arguments[0].Expression;
                    destProp = ExtractPropertyName(firstArg);
                }
                else if (methodName == "ForCtorParam" && current.ArgumentList.Arguments.Count > 0 &&
                         current.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax literal &&
                         literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    destProp = literal.Token.ValueText;
                }
                else if (methodName == "ForPath" && current.ArgumentList.Arguments.Count > 0)
                {
                    destProp = ExtractRootMemberName(current.ArgumentList.Arguments[0].Expression);
                }

                calls.Add(new ChainedCall(methodName, destProp));

                if (current == root)
                {
                    break;
                }
                current = mae.Expression as InvocationExpressionSyntax;
            }
            else
            {
                break;
            }
        }

        return calls;
    }

    private static string? ExtractPropertyName(ExpressionSyntax expression)
    {
        if (expression is SimpleLambdaExpressionSyntax simpleLambda)
        {
            return ExtractMemberName(simpleLambda.Body);
        }
        if (expression is ParenthesizedLambdaExpressionSyntax parenLambda)
        {
            return ExtractMemberName(parenLambda.Body);
        }
        return null;
    }

    private static string? ExtractRootMemberName(ExpressionSyntax expression)
    {
        var body = expression switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Body,
            ParenthesizedLambdaExpressionSyntax paren => paren.Body,
            _ => null
        };

        var current = body as ExpressionSyntax;
        while (current is PostfixUnaryExpressionSyntax postfix)
        {
            current = postfix.Operand;
        }

        string? root = null;
        while (current is MemberAccessExpressionSyntax member)
        {
            root = member.Name.Identifier.ValueText;
            current = member.Expression;
            while (current is PostfixUnaryExpressionSyntax inner)
            {
                current = inner.Operand;
            }
        }

        return current is IdentifierNameSyntax ? root : null;
    }

    private static string? ExtractMemberName(CSharpSyntaxNode body)
    {
        if (body is MemberAccessExpressionSyntax mae)
        {
            return mae.Name.Identifier.ValueText;
        }
        return null;
    }

    private class ChainedCall
    {
        public string MethodName { get; }
        public string? DestinationPropertyName { get; }

        public ChainedCall(string methodName, string? destProp)
        {
            MethodName = methodName;
            DestinationPropertyName = destProp;
        }
    }
}
