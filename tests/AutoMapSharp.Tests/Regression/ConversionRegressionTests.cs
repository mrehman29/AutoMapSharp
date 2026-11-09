using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace AutoMapSharp.Tests.Regression;

public class ConversionRegressionTests
{
    public enum SourceColor { Green, Red, Blue, Purple }
    public enum DestinationColor { Red, Blue, Green, Yellow }

    public class Paint { public SourceColor Color { get; set; } }
    public class PaintDto { public DestinationColor Color { get; set; } }

    public class EnumModule : MappingModule
    {
        public EnumModule() { DefineMap<Paint, PaintDto>(); }
    }

    [Theory]
    [InlineData(SourceColor.Green, DestinationColor.Green)]
    [InlineData(SourceColor.Red, DestinationColor.Red)]
    [InlineData(SourceColor.Blue, DestinationColor.Blue)]
    [Trait("Finding", "F12")]
    public void F12_EnumToEnum_MapsByName(SourceColor source, DestinationColor expected)
    {
        var engine = new MapEngine(new EnumModule());
        Assert.Equal(expected, engine.Map<Paint, PaintDto>(new Paint { Color = source }).Color);
    }

    [Fact]
    [Trait("Finding", "F12")]
    public void F12_EnumToEnum_MissingName_FallsBackToNumericValue()
    {
        var engine = new MapEngine(new EnumModule());

        var result = engine.Map<Paint, PaintDto>(new Paint { Color = SourceColor.Purple });

        Assert.Equal(DestinationColor.Yellow, result.Color);
    }
}

[Collection(NonParallelCollection.Name)]
public class ConversionExceptionRegressionTests
{
    public class Address { public string City { get; set; } = "c"; }
    public class AddressDto { public string City { get; set; } = ""; }
    public class Order { public int Id { get; set; } public Address Address { get; set; } = new(); }
    public class OrderDto { public int Id { get; set; } public AddressDto? Address { get; set; } }

    public class OrderModule : MappingModule
    {
        public OrderModule()
        {
            DefineMap<Address, AddressDto>();
            DefineMap<Order, OrderDto>();
        }
    }

    [Fact]
    [Trait("Finding", "F22")]
    public void F22_NestedObjectMapping_ThrowsNoFirstChanceExceptions()
    {
        var engine = new MapEngine(new OrderModule());
        var orders = Enumerable.Range(0, 1000).Select(i => new Order { Id = i }).ToList();
        engine.Map<List<Order>, List<OrderDto>>(orders);

        int count = 0;
        var thread = Environment.CurrentManagedThreadId;
        EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> handler = (_, _) =>
        {
            if (Environment.CurrentManagedThreadId == thread) Interlocked.Increment(ref count);
        };

        AppDomain.CurrentDomain.FirstChanceException += handler;
        try
        {
            var result = engine.Map<List<Order>, List<OrderDto>>(orders);
            Assert.Equal(1000, result.Count);
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= handler;
        }

        Assert.Equal(0, count);
    }
}
