using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace AutoMapSharp.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnmappedPropertyCodeFixProvider)), Shared]
public class UnmappedPropertyCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("AMS001");

    public sealed override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        foreach (var diagnostic in context.Diagnostics)
        {
            if (diagnostic.Id != "AMS001") continue;

            if (!diagnostic.Properties.TryGetValue("PropertyName", out var propertyName) || string.IsNullOrEmpty(propertyName))
            {
                continue;
            }

            var node = root.FindNode(diagnostic.Location.SourceSpan);
            var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
            if (invocation == null) continue;

            var topInvocation = invocation;
            while (topInvocation.Parent is MemberAccessExpressionSyntax mae && mae.Parent is InvocationExpressionSyntax parentInv)
            {
                topInvocation = parentInv;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Ignore property '{propertyName}' using .Skip()",
                    createChangedDocument: c => AddForPropertyCallAsync(context.Document, root, topInvocation, propertyName!, "opt => opt.Skip()", c),
                    equivalenceKey: $"Skip_{propertyName}"),
                diagnostic);

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Map property '{propertyName}' using .MapSource()",
                    createChangedDocument: c => AddForPropertyCallAsync(context.Document, root, topInvocation, propertyName!, $"opt => opt.MapSource(src => src.{propertyName})", c),
                    equivalenceKey: $"MapSource_{propertyName}"),
                diagnostic);
        }
    }

    private static Task<Document> AddForPropertyCallAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax topInvocation,
        string propertyName,
        string optionLambda,
        CancellationToken cancellationToken)
    {
        var codeToAppend = $"\n            .ForProperty(dest => dest.{propertyName}, {optionLambda})";
        var parsedChain = SyntaxFactory.ParseExpression(topInvocation.ToString() + codeToAppend)
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = root.ReplaceNode(topInvocation, parsedChain);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
