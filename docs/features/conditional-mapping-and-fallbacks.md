# Conditionals & Fallbacks (`If`, `Fallback`)

## Overview
AutoMapSharp allows fine-grained control over property assignments using boolean execution conditions (`If`) and default substitutions when source expressions resolve to `null` (`Fallback`).

## AutoMapSharp Usage

```csharp
using AutoMapSharp;

public class CustomerModule : MappingModule
{
    public CustomerModule()
    {
        DefineMap<Customer, CustomerViewModel>()
            // Only map when source satisfies condition
            .ForProperty(dest => dest.DiscountTier,
                opt => opt.If(src => src.TotalSpend > 1000m))

            // Only map when both source and destination satisfy condition
            .ForProperty(dest => dest.LoyaltyPoints,
                opt => opt.If((src, dest) => src.IsActive && dest.CanEarnPoints))

            // Provide default fallback value if source property is null
            .ForProperty(dest => dest.MiddleName,
                opt => opt.Fallback("N/A"))

            .ForProperty(dest => dest.Bio,
                opt => opt.Fallback("No biography provided."));
    }
}
```

### Preserving Existing Values on In-Place Update
When updating existing destination instances, if `If(...)` evaluates to `false`, the destination property is completely bypassed and retains its existing value:

```csharp
var existingCustomer = new CustomerViewModel { DiscountTier = "Platinum" };
var update = new Customer { TotalSpend = 50m }; // Less than 1000m -> condition fails

engine.Map(update, existingCustomer);
// existingCustomer.DiscountTier is STILL "Platinum"!
```

## Migrating from AutoMapper

| AutoMapper Syntax | AutoMapSharp Syntax | Migration Notes |
|---|---|---|
| `opt.Condition(src => ...)` | `opt.If(src => ...)` | Concise, idiomatic naming |
| `opt.Condition((src, dest) => ...)` | `opt.If((src, dest) => ...)` | Full access to source and target |
| `opt.NullSubstitute("Default")` | `opt.Fallback("Default")` | Idiomatic naming for null coalescing |

## Performance & Null Safety
- Conditions short-circuit before executing expensive downstream conversions.
- Fallbacks work cleanly with nullable and non-nullable destination types.

## Conditions that see the value

`If` and `PreCondition` with the source, or the source and destination, run before the value is read. A false condition also skips a resolver.

Two more overloads run after the value is resolved and converted, and receive it:

```csharp
DefineMap<Person, PersonDto>()
    // Only assign names longer than one character.
    .ForProperty(d => d.Name, o => o.If((src, dest, value) => value.Length > 1))
    // Only raise the age, never lower it.
    .ForProperty(d => d.Age, o => o.If((src, dest, value, current) => value > current));
```

| AutoMapper | AutoMapSharp |
|---|---|
| `PreCondition(src => ...)` | `PreCondition(src => ...)` or `If(src => ...)` |
| `Condition((src, dest, srcMember) => ...)` | `If((src, dest, value) => ...)` |
| `Condition((src, dest, srcMember, destMember) => ...)` | `If((src, dest, value, current) => ...)` |

