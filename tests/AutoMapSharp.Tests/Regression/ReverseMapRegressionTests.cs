using AutoMapSharp.Exceptions;
using Xunit;

namespace AutoMapSharp.Tests.Regression;

public class ReverseMapRegressionTests
{
    public class Customer { public string Name { get; set; } = ""; }

    public class Order
    {
        public string Name { get; set; } = "";
        public Customer? Customer { get; set; }
    }

    public class OrderDto
    {
        public string Name { get; set; } = "";
        public string CustomerName { get; set; } = "";
    }

    public class ReverseModule : MappingModule
    {
        public ReverseModule()
        {
            DefineMap<Order, OrderDto>()
                .ForProperty(d => d.CustomerName, o => o.MapSource(s => s.Customer!.Name))
                .ReverseMap();
        }
    }

    public class NestedForPropertyModule : MappingModule
    {
        public NestedForPropertyModule()
        {
            DefineMap<OrderDto, Order>()
                .ForProperty(d => d.Customer!.Name, o => o.MapSource(s => s.CustomerName));
        }
    }

    [Fact]
    [Trait("Finding", "F10")]
    public void F10_ReverseOfFlattenedMapSource_DoesNotOverwriteRootMember()
    {
        var engine = new MapEngine(new ReverseModule());

        var result = engine.Map<OrderDto, Order>(new OrderDto { Name = "Order-1", CustomerName = "Ann" });

        Assert.Equal("Order-1", result.Name);
    }

    [Fact]
    [Trait("Finding", "F10")]
    public void F10_ReverseOfFlattenedMapSource_UnflattensIntoNestedMember()
    {
        var engine = new MapEngine(new ReverseModule());

        var result = engine.Map<OrderDto, Order>(new OrderDto { Name = "Order-1", CustomerName = "Ann" });

        Assert.NotNull(result.Customer);
        Assert.Equal("Ann", result.Customer!.Name);
    }

    [Fact]
    [Trait("Finding", "F11")]
    public void F11_ForProperty_WithNestedMember_ThrowsAndPointsToForPath()
    {
        var ex = Assert.ThrowsAny<AutoMapSharpConfigurationException>(() => new NestedForPropertyModule());

        Assert.Contains("ForPath", ex.Message);
    }
}
