# Custom Source Property Redirection (`MapSource`)

## Overview
By default, AutoMapSharp matches properties by convention or flattens nested paths. When source and destination naming diverges significantly or destination properties require computed expressions, use `.ForProperty(dest => dest.Prop, opt => opt.MapSource(src => ...))`.

## AutoMapSharp Usage

```csharp
using AutoMapSharp;

public class EmployeeModule : MappingModule
{
    public EmployeeModule()
    {
        DefineMap<Employee, EmployeeCardViewModel>()
            // Calculated expression
            .ForProperty(dest => dest.DisplayName,
                opt => opt.MapSource(src => $"{src.Title} {src.FirstName} {src.LastName}"))

            // Custom multi-field synthesis
            .ForProperty(dest => dest.FullLocation,
                opt => opt.MapSource(src => $"{src.OfficeCity}, {src.OfficeCountry}"))

            // Redirection from divergent property name
            .ForProperty(dest => dest.Email,
                opt => opt.MapSource(src => src.CorporateEmailAddress));
    }
}
```

## Migrating from AutoMapper

| AutoMapper Syntax | AutoMapSharp Syntax | Migration Notes |
|---|---|---|
| `opt.MapFrom(src => src.Name)` | `opt.MapSource(src => src.Name)` | Direct replacement for property-to-property redirection |
| `opt.MapFrom(src => src.A + src.B)` | `opt.MapSource(src => src.A + src.B)` | Full support for computed C# expressions |
| `opt.MapFrom<CustomResolver>()` | `opt.MapWith<CustomResolver>()` | For class-based resolvers with DI (see Phase 5) |

## Reverse Mapping Integration
When configuring bidirectional mappings with `.ReverseMap()`, AutoMapSharp automatically inspects `MapSource` member redirections and inverts them:
```csharp
DefineMap<User, UserDto>()
    .ForProperty(d => d.FullName, opt => opt.MapSource(s => s.Name))
    .ReverseMap();
// Reverse map automatically assigns User.Name from UserDto.FullName!
```
