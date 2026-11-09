using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoMapSharp.MigrationAnalyzer;

public class MigrationItem
{
    public string RuleId { get; set; } = "";
    public string Description { get; set; } = "";
    public int LineNumber { get; set; }
}

public class MigrationReport
{
    public string OriginalCode { get; set; } = "";
    public string MigratedCode { get; set; } = "";
    public List<MigrationItem> Changes { get; set; } = new();
    public int TotalReplacements => Changes.Count;
    public bool WasModified => Changes.Count > 0;
}

public static class AutoMapperMigrator
{
    public static MigrationReport MigrateSourceCode(string originalCsharpCode)
    {
        var report = new MigrationReport
        {
            OriginalCode = originalCsharpCode
        };

        if (string.IsNullOrWhiteSpace(originalCsharpCode))
        {
            report.MigratedCode = originalCsharpCode;
            return report;
        }

        var tree = CSharpSyntaxTree.ParseText(originalCsharpCode);
        var root = tree.GetRoot();

        // Names are matched without a compilation, so only touch files that import AutoMapper
        // (otherwise EF Core's Ignore() would be renamed too).
        if (!root.DescendantNodes().OfType<UsingDirectiveSyntax>().Any(u =>
                u.Name?.ToString() == "AutoMapper" ||
                (u.Name?.ToString().StartsWith("AutoMapper.", StringComparison.Ordinal) ?? false)))
        {
            report.MigratedCode = originalCsharpCode;
            return report;
        }

        var rewriter = new MigrationSyntaxRewriter(report);
        var newRoot = RemoveDuplicateUsings(rewriter.Visit(root));

        report.MigratedCode = newRoot.ToFullString();
        return report;
    }

