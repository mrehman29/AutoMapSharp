using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using AutoMapSharp.Projections;
using Xunit;

namespace AutoMapSharp.Tests.Regression;

public class ProjectionRegressionTests
{
    public class Item
    {
        public int UserId { get; set; }
        public int Value { get; set; }
    }

    public class Account
    {
        public int UserId { get; set; }
        public string? Nickname { get; set; }
        public List<Item> Items { get; set; } = new();
    }

    public class AccountDto
    {
        public int UserId { get; set; }
        public int MyItemCount { get; set; }
    }

    public class IsolatedAccountDto
    {
        public int MyItemCount { get; set; }
    }

    public class NicknameDto
    {
        public string? Nickname { get; set; }
    }

    public class Person { public string Name { get; set; } = ""; }
    public class PersonDto { public string? Name { get; set; } }

    public class TaxResolver : IPropertyResolver<Account, NicknameDto, string?>
    {
        public string? Resolve(Account source, NicknameDto destination, ResolutionContext context) => "resolved";
    }

    public class ParameterModule : MappingModule
    {
        public ParameterModule()
        {
            // Documented pattern: a local captured by the MapSource lambda acts as a projection parameter.
            int userId = 0;
            DefineMap<Account, AccountDto>()
                .ForProperty(d => d.MyItemCount, o => o.MapSource(s => s.Items.Count(i => i.UserId == userId)));
        }
    }

    public class IsolatedParameterModule : MappingModule
    {
        public IsolatedParameterModule()
        {
            int userId = 0;
            DefineMap<Account, IsolatedAccountDto>()
                .ForProperty(d => d.MyItemCount, o => o.MapSource(s => s.Items.Count(i => i.UserId == userId)));
        }
    }

    public class PlainPersonModule : MappingModule
    {
        public PlainPersonModule() { DefineMap<Person, PersonDto>(); }
    }

    public class SkipNameModule : MappingModule
    {
        public SkipNameModule()
        {
            DefineMap<Person, PersonDto>().ForProperty(d => d.Name, o => o.Skip());
        }
    }

    public class FallbackModule : MappingModule
    {
        public FallbackModule()
        {
            DefineMap<Account, NicknameDto>().ForProperty(d => d.Nickname, o => o.Fallback("anonymous"));
        }
    }

    public class ResolverModule : MappingModule
    {
        public ResolverModule()
        {
            DefineMap<Account, NicknameDto>().ForProperty(d => d.Nickname, o => o.MapWith<TaxResolver>());
        }
    }

    private static IQueryable<Account> Accounts() => new List<Account>
    {
        new()
        {
            UserId = 1,
            Items = new()
            {
                new() { UserId = 5, Value = 1 },
                new() { UserId = 7, Value = 2 },
                new() { UserId = 7, Value = 3 }
            }
        }
    }.AsQueryable();

    [Fact]
    [Trait("Finding", "F03")]
    public void F03_ParameterValues_AreNotCachedBetweenCalls()
    {
        var engine = new MapEngine(new ParameterModule());

        var forUser5 = Accounts().ProjectOnto<AccountDto>(engine, new { userId = 5 }).Single();
        var forUser7 = Accounts().ProjectOnto<AccountDto>(engine, new { userId = 7 }).Single();

        Assert.Equal(1, forUser5.MyItemCount);
        Assert.Equal(2, forUser7.MyItemCount);
    }

    [Fact]
    [Trait("Finding", "F03")]
    public void F03_ParameterValue_IsNotEmbeddedAsConstant()
    {
        // Uses its own type pair so no other test can have cached this projection first.
        var engine = new MapEngine(new IsolatedParameterModule());

        var query = Accounts().ProjectOnto<IsolatedAccountDto>(engine, new { userId = 98765 });
        var constants = new ConstantCollector();
        constants.Visit(query.Expression);

        Assert.DoesNotContain(constants.Values, v => v is int i && i == 98765);
    }

    [Fact]
    [Trait("Finding", "F04")]
    public void F04_EntityMemberWithSameNameAsParameter_IsNotReplaced()
    {
        var engine = new MapEngine(new ParameterModule());

        var query = Accounts().ProjectOnto<AccountDto>(engine, new { userId = 5 });

        Assert.Contains("i.UserId", query.Expression.ToString());
        Assert.Equal(1, query.Single().UserId);
    }

    [Fact]
    [Trait("Finding", "F05")]
    public void F05_ProjectionCache_IsNotSharedAcrossEngines()
    {
        var people = new[] { new Person { Name = "Ann" } }.AsQueryable();
        var plain = new MapEngine(new PlainPersonModule());
        var skipping = new MapEngine(new SkipNameModule());

        var fromPlain = people.ProjectOnto<PersonDto>(plain).Single();
        var fromSkipping = people.ProjectOnto<PersonDto>(skipping).Single();

        Assert.Equal("Ann", fromPlain.Name);
        Assert.Null(fromSkipping.Name);
    }

    [Fact]
    [Trait("Finding", "F28")]
    public void F28_Projection_AppliesFallback()
    {
        var engine = new MapEngine(new FallbackModule());
        var accounts = new[] { new Account { Nickname = null } }.AsQueryable();

        var result = accounts.ProjectOnto<NicknameDto>(engine).Single();

        Assert.Equal("anonymous", result.Nickname);
    }

    [Fact]
    [Trait("Finding", "F28")]
    public void F28_Projection_WithResolver_ThrowsClearError()
    {
        var engine = new MapEngine(new ResolverModule());
        var accounts = new[] { new Account { Nickname = "x" } }.AsQueryable();

        var ex = Assert.ThrowsAny<Exception>(() => accounts.ProjectOnto<NicknameDto>(engine).ToList());

        Assert.Contains("Nickname", ex.Message);
        Assert.Contains("resolver", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ConstantCollector : ExpressionVisitor
    {
        public List<object?> Values { get; } = new();

        protected override Expression VisitConstant(ConstantExpression node)
        {
            Values.Add(node.Value);
            return base.VisitConstant(node);
        }
    }
}
