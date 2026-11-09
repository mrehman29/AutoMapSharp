using System.Diagnostics;
using Xunit;

namespace AutoMapSharp.Tests.Regression;

public class FlatteningRegressionTests
{
    public class Source
    {
        public string Name { get; set; } = "";
    }

    public class Destination
    {
        public string Name { get; set; } = "";
        public string? Type { get; set; }
        public string? HashCode { get; set; }
        public string? String { get; set; }
    }

    public class ObjectMethodModule : MappingModule
    {
        public ObjectMethodModule() { DefineMap<Source, Destination>(); }
    }

    [Fact]
    [Trait("Finding", "F06")]
    public void F06_ObjectMethods_AreNotUsedAsSourceMembers_AtRuntime()
    {
        var engine = new MapEngine(new ObjectMethodModule());

        var result = engine.Map<Source, Destination>(new Source { Name = "x" });

        Assert.Equal("x", result.Name);
        Assert.Null(result.Type);
        Assert.Null(result.HashCode);
        Assert.Null(result.String);
    }

    [Fact]
    [Trait("Finding", "F06")]
    public void F06_ObjectMethods_AreReportedUnmapped_ByValidation()
    {
        var engine = new MapEngine(new ObjectMethodModule());

        var result = engine.ValidateConfiguration();

        var error = Assert.Single(result.Errors);
        Assert.Contains(nameof(Destination.Type), error.UnmappedProperties);
        Assert.Contains(nameof(Destination.HashCode), error.UnmappedProperties);
        Assert.Contains(nameof(Destination.String), error.UnmappedProperties);
    }
}

[Collection(NonParallelCollection.Name)]
public class FlatteningPerformanceRegressionTests
{
    public class City { public string Name { get; set; } = "Oslo"; }
    public class Address { public City City { get; set; } = new(); public string Street { get; set; } = "Main"; }
    public class Customer { public Address Address { get; set; } = new(); public string Email { get; set; } = "a@b.c"; }
    public class Order { public Customer Customer { get; set; } = new(); public int Id { get; set; } = 1; }

    public class OrderDto
    {
        public int Id { get; set; }
        public string CustomerEmail { get; set; } = "";
        public string CustomerAddressStreet { get; set; } = "";
        public string CustomerAddressCityName { get; set; } = "";
    }

    public class FlatteningModule : MappingModule
    {
        public FlatteningModule() { DefineMap<Order, OrderDto>(); }
    }

    [Fact]
    [Trait("Finding", "F23")]
    public void F23_FlatteningPaths_AreResolvedOnce_NotPerCall()
    {
        var engine = new MapEngine(new FlatteningModule());
        var order = new Order();
        for (int i = 0; i < 200; i++) engine.Map<Order, OrderDto>(order);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10_000; i++) engine.Map<Order, OrderDto>(order);
        sw.Stop();

        // A compiled plan needs a few ms for this. Per-call reflection took about 140 ms when this test was written.
        Assert.True(sw.ElapsedMilliseconds < 50, $"10,000 flattened maps took {sw.ElapsedMilliseconds} ms.");
    }
}
