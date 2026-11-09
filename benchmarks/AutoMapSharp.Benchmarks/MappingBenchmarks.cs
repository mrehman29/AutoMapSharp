using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Mapster;

namespace AutoMapSharp.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class FlatMappingBenchmark
{
    private readonly FlatSource _source = new();

    [Benchmark(Baseline = true), BenchmarkCategory("Flat")]
    public FlatDestination HandWritten() => new()
    {
        Id = _source.Id,
        FirstName = _source.FirstName,
        LastName = _source.LastName,
        Email = _source.Email,
        Age = _source.Age,
        Balance = _source.Balance,
        IsActive = _source.IsActive,
        CreatedAt = _source.CreatedAt,
        Role = _source.Role,
        Country = _source.Country
    };

    [Benchmark, BenchmarkCategory("Flat")]
    public FlatDestination AutoMapSharp() => Mappers.AutoMapSharp.Map<FlatSource, FlatDestination>(_source);

    [Benchmark, BenchmarkCategory("Flat")]
    public FlatDestination AutoMapper() => Mappers.AutoMapper.Map<FlatSource, FlatDestination>(_source);

    [Benchmark, BenchmarkCategory("Flat")]
    public FlatDestination Mapster() => _source.Adapt<FlatSource, FlatDestination>(Mappers.Mapster);
}

[MemoryDiagnoser]
public class NestedMappingBenchmark
{
    private readonly Order _source = new();

    [Benchmark(Baseline = true)]
    public OrderDto HandWritten() => HandWrittenMaps.Map(_source);

    [Benchmark]
    public OrderDto AutoMapSharp() => Mappers.AutoMapSharp.Map<Order, OrderDto>(_source);

    [Benchmark]
    public OrderDto AutoMapper() => Mappers.AutoMapper.Map<Order, OrderDto>(_source);

    [Benchmark]
    public OrderDto Mapster() => _source.Adapt<Order, OrderDto>(Mappers.Mapster);
}

[MemoryDiagnoser]
public class CollectionMappingBenchmark
{
    private readonly List<FlatSource> _sources = Enumerable.Range(1, 1000).Select(i => new FlatSource { Id = i }).ToList();

    [Benchmark(Baseline = true)]
    public List<FlatDestination> HandWritten()
    {
        var list = new List<FlatDestination>(_sources.Count);
        foreach (var s in _sources)
        {
            list.Add(new FlatDestination
            {
                Id = s.Id,
                FirstName = s.FirstName,
                LastName = s.LastName,
                Email = s.Email,
                Age = s.Age,
                Balance = s.Balance,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt,
                Role = s.Role,
                Country = s.Country
            });
        }
        return list;
    }

    [Benchmark]
    public List<FlatDestination> AutoMapSharp() => Mappers.AutoMapSharp.Map<List<FlatSource>, List<FlatDestination>>(_sources);

    [Benchmark]
    public List<FlatDestination> AutoMapper() => Mappers.AutoMapper.Map<List<FlatSource>, List<FlatDestination>>(_sources);

    [Benchmark]
    public List<FlatDestination> Mapster() => _sources.Adapt<List<FlatSource>, List<FlatDestination>>(Mappers.Mapster);
}

[MemoryDiagnoser]
public class MapSourceBenchmark
{
    private readonly PersonSource _source = new();

    [Benchmark(Baseline = true)]
    public PersonDestination HandWritten() => new() { Id = _source.Id, FullName = _source.First + " " + _source.Last };

    [Benchmark]
    public PersonDestination AutoMapSharp() => Mappers.AutoMapSharp.Map<PersonSource, PersonDestination>(_source);

    [Benchmark]
    public PersonDestination AutoMapper() => Mappers.AutoMapper.Map<PersonSource, PersonDestination>(_source);

    [Benchmark]
    public PersonDestination Mapster() => _source.Adapt<PersonSource, PersonDestination>(Mappers.Mapster);
}

public static class HandWrittenMaps
{
    public static OrderDto Map(Order o) => new()
    {
        Id = o.Id,
        PlacedAt = o.PlacedAt,
        Customer = new CustomerDto
        {
            Id = o.Customer.Id,
            Name = o.Customer.Name,
            Address = new AddressDto
            {
                Street = o.Customer.Address.Street,
                City = o.Customer.Address.City,
                PostalCode = o.Customer.Address.PostalCode
            }
        },
        Lines = o.Lines.Select(l => new OrderLineDto { Sku = l.Sku, Quantity = l.Quantity, UnitPrice = l.UnitPrice }).ToList()
    };
}
