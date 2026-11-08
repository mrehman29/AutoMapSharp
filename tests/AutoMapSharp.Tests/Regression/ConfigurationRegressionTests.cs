using System;
using AutoMapSharp.Exceptions;
using Xunit;

namespace AutoMapSharp.Tests.Regression;

public class ConfigurationRegressionTests
{
    public class Customer { public string Name { get; set; } = ""; }
    public class Order { public Customer? Customer { get; set; } public string Name { get; set; } = ""; }
    public class NameDto { public string? Name { get; set; } }

    private static string Boom(Order order) => throw new InvalidOperationException("bug in user code");

    public class ThrowingModule : MappingModule
    {
        public ThrowingModule()
        {
            DefineMap<Order, NameDto>().ForProperty(d => d.Name, o => o.MapSource(s => Boom(s)));
        }
    }

    public class NullChainModule : MappingModule
    {
        public NullChainModule()
        {
            DefineMap<Order, NameDto>().ForProperty(d => d.Name, o => o.MapSource(s => s.Customer!.Name));
        }
    }

    [Fact]
    [Trait("Finding", "F14")]
    public void F14_ExceptionInMapSource_IsWrappedNotSwallowed()
    {
        var engine = new MapEngine(new ThrowingModule());

        var ex = Assert.Throws<AutoMapSharpMappingException>(() => engine.Map<Order, NameDto>(new Order()));

        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains(nameof(NameDto.Name), ex.Message);
    }

    [Fact]
    [Trait("Finding", "F14")]
    public void F14_NullMemberChainInMapSource_ReturnsNull()
    {
        var engine = new MapEngine(new NullChainModule());

        var result = engine.Map<Order, NameDto>(new Order { Customer = null });

        Assert.Null(result.Name);
    }

    public class FirstModule : MappingModule
    {
        public FirstModule() { DefineMap<Customer, NameDto>(); }
    }

    public class SecondModule : MappingModule
    {
        public SecondModule() { DefineMap<Customer, NameDto>(); }
    }

    [Fact]
    [Trait("Finding", "F15")]
    public void F15_DuplicateMapAcrossModules_Throws()
    {
        var ex = Assert.Throws<AutoMapSharpConfigurationException>(() => new MapEngine(new FirstModule(), new SecondModule()));

        Assert.Contains(nameof(FirstModule), ex.Message);
        Assert.Contains(nameof(SecondModule), ex.Message);
    }

    public class LegacyCustomer { public string StrName { get; set; } = ""; }
    public class LegacyDto { public string Name { get; set; } = ""; }

    public class LateSettingsModule : MappingModule
    {
        public LateSettingsModule()
        {
            DefineMap<Customer, NameDto>();
            DefineMap<LegacyCustomer, LegacyDto>();

            // Configured after DefineMap. Must still apply to the maps above.
            ValueTransformers.Add<string?>(s => s?.ToUpperInvariant());
            RecognizePrefixes("Str");
        }
    }

    [Fact]
    [Trait("Finding", "F17")]
    public void F17_ValueTransformerAddedAfterDefineMap_StillApplies()
    {
        var engine = new MapEngine(new LateSettingsModule());

        var result = engine.Map<Customer, NameDto>(new Customer { Name = "ann" });

        Assert.Equal("ANN", result.Name);
    }

    [Fact]
    [Trait("Finding", "F17")]
    public void F17_PrefixRecognizedAfterDefineMap_StillApplies()
    {
        var engine = new MapEngine(new LateSettingsModule());

        var result = engine.Map<LegacyCustomer, LegacyDto>(new LegacyCustomer { StrName = "ann" });

        Assert.Equal("ANN", result.Name);
    }

    public class Address { public string City { get; set; } = ""; }
    public class Source { public string Name { get; set; } = ""; public Address Address { get; set; } = new(); }
    public class ConstructedDto { public string Name { get; set; } = ""; public string Unmapped { get; set; } = ""; }
    public class MismatchDto { public int Address { get; set; } }

    public class ConstructUsingModule : MappingModule
    {
        public ConstructUsingModule()
        {
            DefineMap<Source, ConstructedDto>().ConstructUsing(s => new ConstructedDto());
        }
    }

    public class MismatchModule : MappingModule
    {
        public MismatchModule() { DefineMap<Source, MismatchDto>(); }
    }

    [Fact]
    [Trait("Finding", "F26")]
    public void F26_Validation_ChecksMapsThatUseConstructUsing()
    {
        var result = new MapEngine(new ConstructUsingModule()).ValidateConfiguration();

        var error = Assert.Single(result.Errors);
        Assert.Contains(nameof(ConstructedDto.Unmapped), error.UnmappedProperties);
    }

    [Fact]
    [Trait("Finding", "F26")]
    public void F26_Validation_ReportsIncompatibleMemberTypes()
    {
        var result = new MapEngine(new MismatchModule()).ValidateConfiguration();

        Assert.False(result.IsValid);
        Assert.Contains(nameof(MismatchDto.Address), result.GenerateReport());
    }
}
