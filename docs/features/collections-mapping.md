# Standard Collection Mapping (Arrays, Lists, Enumerables)

## Overview
AutoMapSharp provides high-performance automatic mapping for arrays (`T[]`), lists (`List<T>`, `IList<T>`, `ICollection<T>`), and generic enumerables (`IEnumerable<T>`).

Inner elements are resolved and converted automatically using registered element type maps or primitive type conversions. Where source count is known (`ICollection`), destination collections are pre-sized to eliminate continuous memory re-allocation.

## AutoMapSharp Usage

```csharp
using AutoMapSharp;

public class OrderModule : MappingModule
{
    public OrderModule()
    {
        // Define element map
        DefineMap<OrderItem, OrderItemDto>();

        // Collections on parent objects map automatically
        DefineMap<Order, OrderDto>();
    }
}
```

### Top-Level Collection Mapping
You can map collections directly without defining collection-level maps:

```csharp
var items = new List<OrderItem> { ... };

// Map List -> List
List<OrderItemDto> dtos = engine.Map<List<OrderItem>, List<OrderItemDto>>(items);

// Map Array -> Array
OrderItemDto[] dtoArray = engine.Map<OrderItem[], OrderItemDto[]>(itemArray);
```

## Migrating from AutoMapper

| AutoMapper Behavior | AutoMapSharp Behavior | Migration Notes |
|---|---|---|
| Automatic `IEnumerable<T>` mapping | Automatic `IEnumerable<T>` mapping | 100% compatible |
| Top-level `mapper.Map<List<T>>(src)` | `mapper.Map<List<TSrc>, List<TDest>>(src)` | Strongly-typed top-level signatures |
| Re-allocation on unknown counts | Pre-sizes lists whenever source implements `ICollection` | Improved performance |

## Supported destination types

| Destination | How it is created |
|---|---|
| `T[]` | A new array |
| `List<T>`, and interfaces it implements such as `IEnumerable<T>`, `IList<T>`, `IReadOnlyList<T>` | A new `List<T>` |
| `ISet<T>`, `IReadOnlySet<T>` | A new `HashSet<T>` |
| Concrete types with a parameterless constructor and `Add`, such as `HashSet<T>`, `SortedSet<T>`, `ObservableCollection<T>`, `Collection<T>`, `LinkedList<T>` | Created, then filled with `Add` |
| Types with a constructor that takes a list, such as `ReadOnlyCollection<T>`, `Queue<T>`, `Stack<T>` | Built from a `List<T>` |
| Immutable collections, such as `ImmutableList<T>`, `ImmutableArray<T>`, `ImmutableHashSet<T>` | Built with `CreateRange` |
| `Dictionary<TKey, TValue>`, `IDictionary<,>`, `IReadOnlyDictionary<,>`, `SortedDictionary<,>`, `ConcurrentDictionary<,>`, `ImmutableDictionary<,>` | Keys and values are both mapped |

Any other collection type raises an `AutoMapSharpMappingException` that names the type.

Collections are always copied. A null source collection gives an empty collection, or null with `AllowNullCollections(true)`. When the destination already holds a mutable collection, it is cleared and refilled in place. See [Existing Collection Updates](existing-collection-updates.md).

Elements that already have the destination element type are copied as they are. Elements with a defined map, or of a derived type covered by `Include`, are mapped with that map.

## Performance Highlights
- Pre-sized allocations: `new List<TDest>(count)` allocates internal arrays once.
- Direct indexed element assignments for arrays without LINQ `.Select().ToArray()`.