    private static SyntaxNode RemoveDuplicateUsings(SyntaxNode root)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = root.DescendantNodes().OfType<UsingDirectiveSyntax>()
            .Where(u => u.Parent is CompilationUnitSyntax && !seen.Add(u.ToString().Trim()))
            .ToList();
        return duplicates.Count == 0 ? root : root.RemoveNodes(duplicates, SyntaxRemoveOptions.KeepNoTrivia)!;
    }

    private class MigrationSyntaxRewriter : CSharpSyntaxRewriter
    {
        private readonly MigrationReport _report;

        public MigrationSyntaxRewriter(MigrationReport report)
        {
            _report = report;
        }

        public override SyntaxNode? VisitUsingDirective(UsingDirectiveSyntax node)
        {
            var text = node.Name?.ToString();
            if (text == "AutoMapper")
            {
                RecordChange("AMSMIG001", "Replaced 'using AutoMapper;' with 'using AutoMapSharp;'", node);
                return node.WithName(SyntaxFactory.ParseName("AutoMapSharp").WithTriviaFrom(node.Name!));
            }
            if (text == "AutoMapper.QueryableExtensions")
            {
                RecordChange("AMSMIG001", "Replaced 'using AutoMapper.QueryableExtensions;' with 'using AutoMapSharp;'", node);
                return node.WithName(SyntaxFactory.ParseName("AutoMapSharp").WithTriviaFrom(node.Name!));
            }
            return base.VisitUsingDirective(node);
        }

        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var isProfile = new List<bool>();
            foreach (var baseType in node.BaseList?.Types ?? default)
            {
                var name = baseType.Type.ToString();
                var match = name == "Profile" || name == "AutoMapper.Profile";
                isProfile.Add(match);
                if (match)
                {
                    RecordChange("AMSMIG002", $"Class '{node.Identifier.Text}' base 'Profile' replaced with 'MappingModule'", baseType);
                }
            }

            // Visit before swapping the base type, otherwise line numbers inside the class are lost.
            var visited = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
            if (visited.BaseList == null || !isProfile.Contains(true))
            {
                return visited;
            }

            var types = visited.BaseList.Types;
            var replaced = types.Select((baseType, i) => isProfile[i]
                ? SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("MappingModule")).WithTriviaFrom(baseType)
                : baseType);
            return visited.WithBaseList(visited.BaseList.WithTypes(SyntaxFactory.SeparatedList<BaseTypeSyntax>(replaced, types.GetSeparators())));
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            var id = node.Identifier.Text;

            if (id == "IMapper" && node.FirstAncestorOrSelf<UsingDirectiveSyntax>() == null)
            {
                RecordChange("AMSMIG008", "Replaced 'IMapper' with 'IMapEngine'", node);
                return SyntaxFactory.IdentifierName("IMapEngine").WithTriviaFrom(node);
            }

            // Only rename invocations, so a property called Ignore or Condition is left alone.
            if (!IsInvokedName(node))
            {
                return base.VisitIdentifierName(node);
            }

            var replacement = id switch
            {
                "CreateMap" => ("AMSMIG003", "DefineMap"),
                "ForMember" => ("AMSMIG004", "ForProperty"),
                "MapFrom" => ("AMSMIG005", "MapSource"),
                "Ignore" => ("AMSMIG006", "Skip"),
                "ProjectTo" => ("AMSMIG007", "ProjectOnto"),
                "Condition" => ("AMSMIG009", "If"),
                "NullSubstitute" => ("AMSMIG010", "Fallback"),
                _ => default
            };

            if (replacement.Item2 == null)
            {
                return base.VisitIdentifierName(node);
            }

            RecordChange(replacement.Item1, $"Replaced '{id}' with '{replacement.Item2}'", node);
            return SyntaxFactory.IdentifierName(replacement.Item2).WithTriviaFrom(node);
        }

        public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
        {
            var id = node.Identifier.Text;
            if (id == "CreateMap" && IsInvokedName(node))
            {
                RecordChange("AMSMIG003", "Replaced 'CreateMap<...>' with 'DefineMap<...>'", node);
                return Rename(node, "DefineMap");
            }

            if (id == "ProjectTo" && IsInvokedName(node))
            {
                RecordChange("AMSMIG007", "Replaced 'ProjectTo<...>' with 'ProjectOnto<...>'", node);
                return Rename(node, "ProjectOnto");
            }

            if (id == "MapFrom" && IsInvokedName(node) && node.TypeArgumentList.Arguments.Count == 1 &&
                node.Parent is MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0 } })
            {
                RecordChange("AMSMIG005", "Replaced 'MapFrom<TResolver>()' with 'MapWith<TResolver>()'", node);
                return Rename(node, "MapWith");
            }

            if (id == "IValueResolver")
            {
                RecordChange("AMSMIG011", "Replaced 'IValueResolver' with 'IPropertyResolver'. Remove the destMember parameter from Resolve: AutoMapSharp passes (source, destination, context)", node);
                return Rename(node, "IPropertyResolver");
            }

            return base.VisitGenericName(node);
        }

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (node.Name.Identifier.Text == "ConfigurationProvider" && node.Parent is ArgumentSyntax)
            {
                RecordChange("AMSMIG007", "Replaced 'mapper.ConfigurationProvider' with the engine itself", node);
                return Visit(node.Expression)!.WithTriviaFrom(node);
            }

            return base.VisitMemberAccessExpression(node);
        }

        private SyntaxNode Rename(GenericNameSyntax node, string newName)
        {
            var visited = (GenericNameSyntax)base.VisitGenericName(node)!;
            return visited.WithIdentifier(SyntaxFactory.Identifier(newName).WithTriviaFrom(visited.Identifier));
        }

        private static bool IsInvokedName(SimpleNameSyntax node) =>
            node.Parent is MemberAccessExpressionSyntax access && access.Name == node && access.Parent is InvocationExpressionSyntax ||
            node.Parent is InvocationExpressionSyntax invocation && invocation.Expression == node;

        private void RecordChange(string ruleId, string description, SyntaxNode node)
        {
            var lineSpan = node.GetLocation().GetLineSpan();
            _report.Changes.Add(new MigrationItem
            {
                RuleId = ruleId,
                Description = description,
                LineNumber = lineSpan.StartLinePosition.Line + 1
            });
        }
    }
}
