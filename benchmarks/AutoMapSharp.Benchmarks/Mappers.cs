using System;
using Mapster;
using Microsoft.Extensions.Logging.Abstractions;

namespace AutoMapSharp.Benchmarks;

public class BenchmarkModule : MappingModule
{
    public BenchmarkModule()
    {
        DefineMap<FlatSource, FlatDestination>();
        DefineMap<Address, AddressDto>();
        DefineMap<Customer, CustomerDto>();
        DefineMap<OrderLine, OrderLineDto>();
        DefineMap<Order, OrderDto>();
        DefineMap<PersonSource, PersonDestination>()
            .ForProperty(d => d.FullName, o => o.MapSource(s => s.First + " " + s.Last));
    }
}

/// <summary>
/// Builds one configured instance of each library, shared by all benchmarks.
/// </summary>
public static class Mappers
{
    public static readonly IMapEngine AutoMapSharp = new MapEngine(new BenchmarkModule());

    public static readonly global::AutoMapper.IMapper AutoMapper = CreateAutoMapper();

    public static readonly TypeAdapterConfig Mapster = CreateMapster();

    private static global::AutoMapper.IMapper CreateAutoMapper()
    {
        var config = new global::AutoMapper.MapperConfiguration(cfg =>
        {
            // AutoMapper 15+ needs a license key for production use. Without one it logs a warning and still runs.
            var key = Environment.GetEnvironmentVariable("AUTOMAPPER_LICENSE_KEY");
            if (!string.IsNullOrEmpty(key))
            {
                cfg.LicenseKey = key;
            }

            cfg.CreateMap<FlatSource, FlatDestination>();
            cfg.CreateMap<Address, AddressDto>();
            cfg.CreateMap<Customer, CustomerDto>();
            cfg.CreateMap<OrderLine, OrderLineDto>();
            cfg.CreateMap<Order, OrderDto>();
            cfg.CreateMap<PersonSource, PersonDestination>()
                .ForMember(d => d.FullName, o => o.MapFrom(s => s.First + " " + s.Last));
        }, NullLoggerFactory.Instance);

        config.AssertConfigurationIsValid();
        config.CompileMappings();
        return config.CreateMapper();
    }

    private static TypeAdapterConfig CreateMapster()
    {
        var config = new TypeAdapterConfig();
        config.NewConfig<PersonSource, PersonDestination>()
            .Map(d => d.FullName, s => s.First + " " + s.Last);
        config.Compile();
        return config;
    }
}
