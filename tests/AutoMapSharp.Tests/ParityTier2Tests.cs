using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using AutoMapSharp.Collections;
using AutoMapSharp.Exceptions;
using AutoMapSharp.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoMapSharp.Tests;

/// <summary>
/// AutoMapper parity features from remediation Phase 14.
/// </summary>
public class ParityTier2Tests
{
    // 14.1 Projections -----------------------------------------------------------

    public abstract class Media { public int Id { get; set; } public string Title { get; set; } = ""; }
    public class Book : Media { public int Pages { get; set; } }
    public class Film : Media { public int Minutes { get; set; } }
    public abstract class MediaDto { public int Id { get; set; } public string Title { get; set; } = ""; }
    public class BookDto : MediaDto { public int Pages { get; set; } }
    public class FilmDto : MediaDto { public int Minutes { get; set; } }

    public class Category { public string Name { get; set; } = ""; public Category? Parent { get; set; } }
    public class CategoryDto { public string Name { get; set; } = ""; public CategoryDto? Parent { get; set; } }

    public class Shelf { public string Label { get; set; } = ""; public List<Media> Items { get; set; } = new(); public string? Note { get; set; } }
    public class ShelfDto { public string Label { get; set; } = ""; public List<MediaDto> Items { get; set; } = new(); public string Note { get; set; } = ""; }

    public class ProjectionModule : MappingModule
    {
        public ProjectionModule()
        {
            DefineMap<Media, MediaDto>().Include<Book, BookDto>().Include<Film, FilmDto>();
            DefineMap<Book, BookDto>();
            DefineMap<Film, FilmDto>();
            DefineMap<Category, CategoryDto>().MaxDepth(2);
            DefineMap<Shelf, ShelfDto>()
                .ForProperty(d => d.Note, o => o.Fallback("(empty)"))
                .ForProperty(d => d.Items, o => o.ExplicitExpansion());
        }
    }

    [Fact]
    public void T14_1_Projection_HandlesInheritanceWithTypeChecks()
    {
        var engine = new MapEngine(new ProjectionModule());
        var media = new List<Media> { new Book { Id = 1, Pages = 300 }, new Film { Id = 2, Minutes = 90 } }.AsQueryable();

        var result = media.ProjectOnto<MediaDto>(engine).ToList();

        Assert.Equal(300, Assert.IsType<BookDto>(result[0]).Pages);
        Assert.Equal(90, Assert.IsType<FilmDto>(result[1]).Minutes);
    }

    [Fact]
    public void T14_1_Projection_HonorsMaxDepth()
    {
        var engine = new MapEngine(new ProjectionModule());
        var categories = new[] { new Category { Name = "c", Parent = new Category { Name = "b", Parent = new Category { Name = "a" } } } }.AsQueryable();

        var result = categories.ProjectOnto<CategoryDto>(engine).Single();

        Assert.Equal("b", result.Parent!.Name);
        Assert.Null(result.Parent.Parent);
    }

    public class Converted { public string Name { get; set; } = ""; }
    public class ConverterModule : MappingModule
    {
        public ConverterModule()
        {
            DefineMap<Category, Converted>().ConvertUsing(c => new Converted { Name = c.Name });
            DefineMap<Book, CategoryDto>().ForProperty(d => d.Name, o => o.If(b => b.Pages > 0).MapSource(b => b.Title)).ForProperty(d => d.Parent, o => o.Skip());
        }
    }

    [Fact]
    public void T14_1_Projection_RejectsCodeThatCannotBeTranslated()
    {
        var engine = new MapEngine(new ConverterModule());

        var converter = Assert.Throws<AutoMapSharpConfigurationException>(() => new[] { new Category() }.AsQueryable().ProjectOnto<Converted>(engine).ToList());
        var condition = Assert.Throws<AutoMapSharpConfigurationException>(() => new[] { new Book() }.AsQueryable().ProjectOnto<CategoryDto>(engine).ToList());

        Assert.Contains("type converter", converter.Message);
        Assert.Contains("condition", condition.Message);
        Assert.Contains("'Name'", condition.Message);
    }

    // 14.2 Expression-based expansion -------------------------------------------

