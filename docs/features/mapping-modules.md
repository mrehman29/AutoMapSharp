# Mapping Modules (`MappingModule`)

## Overview
A `MappingModule` encapsulates type-to-type mapping definitions, member configurations, and conventions. It directly replaces AutoMapper's `Profile` class.

## AutoMapSharp Usage

```csharp
using AutoMapSharp;

public class CustomerMappingModule : MappingModule
{
    public CustomerMappingModule()
    {
        // Define a type-to-type map
        DefineMap<Customer, CustomerDto>()
            .ForProperty(dest => dest.FullName, opt => opt.MapSource(src => src.FirstName + " " + src.LastName))
            .ForProperty(dest => dest.InternalNotes, opt => opt.Skip())
            .ReverseMap(); // Automatically configure the reverse mapping
    }
}
```

## Migrating from AutoMapper

| AutoMapper Syntax | AutoMapSharp Syntax |
|---|---|
| `public class MyProfile : Profile` | `public class MyModule : MappingModule` |
| `CreateMap<Source, Dest>()` | `DefineMap<Source, Dest>()` |
| `ForMember(dest => dest.X, opt => opt.MapFrom(...))` | `ForProperty(dest => dest.X, opt => opt.MapSource(...))` |
| `ForMember(dest => dest.X, opt => opt.Ignore())` | `ForProperty(dest => dest.X, opt => opt.Skip())` |
| `.ReverseMap()` | `.ReverseMap()` |

## Configuration Safety
* Attempting to register the same type pair more than once inside a module throws an `AutoMapSharpConfigurationException` immediately to prevent accidental override conflicts.
