# Entity Framework Core Expression Tree Translation

When executing queries against Entity Framework Core, queries cannot execute arbitrary C# methods or reflection. Instead, EF Core requires pure LINQ Expression Trees (`Expression<Func<TSource, TDest>>`) composed of member bindings and translatable LINQ methods.

AutoMapSharp's `ProjectionExpressionBuilder` translates high-level mapping rules into pure expression trees.

---

## 1. Flattening into SQL JOINs

When a destination property flattens a nested relationship (e.g. `CustomerAddressCity` from `Order.Customer.Address.City`), `ProjectionExpressionBuilder` dynamically constructs chained `MemberExpression` nodes:

```csharp
// Source mapping expression constructed internally:
src => new OrderSummaryDto
{
    Id = src.Id,
    CustomerName = src.Customer.Name,
    CustomerAddressCity = src.Customer.Address.City
}
```

EF Core translates this into SQL `LEFT JOIN` clauses automatically:

```sql
SELECT [o].[Id], [c].[Name] AS [CustomerName], [a].[City] AS [CustomerAddressCity]
FROM [Orders] AS [o]
LEFT JOIN [Customers] AS [c] ON [o].[CustomerId] = [c].[Id]
LEFT JOIN [Addresses] AS [a] ON [c].[AddressId] = [a].[Id]
```

---

## 2. Relational Collection Projections

When projecting related 1-to-N navigation collections (e.g. `Order.Items` $\rightarrow$ `OrderDto.Items`), `ProjectionExpressionBuilder` constructs a nested `.Select(...)` expression:

```csharp
src => new OrderSummaryDto
{
    Id = src.Id,
    Items = src.Items.Select(item => new OrderItemDto
    {
        Id = item.Id,
        ProductName = item.ProductName,
        LineTotal = item.Price * (decimal)item.Qty
    }).ToList()
}
```

In relational databases, EF Core queries the related collection in a single query (or split query) without client-side evaluation warnings.

---

## 3. Supported & Unsupported Constructs in Projections

| Feature | Supported in `ProjectOnto`? | Explanation |
| :--- | :--- | :--- |
| Direct Member Matching | **Yes** | Translates to `SELECT column` |
| Deep Flattening | **Yes** | Translates to `LEFT JOIN` and column selection |
| Nested Collections | **Yes** | Translates to `.Select(...)` subqueries |
| Constant Values (`UseValue`) | **Yes** | Translates to SQL literal or constant |
| Custom Lambda (`MapSource`) | **Yes** (if translatable) | Supported if using translatable math/string methods |
| Custom Resolvers (`MapWith`) | **No** (by design) | Custom resolvers require C# DI and cannot translate to SQL |
| Lifecycle Hooks (`Before/After`) | **No** (by design) | SQL queries project directly to columns |