    [Fact]
    public void T14_2_ExplicitExpansion_ByExpression()
    {
        var engine = new MapEngine(new ProjectionModule());
        var shelves = new[] { new Shelf { Label = "s", Items = { new Book { Id = 1, Pages = 10 } } } }.AsQueryable();

        var collapsed = shelves.ProjectOnto<ShelfDto>(engine).Single();
        var expanded = shelves.ProjectOnto<ShelfDto>(engine, null, d => d.Items).Single();

        Assert.Empty(collapsed.Items);
        Assert.Single(expanded.Items);
        Assert.Equal("(empty)", expanded.Note);
    }

    // 14.3 Dictionaries and ExpandoObject ---------------------------------------

    public class Address { public string City { get; set; } = ""; }
    public class Person { public string Name { get; set; } = ""; public int Age { get; set; } public Address? Address { get; set; } public List<string> Tags { get; set; } = new(); }

    [Fact]
    public void T14_3_DictionaryAndExpando_MapToObjects()
    {
        var engine = new MapEngine();
        var dictionary = new Dictionary<string, object?>
        {
            ["name"] = "Ann",
            ["Age"] = "42",
            ["address"] = new Dictionary<string, object?> { ["City"] = "Oslo" },
            ["Tags"] = new[] { "a", "b" }
        };
        dynamic expando = new ExpandoObject();
        expando.Name = "Bo";
        expando.Age = 7;

        var fromDictionary = engine.Map<Dictionary<string, object?>, Person>(dictionary);
        var fromExpando = engine.Map<Person>((object)expando);

        Assert.Equal("Ann", fromDictionary.Name);
        Assert.Equal(42, fromDictionary.Age);
        Assert.Equal("Oslo", fromDictionary.Address!.City);
        Assert.Equal(new[] { "a", "b" }, fromDictionary.Tags);
        Assert.Equal("Bo", fromExpando.Name);
        Assert.Equal(7, fromExpando.Age);
    }

    [Fact]
    public void T14_3_Objects_MapToDictionaryAndExpando()
    {
        var engine = new MapEngine();
        var person = new Person { Name = "Ann", Age = 42 };

        var dictionary = engine.Map<Person, Dictionary<string, object?>>(person);
        IDictionary<string, object?> expando = engine.Map<Person, ExpandoObject>(person);

        Assert.Equal("Ann", dictionary["Name"]);
        Assert.Equal(42, dictionary["Age"]);
        Assert.Equal("Ann", expando["Name"]);
    }

    // 14.4 Source extension methods ---------------------------------------------

    public class Customer { public List<int> Orders { get; set; } = new() { 1, 2, 3 }; public string First { get; set; } = "Ada"; public string Last { get; set; } = "Lovelace"; }
    public class CustomerSummary { public int OrderCount { get; set; } public string FullName { get; set; } = ""; }

    public class ExtensionModule : MappingModule
    {
        public ExtensionModule()
        {
            IncludeSourceExtensionMethods(typeof(CustomerExtensions));
            DefineMap<Customer, CustomerSummary>();
        }
    }

    [Fact]
    public void T14_4_SourceExtensionMethods_ActAsMembers()
    {
        var engine = new MapEngine(new ExtensionModule());

        var summary = engine.Map<Customer, CustomerSummary>(new Customer());

        Assert.Equal(3, summary.OrderCount);
        Assert.Equal("Ada Lovelace", summary.FullName);
        engine.AssertConfigurationIsValid();
    }

    // 14.5 Attributes ------------------------------------------------------------

    [Fact]
    public void T14_5_AttributeMaps_And_MemberAttributes()
    {
        var config = new MappingConfiguration(cfg => cfg.AddAttributeMaps(typeof(ParityTier2Tests).Assembly));
        var engine = config.CreateEngine();

        var dto = engine.Map<AttributeSource, AttributeTarget>(new AttributeSource { Name = "Ann", Secret = "s", LegacyCode = "X9" });
        var back = engine.Map<AttributeTarget, AttributeSource>(new AttributeTarget { Name = "Bo" });

        Assert.Equal("Ann", dto.Name);
        Assert.Null(dto.Secret);
        Assert.Equal("X9", dto.Code);
        Assert.Equal("Bo", back.Name);
        config.AssertConfigurationIsValid();
    }

