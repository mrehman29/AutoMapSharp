# Existing Collection Updates (In-Place vs Replacement)

## Overview
When mapping an updated model onto an existing destination entity (`engine.Map(source, existingDestination)`), replacing collection references breaks ORM change trackers (such as Entity Framework Core tracked navigation properties).

AutoMapSharp inspects destination collections: if the destination collection instance is already instantiated, it updates it **in-place** using `.Clear()` and `.Add(...)`, strictly preserving the collection's reference identity.

## AutoMapSharp Usage

```csharp
// Existing tracked domain entity
var existingOrder = dbContext.Orders.Include(o => o.LineItems).First();
var originalListReference = existingOrder.LineItems;

var updateDto = new OrderUpdateDto
{
    LineItems = new List<LineItemDto>
    {
        new() { Sku = "SKU-NEW", Quantity = 2 }
    }
};

// In-place mapping!
engine.Map(updateDto, existingOrder);

// The collection instance was updated in-place:
Assert.Same(originalListReference, existingOrder.LineItems);
Assert.Single(existingOrder.LineItems);
```

## Choosing per member

By default, a destination member that already holds an object or a mutable collection is mapped into, and a null source value sets the member to null. Two options change that:

```csharp
DefineMap<Account, AccountDto>()
    // Always map into the existing object, and keep it when the source value is null.
    .ForProperty(d => d.Settings, o => o.UseDestinationValue())
    // Always create a new object, even when one exists.
    .ForProperty(d => d.Snapshot, o => o.DoNotUseDestinationValue());
```

## Migrating from AutoMapper

| AutoMapper Behavior | AutoMapSharp Behavior | Migration Notes |
|---|---|---|
| In-place update for existing collections | In-place update for existing collections | Fully compatible with EF Core change trackers |
| Re-instantiation if null | Re-instantiation if null | Matches expected behavior |

## ORM Benefits
- Avoids EF Core exception: *"The collection property ... cannot be set because it is tracked by Entity Framework."*
- Preserves internal collection event listeners and change-tracking proxies.
