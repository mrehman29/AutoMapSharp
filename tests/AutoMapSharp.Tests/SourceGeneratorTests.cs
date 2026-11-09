using System;
using AutoMapSharp.Generated;
using Xunit;

namespace AutoMapSharp.Tests;

/// <summary>
/// The source generator (remediation Phase 15). This project references the generator, so eligible maps in every test
/// class run through generated code. CI also runs the whole suite with AUTOMAPSHARP_DISABLE_GENERATED=1 to compare.
/// </summary>
public class SourceGeneratorTests
{
    private static bool GeneratedEnabled =>
        !string.Equals(Environment.GetEnvironmentVariable("AUTOMAPSHARP_DISABLE_GENERATED"), "1", StringComparison.Ordinal);

    public class Address { public string City { get; set; } = ""; }
    public class Customer { public int Id { get; set; } public string Name { get; set; } = ""; public Address? Address { get; set; } public int? Rating { get; set; } }
    public class CustomerDto { public int Id { get; set; } public string Name { get; set; } = ""; public string? AddressCity { get; set; } public long Rating { get; set; } public string? Note { get; set; } }

    public class GeneratedModule : MappingModule
    {
        public GeneratedModule()
        {
            DefineMap<Customer, CustomerDto>().ForProperty(d => d.Note, o => o.UseValue("generated"));
        }
    }

    public class NotGeneratedModule : MappingModule
    {
        public NotGeneratedModule()
        {
            ValueTransformers.Add<string>(s => s?.ToUpperInvariant()!);
            DefineMap<Address, CustomerDto>().ForProperty(d => d.Name, o => o.MapSource(a => a.City));
        }
    }

    [Fact]
    public void SimpleMap_UsesGeneratedCode()
    {
        var engine = new MapEngine(new GeneratedModule());

        var info = engine.GetPlanInfo(typeof(Customer), typeof(CustomerDto));
        var dto = engine.Map<Customer, CustomerDto>(new Customer { Id = 7, Name = "Ann", Address = new Address { City = "Oslo" }, Rating = 4 });

        Assert.Equal(GeneratedEnabled ? MapPlanKind.Generated : MapPlanKind.Compiled, info.Kind);
        Assert.Equal(7, dto.Id);
        Assert.Equal("Oslo", dto.AddressCity);
        Assert.Equal(4L, dto.Rating);
        Assert.Equal("generated", dto.Note);
    }

    [Fact]
    public void MapWithUnsupportedOptions_UsesCompiledPlanAndSaysWhy()
    {
        var engine = new MapEngine(new NotGeneratedModule());

        var info = engine.GetPlanInfo(typeof(Address), typeof(CustomerDto));

        Assert.NotEqual(MapPlanKind.Generated, info.Kind);
        Assert.False(string.IsNullOrEmpty(info.Reason));
        Assert.Equal("OSLO", engine.Map<Address, CustomerDto>(new Address { City = "Oslo" }).Name);
    }

    public class CloneAddressModule : MappingModule
    {
        public CloneAddressModule()
        {
            DefineMap<Customer, CustomerDto>().ForProperty(d => d.Note, o => o.UseValue("generated"));
            DefineMap<Address, CustomerDto>().ForProperty(d => d.Name, o => o.Skip());
            ValueTransformers.Add<long>(v => v * 10);
        }
    }

    [Fact]
    public void GeneratedCode_IsRejected_WhenRuntimeConfigurationDiffers()
    {
        // Same Customer -> CustomerDto pair as GeneratedModule, but this module adds a value transformer for long,
        // which the generated code for that pair does not apply. The engine must notice and use its compiled plan.
        var engine = new MapEngine(new CloneAddressModule());

        var info = engine.GetPlanInfo(typeof(Customer), typeof(CustomerDto));
        var dto = engine.Map<Customer, CustomerDto>(new Customer { Rating = 4 });

        Assert.NotEqual(MapPlanKind.Generated, info.Kind);
        Assert.Contains(GeneratedEnabled ? "transformer" : "disabled", info.Reason);
        Assert.Equal(40L, dto.Rating);
    }
}
