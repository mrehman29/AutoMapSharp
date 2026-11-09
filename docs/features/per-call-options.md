# Per-Call Options

`Map` accepts options for one call.

```csharp
var dto = engine.Map<Order, OrderDto>(order, opt =>
{
    opt.Items["currency"] = "EUR";
    opt.BeforeMap((src, dest) => dest.Audit = "started");
    opt.AfterMap((src, dest) => dest.Audit += ", done");
});

var untyped = engine.Map<OrderDto>(order, opt => opt.Items["currency"] = "EUR");
var updated = engine.Map(order, existingDto, opt => opt.Items["currency"] = "EUR");
```

* `Items` is available as `context.Items` in resolvers, converters, value converters, mapping actions, `ConstructUsing` with the context, and callbacks.
* Per-call `BeforeMap` runs after the top-level destination object is created and after the map's own `BeforeMap` callbacks. Per-call `AfterMap` runs after the map's own `AfterMap` callbacks. Both run only for the top-level object, not for nested objects.
* Per-call callbacks do not run when the top-level map is a collection or a simple value.

## AutoMapper Migration Guide

| AutoMapper | AutoMapSharp |
|---|---|
| `mapper.Map<D>(src, opts => opts.Items["k"] = v)` | `engine.Map<D>(src, opts => opts.Items["k"] = v)` |
| `opts.BeforeMap(...)`, `opts.AfterMap(...)` | Same, on the typed overloads |
| `context.Items` in resolvers | Same |
