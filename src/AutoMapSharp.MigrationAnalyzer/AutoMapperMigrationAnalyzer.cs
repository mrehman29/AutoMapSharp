using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AutoMapSharp.MigrationAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AutoMapperMigrationAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            MigrationDiagnosticDescriptors.UsingAutoMapper,
            MigrationDiagnosticDescriptors.InheritsProfile,
            MigrationDiagnosticDescriptors.CreateMapUsage,
            MigrationDiagnosticDescriptors.ForMemberUsage,
            MigrationDiagnosticDescriptors.MapFromUsage,
            MigrationDiagnosticDescriptors.IgnoreUsage,
            MigrationDiagnosticDescriptors.ProjectToUsage,
            MigrationDiagnosticDescriptors.IMapperUsage);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeUsingDirective, SyntaxKind.UsingDirective);
        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeIdentifier, SyntaxKind.IdentifierName);
    }

    private static void AnalyzeUsingDirective(SyntaxNodeAnalysisContext context)
    {
        var usingDirective = (UsingDirectiveSyntax)context.Node;
        var name = usingDirective.Name?.ToString();
        if (name == "AutoMapper" || name == "AutoMapper.QueryableExtensions")
        {
            var diag = Diagnostic.Create(
                MigrationDiagnosticDescriptors.UsingAutoMapper,
                usingDirective.GetLocation());
            context.ReportDiagnostic(diag);
        }
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        if (classDeclaration.BaseList == null) return;

        foreach (var baseType in classDeclaration.BaseList.Types)
        {
            var typeName = baseType.Type.ToString();
            if ((typeName == "Profile" || typeName == "AutoMapper.Profile") && IsAutoMapperApi(context, baseType.Type))
            {
                var diag = Diagnostic.Create(
                    MigrationDiagnosticDescriptors.InheritsProfile,
                    baseType.GetLocation(),
                    classDeclaration.Identifier.Text);
                context.ReportDiagnostic(diag);
            }
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        SimpleNameSyntax? methodNameSyntax = null;

        if (invocation.Expression is MemberAccessExpressionSyntax mae)
        {
            methodNameSyntax = mae.Name;
        }
        else if (invocation.Expression is SimpleNameSyntax simpleName)
        {
            methodNameSyntax = simpleName;
        }

        if (methodNameSyntax == null) return;

        var methodName = methodNameSyntax.Identifier.Text;
        if (methodName is not ("CreateMap" or "ForMember" or "MapFrom" or "Ignore" or "ProjectTo"))
        {
            return;
        }

        if (!IsAutoMapperApi(context, invocation))
        {
            return;
        }

        switch (methodName)
        {
            case "CreateMap":
                string tSource = "TSource";
                string tDest = "TDestination";
                if (methodNameSyntax is GenericNameSyntax gn && gn.TypeArgumentList.Arguments.Count == 2)
                {
                    tSource = gn.TypeArgumentList.Arguments[0].ToString();
                    tDest = gn.TypeArgumentList.Arguments[1].ToString();
                }
                context.ReportDiagnostic(Diagnostic.Create(
                    MigrationDiagnosticDescriptors.CreateMapUsage,
                    methodNameSyntax.GetLocation(),
                    tSource,
                    tDest));
                break;

            case "ForMember":
                context.ReportDiagnostic(Diagnostic.Create(
                    MigrationDiagnosticDescriptors.ForMemberUsage,
                    methodNameSyntax.GetLocation()));
                break;

            case "MapFrom":
                context.ReportDiagnostic(Diagnostic.Create(
                    MigrationDiagnosticDescriptors.MapFromUsage,
                    methodNameSyntax.GetLocation()));
                break;

            case "Ignore":
                context.ReportDiagnostic(Diagnostic.Create(
                    MigrationDiagnosticDescriptors.IgnoreUsage,
                    methodNameSyntax.GetLocation()));
                break;

            case "ProjectTo":
                string targetType = "TDestination";
                if (methodNameSyntax is GenericNameSyntax projGn && projGn.TypeArgumentList.Arguments.Count > 0)
                {
                    targetType = projGn.TypeArgumentList.Arguments[0].ToString();
                }
                context.ReportDiagnostic(Diagnostic.Create(
                    MigrationDiagnosticDescriptors.ProjectToUsage,
                    methodNameSyntax.GetLocation(),
                    targetType));
                break;
        }
    }

    private static void AnalyzeIdentifier(SyntaxNodeAnalysisContext context)
    {
        var identifier = (IdentifierNameSyntax)context.Node;
        if (identifier.Identifier.Text == "IMapper")
        {
            if (identifier.FirstAncestorOrSelf<UsingDirectiveSyntax>() != null) return;
            if (!IsAutoMapperApi(context, identifier)) return;

            context.ReportDiagnostic(Diagnostic.Create(
                MigrationDiagnosticDescriptors.IMapperUsage,
                identifier.GetLocation()));
        }
    }

    /// <summary>
    /// Decides whether a node refers to AutoMapper rather than an unrelated API with the same name,
    /// such as EF Core's <c>Ignore()</c> or Mapster's <c>IMapper</c>.
    /// </summary>
    /// <remarks>
    /// When the symbol resolves, it must live in the <c>AutoMapper</c> namespace.
    /// When it does not resolve, which is normal once the AutoMapper package has been removed mid-migration,
    /// the file must import an <c>AutoMapper</c> namespace.
    /// </remarks>
    private static bool IsAutoMapperApi(SyntaxNodeAnalysisContext context, SyntaxNode node)
    {
        var info = context.SemanticModel.GetSymbolInfo(node, context.CancellationToken);
        var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
        if (symbol != null)
        {
            return IsInAutoMapperNamespace(symbol.ContainingNamespace);
        }

        return FileImportsAutoMapper(node.SyntaxTree.GetRoot(context.CancellationToken));
    }

    private static bool IsInAutoMapperNamespace(INamespaceSymbol? ns)
    {
        while (ns != null && !ns.IsGlobalNamespace)
        {
            if (ns.ContainingNamespace?.IsGlobalNamespace == true)
            {
                return ns.Name == "AutoMapper";
            }

            ns = ns.ContainingNamespace;
        }

        return false;
    }

    private static bool FileImportsAutoMapper(SyntaxNode root)
    {
        foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            var name = directive.Name?.ToString();
            if (name == "AutoMapper" || (name != null && name.StartsWith("AutoMapper.", System.StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }
}
