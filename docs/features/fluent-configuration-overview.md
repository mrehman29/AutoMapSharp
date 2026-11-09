# Fluent Configuration API Overview

## Overview
AutoMapSharp provides a strongly typed, fluent configuration API anchored around `MappingModule` and `DefineMap<TSource, TDestination>()`. It enables declarative customization of property mappings, constructor parameters, lifecycle hooks, and reverse mapping pairs.

## AutoMapSharp Usage

```csharp
using AutoMapSharp;

public class OrderMappingModule : MappingModule
{
    public OrderMappingModule()
    {
        DefineMap<Order, OrderDto>()
            .ForProperty(dest => dest.CustomerName, opt => opt.MapSource(src => src.Customer.FullName))
            .ForProperty(dest => dest.InternalNotes, opt => opt.Skip())
            .ForProperty(dest => dest.StatusBadge, opt => opt.UseValue("PROCESSED"))
            .ForProperty(dest => dest.Discount, opt => opt.If(src => src.TotalAmount > 100))
            .ForProperty(dest => dest.Description, opt => opt.Fallback("No description available"))
            .ReverseMap();
    }
}
```

## Migrating from AutoMapper

| AutoMapper Syntax | AutoMapSharp Syntax | Notes |
|---|---|---|
| `CreateMap<TSource, TDest>()` | `DefineMap<TSource, TDest>()` | Returns `IMappingDefinition<TSource, TDest>` |
| `ForMember(dest => dest.Prop, opt => ...)` | `ForProperty(dest => dest.Prop, opt => ...)` | Strongly-typed member configuration |
| `opt.Ignore()` | `opt.Skip()` | Destination property will not be assigned |
| `opt.MapFrom(src => ...)` | `opt.MapSource(src => ...)` | Custom expression or redirection |
| `opt.Condition(...)` | `opt.If(...)` | Conditional assignment |
| `opt.NullSubstitute(...)` | `opt.Fallback(...)` | Fallback value when source is null |
| `ReverseMap()` | `ReverseMap()` | Automatically registers inverse map |

## Performance & Native AOT
- Fluent definitions are collected when modules are constructed and frozen when the engine is built. Each type pair is compiled once, so configuration adds no cost per map.
- Module-level settings, such as naming conventions, prefixes and value transformers, apply to every map in the module, wherever they appear in the constructor.
- Native AOT is not verified yet. See [Native AOT & Trimming: Current Status](native-aot-and-trimming.md).