    [Fact]
    public void T14_5_MemberAttributes_ApplyToFluentMapsToo()
    {
        var engine = new MapEngine(cfg => cfg.DefineMap<AttributeSource, AttributeTarget>());

        var dto = engine.Map<AttributeSource, AttributeTarget>(new AttributeSource { Secret = "s", LegacyCode = "X9" });

        Assert.Null(dto.Secret);
        Assert.Equal("X9", dto.Code);
    }

    [Fact]
    public void T14_5_DependencyInjection_RegistersAttributeMaps()
    {
        var services = new ServiceCollection();

        services.AddAutoMapSharp(typeof(ParityTier2Tests).Assembly);

        Assert.Contains(services, d => d.ServiceType == typeof(MappingModule) && d.ImplementationInstance is AttributeMappingModule);
    }

    // 14.6 Execution plans ------------------------------------------------------

    [Fact]
    public void T14_6_BuildExecutionPlan_And_CompileMappings()
    {
        var engine = new MapEngine(new ExtensionModule(), new ProjectionModule());

        var plan = engine.BuildExecutionPlan(typeof(Customer), typeof(CustomerSummary));
        engine.CompileMappings();

        Assert.Equal(typeof(Customer), plan.Parameters[0].Type);
        Assert.Equal(typeof(CustomerSummary), plan.ReturnType);
        Assert.Contains(MemberNames(plan), n => n == nameof(CustomerSummary.OrderCount));
        Assert.Equal(3, engine.Map<Customer, CustomerSummary>(new Customer()).OrderCount);
    }

    private static List<string> MemberNames(Expression expression)
    {
        var names = new List<string>();
        new MemberCollector(names).Visit(expression);
        return names;
    }

    private sealed class MemberCollector : ExpressionVisitor
    {
        private readonly List<string> _names;
        public MemberCollector(List<string> names) { _names = names; }
        protected override Expression VisitMember(MemberExpression node) { _names.Add(node.Member.Name); return base.VisitMember(node); }
    }

    // 14.7 Collection merging ---------------------------------------------------

    public class LineDto { public int Id { get; set; } public int Quantity { get; set; } }
    public class OrderDto { public List<LineDto> Lines { get; set; } = new(); }
    public class Line { public int Id { get; set; } public int Quantity { get; set; } }
    public class Order { public List<Line> Lines { get; set; } = new(); }

    public class MergeModule : MappingModule
    {
        public MergeModule()
        {
            DefineMap<LineDto, Line>().EqualityComparison((dto, line) => dto.Id == line.Id);
            DefineMap<OrderDto, Order>();
        }
    }

    [Fact]
    public void T14_7_EqualityComparison_MergesExistingCollection()
    {
        var engine = new MapEngine(new MergeModule());
        var keep = new Line { Id = 1, Quantity = 1 };
        var removed = new Line { Id = 2, Quantity = 1 };
        var lines = new List<Line> { keep, removed };
        var order = new Order { Lines = lines };

        engine.Map(new OrderDto { Lines = { new LineDto { Id = 1, Quantity = 5 }, new LineDto { Id = 3, Quantity = 2 } } }, order);

        Assert.Same(lines, order.Lines);
        Assert.Same(keep, order.Lines[0]);
        Assert.Equal(5, keep.Quantity);
        Assert.DoesNotContain(removed, order.Lines);
        Assert.Equal(new[] { 1, 3 }, order.Lines.Select(l => l.Id));
    }
}

public static class CustomerExtensions
{
    public static int OrderCount(this ParityTier2Tests.Customer customer) => customer.Orders.Count;

    public static string GetFullName(this ParityTier2Tests.Customer customer) => customer.First + " " + customer.Last;
}

public class AttributeSource { public string Name { get; set; } = ""; public string? Secret { get; set; } public string LegacyCode { get; set; } = ""; }

[MapFrom(typeof(AttributeSource), ReverseMap = true)]
public class AttributeTarget
{
    public string Name { get; set; } = "";

    [SkipMap]
    public string? Secret { get; set; }

    [SourceMember(nameof(AttributeSource.LegacyCode))]
    public string Code { get; set; } = "";
}
