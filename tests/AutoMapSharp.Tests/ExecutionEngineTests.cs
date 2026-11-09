using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapSharp.Exceptions;
using Xunit;

namespace AutoMapSharp.Tests;

/// <summary>
/// Behavior of the compiled execution-plan engine.
/// </summary>
public class ExecutionEngineTests
{
    public class Address { public string City { get; set; } = ""; public string Street { get; set; } = ""; }
    public class Customer { public string Name { get; set; } = ""; public string Email { get; set; } = ""; public Address? Address { get; set; } }
    public class Order { public int Id { get; set; } public Customer? Customer { get; set; } }

    public class OrderForm
    {
        public int Id { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? City { get; set; }
        public bool HasAddress { get; set; }
    }

    public class ForPathModule : MappingModule
    {
        public ForPathModule()
        {
            DefineMap<OrderForm, Order>()
                .ForProperty(d => d.Customer, o => o.Skip())
                .ForPath(d => d.Customer!.Name, o => o.MapSource(s => s.CustomerName))
                .ForPath(d => d.Customer!.Email, o => o.Fallback("unknown@example.com").MapSource(s => s.CustomerEmail))
                .ForPath(d => d.Customer!.Address!.City, o => o.If(s => s.HasAddress).MapSource(s => s.City))
                .ForPath(d => d.Customer!.Address!.Street, o => o.If(s => s.HasAddress).UseValue("Main St"));
        }
    }

    [Fact]
    public void ForPath_CreatesMissingIntermediateObjects()
    {
        var engine = new MapEngine(new ForPathModule());

        var order = engine.Map<OrderForm, Order>(new OrderForm { Id = 1, CustomerName = "Ann", City = "Oslo", HasAddress = true });

        Assert.Equal("Ann", order.Customer!.Name);
        Assert.Equal("unknown@example.com", order.Customer.Email);
        Assert.Equal("Oslo", order.Customer.Address!.City);
        Assert.Equal("Main St", order.Customer.Address.Street);
    }

    [Fact]
    public void ForPath_Condition_SkipsPathAndDoesNotCreateIntermediate()
    {
        var engine = new MapEngine(new ForPathModule());

        var order = engine.Map<OrderForm, Order>(new OrderForm { Id = 1, CustomerName = "Ann", HasAddress = false });

        Assert.NotNull(order.Customer);
        Assert.Null(order.Customer!.Address);
    }

    [Fact]
    public void ForPath_KeepsExistingIntermediateObject()
    {
        var engine = new MapEngine(new ForPathModule());
        var customer = new Customer { Name = "Old", Email = "old@example.com" };
        var order = new Order { Id = 1, Customer = customer };

        engine.Map(new OrderForm { Id = 1, CustomerName = "New", CustomerEmail = "new@example.com" }, order);

        Assert.Same(customer, order.Customer);
        Assert.Equal("New", customer.Name);
        Assert.Equal("new@example.com", customer.Email);
    }

    public class TopLevelForPathModule : MappingModule
    {
        public TopLevelForPathModule()
        {
            DefineMap<OrderForm, Order>().ForPath(d => d.Id, o => o.UseValue(5));
        }
    }

    [Fact]
    public void ForPath_OnTopLevelMember_PointsToForProperty()
    {
        var ex = Assert.Throws<AutoMapSharpConfigurationException>(() => new TopLevelForPathModule());
        Assert.Contains("ForProperty", ex.Message);
    }

    public class OrderSummary
    {
        public int Id { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? CustomerAddressCity { get; set; }
    }

    public class SummaryModule : MappingModule
    {
        public SummaryModule()
        {
            DefineMap<Order, OrderSummary>().ReverseMap();
        }
    }

    [Fact]
    public void ReverseMap_UnflattensConventionMatchedMembers()
    {
        var engine = new MapEngine(new SummaryModule());

        var order = engine.Map<OrderSummary, Order>(new OrderSummary
        {
            Id = 9,
            CustomerName = "Ann",
            CustomerEmail = "ann@example.com",
            CustomerAddressCity = "Oslo"
        });

        Assert.Equal(9, order.Id);
        Assert.Equal("Ann", order.Customer!.Name);
        Assert.Equal("ann@example.com", order.Customer.Email);
        Assert.Equal("Oslo", order.Customer.Address!.City);
    }

    [Fact]
    public void ReverseMap_ForwardDirection_StillFlattens()
    {
        var engine = new MapEngine(new SummaryModule());
        var order = new Order { Id = 9, Customer = new Customer { Name = "Ann", Address = new Address { City = "Oslo" } } };

        var summary = engine.Map<Order, OrderSummary>(order);

        Assert.Equal("Ann", summary.CustomerName);
        Assert.Equal("Oslo", summary.CustomerAddressCity);
    }

    public enum Status { Draft, Published }

