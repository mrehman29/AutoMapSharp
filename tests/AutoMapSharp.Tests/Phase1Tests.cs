using System;
using System.Reflection;
using AutoMapSharp.Exceptions;
using AutoMapSharp.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoMapSharp.Tests;

public class Phase1Tests
{
    public class Order
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
    }

    public class OrderDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
    }

    public class OrderModule : MappingModule
    {
        public OrderModule()
        {
            DefineMap<Order, OrderDto>();
        }
    }

    public class DuplicateModule : MappingModule
    {
        public DuplicateModule()
        {
            DefineMap<Order, OrderDto>();
            DefineMap<Order, OrderDto>(); // Should throw Duplicate / Configuration exception
        }
    }

    [Fact]
    public void Map_NewInstance_GenericOverload_MapsProperties()
    {
        // Arrange
        var module = new OrderModule();
        var engine = new MapEngine(new[] { module });
        var order = new Order { Id = 101, OrderNumber = "ORD-101", TotalAmount = 99.50m };

        // Act
        var dto = engine.Map<Order, OrderDto>(order);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(101, dto.Id);
        Assert.Equal("ORD-101", dto.OrderNumber);
        Assert.Equal(99.50m, dto.TotalAmount);
    }

    [Fact]
    public void Map_ExistingInstance_UpdatesPropertiesInPlace()
    {
        // Arrange
        var module = new OrderModule();
        var engine = new MapEngine(new[] { module });
        var order = new Order { Id = 202, OrderNumber = "ORD-202", TotalAmount = 150.00m };
        var existingDto = new OrderDto { Id = 1, OrderNumber = "OLD", TotalAmount = 0m };

        // Act
        var result = engine.Map<Order, OrderDto>(order, existingDto);

        // Assert
        Assert.Same(existingDto, result);
        Assert.Equal(202, existingDto.Id);
        Assert.Equal("ORD-202", existingDto.OrderNumber);
        Assert.Equal(150.00m, existingDto.TotalAmount);
    }

    [Fact]
    public void Map_UntypedSource_ResolvesAndMaps()
    {
        // Arrange
        var module = new OrderModule();
        var engine = new MapEngine(new[] { module });
        object order = new Order { Id = 303, OrderNumber = "ORD-303", TotalAmount = 45.00m };

        // Act
        var dto = engine.Map<OrderDto>(order);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(303, dto.Id);
        Assert.Equal("ORD-303", dto.OrderNumber);
        Assert.Equal(45.00m, dto.TotalAmount);
    }

    [Fact]
    public void Map_DynamicReflectionOverload_MapsSuccessfully()
    {
        // Arrange
        var module = new OrderModule();
        var engine = new MapEngine(new[] { module });
        object order = new Order { Id = 404, OrderNumber = "ORD-404", TotalAmount = 12.00m };

        // Act
        var result = engine.Map(order, typeof(Order), typeof(OrderDto));

        // Assert
        Assert.NotNull(result);
        var dto = Assert.IsType<OrderDto>(result);
        Assert.Equal(404, dto.Id);
        Assert.Equal("ORD-404", dto.OrderNumber);
    }

    [Fact]
    public void Map_DynamicReflectionOverload_ExistingInstance_MapsSuccessfully()
    {
        // Arrange
        var module = new OrderModule();
        var engine = new MapEngine(new[] { module });
        object order = new Order { Id = 505, OrderNumber = "ORD-505", TotalAmount = 88.00m };
        object existingDto = new OrderDto();

        // Act
        var result = engine.Map(order, existingDto, typeof(Order), typeof(OrderDto));

        // Assert
        Assert.Same(existingDto, result);
        var dto = (OrderDto)result;
        Assert.Equal(505, dto.Id);
        Assert.Equal("ORD-505", dto.OrderNumber);
    }

    [Fact]
    public void DefineMap_DuplicateMapping_ThrowsAutoMapSharpConfigurationException()
    {
        // Act & Assert
        var ex = Assert.Throws<AutoMapSharpConfigurationException>(() => new DuplicateModule());
        Assert.Contains("already defined", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddAutoMapSharp_DependencyInjection_RegistersEngineAndDiscoversModules()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAutoMapSharp(typeof(OrderModule));
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var engine = serviceProvider.GetService<IMapEngine>();

        // Assert
        Assert.NotNull(engine);
        var order = new Order { Id = 606, OrderNumber = "ORD-DI", TotalAmount = 100m };
        var dto = engine.Map<Order, OrderDto>(order);
        Assert.NotNull(dto);
        Assert.Equal("ORD-DI", dto.OrderNumber);
    }

    [Fact]
    public void ReverseMap_CreatesInverseMappingAutomatically()
    {
        // Arrange
        var module = new ReverseMappingModule();
        var engine = new MapEngine(new[] { module });
        var dto = new OrderDto { Id = 707, OrderNumber = "ORD-REV", TotalAmount = 300m };

        // Act
        var order = engine.Map<OrderDto, Order>(dto);

        // Assert
        Assert.NotNull(order);
        Assert.Equal(707, order.Id);
        Assert.Equal("ORD-REV", order.OrderNumber);
        Assert.Equal(300m, order.TotalAmount);
    }

    public class ReverseMappingModule : MappingModule
    {
        public ReverseMappingModule()
        {
            DefineMap<Order, OrderDto>().ReverseMap();
        }
    }
}
