# ForPath & Unflattening

`ForPath` configures a nested destination member, such as `Customer.Name`. `ReverseMap` uses the same mechanism to unflatten a flat DTO back into a nested object.

## 1. ForPath

```csharp
DefineMap<OrderForm, Order>()
    .ForProperty(d => d.Customer, o => o.Skip())
    .ForPath(d => d.Customer.Name, o => o.MapSource(s => s.CustomerName))
    .ForPath(d => d.Customer.Email, o => o.Fallback("unknown@example.com").MapSource(s => s.CustomerEmail))
    .ForPath(d => d.Customer.Address.City, o => o.If(s => s.HasAddress).MapSource(s => s.City));
```

The options are `MapSource`, `UseValue`, `If`, `Fallback` and `Skip`.

* A missing intermediate object, such as `Customer` above, is created with its parameterless constructor.
* An existing intermediate object is kept and updated, so mapping onto a tracked entity does not replace it.
* When an `If` condition is false, nothing on the path is created or changed.
* Every intermediate member needs a getter and a setter and must be a class with a parameterless constructor. The last member needs a setter. Otherwise the engine throws an `AutoMapSharpConfigurationException` that names the path.
* Path rules run after the top-level members, so they win over a value mapped into the same nested object.

`ForProperty` only accepts top-level members. `ForProperty(d => d.Customer.Name, ...)` throws an `AutoMapSharpConfigurationException` that points to `ForPath`.

## 2. Unflattening with ReverseMap

```csharp
DefineMap<Order, OrderSummary>().ReverseMap();
```

| `OrderSummary` member | Forward: read from | Reverse: written to |
|---|---|---|
| `CustomerName` | `Order.Customer.Name` | `Order.Customer.Name` |
| `CustomerAddressCity` | `Order.Customer.Address.City` | `Order.Customer.Address.City` |

The reverse map adds an implicit path rule for every source member that has no destination member of the same name and that matches a nested destination path by convention. Explicit `MapSource(s => s.Customer.Name)` rules on the forward map are inverted the same way.

## 3. AutoMapper Migration Guide

| AutoMapper | AutoMapSharp |
|---|---|
| `ForPath(d => d.Customer.Name, o => o.MapFrom(s => s.CustomerName))` | `ForPath(d => d.Customer.Name, o => o.MapSource(s => s.CustomerName))` |
| `ReverseMap()` unflattening | `ReverseMap()` unflattening |
| `ForMember(d => d.Customer.Name, ...)` throws | `ForProperty(d => d.Customer.Name, ...)` throws and points to `ForPath` |

## 4. See it running

The sample web app has a **ReverseMap Unflattening** page under **Reliability & Parity**, with an edit form that maps back into the nested entity.
