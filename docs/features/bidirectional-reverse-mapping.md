# Bidirectional Mapping (`ReverseMap`)

## Overview
Many applications require mapping symmetrically in both directions (e.g., Domain Entity $\leftrightarrow$ DTO). Configuring both directions manually duplicates effort and introduces drift.

AutoMapSharp provides `.ReverseMap()` on `IMappingDefinition<TSource, TDestination>`, returning a new `IMappingDefinition<TDestination, TSource>` that inverts standard properties, conventions, and `MapSource` member redirections.

## AutoMapSharp Usage

```csharp
using AutoMapSharp;

public class OrderModule : MappingModule
{
    public OrderModule()
    {
        DefineMap<OrderEntity, OrderDto>()
            .ForProperty(dest => dest.CustomerDisplayName,
                opt => opt.MapSource(src => src.CustomerName))
            .ReverseMap()
            // Override or customize properties specifically for the reverse direction
            .ForProperty(dest => dest.InternalAuditNotes, opt => opt.Skip());
    }
}
```

Now both mapping operations work seamlessly:

```csharp
// Entity -> DTO
var dto = engine.Map<OrderEntity, OrderDto>(entity);

// DTO -> Entity (Reverse)
var entity = engine.Map<OrderDto, OrderEntity>(dto);
```

## Migrating from AutoMapper

| AutoMapper Syntax | AutoMapSharp Syntax | Migration Notes |
|---|---|---|
| `CreateMap<Src, Dest>().ReverseMap()` | `DefineMap<Src, Dest>().ReverseMap()` | 100% equivalent behavior |
| Automatic member un-flattening | Automatic member un-flattening & redirection | Inverts `MapSource` property expressions |

## Supported Inversions
- Inverts direct name and convention-matched properties.
- Inverts `MapSource(src => src.Property)` into a rule that reads `dest.Property`.
- Inverts a member chain such as `MapSource(src => src.Customer.Name)` into a path rule that writes `Customer.Name`, creating `Customer` when it is missing.
- Unflattens convention-matched members: `OrderDto.CustomerName` is written to `Order.Customer.Name`.
- Swaps the module's source and destination naming conventions, and applies the module's value transformers.
- Does not invert `UseValue`, conditions, resolvers or computed expressions. Configure those on the reverse map if you need them.

See [ForPath & Unflattening](forpath-and-unflattening.md) for the details.

