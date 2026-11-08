# ORM Database Projections (`ProjectOnto`)

AutoMapSharp provides the `.ProjectOnto<TDest>()` extension method on `IQueryable` to enable SQL-level database projections through Entity Framework Core and other LINQ ORM providers.

Rather than querying entire relational entity tables into memory and performing in-memory mapping, `ProjectOnto` translates mapping rules into a LINQ `Expression<Func<TSource, TDest>>` that EF Core converts directly into a minimal SQL `SELECT` statement fetching only projected columns.

---

## 1. Basic Projection Query

Given an Entity Framework Core `DbSet<Order>`:

```csharp
using AutoMapSharp;
using Microsoft.EntityFrameworkCore;

// Executes a single optimized SQL query fetching only projected columns:
List<OrderSummaryDto> summaries = await dbContext.Orders
    .Where(o => o.TotalAmount > 100m)
    .ProjectOnto<OrderSummaryDto>(mapEngine)
    .ToListAsync();
```

---

## 2. In-Memory vs. Database Projection Comparison

### Naive In-Memory Mapping (Bad):
```csharp
// Fetches all columns and entire tracking graph into memory:
var orders = await dbContext.Orders
    .Include(o => o.Customer)
        .ThenInclude(c => c.Address)
    .Include(o => o.Items)
    .ToListAsync(); // Heavy SQL query transferring megabytes of unused data

// Maps in memory:
var dtos = mapEngine.Map<List<Order>, List<OrderSummaryDto>>(orders);
```

### Projected Mapping via `ProjectOnto` (Optimized):
```csharp
// EF Core generates:
// SELECT o.Id, o.OrderNumber, o.TotalAmount, c.Name AS CustomerName, a.City AS CustomerAddressCity
// FROM Orders AS o
// LEFT JOIN Customers AS c ON o.CustomerId = c.Id
// LEFT JOIN Addresses AS a ON c.AddressId = a.Id
// WHERE o.TotalAmount > 100.0
var dtos = await dbContext.Orders
    .Where(o => o.TotalAmount > 100m)
    .ProjectOnto<OrderSummaryDto>(mapEngine)
    .ToListAsync();
```

---

## What a projection can translate

A projection is an expression the database runs, so it can only contain what the LINQ provider translates.

| Configuration | In a projection |
|---|---|
| Name, convention and flattening matches, public fields | Member reads |
| `MapSource(s => ...)` | The expression, inlined |
| `UseValue(...)` | A constant |
| `Fallback(...)` | `COALESCE` |
| Nested objects and collections | Nested selects, `Select` and `ToList`. Null nested objects stay null |
| `Include`, `IncludeAllDerived`, `As` | Type checks, most derived first. EF Core translates them for TPH models |
| `MaxDepth` | Recursion stops at the configured depth. Without it, projections stop at 10 levels |
| `ExplicitExpansion` | Left out unless expanded by name or expression |
| Resolvers (`MapWith`), value converters, conditions (`If`, `PreCondition`), type converters (`ConvertUsing` on the map) | Rejected with an `AutoMapSharpConfigurationException` that names the member |
| Value transformers | Not applied |

## 3. AutoMapper Migration Guide

| AutoMapper Syntax | AutoMapSharp Equivalent | Notes |
| :--- | :--- | :--- |
| `query.ProjectTo<Dto>(config)` | `query.ProjectOnto<Dto>(engine)` | Identical semantics; passes `IMapEngine` |
| `ProjectTo<Dto>(config, null, "Items")` | `query.ProjectOnto<Dto>(engine, null, "Items")` | Explicit expansion support |
| Expression Tree Caching | Expression Tree Caching | Thread-safe caching across queries |