    public class ConversionSource
    {
        public string StatusName { get; set; } = "published";
        public Status StatusEnum { get; set; } = Status.Published;
        public int StatusNumber { get; set; } = 1;
        public string Number { get; set; } = "42";
        public int Count { get; set; } = 7;
        public double Rounded { get; set; } = 2.5;
        public string Id { get; set; } = "6f9619ff-8b86-d011-b42d-00c04fc964ff";
        public DateTime When { get; set; } = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        public decimal Price { get; set; } = 1234.5m;
    }

    public class ConversionDestination
    {
        public Status StatusName { get; set; }
        public string? StatusEnum { get; set; }
        public Status StatusNumber { get; set; }
        public int Number { get; set; }
        public string? Count { get; set; }
        public int Rounded { get; set; }
        public Guid Id { get; set; }
        public DateTimeOffset When { get; set; }
        public string? Price { get; set; }
    }

    public class ConversionModule : MappingModule
    {
        public ConversionModule() { DefineMap<ConversionSource, ConversionDestination>(); }
    }

    [Fact]
    public void Conversions_FollowSystemConvertRules()
    {
        var engine = new MapEngine(new ConversionModule());

        var result = engine.Map<ConversionSource, ConversionDestination>(new ConversionSource());

        Assert.Equal(Status.Published, result.StatusName);
        Assert.Equal("Published", result.StatusEnum);
        Assert.Equal(Status.Published, result.StatusNumber);
        Assert.Equal(42, result.Number);
        Assert.Equal("7", result.Count);
        Assert.Equal(2, result.Rounded); // Convert.ToInt32 rounds half to even, like Convert.ChangeType.
        Assert.Equal(new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff"), result.Id);
        Assert.Equal(new DateTimeOffset(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)), result.When);
        Assert.Equal("1234.5", result.Price); // Invariant culture.
    }

    public class BadNumber { public string Number { get; set; } = "not a number"; }
    public class NumberDto { public int Number { get; set; } }

    public class BadNumberModule : MappingModule
    {
        public BadNumberModule() { DefineMap<BadNumber, NumberDto>(); }
    }

    [Fact]
    public void FailedConversion_NamesTheMember()
    {
        var engine = new MapEngine(new BadNumberModule());

        var ex = Assert.Throws<AutoMapSharpMappingException>(() => engine.Map<BadNumber, NumberDto>(new BadNumber()));

        Assert.Contains("'Number'", ex.Message);
        Assert.IsType<FormatException>(ex.InnerException);
    }

    public abstract class Shape { public string Name { get; set; } = ""; }
    public class Circle : Shape { public double Radius { get; set; } }
    public class Square : Shape { public double Side { get; set; } }
    public abstract class ShapeDto { public string Name { get; set; } = ""; }
    public class CircleDto : ShapeDto { public double Radius { get; set; } }
    public class SquareDto : ShapeDto { public double Side { get; set; } }

    public class Drawing
    {
        public List<Shape> Shapes { get; set; } = new();
        public Dictionary<string, Shape> Named { get; set; } = new();
    }

    public class DrawingDto
    {
        public IEnumerable<ShapeDto> Shapes { get; set; } = Array.Empty<ShapeDto>();
        public IReadOnlyDictionary<string, ShapeDto> Named { get; set; } = new Dictionary<string, ShapeDto>();
    }

    public class ShapeModule : MappingModule
    {
        public ShapeModule()
        {
            DefineMap<Shape, ShapeDto>().Include<Circle, CircleDto>().Include<Square, SquareDto>();
            DefineMap<Circle, CircleDto>().IncludeBase<Shape, ShapeDto>();
            DefineMap<Square, SquareDto>().IncludeBase<Shape, ShapeDto>();
            DefineMap<Drawing, DrawingDto>();
        }
    }

    [Fact]
    public void Collections_DispatchEachElementToItsDerivedMap()
    {
        var engine = new MapEngine(new ShapeModule());
        var drawing = new Drawing
        {
            Shapes = { new Circle { Name = "c", Radius = 2 }, new Square { Name = "s", Side = 3 } },
            Named = { ["big"] = new Circle { Name = "big", Radius = 10 } }
        };

        var dto = engine.Map<Drawing, DrawingDto>(drawing);

        var shapes = dto.Shapes.ToList();
        Assert.Equal(2, ((CircleDto)shapes[0]).Radius);
        Assert.Equal(3, ((SquareDto)shapes[1]).Side);
        Assert.Equal(10, Assert.IsType<CircleDto>(dto.Named["big"]).Radius);
    }

    public class TreeNode { public string Name { get; set; } = ""; public TreeNode? Child { get; set; } }
    public class TreeNodeDto { public string Name { get; set; } = ""; public TreeNodeDto? Child { get; set; } }

    public class DepthModule : MappingModule
    {
        public DepthModule() { DefineMap<TreeNode, TreeNodeDto>().MaxDepth(2); }
    }

