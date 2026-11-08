# Parameterized Database Projections

A projection parameter lets one `MapSource` expression use a value that changes per query, such as the current user ID or a minimum quantity. EF Core sends the value as a SQL parameter, so the query plan is reused and the value never becomes part of the SQL text.

---

## 1. Declare the parameter in the module

Declare a local in the module constructor and capture it in `MapSource`. The value you give it here is only a placeholder.

```csharp
public class OrderModule : MappingModule
{
    public OrderModule()
    {
        int minQuantity = 0;

        DefineMap<Order, BulkOrderSummaryDto>()
            .ForProperty(d => d.BulkLineCount,
                opt => opt.MapSource(s => s.Items.Count(i => i.Quantity >= minQuantity)));
    }
}
```

## 2. Pass the value with each query

Pass an anonymous object, or an `IDictionary<string, object?>`, whose member names match the captured locals. Names are matched ignoring case.

```csharp
var query = dbContext.Orders
    .ProjectOnto<BulkOrderSummaryDto>(engine, new { minQuantity = 2 });
```

EF Core generates SQL like this. The value is a parameter:

```sql
.param set @__Value_0 2

SELECT "o"."Id", "o"."OrderNumber", (
    SELECT COUNT(*)
    FROM "OrderItems" AS "o0"
    WHERE "o"."Id" = "o0"."OrderId" AND "o0"."Quantity" >= @__Value_0) AS "BulkLineCount"
FROM "Orders" AS "o"
```

This is the output of `ToQueryString()` for SQLite.

The SQL parameter is named `@__Value_0` rather than after your local. This only affects how the SQL reads in logs.

## 3. How it works

* The projection expression is built once per engine and type pair, then cached. Parameter values are never stored in the cache.
* On each call, only member accesses on the captured closure object are rebound to the new value. Member accesses that start from a lambda parameter, such as `i.Quantity` or `s.UserId`, are never touched, whatever their name.
* Each engine has its own projection cache, so two engines with different configuration never share a projection.
* A parameter you don't pass keeps its placeholder value. A value that cannot be converted to the local's type throws an `ArgumentException` that names the parameter.

## 4. Filtering without a parameter

When the value only filters rows, you can apply it with `Where` before `ProjectOnto` instead. EF Core parameterizes that value too:

```csharp
var query = dbContext.Orders
    .Where(o => o.TotalAmount >= minTotal)
    .ProjectOnto<OrderProjectionDto>(engine);
```

Use a projection parameter when the value is needed inside a mapped member, as in the example above.

---

## 5. AutoMapper Migration Guide

| AutoMapper | AutoMapSharp | Notes |
| :--- | :--- | :--- |
| Captured local in `MapFrom` inside the profile | Captured local in `MapSource` inside the module | Same pattern |
| `ProjectTo<Dto>(cfg, new { p = 1 })` | `ProjectOnto<Dto>(engine, new { p = 1 })` | Anonymous object |
| `ProjectTo<Dto>(cfg, paramDict)` | `ProjectOnto<Dto>(engine, paramDict)` | `IDictionary<string, object?>` |

## 6. See it running

The sample web app has a **Safe Parameterized Projections** page under **Reliability & Parity**. It runs the same projection with two values in one request and shows the SQL EF Core generated.
