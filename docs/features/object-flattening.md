# Deep Object Flattening

## Overview
AutoMapSharp automatically resolves nested object models and getter methods into flat destination properties. It traverses multi-level object graphs and produces null-safe evaluations to prevent `NullReferenceException` when intermediate objects in the hierarchy are null.

## AutoMapSharp Usage

Given a nested domain model:
```csharp
public class Order
{
    public int Id { get; set; }
    public Customer? Customer { get; set; }
    public string GetSpecialNotes() => "Confidential";
}

public class Customer
{
    public string Name { get; set; } = string.Empty;
    public Address? Address { get; set; }
}

public class Address
{
    public string City { get; set; } = string.Empty;
}
```

Mapping into a flat DTO:
```csharp
public class OrderSummaryDto
{
    public int Id { get; set; }
    public string? CustomerName { get; set; }          // Maps to Order.Customer.Name
    public string? CustomerAddressCity { get; set; }  // Maps to Order.Customer.Address.City
    public string? SpecialNotes { get; set; }         // Maps to Order.GetSpecialNotes()
}

public class OrderModule : MappingModule
{
    public OrderModule()
    {
        DefineMap<Order, OrderSummaryDto>();
    }
}
```

## Null-Safety Guarantees
If `order.Customer` or `order.Customer.Address` is `null`, AutoMapSharp safely short-circuits and assigns `null` (or the default value for value types) to `CustomerAddressCity` without throwing a `NullReferenceException`.

## Which source methods are used

A parameterless instance method that returns a value can act as a source member. Its name is matched with or without the `Get` prefix, so `GetSpecialNotes()` maps to `SpecialNotes`.

These methods are never used:

* Methods declared on `object`, including overrides such as a record's `GetHashCode()` and `ToString()`. A destination property named `Type`, `HashCode` or `String` is therefore never filled from `GetType()`, `GetHashCode()` or `ToString()`.
* Static, generic, special-name and compiler-generated methods.

## Migrating from AutoMapper

| AutoMapper Pattern | AutoMapSharp Pattern |
|---|---|
| Automatic PascalCase flattening | Automatic PascalCase flattening |
| Recognizes `GetX()` getter methods | Recognizes `GetX()` getter methods |
| Explicit `ForMember(d => d.City, opt => opt.MapFrom(s => s.Customer.Address.City))` | Not required; convention resolves automatically |
