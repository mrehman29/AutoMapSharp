using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AutoMapSharp.MigrationAnalyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AutoMapSharp.Tests.Regression;

/// <summary>
/// F30: the migration analyzer matched AutoMapper APIs by name only, so unrelated APIs such as
/// EF Core's <c>Ignore()</c> or Mapster's <c>IMapper</c> were reported and broke builds.
/// </summary>
public class MigrationAnalyzerRegressionTests
{
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string code)
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location)
        };

        var compilation = CSharpCompilation.Create(
            "F30",
            new[] { CSharpSyntaxTree.ParseText(code) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new AutoMapperMigrationAnalyzer()))
            .GetAnalyzerDiagnosticsAsync(CancellationToken.None);
    }

    [Fact]
    [Trait("Finding", "F30")]
    public async Task F30_NonAutoMapperApisWithSameNames_AreNotReported()
    {
        const string code = @"
namespace OtherLibrary
{
    public class EntityBuilder { public EntityBuilder Ignore(string name) => this; }
    public class Query { public Query ProjectTo<T>() => this; public Query MapFrom(int x) => this; public Query ForMember(int x) => this; }
    public interface IMapper { }
    public abstract class Profile { }
    public class Registry { public Registry CreateMap<A, B>() => this; }
}

namespace App
{
    using OtherLibrary;

    public class UserProfile : Profile { }

    public class Config
    {
        public void Configure(EntityBuilder builder, Query query, IMapper mapper, Registry registry)
        {
            builder.Ignore(""Tag"");
            query.ProjectTo<int>().MapFrom(1).ForMember(2);
            registry.CreateMap<int, string>();
        }
    }
}";

        var diagnostics = await AnalyzeAsync(code);

        Assert.DoesNotContain(diagnostics, d => d.Id.StartsWith("AMSMIG"));
    }

    [Fact]
    [Trait("Finding", "F30")]
    public async Task F30_ResolvedAutoMapperApis_AreStillReported()
    {
        const string code = @"
namespace AutoMapper
{
    public interface IMapper { }
    public class MemberOptions { public void Ignore() { } }
    public abstract class Profile
    {
        protected MapExpression CreateMap<A, B>() => new MapExpression();
    }
    public class MapExpression { public MapExpression ForMember(int x, System.Action<MemberOptions> o) => this; }
}

namespace App
{
    using AutoMapper;

    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<int, string>().ForMember(1, o => o.Ignore());
        }

        public void Use(IMapper mapper) { }
    }
}";

        var diagnostics = await AnalyzeAsync(code);

        Assert.Contains(diagnostics, d => d.Id == "AMSMIG002");
        Assert.Contains(diagnostics, d => d.Id == "AMSMIG003");
        Assert.Contains(diagnostics, d => d.Id == "AMSMIG004");
        Assert.Contains(diagnostics, d => d.Id == "AMSMIG006");
        Assert.Contains(diagnostics, d => d.Id == "AMSMIG008");
    }

    [Fact]
    [Trait("Finding", "F30")]
    public void F30_Migrator_LeavesFilesWithoutAutoMapperUnchanged()
    {
        const string code = @"using Microsoft.EntityFrameworkCore;

public class ShopContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>().Ignore(o => o.Tag);
    }
}";

        var report = AutoMapperMigrator.MigrateSourceCode(code);

        Assert.False(report.WasModified);
        Assert.Equal(code, report.MigratedCode);
    }

    [Fact]
    [Trait("Finding", "F29")]
    public void F29_Migrator_MapsQueryableExtensionsToTheNamespaceThatHoldsProjectOnto()
    {
        const string code = @"using AutoMapper;
using AutoMapper.QueryableExtensions;

public class Query
{
    public object Run(System.Linq.IQueryable<Order> orders, IMapper mapper) => orders.ProjectTo<OrderDto>(mapper.ConfigurationProvider);
}";

        var report = AutoMapperMigrator.MigrateSourceCode(code);

        Assert.DoesNotContain("AutoMapSharp.Projections", report.MigratedCode);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(report.MigratedCode, @"using AutoMapSharp;"));
        Assert.Contains("orders.ProjectOnto<OrderDto>(mapper)", report.MigratedCode);
    }

    [Fact]
    [Trait("Finding", "F29")]
    public void F29_Migrator_RewritesConditionNullSubstituteResolversAndOnlyRenamesCalls()
    {
        const string code = @"using AutoMapper;

public class TaxResolver : IValueResolver<Order, OrderDto, decimal> { }

public class OrderProfile : Profile
{
    public string Condition { get; set; }
    public OrderProfile()
    {
        CreateMap<Order, OrderDto>()
            .ForMember(d => d.Tax, o => o.MapFrom<TaxResolver>())
            .ForMember(d => d.Note, o => o.NullSubstitute(""none""))
            .ForMember(d => d.Total, o => o.Condition(s => s.Total > 0));
    }
}";

        var migrated = AutoMapperMigrator.MigrateSourceCode(code).MigratedCode;

        Assert.Contains("o.MapWith<TaxResolver>()", migrated);
        Assert.Contains("o.Fallback(\"none\")", migrated);
        Assert.Contains("o.If(s => s.Total > 0)", migrated);
        Assert.Contains("IPropertyResolver<Order, OrderDto, decimal>", migrated);
        Assert.Contains("public string Condition { get; set; }", migrated);
    }

    /// <summary>
    /// The migration tool reported line 1 for every change inside a class that inherits Profile,
    /// because the base class was replaced before the class body was visited.
    /// </summary>
    [Fact]
    [Trait("Finding", "MigrationLineNumbers")]
    public void Migrator_ReportsTheRealLineOfEveryChange()
    {
        const string code = """
            using AutoMapper;

            public class OrderProfile : Profile
            {
                public OrderProfile()
                {
                    CreateMap<Order, OrderDto>()
                        .ForMember(d => d.Notes, o => o.Ignore())
                        .ForMember(d => d.Tax, o => o.MapFrom<TaxResolver>());
                }
            }

            public class Holder
            {
                public System.Collections.Generic.List<IMapper> Mappers { get; } = new();
            }
            """;

        var report = AutoMapperMigrator.MigrateSourceCode(code);

        int LineOf(string ruleId, string text) =>
            report.Changes.First(c => c.RuleId == ruleId && c.Description.Contains(text)).LineNumber;

        Assert.Equal(1, LineOf("AMSMIG001", "using AutoMapper"));
        Assert.Equal(3, LineOf("AMSMIG002", "Profile"));
        Assert.Equal(7, LineOf("AMSMIG003", "CreateMap"));
        Assert.Equal(8, LineOf("AMSMIG006", "Ignore"));
        Assert.Equal(9, LineOf("AMSMIG005", "MapWith"));
        Assert.Equal(15, LineOf("AMSMIG008", "IMapper"));
        Assert.Equal(new[] { 8, 9 }, report.Changes.Where(c => c.RuleId == "AMSMIG004").Select(c => c.LineNumber));

        // The rewrite itself is unchanged.
        Assert.Contains("public class OrderProfile : MappingModule", report.MigratedCode);
        Assert.Contains("DefineMap<Order, OrderDto>()", report.MigratedCode);
        Assert.Contains("o.MapWith<TaxResolver>()", report.MigratedCode);
        Assert.Contains("List<IMapEngine>", report.MigratedCode);
    }
}
