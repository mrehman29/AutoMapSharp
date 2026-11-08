using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;

namespace AutoMapSharp.MigrationAnalyzer;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AutoMapperMigrationCodeFixProvider)), Shared]
public class AutoMapperMigrationCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(
            "AMSMIG001",
            "AMSMIG002",
            "AMSMIG003",
            "AMSMIG004",
            "AMSMIG005",
            "AMSMIG006",
            "AMSMIG007",
            "AMSMIG008");

    public sealed override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        foreach (var diagnostic in context.Diagnostics)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Migrate AutoMapper syntax to AutoMapSharp",
                    createChangedDocument: c => MigrateDocumentAsync(context.Document, root),
                    equivalenceKey: "MigrateToAutoMapSharp"),
                diagnostic);
        }
    }

    private static Task<Document> MigrateDocumentAsync(Document document, SyntaxNode root)
    {
        var originalText = root.ToFullString();
        var report = AutoMapperMigrator.MigrateSourceCode(originalText);
        var newSyntaxTree = CSharpSyntaxTree.ParseText(report.MigratedCode);
        var newRoot = newSyntaxTree.GetRoot();
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
