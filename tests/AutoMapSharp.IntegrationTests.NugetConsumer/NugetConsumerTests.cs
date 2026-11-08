using System;
using AutoMapSharp;
using AutoMapSharp.Conventions;
using AutoMapSharp.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoMapSharp.IntegrationTests.NugetConsumer;

public enum CustomerTier
{
    Standard = 0,
    Gold = 1,
    Platinum = 2
}

public class AddressInfo
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
}

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public AddressInfo? Address { get; set; }
    public CustomerTier Tier { get; set; }
    public Guid CustomerGuid { get; set; }
}

public class CustomerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AddressCity { get; set; } // Flattening
    public string? Tier { get; set; } // Enum to String
    public string? CustomerGuid { get; set; } // Guid to String
}

public record CustomerRecord(int Id, string Name);

public class CustomerMappingModule : MappingModule
{
    public CustomerMappingModule()
    {
        DefineMap<Customer, CustomerDto>().ReverseMap();
        DefineMap<Customer, CustomerRecord>();
    }
}

public class NugetConsumerTests
{
    [Fact]
    public void NugetPackage_ResolvesViaDI_AndMapsNewInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAutoMapSharp(typeof(CustomerMappingModule));
        var serviceProvider = services.BuildServiceProvider();

        var engine = serviceProvider.GetRequiredService<IMapEngine>();
        var customer = new Customer { Id = 10, Name = "Alice", Email = "alice@example.com" };

        // Act
        var dto = engine.Map<Customer, CustomerDto>(customer);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(10, dto.Id);
        Assert.Equal("Alice", dto.Name);
        Assert.Equal("alice@example.com", dto.Email);
    }

    [Fact]
    public void NugetPackage_MapsExistingDestinationInstance()
    {
        // Arrange
        var engine = new MapEngine(new[] { new CustomerMappingModule() });
        var customer = new Customer { Id = 20, Name = "Bob", Email = "bob@example.com" };
        var existingDto = new CustomerDto { Id = 1, Name = "Old", Email = "old@example.com" };

        // Act
        var result = engine.Map<Customer, CustomerDto>(customer, existingDto);

        // Assert
        Assert.Same(existingDto, result);
        Assert.Equal(20, existingDto.Id);
        Assert.Equal("Bob", existingDto.Name);
        Assert.Equal("bob@example.com", existingDto.Email);
    }

    [Fact]
    public void NugetPackage_MapsUntypedObject()
    {
        // Arrange
        var engine = new MapEngine(new[] { new CustomerMappingModule() });
        object customer = new Customer { Id = 30, Name = "Charlie", Email = "charlie@example.com" };

        // Act
        var dto = engine.Map<CustomerDto>(customer);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal(30, dto.Id);
        Assert.Equal("Charlie", dto.Name);
    }

    [Fact]
    public void NugetPackage_ReverseMap_WorksInBothDirections()
    {
        // Arrange
        var engine = new MapEngine(new[] { new CustomerMappingModule() });
        var dto = new CustomerDto { Id = 40, Name = "Dana", Email = "dana@example.com" };

        // Act
        var customer = engine.Map<CustomerDto, Customer>(dto);

        // Assert
        Assert.NotNull(customer);
        Assert.Equal(40, customer.Id);
        Assert.Equal("Dana", customer.Name);
        Assert.Equal("dana@example.com", customer.Email);
    }

    [Fact]
    public void NugetPackage_Phase2_FlatteningAndTypeConversions()
    {
        // Arrange
        var engine = new MapEngine(new[] { new CustomerMappingModule() });
        var guid = Guid.NewGuid();
        var customer = new Customer
        {
            Id = 50,
            Name = "Eve",
            Email = "eve@example.com",
            Address = new AddressInfo { City = "San Francisco", Street = "Market St" },
            Tier = CustomerTier.Platinum,
            CustomerGuid = guid
        };

        // Act
        var dto = engine.Map<Customer, CustomerDto>(customer);

        // Assert
        Assert.NotNull(dto);
        Assert.Equal("San Francisco", dto.AddressCity);
        Assert.Equal("Platinum", dto.Tier);
        Assert.Equal(guid.ToString(), dto.CustomerGuid);
    }

    [Fact]
    public void NugetPackage_Phase2_ConstructorMappingToPositionalRecord()
    {
        // Arrange
        var engine = new MapEngine(new[] { new CustomerMappingModule() });
        var customer = new Customer { Id = 60, Name = "Frank" };

        // Act
        var record = engine.Map<Customer, CustomerRecord>(customer);

        // Assert
        Assert.NotNull(record);
        Assert.Equal(60, record.Id);
        Assert.Equal("Frank", record.Name);
    }

    [Fact]
    public void NugetPackage_SourceGenerator_EmitsMapsThatTheEngineUses()
    {
        // The source generator bundled in analyzers/dotnet/cs runs in this consumer's build and registers typed maps.
        var generated = Type.GetType("AutoMapSharp.Generated.AutoMapSharpGeneratedMaps, AutoMapSharp.IntegrationTests.NugetConsumer");
        Assert.NotNull(generated);

        var engine = new MapEngine(new CustomerMappingModule());
        var info = engine.GetPlanInfo(typeof(Customer), typeof(CustomerDto));
        Assert.Equal(AutoMapSharp.Generated.MapPlanKind.Generated, info.Kind);

        var dto = engine.Map<Customer, CustomerDto>(new Customer { Id = 1, Name = "Ann", Tier = CustomerTier.Gold, Address = new AddressInfo { City = "Oslo" } });
        Assert.Equal("Gold", dto.Tier);
        Assert.Equal("Oslo", dto.AddressCity);
    }
}
