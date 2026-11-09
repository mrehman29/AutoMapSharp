# Mapping Lifecycle Callbacks (`BeforeMap` & `AfterMap`)

AutoMapSharp provides lifecycle hooks that execute custom logic immediately before property assignments begin and after all property assignments complete. These callbacks are essential for audit logging, security timestamping, initializing complex structures, or executing post-mapping calculations.

---

## 1. Using `BeforeMap` and `AfterMap`

Callbacks can be configured with or without access to the `ResolutionContext`:

```csharp
DefineMap<OrderSource, OrderDto>()
    .BeforeMap((src, dest, ctx) =>
    {
        // Executes prior to member mapping
        dest.AuditTrail = $"Mapping started at {DateTime.UtcNow:O}";
    })
    .AfterMap((src, dest, ctx) =>
    {
        // Executes after all members and sub-mappers complete
        dest.ProcessedAtUtc = DateTime.UtcNow;
        dest.Checksum = ComputeSecurityHash(dest);
    });
```

---

## 2. Execution Order Guarantees

1. **Instantiation**: The destination object is created (via parameterless constructor, positional record constructor, or `ConstructUsing`).
2. **Reference Preservation**: The instance is registered in `ResolutionContext` for circular graph safety.
3. **`BeforeMap`**: All registered `BeforeMap` actions execute in order of registration. Base actions run first if `IncludeBase` was configured.
4. **Member Mapping**: Properties, custom resolvers, value transformers, and sub-mappers execute.
5. **`AfterMap`**: All registered `AfterMap` actions execute in order of registration.

---

## Reusable mapping actions

A class that implements `IMappingAction<TSource, TDestination>` can be registered as a callback. It is created from the service provider when registered there, so it can take dependencies:

```csharp
public class StampAction : IMappingAction<Order, OrderDto>
{
    private readonly IClock _clock;
    public StampAction(IClock clock) => _clock = clock;
    public void Process(Order source, OrderDto destination, ResolutionContext context) => destination.MappedAt = _clock.Now;
}

DefineMap<Order, OrderDto>().AfterMap<StampAction>();
```

`Map` also accepts per-call callbacks. See [Per-Call Options](per-call-options.md).

## 3. AutoMapper Migration Guide

| AutoMapper Syntax | AutoMapSharp Equivalent | Notes |
| :--- | :--- | :--- |
| `.BeforeMap((src, dest) => { ... })` | `.BeforeMap((src, dest) => { ... })` | Exact match |
| `.BeforeMap((src, dest, ctx) => { ... })` | `.BeforeMap((src, dest, ctx) => { ... })` | Full context access |
| `.AfterMap((src, dest) => { ... })` | `.AfterMap((src, dest) => { ... })` | Exact match |
| `.AfterMap((src, dest, ctx) => { ... })` | `.AfterMap((src, dest, ctx) => { ... })` | Full context access |
| Action execution order | Action execution order | Symmetrically preserves base-then-derived ordering |
