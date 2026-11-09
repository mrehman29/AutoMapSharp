# Sets & Dictionaries (`ISet`, `HashSet`, `IDictionary`, `Dictionary`)

## Overview
AutoMapSharp supports mapping into set collections (`ISet<T>`, `HashSet<T>`) and key-value dictionaries (`IDictionary<TKey, TValue>`, `Dictionary<TKey, TValue>`, `IReadOnlyDictionary<TKey, TValue>`).

When mapping sets, duplicate items are eliminated according to destination equality. When mapping dictionaries, both keys and values are independently converted and mapped according to their respective rules.

## AutoMapSharp Usage

```csharp
using AutoMapSharp;

public class CatalogModule : MappingModule
{
    public CatalogModule()
    {
        DefineMap<FeatureEntity, FeatureDto>();
        DefineMap<Product, ProductDto>();
    }
}
```

```csharp
public class Product
{
    // Unique tags
    public HashSet<string> Tags { get; set; } = new();

    // Key-Value specifications
    public Dictionary<string, FeatureEntity> Features { get; set; } = new();
}

public class ProductDto
{
    public HashSet<string> Tags { get; set; } = new();
    public Dictionary<string, FeatureDto> Features { get; set; } = new();
}
```

## Migrating from AutoMapper

| AutoMapper Feature | AutoMapSharp Feature | Migration Notes |
|---|---|---|
| Automatic Dictionary mapping | Automatic Dictionary mapping | Maps both key types and value types |
| Automatic Set mapping | Automatic Set mapping | Maps into `HashSet<T>` |
| Custom dictionary key mapper | Automatic key type converter | Keys converted automatically |

## Notes
- Dictionaries preserve keys while mapping values recursively.
- Sets automatically apply element equality comparisons to guarantee uniqueness.
