# Explicit Expansion for Navigation Properties

Relational database models frequently include large or costly navigation collections (such as audit logs, order line items, or binary attachments). Projecting these relationships eagerly on every query can cause significant database performance degradation.

AutoMapSharp provides `.ExplicitExpansion()` to prevent eager projection of navigation properties unless explicitly requested by the caller.

---

## 1. Configuring Explicit Expansion

Mark heavy navigation properties with `.ExplicitExpansion()` in your mapping module:

```csharp
using AutoMapSharp;

public class OrderProjectionModule : MappingModule
{
    public OrderProjectionModule()
    {
        DefineMap<Order, OrderSummaryDto>()
            // Only projected when caller explicitly requests "AuditLogs"
            .ForProperty(d => d.AuditLogs, opt => opt.ExplicitExpansion())
            // Only projected when caller explicitly requests "OrderItems"
            .ForProperty(d => d.OrderItems, opt => opt.ExplicitExpansion());
    }
}
```

---

## 2. Querying with and without Explicit Expansion

### Default Query (Omitted):
```csharp
// AuditLogs and OrderItems are omitted from the projection:
// SQL executes without JOINing AuditLogs or OrderItems tables!
var orders = await dbContext.Orders
    .ProjectOnto<OrderSummaryDto>(engine)
    .ToListAsync();

// orders[0].AuditLogs is null
// orders[0].OrderItems is null
```

### Expanded Query (Explicitly Requested):
```csharp
// Pass the property name(s) to include:
var orders = await dbContext.Orders
    .ProjectOnto<OrderSummaryDto>(engine, membersToExpand: nameof(OrderSummaryDto.OrderItems))
    .ToListAsync();

// orders[0].OrderItems is populated
// orders[0].AuditLogs remains null
```

---

### Expanding with expressions

Pass expressions instead of names to keep refactoring safe. Nested members use a member chain:

```csharp
var query = dbContext.Orders.ProjectOnto<OrderDto>(engine, null, d => d.Items, d => d.Customer.Address);
```

The expression overload requires at least one expression. It is the same as passing `"Items"` and `"Customer.Address"`.

## 3. AutoMapper Migration Guide

| AutoMapper Syntax | AutoMapSharp Equivalent | Notes |
| :--- | :--- | :--- |
| `opt.ExplicitExpansion()` | `opt.ExplicitExpansion()` | Same fluent method name |
| `ProjectTo<Dto>(cfg, null, "Items")` | `ProjectOnto<Dto>(engine, null, "Items")` | Expands named member paths |
| Omits SQL JOINs | Omits SQL JOINs | Zero query overhead when not requested |
| `ProjectTo<OrderDto>(config, null, d => d.Items)` | `ProjectOnto<OrderDto>(engine, null, d => d.Items)` | Expression-based expansion |
