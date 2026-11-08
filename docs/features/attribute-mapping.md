# Attribute Configuration

Maps can be defined with attributes on the destination type.

```csharp
[MapFrom(typeof(SupportTicket), ReverseMap = true)]
public class SupportTicketView
{
    public int Number { get; set; }

    [SourceMember(nameof(SupportTicket.AssignedAgent))]
    public string Owner { get; set; } = "";

    [SkipMap]
    public string? InternalNotes { get; set; }
}
```

## Registering attribute maps

* Dependency injection: `AddAutoMapSharp(assembly)` scans the assembly for `[MapFrom]` and registers the maps.
* Inline configuration: `cfg.AddAttributeMaps(assembly)`.
* Without either: `new MapEngine(new AttributeMappingModule(assembly))`.

A type can carry several `[MapFrom]` attributes. A pair that is also defined in a module is reported as a duplicate when the engine is built.

## Member attributes

| Attribute | Effect |
|---|---|
| `[SkipMap]` | Skips the member |
| `[SourceMember("Name")]` | Reads the member from the source member with that name |

Member attributes apply to every map that writes the member, including maps defined in modules. A `ForProperty` rule for the same member wins. The analyzer honors both attributes.

## AutoMapper Migration Guide

| AutoMapper | AutoMapSharp |
|---|---|
| `[AutoMap(typeof(Source), ReverseMap = true)]` | `[MapFrom(typeof(Source), ReverseMap = true)]` |
| `[Ignore]` | `[SkipMap]` |
| `[SourceMember("Name")]` | `[SourceMember("Name")]` |
| `cfg.AddMaps(assembly)` | `AddAutoMapSharp(assembly)` or `cfg.AddAttributeMaps(assembly)` |