    [Fact]
    public void MaxDepth_StopsAtConfiguredLevel()
    {
        var engine = new MapEngine(new DepthModule());
        var tree = new TreeNode { Name = "1", Child = new TreeNode { Name = "2", Child = new TreeNode { Name = "3" } } };

        var dto = engine.Map<TreeNode, TreeNodeDto>(tree);

        Assert.Equal("2", dto.Child!.Name);
        Assert.Null(dto.Child.Child);
    }

    public readonly struct Money
    {
        public Money(decimal amount, string currency) { Amount = amount; Currency = currency; }
        public decimal Amount { get; }
        public string Currency { get; }
    }

    public class PriceSource { public decimal Amount { get; set; } public string Currency { get; set; } = ""; }
    public class Product { public string Name { get; set; } = ""; public PriceSource Price { get; set; } = new(); }
    public class ProductDto { public string Name { get; set; } = ""; public Money Price { get; set; } }

    public class StructModule : MappingModule
    {
        public StructModule()
        {
            DefineMap<PriceSource, Money>();
            DefineMap<Product, ProductDto>();
        }
    }

    [Fact]
    public void ReadonlyStructDestination_IsBuiltThroughItsConstructor()
    {
        var engine = new MapEngine(new StructModule());

        var dto = engine.Map<Product, ProductDto>(new Product { Name = "p", Price = new PriceSource { Amount = 9.5m, Currency = "EUR" } });

        Assert.Equal(9.5m, dto.Price.Amount);
        Assert.Equal("EUR", dto.Price.Currency);
    }

    public class CustomerDto { public string Name { get; set; } = ""; public AddressDto? Address { get; set; } }
    public class AddressDto { public string City { get; set; } = ""; }

    public class ExistingModule : MappingModule
    {
        public ExistingModule()
        {
            DefineMap<Customer, CustomerDto>();
            DefineMap<Address, AddressDto>();
        }
    }

    [Fact]
    public void MapOntoExisting_UpdatesNestedObjectInPlace()
    {
        var engine = new MapEngine(new ExistingModule());
        var address = new AddressDto { City = "Old" };
        var target = new CustomerDto { Address = address };

        engine.Map(new Customer { Name = "Ann", Address = new Address { City = "Oslo" } }, target);

        Assert.Same(address, target.Address);
        Assert.Equal("Oslo", address.City);
    }

    public class CallbackModule : MappingModule
    {
        public CallbackModule()
        {
            DefineMap<Customer, CustomerDto>().BeforeMap((s, d) => throw new InvalidOperationException("boom"));
        }
    }

    [Fact]
    public void ExceptionInBeforeMap_IsReportedAsCallbackError()
    {
        var engine = new MapEngine(new CallbackModule());

        var ex = Assert.Throws<AutoMapSharpMappingException>(() => engine.Map<Customer, CustomerDto>(new Customer()));

        Assert.Contains("BeforeMap", ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void SameModuleInstances_CanBuildSeveralEngines()
    {
        var module = new ShapeModule();

        var first = new MapEngine(module);
        var second = new MapEngine(module);

        Assert.IsType<CircleDto>(first.Map<Shape, ShapeDto>(new Circle()));
        Assert.IsType<SquareDto>(second.Map<Shape, ShapeDto>(new Square()));
        Assert.Equal(first.FindDescriptor(typeof(Circle), typeof(CircleDto))!.PropertyRules.Count,
                     second.FindDescriptor(typeof(Circle), typeof(CircleDto))!.PropertyRules.Count);
    }

    [Fact]
    public async Task ParallelFirstMaps_OfManyTypePairs_AreCorrect()
    {
        var engine = new MapEngine(new ShapeModule(), new ConversionModule(), new SummaryModule(), new ExistingModule());

        var tasks = Enumerable.Range(0, 200).Select(i => Task.Run(() =>
        {
            switch (i % 4)
            {
                case 0: return engine.Map<Shape, ShapeDto>(new Circle { Name = "c" + i }).Name == "c" + i;
                case 1: return engine.Map<ConversionSource, ConversionDestination>(new ConversionSource()).Number == 42;
                case 2: return engine.Map<OrderSummary, Order>(new OrderSummary { CustomerName = "n" + i }).Customer!.Name == "n" + i;
                default: return engine.Map<Customer, CustomerDto>(new Customer { Name = "x" + i }).Name == "x" + i;
            }
        }));

        var results = await Task.WhenAll(tasks);

        Assert.All(results, Assert.True);
    }

    [Fact]
    public void UntypedOverloads_UseRuntimeType()
    {
        var engine = new MapEngine(new ShapeModule());
        object circle = new Circle { Name = "c", Radius = 1 };

        var viaGeneric = engine.Map<ShapeDto>(circle);
        var viaTypes = engine.Map(circle, typeof(Shape), typeof(ShapeDto));

        Assert.IsType<CircleDto>(viaGeneric);
        Assert.IsType<CircleDto>(viaTypes);
    }
}
