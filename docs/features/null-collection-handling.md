# Null Collection Handling Policies (`AllowNullCollections`)

## Overview
A common dilemma in object-to-object mapping is what to do when a source collection property is `null`. Modern .NET best practices recommend returning an empty collection (`[]`) rather than `null` to prevent `NullReferenceException` in client code and UI views.

AutoMapSharp adopts `AllowNullCollections = false` by default, but allows configuring it globally on modules or per-type mapping.

## AutoMapSharp Usage

### Default Behavior (`AllowNullCollections = false`)
When source collection is `null`, AutoMapSharp produces an initialized empty collection:

```csharp
var order = new Order { Items = null };
var dto = engine.Map<Order, OrderDto>(order);

// dto.Items is an empty List<OrderItemDto>() (Count == 0), NOT null!
Assert.NotNull(dto.Items);
Assert.Empty(dto.Items);
```

### Allowing Null Collections (`AllowNullCollections = true`)
You can opt in to returning `null` when the source collection is `null`:

```csharp
public class LegacyExportModule : MappingModule
{
    public LegacyExportModule()
    {
        // Per-map configuration
        DefineMap<Order, OrderDto>()
            .AllowNullCollections(true);

        // Or module-wide configuration:
        // AllowNullCollections = true;
    }
}
```

## Migrating from AutoMapper

| AutoMapper Configuration | AutoMapSharp Configuration | Migration Notes |
|---|---|---|
| `cfg.AllowNullCollections = true;` | `AllowNullCollections = true;` | Configurable on `MappingModule` |
| `CreateMap<S, D>().AllowNullCollections(true)` | `DefineMap<S, D>().AllowNullCollections(true)` | Per-map configuration |

## Summary
- Default `false`: Eliminates null check boilerplate in downstream consumers.
- Explicit `true`: Preserves exact `null` semantics for legacy APIs and serializers.
